#if !CARDTABLE_DISABLE_SHUFFLE
using System;

namespace CardKKK.Tabletop.Core
{
    public sealed partial class BoardEngine
    {
        public OperationResult ShufflePile(string pileId, int seed)
        {
            if ((rules.Features & TableFeatures.Shuffle) == 0)
                return OperationResult.Fail("当前配置已关闭洗牌功能。");
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var pile = FindPile(candidate, pileId);
            if (pile == null) return OperationResult.Fail("找不到该牌堆。");
            var random = new Random(seed);
            for (int i = pile.CardIds.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                string held = pile.CardIds[i];
                pile.CardIds[i] = pile.CardIds[other];
                pile.CardIds[other] = held;
            }
            return Commit(candidate, pile.Id);
        }
    }
}
#endif
