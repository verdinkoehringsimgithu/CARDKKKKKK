using UnityEngine;

namespace CardKKK.IslandSurvival.Unity
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class IslandDesktopLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport, content;
        [SerializeField] private Vector2 logicalSize = new Vector2(1600, 900);
        private bool applying;
        private Vector2 previous;
        private void OnEnable() => Apply(true);
        private void OnValidate() => Apply(true);
        private void OnRectTransformDimensionsChange() => Apply(false);
        private void LateUpdate() => Apply(false);
        private void Apply(bool force)
        {
            if (applying || viewport == null || content == null || logicalSize.x <= 0 || logicalSize.y <= 0) return;
            Vector2 size = viewport.rect.size;
            if (size.x <= 0 || size.y <= 0 || !force && size == previous) return;
            applying = true; previous = size;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f); content.anchoredPosition = Vector2.zero; content.sizeDelta = logicalSize;
            float scale = Mathf.Min(size.x / logicalSize.x, size.y / logicalSize.y); content.localScale = new Vector3(scale, scale, 1); applying = false;
        }
    }
}
