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
        private readonly Dictionary<int, VoyageSlotView> slotViews = new Dictionary<int, VoyageSlotView>();
        private readonly Dictionary<int, Vector3> dragOffsets = new Dictionary<int, Vector3>();
        private readonly List<VoyageCardView> retiredCards = new List<VoyageCardView>();
        private int mergeTarget;
        private PointerEventData dragPointer;
        private VoyageDeskRules deskRules;
        private bool singleDrag;
        private int guidanceSlot = -1;
        private float guidanceUntil, panelTime;
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
            guidanceSlot = index;
            guidanceUntil = Time.unscaledTime + 3f;
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
                    candidate = engine.IsLocation(card.Id)
                        ? engine.FindAcceptingSlot(dragging.CardId, card.Id) >= 0
                        : engine.CanStack(dragging.CardId, card.Id, singleDrag, out _) || engine.CanStagePair(card.Id, dragging.CardId);
                }
                else if (guidanceSlot >= 0)
                    candidate = engine.CanPut(card.Id, state.PreparationLocation, guidanceSlot, out _);
                view.Guide(candidate);
            }
            foreach (var pair in slotViews)
            {
                VoyageSlotOffer offer = engine.Slots(state.PreparationLocation).FirstOrDefault(slot => slot.Index == pair.Key);
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
            foreach (int id in engine.DragCards(card.CardId, single))
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

        internal void Drop(VoyageCardView card, PointerEventData pointer)
        {
            if (dragging != card) return;
            int id = card.CardId;
            bool single = singleDrag;
            ReadDropTargets(pointer, out VoyageSlotView slot, out VoyageCardView target);
            Vector3 local = tabletop.InverseTransformPoint(card.Rect.TransformPoint(card.Rect.rect.center));
            float x = Mathf.Clamp01(local.x / tabletop.rect.width), y = Mathf.Clamp01(-local.y / tabletop.rect.height);
            FinishDragSession();
            if (preparationVisible && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)preparationPanel.transform,
                pointer.position, pointer.pressEventCamera) && !RectTransformUtility.RectangleContainsScreenPoint(stageDrop, pointer.position, pointer.pressEventCamera))
            { Render(); return; }
            if (preparationVisible && RectTransformUtility.RectangleContainsScreenPoint(stageDrop, pointer.position, pointer.pressEventCamera))
            {
                int index = slot != null ? slot.Index : target != null ? engine.Slots(state.PreparationLocation).FirstOrDefault(item => item.CardId == target.CardId)?.Index ?? -1 : -1;
                if (index < 0) index = engine.FindAcceptingSlot(id, state.PreparationLocation);
                Command(value => value.Put(id, state.PreparationLocation, index));
            }
            else if (target != null)
            {
                if (engine.IsLocation(target.CardId))
                {
                    int location = target.CardId;
                    int index = engine.FindAcceptingSlot(id, location);
                    if (index >= 0) OpenPreparation(location);
                    Command(value => value.Put(id, location, index));
                }
                else if (cards[cardById[id].CardId].Kind == VoyageCardKind.Injury &&
                    cardById[id].CardId == cardById[target.CardId].CardId && cardById[id].Limb == cardById[target.CardId].Limb)
                {
                    mergeTarget = target.CardId;
                    Command(value => value.Merge(id, target.CardId));
                    mergeTarget = 0;
                }
                else if (engine.CanStack(id, target.CardId, single, out _)) Command(value => value.Stack(id, target.CardId, single));
                else if (engine.CanStagePair(target.CardId, id))
                {
                    // Non-stacking recipe pairs use the same slot acceptance as direct insertion.
                    OpenPreparation(0);
                    Command(value => value.StagePair(target.CardId, id));
                }
                else Command(value => value.DropOnDesk(id, x, y, single));
            }
            else if (RectTransformUtility.RectangleContainsScreenPoint(tabletopViewport, pointer.position, pointer.pressEventCamera)
                && (!preparationVisible || !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)preparationPanel.transform, pointer.position, pointer.pressEventCamera)))
            {
                int near = NearestStackTarget(id, card, single);
                if (near != 0) Command(value => value.Stack(id, near, single));
                else Command(value => value.DropOnDesk(id, x, y, single));
            }
            Render();
        }

        private void ReadDropTargets(PointerEventData pointer, out VoyageSlotView slot, out VoyageCardView target)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current?.RaycastAll(pointer, hits);
            slot = null; target = null;
            foreach (RaycastResult hit in hits)
            {
                if (slot == null) slot = hit.gameObject.GetComponentInParent<VoyageSlotView>();
                VoyageCardView candidate = hit.gameObject.GetComponentInParent<VoyageCardView>();
                if (target == null && candidate != null && !dragOffsets.ContainsKey(candidate.CardId)) target = candidate;
            }
        }

        private void PreviewDrop(VoyageCardView source, PointerEventData pointer)
        {
            dropGhost.gameObject.SetActive(false);
            ReadDropTargets(pointer, out VoyageSlotView slot, out VoyageCardView target);
            if (preparationVisible && RectTransformUtility.RectangleContainsScreenPoint(stageDrop, pointer.position, pointer.pressEventCamera))
            {
                int index = slot != null ? slot.Index : target != null
                    ? engine.Slots(state.PreparationLocation).FirstOrDefault(item => item.CardId == target.CardId)?.Index ?? -1 : -1;
                if (index < 0) index = engine.FindAcceptingSlot(source.CardId, state.PreparationLocation);
                if (index < 0 || !engine.CanPut(source.CardId, state.PreparationLocation, index, out _) || !slotViews.TryGetValue(index, out VoyageSlotView view)) return;
                dropGhost.SetParent(dragLayer, false);
                dropGhost.position = view.Rect.position;
            }
            else
            {
                if (target != null && engine.IsLocation(target.CardId)) return; // Target itself is already highlighted.
                int stack = target != null && engine.CanStack(source.CardId, target.CardId, singleDrag, out _) ? target.CardId : NearestStackTarget(source.CardId, source, singleDrag);
                if (stack != 0)
                {
                    if (!engine.PreviewStack(source.CardId, stack, singleDrag, out float x, out float y, out int depth, out _)) return;
                    dropGhost.SetParent(tabletop, false);
                    dropGhost.anchoredPosition = TablePosition(new VoyageCardState { X = x, Y = y }, source.Rect) + Vector2.down * (depth * deskRules.StackOffset);
                }
                else
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(tabletopViewport, pointer.position, pointer.pressEventCamera) ||
                        preparationVisible && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)preparationPanel.transform, pointer.position, pointer.pressEventCamera)) return;
                    Vector3 local = tabletop.InverseTransformPoint(source.Rect.TransformPoint(source.Rect.rect.center));
                    float x = Mathf.Clamp01(local.x / tabletop.rect.width), y = Mathf.Clamp01(-local.y / tabletop.rect.height);
                    if (!engine.PreviewDeskDrop(source.CardId, x, y, singleDrag, out x, out y, out _)) return;
                    dropGhost.SetParent(tabletop, false);
                    dropGhost.anchoredPosition = TablePosition(new VoyageCardState { X = x, Y = y }, source.Rect);
                }
            }
            dropGhost.SetAsLastSibling();
            dropGhost.gameObject.SetActive(true);
        }

        private int NearestStackTarget(int id, VoyageCardView source, bool single)
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
            Vector2 point = TablePosition(root, rect);
            int count = engine.DragCards(root.Id, false).Count;
            point.y = Mathf.Max(point.y, -tabletop.rect.height + rect.rect.height + 4 + (count - 1) * deskRules.StackOffset);
            point.y -= depth * deskRules.StackOffset;
            point.y = Mathf.Max(point.y, -tabletop.rect.height + rect.rect.height + 4);
            return point;
        }

        private void TickDesktop()
        {
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
            if (guidanceSlot >= 0 && (Time.unscaledTime > guidanceUntil || Input.GetMouseButtonDown(0)))
            { guidanceSlot = -1; RefreshGuidance(); }
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
}
