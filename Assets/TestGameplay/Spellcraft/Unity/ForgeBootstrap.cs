using System;
using System.IO;
using CardKKK.Spellcraft.Core;
using CardKKK.TestGameplay.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardKKK.Spellcraft.Unity
{
    /// <summary>只装配规则、存档与真实界面，场景内容不在启动时生成。</summary>
    public sealed class ForgeBootstrap : MonoBehaviour
    {
        [SerializeField, InspectorName("法术规则配置")] private ForgeConfiguration configuration;
        [SerializeField, InspectorName("法术工坊界面")] private ForgeView view;
        [SerializeField, InspectorName("继续独立存档")] private bool resumeSave = true;
        private ForgeEngine engine;
        private ForgeSaveStore saves;
        private bool leaving;
        private bool allowAutomaticSave;

        private void Start()
        {
            if (view == null) { Debug.LogError("法术工坊缺少界面引用。", this); return; }
            Bind(true);
            try
            {
                if (configuration == null) throw new ArgumentException("法术工坊缺少配置。");
                ForgeDefinition definition = configuration.BuildDefinition();
                engine = new ForgeEngine(definition, NewSeed());
                saves = new ForgeSaveStore(Path.Combine(Application.persistentDataPath,
                    "TestGameplay", "spellcraft-v1.json"), definition, configuration.PreviousPresentation(definition));
                bool restored = true;
                string note = "";
                if (resumeSave && saves.HasSave) restored = saves.Load(engine, out note);
                allowAutomaticSave = restored;
                view.Initialize(engine, configuration);
                view.Render(engine.State);
                if (!restored) view.ShowMessage(note + " 原存档已保留。", true);
                else if (!string.IsNullOrEmpty(note)) view.ShowMessage(note);
            }
            catch (Exception exception)
            {
                view.ShowMessage("法术工坊初始化失败，请检查规则配置。", true);
                Debug.LogException(exception, this);
            }
        }

        private void Bind(bool subscribe)
        {
            if (subscribe)
            {
                view.CardRequested += PlayCard; view.CircuitRequested += Activate;
                view.TargetRequested += SelectEnemy;
                view.CircuitConfigured += Configure; view.CircuitCleared += Clear;
                view.EndTurnRequested += EndTurn; view.StartBattleRequested += StartBattle;
                view.RewardRequested += Reward; view.ReturnRequested += Return;
                view.RestartRequested += Restart; view.SaveRequested += Save; view.LoadRequested += Load;
            }
            else
            {
                view.CardRequested -= PlayCard; view.CircuitRequested -= Activate;
                view.TargetRequested -= SelectEnemy;
                view.CircuitConfigured -= Configure; view.CircuitCleared -= Clear;
                view.EndTurnRequested -= EndTurn; view.StartBattleRequested -= StartBattle;
                view.RewardRequested -= Reward; view.ReturnRequested -= Return;
                view.RestartRequested -= Restart; view.SaveRequested -= Save; view.LoadRequested -= Load;
            }
        }

        private void SelectEnemy(int target) { if (Ready) Complete(engine.SelectEnemy(target)); }
        private void PlayCard(int card, int target) { if (Ready) Complete(engine.PlayCard(card, target)); }
        private void Activate(int slot, int target) { if (Ready) Complete(engine.ActivateCircuit(slot, target)); }
        private void Configure(int slot, string trigger, string effect, string modifier)
        { if (Ready) Complete(engine.ConfigureCircuit(slot, trigger, effect, modifier)); }
        private void Clear(int slot) { if (Ready) Complete(engine.ClearCircuit(slot)); }
        private void StartBattle() { if (Ready) Complete(engine.StartBattle()); }
        private void EndTurn() { if (Ready) Complete(engine.EndTurn()); }
        private void Reward(string module) { if (Ready) Complete(engine.ChooseReward(module)); }
        private bool Ready => !leaving && engine != null;

        private void Complete(ForgeResult result)
        {
            if (!result.Success) { view.ShowMessage(result.Error, true); return; }
            view.Render(engine.State);
            view.Present(result);
            if (allowAutomaticSave && saves != null && !saves.Save(engine.State, out string error))
                view.ShowMessage(error, true);
        }

        private void Save()
        {
            if (!Ready || saves == null) return;
            bool success = saves.Save(engine.State, out string error);
            if (success) allowAutomaticSave = true;
            view.ShowMessage(success ? "已保存。" : error, !success);
        }

        private void Load()
        {
            if (!Ready || saves == null) return;
            if (saves.Load(engine, out string note))
            {
                allowAutomaticSave = true;
                view.Render(engine.State);
                view.ShowMessage(string.IsNullOrEmpty(note) ? "已读取。" : note);
            }
            else
            {
                allowAutomaticSave = false;
                view.ShowMessage(note + " 原存档已保留，自动保存已暂停。", true);
            }
        }

        private void Restart()
        {
            if (leaving || configuration == null) return;
            try
            {
                ForgeDefinition definition = configuration.BuildDefinition();
                engine = new ForgeEngine(definition, NewSeed());
                saves = new ForgeSaveStore(Path.Combine(Application.persistentDataPath,
                    "TestGameplay", "spellcraft-v1.json"), definition, configuration.PreviousPresentation(definition));
                view.Unbind();
                view.Initialize(engine, configuration);
                view.Render(engine.State);
                allowAutomaticSave = saves.Save(engine.State, out string error);
                if (!allowAutomaticSave) view.ShowMessage("新开局已开始，保存未完成。", true);
            }
            catch (Exception exception) { view.ShowMessage("无法重新开始。", true); Debug.LogException(exception, this); }
        }

        private void Return()
        {
            if (leaving) return;
            SaveAutomatically();
            leaving = true;
            if (TestGameplayEntry.TryReturnToTable()) return;
            try { SceneManager.LoadSceneAsync("Assets/Archives/Scenes/CardTableDemo.unity", LoadSceneMode.Single); }
            catch (Exception exception)
            { leaving = false; view.ShowMessage("未能返回牌桌。", true); Debug.LogException(exception, this); }
        }

        private static int NewSeed() => unchecked((int)DateTime.UtcNow.Ticks ^ Environment.TickCount);
        private void SaveAutomatically()
        {
            if (allowAutomaticSave && engine != null && saves != null &&
                !saves.Save(engine.State, out string error)) Debug.LogWarning(error, this);
        }
        private void OnApplicationPause(bool paused) { if (paused && !leaving) SaveAutomatically(); }
        private void OnApplicationQuit() { if (!leaving) SaveAutomatically(); }
        private void OnDestroy() { if (view != null) { Bind(false); view.Unbind(); } }
    }
}
