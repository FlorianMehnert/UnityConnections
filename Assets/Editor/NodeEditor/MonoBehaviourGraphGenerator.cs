using UnityEditor;
using UnityEngine;
using System.Linq;

public class SceneComponentGraphGenerator : EditorWindow
{
    private bool includeInactiveObjects = false;

    [MenuItem("Tools/Generate Scene Component Graph")]
    public static void ShowWindow()
    {
        GetWindow<SceneComponentGraphGenerator>("Scene Component Graph");
    }

    private void OnGUI()
    {
        includeInactiveObjects = EditorGUILayout.Toggle("Include Inactive Objects", includeInactiveObjects);

        if (GUILayout.Button("Generate Graph"))
        {
            GameObject[] sceneObjects = GetSceneObjects();
            SceneComponentGraph graph = CreateInstance<SceneComponentGraph>();

            // Start async graph generation
            graph.GenerateGraphAsync(sceneObjects);

            string path = EditorUtility.SaveFilePanelInProject("Save Scene Component Graph", "SceneComponentGraph", "asset", "Save graph asset");
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(graph, path);
                AssetDatabase.SaveAssets();
            }
        }
    }

    private GameObject[] GetSceneObjects()
    {
        return includeInactiveObjects ?
            Resources.FindObjectsOfTypeAll<GameObject>().Where(go => go.scene.isLoaded).ToArray() :
            GameObject.FindObjectsOfType<GameObject>();
    }
}
