using System.Collections.Generic;
using CardKKK.Tabletop.Core;
using UnityEngine;

namespace CardKKK.Tabletop.Unity
{
    /// <summary>设计时的卡牌定义；运行中的实例属性由核心目录深拷贝。</summary>
    [CreateAssetMenu(menuName = "孤岛生存/历史演示/自由牌桌/卡牌定义", fileName = "新卡牌")]
    public sealed class CardDefinition : ScriptableObject
    {
        [InspectorName("定义 ID"), Tooltip("稳定且唯一的技术标识。存档通过它查找卡牌，发布后不要随意改名。")]
        public string Id;
        [InspectorName("卡牌名称")] public string DisplayName;
        [InspectorName("堆叠分类"), Tooltip("选择“相同分类”堆叠规则时，分类相同的牌兼容。")]
        public string Family;
        [InspectorName("排序序号")] public int SortRank;
        [InspectorName("初始实例属性")] public List<CardAttribute> Attributes = new List<CardAttribute>();
        [InspectorName("卡面插画（可留空）")] public Sprite Artwork;
        [InspectorName("旧版定义 ID（迁移）"), Tooltip("仅用于读取旧存档；新牌保持为空。与旧版序号一起匹配旧定义。")]
        public string LegacyDefinitionId;
        [InspectorName("旧版序号（迁移）")] public int LegacyRank;

        public CardDefinitionData ToData()
        {
            return new CardDefinitionData(Id, DisplayName, Family, SortRank, Attributes,
                string.IsNullOrWhiteSpace(LegacyDefinitionId) ? null : LegacyDefinitionId, LegacyRank);
        }
    }
}
