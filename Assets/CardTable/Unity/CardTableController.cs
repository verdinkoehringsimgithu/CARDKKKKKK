using System;
using System.Collections.Generic;
#if !CARDTABLE_DISABLE_SAVE
using System.IO;
#endif
using CardKKK.Tabletop.Core;
using UnityEngine;

namespace CardKKK.Tabletop.Unity
{
    public enum TableAction
    {
        [InspectorName("生成卡牌")] Spawn,
        [InspectorName("翻面")] Flip,
        [InspectorName("整理牌堆")] Sort,
        [InspectorName("打乱牌序")] Shuffle,
        [InspectorName("堆内前移")] ReorderEarlier,
        [InspectorName("堆内后移")] ReorderLater,
        [InspectorName("抽出顶牌")] Pop,
        [InspectorName("整理桌面")] Tidy,
        [InspectorName("保存桌面")] Save,
        [InspectorName("读取桌面")] Load
    }
    public enum TableSound { PickUp, Drop, Rejected, Flip }

    /// <summary>按钮和快捷键共用此命令入口，不依赖场景层级或牌面控件。</summary>
    public sealed class CardTableController : IDisposable
    {
        private readonly CardCatalog catalog;
        private readonly Func<bool> animationBusy;
        private readonly Dictionary<string, CardData> cards = new Dictionary<string, CardData>();
        private readonly Dictionary<string, PileData> piles = new Dictionary<string, PileData>();
#if !CARDTABLE_DISABLE_SAVE
        private readonly BoardSaveStore saves;
#endif
        public CardTableConfiguration Config { get; }
        public BoardEngine Engine { get; }
        public BoardSnapshot Snapshot { get; private set; }
        public string SelectedCardId { get; private set; }
        public string SelectedPileId { get; private set; }
        public event Action StateChanged;
        public event Action SelectionChanged;
        public event Action DragCancelled;
        public event Action<string> Message;
        public event Action<TableSound> Sound;

        public CardTableController(CardTableConfiguration config, Func<bool> animationBusy)
        {
            Config = config;
            this.animationBusy = animationBusy;
            catalog = config.BuildCatalog();
            Engine = new BoardEngine(config.BuildRules(), catalog);
#if !CARDTABLE_DISABLE_SAVE
            saves = new BoardSaveStore(Path.Combine(Application.persistentDataPath, config.SaveFileName), catalog);
#endif
            var initial = new BoardSnapshot();
            foreach (InitialPile group in config.InitialPiles)
            {
                var pile = new PileData { Id = Guid.NewGuid().ToString("N"), Position = ToPoint(group.Position) };
                foreach (CardDefinition definition in group.Cards)
                {
                    CardData card = catalog.CreateCard(definition.Id);
                    card.Id = Guid.NewGuid().ToString("N");
                    initial.Cards.Add(card);
                    pile.CardIds.Add(card.Id);
                }
                if (pile.CardIds.Count > 0) initial.Piles.Add(pile);
            }
            OperationResult result = Engine.Restore(initial);
            if (!result.Success) throw new InvalidOperationException(result.Error);
            CaptureState();
            Engine.Changed += CaptureState;
        }

        public CardData FindCard(string id) => id != null && cards.TryGetValue(id, out CardData card) ? card : null;
        public PileData FindPile(string id) => id != null && piles.TryGetValue(id, out PileData pile) ? pile : null;

        public bool IsAvailable(TableAction action)
        {
            switch (action)
            {
                case TableAction.Spawn: return true;
#if !CARDTABLE_DISABLE_FLIP
                case TableAction.Flip: return Enabled(TableFeatures.Flip);
#endif
#if !CARDTABLE_DISABLE_SORT
                case TableAction.Sort: return Enabled(TableFeatures.Sort);
#endif
#if !CARDTABLE_DISABLE_SHUFFLE
                case TableAction.Shuffle: return Enabled(TableFeatures.Shuffle);
#endif
#if !CARDTABLE_DISABLE_REORDER
                case TableAction.ReorderEarlier:
                case TableAction.ReorderLater: return Enabled(TableFeatures.Reorder);
#endif
#if !CARDTABLE_DISABLE_POP
                case TableAction.Pop: return Enabled(TableFeatures.Pop);
#endif
#if !CARDTABLE_DISABLE_TIDY
                case TableAction.Tidy: return Enabled(TableFeatures.Tidy);
#endif
#if !CARDTABLE_DISABLE_SAVE
                case TableAction.Save:
                case TableAction.Load: return Enabled(TableFeatures.Save);
#endif
                default: return false;
            }
        }

        public void Execute(TableAction action, string definitionId = null)
        {
            if (!IsAvailable(action)) { Tell("此功能未启用。"); return; }
            if (!CanInteract()) return;
            OperationResult result;
            string message = "";
            PileData selected = FindPile(SelectedPileId);
            switch (action)
            {
                case TableAction.Spawn:
                    if (string.IsNullOrEmpty(definitionId) || !catalog.TryGet(definitionId, out _))
                    { Tell("没有可发的牌。"); return; }
                    BoardRules rules = Engine.Rules;
                    result = Engine.Spawn(catalog.CreateCard(definitionId), new TablePoint(rules.Width * 0.5f, rules.Height * 0.5f));
                    break;
#if !CARDTABLE_DISABLE_FLIP
                case TableAction.Flip:
                    if (selected == null) { Tell("请先选中卡牌。"); return; }
                    result = Engine.FlipCard(SelectedCardId); break;
#endif
#if !CARDTABLE_DISABLE_SORT
                case TableAction.Sort:
                    if (selected == null) { Tell("请先选中牌堆。"); return; }
                    result = Engine.SortPile(selected.Id); break;
#endif
#if !CARDTABLE_DISABLE_SHUFFLE
                case TableAction.Shuffle:
                    if (selected == null) { Tell("请先选中牌堆。"); return; }
                    result = Engine.ShufflePile(selected.Id, UnityEngine.Random.Range(0, int.MaxValue)); break;
#endif
#if !CARDTABLE_DISABLE_REORDER
                case TableAction.ReorderEarlier:
                case TableAction.ReorderLater:
                    if (selected == null) { Tell("请先选中卡牌。"); return; }
                    int index = selected.CardIds.IndexOf(SelectedCardId) + (action == TableAction.ReorderEarlier ? -1 : 1);
                    result = Engine.ReorderCard(selected.Id, SelectedCardId, index); break;
#endif
#if !CARDTABLE_DISABLE_POP
                case TableAction.Pop:
                    if (selected == null) { Tell("请先选中牌堆。"); return; }
                    result = Engine.PopCard(selected.Id); break;
#endif
#if !CARDTABLE_DISABLE_TIDY
                case TableAction.Tidy:
                    result = Engine.TidyTable(); break;
#endif
#if !CARDTABLE_DISABLE_SAVE
                case TableAction.Save:
                    result = saves.Save(Engine); message = "已保存"; break;
                case TableAction.Load:
                    result = saves.Load(Engine); message = saves.LoadedFromBackup ? "已从备份读取" : "已读取"; break;
#endif
                default: Tell("此功能未启用。"); return;
            }
            if (result.Success)
            {
                if (result.CardId != null) SetSelection(result.CardId, result.PileId);
                Tell(message);
#if !CARDTABLE_DISABLE_FLIP
                if (action == TableAction.Flip) Sound?.Invoke(TableSound.Flip);
#endif
            }
            else { Tell(result.Error); Sound?.Invoke(TableSound.Rejected); }
        }

        public void Select(string cardId, string pileId)
        {
            if (!CanInteract()) return;
            SetSelection(cardId, pileId);
            Engine.BringToFront(pileId);
        }

        public bool BeginDrag(string cardId, string pileId, bool singleCard)
        {
            if (!CanInteract()) return false;
            OperationResult result = Engine.BeginDrag(pileId, singleCard ? cardId : null);
            if (!result.Success) { Tell(result.Error); return false; }
            SetSelection(cardId, pileId);
            Sound?.Invoke(TableSound.PickUp);
            return true;
        }

        public DropPreview Preview(Vector2 position) => Engine.PreviewDrop(ToPoint(position));
        public void CommitDrag(Vector2 position)
        {
            OperationResult result = Engine.CommitDrop(ToPoint(position));
            if (result.Success)
            {
                UpdateSelectionPile(); SelectionChanged?.Invoke();
                Tell(""); Sound?.Invoke(TableSound.Drop);
            }
            else { Tell(result.Error); Sound?.Invoke(TableSound.Rejected); }
            DragCancelled?.Invoke();
        }
        public void CancelDrag()
        {
            if (!Engine.IsDragging) return;
            Engine.CancelDrag(); DragCancelled?.Invoke(); Tell("");
        }
        public void Tell(string message) => Message?.Invoke(message);
        public void Dispose() => Engine.Changed -= CaptureState;
        private bool Enabled(TableFeatures feature) => (Engine.Rules.Features & feature) != 0;
        private bool CanInteract()
        {
            if (Engine.IsDragging) { Tell("先放下卡牌。"); return false; }
            if (animationBusy != null && animationBusy()) return false;
            return true;
        }
        private void SetSelection(string cardId, string pileId)
        { SelectedCardId = cardId; SelectedPileId = pileId; SelectionChanged?.Invoke(); }
        private void CaptureState()
        {
            Snapshot = Engine.Capture(); cards.Clear(); piles.Clear();
            foreach (CardData card in Snapshot.Cards) cards.Add(card.Id, card);
            foreach (PileData pile in Snapshot.Piles) piles.Add(pile.Id, pile);
            UpdateSelectionPile(); StateChanged?.Invoke();
        }
        private void UpdateSelectionPile()
        {
            if (SelectedCardId == null) return;
            foreach (PileData pile in Snapshot.Piles)
                if (pile.CardIds.Contains(SelectedCardId)) { SelectedPileId = pile.Id; return; }
            SelectedCardId = SelectedPileId = null;
        }
        private static TablePoint ToPoint(Vector2 point) => new TablePoint(point.x, point.y);
    }
}
