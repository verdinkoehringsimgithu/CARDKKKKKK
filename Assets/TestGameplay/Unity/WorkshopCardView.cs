using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.TestGameplay.Unity
{
    public enum WorkshopCardKind { Item, Site, Facility }

    /// <summary>工坊对象的呈现和拖动预览；不拥有库存，不直接执行制作。</summary>
    public sealed class WorkshopCardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler,
        IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, InspectorName("卡牌矩形")] private RectTransform rect;
        [SerializeField, InspectorName("卡框")] private Image face;
        [SerializeField, InspectorName("物件图")] private Image artwork;
        [SerializeField, InspectorName("选中轮廓")] private Image selection;
        [SerializeField, InspectorName("进度条")] private Image progress;
        [SerializeField, InspectorName("名称")] private Text heading;
        [SerializeField, InspectorName("数量")] private Text quantity;
        [SerializeField, InspectorName("状态")] private Text status;
        [SerializeField, InspectorName("预置物品 ID")] private string objectId;
        private WorkshopView owner;
        private WorkshopConfiguration configuration;
        private int lastQuantity = -1;
        private string lastDescription;
        private float displayedProgress, targetProgress;
        private float pulse;
        private bool isSelected, isDropTarget;
        public string ObjectId => objectId;
        public WorkshopCardKind Kind { get; private set; }
        public RectTransform Rect => rect;

        public void Initialize(WorkshopView view, WorkshopConfiguration settings, Font font)
        {
            owner = view; configuration = settings;
            heading.font = quantity.font = status.font = font;
            face.sprite = settings.CardFrame;
            face.type = face.sprite != null && face.sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            face.color = face.sprite != null ? Color.white : settings.CardColor;
            heading.color = quantity.color = settings.TextColor;
            status.color = new Color(settings.TextColor.r, settings.TextColor.g, settings.TextColor.b, 0.85f);
            selection.color = Color.clear;
            foreach (Image edge in selection.GetComponentsInChildren<Image>(true))
                if (edge != selection) edge.color = settings.AccentColor;
            artwork.preserveAspect = true;
            progress.color = settings.AccentColor;
        }

        public void Bind(string id, WorkshopCardKind kind, string displayName, int count,
            string description, float completion, bool selected, bool animate)
        {
            objectId = id; Kind = kind;
            heading.text = displayName;
            quantity.text = kind == WorkshopCardKind.Item ? count.ToString() : "";
            status.text = description ?? "";
            artwork.sprite = configuration.ArtworkFor(id);
            artwork.enabled = artwork.sprite != null;
            progress.enabled = completion >= 0;
            progress.transform.parent.gameObject.SetActive(completion >= 0);
            targetProgress = Mathf.Clamp01(completion);
            if (!animate) displayedProgress = targetProgress;
            PaintProgress();
            isSelected = selected;
            PaintSelection();
            if (animate && (lastQuantity < 0 || count != lastQuantity || lastDescription != description))
                pulse = configuration.TransitionSeconds;
            if (!animate) { pulse = 0; rect.localScale = Vector3.one; }
            lastQuantity = count;
            lastDescription = description;
        }

        public void SetSelected(bool selected) { isSelected = selected; PaintSelection(); }
        private void PaintSelection() => selection.gameObject.SetActive(isSelected || isDropTarget);
        public void Animate(float delta)
        {
            if (progress.enabled && !Mathf.Approximately(displayedProgress, targetProgress))
            {
                displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress,
                    delta / Mathf.Max(0.05f, configuration.TransitionSeconds));
                PaintProgress();
            }
            if (pulse <= 0) return;
            pulse = Mathf.Max(0, pulse - delta);
            float t = 1 - pulse / Mathf.Max(0.05f, configuration.TransitionSeconds);
            rect.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * 0.06f);
            if (pulse == 0) rect.localScale = Vector3.one;
        }
        private void PaintProgress()
        {
            progress.fillAmount = displayedProgress;
            progress.rectTransform.anchorMax = new Vector2(displayedProgress, 1);
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && !data.dragging)
                owner?.SelectObject(ObjectId, Kind);
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && Kind == WorkshopCardKind.Item)
                owner?.BeginItemDrag(this, data);
        }
        public void OnDrag(PointerEventData data) => owner?.MoveItemDrag(data);
        public void OnEndDrag(PointerEventData data) => owner?.EndItemDrag(data);
        public void OnDrop(PointerEventData data)
        {
            if (Kind == WorkshopCardKind.Facility) owner?.DropOnFacility(ObjectId);
        }
        public void OnPointerEnter(PointerEventData data)
        {
            isDropTarget = Kind == WorkshopCardKind.Facility && owner != null && owner.IsDraggingItem;
            PaintSelection();
        }
        public void OnPointerExit(PointerEventData data) { isDropTarget = false; PaintSelection(); }
        private void OnDisable()
        {
            pulse = 0; isDropTarget = false;
            if (rect != null) rect.localScale = Vector3.one;
        }
    }
}
