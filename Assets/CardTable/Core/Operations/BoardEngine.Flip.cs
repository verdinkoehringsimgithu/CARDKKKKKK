#if !CARDTABLE_DISABLE_FLIP
namespace CardKKK.Tabletop.Core
{
    public sealed partial class BoardEngine
    {
        public OperationResult FlipCard(string cardId)
        {
            if ((rules.Features & TableFeatures.Flip) == 0)
                return OperationResult.Fail("当前配置已关闭翻面功能。");
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var card = candidate.Cards.Find(item => item.Id == cardId);
            if (card == null) return OperationResult.Fail("找不到这张卡牌。");
            card.FaceUp = !card.FaceUp;
            return Commit(candidate, FindCardPile(candidate, cardId).Id, cardId);
        }

        public OperationResult FlipPile(string pileId)
        {
            if ((rules.Features & TableFeatures.Flip) == 0)
                return OperationResult.Fail("当前配置已关闭翻面功能。");
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var pile = FindPile(candidate, pileId);
            if (pile == null) return OperationResult.Fail("找不到该牌堆。");
            var cards = CardMap(candidate);
            foreach (string id in pile.CardIds) cards[id].FaceUp = !cards[id].FaceUp;
            return Commit(candidate, pile.Id);
        }
    }
}
#endif
