using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.IslandSurvival.Unity
{
    public sealed class VoyageCardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler,
        IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image face, inner, artwork, liquidFill, wearLeft, wearRight;
        [SerializeField] private Text glyph, title, status;
        [SerializeField] private GameObject liquid;
        private VoyageView owner;
        private float lastDrop = -1;
        private Vector3 grabOffset;
        private Vector2 wearLeftSize, wearRightSize;
        private bool hasSize;
        private Sprite previousSprite;

        public int CardId { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        public bool Dragging { get; private set; }

        public void Display(VoyageView view, int cardId, string name, string mark, string shortStatus, bool textCard, Sprite sprite,
            Material artMaterial, Color color, Color frame, float water, Color waterColor, float wear)
        {
            owner = view;
            CardId = cardId;
            if (title.text != name) title.text = name;
            if (status.text != shortStatus) status.text = shortStatus;
            if (glyph.text != mark) glyph.text = mark;
            glyph.enabled = sprite == null;
            glyph.fontSize = textCard ? 12 : 28;
            glyph.alignment = textCard ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter;
            artwork.enabled = sprite != null;
            if (previousSprite != sprite)
            {
                artwork.sprite = sprite;
                artwork.preserveAspect = true;
                previousSprite = sprite;
            }
            if (artwork.material != artMaterial) artwork.material = artMaterial;
            face.color = frame;
            inner.color = color;
            liquid.SetActive(water >= 0);
            if (water >= 0)
            {
                liquidFill.rectTransform.anchorMax = new Vector2(1, Mathf.Clamp01(water));
                liquidFill.color = waterColor;
            }

            if (!hasSize)
            {
                wearLeftSize = wearLeft.rectTransform.sizeDelta;
                wearRightSize = wearRight.rectTransform.sizeDelta;
                hasSize = true;
            }
            wearLeft.enabled = wear > .01f;
            wearRight.enabled = wear > .35f;
            wearLeft.rectTransform.sizeDelta = new Vector2(wearLeftSize.x, wearLeftSize.y * wear);
            wearRight.rectTransform.sizeDelta = new Vector2(wearRightSize.x, wearRightSize.y * Mathf.InverseLerp(.35f, 1, wear));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Dragging || Time.unscaledTime - lastDrop < .15f) return;
            if (eventData.button == PointerEventData.InputButton.Left) owner?.Select(CardId, eventData.clickCount);
            else if (eventData.button == PointerEventData.InputButton.Right) owner?.RemoveFromStage(CardId);
        }

        public void OnPointerEnter(PointerEventData eventData) => owner?.Hover(CardId, true);
        public void OnPointerExit(PointerEventData eventData) => owner?.Hover(CardId, false);

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || owner == null || !owner.BeginDrag(this)) return;
            Dragging = true;
            face.raycastTarget = false;
            Rect.SetParent(owner.DragLayer, true);
            Rect.SetAsLastSibling();
            grabOffset = Vector3.zero;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(owner.DragLayer, eventData.position,
                eventData.pressEventCamera, out Vector3 point)) grabOffset = Rect.position - point;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Dragging && RectTransformUtility.ScreenPointToWorldPointInRectangle(owner.DragLayer,
                eventData.position, eventData.pressEventCamera, out Vector3 point)) Rect.position = point + grabOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!Dragging) return;
            FinishVisualDrag();
            owner.Drop(this, eventData);
        }

        public void FinishVisualDrag()
        {
            Dragging = false;
            lastDrop = Time.unscaledTime;
            face.raycastTarget = true;
        }
    }
}
