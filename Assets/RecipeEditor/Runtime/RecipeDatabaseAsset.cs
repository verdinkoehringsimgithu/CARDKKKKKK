using System;
using System.Collections.Generic;
using CardKKK.RecipeEditor.Core;
using UnityEngine;

namespace CardKKK.RecipeEditor.Runtime
{
    /// <summary>
    /// 配方数据库资产（ScriptableObject）。
    /// 作为 Unity 资源在工程中保存与引用，提供可视化 Inspector 与运行时加载支持。
    /// </summary>
    [CreateAssetMenu(fileName = "新配方数据库", menuName = "卡牌工坊/配方数据库资产", order = 120)]
    public sealed class RecipeDatabaseAsset : ScriptableObject
    {
        [SerializeField]
        public RecipeDatabaseData Database = new RecipeDatabaseData();

        /// <summary>
        /// 执行配方数据库的完整性静态校验
        /// </summary>
        public List<ValidationMessage> ValidateDatabase()
        {
            return RecipeValidator.Validate(Database);
        }

        /// <summary>
        /// 从外部数据库深拷贝覆盖当前数据
        /// </summary>
        public void LoadFrom(RecipeDatabaseData source)
        {
            if (source == null) return;
            Database = source.Clone();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 导出当前数据库为 JSON 字符串
        /// </summary>
        public string ExportToJson(bool prettyPrint = true)
        {
            return RecipeJsonSerializer.ToJson(Database, prettyPrint);
        }

        /// <summary>
        /// 从 JSON 字符串载入数据
        /// </summary>
        public bool ImportFromJson(string json, out string error)
        {
            error = string.Empty;
            try
            {
                var loaded = RecipeJsonSerializer.FromJson(json);
                if (loaded == null)
                {
                    error = "反序列化数据为空。";
                    return false;
                }
                Database = loaded;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
