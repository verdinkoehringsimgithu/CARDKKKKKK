using System;
using System.Collections.Generic;
using System.IO;
using CardKKK.IslandSurvival.Core;
using CardKKK.TestGameplay.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardKKK.IslandSurvival.Unity
{
    public sealed class VoyageBootstrap : MonoBehaviour
    {
        [SerializeField] private VoyageConfiguration configuration;
        [SerializeField] private VoyageView view;
        [SerializeField] private AudioSource ambience;
        [SerializeField] private AudioSource thoughtAudio;

        private VoyageEngine engine;
        private VoyageSaveStore automatic, manual;
        private double accumulated, secondsPerMinute;
        private float saveClock, ambientVolume, targetVolume;
        private int soundCursor;
        private readonly HashSet<string> audibleThoughts = new HashSet<string>(StringComparer.Ordinal);
        private bool running, dirty, allowAutosave, returning;

        private void Start()
        {
            try
            {
                CreateGame();
                string message = "";
                allowAutosave = !automatic.Exists || automatic.Load(engine, out message);
                Bind();
                if (ambience != null)
                {
                    ambientVolume = ambience.volume;
                    SyncAudio(engine.Snapshot, false);
                    ambience.volume = targetVolume;
                    if (ambience.clip != null) ambience.Play();
                }
                if (message.Length > 0) view.Message(message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (view != null) view.Message("出航规则无法加载：" + exception.Message);
                enabled = false;
            }
        }

        private void CreateGame()
        {
            if (configuration == null || view == null) throw new InvalidOperationException("未绑定出航规则或桌面。");
            VoyageDefinition definition = configuration.BuildDefinition();
            var newEngine = new VoyageEngine(definition, view.MeasureDesk());
            string directory = Path.Combine(Application.persistentDataPath, "IslandSurvival");
            automatic = new VoyageSaveStore(Path.Combine(directory, "voyage-auto-v1.json"), definition);
            manual = new VoyageSaveStore(Path.Combine(directory, "voyage-manual-v1.json"), definition);
            engine = newEngine;
            secondsPerMinute = definition.RealSecondsPerMinute;
            audibleThoughts.Clear();
            foreach (VoyageCardRule card in definition.Cards)
                if (card.Kind == VoyageCardKind.Thought && card.Tags.Contains("whisper")) audibleThoughts.Add(card.Id);
            SyncAudio(engine.Snapshot, false);
            accumulated = 0;
            saveClock = 0;
            running = false;
            dirty = false;
        }

        private void Bind() => view.Bind(engine, configuration, Run, Save, Load, Restart, ReturnToTable);

        private void Update()
        {
            if (engine == null || returning) return;
            if (running)
            {
                accumulated += Math.Min(Time.unscaledDeltaTime, 1f) / secondsPerMinute;
                int minutes = (int)Math.Min(12, accumulated);
                if (minutes > 0)
                {
                    accumulated -= minutes;
                    Run(value => value.Advance(minutes));
                }
            }
            if (ambience != null) ambience.volume = Mathf.MoveTowards(ambience.volume, targetVolume, Time.unscaledDeltaTime * .5f);
            saveClock += Time.unscaledDeltaTime;
            if (dirty && allowAutosave && saveClock >= 2f) SaveAutomatic();
        }

        private void Run(Func<VoyageEngine, VoyageResult> command)
        {
            if (engine == null || returning) return;
            VoyageResult result = command(engine);
            if (result.Changed)
            {
                VoyageState snapshot = engine.Snapshot;
                running = !snapshot.Paused && !snapshot.Won && !snapshot.Dead;
                dirty = true;
                SyncAudio(snapshot, true);
                view.Render();
            }
            if (!result.Success) view.Reject();
            if (result.Message.Length > 0) view.Message(result.Message);
        }

        private void SyncAudio(VoyageState snapshot, bool playNewThoughts)
        {
            float clarity = Mathf.InverseLerp(0f, 70f, (float)snapshot.San);
            targetVolume = ambientVolume * clarity * clarity;
            if (playNewThoughts && thoughtAudio != null && thoughtAudio.clip != null)
                foreach (VoyageCardState card in snapshot.Cards)
                    if (card.Id >= soundCursor && audibleThoughts.Contains(card.CardId))
                    {
                        thoughtAudio.Play();
                        break;
                    }
            soundCursor = snapshot.NextCardId;
        }

        private void SaveAutomatic()
        {
            saveClock = 0;
            if (automatic.Save(engine.Snapshot, out string message)) dirty = false;
            else
            {
                allowAutosave = false;
                view.Message(message);
            }
        }

        private void Save()
        {
            if (manual.Save(engine.Snapshot, out string message)) message = "已保存手动进度。";
            view.Message(message);
        }

        private void Load()
        {
            if (manual.Load(engine, out string message))
            {
                engine.SetPaused(true);
                accumulated = 0;
                running = false;
                dirty = true;
                SyncAudio(engine.Snapshot, false);
                view.Render();
                if (message.Length == 0) message = "已读取，时间暂停。";
            }
            view.Message(message);
        }

        private void Restart()
        {
            try
            {
                CreateGame();
                allowAutosave = true;
                dirty = true;
                Bind();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                view.Message("无法重新开始：" + exception.Message);
            }
        }

        private void ReturnToTable()
        {
            if (returning) return;
            if (dirty && allowAutosave) SaveAutomatic();
            returning = true;
            if (TestGameplayEntry.TryReturnToTable()) return;
            try { SceneManager.LoadSceneAsync("Assets/Archives/Scenes/CardTableDemo.unity", LoadSceneMode.Single); }
            catch (Exception exception)
            {
                returning = false;
                Debug.LogException(exception);
                view.Message("返回牌桌失败：" + exception.Message);
            }
        }

        private void PauseOnFocusLoss()
        {
            if (engine == null || returning) return;
            Run(value => value.SetPaused(true));
            if (dirty && allowAutosave) SaveAutomatic();
        }

        private void OnApplicationFocus(bool focused) { if (!focused) PauseOnFocusLoss(); }
        private void OnApplicationPause(bool paused) { if (paused) PauseOnFocusLoss(); }
        private void OnApplicationQuit() { if (dirty && allowAutosave && !returning) SaveAutomatic(); }
    }
}
