#if !CARDTABLE_DISABLE_SORT
using System.Collections.Generic;

namespace CardKKK.Tabletop.Core
{
    public sealed partial class BoardEngine
    {
        public OperationResult SortPile(string pileId)
        {
            if ((rules.Features & TableFeatures.Sort) == 0)
                return OperationResult.Fail("当前配置已关闭排序功能。");
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var pile = FindPile(candidate, pileId);
            if (pile == null) return OperationResult.Fail("找不到该牌堆。");
            var cards = CardMap(candidate);
            var originalOrder = new Dictionary<string, int>();
            for (int i = 0; i < pile.CardIds.Count; i++) originalOrder[pile.CardIds[i]] = i;
            pile.CardIds.Sort((a, b) =>
            {
                int compared = CompareForSort(cards[a], cards[b]);
                return compared != 0 ? compared : originalOrder[a].CompareTo(originalOrder[b]);
            });
            return Commit(candidate, pile.Id);
        }

        private int CompareForSort(CardData a, CardData b)
        {
            switch (rules.SortRule)
            {
                case CardSortRule.RankDescending: return b.Rank.CompareTo(a.Rank);
                case CardSortRule.NameAscending: return string.CompareOrdinal(a.Label, b.Label);
                default: return a.Rank.CompareTo(b.Rank);
            }
        }
    }
}
#endif
