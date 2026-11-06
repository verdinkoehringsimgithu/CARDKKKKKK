using System;
using CardKKK.TestGameplay.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CardKKK.IslandSurvival.Unity
{
    /// <summary>独立孤岛入口的准备界面；尚不提供生存玩法操作。</summary>
    public sealed class IslandPreparationView : MonoBehaviour
    {
        [SerializeField, InspectorName("返回牌桌按钮")] private Button returnButton;
        [SerializeField, InspectorName("状态文字")] private Text statusText;
        [SerializeField, InspectorName("原牌桌场景")]
        private string tableScenePath = "Assets/CardTable/Scenes/CardTableDemo.unity";
        private bool returning;

        private void Awake()
        {
            if (returnButton == null)
            {
                Debug.LogError("孤岛生存准备界面缺少返回按钮。", this);
                enabled = false;
                return;
            }
            returnButton.onClick.AddListener(ReturnToTable);
        }

        private void Start()
        {
            if (returnButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) ReturnToTable();
        }

        public void ReturnToTable()
        {
            if (returning) return;
            returning = true;
            returnButton.interactable = false;
            if (TestGameplayEntry.TryReturnToTable()) return;

            // 直接在独立场景按Play时，没有保留中的原牌桌，返回默认桌面入口。
            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(tableScenePath, LoadSceneMode.Single);
                if (operation != null) return;
                ShowReturnError("无法返回牌桌：未能加载场景。");
            }
            catch (Exception exception)
            {
                Debug.LogError("无法返回牌桌：" + exception.Message, this);
                ShowReturnError("无法返回牌桌，请检查场景引用。");
            }
        }

        private void ShowReturnError(string message)
        {
            if (statusText != null) statusText.text = message;
            returning = false;
            returnButton.interactable = true;
        }

        private void OnDestroy()
        {
            if (returnButton != null) returnButton.onClick.RemoveListener(ReturnToTable);
        }
    }
}
