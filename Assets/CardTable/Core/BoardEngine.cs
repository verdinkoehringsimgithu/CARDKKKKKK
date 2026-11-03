using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CardKKK.Tabletop.Core
{
    /// <summary>
    /// Owns committed card state. Commands validate a candidate and publish it once.
    /// Drag previews never mutate that state. Use from one thread (the Unity main thread).
    /// </summary>
    public sealed class BoardEngine
    {
        private const int MaxPlacementCandidates = 20000;
        private readonly BoardRules rules;
        private BoardSnapshot state = new BoardSnapshot();
        private DragSession drag;
        private bool notifying;

        public event Action Changed;
        public BoardRules Rules => rules.Copy();
        public bool IsDragging => drag != null;
        public DragInfo CurrentDrag => drag == null ? null : new DragInfo(drag.SourceId, drag.CardIds);

        public BoardEngine(BoardRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            this.rules = rules.Copy();
            ValidateRules(this.rules);
        }

        public BoardSnapshot Capture() { return Copy(state); }

        public OperationResult Spawn(CardData template, TablePoint position)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            string error = ValidateCard(template, false);
            if (error != null) return OperationResult.Fail(error);
            if (state.Cards.Count >= rules.MaxCards) return OperationResult.Fail("Card limit reached.");
            if (!Finite(position.X) || !Finite(position.Y)) return OperationResult.Fail("Spawn coordinates must be finite.");
            position = new TablePoint(Clamp(position.X, 0, rules.Width - rules.CardWidth),
                Clamp(position.Y, 0, rules.Height - rules.CardHeight));
            var candidate = Copy(state);
            var card = CopyCard(template);
            card.Id = NewId();
            var pile = new PileData { Id = NewId(), Position = position };
            pile.CardIds.Add(card.Id);
            candidate.Cards.Add(card);
            candidate.Piles.Add(pile);
            error = ResolveCandidateOverlaps(candidate);
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
            if (pile == null) return OperationResult.Fail("Pile does not exist.");
            candidate.Piles.Remove(pile);
            candidate.Piles.Add(pile);
            return Commit(candidate, pile.Id);
        }

        public OperationResult BeginDrag(string pileId, string cardId = null)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var pile = FindPile(state, pileId);
            if (pile == null) return OperationResult.Fail("Pile does not exist.");
            if (cardId != null && !pile.CardIds.Contains(cardId))
                return OperationResult.Fail("Card does not belong to this pile.");
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
            if (drag == null) return InvalidPlan(position, "No drag is active.");
            if (drag.LastPreview != null && position.X == drag.LastPosition.X && position.Y == drag.LastPosition.Y)
                return drag.LastPreview;
            var preview = PlanDrop(position);
            drag.LastPosition = position;
            drag.LastPreview = preview;
            return preview;
        }

        public OperationResult CommitDrop(TablePoint position)
        {
            if (notifying) return OperationResult.Fail("A state notification is in progress.");
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
            if (notifying) return OperationResult.Fail("A state notification is in progress.");
            if (drag == null) return OperationResult.Fail("No drag is active.");
            string id = drag.SourceId;
            drag = null;
            return OperationResult.Ok(id);
        }

        public OperationResult FlipCard(string cardId)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var card = candidate.Cards.Find(item => item.Id == cardId);
            if (card == null) return OperationResult.Fail("Card does not exist.");
            card.FaceUp = !card.FaceUp;
            return Commit(candidate, FindCardPile(candidate, cardId).Id, cardId);
        }

        public OperationResult FlipPile(string pileId)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var pile = FindPile(candidate, pileId);
            if (pile == null) return OperationResult.Fail("Pile does not exist.");
            var cards = CardMap(candidate);
            foreach (string id in pile.CardIds) cards[id].FaceUp = !cards[id].FaceUp;
            return Commit(candidate, pile.Id);
        }

        public OperationResult SortPile(string pileId)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var pile = FindPile(candidate, pileId);
            if (pile == null) return OperationResult.Fail("Pile does not exist.");
            var cards = CardMap(candidate);
            var originalOrder = new Dictionary<string, int>();
            for (int i = 0; i < pile.CardIds.Count; i++) originalOrder[pile.CardIds[i]] = i;
            pile.CardIds.Sort((a, b) =>
            {
                int byRank = cards[a].Rank.CompareTo(cards[b].Rank);
                return byRank != 0 ? byRank : originalOrder[a].CompareTo(originalOrder[b]);
            });
            return Commit(candidate, pile.Id);
        }

        public OperationResult ReorderCard(string pileId, string cardId, int index)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var pile = FindPile(candidate, pileId);
            if (pile == null || !pile.CardIds.Contains(cardId))
                return OperationResult.Fail("Card does not belong to this pile.");
            if (index < 0 || index >= pile.CardIds.Count)
                return OperationResult.Fail("Insertion index is outside this pile.");
            pile.CardIds.Remove(cardId);
            pile.CardIds.Insert(index, cardId);
            return Commit(candidate, pile.Id, cardId);
        }

        public OperationResult ShufflePile(string pileId, int seed)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var pile = FindPile(candidate, pileId);
            if (pile == null) return OperationResult.Fail("Pile does not exist.");
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

        public OperationResult PopCard(string pileId, string cardId = null)
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            var source = FindPile(candidate, pileId);
            if (source == null) return OperationResult.Fail("Pile does not exist.");
            cardId = cardId ?? source.CardIds[source.CardIds.Count - 1];
            if (!source.CardIds.Contains(cardId)) return OperationResult.Fail("Card does not belong to this pile.");
            if (source.CardIds.Count == 1) return OperationResult.Fail("This pile already contains one card.");
            source.CardIds.Remove(cardId);
            var wanted = new TablePoint(source.Position.X + rules.CardWidth + rules.Gap, source.Position.Y);
            if (!FindFree(candidate, 1, wanted, out TablePoint position))
                return OperationResult.Fail("No free card slot was found on the table.");
            var pile = new PileData { Id = NewId(), Position = position };
            pile.CardIds.Add(cardId);
            candidate.Piles.Add(pile);
            return Commit(candidate, pile.Id, cardId);
        }

        public OperationResult TidyTable()
        {
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            MergeNearby(candidate);
            if (!PackColumns(candidate)) return OperationResult.Fail("The table has insufficient room for this arrangement.");
            return Commit(candidate);
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
            string error = Validate(snapshot);
            if (error != null) return OperationResult.Fail(error);
            return Commit(Copy(snapshot));
        }

        private DropPreview PlanDrop(TablePoint position)
        {
            if (drag == null) return InvalidPlan(position, "No drag is active.");
            if (drag.Revision != state.Revision) return InvalidPlan(position, "The table changed during this drag.");
            if (!Finite(position.X) || !Finite(position.Y)) return InvalidPlan(position, "Drop coordinates must be finite.");
            var candidate = drag.BaseState;
            var cards = drag.Cards;
            string family = cards[drag.CardIds[0]].Family;
            var index = drag.Index;
            var search = new TableRect(position.X, position.Y, 0, 0).Expand(rules.SnapDistance);
            int best = -1;
            double bestDistance = double.PositiveInfinity;
            index.QueryInto(search, drag.Candidates);
            foreach (int otherIndex in drag.Candidates)
            {
                var other = candidate.Piles[otherIndex];
                double distance = Rect(other).DistanceSquared(position);
                bool compatible = cards[other.CardIds[0]].Family == family &&
                                  other.CardIds.Count + drag.CardIds.Count <= rules.MaxPileSize;
                if (!compatible && distance == 0)
                    return InvalidPlan(position, "This pile is incompatible or at capacity.");
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
            if (!Inside(footprint)) return InvalidPlan(position, "Drop position is outside the table.");
            index.QueryInto(footprint, drag.Candidates);
            foreach (int other in drag.Candidates)
                if (other != best && footprint.Overlaps(Rect(candidate.Piles[other])))
                    return InvalidPlan(position, "Pile footprints overlap.");
            return new DropPreview(true, position, targetId, insertion, "", drag.ReadOnlyCardIds);
        }

        private DropPreview InvalidPlan(TablePoint position, string reason)
        {
            return new DropPreview(false, position, null, 0, reason, drag?.ReadOnlyCardIds);
        }

        private string MutationBlock()
        {
            if (notifying) return "A state notification is in progress.";
            return drag == null ? null : "Finish or cancel the current drag first.";
        }

        private OperationResult Commit(BoardSnapshot candidate, string pileId = null, string cardId = null)
        {
            string error = Validate(candidate);
            if (error != null) return OperationResult.Fail(error);
            if (state.Revision == int.MaxValue) return OperationResult.Fail("Revision limit reached; start a new table.");
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
                    catch (Exception error) { Trace.TraceError("Card table observer failed: {0}", error); }
                }
            }
            finally { notifying = false; }
        }

        private string ResolveCandidateOverlaps(BoardSnapshot candidate)
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
                bool canMerge = cards[a.CardIds[0]].Family == cards[b.CardIds[0]].Family &&
                                count <= rules.MaxPileSize && Inside(Rect(a.Position, count));
                if (canMerge)
                {
                    a.CardIds.AddRange(b.CardIds);
                    candidate.Piles.RemoveAt(second);
                }
                else
                {
                    candidate.Piles.RemoveAt(second);
                    if (!FindFree(candidate, b.CardIds.Count, b.Position, out TablePoint position))
                        return "No collision-free position was found on the table.";
                    b.Position = position;
                    candidate.Piles.Insert(second, b);
                }
            }
            return "Overlap resolution reached its bounded work limit.";
        }

        private void MergeNearby(BoardSnapshot candidate)
        {
            int count = candidate.Piles.Count;
            var parent = new int[count];
            var sizes = new int[count];
            for (int i = 0; i < count; i++) { parent[i] = i; sizes[i] = candidate.Piles[i].CardIds.Count; }
            var cards = CardMap(candidate);
            var index = new SpatialIndex(candidate.Piles, rules);
            var pairs = new List<NearbyPair>();
            for (int i = 0; i < count; i++)
            {
                var a = candidate.Piles[i];
                foreach (int j in index.Query(Rect(a).Expand(rules.SnapDistance)))
                {
                    if (j <= i) continue;
                    var b = candidate.Piles[j];
                    if (cards[a.CardIds[0]].Family != cards[b.CardIds[0]].Family) continue;
                    double distance = Rect(a).DistanceSquared(Rect(b));
                    if (distance <= (double)rules.SnapDistance * rules.SnapDistance)
                        pairs.Add(new NearbyPair { A = i, B = j, Distance = distance });
                }
            }
            pairs.Sort((a, b) =>
            {
                int distance = a.Distance.CompareTo(b.Distance);
                if (distance != 0) return distance;
                int first = a.A.CompareTo(b.A);
                return first != 0 ? first : a.B.CompareTo(b.B);
            });
            foreach (var pair in pairs)
            {
                int a = Root(parent, pair.A), b = Root(parent, pair.B);
                if (a == b || sizes[a] + sizes[b] > rules.MaxPileSize) continue;
                int root = Math.Min(a, b), child = Math.Max(a, b);
                parent[child] = root;
                sizes[root] += sizes[child];
            }
            var groups = new Dictionary<int, PileData>();
            var merged = new List<PileData>();
            for (int i = 0; i < count; i++)
            {
                int root = Root(parent, i);
                if (!groups.TryGetValue(root, out var pile))
                {
                    pile = new PileData { Id = candidate.Piles[root].Id };
                    groups.Add(root, pile);
                    merged.Add(pile);
                }
                pile.CardIds.AddRange(candidate.Piles[i].CardIds);
            }
            candidate.Piles = merged;
        }

        private bool PackColumns(BoardSnapshot candidate)
        {
            if (candidate.Piles.Count == 0) return true;
            int columns = Math.Min(candidate.Piles.Count,
                (int)Math.Floor((rules.Width + rules.Gap) / (rules.CardWidth + rules.Gap)));
            if (columns <= 0) return false;
            var loads = new float[columns];
            var order = new List<int>();
            for (int i = 0; i < candidate.Piles.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int byHeight = Rect(candidate.Piles[b]).Height.CompareTo(Rect(candidate.Piles[a]).Height);
                return byHeight != 0 ? byHeight : a.CompareTo(b);
            });
            foreach (int pileIndex in order)
            {
                int column = 0;
                for (int i = 1; i < columns; i++) if (loads[i] < loads[column]) column = i;
                float y = loads[column] == 0 ? 0 : loads[column] + rules.Gap;
                var pile = candidate.Piles[pileIndex];
                float height = Rect(pile).Height;
                if (y + height > rules.Height + 0.0001f) return false;
                pile.Position = new TablePoint(column * (rules.CardWidth + rules.Gap), y);
                loads[column] = y + height;
            }
            return true;
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
            if (snapshot == null) return "Table snapshot is missing.";
            if (snapshot.SchemaVersion != 1) return "Unsupported table schema version.";
            if (snapshot.Revision < 0) return "Table revision cannot be negative.";
            if (snapshot.Cards == null || snapshot.Piles == null) return "Card and pile lists are required.";
            if (snapshot.Cards.Count > rules.MaxCards || snapshot.Piles.Count > rules.MaxCards)
                return "The saved table exceeds the configured card limit.";
            var cards = new Dictionary<string, CardData>();
            foreach (var card in snapshot.Cards)
            {
                string error = ValidateCard(card, true);
                if (error != null) return error;
                if (cards.ContainsKey(card.Id)) return "Duplicate card ID.";
                cards.Add(card.Id, card);
            }
            var piles = new HashSet<string>();
            var membership = new HashSet<string>();
            foreach (var pile in snapshot.Piles)
            {
                if (pile == null || string.IsNullOrWhiteSpace(pile.Id) || !piles.Add(pile.Id))
                    return "Pile IDs must be nonempty and unique.";
                if (pile.CardIds == null || pile.CardIds.Count == 0 || pile.CardIds.Count > rules.MaxPileSize)
                    return "A pile is empty or exceeds its card limit.";
                if (!Inside(Rect(pile))) return "A pile is outside the table.";
                string family = null;
                foreach (string cardId in pile.CardIds)
                {
                    if (cardId == null || !cards.TryGetValue(cardId, out var card)) return "Pile refers to a missing card.";
                    if (!membership.Add(cardId)) return "A card belongs to more than one pile.";
                    if (family == null) family = card.Family;
                    else if (family != card.Family) return "A pile contains incompatible card families.";
                }
            }
            if (membership.Count != cards.Count) return "Every card must belong to exactly one pile.";
            var index = new SpatialIndex(snapshot.Piles, rules);
            for (int i = 0; i < snapshot.Piles.Count; i++)
            {
                var rect = Rect(snapshot.Piles[i]);
                foreach (int j in index.Query(rect))
                    if (j > i && rect.Overlaps(Rect(snapshot.Piles[j]))) return "Pile footprints overlap.";
            }
            return null;
        }

        private static string ValidateCard(CardData card, bool requireId)
        {
            if (card == null) return "Card is missing.";
            if (requireId && string.IsNullOrWhiteSpace(card.Id)) return "Card ID is required.";
            if (string.IsNullOrWhiteSpace(card.DefinitionId) || string.IsNullOrWhiteSpace(card.Family))
                return "Card definition and family are required.";
            if (card.Attributes == null) return "Card attributes list is required.";
            var keys = new HashSet<string>();
            foreach (var attribute in card.Attributes)
                if (attribute == null || string.IsNullOrWhiteSpace(attribute.Key) || !keys.Add(attribute.Key))
                    return "Card attribute keys must be nonempty and unique.";
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
                !Finite(rules.Width + 2 * rules.SnapDistance) || !Finite(rules.Height + 2 * rules.SnapDistance))
                throw new ArgumentException("Board geometry and limits must be finite, positive, and bounded.", nameof(rules));
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
        private static int Root(int[] parents, int node)
        {
            while (parents[node] != node)
            {
                parents[node] = parents[parents[node]];
                node = parents[node];
            }
            return node;
        }
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
                result.Piles.Add(new PileData
                {
                    Id = pile.Id,
                    Position = pile.Position,
                    CardIds = new List<string>(pile.CardIds)
                });
            return result;
        }
        private static CardData CopyCard(CardData card)
        {
            var result = new CardData
            {
                Id = card.Id, DefinitionId = card.DefinitionId, Label = card.Label,
                Family = card.Family, Rank = card.Rank, FaceUp = card.FaceUp
            };
            foreach (var attribute in card.Attributes)
                result.Attributes.Add(new CardAttribute { Key = attribute.Key, Value = attribute.Value });
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
        private struct NearbyPair
        {
            internal int A, B;
            internal double Distance;
        }
    }
}
