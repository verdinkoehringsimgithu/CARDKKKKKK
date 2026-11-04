using System;
using System.IO;
using CardKKK.TestGameplay.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardKKK.TestGameplay.Unity
{
    /// <summary>独立工坊的装配与命令入口；不引用原牌桌控制器。</summary>
    public sealed class WorkshopBootstrap : MonoBehaviour
    {
        [SerializeField, InspectorName("测试玩法配置")] private WorkshopConfiguration configuration;
        [SerializeField, InspectorName("测试玩法界面")] private WorkshopView view;
        [SerializeField, InspectorName("进入时继续独立存档")] private bool resumeSave = true;
        private WorkshopEngine engine;
        private WorkshopSaveStore saves;
        private bool leaving;
        private bool allowAutomaticSave = true;

        private void Start()
        {
            if (view == null) { Debug.LogError("测试玩法缺少界面引用。", this); return; }
            view.ReturnRequested += Return;
            view.SaveRequested += Save;
            view.LoadRequested += Load;
            view.RestartRequested += Restart;
            view.WaitRequested += Wait;
            view.RecipeRequested += StartRecipe;
            view.GatherRequested += Gather;
            if (configuration == null)
            { view.ShowMessage("测试玩法缺少配置。", true); return; }
            if (!configuration.Validate(out string error))
            { view.ShowMessage(error, true); return; }
            try
            {
                WorkshopDefinition definition = configuration.ToDefinition();
                engine = new WorkshopEngine(definition, configuration.DefaultScenarioId);
                engine.ObserverFailed += ReportObserverFailure;
                // 配置只允许稳定标识；路径仍固定在测试玩法自己的子目录。
                string scenarioName = configuration.DefaultScenarioId;
                foreach (char part in scenarioName)
                    if (!char.IsLetterOrDigit(part) && part != '-' && part != '_')
                        throw new ArgumentException("开局标识只允许字母、数字、横线和下划线。");
                saves = new WorkshopSaveStore(Path.Combine(Application.persistentDataPath,
                    "TestGameplay", "workshop-" + scenarioName + "-v1.json"), definition);
                view.Initialize(engine, configuration);
                if (resumeSave && saves.HasSave && !saves.Load(engine, out string note))
                {
                    allowAutomaticSave = false;
                    view.ShowMessage(note + " 原档已保留，可重新开始或手动保存。", true);
                }
            }
            catch (Exception exception)
            { view.ShowMessage("测试玩法初始化失败，请检查玩法配置。", true); Debug.LogException(exception, this); }
        }

        private void Update()
        {
            if (leaving) return;
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F9)) Load();
        }
        private void StartRecipe(string recipeId) { if (engine != null) Complete(engine.StartRecipe(recipeId)); }
        private void Gather(string gatherId) { if (engine != null) Complete(engine.Gather(gatherId)); }
        private void Wait() { if (engine != null) Complete(engine.WaitForNextCompletion()); }

        private void Complete(WorkshopResult result)
        {
            if (!result.Success) { view.ShowMessage(result.Error); return; }
            if (result.GoalJustCompleted)
                view.ShowMessage("目标完成，可以继续制作。");
        }
        private void Save()
        {
            if (engine == null || saves == null) return;
            bool success = saves.Save(engine.Snapshot, out string error);
            if (success) allowAutomaticSave = true;
            view.ShowMessage(success ? "已保存。" : error);
        }
        private void Load()
        {
            if (engine == null || saves == null) return;
            if (saves.Load(engine, out string note))
            {
                allowAutomaticSave = true;
                view.ShowMessage(string.IsNullOrEmpty(note) ? "已读取。" : note);
            }
            else view.ShowMessage(note);
        }
        private void Restart()
        {
            if (engine == null) return;
            WorkshopResult result = engine.Restart(configuration.DefaultScenarioId);
            Complete(result);
            if (result.Success && saves != null)
            {
                allowAutomaticSave = saves.Save(engine.Snapshot, out string error);
                if (!allowAutomaticSave) view.ShowMessage(error);
            }
        }
        private void Return()
        {
            if (leaving) return;
            if (allowAutomaticSave && engine != null && saves != null &&
                !saves.Save(engine.Snapshot, out string error))
                Debug.LogWarning(error, this);
            leaving = true;
            if (!TestGameplayEntry.TryReturnToTable())
            {
                // 独立打开测试场景时没有被挂起的牌桌，才创建默认牌桌。
                try { SceneManager.LoadSceneAsync("Assets/CardTable/Scenes/CardTableDemo.unity", LoadSceneMode.Single); }
                catch (Exception exception)
                { leaving = false; view?.ShowMessage("返回失败，未能载入原牌桌。"); Debug.LogException(exception, this); }
            }
        }
        private void OnApplicationQuit()
        {
            if (!leaving && allowAutomaticSave && engine != null && saves != null &&
                !saves.Save(engine.Snapshot, out string error)) Debug.LogWarning(error, this);
        }
        private void OnDestroy()
        {
            if (view == null) return;
            if (engine != null) engine.ObserverFailed -= ReportObserverFailure;
            view.ReturnRequested -= Return;
            view.SaveRequested -= Save;
            view.LoadRequested -= Load;
            view.RestartRequested -= Restart;
            view.WaitRequested -= Wait;
            view.RecipeRequested -= StartRecipe;
            view.GatherRequested -= Gather;
            view.Unbind();
        }
        private void ReportObserverFailure(Exception exception) { Debug.LogException(exception, this); }
    }
}
