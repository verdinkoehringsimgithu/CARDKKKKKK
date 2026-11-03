#if !CARDTABLE_DISABLE_REORDER
namespace CardKKK.Tabletop.Core
{
    public sealed partial class BoardEngine
    {
        public OperationResult ReorderCard(string pileId, string cardId, int index)
        {
            if ((rules.Features & TableFeatures.Reorder) == 0)
                return OperationResult.Fail("当前配置已关闭调序功能。");
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var pile = FindPile(candidate, pileId);
            if (pile == null || !pile.CardIds.Contains(cardId))
                return OperationResult.Fail("这张卡牌不属于该牌堆。");
            if (index < 0 || index >= pile.CardIds.Count)
                return OperationResult.Fail("插入位置超出了该牌堆的范围。");
            pile.CardIds.Remove(cardId);
            pile.CardIds.Insert(index, cardId);
            return Commit(candidate, pile.Id, cardId);
        }
    }
}
#endif
