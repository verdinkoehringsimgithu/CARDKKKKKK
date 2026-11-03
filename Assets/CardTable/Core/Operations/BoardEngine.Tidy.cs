#if !CARDTABLE_DISABLE_TIDY
using System;
using System.Collections.Generic;

namespace CardKKK.Tabletop.Core
{
    public sealed partial class BoardEngine
    {
        public OperationResult TidyTable()
        {
            if ((rules.Features & TableFeatures.Tidy) == 0)
                return OperationResult.Fail("当前配置已关闭桌面整理功能。");
            string blocked = MutationBlock();
            if (blocked != null) return OperationResult.Fail(blocked);
            var candidate = Copy(state);
            if (rules.AutoMergeOnTidy) MergeNearby(candidate);
            if (!PackColumns(candidate)) return OperationResult.Fail("桌面空间不足，无法完成本次整理。");
            return Commit(candidate);
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
                    if (!CanStack(cards[a.CardIds[0]], cards[b.CardIds[0]],
                        a.CardIds.Count + b.CardIds.Count)) continue;
                    double distance = RectDistanceSquared(Rect(a), Rect(b));
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

        private static double RectDistanceSquared(TableRect first, TableRect second)
        {
            double dx = Math.Max(Math.Max(first.X - second.Right, 0), second.X - first.Right);
            double dy = Math.Max(Math.Max(first.Y - second.Top, 0), second.Y - first.Top);
            return dx * dx + dy * dy;
        }

        private static int Root(int[] parents, int node)
        {
            while (parents[node] != node)
            {
                parents[node] = parents[parents[node]];
                node = parents[node];
            }
            return node;
        }

        private struct NearbyPair
        {
            internal int A, B;
            internal double Distance;
        }
    }
}
#endif
