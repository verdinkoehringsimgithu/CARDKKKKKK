using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CardKKK.Spellcraft.Core;
using UnityEngine;

namespace CardKKK.Spellcraft.Unity
{
    /// <summary>规则指纹与独立载荷；不读写牌桌或旧工坊存档。</summary>
    public sealed class ForgeSaveStore
    {
        private const int MaxBytes = 2 * 1024 * 1024;
        private readonly string path;
        private readonly string fingerprint;
        private readonly ForgeDefinition definition;
        private readonly HashSet<string> legacyFingerprints = new HashSet<string>(StringComparer.Ordinal);
        [Serializable] private sealed class Envelope
        {
            public int Version = 2;
            public string Rules, Payload, Sha256;
        }

        public ForgeSaveStore(string filePath, ForgeDefinition rules, ForgeDefinition previousPresentation = null)
        {
            path = filePath;
            string json = JsonUtility.ToJson(rules);
            definition = JsonUtility.FromJson<ForgeDefinition>(json);
            fingerprint = RuleFingerprint(definition);
            legacyFingerprints.Add(Hash(json));
            if (previousPresentation != null && RuleFingerprint(previousPresentation) == fingerprint)
                legacyFingerprints.Add(Hash(JsonUtility.ToJson(previousPresentation)));
        }
        public bool HasSave => File.Exists(path) || File.Exists(path + ".bak");

        public bool Save(ForgeState state, out string error)
        {
            error = "";
            string temporary = path + ".tmp";
            try
            {
                if (!IsValid(state)) { error = "当前状态无法保存。"; return false; }
                string payload = JsonUtility.ToJson(state);
                var envelope = new Envelope { Rules = fingerprint, Payload = payload, Sha256 = Hash(payload) };
                byte[] data = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(envelope));
                if (data.Length > MaxBytes) throw new IOException("存档过大。");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                { stream.Write(data, 0, data.Length); stream.Flush(true); }
                if (File.Exists(path))
                {
                    bool validOld = TryRead(path, out ForgeState old, out _) && IsValid(old);
                    File.Replace(temporary, path, validOld ? path + ".bak" : null);
                }
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception exception) when (IsFileFailure(exception))
            { Debug.LogException(exception); error = "保存失败，请检查存档目录是否可写。"; return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        public bool Load(ForgeEngine engine, out string message)
        {
            message = "";
            if (TryRead(path, out ForgeState primary, out string primaryError))
            {
                ForgeResult result = engine.Restore(primary);
                if (result.Success) return true;
                primaryError = result.Error;
            }
            if (TryRead(path + ".bak", out ForgeState backup, out string backupError))
            {
                ForgeResult result = engine.Restore(backup);
                if (result.Success) { message = "已恢复法术工坊备份。"; return true; }
                backupError = result.Error;
            }
            message = "读取失败：" + (string.IsNullOrEmpty(primaryError) ? backupError : primaryError);
            return false;
        }

        private bool TryRead(string candidate, out ForgeState state, out string error)
        {
            state = null; error = "";
            try
            {
                var file = new FileInfo(candidate);
                if (!file.Exists) { error = "没有可用存档。"; return false; }
                if (file.Length <= 0 || file.Length > MaxBytes) { error = "存档大小异常。"; return false; }
                Envelope envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(candidate, Encoding.UTF8));
                bool matches = envelope != null && (envelope.Version == 2 ? envelope.Rules == fingerprint :
                    envelope.Version == 1 && legacyFingerprints.Contains(envelope.Rules));
                if (!matches)
                { error = "存档与当前规则不一致。"; return false; }
                if (string.IsNullOrEmpty(envelope.Payload) || envelope.Sha256 != Hash(envelope.Payload))
                { error = "存档内容不完整。"; return false; }
                state = JsonUtility.FromJson<ForgeState>(envelope.Payload);
                if (state == null) { error = "存档没有角色状态。"; return false; }
                return true;
            }
            catch (Exception exception) when (IsFileFailure(exception))
            { Debug.LogException(exception); error = "存档无法读取。"; return false; }
        }
        private bool IsValid(ForgeState state)
        {
            if (state == null) return false;
            var temporary = new ForgeEngine(definition, state.Seed);
            return temporary.Restore(state).Success;
        }
        private static bool IsFileFailure(Exception e) => e is IOException || e is UnauthorizedAccessException ||
            e is ArgumentException || e is NotSupportedException || e is System.Security.SecurityException;
        private static string RuleFingerprint(ForgeDefinition source)
        {
            ForgeDefinition rules = JsonUtility.FromJson<ForgeDefinition>(JsonUtility.ToJson(source));
            foreach (ForgeModule item in rules.Modules) { item.Name = ""; item.Description = ""; item.SpriteIndex = 0; }
            foreach (ForgeCard item in rules.Cards) { item.Name = ""; item.Description = ""; item.SpriteIndex = 0; }
            foreach (ForgeEncounter encounter in rules.Encounters)
            {
                encounter.Name = encounter.Description = "";
                foreach (ForgeEnemyTemplate enemy in encounter.Enemies)
                {
                    enemy.Name = enemy.Description = ""; enemy.SpriteIndex = 0;
                    foreach (ForgeIntent intent in enemy.Intents) intent.Name = intent.Description = "";
                }
            }
            return Hash(JsonUtility.ToJson(rules));
        }
        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
    }
}
