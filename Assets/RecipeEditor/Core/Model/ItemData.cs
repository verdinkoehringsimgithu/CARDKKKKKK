using System;
using System.Collections.Generic;

namespace CardKKK.RecipeEditor.Core
{
    /// <summary>
    /// 自定义属性键值对（例如耐久度、能量值、腐败时间等）
    /// </summary>
    [Serializable]
    public sealed class RecipePropertyEntry
    {
        public string Key;
        public string Value;

        public RecipePropertyEntry() { }

        public RecipePropertyEntry(string key, string value)
        {
            Key = key ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public RecipePropertyEntry Clone()
        {
            return new RecipePropertyEntry(Key, Value);
        }
    }

    /// <summary>
    /// 游戏物品定义。
    /// 支持挂载复用标签池中的标签，支持设置分类与自定义属性。
    /// </summary>
    [Serializable]
    public sealed class RecipeItem
    {
        public string Id;
        public string Name;
        public string Category = "材料";
        public string Description = "";
        public int MaxStack = 99;

        /// <summary>
        /// 绑定的复用标签 ID 列表
        /// </summary>
        public List<string> TagIds = new List<string>();

        /// <summary>
        /// 物品的自定义数值与特性属性
        /// </summary>
        public List<RecipePropertyEntry> Properties = new List<RecipePropertyEntry>();

        /// <summary>
        /// 卡牌携带的默认能力点数与换算率配置（不填即为0点，换算率默认 100%）
        /// </summary>
        public List<ItemAbilityContribution> AbilityContributions = new List<ItemAbilityContribution>();

        public RecipeItem() { }

        public RecipeItem(string id, string name, string category = "材料", string description = "")
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Category = string.IsNullOrEmpty(category) ? "材料" : category;
            Description = description ?? string.Empty;
        }

        public RecipeItem Clone()
        {
            var item = new RecipeItem(Id, Name, Category, Description)
            {
                MaxStack = MaxStack,
                TagIds = new List<string>(TagIds ?? new List<string>()),
                Properties = new List<RecipePropertyEntry>()
            };

            if (Properties != null)
            {
                foreach (var prop in Properties)
                {
                    if (prop != null)
                    {
                        item.Properties.Add(prop.Clone());
                    }
                }
            }

            if (AbilityContributions != null)
            {
                foreach (var ac in AbilityContributions)
                {
                    if (ac != null)
                    {
                        item.AbilityContributions.Add(ac.Clone());
                    }
                }
            }

            return item;
        }

        public bool HasTag(string tagId)
        {
            return TagIds != null && TagIds.Contains(tagId);
        }

        public void AddTag(string tagId)
        {
            if (string.IsNullOrEmpty(tagId)) return;
            if (TagIds == null) TagIds = new List<string>();
            if (!TagIds.Contains(tagId))
            {
                TagIds.Add(tagId);
            }
        }

        public bool RemoveTag(string tagId)
        {
            if (TagIds == null) return false;
            return TagIds.Remove(tagId);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? Id : $"{Name} ({Id})";
        }
    }
}
