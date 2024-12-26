using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class ExportObjectPositions : EditorWindow
{
    private bool exportAllObjects = false;
    private bool exportInROSCoordinateFrame = false;
    private string fileName = "ObjectPositions.txt";

    [MenuItem("Tools/Export Object Positions…")]
    static void Init()
    {
        ExportObjectPositions window = (ExportObjectPositions)GetWindow(typeof(ExportObjectPositions));
        window.titleContent = new GUIContent("Export Positions");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Export Object Positions", EditorStyles.boldLabel);

        EditorGUILayout.Space();
        exportAllObjects = EditorGUILayout.Toggle("Export All Objects", exportAllObjects);

        EditorGUILayout.Space();
        exportInROSCoordinateFrame = EditorGUILayout.Toggle("Export In ROS Coordinate", exportInROSCoordinateFrame);

        EditorGUILayout.Space();
        fileName = EditorGUILayout.TextField("File Name", fileName);

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
            // Only export currently selected objects
            foreach (var obj in Selection.objects)
            {
                if (obj is GameObject go)
                {
                    objectsToExport.Add(go);
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
}
