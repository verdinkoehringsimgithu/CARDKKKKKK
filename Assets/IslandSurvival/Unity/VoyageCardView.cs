using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.IslandSurvival.Unity
{
    public sealed class VoyageCardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler,
        IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image face, inner, artwork, liquidFill, wearLeft, wearRight, jobFill;
        [SerializeField] private Text glyph, title, status;
        [SerializeField] private GameObject liquid;
        private VoyageView owner;
        private float lastDrop = -1;
        private Vector3 grabOffset;
        private Vector2 wearLeftSize, wearRightSize;
        private bool hasSize;
        private Sprite previousSprite;
        private Color normalFrame;
        private Vector2 motionFrom, motionTo;
        private float motionTime, motionArc;
        private bool moving;
        private float pickupTime;
        private bool retiring;
        private Vector3 retireFrom, retireTo;
        private CanvasGroup retireFade;
        private float retireTime;

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
            normalFrame = frame;
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
            else if (eventData.button == PointerEventData.InputButton.Right) owner?.QuickAction(CardId);
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
                eventData.position, eventData.pressEventCamera, out Vector3 point))
            {
                Rect.position = point + grabOffset;
                owner.DragMoved(this, eventData);
            }
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
            Rect.localScale = Vector3.one;
            Rect.localRotation = Quaternion.identity;
        }

        public void SetHeld(bool held)
        {
            StopMotion();
            Dragging = held;
            if (held) pickupTime = 0;
            face.raycastTarget = !held;
        }

        public void Guide(bool candidate) => face.color = candidate ? new Color(.46f, 1f, .16f) : normalFrame;

        public void SetProgress(float progress)
        {
            if (jobFill == null) return;
            jobFill.gameObject.SetActive(progress >= 0);
            jobFill.fillAmount = Mathf.Clamp01(progress);
        }

        public void SnapTo(RectTransform parent, Vector2 position)
        {
            moving = false;
            Rect.SetParent(parent, false);
            Rect.localScale = Vector3.one;
            Rect.anchoredPosition = motionTo = position;
        }

        public void LayoutTo(RectTransform parent, Vector2 position, bool animate, float arc)
        {
            if (Dragging) return;
            bool changedParent = Rect.parent != parent;
            if (!changedParent && moving && (motionTo - position).sqrMagnitude < .01f) return;
            if (!changedParent && !moving && (Rect.anchoredPosition - position).sqrMagnitude < .01f) return;
            if (!animate || !gameObject.activeInHierarchy) { SnapTo(parent, position); return; }
            if (changedParent) Rect.SetParent(parent, true);
            Rect.localScale = Vector3.one;
            motionFrom = Rect.anchoredPosition;
            motionTo = position;
            motionArc = arc;
            motionTime = 0;
            moving = true;
        }

        public void StopMotion() => moving = false;

        public void RetireTo(Vector3 destination)
        {
            StopMotion();
            FinishVisualDrag();
            face.raycastTarget = false;
            owner = null;
            retiring = true;
            retireTime = 0;
            retireFrom = Rect.position;
            retireTo = destination;
            retireFade = GetComponent<CanvasGroup>();
            if (retireFade == null) retireFade = gameObject.AddComponent<CanvasGroup>();
            retireFade.interactable = retireFade.blocksRaycasts = false;
        }

        private void Update()
        {
            if (retiring)
            {
                retireTime = Mathf.Min(1, retireTime + Time.unscaledDeltaTime / .22f);
                float t = Mathf.SmoothStep(0, 1, retireTime);
                Rect.position = Vector3.LerpUnclamped(retireFrom, retireTo, t);
                Rect.localScale = Vector3.one * Mathf.Lerp(1, .25f, t);
                retireFade.alpha = 1 - t;
                if (retireTime >= 1) Destroy(gameObject);
                return;
            }
            if (Dragging)
            {
                pickupTime = Mathf.Min(1, pickupTime + Time.unscaledDeltaTime / .15f);
                Rect.localScale = Vector3.one * Mathf.Lerp(1, 1.04f, pickupTime);
                Rect.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(pickupTime * Mathf.PI * 2) * 3 * (1 - pickupTime));
                return;
            }
            if (!moving) return;
            motionTime += Time.unscaledDeltaTime / .30f;
            float t = Mathf.Clamp01(motionTime);
            float smooth = t * t * (3 - 2 * t);
            Vector2 position = Vector2.LerpUnclamped(motionFrom, motionTo, smooth);
            position.y += Mathf.Sin(t * Mathf.PI) * motionArc;
            Rect.anchoredPosition = position;
            if (t >= 1) { moving = false; Rect.anchoredPosition = motionTo; }
        }
    }
}
