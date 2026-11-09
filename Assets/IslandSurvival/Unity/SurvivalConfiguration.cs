using System;
using System.Collections.Generic;
using CardKKK.IslandSurvival.Core;
using CardKKK.Tabletop.Unity;
using UnityEngine;

namespace CardKKK.IslandSurvival.Unity
{
    [Serializable] public sealed class SurvivalContentNote
    {
        [InspectorName("卡牌")] public CardDefinition Card;
        [InspectorName("已注明环境来源")] public bool EnvironmentalSource;
        [InspectorName("已注明终端用途")] public bool Terminal;
        [InspectorName("理由"),TextArea(2,5)] public string Reason = "";
    }
    [CreateAssetMenu(menuName = "孤岛生存/生存规则", fileName = "孤岛生存规则")]
    public sealed class SurvivalConfiguration : ScriptableObject
    {
        [Tooltip("完整设计1.2。修改资产后重新进入或开始新游戏应用；运行中不改写已加载的规则。")]
        public SurvivalDefinition Rules = new SurvivalDefinition();
        [Tooltip("显示名、分类、排序、初始实例属性、插画的唯一来源。这里仅引用本模块卡牌。")]
        public List<CardDefinition> Cards = new List<CardDefinition>();
        [Tooltip("编辑器内容审查注释，不改变游戏来源、配方或产量。须写理由才能消除缺项警告。")]
        public List<SurvivalContentNote> ContentNotes = new List<SurvivalContentNote>();

        // CardDefinition 的整数默认实例属性；机制曲线和工艺规则仍在 Rules 中配置。
        public const string StackAttribute = "堆叠上限";
        public const string FootprintAttribute = "占地单位";
        public const string DurabilityAttribute = "初始耐久";
        public const string StructureAttribute = "结构耐久";
        public const string HardnessAttribute = "硬度";

        /// <summary>创建与资产无可写共享的核心规则。显示信息和初始实例属性只从卡牌资产桥接。</summary>
        public SurvivalDefinition BuildDefinition()
        {
            if (Rules == null) throw new InvalidOperationException("尚未配置生存规则。");
            SurvivalDefinition definition = JsonUtility.FromJson<SurvivalDefinition>(JsonUtility.ToJson(Rules));
            if (Cards == null) throw new InvalidOperationException("尚未配置卡牌资产目录。");
            var catalog = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
            foreach (CardDefinition card in Cards)
            {
                if (card == null) throw new InvalidOperationException("卡牌目录存在空引用。");
                if (string.IsNullOrWhiteSpace(card.Id)) throw new InvalidOperationException("卡牌资产缺少稳定ID：" + card.name);
                if (catalog.ContainsKey(card.Id)) throw new InvalidOperationException("卡牌目录ID重复：" + card.Id);
                catalog.Add(card.Id, card);
            }
            foreach (CardRule rule in definition.Cards)
            {
                if (!catalog.TryGetValue(rule.Id ?? "", out CardDefinition card))
                    throw new InvalidOperationException("规则缺少对应卡牌资产：" + rule.Id);
                rule.Name = card.DisplayName;
                rule.Family = card.Family;
                rule.SortRank = card.SortRank;
                rule.Kind = ParseKind(card.Family, card.Id);
                rule.StackLimit = Attribute(card, StackAttribute);
                rule.Footprint = Attribute(card, FootprintAttribute);
                rule.InitialDurability = Attribute(card, DurabilityAttribute);
                rule.Structure = Attribute(card, StructureAttribute);
                rule.Hardness = Attribute(card, HardnessAttribute);
            }
            if (catalog.Count != definition.Cards.Count)
                throw new InvalidOperationException("卡牌目录与机制规则数量不同；新增卡牌须同时增加对应ID的机制规则。");
            return definition;
        }

        public CardDefinition FindCard(string id)
        {
            foreach (CardDefinition card in Cards) if (card != null && card.Id == id) return card;
            return null;
        }
        public Sprite FindArtwork(string id)
        {
            CardDefinition card = FindCard(id);
            return card != null ? card.Artwork : null;
        }
        public string CheckConfiguration()
        {
            try { return SurvivalEngine.ValidateDefinition(BuildDefinition()); }
            catch (Exception exception) { return exception.Message; }
        }
        private static int Attribute(CardDefinition card, string key)
        {
            int value = 0;
            bool found = false;
            if (card.Attributes != null) foreach (var attribute in card.Attributes)
            {
                if (attribute == null || attribute.Key != key) continue;
                if (found) throw new InvalidOperationException(card.DisplayName + "的默认属性重复：" + key);
                value = attribute.Value;
                found = true;
            }
            if (!found) throw new InvalidOperationException(card.DisplayName + "缺少默认实例属性：" + key + "。请在卡牌配置中补齐五项初始属性。");
            return value;
        }
        private static CardKind ParseKind(string family, string id)
        {
            switch (family)
            {
                case "材料": return CardKind.Material;
                case "工具": return CardKind.Tool;
                case "设施": return CardKind.Facility;
                case "区域": return CardKind.Area;
                case "生物": return CardKind.Creature;
                case "状态": return CardKind.Condition;
                default: throw new InvalidOperationException("卡牌" + id + "的分类必须为：材料、工具、设施、区域、生物、状态。");
            }
        }
    }
}
