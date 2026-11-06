using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.Spellcraft.Unity
{
    /// <summary>三段装置的可见规则、可用次数与触发反馈。</summary>
    public sealed class ForgeDeviceView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private RectTransform visual;
        [SerializeField] private Image frame, artwork, connection;
        [SerializeField] private GameObject selection;
        [SerializeField] private Text heading, trigger, effect, modifier, limit, actionLabel;
        [SerializeField] private Button button;
        private bool selected, hovered, bound, connectionColorCaptured;
        private Color connectionColor;
        private float pulse;
        private string detail;
        public int Index { get; private set; }
        public Button Button => button;
        public event Action<int> Clicked;
        public event Action<int> PreviewRequested;
        public event Action<string> Inspected;

        public void Initialize(ForgeConfiguration config)
        {
            if (!bound) { button.onClick.AddListener(() => Clicked?.Invoke(Index)); bound = true; }
            foreach (Text text in GetComponentsInChildren<Text>(true)) text.font = config.Font;
            // 装置是横向信息面板，不能拉伸竖版插画卡框。
            frame.sprite = null;
            frame.type = Image.Type.Simple;
            if (connection != null && !connectionColorCaptured)
            { connectionColor = connection.color; connectionColorCaptured = true; }
        }

        public void Bind(int index, string title, string when, string what, string how,
            string uses, string action, Sprite art, bool available, string rules)
        {
            Index = index; heading.text = title; trigger.text = when; effect.text = what;
            modifier.text = how; limit.text = uses; actionLabel.text = action;
            artwork.sprite = art; artwork.enabled = art != null;
            button.interactable = available; detail = rules; PaintSelection();
        }

        public void SetSelected(bool value) { selected = value; PaintSelection(); }
        public void Present() { if (isActiveAndEnabled) pulse = 0.28f; }
        public void Animate(float delta)
        {
            if (pulse <= 0) return;
            pulse = Mathf.Max(0, pulse - delta);
            visual.localScale = Vector3.one * (1 + Mathf.Sin(pulse / 0.28f * Mathf.PI) * 0.045f);
            if (connection != null) connection.color = Color.Lerp(connectionColor,
                new Color(1, 0.83f, 0.28f), Mathf.Sin(pulse / 0.28f * Mathf.PI));
        }
        private void PaintSelection() { if (selection != null) selection.SetActive(selected || hovered); }
        public void OnPointerEnter(PointerEventData data) { hovered = true; PaintSelection(); Inspected?.Invoke(detail); PreviewRequested?.Invoke(Index); }
        public void OnPointerExit(PointerEventData data) { hovered = false; PaintSelection(); Inspected?.Invoke(""); }
        public void OnSelect(BaseEventData data) { hovered = true; PaintSelection(); Inspected?.Invoke(detail); PreviewRequested?.Invoke(Index); }
        public void OnDeselect(BaseEventData data) { hovered = false; PaintSelection(); Inspected?.Invoke(""); }
        private void OnDisable()
        {
            pulse = 0; hovered = false;
            if (visual != null) visual.localScale = Vector3.one;
            if (connection != null && connectionColorCaptured) connection.color = connectionColor;
            PaintSelection();
        }
    }
}
