using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CardKKK.TestGameplay.Unity
{
    /// <summary>只负责独立场景的进入/返回，不读取或改写原牌桌数据。</summary>
    public sealed class TestGameplayEntry : MonoBehaviour
    {
        [SerializeField, InspectorName("原牌桌根对象")] private GameObject originalTableRoot;
        [SerializeField, InspectorName("入口画布")] private Canvas entryCanvas;
        [SerializeField, InspectorName("测试玩法按钮")] private Button playButton;
        [SerializeField, InspectorName("入口文字")] private Text label;
        [SerializeField, InspectorName("中文字体")] private Font chineseFont;
        [SerializeField, InspectorName("孤岛生存按钮")] private Button islandButton;
        [SerializeField, InspectorName("孤岛生存文字")] private Text islandLabel;
        [SerializeField, InspectorName("孤岛生存场景")]
        private string islandScenePath = "Assets/IslandSurvival/Scenes/IslandSurvival.unity";
        [SerializeField, InspectorName("测试玩法场景")]
        private string gameplayScenePath = "Assets/Archives/Scenes/Spellcraft.unity";
        private static TestGameplayEntry activeEntry;
        private Scene originalScene;
        private Scene gameplayScene;
        private bool busy;
        private bool originalWasActive;
        private bool returnAfterEnter;
        private string activeScenePath;

        private void Awake()
        {
            if (playButton == null || entryCanvas == null || originalTableRoot == null)
            { Debug.LogError("测试玩法入口缺少场景引用。", this); enabled = false; return; }
            originalScene = gameObject.scene;
            if (label != null)
            {
                label.text = "测试玩法";
                if (chineseFont != null) label.font = chineseFont;
            }
            playButton.onClick.AddListener(Enter);
            if (islandButton != null)
            {
                islandButton.onClick.AddListener(EnterIsland);
                if (islandLabel != null)
                {
                    islandLabel.text = "孤岛生存";
                    if (chineseFont != null) islandLabel.font = chineseFont;
                }
            }
        }

        public void Enter()
        {
            EnterScene(gameplayScenePath);
        }

        public void EnterIsland()
        {
            EnterScene(islandScenePath);
        }

        private void EnterScene(string scenePath)
        {
            if (busy || gameplayScene.IsValid() || activeEntry != null) return;
            if (string.IsNullOrWhiteSpace(scenePath))
            { Debug.LogError("独立玩法入口缺少场景路径。", this); return; }
            activeScenePath = scenePath;
            StartCoroutine(EnterRoutine());
        }

        public static bool TryReturnToTable()
        {
            if (activeEntry == null) return false;
            if (activeEntry.busy)
            { activeEntry.returnAfterEnter = true; return true; }
            if (!activeEntry.gameplayScene.IsValid()) return false;
            activeEntry.StartCoroutine(activeEntry.ReturnRoutine());
            return true;
        }

        private IEnumerator EnterRoutine()
        {
            busy = true;
            playButton.interactable = false;
            if (islandButton != null) islandButton.interactable = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            originalWasActive = originalTableRoot.activeSelf;
            originalTableRoot.SetActive(false);
            entryCanvas.gameObject.SetActive(false);
            activeEntry = this;
            AsyncOperation operation = null;
            string failure = null;
            try { operation = SceneManager.LoadSceneAsync(activeScenePath, LoadSceneMode.Additive); }
            catch (Exception exception) { failure = exception.Message; }
            if (operation == null)
            {
                RestoreTable();
                Debug.LogError("无法进入独立玩法：" + (failure ?? "未能加载场景。"), this);
                yield break;
            }
            yield return operation;
            gameplayScene = SceneManager.GetSceneByPath(activeScenePath);
            if (!gameplayScene.IsValid() || !gameplayScene.isLoaded)
            {
                RestoreTable();
                Debug.LogError("独立玩法场景没有完成加载。", this);
                yield break;
            }
            SceneManager.SetActiveScene(gameplayScene);
            busy = false;
            if (returnAfterEnter) StartCoroutine(ReturnRoutine());
        }

        private IEnumerator ReturnRoutine()
        {
            busy = true;
            // 先关闭测试场景，保证两套相机、事件系统和音频监听器不会同时启用。
            foreach (GameObject root in gameplayScene.GetRootGameObjects()) root.SetActive(false);
            if (originalScene.IsValid() && originalScene.isLoaded)
                SceneManager.SetActiveScene(originalScene);
            AsyncOperation operation = SceneManager.UnloadSceneAsync(gameplayScene);
            if (operation != null) yield return operation;
            gameplayScene = default(Scene);
            RestoreTable();
        }

        private void RestoreTable()
        {
            if (originalTableRoot != null) originalTableRoot.SetActive(originalWasActive);
            if (entryCanvas != null) entryCanvas.gameObject.SetActive(true);
            if (playButton != null) playButton.interactable = true;
            if (islandButton != null) islandButton.interactable = true;
            if (activeEntry == this) activeEntry = null;
            returnAfterEnter = false;
            activeScenePath = null;
            busy = false;
        }

        private void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveListener(Enter);
            if (islandButton != null) islandButton.onClick.RemoveListener(EnterIsland);
            if (activeEntry == this) activeEntry = null;
        }
    }
}
