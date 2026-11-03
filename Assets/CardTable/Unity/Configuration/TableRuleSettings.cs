using System;
using CardKKK.Tabletop.Core;
using UnityEngine;

namespace CardKKK.Tabletop.Unity
{
    [Serializable]
    public sealed class TableRuleSettings
    {
        [InspectorName("桌面宽度"), Min(1)] public float Width = 1600;
        [InspectorName("桌面高度"), Min(1)] public float Height = 900;
        [InspectorName("卡牌宽度"), Min(1)] public float CardWidth = 100;
        [InspectorName("卡牌高度"), Min(1)] public float CardHeight = 140;
        [InspectorName("堆内纵向错开"), Min(0.01f)] public float PileOffset = 22;
        [InspectorName("吸附距离"), Min(0)] public float SnapDistance = 65;
        [InspectorName("整理与空位间距"), Min(0)] public float Gap = 12;
        [InspectorName("全桌卡牌上限"), Range(1, 10000)] public int MaxCards = 1000;
        [InspectorName("单堆卡牌上限"), Range(1, 10000)] public int MaxPileSize = 20;
        [InspectorName("允许堆叠")] public bool AllowStacking = true;
        [InspectorName("允许整堆拖动")] public bool AllowWholePileDrag = true;
        [InspectorName("允许拆出单张拖动")] public bool AllowSingleCardDrag = true;
        [InspectorName("生成重叠时合并兼容牌堆")] public bool AutoMergeOnSpawn = true;
        [InspectorName("整理时合并附近兼容牌堆")] public bool AutoMergeOnTidy = true;
        [InspectorName("堆叠兼容方式")] public StackRule StackRule = StackRule.SameFamily;
        [InspectorName("牌堆排序方式")] public CardSortRule SortRule = CardSortRule.RankAscending;

        public BoardRules ToRules()
        {
            return new BoardRules
            {
                Width = Width, Height = Height, CardWidth = CardWidth, CardHeight = CardHeight,
                PileOffset = PileOffset, SnapDistance = SnapDistance, Gap = Gap,
                MaxCards = MaxCards, MaxPileSize = MaxPileSize,
                AllowStacking = AllowStacking, AllowWholePileDrag = AllowWholePileDrag,
                AllowSingleCardDrag = AllowSingleCardDrag, AutoMergeOnSpawn = AutoMergeOnSpawn,
                AutoMergeOnTidy = AutoMergeOnTidy, StackRule = StackRule, SortRule = SortRule
            };
        }
    }

    [Serializable]
    public sealed class FeatureSelection
    {
        [InspectorName("翻面")] public bool Flip = true;
        [InspectorName("牌堆排序")] public bool Sort = true;
        [InspectorName("洗牌")] public bool Shuffle = true;
        [InspectorName("堆内调序")] public bool Reorder = true;
        [InspectorName("抽出顶牌")] public bool Pop = true;
        [InspectorName("整理桌面")] public bool Tidy = true;
        [InspectorName("保存与读取")] public bool Save = true;

        public TableFeatures ToFlags()
        {
            TableFeatures flags = TableFeatures.None;
            if (Flip) flags |= TableFeatures.Flip;
            if (Sort) flags |= TableFeatures.Sort;
            if (Shuffle) flags |= TableFeatures.Shuffle;
            if (Reorder) flags |= TableFeatures.Reorder;
            if (Pop) flags |= TableFeatures.Pop;
            if (Tidy) flags |= TableFeatures.Tidy;
            if (Save) flags |= TableFeatures.Save;
            return flags;
        }
    }

    [Serializable]
    public sealed class InitialPile
    {
        [InspectorName("底牌左下角位置")] public Vector2 Position;
        [InspectorName("卡牌顺序（从底到顶）")] public System.Collections.Generic.List<CardDefinition> Cards
            = new System.Collections.Generic.List<CardDefinition>();
    }
}
