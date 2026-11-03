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
                throw new ArgumentException("Save path is required.", nameof(path));
            this.path = Path.GetFullPath(path);
        }

        public OperationResult Save(BoardEngine engine)
        {
            if (engine == null)
                return OperationResult.Fail("The board is not available.");
            if (engine.IsDragging)
                return OperationResult.Fail("Finish or cancel the drag before saving.");

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
                    return OperationResult.Fail("The save exceeds the supported file size.");

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
                return OperationResult.Fail("Save failed: " + exception.Message);
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
                return OperationResult.Fail("The board is not available.");
            if (engine.IsDragging)
                return OperationResult.Fail("Finish or cancel the drag before loading.");

            string primaryError;
            if (TryRead(path, out BoardSnapshot snapshot, out primaryError))
            {
                OperationResult restored = engine.Restore(snapshot);
                if (restored.Success)
                    return restored;
                primaryError = restored.Error;
            }

            if (TryRead(path + ".bak", out BoardSnapshot backup, out string backupError))
            {
                OperationResult restored = engine.Restore(backup);
                if (restored.Success)
                {
                    LoadedFromBackup = true;
                    return restored;
                }
                backupError = restored.Error;
            }
            return OperationResult.Fail("Load failed. Primary: " + primaryError + ". Backup: " + backupError);
        }

        private static bool TryRead(string filename, out BoardSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = "File does not exist";
            try
            {
                var info = new FileInfo(filename);
                if (!info.Exists)
                    return false;
                if (info.Length == 0 || info.Length > MaximumFileBytes)
                {
                    error = "File size is invalid";
                    return false;
                }
                string json;
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Check again after opening, before allocating for the input.
                    if (stream.Length == 0 || stream.Length > MaximumFileBytes)
                    {
                        error = "File size is invalid";
                        return false;
                    }
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                        json = reader.ReadToEnd();
                }
                Document document = JsonUtility.FromJson<Document>(json);
                if (document == null || document.Format != Format || document.Version != CurrentVersion)
                {
                    error = "Unsupported save format or version";
                    return false;
                }
                if (string.IsNullOrEmpty(document.Payload) || document.Sha256 != Hash(document.Payload))
                {
                    error = "Save integrity check failed";
                    return false;
                }
                snapshot = JsonUtility.FromJson<BoardSnapshot>(document.Payload);
                if (snapshot == null)
                {
                    error = "Save contains no board";
                    return false;
                }
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (IsFileOrDataError(exception))
            {
                error = exception.Message;
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
