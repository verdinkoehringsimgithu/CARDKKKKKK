using System;
using System.Collections.Generic;

namespace CardKKK.Tabletop.Core
{
    /// <summary>卡牌定义的不可变快照，不引用 Unity 资产或可变的外部属性列表。</summary>
    public sealed class CardDefinitionData
    {
        private readonly List<CardAttribute> attributes;
        public string Id { get; }
        public string Label { get; }
        public string Family { get; }
        public int Rank { get; }
        public string LegacyDefinitionId { get; }
        public int LegacyRank { get; }

        // CardAttribute 是存档用的可变 DTO，因此每次公开读取也提供深拷贝。
        public IReadOnlyList<CardAttribute> Attributes => CopyAttributes().AsReadOnly();

        public CardDefinitionData(string id, string label, string family, int rank,
            IEnumerable<CardAttribute> attributes, string legacyDefinitionId = null, int legacyRank = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("卡牌定义标识不能为空。", nameof(id));
            if (string.IsNullOrWhiteSpace(family)) throw new ArgumentException("卡牌定义类型不能为空。", nameof(family));
            if (attributes == null) throw new ArgumentNullException(nameof(attributes), "卡牌定义必须包含属性列表。");
            if (legacyDefinitionId != null && string.IsNullOrWhiteSpace(legacyDefinitionId))
                throw new ArgumentException("旧版定义标识为空时请使用 null。", nameof(legacyDefinitionId));
            Id = id;
            Label = label ?? "";
            Family = family;
            Rank = rank;
            LegacyDefinitionId = legacyDefinitionId;
            LegacyRank = legacyRank;
            this.attributes = new List<CardAttribute>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attribute in attributes)
            {
                if (attribute == null || string.IsNullOrWhiteSpace(attribute.Key) || !keys.Add(attribute.Key))
                    throw new ArgumentException("卡牌定义属性名称必须非空，且不能重复。", nameof(attributes));
                this.attributes.Add(new CardAttribute { Key = attribute.Key, Value = attribute.Value });
            }
        }

        internal List<CardAttribute> CopyAttributes()
        {
            var copy = new List<CardAttribute>(attributes.Count);
            foreach (var attribute in attributes)
                copy.Add(new CardAttribute { Key = attribute.Key, Value = attribute.Value });
            return copy;
        }
    }

    /// <summary>按稳定定义标识查询。目录建立后保持只读，卡牌实例互不共享可变属性。</summary>
    public sealed class CardCatalog
    {
        private readonly Dictionary<string, CardDefinitionData> definitions =
            new Dictionary<string, CardDefinitionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<int, CardDefinitionData>> legacyDefinitions =
            new Dictionary<string, Dictionary<int, CardDefinitionData>>(StringComparer.Ordinal);

        public CardCatalog(IEnumerable<CardDefinitionData> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions), "卡牌目录不能为空。");
            foreach (var definition in definitions)
            {
                if (definition == null) throw new ArgumentException("卡牌目录包含空定义。", nameof(definitions));
                if (this.definitions.ContainsKey(definition.Id))
                    throw new ArgumentException("卡牌目录中存在重复定义标识：" + definition.Id, nameof(definitions));
                this.definitions.Add(definition.Id, definition);
                if (definition.LegacyDefinitionId == null) continue;
                if (!legacyDefinitions.TryGetValue(definition.LegacyDefinitionId, out var ranks))
                {
                    ranks = new Dictionary<int, CardDefinitionData>();
                    legacyDefinitions.Add(definition.LegacyDefinitionId, ranks);
                }
                if (ranks.ContainsKey(definition.LegacyRank))
                    throw new ArgumentException("卡牌目录中存在重复的旧版定义与序号映射。", nameof(definitions));
                ranks.Add(definition.LegacyRank, definition);
            }
        }

        public bool TryGet(string id, out CardDefinitionData definition)
        {
            definition = null;
            return id != null && definitions.TryGetValue(id, out definition);
        }

        public bool TryResolveSavedCard(CardData card, out CardDefinitionData definition)
        {
            definition = null;
            if (card == null || card.DefinitionId == null) return false;
            if (legacyDefinitions.TryGetValue(card.DefinitionId, out var ranks) &&
                ranks.TryGetValue(card.Rank, out definition)) return true;
            return TryGet(card.DefinitionId, out definition);
        }

        public CardData CreateCard(string definitionId)
        {
            if (!TryGet(definitionId, out var definition))
                throw new KeyNotFoundException("卡牌目录中找不到定义：" + definitionId);
            return new CardData
            {
                DefinitionId = definition.Id, Label = definition.Label, Family = definition.Family,
                Rank = definition.Rank, Attributes = definition.CopyAttributes()
            };
        }
    }
}
