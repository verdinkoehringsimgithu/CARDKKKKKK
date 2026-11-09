using System;
using System.IO;
using System.Text;
using CardKKK.RecipeEditor.Core;
using UnityEngine;

namespace CardKKK.RecipeEditor.Runtime
{
    /// <summary>
    /// 配方数据 JSON 序列化器与文件导入导出助手。
    /// 支持美化排版导出、文件读写、编码处理及异常捕获。
    /// </summary>
    public static class RecipeJsonSerializer
    {
        public static string ToJson(RecipeDatabaseData data, bool prettyPrint = true)
        {
            if (data == null) return "{}";
            return JsonUtility.ToJson(data, prettyPrint);
        }

        public static RecipeDatabaseData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new RecipeDatabaseData();
            return JsonUtility.FromJson<RecipeDatabaseData>(json);
        }

        public static bool ExportToFile(RecipeDatabaseData data, string filePath, out string error)
        {
            error = string.Empty;
            if (data == null)
            {
                error = "无法导出空的配方数据。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "导出文件路径无效。";
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = ToJson(data, prettyPrint: true);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                error = $"写入文件失败: {ex.Message}";
                return false;
            }
        }

        public static RecipeDatabaseData ImportFromFile(string filePath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                error = $"指定的文件不存在: {filePath}";
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                var data = FromJson(json);
                if (data == null)
                {
                    error = "JSON 解析结果为空或数据格式不匹配。";
                    return null;
                }
                return data;
            }
            catch (Exception ex)
            {
                error = $"读取或解析文件失败: {ex.Message}";
                return null;
            }
        }
    }
}
