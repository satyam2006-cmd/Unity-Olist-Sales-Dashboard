#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class SalescopeSceneSetup
{
    const string ScenePath = "Assets/Scenes/SalescopeDashboard.unity";

    static SalescopeSceneSetup()
    {
        EditorApplication.delayCall += EnsureScene;
    }

    [MenuItem("Tools/Salescope/Build Editable 3D Dashboard")]
    static void BuildEditableDashboard()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Salescope" || go.name == "Sales Dashboard");
        if (root == null) root = new GameObject("Salescope");
        root.name = "Salescope";
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        var dashboard = root.GetComponent<ProjectPulseDashboard>() ?? root.AddComponent<ProjectPulseDashboard>();
        dashboard.BuildInEditor();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log("Built and saved editable 3D sales dashboard hierarchy to " + ScenePath);
    }

    static void EnsureScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || System.IO.File.Exists(ScenePath)) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Salescope");
        var dashboard = root.AddComponent<ProjectPulseDashboard>();
        dashboard.BuildInEditor();
        EditorSceneManager.SaveScene(scene, ScenePath);

        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("Salescope scene created at " + ScenePath + ". Open it and press Play to load the sales dashboard.");
    }
}
#endif
