using CardKKK.Tabletop.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.Tabletop.Unity
{
    /// <summary>可编辑的单张卡牌预制体。只显示已提交状态，拖动位置是临时视觉。</summary>
    public sealed class TableCardView : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField, InspectorName("卡牌矩形")] private RectTransform rect;
        [SerializeField, InspectorName("卡底")] private Image background;
        [SerializeField, InspectorName("通用卡框")] private Image frame;
        [SerializeField, InspectorName("定义插画")] private Image illustration;
        [SerializeField, InspectorName("选中标记")] private Image selection;
        [SerializeField, InspectorName("标题")] private Text heading;
        [SerializeField, InspectorName("底部说明")] private Text footer;
        private CardTableView owner;
        private CardData card;
        private CardTableTheme theme;
        private Sprite artwork;
        private Vector2 target;
        private bool targetFace, displayedFace, faceInitialized;
        private float flipTime, duration;
        public string CardId { get; private set; }
        public string PileId { get; private set; }
        public Vector2 Position => rect.anchoredPosition;
        public bool IsAnimating => flipTime > 0 || (rect.anchoredPosition - target).sqrMagnitude > 0.04f;

        public void Initialize(CardTableView view, Vector2 size, Font font, CardTableTheme tableTheme)
        {
            owner = view; theme = tableTheme;
            duration = theme != null ? Mathf.Max(0.05f, theme.TransitionSeconds) : 0.18f;
            rect.sizeDelta = size;
            illustration.preserveAspect = true;
            heading.font = footer.font = font;
        }
        public void Bind(CardData data, PileData pile, Vector2 position, Sprite sprite, bool selected, bool immediate)
        {
            bool isNew = CardId != data.Id;
            card = data; CardId = data.Id; PileId = pile.Id; artwork = sprite; target = position;
            SetSelected(selected);
            if (isNew || immediate)
            { rect.anchoredPosition = target; flipTime = 0; rect.localScale = Vector3.one; faceInitialized = false; }
            if (!faceInitialized)
            { targetFace = displayedFace = data.FaceUp; faceInitialized = true; PaintFace(); }
            else if (targetFace != data.FaceUp) { targetFace = data.FaceUp; flipTime = duration; }
            else PaintFace();
        }
        public void SetSelected(bool value) => selection.enabled = value;
        public void SetPosition(Vector2 position, RectTransform layer)
        { SetLayer(layer); target = position; rect.anchoredPosition = position; }
        public void SetLayer(RectTransform layer)
        { if (rect.parent != layer) rect.SetParent(layer, false); rect.SetAsLastSibling(); }
        public void EnterFrom(Vector2 position) => rect.anchoredPosition = position;
        public void Animate(float delta)
        {
            if ((rect.anchoredPosition - target).sqrMagnitude > 0.04f)
                rect.anchoredPosition = Vector2.LerpUnclamped(rect.anchoredPosition, target, 1 - Mathf.Exp(-8f * delta / duration));
            else rect.anchoredPosition = target;
            if (flipTime <= 0) return;
            flipTime = Mathf.Max(0, flipTime - delta);
            float progress = 1 - flipTime / duration;
            if (progress >= 0.5f && displayedFace != targetFace) { displayedFace = targetFace; PaintFace(); }
            rect.localScale = new Vector3(Mathf.Max(0.02f, Mathf.Abs(1 - progress * 2)), 1, 1);
            if (flipTime == 0) rect.localScale = Vector3.one;
        }
        private void PaintFace()
        {
            CardTableArt.ApplySprite(frame, theme != null ? (displayedFace ? theme.CardFrame : theme.CardBack) : null);
            frame.enabled = frame.sprite != null;
            background.color = frame.enabled ? Color.clear
                : displayedFace ? (theme != null ? theme.CardColor : new Color(0.88f, 0.89f, 0.83f))
                : (theme != null ? theme.BackColor : new Color(0.18f, 0.27f, 0.35f));
            illustration.sprite = artwork;
            illustration.enabled = displayedFace && artwork != null;
            heading.text = displayedFace ? card.Label : "";
            Color textColor = theme != null ? theme.CardTextColor : new Color(0.1f, 0.12f, 0.14f);
            heading.color = displayedFace ? textColor : Color.white;
            footer.text = displayedFace ? card.Rank.ToString() : "";
            footer.color = displayedFace ? textColor : Color.white;
        }
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) owner?.Select(this); }
        public void OnBeginDrag(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) owner?.BeginCardDrag(this, data); }
        public void OnDrag(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) owner?.UpdateCardDrag(data); }
        public void OnEndDrag(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) owner?.EndCardDrag(data); }
    }
}
