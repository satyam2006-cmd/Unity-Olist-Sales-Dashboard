#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class QuakeScopeSceneSetup
{
    const string ScenePath = "Assets/Scenes/QuakeScopeDashboard.unity";

    static QuakeScopeSceneSetup()
    {
        EditorApplication.delayCall += EnsureScene;
    }

    [MenuItem("Tools/QuakeScope/Build Editable 3D Dashboard")]
    static void BuildEditableDashboard()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Sales Dashboard" || go.name == "QuakeScope Dashboard");
        if (root == null) root = new GameObject("Sales Dashboard");
        root.name = "Sales Dashboard";
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        var oldDashboard = root.GetComponent<QuakeScopeDashboard>();
        if (oldDashboard != null) Object.DestroyImmediate(oldDashboard);
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
        var root = new GameObject("QuakeScope Dashboard");
        root.AddComponent<QuakeScopeDashboard>();
        EditorSceneManager.SaveScene(scene, ScenePath);

        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("QuakeScope scene created at " + ScenePath + ". Open it and press Play to load current USGS data.");
    }
}
#endif
