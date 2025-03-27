//Check for meshFilters and Terrains and combine all mesh- by yuxuan
using UnityEngine;
using UnityEditor;

public class MeshCombiner : Editor
{
    [MenuItem("Tools/Combine Meshes")]
    static void CombineSelectedMeshes()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("No GameObject selected.");
            return;
        }

        MeshFilter[] meshFilters = selected.GetComponentsInChildren<MeshFilter>();
        Terrain[] terrains = selected.GetComponentsInChildren<Terrain>();
        CombineInstance[] combine = new CombineInstance[meshFilters.Length + terrains.Length];

        for (int i = 0; i < meshFilters.Length; i++)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
            //meshFilters[i].gameObject.SetActive(false);
        }

        for (int i = meshFilters.Length; i < meshFilters.Length + terrains.Length; i++)
        {
            combine[i].mesh = ExtensionMethods.TerrainToMesh(terrains[i - meshFilters.Length]);
            combine[i].transform = terrains[i - meshFilters.Length].gameObject.transform.localToWorldMatrix;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combinedMesh.CombineMeshes(combine);

        GameObject combinedObject = new GameObject("CombinedMesh");
        combinedObject.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
        if (meshFilters.Length > 0)
        {
            combinedObject.AddComponent<MeshRenderer>().sharedMaterial = meshFilters[0].GetComponent<MeshRenderer>().sharedMaterial;
        }
        else
        {
            combinedObject.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard"));
        }
    }
}
