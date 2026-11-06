using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.Spellcraft.Unity
{
    /// <summary>一张可绑定的牌；只发送选择意图并播放已经结算的反馈。</summary>
    public sealed class ForgeCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private RectTransform visual;
        [SerializeField] private Image frame, artwork;
        [SerializeField] private GameObject selection;
        [SerializeField] private Text heading, body, cost, status, feedback;
        [SerializeField] private Button button;
        [SerializeField] private bool useCardFrame = true;
        private bool selected, hovered, bound, feedbackOriginCaptured;
        private float pulse, shake, feedbackTime;
        private Vector2 feedbackOrigin;
        public int Index { get; private set; }
        public string Id { get; private set; }
        public Button Button => button;
        public event Action<ForgeCardView> Clicked, Focused;

        public void Initialize(ForgeConfiguration configuration)
        {
            if (!bound) { button.onClick.AddListener(ClickedInternal); bound = true; }
            foreach (Text label in GetComponentsInChildren<Text>(true)) label.font = configuration.Font;
            if (useCardFrame && frame != null && configuration.CardFrame != null)
            {
                frame.sprite = configuration.CardFrame;
                frame.type = frame.sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
                frame.pixelsPerUnitMultiplier = 7;
            }
            else if (frame != null) { frame.sprite = null; frame.type = Image.Type.Simple; }
            if (feedback != null)
            {
                if (!feedbackOriginCaptured)
                { feedbackOrigin = feedback.rectTransform.anchoredPosition; feedbackOriginCaptured = true; }
                ResetFeedback();
            }
        }

        public void Bind(int index, string id, string title, string rules, string price,
            string state, Sprite art, bool available, bool isSelected = false)
        {
            Index = index; Id = id;
            if (heading != null) heading.text = title;
            if (body != null) body.text = rules;
            if (cost != null) cost.text = price;
            if (status != null) status.text = state;
            if (artwork != null)
            {
                artwork.sprite = art; artwork.enabled = art != null;
                artwork.color = available ? Color.white : new Color(0.45f, 0.48f, 0.51f, 0.85f);
            }
            button.interactable = available;
            selected = isSelected; PaintSelection();
        }

        public void SetSelected(bool value) { selected = value; PaintSelection(); }
        public void Present(int value, bool damage)
        {
            if (!isActiveAndEnabled) return;
            pulse = 0.24f;
            if (damage) shake = 0.24f;
            if (feedback != null && value != 0)
            {
                feedback.text = (damage ? "−" : "+") + Mathf.Abs(value);
                feedback.color = damage ? new Color(1f, 0.38f, 0.27f) : new Color(1f, 0.83f, 0.3f);
                feedbackTime = 0.65f;
                feedback.rectTransform.anchoredPosition = feedbackOrigin;
            }
        }

        public void Animate(float delta)
        {
            if (pulse > 0)
            {
                pulse = Mathf.Max(0, pulse - delta);
                visual.localScale = Vector3.one * (1 + Mathf.Sin(pulse / 0.24f * Mathf.PI) * 0.045f);
            }
            if (shake > 0)
            {
                shake = Mathf.Max(0, shake - delta);
                visual.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(shake * 105) * 3 * shake / 0.24f);
            }
            if (feedbackTime > 0 && feedback != null)
            {
                feedbackTime = Mathf.Max(0, feedbackTime - delta);
                feedback.rectTransform.anchoredPosition = feedbackOrigin + Vector2.up * ((0.65f - feedbackTime) * 48);
                Color color = feedback.color; color.a = Mathf.Clamp01(feedbackTime / 0.2f); feedback.color = color;
                if (feedbackTime == 0) ResetFeedback();
            }
        }

        private void ClickedInternal() => Clicked?.Invoke(this);
        private void PaintSelection() { if (selection != null) selection.SetActive(selected || hovered); }
        public void OnPointerEnter(PointerEventData data) { hovered = true; PaintSelection(); Focused?.Invoke(this); }
        public void OnPointerExit(PointerEventData data) { hovered = false; PaintSelection(); }
        public void OnSelect(BaseEventData data) { hovered = true; PaintSelection(); Focused?.Invoke(this); }
        public void OnDeselect(BaseEventData data) { hovered = false; PaintSelection(); }
        private void OnDisable()
        {
            hovered = false; pulse = shake = feedbackTime = 0;
            if (visual != null) { visual.localScale = Vector3.one; visual.localRotation = Quaternion.identity; }
            ResetFeedback(); PaintSelection();
        }
        private void ResetFeedback()
        {
            feedbackTime = 0;
            if (feedback == null) return;
            feedback.text = "";
            if (feedbackOriginCaptured) feedback.rectTransform.anchoredPosition = feedbackOrigin;
        }
    }
}
