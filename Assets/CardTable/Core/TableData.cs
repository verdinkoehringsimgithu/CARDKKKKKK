using System;
using System.Collections.Generic;

namespace CardKKK.Tabletop.Core
{
    [Serializable]
    public struct TablePoint
    {
        public float X;
        public float Y;
        public TablePoint(float x, float y) { X = x; Y = y; }
    }

    [Serializable]
    public sealed class CardAttribute
    {
        public string Key;
        public int Value;
    }

    [Serializable]
    public sealed class CardData
    {
        public string Id;
        public string DefinitionId;
        public string Label;
        public string Family;
        public int Rank;
        public bool FaceUp = true;
        public List<CardAttribute> Attributes = new List<CardAttribute>();
    }

    [Serializable]
    public sealed class PileData
    {
        public string Id;
        public TablePoint Position;
        public List<string> CardIds = new List<string>();
    }

    [Serializable]
    public sealed class BoardSnapshot
    {
        public int SchemaVersion = 1;
        public int Revision;
        public List<CardData> Cards = new List<CardData>();
        public List<PileData> Piles = new List<PileData>();
    }

    [Serializable]
    public sealed class BoardRules
    {
        public float Width = 1600;
        public float Height = 900;
        public float CardWidth = 100;
        public float CardHeight = 140;
        public float PileOffset = 22;
        public float SnapDistance = 65;
        public float Gap = 12;
        public int MaxCards = 1000;
        public int MaxPileSize = 20;
        internal BoardRules Copy() { return (BoardRules)MemberwiseClone(); }
    }

    public sealed class OperationResult
    {
        public bool Success { get; }
        public string Error { get; }
        public string PileId { get; }
        public string CardId { get; }
        public OperationResult(bool success, string error = "", string pileId = null, string cardId = null)
        {
            Success = success;
            Error = error ?? "";
            PileId = pileId;
            CardId = cardId;
        }
        public static OperationResult Ok(string pileId = null, string cardId = null)
        {
            return new OperationResult(true, "", pileId, cardId);
        }
        public static OperationResult Fail(string error) { return new OperationResult(false, error); }
    }

    public sealed class DragInfo
    {
        public IReadOnlyList<string> CardIds { get; }
        public string SourcePileId { get; }
        internal DragInfo(string sourcePileId, IEnumerable<string> cardIds)
        {
            SourcePileId = sourcePileId;
            CardIds = new List<string>(cardIds).AsReadOnly();
        }
    }

    public sealed class DropPreview
    {
        public bool IsValid { get; }
        public TablePoint Position { get; }
        public string TargetPileId { get; }
        public int InsertionIndex { get; }
        public string Reason { get; }
        public IReadOnlyList<string> CardIds { get; }
        internal DropPreview(bool valid, TablePoint position, string target, int index,
            string reason, IReadOnlyList<string> cardIds)
        {
            IsValid = valid;
            Position = position;
            TargetPileId = target;
            InsertionIndex = index;
            Reason = reason ?? "";
            CardIds = cardIds ?? Array.Empty<string>();
        }
    }
}
