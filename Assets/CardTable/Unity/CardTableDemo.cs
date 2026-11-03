using System;
using UnityEngine;

namespace CardKKK.Tabletop.Unity
{
    /// <summary>场景装配入口，只负责连接配置、控制器、界面和输入。</summary>
    public sealed class CardTableDemo : MonoBehaviour
    {
        [SerializeField, InspectorName("游戏配置")] private CardTableConfiguration configuration;
        [SerializeField, InspectorName("桌面界面")] private CardTableView view;
        [SerializeField, InspectorName("快捷键输入")] private TableInputRouter input;
        private CardTableController controller;
        public CardTableConfiguration Configuration => configuration;
        public CardTableView View => view;

        private void Start()
        {
            if (configuration == null || view == null || input == null)
            { ReportError("场景缺少游戏配置、桌面界面或快捷键输入引用。"); return; }
            if (!configuration.Validate(out string error)) { ReportError(error); return; }
            try
            {
                controller = new CardTableController(configuration, () => view.IsAnimating);
                view.Initialize(controller);
                input.Bind(controller);
            }
            catch (Exception exception)
            {
                ReportError("桌面初始化失败：" + exception.Message);
                view.Unbind(); input.Bind(null);
                controller?.Dispose(); controller = null;
            }
        }
        private void OnEnable() { if (controller != null) input.Bind(controller); }
        private void OnDisable() { input?.Bind(null); controller?.CancelDrag(); }
        private void OnApplicationFocus(bool focused) { if (!focused) controller?.CancelDrag(); }
        private void OnDestroy() { view?.Unbind(); controller?.Dispose(); }
        private void ReportError(string error)
        { Debug.LogError(error, this); if (view != null) view.ShowMessage(error); }
    }
}
