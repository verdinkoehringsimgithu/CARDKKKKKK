using System;
using CardKKK.IslandSurvival.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.IslandSurvival.Unity
{
    public sealed class SurvivalCardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image face, artwork;
        [SerializeField] private Text titleText, detailText, countText;
        [SerializeField] private GameObject selectedMark;
        public string ItemId { get; private set; }
        public bool Dragging { get; private set; }
        private SurvivalView owner;
        private Transform previousParent;
        private Vector2 previousPosition;
        private Vector3 previousScale;
        private int sibling;
        private float dragEnd;
        private bool single;
        private Vector3 grabOffset;
        private Color baseColor;
        private float pulse;
        private string previousContent = "";
        public void Render(SurvivalView view, ItemState item, CardRule rule, string detail, Sprite art, bool selected)
        {
            owner = view; ItemId = item.Id;
            titleText.text = rule.Name; detailText.text = detail; countText.text = rule.Kind == CardKind.Material ? "×" + item.Quantity : "";
            artwork.sprite = art; artwork.enabled = art != null; artwork.color = Color.white; artwork.preserveAspect = true;
            // 没有插画时仍由牌名、类别颜色和实例状态表达真实对象。
            baseColor = owner.Theme.CardColor(rule.Kind);
            selectedMark.SetActive(selected);
            string content = item.Quantity + ":" + detail;
            if (previousContent != "" && previousContent != content) pulse = owner.Theme.PulseSeconds;
            previousContent = content; face.color = baseColor;
        }
        private void Update()
        {
            if (pulse <= 0) return;
            pulse = Mathf.Max(0, pulse - Time.unscaledDeltaTime);
            face.color = Color.Lerp(baseColor, owner.Theme.Pulse, pulse / Mathf.Max(.05f, owner.Theme.PulseSeconds));
        }
        public void OnPointerClick(PointerEventData data)
        { if (!Dragging && Time.unscaledTime - dragEnd > .1f && data.button == PointerEventData.InputButton.Left) owner?.Select(ItemId); }
        public void OnBeginDrag(PointerEventData data)
        {
            if (owner == null || !owner.CanDrag(ItemId) || data.button != PointerEventData.InputButton.Left) return;
            owner.BeginCardDrag();
            single = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && owner.CanSplitDrag(ItemId);
            if (single) countText.text = "×1";
            Dragging = true; var rect = (RectTransform)transform;
            previousParent = transform.parent; previousPosition = rect.anchoredPosition; previousScale = transform.localScale; sibling = transform.GetSiblingIndex();
            transform.SetParent(owner.DragLayer, true); transform.SetAsLastSibling(); face.raycastTarget = false;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(owner.DragLayer, data.position, data.pressEventCamera, out Vector3 pointer)) grabOffset = transform.position - pointer;
        }
        public void OnDrag(PointerEventData data)
        {
            if (!Dragging) return;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(owner.DragLayer, data.position, data.pressEventCamera, out Vector3 point)) transform.position = point + grabOffset;
        }
        public void OnEndDrag(PointerEventData data)
        {
            if (!Dragging) return;
            Dragging = false; dragEnd = Time.unscaledTime;
            Vector3 droppedPosition = transform.position;
            transform.SetParent(previousParent, false); transform.SetSiblingIndex(sibling); transform.localScale = previousScale; ((RectTransform)transform).anchoredPosition = previousPosition;
            face.raycastTarget = true;
            owner.Drop(ItemId, data, single, droppedPosition);
            owner.EndCardDrag();
        }
    }
}
