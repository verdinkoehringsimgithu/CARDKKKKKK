using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CardKKK.IslandSurvival.Core;
using UnityEngine;

namespace CardKKK.IslandSurvival.Unity
{
    internal sealed class VoyageSaveStore
    {
        private const int MaximumBytes = 8 * 1024 * 1024;
        private readonly string path, fingerprint, legacyFingerprint;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        [Serializable]
        private sealed class Envelope
        {
            public int Version = 1;
            public string Rules, Payload, Sha256;
        }

        public VoyageSaveStore(string path, VoyageDefinition definition)
        {
            this.path = path;
            VoyageDefinition rules = JsonUtility.FromJson<VoyageDefinition>(JsonUtility.ToJson(definition));
            foreach (VoyageCardRule card in rules.Cards) { card.Name = card.Description = ""; card.SortRank = 0; }
            foreach (VoyageRecipeRule recipe in rules.Recipes) recipe.Name = "";
            string serialized = JsonUtility.ToJson(rules);
            fingerprint = Hash(serialized);
            // Exact v1 gameplay fingerprint, before desktop ownership/interaction settings were added.
            string legacy = Regex.Replace(serialized, "\"Desk\":\\{[^{}]*\\},", "");
            legacy = Regex.Replace(legacy, ",\"Family\":\"(?:[^\"\\\\]|\\\\.)*\"", "");
            legacyFingerprint = Hash(legacy);
        }

        public bool Exists => File.Exists(path) || File.Exists(path + ".bak");

        public bool Save(VoyageState state, out string message)
        {
            message = "";
            string temporary = path + ".tmp";
            try
            {
                string payload = JsonUtility.ToJson(state);
                byte[] bytes = Utf8.GetBytes(JsonUtility.ToJson(new Envelope { Rules = fingerprint, Payload = payload, Sha256 = Hash(payload) }));
                if (bytes.Length > MaximumBytes)
                {
                    message = "出航存档已超过大小限制。";
                    return false;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    string backup = TryRead(path, out _, out _) ? path + ".bak" : path + ".保留-" + Guid.NewGuid().ToString("N");
                    File.Replace(temporary, path, backup);
                }
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception exception) when (FileFailure(exception))
            {
                Debug.LogException(exception);
                message = "无法保存出航进度，请检查存档目录。";
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception exception) when (FileFailure(exception)) { Debug.LogWarning(exception.Message); }
            }
        }

        public bool Load(VoyageEngine engine, out string message)
        {
            if (TryRestore(path, engine, out string primary))
            {
                engine.SetPaused(true);
                message = "";
                return true;
            }
            if (TryRestore(path + ".bak", engine, out string backup))
            {
                engine.SetPaused(true);
                message = "已恢复前一份出航进度，时间暂停。";
                return true;
            }
            message = "读取出航进度失败：" + (File.Exists(path) ? primary : backup);
            return false;
        }

        private bool TryRestore(string candidate, VoyageEngine engine, out string message)
        {
            if (!TryRead(candidate, out VoyageState state, out message)) return false;
            VoyageResult restored = engine.Restore(state);
            message = restored.Message;
            return restored.Success;
        }

        private bool TryRead(string candidate, out VoyageState state, out string message)
        {
            state = null;
            message = "没有可用的存档。";
            try
            {
                var info = new FileInfo(candidate);
                if (!info.Exists) return false;
                if (info.Length <= 0 || info.Length > MaximumBytes) { message = "存档大小异常。"; return false; }
                Envelope envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(candidate, Utf8));
                if (envelope == null || envelope.Version != 1 || envelope.Rules != fingerprint && envelope.Rules != legacyFingerprint)
                { message = "存档对应其他版本的出航规则。"; return false; }
                if (string.IsNullOrEmpty(envelope.Payload) || envelope.Sha256 != Hash(envelope.Payload))
                { message = "存档内容损坏。"; return false; }
                state = JsonUtility.FromJson<VoyageState>(envelope.Payload);
                if (state != null && state.DeskVersion != 0 && envelope.Rules != fingerprint)
                { state = null; message = "牌桌规则与此存档不一致。"; return false; }
                return state != null;
            }
            catch (Exception exception) when (FileFailure(exception))
            {
                Debug.LogWarning(exception.Message);
                message = "存档无法读取。";
                return false;
            }
        }

        private static bool FileFailure(Exception exception) => exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is NotSupportedException || exception is System.Security.SecurityException;

        private static string Hash(string value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(Utf8.GetBytes(value))).Replace("-", "");
        }
    }
}
