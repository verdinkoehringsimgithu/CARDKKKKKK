using UnityEngine;

namespace CardKKK.Spellcraft.Unity
{
    /// <summary>固定逻辑桌面只做整体等比缩放，字体和卡片保持相同比例。</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ForgeDesktopLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private Vector2 logicalSize = new Vector2(1600, 900);
        private bool applying;
        private Vector2 previousSize;
        private void OnEnable() { Apply(true); }
        private void OnValidate() { Apply(true); }
        private void OnRectTransformDimensionsChange() { Apply(false); }
        private void LateUpdate() { Apply(false); }
        private void Apply(bool force)
        {
            if (applying || viewport == null || content == null || logicalSize.x <= 0 || logicalSize.y <= 0) return;
            Vector2 size = viewport.rect.size;
            if (size.x <= 0 || size.y <= 0 || (!force && size == previousSize)) return;
            applying = true;
            previousSize = size;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = logicalSize;
            float scale = Mathf.Min(size.x / logicalSize.x, size.y / logicalSize.y);
            content.localScale = new Vector3(scale, scale, 1);
            applying = false;
        }
    }
}
