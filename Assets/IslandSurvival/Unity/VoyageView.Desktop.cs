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

        public VoyageDeskGeometry MeasureDesk()
        {
            Canvas.ForceUpdateCanvases();
            return new VoyageDeskGeometry(tabletop.rect.width, tabletop.rect.height, cardTemplate.Rect.rect.width, cardTemplate.Rect.rect.height);
        }

        private void DisplaySlots()
        {
            IReadOnlyList<VoyageSlotOffer> slots = engine.Slots(state.PreparationLocation);
            foreach (int index in slotViews.Keys.Where(index => index >= slots.Count).ToArray())
            {
                Destroy(slotViews[index].gameObject);
                slotViews.Remove(index);
            }
            foreach (VoyageSlotOffer slot in slots)
            {
                if (!slotViews.TryGetValue(slot.Index, out VoyageSlotView view) || view == null)
                {
                    view = Instantiate(slotTemplate, stageCards, false);
                    slotViews[slot.Index] = view;
                }
                view.Rect.anchoredPosition = SlotPosition(slot.Index);
                view.transform.SetAsFirstSibling();
                view.Display(this, slot);
                view.gameObject.SetActive(true);
            }
            SetHeight(stageCards, Mathf.Max(190, Mathf.CeilToInt(slots.Count / 3f) * 180 + 8));
        }

        private static Vector2 SlotPosition(int index) => new Vector2(8 + index % 3 * 138, -5 - index / 3 * 180);

        internal void SelectSlot(int index)
        {
            if (dragging != null || menuPanel.activeSelf || readPanel.activeSelf) return;
            VoyageSlotOffer slot = engine.Slots(state.PreparationLocation).FirstOrDefault(item => item.Index == index);
            if (slot == null || slot.Locked) return;
            if (guidanceSlot == index) { guidanceSlot = -1; RefreshGuidance(); return; }
            guidanceSlot = index;
            guidanceUntil = Time.unscaledTime + 6f;
            RefreshGuidance();
        }

        private void RefreshGuidance()
        {
            if (engine == null || state == null) return;
            IReadOnlyList<VoyageSlotOffer> offers = engine.Slots(state.PreparationLocation);
            foreach (VoyageCardState card in state.Cards)
            {
                if (!cardViews.TryGetValue(card.Id, out VoyageCardView view) || view == null) continue;
                bool candidate = false;
                if (dragging != null && !dragOffsets.ContainsKey(card.Id))
                {
                    VoyageSlotOffer occupied = preparationVisible ? offers.FirstOrDefault(slot => slot.CardId == card.Id) : null;
                    candidate = occupied != null
                        ? engine.CanPut(dragging.CardId, occupied.Location, occupied.Index, out _)
                        : engine.IsLocation(card.Id)
                            ? engine.FindAcceptingSlot(dragging.CardId, card.Id) >= 0
                            : engine.CanMerge(dragging.CardId, card.Id, out _) || engine.CanStack(dragging.CardId, card.Id, singleDrag, out _) || engine.CanStagePair(card.Id, dragging.CardId);
                }
                else if (guidanceSlot >= 0)
                    candidate = engine.CanPut(card.Id, state.PreparationLocation, guidanceSlot, out _);
                view.Guide(candidate);
            }
            foreach (var pair in slotViews)
            {
                VoyageSlotOffer offer = offers.FirstOrDefault(slot => slot.Index == pair.Key);
                if (offer == null) continue;
                pair.Value.Display(this, offer);
                pair.Value.Guide(dragging != null && engine.CanPut(dragging.CardId, offer.Location, offer.Index, out _), guidanceSlot == pair.Key);
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
            bool inPanel = preparationVisible && RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)preparationPanel.transform, pointer.position, pointer.pressEventCamera);
            bool inSlots = inPanel && RectTransformUtility.RectangleContainsScreenPoint(stageDrop, pointer.position, pointer.pressEventCamera);
            if (inPanel && !inSlots) return intent;
            if (!ReadDropTarget(pointer, out VoyageSlotView slot, out VoyageCardView target)) return intent;
            int id = source.CardId;
            if (inSlots)
            {
                int index = slot != null && slot.Location == state.PreparationLocation ? slot.Index : target != null
                    ? engine.Slots(state.PreparationLocation).FirstOrDefault(item => item.CardId == target.CardId)?.Index ?? -1 : -1;
                if (index < 0) index = engine.FindAcceptingSlot(id, state.PreparationLocation);
                intent.Action = DropAction.Put;
                intent.Location = state.PreparationLocation;
                intent.Slot = index;
                return intent;
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
                // Empty table/slot backgrounds accept a drop; other foreground UI consumes it.
                return surface == tabletopViewport || surface == tabletop || surface.IsChildOf(tabletop) ||
                    preparationVisible && (surface == stageDrop || surface.IsChildOf(stageDrop));
            }
            return false;
        }

        private void PreviewDrop(VoyageCardView source, PointerEventData pointer)
        {
            if (dropGhost == null) return;
            dropGhost.gameObject.SetActive(false);
            DropIntent intent = ResolveDrop(source, pointer);
            if (intent.Action == DropAction.Put)
            {
                if (!preparationVisible || intent.Location != state.PreparationLocation ||
                    !engine.CanPut(source.CardId, intent.Location, intent.Slot, out _) ||
                    !slotViews.TryGetValue(intent.Slot, out VoyageSlotView view)) return;
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
                    preparationVisible && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)preparationPanel.transform, screen, pointer.pressEventCamera)) continue;
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
            if (dragging != null && dragPointer != null && !menuPanel.activeSelf && !readPanel.activeSelf)
            {
                bool inPanel = preparationVisible && RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)preparationPanel.transform, dragPointer.position, dragPointer.pressEventCamera);
                if (inPanel) ScrollWhileDragging(stageScroll, stageDrop);
                else ScrollWhileDragging(tableScroll, tabletopViewport);
                // The held card can stay still while the world or its legal targets change.
                if (Time.unscaledTime >= nextDropRefresh)
                {
                    PreviewDrop(dragging, dragPointer);
                    nextDropRefresh = Time.unscaledTime + .05f;
                }
            }
            if (preparationFade != null)
            {
                float target = preparationVisible ? 1 : 0;
                panelTime = Mathf.MoveTowards(panelTime, target, Time.unscaledDeltaTime / .333333f);
                float t = Mathf.SmoothStep(0, 1, panelTime);
                preparationFade.alpha = t;
                preparationFade.interactable = preparationVisible;
                preparationFade.blocksRaycasts = preparationVisible;
                preparationPanel.transform.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, t);
                if (panelTime <= 0 && !preparationVisible) preparationPanel.SetActive(false);
                if (dragging != null && dragPointer != null && panelTime > 0 && panelTime < 1) PreviewDrop(dragging, dragPointer);
            }
            if (guidanceSlot >= 0 && Time.unscaledTime > guidanceUntil)
            { guidanceSlot = -1; RefreshGuidance(); }
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
            if (preparationFade == null)
            {
                preparationFade = preparationPanel.GetComponent<CanvasGroup>();
                if (preparationFade == null) preparationFade = preparationPanel.AddComponent<CanvasGroup>();
            }
            if (preparationVisible) preparationPanel.SetActive(true);
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
