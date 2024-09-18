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
        CombineInstance[] combine = new CombineInstance[meshFilters.Length];

        for (int i = 0; i < meshFilters.Length; i++)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
            meshFilters[i].gameObject.SetActive(false);
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combine);

        GameObject combinedObject = new GameObject("CombinedMesh");
        combinedObject.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
        combinedObject.AddComponent<MeshRenderer>().sharedMaterial = meshFilters[0].GetComponent<MeshRenderer>().sharedMaterial;
    }
}
