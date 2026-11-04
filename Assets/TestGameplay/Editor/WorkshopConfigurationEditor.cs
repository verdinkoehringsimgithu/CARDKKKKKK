using UnityEditor;
using UnityEngine;

namespace CardKKK.TestGameplay.Unity.Editor
{
    [CustomEditor(typeof(WorkshopConfiguration))]
    public sealed class WorkshopConfigurationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("检查测试玩法配置"))
            {
                var configuration = (WorkshopConfiguration)target;
                if (configuration.Validate(out string error))
                    EditorUtility.DisplayDialog("测试玩法", "配置检查完成。", "确定");
                else EditorUtility.DisplayDialog("测试玩法", error, "确定");
            }
        }

        [MenuItem("测试玩法/选择玩法配置")]
        private static void SelectConfiguration()
        {
            var configuration = AssetDatabase.LoadAssetAtPath<WorkshopConfiguration>(
                "Assets/TestGameplay/Configuration/测试工坊配置.asset");
            if (configuration == null)
            {
                Debug.LogError("找不到测试工坊配置资产。");
                return;
            }
            Selection.activeObject = configuration;
            EditorGUIUtility.PingObject(configuration);
        }
    }
}
