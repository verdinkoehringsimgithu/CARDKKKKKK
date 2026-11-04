using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CardKKK.TestGameplay.Core;
using UnityEngine;

namespace CardKKK.TestGameplay.Unity
{
    /// <summary>测试玩法专用存档。路径与载荷均不复用自由牌桌存档。</summary>
    public sealed class WorkshopSaveStore
    {
        private const int MaxFileBytes = 2 * 1024 * 1024;
        private readonly string path;
        private readonly string definitionHash;
        private readonly WorkshopDefinition definition;

        [Serializable]
        private sealed class Envelope
        {
            public int Version = 1;
            public string DefinitionHash;
            public string Payload;
            public string Sha256;
        }

        public WorkshopSaveStore(string filePath, WorkshopDefinition definition)
        {
            path = filePath ?? throw new ArgumentNullException(nameof(filePath));
            string content = JsonUtility.ToJson(definition);
            this.definition = JsonUtility.FromJson<WorkshopDefinition>(content);
            definitionHash = Hash(content);
        }

        public bool HasSave => File.Exists(path) || File.Exists(path + ".bak");

        public bool Save(WorkshopSnapshot snapshot, out string error)
        {
            error = "";
            string temporary = path + ".tmp";
            try
            {
                string payload = JsonUtility.ToJson(snapshot);
                var envelope = new Envelope
                { DefinitionHash = definitionHash, Payload = payload, Sha256 = Hash(payload) };
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(envelope));
                if (bytes.Length > MaxFileBytes) throw new IOException("测试存档超过大小限制。");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path))
                {
                    // 原文件坏了时保留已有备份，避免用坏档覆盖好的备份。
                    string backup = TryRead(path, out WorkshopSnapshot old, out _) &&
                        IsSemanticallyValid(old) ? path + ".bak" : null;
                    File.Replace(temporary, path, backup);
                }
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException || exception is ArgumentException ||
                exception is NotSupportedException || exception is System.Security.SecurityException)
            { Debug.LogException(exception); error = "保存失败：" + FileError(exception); return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public bool Load(WorkshopEngine engine, out string message)
        {
            message = "";
            string primaryError = "";
            if (TryRead(path, out WorkshopSnapshot primary, out primaryError))
            {
                WorkshopResult result = engine.Restore(primary);
                if (result.Success) return true;
                primaryError = result.Error;
            }
            if (TryRead(path + ".bak", out WorkshopSnapshot backup, out string backupError))
            {
                WorkshopResult result = engine.Restore(backup);
                if (result.Success) { message = "已恢复测试玩法备份。"; return true; }
                backupError = result.Error;
            }
            message = "读取失败：" + (string.IsNullOrEmpty(primaryError) ? backupError : primaryError);
            return false;
        }

        private bool TryRead(string candidatePath, out WorkshopSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = "";
            try
            {
                var info = new FileInfo(candidatePath);
                if (!info.Exists) { error = "没有测试玩法存档。"; return false; }
                if (info.Length <= 0 || info.Length > MaxFileBytes)
                { error = "测试存档大小不合法。"; return false; }
                Envelope envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(candidatePath, Encoding.UTF8));
                if (envelope == null || envelope.Version != 1 || string.IsNullOrEmpty(envelope.Payload) ||
                    envelope.Sha256 != Hash(envelope.Payload))
                { error = "测试存档不完整。"; return false; }
                if (envelope.DefinitionHash != definitionHash)
                { error = "玩法配置已变化，请重新开始测试。"; return false; }
                snapshot = JsonUtility.FromJson<WorkshopSnapshot>(envelope.Payload);
                if (snapshot == null) { error = "测试存档没有游戏状态。"; return false; }
                return true;
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException || exception is ArgumentException ||
                exception is NotSupportedException || exception is System.Security.SecurityException)
            { Debug.LogException(exception); error = FileError(exception); return false; }
        }

        private static string FileError(Exception exception)
        {
            if (exception is UnauthorizedAccessException || exception is System.Security.SecurityException)
                return "没有测试存档的读写权限。";
            if (exception is IOException) return "无法读写测试存档，请检查磁盘空间或文件占用。";
            if (exception is ArgumentException) return "测试存档内容无法识别。";
            return "当前环境不支持这项存档操作。";
        }

        private bool IsSemanticallyValid(WorkshopSnapshot snapshot)
        {
            try
            {
                var candidate = new WorkshopEngine(definition, snapshot.ScenarioId);
                return candidate.Restore(snapshot).Success;
            }
            catch (ArgumentException) { return false; }
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
                var text = new StringBuilder(digest.Length * 2);
                foreach (byte part in digest) text.Append(part.ToString("x2"));
                return text.ToString();
            }
        }
    }
}
