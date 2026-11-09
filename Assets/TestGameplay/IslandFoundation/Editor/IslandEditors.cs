using System;
using System.Collections.Generic;
using CardKKK.Island.Core;
using CardKKK.Island.Unity;
using UnityEditor;
using UnityEngine;

namespace CardKKK.Island.Editor
{
    [CustomEditor(typeof(IslandConfiguration))]
    public sealed class IslandConfigurationEditor : UnityEditor.Editor
    {
        [MenuItem("孤岛底层/在当前场景创建会话（使用选中配置）")]
        private static void CreateSession()
        {
            var configuration = Selection.activeObject as IslandConfiguration;
            if (configuration == null)
            { EditorUtility.DisplayDialog("创建孤岛会话", "请先选中孤岛玩法配置资产。", "确定"); return; }
            try { configuration.CreateEngine(); }
            catch (Exception error)
            { EditorUtility.DisplayDialog("配置校验失败", error.Message, "确定"); return; }
            var host = new GameObject("孤岛底层会话");
            Undo.RegisterCreatedObjectUndo(host, "创建孤岛会话");
            var session = Undo.AddComponent<IslandSession>(host);
            var serialized = new SerializedObject(session);
            serialized.FindProperty("configuration").objectReferenceValue = configuration;
            serialized.ApplyModifiedProperties();
            Selection.activeGameObject = host;
        }

        private string message;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("校验定义与开局"))
            {
                try { ((IslandConfiguration)target).CreateEngine(); message = "定义与开局校验通过。"; }
                catch (Exception error) { message = error.Message; }
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        }
    }

    [CustomEditor(typeof(IslandSession))]
    public sealed class IslandSessionEditor : UnityEditor.Editor
    {
        private int recipeIndex;
        private string regionId = "";
        private int minutes = 1;
        private string snapshotJson = "";
        private readonly Dictionary<string, string> bindings = new Dictionary<string, string>();
        private Vector2 scroll;
        private int moveIndex;
        private Vector2 position;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var session = (IslandSession)target;
            EditorGUILayout.HelpBox("底层调试入口。配置在停止运行时编辑，运行时命令通过会话提交。", MessageType.Info);
            if (!Application.isPlaying) return;
            if (GUILayout.Button("按配置重新初始化")) session.Initialize();
            if (!string.IsNullOrEmpty(session.LastError)) EditorGUILayout.HelpBox(session.LastError, MessageType.Warning);
            if (!session.IsReady) return;
            var state = session.Snapshot;
            EditorGUILayout.LabelField("游戏分钟 / 状态版本", state.Minute + " / " + state.Revision);
            minutes = EditorGUILayout.IntField("推进分钟数", minutes);
            if (GUILayout.Button("推进时间") && minutes > 0)
            {
                try { session.AdvanceTo(checked(state.Minute + minutes)); }
                catch (OverflowException) { Debug.LogWarning("时间超出范围。", session); }
            }
            regionId = EditorGUILayout.TextField("区域实例 ID（空为桌面）", regionId);
            var definitions = session.Configuration.Definition.Recipes;
            if (definitions.Count > 0)
            {
                var names = definitions.ConvertAll(recipe => recipe.Name + " [" + recipe.Id + "]").ToArray();
                recipeIndex = EditorGUILayout.Popup("配方", Math.Min(recipeIndex, names.Length - 1), names);
                var recipe = definitions[recipeIndex];
                foreach (var slot in recipe.Ingredients)
                {
                    bindings.TryGetValue(slot.SlotId, out string id);
                    bindings[slot.SlotId] = EditorGUILayout.TextField("槽位 " + slot.SlotId, id ?? "");
                }
                if (GUILayout.Button("提交配方"))
                {
                    var selected = new List<SlotBinding>();
                    foreach (var slot in recipe.Ingredients)
                        selected.Add(new SlotBinding { SlotId = slot.SlotId, InstanceId = bindings[slot.SlotId] });
                    session.StartRecipe(recipe.Id, regionId, selected);
                }
            }
            if (state.Cards.Count > 0)
            {
                var names = state.Cards.ConvertAll(card => card.DefinitionId + " [" + card.Id + "]").ToArray();
                moveIndex = EditorGUILayout.Popup("移动对象", Math.Min(moveIndex, names.Length - 1), names);
                position = EditorGUILayout.Vector2Field("位置", position);
                if (GUILayout.Button("移动到指定区域与位置")) session.Move(state.Cards[moveIndex].Id, regionId, position);
            }
            if (GUILayout.Button("导出快照到下方")) snapshotJson = session.ExportJson();
            snapshotJson = EditorGUILayout.TextArea(snapshotJson, GUILayout.Height(70));
            if (GUILayout.Button("从上方快照恢复")) session.RestoreJson(snapshotJson);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(180));
            EditorGUILayout.SelectableLabel(session.ExportJson(), GUILayout.Height(Math.Max(180, state.Cards.Count * 230)));
            EditorGUILayout.EndScrollView();
        }
    }
}
