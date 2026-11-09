using System;
using System.Collections.Generic;
using CardKKK.IslandSurvival.Core;
using CardKKK.Tabletop.Unity;
using UnityEngine;

namespace CardKKK.IslandSurvival.Unity
{
    [CreateAssetMenu(menuName = "孤岛生存/出航规则", fileName = "VoyageRules")]
    public sealed class VoyageConfiguration : ScriptableObject
    {
        [InspectorName("出航规则")] public VoyageDefinition Rules = new VoyageDefinition();
        [InspectorName("卡牌资产")] public List<CardDefinition> Cards = new List<CardDefinition>();

        private static readonly Dictionary<string, VoyageCardKind> Kinds = new Dictionary<string, VoyageCardKind>(StringComparer.Ordinal)
        {
            { "身体", VoyageCardKind.Body }, { "资源点", VoyageCardKind.Resource }, { "生产点", VoyageCardKind.Facility },
            { "生物", VoyageCardKind.Creature }, { "材料", VoyageCardKind.Material }, { "食物", VoyageCardKind.Food },
            { "工具", VoyageCardKind.Tool }, { "伤势", VoyageCardKind.Injury }, { "思绪", VoyageCardKind.Thought },
            { "信息卡", VoyageCardKind.Information }, { "造船部件", VoyageCardKind.Boat }, { "结局", VoyageCardKind.Ending }
        };

        public VoyageDefinition BuildDefinition()
        {
            if (Rules == null || Rules.Cards == null || Cards == null) throw new InvalidOperationException("缺少出航规则或卡牌目录。");
            VoyageDefinition definition = JsonUtility.FromJson<VoyageDefinition>(JsonUtility.ToJson(Rules));
            var assets = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
            foreach (CardDefinition card in Cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.Id)) throw new InvalidOperationException("卡牌目录中有空引用或空 ID。");
                if (assets.ContainsKey(card.Id)) throw new InvalidOperationException("卡牌资产 ID 重复：" + card.Id);
                assets.Add(card.Id, card);
            }
            foreach (VoyageCardRule rule in definition.Cards)
            {
                if (rule == null || string.IsNullOrEmpty(rule.Id) || !assets.TryGetValue(rule.Id, out CardDefinition card))
                    throw new InvalidOperationException("找不到机制规则对应的卡牌资产：" + rule?.Id);
                if (!Kinds.TryGetValue(card.Family ?? "", out VoyageCardKind kind))
                    throw new InvalidOperationException(card.DisplayName + "的卡牌分类不适用于出航规则：" + card.Family);
                rule.Kind = kind;
                rule.Name = card.DisplayName;
                rule.SortRank = card.SortRank;
                rule.Durability = InitialDurability(card);
                assets.Remove(rule.Id);
            }
            if (assets.Count != 0) throw new InvalidOperationException("存在未填写机制规则的卡牌资产。");
            return definition;
        }

        public string CheckConfiguration()
        {
            try { return VoyageEngine.ValidateDefinition(BuildDefinition()); }
            catch (InvalidOperationException exception) { return exception.Message; }
        }

        public CardDefinition FindCard(string id)
        {
            foreach (CardDefinition card in Cards)
                if (card != null && card.Id == id) return card;
            return null;
        }

        public Sprite FindArtwork(string id)
        {
            CardDefinition card = FindCard(id);
            return card != null ? card.Artwork : null;
        }

        private static int InitialDurability(CardDefinition card)
        {
            int? durability = null;
            if (card.Attributes != null)
                foreach (var attribute in card.Attributes)
                {
                    if (attribute == null || attribute.Key != SurvivalConfiguration.DurabilityAttribute) continue;
                    if (durability.HasValue) throw new InvalidOperationException(card.DisplayName + "有重复的初始耐久属性。");
                    durability = attribute.Value;
                }
            return durability.GetValueOrDefault();
        }
    }
}
