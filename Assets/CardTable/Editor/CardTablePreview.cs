using System.Collections.Generic;
using CardKKK.Tabletop.Core;
using CardKKK.Tabletop.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CardKKK.Tabletop.Editor
{
    /// <summary>编辑器专属预览。保存或切换模式前还原所有实际组件，不留下临时引用。</summary>
    [InitializeOnLoad]
    public static class CardTablePreview
    {
        private const string PreviewName = "编辑预览（不保存）";
        private static readonly List<GameObject> roots = new List<GameObject>();
        private static readonly Dictionary<Text, TextState> texts = new Dictionary<Text, TextState>();
        private static readonly List<RectState> rectangles = new List<RectState>();
        private static readonly List<ImageState> images = new List<ImageState>();
        private static bool queued;
        private static Font fallback;

        private sealed class TextState
        {
            public Text Target;
            public Font OriginalFont, AppliedFont;
            public string OriginalText, AppliedText;
            public bool FontChanged, TextChanged;
            public TextState(Text target)
            { Target = target; OriginalFont = target.font; OriginalText = target.text; }
            public void Restore()
            {
                if (Target == null) return;
                // 用户在预览后手动改过的字段归用户所有，恢复时保留它。
                if (FontChanged && Target.font == AppliedFont) Target.font = OriginalFont;
                if (TextChanged && Target.text == AppliedText) Target.text = OriginalText;
            }
        }
        private sealed class ImageState
        {
            private readonly Image target;
            private readonly Sprite originalSprite, appliedSprite;
            private readonly Color originalColor, appliedColor;
            private readonly Image.Type originalType, appliedType;

            public ImageState(Image target, CardTableTheme theme)
            {
                this.target = target;
                originalSprite = target.sprite; originalColor = target.color; originalType = target.type;
                CardTableArt.ApplySurface(target, theme);
                appliedSprite = target.sprite; appliedColor = target.color; appliedType = target.type;
            }
            public void Restore()
            {
                if (target == null) return;
                if (target.sprite == appliedSprite) target.sprite = originalSprite;
                if (target.color.Equals(appliedColor)) target.color = originalColor;
                if (target.type == appliedType) target.type = originalType;
            }
        }
        private sealed class RectState
        {
            public RectTransform Target;
            public Vector2 OriginalSize, OriginalPosition, AppliedSize, AppliedPosition;
            public Vector3 OriginalScale, AppliedScale;
            public RectState(RectTransform target)
            {
                Target = target; OriginalSize = target.sizeDelta;
                OriginalPosition = target.anchoredPosition; OriginalScale = target.localScale;
            }
            public void Apply(Vector2 size, Vector2 position, Vector3 scale)
            {
                AppliedSize = size; AppliedPosition = position; AppliedScale = scale;
                Target.sizeDelta = size; Target.anchoredPosition = position; Target.localScale = scale;
            }
            public void Restore()
            {
                if (Target == null) return;
                if (Target.sizeDelta.Equals(AppliedSize)) Target.sizeDelta = OriginalSize;
                if (Target.anchoredPosition.Equals(AppliedPosition)) Target.anchoredPosition = OriginalPosition;
                if (Target.localScale.Equals(AppliedScale)) Target.localScale = OriginalScale;
            }
        }

        static CardTablePreview()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => QueueRefresh();
            EditorSceneManager.sceneSaving += (scene, path) => ClearAll();
            EditorSceneManager.sceneSaved += scene => QueueRefresh();
            EditorSceneManager.sceneClosing += (scene, removing) => ClearAll();
            EditorApplication.projectChanged += QueueRefresh;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) ClearAll();
                if (state == PlayModeStateChange.EnteredEditMode) QueueRefresh();
            };
            AssemblyReloadEvents.beforeAssemblyReload += ClearAll;
            EditorApplication.quitting += ClearAll;
            QueueRefresh();
        }

        [MenuItem("孤岛生存/历史演示/自由牌桌/刷新编辑预览")]
        public static void QueueRefresh()
        {
            if (queued || EditorApplication.isPlayingOrWillChangePlaymode) return;
            queued = true; EditorApplication.delayCall += Refresh;
        }
        private static TextState Remember(Text target)
        {
            if (!texts.TryGetValue(target, out TextState state))
            { state = new TextState(target); texts.Add(target, state); }
            return state;
        }
        private static void SetFont(Text target, Font font)
        {
            TextState state = Remember(target);
            state.AppliedFont = font; state.FontChanged = true; target.font = font;
        }
        private static void SetText(Text target, string value)
        {
            if (target == null) return;
            TextState state = Remember(target);
            state.AppliedText = value; state.TextChanged = true; target.text = value;
        }

        private static void Refresh()
        {
            queued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ClearAll();
            foreach (CardTableDemo demo in Object.FindObjectsOfType<CardTableDemo>())
            {
                if (!demo.gameObject.scene.IsValid() || demo.View == null || demo.Configuration == null) continue;
                CardTableConfiguration config = demo.Configuration;
                var serialized = new SerializedObject(demo.View);
                Text status = serialized.FindProperty("status").objectReferenceValue as Text;
                if (!config.Validate(out string error)) { SetText(status, error); continue; }
                var layer = serialized.FindProperty("cardLayer").objectReferenceValue as RectTransform;
                var prefab = serialized.FindProperty("cardPrefab").objectReferenceValue as TableCardView;
                var table = serialized.FindProperty("board").objectReferenceValue as RectTransform;
                var area = serialized.FindProperty("boardArea").objectReferenceValue as RectTransform;
                if (layer == null || prefab == null || table == null || area == null) continue;
                if (serialized.FindProperty("surface").objectReferenceValue is Image surface)
                    images.Add(new ImageState(surface, config.Theme));
                Font font = config.Theme != null ? config.Theme.UiFont : null;
                if (font == null)
                {
                    if (fallback == null)
                    {
                        fallback = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "SimSun" }, 16);
                        fallback.hideFlags = HideFlags.HideAndDontSave;
                    }
                    font = fallback;
                }
                foreach (Text text in demo.View.GetComponentsInChildren<Text>(true)) SetFont(text, font);
                var root = new GameObject(PreviewName, typeof(RectTransform));
                roots.Add(root);
                root.hideFlags = HideFlags.HideAndDontSave;
                root.transform.SetParent(layer, false);
                var previewRect = (RectTransform)root.transform;
                previewRect.anchorMin = previewRect.anchorMax = previewRect.pivot = Vector2.zero;
                previewRect.anchoredPosition = Vector2.zero; previewRect.sizeDelta = Vector2.zero;
                CardCatalog catalog = config.BuildCatalog();
                BoardRules rules = config.BuildRules();
                Vector2 size = new Vector2(rules.Width, rules.Height);
                float scale = Mathf.Min(area.rect.width / rules.Width, area.rect.height / rules.Height);
                var rectangle = new RectState(table); rectangles.Add(rectangle);
                rectangle.Apply(size, (area.rect.size - size * scale) * 0.5f, new Vector3(scale, scale, 1));
                int pileIndex = 0;
                foreach (InitialPile group in config.InitialPiles)
                {
                    var pile = new PileData { Id = "preview-" + pileIndex++ };
                    for (int i = 0; i < group.Cards.Count; i++)
                    {
                        CardDefinition definition = group.Cards[i];
                        CardData card = catalog.CreateCard(definition.Id); card.Id = pile.Id + "-" + i;
                        TableCardView view = Object.Instantiate(prefab, previewRect);
                        view.name = definition.DisplayName;
                        view.Initialize(null, new Vector2(rules.CardWidth, rules.CardHeight), font, config.Theme);
                        view.Bind(card, pile, group.Position + Vector2.up * (i * rules.PileOffset), definition.Artwork, false, true);
                        view.enabled = false;
                    }
                }
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                    transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
                SetText(status, "");
            }
            SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate();
        }
        private static void ClearAll()
        {
            // 字体引用先还原，再释放临时字体；保存场景时只剩用户实际配置。
            foreach (TextState state in texts.Values) state.Restore();
            texts.Clear();
            foreach (RectState state in rectangles) state.Restore();
            rectangles.Clear();
            foreach (ImageState state in images) state.Restore();
            images.Clear();
            foreach (GameObject root in roots) if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
            if (fallback != null) { Object.DestroyImmediate(fallback); fallback = null; }
        }
    }
}
