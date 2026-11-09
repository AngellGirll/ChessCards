#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChessCards;

public static class PrototypeSetup
{
    [MenuItem("Chess Cards/Create Prototype Scene")]
    public static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/ChessCards/Prototype.unity");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Chess Cards Prototype"); root.AddComponent<ChessCardsApp>();
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.09f, .12f, .17f); camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);
        EditorSceneManager.SaveScene(scene, path);
        // Keep existing scenes in the project. The new prototype becomes the entry scene.
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        scenes.Add(new EditorBuildSettingsScene(path, true));
        foreach (var existing in EditorBuildSettings.scenes) if (existing.path != path) scenes.Add(existing);
        EditorBuildSettings.scenes = scenes.ToArray();
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        Selection.activeGameObject = root;
        Debug.Log("Chess Cards: сцена готова. Нажмите Play и откройте вкладку Game.");
    }
}
#endif
