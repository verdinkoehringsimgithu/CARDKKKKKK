using System;
using System.Collections.Generic;
using System.IO;
using CardKKK.Tabletop.Core;
using UnityEngine;

namespace CardKKK.Tabletop.Unity
{
    [CreateAssetMenu(menuName = "孤岛生存/历史演示/自由牌桌/游戏规则配置", fileName = "新游戏配置")]
    public sealed class CardTableConfiguration : ScriptableObject
    {
        [InspectorName("桌面与交互规则")] public TableRuleSettings Rules = new TableRuleSettings();
        [InspectorName("可选功能")] public FeatureSelection Features = new FeatureSelection();
        [InspectorName("卡牌目录")] public List<CardDefinition> Cards = new List<CardDefinition>();
        [InspectorName("初始桌面")] public List<InitialPile> InitialPiles = new List<InitialPile>();
        [InspectorName("美术与音频主题（可留空）")] public CardTableTheme Theme;
        [InspectorName("存档文件名"), Tooltip("仅填写文件名，例如 card-table.json；保存位置由游戏统一管理。")]
        public string SaveFileName = "card-table.json";

        public CardCatalog BuildCatalog()
        {
            if (Cards == null) throw new InvalidOperationException("缺少卡牌目录。");
            var definitions = new List<CardDefinitionData>(Cards.Count);
            foreach (CardDefinition definition in Cards)
            {
                if (definition == null) throw new InvalidOperationException("卡牌目录中存在空引用。");
                definitions.Add(definition.ToData());
            }
            return new CardCatalog(definitions);
        }

        public BoardRules BuildRules()
        {
            if (Rules == null || Features == null) throw new InvalidOperationException("缺少桌面规则或功能配置。");
            BoardRules result = Rules.ToRules();
            result.Features = Features.ToFlags();
            return result;
        }

        /// <summary>编辑器和运行入口共用此校验，不自动更改设计者填写的数据。</summary>
        public bool Validate(out string error)
        {
            error = null;
            if (Rules == null || Features == null) return Fail("桌面规则与功能配置不能为空。", out error);
            if (Cards == null || Cards.Count == 0) return Fail("卡牌目录至少需要一张卡牌定义。", out error);
            if (InitialPiles == null) return Fail("初始桌面列表不能为空；不需要初始牌时请保留空列表。", out error);
            if (!ValidSaveFileName(SaveFileName))
                return Fail("存档名称只能是有效的 Windows 文件名，不能包含目录、保留名称或路径。", out error);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var references = new HashSet<CardDefinition>();
            var legacyAliases = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Cards.Count; i++)
            {
                CardDefinition card = Cards[i];
                if (card == null) return Fail("卡牌目录第 " + (i + 1) + " 项没有引用卡牌定义。", out error);
                if (string.IsNullOrWhiteSpace(card.Id) || card.Id != card.Id.Trim())
                    return Fail("卡牌“" + card.name + "”的定义 ID 不能为空或包含首尾空格。", out error);
                if (!ids.Add(card.Id)) return Fail("卡牌目录出现重复定义 ID：“" + card.Id + "”。", out error);
                references.Add(card);
                if (string.IsNullOrWhiteSpace(card.DisplayName) || string.IsNullOrWhiteSpace(card.Family))
                    return Fail("卡牌“" + card.Id + "”需要填写中文名称和堆叠分类。", out error);
                if (card.Attributes == null) return Fail("卡牌“" + card.Id + "”缺少属性列表。", out error);
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (CardAttribute attribute in card.Attributes)
                    if (attribute == null || string.IsNullOrWhiteSpace(attribute.Key) || !keys.Add(attribute.Key))
                        return Fail("卡牌“" + card.Id + "”的属性名称必须非空且唯一。", out error);
                if (!string.IsNullOrWhiteSpace(card.LegacyDefinitionId)
                    && !legacyAliases.Add(card.LegacyDefinitionId + "\n" + card.LegacyRank))
                    return Fail("旧版迁移定义 ID 与序号组合重复：“" + card.LegacyDefinitionId + "”。", out error);
            }

            try
            {
                CardCatalog catalog = BuildCatalog();
                var candidate = new BoardSnapshot();
                int cardIndex = 0;
                for (int p = 0; p < InitialPiles.Count; p++)
                {
                    InitialPile initial = InitialPiles[p];
                    if (initial == null || initial.Cards == null || initial.Cards.Count == 0)
                        return Fail("初始桌面第 " + (p + 1) + " 堆必须至少引用一张牌；无需此堆时请删除该项。", out error);
                    if (initial.Cards.Count > Rules.MaxPileSize)
                        return Fail("初始桌面第 " + (p + 1) + " 堆超过单堆上限。", out error);
                    if (initial.Cards.Count > Rules.MaxCards - cardIndex)
                        return Fail("初始桌面的卡牌总数超过全桌上限。", out error);
                    var pile = new PileData
                    {
                        Id = "initial-pile-" + p,
                        Position = new TablePoint(initial.Position.x, initial.Position.y)
                    };
                    foreach (CardDefinition definition in initial.Cards)
                    {
                        if (definition == null || !references.Contains(definition))
                            return Fail("初始桌面第 " + (p + 1) + " 堆引用了空卡牌或目录外的定义。", out error);
                        CardData card = catalog.CreateCard(definition.Id);
                        card.Id = "initial-card-" + cardIndex++;
                        candidate.Cards.Add(card);
                        pile.CardIds.Add(card.Id);
                    }
                    candidate.Piles.Add(pile);
                }
                // 使用同一核心规则校验尺寸、位置、重叠、容量与堆叠兼容，避免配置窗口另写一套规则。
                OperationResult result = new BoardEngine(BuildRules(), catalog).Restore(candidate);
                if (!result.Success) return Fail("配置无效：" + result.Error, out error);
                return true;
            }
            catch (ArgumentException exception)
            {
                return Fail("配置无效：" + exception.Message, out error);
            }
            catch (InvalidOperationException exception)
            {
                return Fail("配置无效：" + exception.Message, out error);
            }
        }

        private static bool Fail(string message, out string error) { error = message; return false; }

        private static bool ValidSaveFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.EndsWith(".")
                || value.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0
                || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value == "." || value == "..") return false;
            foreach (char character in value) if (char.IsControl(character)) return false;
            string stem = value.Split('.')[0].ToUpperInvariant();
            return stem != "CON" && stem != "PRN" && stem != "AUX" && stem != "NUL"
                && !(stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT"))
                    && stem[3] >= '1' && stem[3] <= '9');
        }
    }
}
