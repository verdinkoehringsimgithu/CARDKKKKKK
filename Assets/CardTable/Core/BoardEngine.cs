using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CardKKK.Tabletop.Core
{
    /// <summary>
    /// Owns committed card state. Commands validate a candidate and publish it once.
    /// Drag previews never mutate that state. Use from one thread (the Unity main thread).
    /// </summary>
    public sealed partial class BoardEngine
    {
        private const int MaxPlacementCandidates = 20000;
        private readonly BoardRules rules;
        private readonly CardCatalog catalog;
        private BoardSnapshot state = new BoardSnapshot();
        private DragSession drag;
        private bool notifying;

        public event Action Changed;
        public BoardRules Rules => rules.Copy();
        public bool IsDragging => drag != null;
        public DragInfo CurrentDrag => drag == null ? null : new DragInfo(drag.SourceId, drag.CardIds);

        public BoardEngine(BoardRules rules, CardCatalog catalog = null)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules), "桌面规则不能为空。");
            this.rules = rules.Copy();
            ValidateRules(this.rules);
            this.catalog = catalog;
        }

        public BoardSnapshot Capture() { return Copy(state); }

        public OperationResult Spawn(CardData template, TablePoint position)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            if (template == null) return OperationResult.Fail("缺少卡牌数据。");
            var card = CopyCard(template);
            if (catalog != null)
            {
                if (!catalog.TryGet(card.DefinitionId, out var definition))
                    return OperationResult.Fail("卡牌目录中找不到定义：" + card.DefinitionId);
                ApplyDefinition(card, definition);
            }
            string error = ValidateCard(card, false);
            if (error != null) return OperationResult.Fail(error);
            if (state.Cards.Count >= rules.MaxCards) return OperationResult.Fail("已达到桌面卡牌数量上限。");
            if (!Finite(position.X) || !Finite(position.Y)) return OperationResult.Fail("卡牌生成坐标必须是有效的有限数值。");
            position = new TablePoint(Clamp(position.X, 0, rules.Width - rules.CardWidth),
                Clamp(position.Y, 0, rules.Height - rules.CardHeight));
            var candidate = Copy(state);
            card.Id = NewId();
            var pile = new PileData { Id = NewId(), Position = position };
            pile.CardIds.Add(card.Id);
            candidate.Cards.Add(card);
            candidate.Piles.Add(pile);
            error = ResolveCandidateOverlaps(candidate, rules.AutoMergeOnSpawn);
            if (error != null) return OperationResult.Fail(error);
            string actualPile = FindCardPile(candidate, card.Id)?.Id;
            return Commit(candidate, actualPile, card.Id);
        }

        public OperationResult BringToFront(string pileId)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var pile = FindPile(candidate, pileId);
            if (pile == null) return OperationResult.Fail("找不到该牌堆。");
            candidate.Piles.Remove(pile);
            candidate.Piles.Add(pile);
            return Commit(candidate, pile.Id);
        }

        public OperationResult BeginDrag(string pileId, string cardId = null)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var pile = FindPile(state, pileId);
            if (pile == null) return OperationResult.Fail("找不到该牌堆。");
            if (cardId == null && !rules.AllowWholePileDrag)
                return OperationResult.Fail("当前配置已关闭整堆拖动。");
            if (cardId != null && !rules.AllowSingleCardDrag)
                return OperationResult.Fail("当前配置已关闭单张拖动。");
            if (cardId != null && !pile.CardIds.Contains(cardId))
                return OperationResult.Fail("这张卡牌不属于该牌堆。");
            drag = new DragSession
            {
                SourceId = pileId,
                SingleCardId = cardId,
                Revision = state.Revision,
                CardIds = cardId == null ? new List<string>(pile.CardIds) : new List<string> { cardId }
            };
            drag.BaseState = Copy(state);
            var detachedSource = FindPile(drag.BaseState, pileId);
            if (cardId == null) drag.BaseState.Piles.Remove(detachedSource);
            else
            {
                detachedSource.CardIds.Remove(cardId);
                if (detachedSource.CardIds.Count == 0) drag.BaseState.Piles.Remove(detachedSource);
            }
            drag.Cards = CardMap(drag.BaseState);
            drag.Index = new SpatialIndex(drag.BaseState.Piles, rules);
            drag.ReadOnlyCardIds = drag.CardIds.AsReadOnly();
            return OperationResult.Ok(pileId, cardId);
        }

        public DropPreview PreviewDrop(TablePoint position)
        {
            if (drag == null) return InvalidPlan(position, "当前没有正在拖动的卡牌。");
            if (drag.LastPreview != null && position.X == drag.LastPosition.X && position.Y == drag.LastPosition.Y)
                return drag.LastPreview;
            var preview = PlanDrop(position);
            drag.LastPosition = position;
            drag.LastPreview = preview;
            return preview;
        }

        public OperationResult CommitDrop(TablePoint position)
        {
            if (notifying) return OperationResult.Fail("桌面正在更新，请稍后操作。");
            var preview = PreviewDrop(position);
            var session = drag;
            drag = null;
            if (!preview.IsValid) return OperationResult.Fail(preview.Reason);
            var candidate = Copy(session.BaseState);
            var result = FindPile(candidate, preview.TargetPileId);
            if (result == null)
            {
                result = new PileData
                {
                    Id = session.SingleCardId == null ? session.SourceId : NewId(),
                    Position = preview.Position,
                    CardIds = new List<string>(session.CardIds)
                };
            }
            else result.CardIds.InsertRange(preview.InsertionIndex, session.CardIds);
            candidate.Piles.Remove(result);
            candidate.Piles.Add(result);
            return Commit(candidate, result.Id);
        }

        public OperationResult CancelDrag()
        {
            if (notifying) return OperationResult.Fail("桌面正在更新，请稍后操作。");
            if (drag == null) return OperationResult.Fail("当前没有正在拖动的卡牌。");
            string id = drag.SourceId;
            drag = null;
            return OperationResult.Ok(id);
        }

        public OperationResult ResolveOverlaps()
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            string error = ResolveCandidateOverlaps(candidate);
            return error == null ? Commit(candidate) : OperationResult.Fail(error);
        }

        public OperationResult Restore(BoardSnapshot snapshot)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            string error = ValidateSnapshotHeader(snapshot);
            if (error != null) return OperationResult.Fail(error);
            var candidate = Copy(snapshot);
            if (catalog != null)
            {
                foreach (var card in candidate.Cards)
                {
                    if (card == null) return OperationResult.Fail("缺少卡牌数据。");
                    if (!catalog.TryResolveSavedCard(card, out var definition))
                        return OperationResult.Fail("存档引用了当前卡牌目录中不存在的定义：" + card.DefinitionId);
                    ApplyDefinition(card, definition);
                }
            }
            return Commit(candidate);
        }

        private DropPreview PlanDrop(TablePoint position)
        {
            if (drag == null) return InvalidPlan(position, "当前没有正在拖动的卡牌。");
            if (drag.Revision != state.Revision) return InvalidPlan(position, "拖动期间桌面已发生变化，请重新拖动。");
            if (!Finite(position.X) || !Finite(position.Y)) return InvalidPlan(position, "落点坐标必须是有效的有限数值。");
            var candidate = drag.BaseState;
            var cards = drag.Cards;
            var movingCard = cards[drag.CardIds[0]];
            var index = drag.Index;
            var search = new TableRect(position.X, position.Y, 0, 0).Expand(rules.SnapDistance);
            int best = -1;
            double bestDistance = double.PositiveInfinity;
            index.QueryInto(search, drag.Candidates);
            foreach (int otherIndex in drag.Candidates)
            {
                var other = candidate.Piles[otherIndex];
                double distance = Rect(other).DistanceSquared(position);
                bool compatible = CanStack(movingCard, cards[other.CardIds[0]],
                    other.CardIds.Count + drag.CardIds.Count);
                if (!compatible && distance == 0)
                    return InvalidPlan(position, "该牌堆类型不兼容，或已达到容量上限。");
                if (!compatible || distance > (double)rules.SnapDistance * rules.SnapDistance) continue;
                if (distance < bestDistance || (distance == bestDistance && otherIndex > best))
                {
                    best = otherIndex;
                    bestDistance = distance;
                }
            }
            string targetId = null;
            int insertion = 0;
            TableRect footprint;
            if (best >= 0)
            {
                var result = candidate.Piles[best];
                targetId = result.Id;
                insertion = Math.Max(0, Math.Min(result.CardIds.Count,
                    (int)Math.Floor((position.Y - result.Position.Y) / rules.PileOffset + 0.5f)));
                position = result.Position;
                footprint = Rect(position, result.CardIds.Count + drag.CardIds.Count);
            }
            else footprint = Rect(position, drag.CardIds.Count);
            if (!Inside(footprint)) return InvalidPlan(position, "落点超出了桌面范围。");
            index.QueryInto(footprint, drag.Candidates);
            foreach (int other in drag.Candidates)
                if (other != best && footprint.Overlaps(Rect(candidate.Piles[other])))
                    return InvalidPlan(position, "牌堆占用区域发生重叠。");
            return new DropPreview(true, position, targetId, insertion, "", drag.ReadOnlyCardIds);
        }

        private DropPreview InvalidPlan(TablePoint position, string reason)
        {
            return new DropPreview(false, position, null, 0, reason, drag?.ReadOnlyCardIds);
        }

        private string MutationBlock()
        {
            if (notifying) return "桌面正在更新，请稍后操作。";
            return drag == null ? null : "请先放下卡牌或取消当前拖动。";
        }

        private OperationResult Commit(BoardSnapshot candidate, string pileId = null, string cardId = null)
        {
            string error = Validate(candidate);
            if (error != null) return OperationResult.Fail(error);
            if (state.Revision == int.MaxValue) return OperationResult.Fail("桌面更新次数已达到上限，请新建桌面。");
            candidate.Revision = state.Revision + 1;
            state = candidate;
            NotifyChanged();
            return OperationResult.Ok(pileId, cardId);
        }

        private void NotifyChanged()
        {
            Action handlers = Changed;
            if (handlers == null) return;
            notifying = true;
            try
            {
                foreach (Action handler in handlers.GetInvocationList())
                {
                    try { handler(); }
                    catch (Exception error) { Trace.TraceError("桌面更新通知处理失败：{0}", error); }
                }
            }
            finally { notifying = false; }
        }

        private string ResolveCandidateOverlaps(BoardSnapshot candidate, bool allowMerge = true)
        {
            var cards = CardMap(candidate);
            int limit = Math.Min(20000, Math.Max(1, candidate.Piles.Count * candidate.Piles.Count + 1));
            for (int step = 0; step < limit; step++)
            {
                var index = new SpatialIndex(candidate.Piles, rules);
                int first = -1, second = -1;
                for (int i = 0; i < candidate.Piles.Count && first < 0; i++)
                {
                    var rect = Rect(candidate.Piles[i]);
                    foreach (int j in index.Query(rect))
                    {
                        if (j <= i || !rect.Overlaps(Rect(candidate.Piles[j]))) continue;
                        first = i;
                        second = j;
                        break;
                    }
                }
                if (first < 0) return null;
                var a = candidate.Piles[first];
                var b = candidate.Piles[second];
                int count = a.CardIds.Count + b.CardIds.Count;
                bool canMerge = allowMerge && CanStack(cards[a.CardIds[0]], cards[b.CardIds[0]], count) &&
                                Inside(Rect(a.Position, count));
                if (canMerge)
                {
                    a.CardIds.AddRange(b.CardIds);
                    candidate.Piles.RemoveAt(second);
                }
                else
                {
                    candidate.Piles.RemoveAt(second);
                    if (!FindFree(candidate, b.CardIds.Count, b.Position, out TablePoint position))
                        return "桌面上找不到不会与其他牌堆重叠的空位。";
                    b.Position = position;
                    candidate.Piles.Insert(second, b);
                }
            }
            return "重叠整理已达到本次处理上限，请调整牌堆后重试。";
        }

        private bool FindFree(BoardSnapshot candidate, int cardCount, TablePoint desired, out TablePoint result)
        {
            float width = rules.CardWidth;
            float height = rules.CardHeight + (cardCount - 1) * rules.PileOffset;
            result = default;
            if (height > rules.Height || width > rules.Width) return false;
            float maxX = rules.Width - width, maxY = rules.Height - height;
            desired = new TablePoint(Clamp(desired.X, 0, maxX), Clamp(desired.Y, 0, maxY));
            var positions = new List<TablePoint> { desired };
            AddPosition(positions, new TablePoint(0, 0), maxX, maxY);
            AddPosition(positions, new TablePoint(maxX, 0), maxX, maxY);
            AddPosition(positions, new TablePoint(0, maxY), maxX, maxY);
            AddPosition(positions, new TablePoint(maxX, maxY), maxX, maxY);
            foreach (var pile in candidate.Piles)
            {
                var rect = Rect(pile);
                float left = rect.X - width - rules.Gap, right = rect.Right + rules.Gap;
                float bottom = rect.Y - height - rules.Gap, top = rect.Top + rules.Gap;
                AddPosition(positions, new TablePoint(left, desired.Y), maxX, maxY);
                AddPosition(positions, new TablePoint(right, desired.Y), maxX, maxY);
                AddPosition(positions, new TablePoint(desired.X, bottom), maxX, maxY);
                AddPosition(positions, new TablePoint(desired.X, top), maxX, maxY);
                AddPosition(positions, new TablePoint(left, bottom), maxX, maxY);
                AddPosition(positions, new TablePoint(left, top), maxX, maxY);
                AddPosition(positions, new TablePoint(right, bottom), maxX, maxY);
                AddPosition(positions, new TablePoint(right, top), maxX, maxY);
                if (positions.Count >= MaxPlacementCandidates) break;
            }
            double xStep = width + rules.Gap, yStep = height + rules.Gap;
            for (double y = 0; y <= maxY && positions.Count < MaxPlacementCandidates; y += yStep)
            for (double x = 0; x <= maxX && positions.Count < MaxPlacementCandidates; x += xStep)
                AddPosition(positions, new TablePoint((float)x, (float)y), maxX, maxY);
            positions.Sort((a, b) =>
            {
                int byDistance = Distance(a, desired).CompareTo(Distance(b, desired));
                if (byDistance != 0) return byDistance;
                int byX = a.X.CompareTo(b.X);
                return byX != 0 ? byX : a.Y.CompareTo(b.Y);
            });
            var index = new SpatialIndex(candidate.Piles, rules);
            foreach (var point in positions)
            {
                var area = new TableRect(point.X, point.Y, width, height).Expand(rules.Gap);
                bool free = true;
                foreach (int other in index.Query(area))
                {
                    if (!area.Overlaps(Rect(candidate.Piles[other]))) continue;
                    free = false;
                    break;
                }
                if (!free) continue;
                result = point;
                return true;
            }
            return false;
        }

        private static void AddPosition(List<TablePoint> list, TablePoint position, float maxX, float maxY)
        {
            if (list.Count < MaxPlacementCandidates && position.X >= 0 && position.Y >= 0 &&
                position.X <= maxX && position.Y <= maxY) list.Add(position);
        }

        private string Validate(BoardSnapshot snapshot)
        {
            string headerError = ValidateSnapshotHeader(snapshot);
            if (headerError != null) return headerError;
            var cards = new Dictionary<string, CardData>();
            foreach (var card in snapshot.Cards)
            {
                string error = ValidateCard(card, true);
                if (error != null) return error;
                if (cards.ContainsKey(card.Id)) return "存档中存在重复的卡牌标识。";
                cards.Add(card.Id, card);
            }
            var piles = new HashSet<string>();
            var membership = new HashSet<string>();
            foreach (var pile in snapshot.Piles)
            {
                if (pile == null || string.IsNullOrWhiteSpace(pile.Id) || !piles.Add(pile.Id))
                    return "每个牌堆都必须具有非空且唯一的标识。";
                if (pile.CardIds == null || pile.CardIds.Count == 0 || pile.CardIds.Count > rules.MaxPileSize)
                    return "存档中存在空牌堆，或牌堆超过了容量上限。";
                if (!rules.AllowStacking && pile.CardIds.Count > 1)
                    return "当前规则禁止堆叠，每个牌堆只能包含一张卡牌。";
                if (!Inside(Rect(pile))) return "存档中有牌堆超出了桌面范围。";
                CardData firstCard = null;
                foreach (string cardId in pile.CardIds)
                {
                    if (cardId == null || !cards.TryGetValue(cardId, out var card)) return "牌堆引用了不存在的卡牌。";
                    if (!membership.Add(cardId)) return "同一张卡牌被重复放入了多个牌堆。";
                    if (firstCard == null) firstCard = card;
                    else if (!CanStack(firstCard, card, pile.CardIds.Count))
                        return "同一牌堆中包含不兼容的卡牌类型。";
                }
            }
            if (membership.Count != cards.Count) return "每张卡牌都必须归属于一个牌堆，且只能归属一次。";
            var index = new SpatialIndex(snapshot.Piles, rules);
            for (int i = 0; i < snapshot.Piles.Count; i++)
            {
                var rect = Rect(snapshot.Piles[i]);
                foreach (int j in index.Query(rect))
                    if (j > i && rect.Overlaps(Rect(snapshot.Piles[j]))) return "牌堆占用区域发生重叠。";
            }
            return null;
        }

        private string ValidateSnapshotHeader(BoardSnapshot snapshot)
        {
            if (snapshot == null) return "缺少桌面存档数据。";
            if (snapshot.SchemaVersion != 1) return "不支持此版本的桌面存档。";
            if (snapshot.Revision < 0) return "桌面更新次数不能为负数。";
            if (snapshot.Cards == null || snapshot.Piles == null) return "存档必须包含卡牌列表和牌堆列表。";
            if (snapshot.Cards.Count > rules.MaxCards || snapshot.Piles.Count > rules.MaxCards)
                return "存档中的卡牌或牌堆数量超过了当前上限。";
            return null;
        }

        // 所有合堆与存档校验共用此规则，避免预览与实际提交出现不同判断。
        private bool CanStack(CardData first, CardData second, int combinedCount)
        {
            if (!rules.AllowStacking || combinedCount > rules.MaxPileSize) return false;
            switch (rules.StackRule)
            {
                case StackRule.SameDefinition: return first.DefinitionId == second.DefinitionId;
                case StackRule.AnyCard: return true;
                default: return first.Family == second.Family;
            }
        }

        private static void ApplyDefinition(CardData card, CardDefinitionData definition)
        {
            card.DefinitionId = definition.Id;
            card.Label = definition.Label;
            card.Family = definition.Family;
            card.Rank = definition.Rank;
        }

        private static string ValidateCard(CardData card, bool requireId)
        {
            if (card == null) return "缺少卡牌数据。";
            if (requireId && string.IsNullOrWhiteSpace(card.Id)) return "卡牌必须具有非空标识。";
            if (string.IsNullOrWhiteSpace(card.DefinitionId) || string.IsNullOrWhiteSpace(card.Family))
                return "卡牌必须指定定义标识和类型。";
            if (card.Attributes == null) return "卡牌必须包含属性列表。";
            var keys = new HashSet<string>();
            foreach (var attribute in card.Attributes)
                if (attribute == null || string.IsNullOrWhiteSpace(attribute.Key) || !keys.Add(attribute.Key))
                    return "卡牌属性名称必须非空，且不能重复。";
            return null;
        }

        private static void ValidateRules(BoardRules rules)
        {
            if (!Positive(rules.Width) || !Positive(rules.Height) || !Positive(rules.CardWidth) ||
                !Positive(rules.CardHeight) || !Positive(rules.PileOffset) ||
                !Finite(rules.Gap) || rules.Gap < 0 || !Finite(rules.SnapDistance) || rules.SnapDistance < 0 ||
                rules.Width < rules.CardWidth || rules.Height < rules.CardHeight ||
                rules.MaxCards < 1 || rules.MaxCards > 10000 || rules.MaxPileSize < 1 || rules.MaxPileSize > 10000 ||
                rules.Width / rules.CardWidth > 100000 || rules.Height / rules.CardHeight > 100000 ||
                !Finite(rules.CardHeight + (rules.MaxPileSize - 1) * rules.PileOffset) ||
                !Finite(rules.Width + 2 * rules.SnapDistance) || !Finite(rules.Height + 2 * rules.SnapDistance) ||
                !Finite(rules.Width + 2 * rules.Gap) || !Finite(rules.Height + 2 * rules.Gap))
                throw new ArgumentException("桌面尺寸、卡牌尺寸、间距和容量上限必须符合有效数值及范围要求。", nameof(rules));
            if (!Enum.IsDefined(typeof(StackRule), rules.StackRule) ||
                !Enum.IsDefined(typeof(CardSortRule), rules.SortRule) ||
                (rules.Features & ~TableFeatures.All) != TableFeatures.None)
                throw new ArgumentException("堆叠规则、排序规则或功能选项无效。", nameof(rules));
        }

        private TableRect Rect(PileData pile) { return Rect(pile.Position, pile.CardIds.Count); }
        private TableRect Rect(TablePoint point, int count)
        {
            return new TableRect(point.X, point.Y, rules.CardWidth,
                rules.CardHeight + (count - 1) * rules.PileOffset);
        }
        private bool Inside(TableRect rect)
        {
            return Finite(rect.X) && Finite(rect.Y) && Finite(rect.Right) && Finite(rect.Top) &&
                   rect.X >= 0 && rect.Y >= 0 && rect.Right <= rules.Width + 0.0001f && rect.Top <= rules.Height + 0.0001f;
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Positive(float value) { return Finite(value) && value > 0; }
        private static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
        private static double Distance(TablePoint a, TablePoint b)
        {
            double x = (double)a.X - b.X, y = (double)a.Y - b.Y;
            return x * x + y * y;
        }
        private static string NewId() { return Guid.NewGuid().ToString("N"); }
        private static PileData FindPile(BoardSnapshot snapshot, string pileId)
        {
            return snapshot.Piles.Find(pile => pile.Id == pileId);
        }
        private static PileData FindCardPile(BoardSnapshot snapshot, string cardId)
        {
            return snapshot.Piles.Find(pile => pile.CardIds.Contains(cardId));
        }
        private static Dictionary<string, CardData> CardMap(BoardSnapshot snapshot)
        {
            var result = new Dictionary<string, CardData>();
            foreach (var card in snapshot.Cards) result.Add(card.Id, card);
            return result;
        }
        private static BoardSnapshot Copy(BoardSnapshot snapshot)
        {
            var result = new BoardSnapshot { SchemaVersion = snapshot.SchemaVersion, Revision = snapshot.Revision };
            foreach (var card in snapshot.Cards) result.Cards.Add(CopyCard(card));
            foreach (var pile in snapshot.Piles)
                result.Piles.Add(pile == null ? null : new PileData
                {
                    Id = pile.Id,
                    Position = pile.Position,
                    CardIds = pile.CardIds == null ? null : new List<string>(pile.CardIds)
                });
            return result;
        }
        private static CardData CopyCard(CardData card)
        {
            if (card == null) return null;
            var result = new CardData
            {
                Id = card.Id, DefinitionId = card.DefinitionId, Label = card.Label,
                Family = card.Family, Rank = card.Rank, FaceUp = card.FaceUp,
                Attributes = card.Attributes == null ? null : new List<CardAttribute>()
            };
            if (card.Attributes != null)
                foreach (var attribute in card.Attributes)
                    result.Attributes.Add(attribute == null ? null :
                        new CardAttribute { Key = attribute.Key, Value = attribute.Value });
            return result;
        }
        private sealed class DragSession
        {
            internal string SourceId;
            internal string SingleCardId;
            internal int Revision;
            internal List<string> CardIds;
            internal BoardSnapshot BaseState;
            internal Dictionary<string, CardData> Cards;
            internal SpatialIndex Index;
            internal IReadOnlyList<string> ReadOnlyCardIds;
            internal readonly List<int> Candidates = new List<int>();
            internal TablePoint LastPosition;
            internal DropPreview LastPreview;
        }
    }
}
