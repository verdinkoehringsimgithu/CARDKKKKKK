using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardKKK.Tabletop.Editor
{
    [InitializeOnLoad]
    public static class CardTableMenu
    {
        private const string ScenePath = "Assets/Archives/Scenes/CardTableDemo.unity";
        private const string IslandPath = "Assets/IslandSurvival/Scenes/IslandSurvival.unity";
        private const string IslandMode = "孤岛生存/运行入口/默认进入孤岛";
        private const string CurrentMode = "孤岛生存/运行入口/进入当前场景";
        private const string DemoMode = "孤岛生存/历史演示/自由牌桌/设为运行入口";
        private static string EntryPreference => "CardKKK.PlayEntry.v2." + Application.dataPath;

        static CardTableMenu() { EditorApplication.delayCall += ConfigurePlayEntry; }

        private static void ConfigurePlayEntry()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int mode = EditorPrefs.GetInt(EntryPreference, 0);
            EditorSceneManager.playModeStartScene = mode == 1 ? null :
                AssetDatabase.LoadAssetAtPath<SceneAsset>(mode == 2 ? ScenePath : IslandPath);
        }

        private static void Open(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        [MenuItem("孤岛生存/打开孤岛场景", priority = 0)]
        public static void OpenIsland()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Open(IslandPath);
            EditorPrefs.SetInt(EntryPreference, 0);
            ConfigurePlayEntry();
        }

        [MenuItem("孤岛生存/历史演示/自由牌桌/打开场景")]
        public static void OpenDemo() { Open(ScenePath); }

        [MenuItem("孤岛生存/历史演示/自由牌桌/选择美术主题")]
        public static void SelectTheme()
        {
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath("Assets/CardTable/Art/CardTableTheme.asset");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem(IslandMode)]
        public static void PlayIslandByDefault() { SetMode(0); }
        [MenuItem(DemoMode)]
        public static void PlayDemoByDefault() { SetMode(2); }
        [MenuItem(CurrentMode)]
        public static void PlayCurrentScene() { SetMode(1); }

        private static void SetMode(int mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorPrefs.SetInt(EntryPreference, mode);
            ConfigurePlayEntry();
        }
        private static bool ValidateMode(string menu, int mode)
        {
            Menu.SetChecked(menu, EditorPrefs.GetInt(EntryPreference, 0) == mode);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
        [MenuItem(IslandMode, true)]
        private static bool ValidateIsland() => ValidateMode(IslandMode, 0);
        [MenuItem(DemoMode, true)]
        private static bool ValidatePlayDemo() => ValidateMode(DemoMode, 2);
        [MenuItem(CurrentMode, true)]
        private static bool ValidatePlayCurrentScene() => ValidateMode(CurrentMode, 1);
    }
}
