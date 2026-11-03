using UnityEditor;
using UnityEditor.SceneManagement;

namespace CardKKK.Tabletop.Editor
{
    public static class CardTableMenu
    {
        [MenuItem("CardKKK/Open Card Table Demo")]
        public static void OpenDemo()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/CardTable/Scenes/CardTableDemo.unity");
        }

        [MenuItem("CardKKK/Select Art Theme")]
        public static void SelectTheme()
        {
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath("Assets/CardTable/Art/CardTableTheme.asset");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }
}
