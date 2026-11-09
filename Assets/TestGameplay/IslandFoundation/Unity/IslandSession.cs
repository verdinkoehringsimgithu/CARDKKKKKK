using System;
using System.Collections.Generic;
using CardKKK.Island.Core;
using UnityEngine;

namespace CardKKK.Island.Unity
{
    // The scene owns this session. Input, rendering and file storage call it independently.
    public sealed class IslandSession : MonoBehaviour
    {
        [SerializeField] private IslandConfiguration configuration;
        private IslandEngine engine;
        public IslandConfiguration Configuration => configuration;
        public bool IsReady => engine != null;
        public string LastError { get; private set; }
        public IslandSnapshot Snapshot => engine?.Snapshot;
        public event Action<IslandSnapshot> Changed;

        private void Awake() { Initialize(); }

        public bool Initialize()
        {
            try
            {
                if (configuration == null) throw new ArgumentException("缺少孤岛玩法配置。");
                var replacement = configuration.CreateEngine();
                replacement.ObserverFailed += LogObserverFailure;
                replacement.Changed += ForwardChanged;
                if (engine != null)
                {
                    engine.Changed -= ForwardChanged;
                    engine.ObserverFailed -= LogObserverFailure;
                }
                engine = replacement;
                LastError = "";
                ForwardChanged(engine.Snapshot);
                return true;
            }
            catch (ArgumentException error) { LastError = error.Message; return false; }
        }

        public IslandResult StartRecipe(string recipeId, string regionId, IReadOnlyList<SlotBinding> bindings)
        { return Record(Engine.StartRecipe(recipeId, regionId, bindings)); }

        public IslandResult Move(string instanceId, string regionId, Vector2 position)
        { return Record(Engine.Move(instanceId, regionId, position.x, position.y)); }

        public IslandResult AdvanceTo(long minute) { return Record(Engine.AdvanceTo(minute)); }

        public string ExportJson()
        { return JsonUtility.ToJson(Engine.Snapshot, true); }

        public bool RestoreJson(string json)
        {
            if (engine == null) { LastError = "孤岛会话尚未初始化。"; return false; }
            try
            {
                return Record(engine.Restore(JsonUtility.FromJson<IslandSnapshot>(json))).Success;
            }
            catch (ArgumentException error) { LastError = error.Message; return false; }
        }

        private void LogObserverFailure(Exception error) { Debug.LogException(error, this); }

        private IslandEngine Engine => engine ?? throw new InvalidOperationException("孤岛会话尚未初始化。");

        private IslandResult Record(IslandResult result)
        { LastError = result.Error; return result; }

        private void ForwardChanged(IslandSnapshot snapshot)
        {
            if (Changed == null) return;
            foreach (Action<IslandSnapshot> observer in Changed.GetInvocationList())
                try { observer(JsonUtility.FromJson<IslandSnapshot>(JsonUtility.ToJson(snapshot))); }
                catch (Exception error) { Debug.LogException(error, this); }
        }
    }
}
