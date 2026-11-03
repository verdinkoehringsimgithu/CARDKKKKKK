using CardKKK.Tabletop.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.Tabletop.Unity
{
    public sealed class TableCardView : MonoBehaviour, IPointerDownHandler, IBeginDragHandler,
        IDragHandler, IEndDragHandler
    {
        private CardTableDemo owner;
        private RectTransform rect;
        private Image background;
        private Image frame;
        private Image illustration;
        private Image selection;
        private Text heading;
        private Text details;
        private Text footer;
        private CardData card;
        private CardTableTheme theme;
        private Vector2 target;
        private bool targetFace;
        private bool displayedFace;
        private bool faceInitialized;
        private float flipTime;
        private float duration;

        public string CardId { get; private set; }
        public string PileId { get; private set; }
        public int Index { get; private set; }
        public Vector2 Position => rect.anchoredPosition;
        public bool IsAnimating => flipTime > 0 || (rect.anchoredPosition - target).sqrMagnitude > 0.04f;

        public void Initialize(CardTableDemo controller, RectTransform parent, Vector2 size, Font font,
            CardTableTheme tableTheme)
        {
            owner = controller;
            theme = tableTheme;
            duration = theme != null ? Mathf.Max(0.05f, theme.TransitionSeconds) : 0.18f;
            rect = GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.sizeDelta = size;
            background = gameObject.AddComponent<Image>();
            background.raycastTarget = true;
            frame = owner.MakeImage(rect, "Frame", Vector2.zero, size, Color.white);
            illustration = owner.MakeImage(rect, "Illustration", new Vector2(8, 35), size - new Vector2(16, 68), Color.white);
            illustration.preserveAspect = true;
            selection = owner.MakeImage(rect, "Selection", new Vector2(0, 0), new Vector2(size.x, 4), new Color(0.4f, 0.95f, 0.83f));
            heading = owner.MakeText(rect, "Heading", new Vector2(8, size.y - 37), new Vector2(size.x - 16, 31), 14, Color.black, TextAnchor.UpperLeft);
            details = owner.MakeText(rect, "Details", new Vector2(8, 38), new Vector2(size.x - 16, size.y - 82), 12, new Color(0.13f, 0.2f, 0.23f), TextAnchor.MiddleCenter);
            footer = owner.MakeText(rect, "Footer", new Vector2(8, 3), new Vector2(size.x - 16, 24), 13, Color.black, TextAnchor.MiddleLeft);
        }

        public void Bind(CardData data, PileData pile, int index, Vector2 position, bool selected, bool immediate)
        {
            bool isNew = CardId != data.Id;
            card = data;
            CardId = data.Id;
            PileId = pile.Id;
            Index = index;
            target = position;
            selection.enabled = selected;
            if (isNew || immediate)
            {
                rect.anchoredPosition = target;
                flipTime = 0;
                rect.localScale = Vector3.one;
                faceInitialized = false;
            }
            if (!faceInitialized)
            {
                targetFace = displayedFace = data.FaceUp;
                faceInitialized = true;
                PaintFace();
            }
            else if (targetFace != data.FaceUp)
            {
                targetFace = data.FaceUp;
                flipTime = duration;
            }
            else
            {
                PaintFace();
            }
        }

        public void SetPosition(Vector2 position, RectTransform layer)
        {
            rect.SetParent(layer, false);
            target = position;
            rect.anchoredPosition = position;
        }

        public void SetLayer(RectTransform layer)
        {
            rect.SetParent(layer, false);
            rect.SetAsLastSibling();
        }

        public void EnterFrom(Vector2 position)
        {
            rect.anchoredPosition = position;
        }

        public void Animate(float delta)
        {
            if ((rect.anchoredPosition - target).sqrMagnitude > 0.04f)
            {
                float factor = 1 - Mathf.Exp(-8f * delta / duration);
                rect.anchoredPosition = Vector2.LerpUnclamped(rect.anchoredPosition, target, factor);
            }
            else
            {
                rect.anchoredPosition = target;
            }
            if (flipTime <= 0)
                return;
            flipTime = Mathf.Max(0, flipTime - delta);
            float progress = 1 - flipTime / duration;
            if (progress >= 0.5f && displayedFace != targetFace)
            {
                displayedFace = targetFace;
                PaintFace();
            }
            rect.localScale = new Vector3(Mathf.Max(0.02f, Mathf.Abs(1 - progress * 2)), 1, 1);
            if (flipTime == 0)
                rect.localScale = Vector3.one;
        }

        private void PaintFace()
        {
            Color front = theme != null ? theme.CardColor : new Color(0.88f, 0.89f, 0.83f);
            Color back = theme != null ? theme.BackColor : new Color(0.18f, 0.27f, 0.35f);
            background.color = displayedFace ? front : back;
            frame.sprite = theme != null ? (displayedFace ? theme.CardFrame : theme.CardBack) : null;
            frame.enabled = frame.sprite != null;
            illustration.sprite = theme != null ? theme.FindFace(card.DefinitionId) : null;
            illustration.enabled = displayedFace && illustration.sprite != null;
            heading.text = displayedFace ? CardLabel(card) : "卡牌";
            heading.color = displayedFace ? new Color(0.1f, 0.16f, 0.19f) : Color.white;
            details.text = displayedFace ? FamilyLabel(card.Family) + "\n\n" + card.Rank : "•  •  •";
            details.enabled = !illustration.enabled;
            details.color = displayedFace ? new Color(0.13f, 0.2f, 0.23f) : new Color(0.6f, 0.8f, 0.84f);
            footer.text = displayedFace ? "序 " + card.Rank : "卡牌桌面";
            footer.color = displayedFace ? new Color(0.13f, 0.2f, 0.23f) : Color.white;
        }

        public static string FamilyLabel(string family)
        {
            switch (family)
            {
                case "Archive": return "藏书";
                case "Supply": return "物资";
                case "Tool": return "工具";
                default: return string.IsNullOrEmpty(family) ? "未分类" : family;
            }
        }

        public static string CardLabel(CardData data)
        {
            return data.Label == data.Family + " " + data.Rank
                ? FamilyLabel(data.Family) + " " + data.Rank
                : data.Label;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                owner.Select(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                owner.BeginCardDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                owner.UpdateCardDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                owner.EndCardDrag(eventData);
        }
    }
}
