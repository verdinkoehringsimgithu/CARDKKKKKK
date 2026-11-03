using System;
using System.Collections.Generic;

namespace CardKKK.Tabletop.Core
{
    internal readonly struct TableRect
    {
        internal readonly float X, Y, Width, Height;
        internal float Right => X + Width;
        internal float Top => Y + Height;
        internal TableRect(float x, float y, float width, float height)
        {
            X = x; Y = y; Width = width; Height = height;
        }
        internal bool Overlaps(TableRect other)
        {
            const float epsilon = 0.0001f;
            return X < other.Right - epsilon && Right > other.X + epsilon &&
                   Y < other.Top - epsilon && Top > other.Y + epsilon;
        }
        internal TableRect Expand(float distance)
        {
            return new TableRect(X - distance, Y - distance,
                Width + 2 * distance, Height + 2 * distance);
        }
        internal double DistanceSquared(TablePoint point)
        {
            double dx = Math.Max(Math.Max(X - point.X, 0), point.X - Right);
            double dy = Math.Max(Math.Max(Y - point.Y, 0), point.Y - Top);
            return dx * dx + dy * dy;
        }
        internal double DistanceSquared(TableRect other)
        {
            double dx = Math.Max(Math.Max(X - other.Right, 0), other.X - Right);
            double dy = Math.Max(Math.Max(Y - other.Top, 0), other.Y - Top);
            return dx * dx + dy * dy;
        }
    }

    // The grid only finds candidates. Exact rectangles decide every collision.
    internal sealed class SpatialIndex
    {
        private const int MaxCells = 4096;
        private readonly float cellSize;
        private readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
        private readonly List<int> oversized = new List<int>();
        private readonly int count;
        private readonly int[] seen;
        private int stamp;
        internal SpatialIndex(IReadOnlyList<PileData> piles, BoardRules rules)
        {
            cellSize = Math.Max(rules.CardWidth, rules.CardHeight);
            count = piles.Count;
            seen = new int[count];
            for (int i = 0; i < piles.Count; i++)
            {
                var pile = piles[i];
                Add(i, new TableRect(pile.Position.X, pile.Position.Y,
                    rules.CardWidth, rules.CardHeight + (pile.CardIds.Count - 1) * rules.PileOffset));
            }
        }
        private static long Key(int x, int y) { return ((long)x << 32) ^ (uint)y; }
        private bool CellRange(TableRect rect, out int left, out int right, out int bottom, out int top)
        {
            double l = Math.Floor(rect.X / cellSize), r = Math.Floor(rect.Right / cellSize);
            double b = Math.Floor(rect.Y / cellSize), t = Math.Floor(rect.Top / cellSize);
            left = right = bottom = top = 0;
            if (l < int.MinValue || r >= int.MaxValue || b < int.MinValue || t >= int.MaxValue ||
                (r - l + 1) * (t - b + 1) > MaxCells) return false;
            left = (int)l; right = (int)r; bottom = (int)b; top = (int)t;
            return true;
        }
        private void Add(int index, TableRect rect)
        {
            if (!CellRange(rect, out int left, out int right, out int bottom, out int top))
            {
                oversized.Add(index);
                return;
            }
            for (int x = left; x <= right; x++)
            for (int y = bottom; y <= top; y++)
            {
                long key = Key(x, y);
                if (!cells.TryGetValue(key, out var bucket))
                {
                    bucket = new List<int>();
                    cells.Add(key, bucket);
                }
                bucket.Add(index);
            }
        }
        internal List<int> Query(TableRect area)
        {
            var result = new List<int>();
            QueryInto(area, result);
            return result;
        }
        internal void QueryInto(TableRect area, List<int> result)
        {
            result.Clear();
            if (stamp == int.MaxValue)
            {
                Array.Clear(seen, 0, seen.Length);
                stamp = 0;
            }
            stamp++;
            foreach (int index in oversized) AddResult(result, index);
            if (!CellRange(area, out int left, out int right, out int bottom, out int top))
            {
                for (int i = 0; i < count; i++) AddResult(result, i);
            }
            else
            {
                for (int x = left; x <= right; x++)
                for (int y = bottom; y <= top; y++)
                    if (cells.TryGetValue(Key(x, y), out var bucket))
                        foreach (int index in bucket) AddResult(result, index);
            }
            result.Sort();
        }
        private void AddResult(List<int> result, int index)
        {
            if (seen[index] == stamp) return;
            seen[index] = stamp;
            result.Add(index);
        }
    }
}
