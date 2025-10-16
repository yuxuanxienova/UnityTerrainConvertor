using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapExporter : MonoBehaviour
{
    public string mapName;

    public GameObject scene;
    public List<GameObject> point_sets;
    //folder architecture:
    //mapName
    //├── point_sets
    //│   ├── point_set_1.txt
    //│   ├── point_set_2.txt
    //│   └── ...
    //└── scene
    //    └── scene.usda
    //    └──...other files related to usda export

    public void ExportMap()
    {
        if (string.IsNullOrEmpty(mapName)) { Debug.LogError("MapExporter: mapName is empty."); return; }
        if (scene == null) { Debug.LogError("MapExporter: scene GameObject is null."); return; }

        string baseDir = Application.dataPath;
        string outputRoot = System.IO.Path.Combine(baseDir, "OUTPUT", mapName);
        string pointDir = System.IO.Path.Combine(outputRoot, "point_sets");
        string sceneDir = System.IO.Path.Combine(outputRoot, "scene");

        try
        {
            if (!System.IO.Directory.Exists(outputRoot)) System.IO.Directory.CreateDirectory(outputRoot);
            if (!System.IO.Directory.Exists(pointDir)) System.IO.Directory.CreateDirectory(pointDir);
            if (!System.IO.Directory.Exists(sceneDir)) System.IO.Directory.CreateDirectory(sceneDir);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("MapExporter: failed to create output directories: " + ex.Message);
            return;
        }

        // 1) Export scene USDA using USDAExporter defaults
        string sceneName = string.IsNullOrEmpty(scene.name) ? "scene" : scene.name;
        string sceneUsdPath = System.IO.Path.Combine(sceneDir, sceneName + ".usda");
        bool sceneOk = USDAExporter.ExportGameObjectToUSDA(scene, sceneUsdPath, true, true);
        if (!sceneOk)
        {
            Debug.LogError("MapExporter: scene export failed.");
            return;
        }

        // 2) Export waypoint sets: each GameObject in point_sets is a set; export its children with tag "way_point"
        if (point_sets != null)
        {
            foreach (var setGo in point_sets)
            {
                if (setGo == null) continue;
                string fileSafe = string.IsNullOrEmpty(setGo.name) ? "point_set" : setGo.name;
                string txtPath = System.IO.Path.Combine(pointDir, fileSafe + ".txt");
                bool ok = ExportObjectPositions.ExportWaypoints(setGo, "way_point", txtPath, true, false);
                if (!ok)
                {
                    Debug.LogWarning("MapExporter: failed exporting waypoint set: " + setGo.name);
                }
            }
        }

        Debug.Log("MapExporter: Export completed at " + outputRoot);
    }


    [UnityEditor.MenuItem("Tools/Export Map (from selected MapExporter)")]
    private static void ExportFromMenu()
    {
        var selected = UnityEditor.Selection.activeGameObject;
        if (selected == null) { Debug.LogError("Select a GameObject with MapExporter component."); return; }
        var comp = selected.GetComponent<MapExporter>();
        if (comp == null) { Debug.LogError("Selected object does not have MapExporter component."); return; }
        comp.ExportMap();
    }



}