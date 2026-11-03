using UnityEngine;
using UnityEngine.UI;

namespace CardKKK.Tabletop.Unity
{
    /// <summary>在预制体中配置按钮动作；禁用或裁剪的动作自动隐藏。</summary>
    [RequireComponent(typeof(Button))]
    public sealed class TableActionButton : MonoBehaviour
    {
        [SerializeField, InspectorName("动作")] private TableAction action;
        [SerializeField, InspectorName("按钮")] private Button button;
        [SerializeField, InspectorName("标题")] private Text caption;
        private CardTableController controller;
        private string definitionId;
        public void Bind(CardTableController value, string id = null, string label = null)
        {
            Unbind(); controller = value; definitionId = id;
            if (label != null) caption.text = label;
            gameObject.SetActive(value != null && value.IsAvailable(action));
            if (value != null) button.onClick.AddListener(Invoke);
        }
        public void Unbind() { if (button != null) button.onClick.RemoveListener(Invoke); controller = null; }
        private void Invoke()
        {
            // 按钮只接受鼠标点击；键盘由统一路由处理，避免空格重复触发。
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            controller?.Execute(action, definitionId);
        }
        private void OnDestroy() => Unbind();
    }
}
