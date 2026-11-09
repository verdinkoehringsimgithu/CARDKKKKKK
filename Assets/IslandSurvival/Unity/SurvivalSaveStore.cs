using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CardKKK.IslandSurvival.Core;
using UnityEngine;

namespace CardKKK.IslandSurvival.Unity
{
    /// <summary>独立载荷，校验规则、摘要和核心不变量后读取；主档损坏时尝试备份。</summary>
    public sealed class SurvivalSaveStore
    {
        private const int MaximumBytes = 8 * 1024 * 1024;
        private readonly string path, fingerprint;
        private readonly SurvivalDefinition rules;
        [Serializable] private sealed class Envelope { public int Version = 1; public string Rules, Payload, Sha256; }
        public SurvivalSaveStore(string filePath, SurvivalDefinition definition)
        {
            path = filePath;
            rules = JsonUtility.FromJson<SurvivalDefinition>(JsonUtility.ToJson(definition));
            SurvivalDefinition numerical = JsonUtility.FromJson<SurvivalDefinition>(JsonUtility.ToJson(definition));
            numerical.TemporaryBalance = false;
            foreach (CardRule card in numerical.Cards) { card.Name = card.Description = card.Family = ""; card.SortRank = 0; }
            foreach (RecipeRule recipe in numerical.Recipes) recipe.Name = recipe.Description = "";
            foreach (AfflictionRule rule in numerical.Afflictions) rule.Name = rule.Description = "";
            foreach (TraitRule trait in numerical.Traits) trait.Name = "";
            fingerprint = Hash(JsonUtility.ToJson(numerical));
        }
        public bool HasSave => File.Exists(path) || File.Exists(path + ".bak");
        public bool Save(SurvivalState state, out string error)
        {
            error = "";
            string temporary = path + ".tmp";
            try
            {
                var validator = new SurvivalEngine(rules);
                SurvivalResult validation = validator.Restore(state);
                if (!validation.Success) { error = "保存校验失败：" + validation.Error; return false; }
                string payload = JsonUtility.ToJson(state);
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(new Envelope { Rules = fingerprint, Payload = payload, Sha256 = Hash(payload) }));
                if (bytes.Length > MaximumBytes) { error = "存档超过大小限制。"; return false; }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path))
                {
                    bool valid = TryRead(path, out SurvivalState old, out _) && validator.Restore(old).Success;
                    string previous = valid ? state.Dead ? old.Dead ? null : path + ".bak" : old.Dead ? path + ".failed" : path + ".bak" : path + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                    File.Replace(temporary, path, previous);
                }
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception exception) when (FileFailure(exception)) { Debug.LogException(exception); error = "保存失败，请检查存档目录是否可写。"; return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        public bool Load(SurvivalEngine engine, out string message)
        {
            message = "";
            if (TryRead(path, out SurvivalState primary, out string primaryError))
            { SurvivalResult result = engine.Restore(primary); if (result.Success) return true; primaryError = result.Error; }
            if (TryRead(path + ".bak", out SurvivalState backup, out string backupError))
            { SurvivalResult result = engine.Restore(backup); if (result.Success) { message = "已读取孤岛备份，时间暂停。"; return true; } backupError = result.Error; }
            message = "读取失败：" + (string.IsNullOrEmpty(primaryError) ? backupError : primaryError); return false;
        }
        public bool LoadBackup(SurvivalEngine engine, out string message)
        {
            if (TryRead(path + ".bak", out SurvivalState backup, out string error))
            {
                SurvivalResult result = engine.Restore(backup);
                if (result.Success) { message = "已恢复前一份自动备份，时间暂停。"; return true; }
                error = result.Error;
            }
            message = "备份读取失败：" + error; return false;
        }
        private bool TryRead(string candidate, out SurvivalState saved, out string error)
        {
            saved = null; error = "";
            try
            {
                var info = new FileInfo(candidate);
                if (!info.Exists) { error = "没有可用存档。"; return false; }
                if (info.Length <= 0 || info.Length > MaximumBytes) { error = "存档大小异常。"; return false; }
                Envelope envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(candidate, Encoding.UTF8));
                if (envelope == null || envelope.Version != 1 || envelope.Rules != fingerprint) { error = "存档与当前规则不一致，请保留旧档并开始新局。"; return false; }
                if (string.IsNullOrEmpty(envelope.Payload) || envelope.Sha256 != Hash(envelope.Payload)) { error = "存档内容不完整。"; return false; }
                saved = JsonUtility.FromJson<SurvivalState>(envelope.Payload);
                if (saved == null) { error = "存档没有有效状态。"; return false; }
                return true;
            }
            catch (Exception exception) when (FileFailure(exception)) { Debug.LogException(exception); error = "存档无法读取。"; return false; }
        }
        private static bool FileFailure(Exception exception) => exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException || exception is System.Security.SecurityException;
        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
    }
}
