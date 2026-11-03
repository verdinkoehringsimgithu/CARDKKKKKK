#if !CARDTABLE_DISABLE_POP
namespace CardKKK.Tabletop.Core
{
    public sealed partial class BoardEngine
    {
        public OperationResult PopCard(string pileId, string cardId = null)
        {
            if ((rules.Features & TableFeatures.Pop) == 0)
                return OperationResult.Fail("当前配置已关闭抽牌功能。");
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var source = FindPile(candidate, pileId);
            if (source == null) return OperationResult.Fail("找不到该牌堆。");
            cardId = cardId ?? source.CardIds[source.CardIds.Count - 1];
            if (!source.CardIds.Contains(cardId)) return OperationResult.Fail("这张卡牌不属于该牌堆。");
            if (source.CardIds.Count == 1) return OperationResult.Fail("该牌堆只有一张卡牌，无需抽出。");
            source.CardIds.Remove(cardId);
            var wanted = new TablePoint(source.Position.X + rules.CardWidth + rules.Gap, source.Position.Y);
            if (!FindFree(candidate, 1, wanted, out TablePoint position))
                return OperationResult.Fail("桌面上找不到可放置卡牌的空位。");
            var pile = new PileData { Id = NewId(), Position = position };
            pile.CardIds.Add(cardId);
            candidate.Piles.Add(pile);
            return Commit(candidate, pile.Id, cardId);
        }
    }
}
#endif
