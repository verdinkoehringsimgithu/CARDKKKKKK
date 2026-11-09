using System;
using System.IO;
using CardKKK.IslandSurvival.Core;
using CardKKK.TestGameplay.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardKKK.IslandSurvival.Unity
{
    public sealed class SurvivalBootstrap : MonoBehaviour
    {
        [SerializeField] private SurvivalConfiguration configuration;
        [SerializeField] private SurvivalView view;
        private SurvivalEngine engine;
        private SurvivalSaveStore saves, manualSaves;
        private double secondsPerMinute;
        private int maximumAdvance;
        private double accumulated;
        private float saveClock;
        private bool dirty, allowSave = true, returning;
        private void Start()
        {
            try
            {
                CreateEngine();
                string message = "";
                if (saves.HasSave && !saves.Load(engine, out message)) allowSave = false;
                BindView(); if (message != "") view.Message(message);
            }
            catch (Exception exception)
            { Debug.LogException(exception); if (view != null) view.Message("孤岛规则无法加载：" + exception.Message); enabled = false; }
        }
        private void CreateEngine()
        {
            SurvivalDefinition rules = configuration.BuildDefinition(); engine = new SurvivalEngine(rules);
            saves = new SurvivalSaveStore(Path.Combine(Application.persistentDataPath, "IslandSurvival", "island-v1.json"), rules);
            manualSaves = new SurvivalSaveStore(Path.Combine(Application.persistentDataPath, "IslandSurvival", "island-manual-v1.json"), rules);
            secondsPerMinute = rules.RealSecondsPerMinute; maximumAdvance = rules.MaxMinutesPerAdvance;
            accumulated = 0;
        }
        private void BindView() => view.Bind(engine, configuration, Run, Save, Load, RestoreBackup, Restart, Return, Quit);
        private void Update()
        {
            if (engine == null) return;
            if (!engine.Paused && !engine.Dead)
            {
                accumulated += Math.Min(1, Time.unscaledDeltaTime) * engine.Speed / secondsPerMinute;
                int minutes = Math.Min((int)accumulated, maximumAdvance);
                if (minutes > 0)
                {
                    accumulated -= minutes; SurvivalResult result = engine.Advance(minutes);
                    if (!result.Success) { engine.SetPaused(true); view.Message(result.Error); }
                    else { dirty = true; view.Render(); }
                }
            }
            else accumulated = 0;
            saveClock += Time.unscaledDeltaTime;
            if (dirty && allowSave && saveClock >= 2) AutoSave();
        }
        private void Run(Func<SurvivalEngine, SurvivalResult> command)
        {
            SurvivalResult result = command(engine);
            if (result.Success) view.ClearMessage();
            view.Render();
            if (!result.Success) { view.Message(result.Error); view.Feedback(false); }
            else { if (result.Description != "") view.Message(result.Description); if (result.Changed) { dirty = true; if (allowSave) AutoSave(); } }
        }
        private void AutoSave()
        {
            saveClock = 0;
            if (saves.Save(engine.Snapshot, out string error)) dirty = false;
            else { allowSave = false; view.Message(error); }
        }
        private void Save()
        { bool saved = manualSaves.Save(engine.Snapshot, out string error); view.Message(saved ? "已保存手动存档；自动进度另行保存。" : error); }
        private void Load()
        {
            bool loaded = manualSaves.Load(engine, out string message);
            if (loaded) { accumulated = 0; allowSave = true; dirty = false; view.Render(); }
            else { allowSave = false; accumulated = 0; engine.SetPaused(true); view.Render(); }
            view.Message(message == "" ? "已读取，时间暂停。" : message);
        }
        private void RestoreBackup()
        {
            if (saves.LoadBackup(engine, out string message)) { accumulated = 0; allowSave = true; dirty = false; view.Render(); }
            else { allowSave = false; engine.SetPaused(true); view.Render(); }
            view.Message(message);
        }
        private void Restart()
        { CreateEngine(); allowSave = true; dirty = true; BindView(); AutoSave(); view.Message("新营地已准备，时间暂停。"); }
        private void Return()
        {
            if (returning) return;
            if (allowSave) AutoSave(); returning = true;
            if (TestGameplayEntry.TryReturnToTable()) return;
            try { SceneManager.LoadSceneAsync("Assets/CardTable/Scenes/CardTableDemo.unity", LoadSceneMode.Single); }
            catch (Exception exception) { returning = false; Debug.LogException(exception); view.Message("返回牌桌失败：" + exception.Message); }
        }
        private void Quit()
        {
            allowSave = true; dirty = true; AutoSave();
            if (!allowSave) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void OnApplicationFocus(bool focused)
        { if (!focused && engine != null) { engine.SetPaused(true); accumulated = 0; view.Render(); if (allowSave) AutoSave(); } }
        private void OnApplicationPause(bool paused)
        { if (paused && engine != null) { engine.SetPaused(true); accumulated = 0; view.Render(); if (allowSave) AutoSave(); } }
        private void OnApplicationQuit() { if (engine != null && allowSave) AutoSave(); }
    }
}
