using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardKKK.Tabletop.Editor
{
    [InitializeOnLoad]
    public static class CardTableMenu
    {
        private const string ScenePath = "Assets/CardTable/Scenes/CardTableDemo.unity";
        private static string CurrentScenePreference => "CardKKK.PlayCurrentScene." + Application.dataPath;

        static CardTableMenu()
        {
            EditorApplication.delayCall += ConfigurePlayEntry;
        }

        private static void ConfigurePlayEntry()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(CurrentScenePreference, false))
                return;
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (scene != null)
                EditorSceneManager.playModeStartScene = scene;
        }

        [MenuItem("卡牌桌面/打开卡牌桌面")]
        public static void OpenDemo()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
                EditorApplication.ExecuteMenuItem("Window/General/Game");
            }
        }

        [MenuItem("卡牌桌面/选择美术主题")]
        public static void SelectTheme()
        {
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath("Assets/CardTable/Art/CardTableTheme.asset");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("卡牌桌面/运行时进入卡牌桌面")]
        public static void PlayDemoByDefault()
        {
            EditorPrefs.SetBool(CurrentScenePreference, false);
            ConfigurePlayEntry();
        }

        [MenuItem("卡牌桌面/运行时进入卡牌桌面", true)]
        private static bool ValidatePlayDemo()
        {
            Menu.SetChecked("卡牌桌面/运行时进入卡牌桌面", !EditorPrefs.GetBool(CurrentScenePreference, false));
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem("卡牌桌面/运行时进入当前场景")]
        public static void PlayCurrentScene()
        {
            EditorPrefs.SetBool(CurrentScenePreference, true);
            EditorSceneManager.playModeStartScene = null;
        }

        [MenuItem("卡牌桌面/运行时进入当前场景", true)]
        private static bool ValidatePlayCurrentScene()
        {
            Menu.SetChecked("卡牌桌面/运行时进入当前场景", EditorPrefs.GetBool(CurrentScenePreference, false));
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
