using System;

namespace CardKKK.RecipeEditor.Core
{
    /// <summary>
    /// 可复用的标签定义。
    /// 物品通过引用标签 ID 进行归类与多重标记，配方支持按标签进行材料与条件匹配。
    /// </summary>
    [Serializable]
    public sealed class RecipeTag
    {
        public string Id;
        public string Name;
        public string ColorHex = "#3498DB";
        public string Category = "通用";
        public string Description = "";

        public RecipeTag() { }

        public RecipeTag(string id, string name, string colorHex = "#3498DB", string category = "通用", string description = "")
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            ColorHex = string.IsNullOrEmpty(colorHex) ? "#3498DB" : colorHex;
            Category = string.IsNullOrEmpty(category) ? "通用" : category;
            Description = description ?? string.Empty;
        }

        public RecipeTag Clone()
        {
            return new RecipeTag(Id, Name, ColorHex, Category, Description);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? Id : $"{Name} ({Id})";
        }
    }
}
