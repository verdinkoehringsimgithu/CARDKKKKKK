using System;
using System.Collections.Generic;

namespace CardKKK.RecipeEditor.Core
{
    /// <summary>
    /// 合成场所的类型来源
    /// </summary>
    public enum LocationSourceType
    {
        /// <summary>
        /// 独立/全局场所（例如：徒手/随身、遗迹工坊、海滩等）
        /// </summary>
        Custom = 0,

        /// <summary>
        /// 物品作为合成场所（例如：制作出的篝火、土窑、工作台、大锅等实体卡牌/物品作为工位）
        /// </summary>
        Item = 1
    }

    /// <summary>
    /// 合成场所定义（配方的发生地）。
    /// 支持独立场所，也支持直接添加游戏物品作为合成场所。
    /// </summary>
    [Serializable]
    public sealed class RecipeLocation
    {
        public string Id;
        public string Name;
        public string Description = "";

        /// <summary>
        /// 场所来源类型（独立场所 或 物品作为场所）
        /// </summary>
        public LocationSourceType SourceType = LocationSourceType.Custom;

        /// <summary>
        /// 当 SourceType 为 Item 时，绑定的物品 ID
        /// </summary>
        public string BoundItemId = "";

        /// <summary>
        /// 场所容量（可同时进行的合成工位数）
        /// </summary>
        public int Capacity = 1;

        /// <summary>
        /// 单次作为合成设施/工位使用时的磨损或耐久损耗（默认 0，表示暂不设损耗）。
        /// 工位设施本质具备耐久损耗/燃料消耗属性。
        /// </summary>
        public int DurabilityCost = 0;

        /// <summary>
        /// 场所环境/特性标签列表（例如 高温、通风、露天、水源等）
        /// </summary>
        public List<string> FeatureTags = new List<string>();

        public RecipeLocation() { }

        public RecipeLocation(string id, string name, string description = "", int capacity = 1, int durabilityCost = 0)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            SourceType = LocationSourceType.Custom;
            BoundItemId = "";
            Capacity = capacity > 0 ? capacity : 1;
            DurabilityCost = Math.Max(0, durabilityCost);
        }

        public RecipeLocation(string id, string name, string boundItemId, string description = "", int capacity = 1, int durabilityCost = 0)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            SourceType = LocationSourceType.Item;
            BoundItemId = boundItemId ?? string.Empty;
            Capacity = capacity > 0 ? capacity : 1;
            DurabilityCost = Math.Max(0, durabilityCost);
        }

        public RecipeLocation Clone()
        {
            return new RecipeLocation
            {
                Id = Id,
                Name = Name,
                Description = Description,
                SourceType = SourceType,
                BoundItemId = BoundItemId,
                Capacity = Capacity,
                DurabilityCost = DurabilityCost,
                FeatureTags = new List<string>(FeatureTags ?? new List<string>())
            };
        }

        public override string ToString()
        {
            string typePrefix = SourceType == LocationSourceType.Item ? "[物品工位] " : "[独立工位] ";
            return string.IsNullOrEmpty(Name) ? $"{typePrefix}{Id}" : $"{typePrefix}{Name} ({Id})";
        }
    }
}
