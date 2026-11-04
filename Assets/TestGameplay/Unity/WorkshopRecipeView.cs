using System;
using UnityEngine;
using UnityEngine.UI;

namespace CardKKK.TestGameplay.Unity
{
    /// <summary>一条可查的工艺或采集选项；按钮仅转交玩家的明确选择。</summary>
    public sealed class WorkshopRecipeView : MonoBehaviour
    {
        [SerializeField, InspectorName("名称")] private Text heading;
        [SerializeField, InspectorName("投入和产出")] private Text description;
        [SerializeField, InspectorName("人物和后台时间")] private Text time;
        [SerializeField, InspectorName("条件")] private Text condition;
        [SerializeField, InspectorName("执行")] private Button execute;
        [SerializeField, InspectorName("执行文字")] private Text executeText;
        [SerializeField, InspectorName("结果物件图")] private Image artwork;
        private Action chosen;
        private bool listenerBound;
        public RectTransform Rect => (RectTransform)transform;
        public void Initialize(Font font)
        {
            heading.font = description.font = time.font = condition.font = executeText.font = font;
            execute.navigation = new Navigation { mode = Navigation.Mode.None };
            if (!listenerBound) { execute.onClick.AddListener(Execute); listenerBound = true; }
        }
        public void Bind(string name, string details, string timing, string reason, string buttonText,
            bool canExecute, Sprite sprite, Action action)
        {
            heading.text = name; description.text = details; time.text = timing;
            condition.text = reason ?? ""; condition.gameObject.SetActive(!string.IsNullOrEmpty(reason));
            executeText.text = buttonText; execute.interactable = canExecute;
            artwork.sprite = sprite; artwork.enabled = sprite != null; artwork.preserveAspect = true;
            chosen = action;
        }
        private void Execute() => chosen?.Invoke();
        private void OnDestroy() { if (execute != null) execute.onClick.RemoveListener(Execute); chosen = null; }
    }
}
