using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CardKKK.Tabletop.Core;
using UnityEngine;

namespace CardKKK.Tabletop.Unity
{
    /// <summary>Versioned local saves. A failed write preserves the last completed save.</summary>
    public sealed class BoardSaveStore
    {
        private const string Format = "CardKKK.Tabletop";
        private const int CurrentVersion = 1;
        private const long MaximumFileBytes = 10 * 1024 * 1024;
        private readonly string path;

        public bool LoadedFromBackup { get; private set; }

        [Serializable]
        private sealed class Document
        {
            public string Format;
            public int Version;
            public string Payload;
            public string Sha256;
        }

        public BoardSaveStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("请提供存档路径。", nameof(path));
            this.path = Path.GetFullPath(path);
        }

        public OperationResult Save(BoardEngine engine)
        {
            if (engine == null)
                return OperationResult.Fail("桌面尚未准备好，请稍后重试。");
            if (engine.IsDragging)
                return OperationResult.Fail("请先放下卡牌，或按 Esc 取消拖动，再保存。");

            string temporary = path + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                string payload = JsonUtility.ToJson(engine.Capture());
                var document = new Document
                {
                    Format = Format,
                    Version = CurrentVersion,
                    Payload = payload,
                    Sha256 = Hash(payload)
                };
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(document, true));
                if (bytes.LongLength > MaximumFileBytes)
                    return OperationResult.Fail("存档超过 10 MB，请减少桌面数据后再保存。");

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    // A matching checksum alone does not establish a valid board.
                    // Preserve a good backup if the current primary violates domain rules.
                    string backup = null;
                    if (TryRead(path, out BoardSnapshot previous, out _))
                    {
                        var validator = new BoardEngine(engine.Rules);
                        if (validator.Restore(previous).Success)
                            backup = path + ".bak";
                    }
                    File.Replace(temporary, path, backup, true);
                }
                else
                {
                    File.Move(temporary, path);
                }
                return OperationResult.Ok();
            }
            catch (Exception exception) when (IsFileOrDataError(exception))
            {
                return OperationResult.Fail("保存失败。请检查存档目录的写入权限和可用磁盘空间。");
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public OperationResult Load(BoardEngine engine)
        {
            LoadedFromBackup = false;
            if (engine == null)
                return OperationResult.Fail("桌面尚未准备好，请稍后重试。");
            if (engine.IsDragging)
                return OperationResult.Fail("请先放下卡牌，或按 Esc 取消拖动，再读取。");

            string primaryError;
            if (TryRead(path, out BoardSnapshot snapshot, out primaryError))
            {
                OperationResult restored = engine.Restore(snapshot);
                if (restored.Success)
                    return restored;
                primaryError = "桌面数据不符合当前规则";
            }

            if (TryRead(path + ".bak", out BoardSnapshot backup, out string backupError))
            {
                OperationResult restored = engine.Restore(backup);
                if (restored.Success)
                {
                    LoadedFromBackup = true;
                    return restored;
                }
                backupError = "桌面数据不符合当前规则";
            }
            return OperationResult.Fail("读取失败。主存档：" + primaryError + "。备份：" + backupError + "。请先保存一次，或检查存档文件。");
        }

        private static bool TryRead(string filename, out BoardSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = "未找到存档文件";
            try
            {
                var info = new FileInfo(filename);
                if (!info.Exists)
                    return false;
                if (info.Length == 0 || info.Length > MaximumFileBytes)
                {
                    error = "文件为空或超过 10 MB";
                    return false;
                }
                string json;
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Check again after opening, before allocating for the input.
                    if (stream.Length == 0 || stream.Length > MaximumFileBytes)
                    {
                        error = "文件为空或超过 10 MB";
                        return false;
                    }
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                        json = reader.ReadToEnd();
                }
                Document document = JsonUtility.FromJson<Document>(json);
                if (document == null || document.Format != Format || document.Version != CurrentVersion)
                {
                    error = "存档格式或版本不兼容";
                    return false;
                }
                if (string.IsNullOrEmpty(document.Payload) || document.Sha256 != Hash(document.Payload))
                {
                    error = "文件完整性检查失败，内容可能已损坏";
                    return false;
                }
                snapshot = JsonUtility.FromJson<BoardSnapshot>(document.Payload);
                if (snapshot == null)
                {
                    error = "存档中没有桌面数据";
                    return false;
                }
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (IsFileOrDataError(exception))
            {
                error = "无法读取文件，请检查读取权限及文件是否完整";
                return false;
            }
        }

        private static string Hash(string value)
        {
            using (var hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static bool IsFileOrDataError(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is NotSupportedException ||
                exception is System.Security.SecurityException;
        }
    }
}
