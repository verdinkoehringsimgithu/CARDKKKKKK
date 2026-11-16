using System.Collections.Generic;
using System.Linq;
using CardKKK.IslandSurvival.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CardKKK.IslandSurvival.Unity
{
    public sealed partial class VoyageView
    {
        [SerializeField] private VoyageSlotView slotTemplate;
        [SerializeField] private RectTransform dropGhost;
        private readonly Dictionary<int, float> preparationScroll = new Dictionary<int, float>();
        private readonly Dictionary<int, VoyageSlotView> slotViews = new Dictionary<int, VoyageSlotView>();
        private readonly Dictionary<int, Vector3> dragOffsets = new Dictionary<int, Vector3>();
        private readonly List<VoyageCardView> retiredCards = new List<VoyageCardView>();
        private int mergeTarget;
        private PointerEventData dragPointer;
        private VoyageDeskRules deskRules;
        private bool singleDrag;
        private int guidanceSlot = -1;
        private float guidanceUntil, panelTime, nextDropRefresh;
        private CanvasGroup preparationFade;

        private VoyageSlotView FindSlotView(int location, int index)
        {
            if (openTrays.TryGetValue(location, out VoyageTrayInstance tray) && tray.SlotViews.TryGetValue(index, out VoyageSlotView view))
                return view;
            return null;
        }

        public VoyageDeskGeometry MeasureDesk()
        {
            Canvas.ForceUpdateCanvases();
            return new VoyageDeskGeometry(tabletop.rect.width, tabletop.rect.height, cardTemplate.Rect.rect.width, cardTemplate.Rect.rect.height);
        }

        private void DisplaySlots()
        {
            foreach (var tray in openTrays.Values)
            {
                IReadOnlyList<VoyageSlotOffer> slots = engine.Slots(tray.LocationId);
                var live = new HashSet<int>(slots.Select(s => s.Index));
                foreach (int index in tray.SlotViews.Keys.Where(index => !live.Contains(index)).ToArray())
                {
                    if (tray.SlotViews[index] != null) Destroy(tray.SlotViews[index].gameObject);
                    tray.SlotViews.Remove(index);
                }
                foreach (VoyageSlotOffer slot in slots)
                {
                    if (!tray.SlotViews.TryGetValue(slot.Index, out VoyageSlotView view) || view == null)
                    {
                        view = Instantiate(slotTemplate, tray.StageCards, false);
                        tray.SlotViews[slot.Index] = view;
                    }
                    view.Rect.anchoredPosition = SlotPosition(slot.Index);
                    view.transform.SetAsFirstSibling();
                    view.Display(this, slot);
                    view.gameObject.SetActive(true);
                }
                SetHeight(tray.StageCards, Mathf.Max(190, Mathf.CeilToInt(slots.Count / 3f) * 180 + 8));
            }
        }

        private static Vector2 SlotPosition(int index) => new Vector2(8 + index % 3 * 138, -5 - index / 3 * 180);

        internal void SelectSlot(int location, int index)
        {
            if (dragging != null || menuPanel.activeSelf || readPanel.activeSelf) return;
            VoyageSlotOffer slot = engine.Slots(location).FirstOrDefault(item => item.Index == index);
            if (slot == null || slot.Locked) return;
            if (guidanceSlot == index && guidanceLocation == location)
            {
                guidanceSlot = -1;
                guidanceLocation = -1;
                RefreshGuidance();
                return;
            }
            guidanceLocation = location;
            guidanceSlot = index;
            guidanceUntil = Time.unscaledTime + 6f;
            RefreshGuidance();
        }

        private void RefreshGuidance()
        {
            if (engine == null || state == null) return;
            foreach (VoyageCardState card in state.Cards)
            {
                if (!cardViews.TryGetValue(card.Id, out VoyageCardView view) || view == null) continue;
                bool candidate = false;
                if (dragging != null && !dragOffsets.ContainsKey(card.Id))
                {
                    bool isSlotOccupant = false;
                    foreach (var tray in openTrays.Values)
                    {
                        VoyageSlotOffer occ = engine.Slots(tray.LocationId).FirstOrDefault(s => s.CardId == card.Id);
                        if (occ != null)
                        {
                            isSlotOccupant = true;
                            if (engine.CanPut(dragging.CardId, occ.Location, occ.Index, out _)) candidate = true;
                            break;
                        }
                    }
                    if (!isSlotOccupant)
                    {
                        candidate = engine.IsLocation(card.Id)
                            ? engine.FindAcceptingSlot(dragging.CardId, card.Id) >= 0
                            : engine.CanMerge(dragging.CardId, card.Id, out _) || engine.CanStack(dragging.CardId, card.Id, singleDrag, out _) || engine.CanStagePair(card.Id, dragging.CardId);
                    }
                }
                else if (guidanceSlot >= 0 && guidanceLocation >= 0)
                {
                    candidate = engine.CanPut(card.Id, guidanceLocation, guidanceSlot, out _);
                }
                view.Guide(candidate);
            }
            foreach (var tray in openTrays.Values)
            {
                IReadOnlyList<VoyageSlotOffer> offers = engine.Slots(tray.LocationId);
                foreach (var pair in tray.SlotViews)
                {
                    VoyageSlotOffer offer = offers.FirstOrDefault(slot => slot.Index == pair.Key);
                    if (offer == null) continue;
                    pair.Value.Display(this, offer);
                    pair.Value.Guide(dragging != null && engine.CanPut(dragging.CardId, offer.Location, offer.Index, out _),
                        guidanceLocation == tray.LocationId && guidanceSlot == pair.Key);
                }
            }
        }

        internal bool BeginDrag(VoyageCardView card)
        {
            if (state == null || dragging != null || menuPanel.activeSelf || readPanel.activeSelf) return false;
            bool single = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!engine.CanDrag(card.CardId, single, out string reason)) { Message(reason); Reject(); return false; }
            singleDrag = single;
            dragging = card;
            selected = card.CardId;
            guidanceSlot = -1;
            tableScroll.StopMovement();
            stageScroll.StopMovement();
            dragOffsets.Clear();
            var dragList = new List<int>();
            if (multiSelected.Contains(card.CardId))
                dragList.AddRange(multiSelected.Where(id => engine.CanDrag(id, true, out _)));
            else
                dragList.AddRange(engine.DragCards(card.CardId, single));
            foreach (int id in dragList)
            {
                if (!cardViews.TryGetValue(id, out VoyageCardView member)) continue;
                dragOffsets.Add(id, dragLayer.InverseTransformVector(member.Rect.position - card.Rect.position));
                member.SetHeld(true);
                member.Rect.SetParent(dragLayer, true);
                member.Rect.localScale = Vector3.one;
                member.Rect.SetAsLastSibling();
                engine.Hold(id, true);
            }
            RefreshGuidance();
            PlayFeedback(dragPaper);
            return true;
        }

        internal void DragMoved(VoyageCardView card, PointerEventData pointer)
        {
            if (dragging != card) return;
            dragPointer = pointer;
            foreach (var member in dragOffsets)
                if (member.Key != card.CardId && cardViews.TryGetValue(member.Key, out VoyageCardView view))
                    view.Rect.position = card.Rect.position + dragLayer.TransformVector(member.Value);
            PreviewDrop(card, pointer);
        }

        private void FinishDragSession()
        {
            if (hoveredSlot != null)
            {
                hoveredSlot.SetHoverHighlight(false, false);
                hoveredSlot = null;
            }
            foreach (int id in dragOffsets.Keys)
            {
                engine?.Hold(id, false);
                if (cardViews.TryGetValue(id, out VoyageCardView member) && member != null) member.FinishVisualDrag();
            }
            dragOffsets.Clear();
            if (dropGhost != null) dropGhost.gameObject.SetActive(false);
            dragging = null;
            dragPointer = null;
        }

        private void AbortDrag()
        {
            if (dragging == null && dragOffsets.Count == 0) return;
            FinishDragSession();
            if (isActiveAndEnabled) RefreshGuidance();
        }

        private enum DropAction { Cancel, Put, Merge, Stack, Pair, Desk }

        private struct DropIntent
        {
            public DropAction Action;
            public int Target, Location, Slot;
            public float X, Y;
        }

        // Resolve once from the current pointer and committed state. Both preview and release use this route.
        private DropIntent ResolveDrop(VoyageCardView source, PointerEventData pointer)
        {
            var intent = new DropIntent();
            if (pointer == null || menuPanel.activeSelf || readPanel.activeSelf ||
                tasksPanel.activeSelf && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)tasksPanel.transform,
                    pointer.position, pointer.pressEventCamera)) return intent;
            if (!ReadDropTarget(pointer, out VoyageSlotView slot, out VoyageCardView target)) return intent;
            int id = source.CardId;
            foreach (var tray in openTrays.Values)
            {
                bool inTray = RectTransformUtility.RectangleContainsScreenPoint(tray.Rect, pointer.position, pointer.pressEventCamera);
                bool inSlots = inTray && tray.StageDrop != null && RectTransformUtility.RectangleContainsScreenPoint(tray.StageDrop, pointer.position, pointer.pressEventCamera);
                if (inTray && !inSlots) return intent;
                if (inSlots)
                {
                    int index = slot != null && slot.Location == tray.LocationId ? slot.Index : target != null
                        ? engine.Slots(tray.LocationId).FirstOrDefault(item => item.CardId == target.CardId)?.Index ?? -1 : -1;
                    if (index < 0) index = engine.FindAcceptingSlot(id, tray.LocationId);
                    intent.Action = DropAction.Put;
                    intent.Location = tray.LocationId;
                    intent.Slot = index;
                    return intent;
                }
            }
            if (!RectTransformUtility.RectangleContainsScreenPoint(tabletopViewport, pointer.position, pointer.pressEventCamera)) return intent;
            Vector3 local = tabletop.InverseTransformPoint(source.Rect.TransformPoint(source.Rect.rect.center));
            intent.X = Mathf.Clamp01(local.x / tabletop.rect.width);
            intent.Y = Mathf.Clamp01(-local.y / tabletop.rect.height);
            if (target != null)
            {
                intent.Target = target.CardId;
                if (engine.IsLocation(target.CardId))
                {
                    intent.Action = DropAction.Put;
                    intent.Location = target.CardId;
                    intent.Slot = engine.FindAcceptingSlot(id, target.CardId);
                }
                else if (IsInjuryMerge(id, target.CardId)) intent.Action = DropAction.Merge;
                else if (engine.CanStack(id, target.CardId, singleDrag, out _)) intent.Action = DropAction.Stack;
                else if (engine.CanStagePair(target.CardId, id)) intent.Action = DropAction.Pair;
                else intent.Action = DropAction.Desk;
            }
            else
            {
                intent.Target = NearestStackTarget(id, source, singleDrag, pointer);
                intent.Action = intent.Target == 0 ? DropAction.Desk : DropAction.Stack;
            }
            return intent;
        }

        internal void Drop(VoyageCardView card, PointerEventData pointer)
        {
            if (dragging != card) return;
            int id = card.CardId;
            bool single = singleDrag;
            DropIntent intent = ResolveDrop(card, pointer);
            FinishDragSession();
            switch (intent.Action)
            {
                case DropAction.Put:
                    CapturePreparationScroll();
                    if (Command(value => value.Put(id, intent.Location, intent.Slot, true))) PresentPreparation(intent.Location);
                    break;
                case DropAction.Merge:
                    mergeTarget = intent.Target;
                    try { Command(value => value.Merge(id, intent.Target)); }
                    finally { mergeTarget = 0; }
                    break;
                case DropAction.Stack:
                    Command(value => value.Stack(id, intent.Target, single));
                    break;
                case DropAction.Pair:
                    CapturePreparationScroll();
                    if (Command(value => value.StagePair(intent.Target, id, true))) PresentPreparation(0);
                    break;
                case DropAction.Desk:
                    Command(value => value.DropOnDesk(id, intent.X, intent.Y, single));
                    if (multiSelected.Contains(id) && multiSelected.Count > 1)
                    {
                        foreach (int otherId in multiSelected.ToArray())
                        {
                            if (otherId == id) continue;
                            if (cardViews.TryGetValue(otherId, out VoyageCardView otherView) && dragOffsets.TryGetValue(otherId, out Vector3 offset))
                            {
                                Vector3 worldPos = card.Rect.position + dragLayer.TransformVector(offset);
                                Vector2 tableLocal = tabletop.InverseTransformPoint(worldPos);
                                float normX = Mathf.Clamp01(tableLocal.x / tabletop.rect.width);
                                float normY = Mathf.Clamp01(tableLocal.y / tabletop.rect.height);
                                Command(value => value.DropOnDesk(otherId, normX, normY, true));
                            }
                        }
                        multiSelected.Clear();
                    }
                    break;
            }
            Render();
        }

        private bool IsInjuryMerge(int source, int target) => engine.CanMerge(source, target, out _);

        // RaycastAll is sorted by Unity's UI order. Never combine targets from different visible layers.
        private bool ReadDropTarget(PointerEventData pointer, out VoyageSlotView slot, out VoyageCardView target)
        {
            slot = null; target = null;
            if (EventSystem.current == null) return false;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            foreach (RaycastResult hit in hits)
            {
                VoyageCardView candidate = hit.gameObject.GetComponentInParent<VoyageCardView>();
                if (candidate != null && dragOffsets.ContainsKey(candidate.CardId)) continue;
                target = candidate;
                slot = hit.gameObject.GetComponentInParent<VoyageSlotView>();
                if (target != null || slot != null) return true;
                Transform surface = hit.gameObject.transform;
                bool overAnyTrayDrop = openTrays.Values.Any(t => t.StageDrop != null && (surface == t.StageDrop || surface.IsChildOf(t.StageDrop)));
                return surface == tabletopViewport || surface == tabletop || surface.IsChildOf(tabletop) || overAnyTrayDrop;
            }
            return false;
        }

        private void PreviewDrop(VoyageCardView source, PointerEventData pointer)
        {
            if (dropGhost == null) return;
            dropGhost.gameObject.SetActive(false);
            DropIntent intent = ResolveDrop(source, pointer);
            VoyageSlotView currentSlot = null;
            if (intent.Action == DropAction.Put && intent.Location >= 0 && intent.Slot >= 0)
            {
                currentSlot = FindSlotView(intent.Location, intent.Slot);
            }
            else
            {
                ReadDropTarget(pointer, out currentSlot, out _);
            }
            if (hoveredSlot != currentSlot)
            {
                if (hoveredSlot != null) hoveredSlot.SetHoverHighlight(false, false);
                hoveredSlot = currentSlot;
            }
            if (hoveredSlot != null)
            {
                bool valid = engine.CanPut(source.CardId, hoveredSlot.Location, hoveredSlot.Index, out _);
                hoveredSlot.SetHoverHighlight(true, valid);
            }

            if (intent.Action == DropAction.Put)
            {
                if (!openLocations.Contains(intent.Location) ||
                    !engine.CanPut(source.CardId, intent.Location, intent.Slot, out _) ||
                    FindSlotView(intent.Location, intent.Slot) == null) return;
                VoyageSlotView view = FindSlotView(intent.Location, intent.Slot);
                dropGhost.SetParent(dragLayer, false);
                dropGhost.position = view.Rect.position;
            }
            else if (intent.Action == DropAction.Stack)
            {
                if (!engine.PreviewStack(source.CardId, intent.Target, singleDrag, out float x, out float y, out int depth, out _)) return;
                dropGhost.SetParent(tabletop, false);
                Vector2 rootPos = TablePosition(new VoyageCardState { X = x, Y = y }, source.Rect);
                int count = depth + (singleDrag ? 1 : engine.DragCards(source.CardId, false).Count);
                dropGhost.anchoredPosition = StackedPosition(rootPos, depth, count, source.Rect);
            }
            else if (intent.Action == DropAction.Desk)
            {
                if (!engine.PreviewDeskDrop(source.CardId, intent.X, intent.Y, singleDrag, out float x, out float y, out _)) return;
                dropGhost.SetParent(tabletop, false);
                dropGhost.anchoredPosition = TablePosition(new VoyageCardState { X = x, Y = y }, source.Rect);
            }
            else return;
            dropGhost.SetAsLastSibling();
            dropGhost.gameObject.SetActive(true);
        }

        private int NearestStackTarget(int id, VoyageCardView source, bool single, PointerEventData pointer)
        {
            float best = deskRules.SnapDistance;
            int result = 0;
            Vector2 point = dragLayer.InverseTransformPoint(source.Rect.TransformPoint(source.Rect.rect.center));
            foreach (VoyageCardState card in state.Cards)
            {
                if (!cardViews.TryGetValue(card.Id, out VoyageCardView view) || view == null || !view.gameObject.activeInHierarchy) continue;
                Vector2 other = dragLayer.InverseTransformPoint(view.Rect.TransformPoint(view.Rect.rect.center));
                float distance = Vector2.Distance(point, other);
                if (distance >= best || !engine.CanStack(id, card.Id, single, out _)) continue;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(pointer.pressEventCamera, view.Rect.TransformPoint(view.Rect.rect.center));
                if (!RectTransformUtility.RectangleContainsScreenPoint(tabletopViewport, screen, pointer.pressEventCamera) ||
                    openTrays.Values.Any(t => RectTransformUtility.RectangleContainsScreenPoint(t.Rect, screen, pointer.pressEventCamera))) continue;
                var probe = new PointerEventData(EventSystem.current) { position = screen };
                if (!ReadDropTarget(probe, out _, out VoyageCardView visible) || visible != view) continue;
                best = distance; result = card.Id;
            }
            return result;
        }

        private int StackDepth(VoyageCardState card)
        {
            int depth = 0;
            var seen = new HashSet<int>();
            while (card.StackParent != 0 && seen.Add(card.Id) && cardById.TryGetValue(card.StackParent, out VoyageCardState parent))
            { depth++; card = parent; }
            return depth;
        }

        private Vector2 DesktopPosition(VoyageCardState card, RectTransform rect)
        {
            int depth = StackDepth(card);
            VoyageCardState root = card;
            while (root.StackParent != 0 && cardById.TryGetValue(root.StackParent, out VoyageCardState parent)) root = parent;
            Vector2 rootPos = TablePosition(root, rect);
            int count = engine.DragCards(root.Id, false).Count;
            return StackedPosition(rootPos, depth, count, rect);
        }

        private Vector2 StackedPosition(Vector2 rootPos, int depth, int count, RectTransform rect)
        {
            float y = Mathf.Max(rootPos.y, -tabletop.rect.height + rect.rect.height + 4 + (count - 1) * deskRules.StackOffset);
            y -= depth * deskRules.StackOffset;
            y = Mathf.Max(y, -tabletop.rect.height + rect.rect.height + 4);
            return new Vector2(rootPos.x, y);
        }

        private void TickDesktop()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (dragging == null && !menuPanel.activeSelf && !readPanel.activeSelf &&
                    RectTransformUtility.RectangleContainsScreenPoint(tabletopViewport, Input.mousePosition, null))
                {
                    var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                    var hits = new List<RaycastResult>();
                    EventSystem.current?.RaycastAll(pointer, hits);
                    bool hitInteractive = false;
                    foreach (var hit in hits)
                    {
                        if (hit.gameObject.GetComponentInParent<VoyageCardView>() != null ||
                            hit.gameObject.GetComponentInParent<VoyageSlotView>() != null ||
                            hit.gameObject.GetComponentInParent<UnityEngine.UI.Button>() != null ||
                            openTrays.Values.Any(t => hit.gameObject.transform.IsChildOf(t.Rect)) ||
                            (tasksPanel.activeSelf && hit.gameObject.transform.IsChildOf((RectTransform)tasksPanel.transform)))
                        {
                            hitInteractive = true;
                            break;
                        }
                    }
                    if (!hitInteractive)
                    {
                        isMarqueeSelecting = true;
                        marqueeStartScreen = Input.mousePosition;
                        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl) &&
                            !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                        {
                            multiSelected.Clear();
                        }
                    }
                }
            }
            else if (isMarqueeSelecting && Input.GetMouseButton(0))
            {
                float dist = Vector2.Distance(marqueeStartScreen, (Vector2)Input.mousePosition);
                if (dist > 5f)
                {
                    if (marqueeBox == null)
                    {
                        var go = new GameObject("MarqueeSelectionBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                        go.transform.SetParent(dragLayer, false);
                        marqueeBox = go.GetComponent<UnityEngine.UI.Image>();
                        marqueeBox.color = new Color(0.25f, 0.6f, 1f, 0.22f);
                        marqueeBox.raycastTarget = false;
                    }
                    Vector2 minScreen = Vector2.Min(marqueeStartScreen, Input.mousePosition);
                    Vector2 maxScreen = Vector2.Max(marqueeStartScreen, Input.mousePosition);
                    Rect marqueeScreenRect = new Rect(minScreen, maxScreen - minScreen);

                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, minScreen, null, out Vector2 localMin) &&
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, maxScreen, null, out Vector2 localMax))
                    {
                        marqueeBox.rectTransform.anchoredPosition = localMin;
                        marqueeBox.rectTransform.sizeDelta = localMax - localMin;
                        marqueeBox.gameObject.SetActive(true);
                    }

                    Vector3[] corners = new Vector3[4];
                    foreach (VoyageCardState card in state.Cards)
                    {
                        if (card.JobId != 0 || InOtherPreparation(card.Id) || engine.IsInSlot(card.Id)) continue;
                        if (!cardViews.TryGetValue(card.Id, out VoyageCardView cv) || cv == null || !cv.gameObject.activeInHierarchy) continue;
                        cv.Rect.GetWorldCorners(corners);
                        Vector2 cMin = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                        Vector2 cMax = cMin;
                        for (int k = 1; k < 4; k++)
                        {
                            Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, corners[k]);
                            cMin = Vector2.Min(cMin, sp);
                            cMax = Vector2.Max(cMax, sp);
                        }
                        Rect cardRect = new Rect(cMin, cMax - cMin);
                        if (marqueeScreenRect.Overlaps(cardRect))
                        {
                            multiSelected.Add(card.Id);
                        }
                        else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                        {
                            multiSelected.Remove(card.Id);
                        }
                    }
                    RefreshGuidance();
                }
            }
            else if (isMarqueeSelecting && Input.GetMouseButtonUp(0))
            {
                isMarqueeSelecting = false;
                if (marqueeBox != null) marqueeBox.gameObject.SetActive(false);
                Render();
            }

            if (dragging != null && dragPointer != null && !menuPanel.activeSelf && !readPanel.activeSelf)
            {
                VoyageTrayInstance hitTray = openTrays.Values.FirstOrDefault(t =>
                    RectTransformUtility.RectangleContainsScreenPoint(t.Rect, dragPointer.position, dragPointer.pressEventCamera));
                if (hitTray != null && hitTray.StageScroll != null) ScrollWhileDragging(hitTray.StageScroll, hitTray.StageDrop);
                else ScrollWhileDragging(tableScroll, tabletopViewport);

                if (Time.unscaledTime >= nextDropRefresh)
                {
                    PreviewDrop(dragging, dragPointer);
                    nextDropRefresh = Time.unscaledTime + .05f;
                }
            }
            if (guidanceSlot >= 0 && Time.unscaledTime > guidanceUntil)
            { guidanceSlot = -1; guidanceLocation = -1; RefreshGuidance(); }
            if (!string.IsNullOrEmpty(notice) && Time.unscaledTime > noticeUntil)
                Message("");
        }

        private void ScrollWhileDragging(UnityEngine.UI.ScrollRect scroll, RectTransform viewport)
        {
            if (!scroll.vertical || scroll.content == null || !RectTransformUtility.RectangleContainsScreenPoint(
                viewport, dragPointer.position, dragPointer.pressEventCamera)) return;
            if (!ReadDropTarget(dragPointer, out _, out _)) return;
            float hidden = scroll.content.rect.height - viewport.rect.height;
            if (hidden <= 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,
                dragPointer.position, dragPointer.pressEventCamera, out Vector2 point)) return;
            float edge = Mathf.Min(40f, viewport.rect.height * .15f);
            float top = Mathf.Clamp01(1 - (viewport.rect.yMax - point.y) / edge);
            float bottom = Mathf.Clamp01(1 - (point.y - viewport.rect.yMin) / edge);
            float delta = (top - bottom) * Mathf.Max(1, scroll.scrollSensitivity) * 10f * Time.unscaledDeltaTime / hidden;
            if (Mathf.Abs(delta) > .000001f)
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + delta);
        }

        private void ShowPreparation()
        {
            foreach (var tray in openTrays.Values)
            {
                if (tray.GameObject != null && !tray.GameObject.activeSelf)
                    tray.GameObject.SetActive(true);
            }
        }
    }

    internal sealed class VoyageDraggableWindow : MonoBehaviour, IDragHandler
    {
        private RectTransform target;
        private Action<Vector2> onMoved;

        public void Bind(RectTransform targetRect, Action<Vector2> movedCallback)
        {
            target = targetRect;
            onMoved = movedCallback;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;
            Vector2 delta = eventData.delta;
            Vector2 current = target.anchoredPosition;
            Vector2 next = current + delta;
            next.x = Mathf.Clamp(next.x, 18f, 1600f - target.rect.width - 18f);
            next.y = Mathf.Clamp(next.y, -810f + target.rect.height, -88f);
            target.anchoredPosition = next;
            onMoved?.Invoke(next);
        }
    }
}
