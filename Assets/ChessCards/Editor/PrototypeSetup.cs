#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ChessCards
{
    static class ChessCardsProjectMenu
    {
        const string Project = "Assets/ChessCards/";

        [MenuItem("Chess Cards/Open Courtyard Scene")]
        static void OpenCourtyard() { Open("Main"); }

        [MenuItem("Chess Cards/Open Match Scene")]
        static void OpenMatch() { Open("Match"); }

        [MenuItem("Chess Cards/Select Game Balance")]
        static void SelectBalance() { Select("Config/GameBalance.asset"); }

        [MenuItem("Chess Cards/Select Royal Theme")]
        static void SelectTheme() { Select("Config/RoyalTheme.asset"); }

        static void Open(string name)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(Project + "Scenes/" + name + ".unity");
        }

        static void Select(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(Project + path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
#endif
