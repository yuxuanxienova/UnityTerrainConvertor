using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public class OBJExporter : Editor
{
    [MenuItem("Tools/Export Selected to OBJ")]
    static void ExportSelectedToOBJ()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("No GameObject selected.");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Export OBJ", "", selected.name + ".obj", "obj");
        if (string.IsNullOrEmpty(path))
            return;

        MeshFilter mf = selected.GetComponent<MeshFilter>();
        if (mf == null)
        {
            Debug.LogError("Selected GameObject does not have a MeshFilter.");
            return;
        }

        Mesh mesh = mf.sharedMesh;
        StringBuilder sb = new StringBuilder();

        sb.Append("o ").Append(selected.name).Append("\n");
        foreach (Vector3 v in mesh.vertices)
        {
            sb.Append(string.Format("v {0} {1} {2}\n", v.x, v.y, v.z));
        }
        sb.Append("\n");
        foreach (Vector3 v in mesh.normals)
        {
            sb.Append(string.Format("vn {0} {1} {2}\n", v.x, v.y, v.z));
        }
        sb.Append("\n");
        foreach (Vector2 v in mesh.uv)
        {
            sb.Append(string.Format("vt {0} {1}\n", v.x, v.y));
        }
        sb.Append("\n");
        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n",
                mesh.triangles[i] + 1, mesh.triangles[i + 1] + 1, mesh.triangles[i + 2] + 1));
        }

        File.WriteAllText(path, sb.ToString());
        Debug.Log("Exported OBJ to " + path);
    }
}
