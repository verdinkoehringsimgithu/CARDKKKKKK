using System;
using System.Collections.Generic;
using System.IO;
using CardKKK.Tabletop.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.Tabletop.Unity
{
    public sealed class CardTableDemo : MonoBehaviour
    {
        [SerializeField] private CardTableTheme theme;
        private readonly Dictionary<string, TableCardView> views = new Dictionary<string, TableCardView>();
        private readonly Dictionary<string, CardData> cards = new Dictionary<string, CardData>();
        private readonly Dictionary<string, PileData> piles = new Dictionary<string, PileData>();
        private readonly List<TableCardView> visibleViews = new List<TableCardView>();
        private readonly Stack<TableCardView> freeViews = new Stack<TableCardView>();
        private readonly List<TableCardView> dragViews = new List<TableCardView>();
        private readonly List<string> removedIds = new List<string>();
        private BoardEngine engine;
        private BoardSaveStore saveStore;
        private BoardRules rules;
        private Font font;
        private RectTransform canvasRect;
        private RectTransform boardRect;
        private RectTransform cardLayer;
        private RectTransform dragLayer;
        private RectTransform previewRect;
        private RectTransform insertionRect;
        private Image previewImage;
        private Text statusText;
        private Text selectedText;
        private Text countsText;
        private AudioSource effects;
        private AudioSource music;
        private string selectedCardId;
        private string selectedPileId;
        private bool dragging;
        private Vector2 dragOffset;
        private Vector2 dragPosition;
        private int spawnedCount;

        public BoardEngine Engine => engine;
        public string SavePath => Path.Combine(Application.persistentDataPath, "card-table.json");

        private void Awake()
        {
            rules = new BoardRules();
            engine = new BoardEngine(rules);
            saveStore = new BoardSaveStore(SavePath);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildInterface();
            engine.Changed += Refresh;
            SeedCards();
            Refresh(true);
            SetStatus("Drag a pile. Hold SHIFT to pull one card. Matching families can stack.", false);
        }

        private void OnDestroy()
        {
            if (engine != null)
                engine.Changed -= Refresh;
        }

        private void OnDisable()
        {
            CancelCurrentDrag();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                CancelCurrentDrag();
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            foreach (TableCardView view in visibleViews)
                view.Animate(delta);
            if (Input.GetKeyDown(KeyCode.Escape))
                CancelCurrentDrag();
            if (dragging)
                return;
            if (Input.GetKeyDown(KeyCode.Space)) FlipSelected();
            if (Input.GetKeyDown(KeyCode.S)) SortSelected();
            if (Input.GetKeyDown(KeyCode.H)) ShuffleSelected();
            if (Input.GetKeyDown(KeyCode.P)) PopSelected();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) ReorderSelected(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) ReorderSelected(1);
            if (Input.GetKeyDown(KeyCode.R)) Run(engine.TidyTable, "Table arranged.");
            if (Input.GetKeyDown(KeyCode.O)) Run(engine.ResolveOverlaps, "Overlaps resolved.");
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F9)) Load();
        }

        private bool IsAnimating()
        {
            foreach (TableCardView view in visibleViews)
            {
                if (view.IsAnimating)
                    return true;
            }
            return false;
        }

        private bool CanInteract()
        {
            if (engine.IsDragging || dragging)
            {
                SetStatus("Finish the drag or press ESC first.", true);
                return false;
            }
            if (IsAnimating())
            {
                SetStatus("Cards are settling. Try again in a moment.", true);
                return false;
            }
            return true;
        }

        public void Select(TableCardView view)
        {
            if (!CanInteract())
                return;
            selectedCardId = view.CardId;
            selectedPileId = view.PileId;
            Show(engine.BringToFront(view.PileId), "Selected " + cards[view.CardId].Label);
            Refresh();
        }

        public void BeginCardDrag(TableCardView view, PointerEventData pointer)
        {
            if (!CanInteract())
                return;
            bool single = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            OperationResult result = engine.BeginDrag(view.PileId, single ? view.CardId : null);
            if (!result.Success)
            {
                Show(result, string.Empty);
                return;
            }
            selectedCardId = view.CardId;
            selectedPileId = view.PileId;
            dragging = true;
            PileData pile = piles[view.PileId];
            Vector2 start = single ? view.Position : ToVector(pile.Position);
            dragOffset = PointerPosition(pointer) - start;
            dragViews.Clear();
            foreach (string id in engine.CurrentDrag.CardIds)
            {
                TableCardView draggedView = views[id];
                draggedView.SetLayer(dragLayer);
                dragViews.Add(draggedView);
            }
            Play(theme != null ? theme.PickUpSound : null);
            UpdateCardDrag(pointer);
        }

        public void UpdateCardDrag(PointerEventData pointer)
        {
            if (!dragging)
                return;
            dragPosition = PointerPosition(pointer) - dragOffset;
            for (int index = 0; index < dragViews.Count; index++)
                dragViews[index].SetPosition(dragPosition + new Vector2(0, index * rules.PileOffset), dragLayer);
            DropPreview preview = engine.PreviewDrop(ToPoint(dragPosition));
            previewRect.gameObject.SetActive(true);
            previewRect.anchoredPosition = ToVector(preview.Position);
            int size = preview.CardIds.Count;
            if (!string.IsNullOrEmpty(preview.TargetPileId) && piles.TryGetValue(preview.TargetPileId, out PileData targetPile))
            {
                size += targetPile.CardIds.Count;
                if (preview.TargetPileId == engine.CurrentDrag.SourcePileId)
                    size -= preview.CardIds.Count;
            }
            previewRect.sizeDelta = new Vector2(rules.CardWidth, rules.CardHeight + Mathf.Max(0, size - 1) * rules.PileOffset);
            previewImage.color = preview.IsValid ? new Color(0.22f, 0.9f, 0.68f, 0.28f) : new Color(1, 0.28f, 0.28f, 0.35f);
            bool inserting = preview.IsValid && !string.IsNullOrEmpty(preview.TargetPileId);
            insertionRect.gameObject.SetActive(inserting);
            if (inserting)
            {
                insertionRect.anchoredPosition = ToVector(preview.Position) + new Vector2(-4, preview.InsertionIndex * rules.PileOffset);
                SetStatus("Insert at slot " + (preview.InsertionIndex + 1) + " — release to commit.", false);
            }
            else
            {
                SetStatus(preview.IsValid ? "Free placement — release to commit." : "Cannot drop: " + preview.Reason, !preview.IsValid);
            }
        }

        public void EndCardDrag(PointerEventData pointer)
        {
            if (!dragging)
                return;
            dragPosition = PointerPosition(pointer) - dragOffset;
            dragging = false;
            previewRect.gameObject.SetActive(false);
            insertionRect.gameObject.SetActive(false);
            OperationResult result = engine.CommitDrop(ToPoint(dragPosition));
            if (result.Success && !string.IsNullOrEmpty(result.PileId))
                selectedPileId = result.PileId;
            dragViews.Clear();
            Refresh();
            Show(result, "Drop committed.");
            Play(theme != null ? (result.Success ? theme.DropSound : theme.RejectedSound) : null);
        }

        private void CancelCurrentDrag()
        {
            if (engine == null || !engine.IsDragging)
                return;
            dragging = false;
            engine.CancelDrag();
            dragViews.Clear();
            if (previewRect != null) previewRect.gameObject.SetActive(false);
            if (insertionRect != null) insertionRect.gameObject.SetActive(false);
            Refresh();
            SetStatus("Drag cancelled; original placement restored.", false);
        }

        private Vector2 PointerPosition(PointerEventData pointer)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, pointer.position, pointer.pressEventCamera, out Vector2 local);
            return local;
        }

        private void FlipSelected()
        {
            if (!HasSelection() || !CanInteract()) return;
            OperationResult result = engine.FlipCard(selectedCardId);
            Show(result, "Card flipped.");
            if (result.Success) Play(theme != null ? theme.FlipSound : null);
        }

        private void SortSelected()
        {
            if (HasSelection()) Run(() => engine.SortPile(selectedPileId), "Pile sorted by rank.");
        }

        private void ShuffleSelected()
        {
            if (HasSelection()) Run(() => engine.ShufflePile(selectedPileId, Environment.TickCount), "Pile shuffled.");
        }

        private void PopSelected()
        {
            if (HasSelection()) Run(() => engine.PopCard(selectedPileId), "Top card popped to a free position.");
        }

        private void ReorderSelected(int direction)
        {
            if (!HasSelection()) return;
            int index = piles[selectedPileId].CardIds.IndexOf(selectedCardId);
            int target = Mathf.Clamp(index + direction, 0, piles[selectedPileId].CardIds.Count - 1);
            Run(() => engine.ReorderCard(selectedPileId, selectedCardId, target), "Card order changed.");
        }

        private bool HasSelection()
        {
            if (!string.IsNullOrEmpty(selectedCardId) && cards.ContainsKey(selectedCardId) && piles.ContainsKey(selectedPileId))
                return true;
            SetStatus("Select a card first.", true);
            return false;
        }

        private void Save()
        {
            Run(() => saveStore.Save(engine), "Saved to " + SavePath);
        }

        private void Load()
        {
            if (!CanInteract()) return;
            OperationResult result = saveStore.Load(engine);
            Show(result, saveStore.LoadedFromBackup ? "Backup save recovered." : "Save restored.");
        }

        private void Run(Func<OperationResult> command, string success)
        {
            if (!CanInteract()) return;
            Show(command(), success);
        }

        private void Show(OperationResult result, string success)
        {
            if (result.Success)
            {
                bool selectionChanged = false;
                if (!string.IsNullOrEmpty(result.CardId) && selectedCardId != result.CardId)
                {
                    selectedCardId = result.CardId;
                    selectionChanged = true;
                }
                if (!string.IsNullOrEmpty(result.PileId) && selectedPileId != result.PileId)
                {
                    selectedPileId = result.PileId;
                    selectionChanged = true;
                }
                if (selectionChanged)
                    Refresh();
            }
            SetStatus(result.Success ? success : result.Error, !result.Success);
        }

        private void SetStatus(string text, bool error)
        {
            if (statusText == null) return;
            statusText.text = text;
            statusText.color = error ? new Color(1, 0.56f, 0.49f) : new Color(0.64f, 0.88f, 0.84f);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null) effects.PlayOneShot(clip);
        }

        private void Spawn(string family)
        {
            if (!CanInteract()) return;
            int rank = spawnedCount % 9 + 1;
            CardData template = MakeTemplate(family, rank);
            OperationResult result = engine.Spawn(template, new TablePoint(30, 30));
            if (result.Success)
            {
                spawnedCount++;
            }
            Show(result, "Dealt a " + family + " card.");
        }

        private static CardData MakeTemplate(string family, int rank)
        {
            return new CardData
            {
                DefinitionId = family.ToLowerInvariant() + ".sample",
                Label = family + " " + rank,
                Family = family,
                Rank = rank,
                FaceUp = true,
                Attributes = new List<CardAttribute> { new CardAttribute { Key = "power", Value = rank } }
            };
        }

        private void SeedCards()
        {
            string[] families = { "Archive", "Supply", "Tool" };
            int[] ranks = { 3, 1, 2 };
            for (int family = 0; family < families.Length; family++)
            {
                TablePoint position = new TablePoint(160 + 420 * family, 320);
                for (int index = 0; index < ranks.Length; index++)
                {
                    OperationResult spawn = engine.Spawn(MakeTemplate(families[family], ranks[index]), new TablePoint(position.X + (index == 0 ? 0 : 160), position.Y));
                    if (!spawn.Success) continue;
                    spawnedCount++;
                    if (index > 0)
                    {
                        engine.BeginDrag(spawn.PileId);
                        engine.CommitDrop(position);
                    }
                }
            }
        }

        private void Refresh()
        {
            Refresh(false);
        }

        private void Refresh(bool immediate)
        {
            BoardSnapshot snapshot = engine.Capture();
            cards.Clear();
            piles.Clear();
            foreach (CardData card in snapshot.Cards) cards.Add(card.Id, card);
            foreach (PileData pile in snapshot.Piles) piles.Add(pile.Id, pile);
            removedIds.Clear();
            foreach (KeyValuePair<string, TableCardView> pair in views)
            {
                if (!cards.ContainsKey(pair.Key)) removedIds.Add(pair.Key);
            }
            foreach (string id in removedIds)
            {
                TableCardView view = views[id];
                view.gameObject.SetActive(false);
                freeViews.Push(view);
                views.Remove(id);
            }
            if (!string.IsNullOrEmpty(selectedCardId) && !cards.ContainsKey(selectedCardId))
                selectedCardId = selectedPileId = null;
            visibleViews.Clear();
            foreach (PileData pile in snapshot.Piles)
            {
                for (int index = 0; index < pile.CardIds.Count; index++)
                {
                    string id = pile.CardIds[index];
                    bool isNew = false;
                    if (!views.TryGetValue(id, out TableCardView view))
                    {
                        view = TakeView();
                        views.Add(id, view);
                        isNew = true;
                    }
                    if (id == selectedCardId) selectedPileId = pile.Id;
                    view.SetLayer(cardLayer);
                    view.Bind(cards[id], pile, index, ToVector(pile.Position) + new Vector2(0, index * rules.PileOffset), id == selectedCardId, immediate);
                    if (isNew && !immediate)
                        view.EnterFrom(new Vector2(rules.Width * 0.5f, rules.Height - rules.CardHeight));
                    visibleViews.Add(view);
                }
            }
            countsText.text = snapshot.Cards.Count + " CARDS    /    " + snapshot.Piles.Count + " PILES    /    REV " + snapshot.Revision;
            if (HasSelectedCard(out CardData selected))
            {
                PileData pile = piles[selectedPileId];
                selectedText.text = "SELECTED\n\n" + selected.Label + "\n" + selected.Family + " · rank " + selected.Rank + "\n\nSlot " + (pile.CardIds.IndexOf(selected.Id) + 1) + " / " + pile.CardIds.Count;
            }
            else selectedText.text = "SELECTED\n\nClick a card to inspect.\n\nMatching families stack.\nDifferent families stay apart.";
        }

        private bool HasSelectedCard(out CardData selected)
        {
            selected = null;
            return !string.IsNullOrEmpty(selectedCardId) && cards.TryGetValue(selectedCardId, out selected) && piles.ContainsKey(selectedPileId);
        }

        private TableCardView TakeView()
        {
            if (freeViews.Count > 0)
            {
                TableCardView recycled = freeViews.Pop();
                recycled.gameObject.SetActive(true);
                return recycled;
            }
            GameObject cardObject = new GameObject("Card", typeof(RectTransform));
            TableCardView view = cardObject.AddComponent<TableCardView>();
            view.Initialize(this, cardLayer, new Vector2(rules.CardWidth, rules.CardHeight), font, theme);
            return view;
        }

        private void BuildInterface()
        {
            GameObject cameraObject = new GameObject("Table Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -10);
            Camera tableCamera = cameraObject.GetComponent<Camera>();
            tableCamera.clearFlags = CameraClearFlags.SolidColor;
            tableCamera.backgroundColor = new Color(0.027f, 0.055f, 0.075f);
            tableCamera.cullingMask = 0;
            tableCamera.orthographic = true;
            Canvas canvas = new GameObject("Card Table Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)canvas.transform;
            MakeImage(canvasRect, "Background", Vector2.zero, new Vector2(1920, 1080), new Color(0.027f, 0.055f, 0.075f));
            Text title = MakeText(canvasRect, "Title", new Vector2(40, 993), new Vector2(1000, 48), 32, new Color(0.91f, 0.94f, 0.89f), TextAnchor.MiddleLeft);
            title.text = "CARD TABLE / FOUNDATION";
            Text subtitle = MakeText(canvasRect, "Subtitle", new Vector2(42, 958), new Vector2(1500, 30), 17, new Color(0.53f, 0.68f, 0.72f), TextAnchor.MiddleLeft);
            subtitle.text = "MOUSE TO MOVE A PILE     ·     SHIFT + DRAG FOR ONE CARD     ·     ESC TO CANCEL";
            boardRect = MakeRect(canvasRect, "Board", new Vector2(40, 55), new Vector2(rules.Width, rules.Height));
            Image table = boardRect.gameObject.AddComponent<Image>();
            table.color = theme != null ? theme.TableColor : new Color(0.055f, 0.13f, 0.17f);
            table.sprite = theme != null ? theme.TableSurface : null;
            table.raycastTarget = false;
            for (int x = 100; x < rules.Width; x += 100)
                MakeImage(boardRect, "Grid", new Vector2(x, 0), new Vector2(1, rules.Height), new Color(0.2f, 0.4f, 0.43f, 0.11f));
            for (int y = 100; y < rules.Height; y += 100)
                MakeImage(boardRect, "Grid", new Vector2(0, y), new Vector2(rules.Width, 1), new Color(0.2f, 0.4f, 0.43f, 0.11f));
            previewImage = MakeImage(boardRect, "Drop Preview", Vector2.zero, new Vector2(rules.CardWidth, rules.CardHeight), new Color(0.2f, 0.8f, 0.6f, 0.3f));
            previewRect = previewImage.rectTransform;
            previewRect.gameObject.SetActive(false);
            cardLayer = MakeRect(boardRect, "Cards", Vector2.zero, new Vector2(rules.Width, rules.Height));
            Image insertion = MakeImage(boardRect, "Insertion Marker", Vector2.zero, new Vector2(rules.CardWidth + 8, 5), new Color(0.4f, 1, 0.77f));
            insertionRect = insertion.rectTransform;
            insertionRect.gameObject.SetActive(false);
            dragLayer = MakeRect(boardRect, "Dragging Cards", Vector2.zero, new Vector2(rules.Width, rules.Height));
            RectTransform side = MakeRect(canvasRect, "Actions", new Vector2(1660, 55), new Vector2(220, 900));
            MakeImage(side, "Panel", Vector2.zero, new Vector2(220, 900), new Color(0.055f, 0.095f, 0.12f));
            selectedText = MakeText(side, "Selection", new Vector2(15, 718), new Vector2(190, 165), 16, new Color(0.79f, 0.87f, 0.87f), TextAnchor.UpperLeft);
            float yButton = 668;
            AddButton(side, ref yButton, "Deal Archive", () => Spawn("Archive"));
            AddButton(side, ref yButton, "Deal Supply", () => Spawn("Supply"));
            AddButton(side, ref yButton, "Deal Tool", () => Spawn("Tool"));
            yButton -= 12;
            AddButton(side, ref yButton, "Flip card     [SPACE]", FlipSelected);
            AddButton(side, ref yButton, "Sort pile          [S]", SortSelected);
            AddButton(side, ref yButton, "Shuffle pile     [H]", ShuffleSelected);
            AddButton(side, ref yButton, "Pop card         [P]", PopSelected);
            AddButton(side, ref yButton, "Earlier slot        [←]", () => ReorderSelected(-1));
            AddButton(side, ref yButton, "Later slot           [→]", () => ReorderSelected(1));
            yButton -= 12;
            AddButton(side, ref yButton, "Tidy table        [R]", () => Run(engine.TidyTable, "Table arranged."));
            AddButton(side, ref yButton, "Resolve overlap [O]", () => Run(engine.ResolveOverlaps, "Overlaps resolved."));
            yButton -= 12;
            AddButton(side, ref yButton, "Save                [F5]", Save);
            AddButton(side, ref yButton, "Load                [F9]", Load);
            countsText = MakeText(canvasRect, "Counts", new Vector2(1260, 1000), new Vector2(620, 32), 16, new Color(0.49f, 0.84f, 0.77f), TextAnchor.MiddleRight);
            statusText = MakeText(canvasRect, "Status", new Vector2(40, 12), new Vector2(1840, 30), 16, new Color(0.64f, 0.88f, 0.84f), TextAnchor.MiddleLeft);
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.volume = 0.35f;
            if (theme != null && theme.Music != null)
            {
                music.clip = theme.Music;
                music.Play();
            }
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void AddButton(RectTransform parent, ref float y, string label, Action action)
        {
            RectTransform rect = MakeRect(parent, label, new Vector2(12, y), new Vector2(196, 36));
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.13f, 0.23f, 0.28f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            Text caption = MakeText(rect, "Label", Vector2.zero, new Vector2(196, 36), 15, new Color(0.87f, 0.93f, 0.91f), TextAnchor.MiddleCenter);
            caption.text = label;
            y -= 43;
        }

        private static RectTransform MakeRect(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public Image MakeImage(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            Image image = MakeRect(parent, name, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public Text MakeText(RectTransform parent, string name, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment)
        {
            Text text = MakeRect(parent, name, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Vector2 ToVector(TablePoint point) => new Vector2(point.X, point.Y);
        private static TablePoint ToPoint(Vector2 vector) => new TablePoint(vector.x, vector.y);
    }
}
