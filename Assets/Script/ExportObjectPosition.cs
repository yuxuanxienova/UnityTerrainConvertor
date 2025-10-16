using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class ExportObjectPositions : EditorWindow
{
    private bool exportAllObjects = false;
    private bool exportInROSCoordinateFrame = true;
    private bool includingSelectedObj = false;
    private string fileName = "point_set_1.txt";
    private string childTag = "way_point";  // New field to specify the tag for child objects

    // Programmatic API: export all children with given tag under a parent GameObject
    public static bool ExportWaypoints(GameObject parent, string tag, string path, bool rosCoordinate = true, bool includeParent = false)
    {
        if (parent == null) { Debug.LogError("Waypoint export failed: parent is null."); return false; }
        if (string.IsNullOrEmpty(tag)) { Debug.LogError("Waypoint export failed: tag is empty."); return false; }
        if (string.IsNullOrEmpty(path)) { Debug.LogError("Waypoint export failed: output path is empty."); return false; }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Object Positions Export");
        sb.AppendLine("=======================");

        List<GameObject> objectsToExport = new List<GameObject>();
        if (includeParent) objectsToExport.Add(parent);
        objectsToExport.AddRange(GetChildGameObjectsWithTag_Static(parent, tag));

        foreach (GameObject go in objectsToExport)
        {
            Vector3 pos = go.transform.position;
            if (rosCoordinate)
            {
                pos = ExtensionMethods.VecUnity2Ros(pos);
            }
            sb.AppendLine($"Object: {go.name}, Position: ({pos.x}, {pos.y}, {pos.z})");
        }

        try
        {
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to write waypoint file: " + ex.Message);
            return false;
        }
    }

    [MenuItem("Tools/Export Object Positions…")]
    static void Init()
    {
        ExportObjectPositions window = (ExportObjectPositions)GetWindow(typeof(ExportObjectPositions));
        window.titleContent = new GUIContent("Export Positions");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Export Object Positions With Tag", EditorStyles.boldLabel);

        EditorGUILayout.Space();
        exportAllObjects = EditorGUILayout.Toggle("Export All Objects", exportAllObjects);

        EditorGUILayout.Space();
        exportInROSCoordinateFrame = EditorGUILayout.Toggle("Export In ROS Coordinate", exportInROSCoordinateFrame);

        EditorGUILayout.Space();
        includingSelectedObj = EditorGUILayout.Toggle("Including Selected Obj", includingSelectedObj);

        EditorGUILayout.Space();
        fileName = EditorGUILayout.TextField("File Name", fileName);

        EditorGUILayout.Space();
        // New GUI field to specify the tag used for filtering child objects
        childTag = EditorGUILayout.TextField("Tag", childTag);

        EditorGUILayout.Space();
        if (GUILayout.Button("Export"))
        {
            ExportPositions();
        }
    }

    private void ExportPositions()
    {
        // Gather objects to export
        List<GameObject> objectsToExport = new List<GameObject>();
        if (exportAllObjects)
        {
            // Get all root GameObjects in the scene
            objectsToExport.AddRange(GetAllRootGameObjectsInCurrentScene());
        }
        else
        {
            // Only export currently selected objects and, if specified,
            // all child objects (recursively) that have the given tag.
            foreach (var obj in Selection.objects)
            {
                if (obj is GameObject go)
                {
                    if (includingSelectedObj) 
                    {
                        objectsToExport.Add(go);
                    }
                    

                    // If a specific child tag is provided, add all child objects with that tag.
                    if (!string.IsNullOrEmpty(childTag))
                    {
                        List<GameObject> childObjects = GetChildGameObjectsWithTag(go, childTag);
                        objectsToExport.AddRange(childObjects);
                    }
                }
            }
        }

        if (objectsToExport.Count == 0)
        {
            EditorUtility.DisplayDialog("No Objects", "No objects found to export.", "OK");
            return;
        }

        // Choose a location to save the file
        string path = EditorUtility.SaveFilePanel(
            "Save Object Positions",
            Application.dataPath,
            fileName,
            "txt"
        );

        // If user canceled or path is invalid, do nothing
        if (string.IsNullOrEmpty(path)) return;

        // Build the output string
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Object Positions Export");
        sb.AppendLine("=======================");
        foreach (GameObject go in objectsToExport)
        {
            Vector3 pos = go.transform.position;
            if (exportInROSCoordinateFrame) 
            {
                pos = ExtensionMethods.VecUnity2Ros(pos);
            }

           
            sb.AppendLine($"Object: {go.name}, Position: ({pos.x}, {pos.y}, {pos.z})");
        }

        // Write to file
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Export Complete", $"Positions exported to:\n{path}", "OK");
    }

    /// <summary>
    /// Helper method to get all root GameObjects from the currently open scene.
    /// </summary>
    private static GameObject[] GetAllRootGameObjectsInCurrentScene()
    {
        // If using Scenes in 2019.1+:
        // You can simply use: SceneManager.GetActiveScene().GetRootGameObjects()
        // or get them from the current scene:
        // Scene currentScene = SceneManager.GetActiveScene();
        // return currentScene.GetRootGameObjects();

        // For older versions of Unity or without using SceneManager explicitly:
        return Resources.FindObjectsOfTypeAll<GameObject>();
    }

    /// <summary>
    /// Recursively retrieves all child GameObjects under 'parent' that have the specified tag.
    /// </summary>
    /// <param name="parent">The parent GameObject to search under.</param>
    /// <param name="tag">The tag to filter child objects by.</param>
    /// <returns>A list of GameObjects that are children (or descendants) with the given tag.</returns>
    private List<GameObject> GetChildGameObjectsWithTag(GameObject parent, string tag)
    {
        List<GameObject> result = new List<GameObject>();

        foreach (Transform child in parent.transform)
        {
            if (child.CompareTag(tag))
            {
                result.Add(child.gameObject);
            }

            // Recursively search the child's children.
            result.AddRange(GetChildGameObjectsWithTag(child.gameObject, tag));
        }

        return result;
    }

    // Static utility for programmatic API
    private static List<GameObject> GetChildGameObjectsWithTag_Static(GameObject parent, string tag)
    {
        List<GameObject> result = new List<GameObject>();
        if (parent == null) return result;
        foreach (Transform child in parent.transform)
        {
            if (child.CompareTag(tag))
            {
                result.Add(child.gameObject);
            }
            result.AddRange(GetChildGameObjectsWithTag_Static(child.gameObject, tag));
        }
        return result;
    }
}
