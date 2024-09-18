//using UnityEngine;
//using UnityEditor;
//using System.IO;
//using System.Text;

//public class OBJExporter : Editor
//{
//    [MenuItem("Tools/Export Selected to OBJ")]
//    static void ExportSelectedToOBJ()
//    {
//        GameObject selected = Selection.activeGameObject;
//        if (selected == null)
//        {
//            Debug.LogError("No GameObject selected.");
//            return;
//        }

//        string path = EditorUtility.SaveFilePanel("Export OBJ", "", selected.name + ".obj", "obj");
//        if (string.IsNullOrEmpty(path))
//            return;

//        MeshFilter mf = selected.GetComponent<MeshFilter>();
//        if (mf == null)
//        {
//            Debug.LogError("Selected GameObject does not have a MeshFilter.");
//            return;
//        }

//        Mesh mesh = mf.sharedMesh;
//        StringBuilder sb = new StringBuilder();

//        sb.Append("o ").Append(selected.name).Append("\n");

//        // Vertices
//        for (int i = 0; i < mesh.vertexCount; i++)
//        {
//            if (EditorUtility.DisplayCancelableProgressBar("Exporting OBJ", "Processing Vertices...", (float)i / mesh.vertexCount))
//            {
//                // User canceled the operation
//                EditorUtility.ClearProgressBar();
//                return;
//            }
//            Vector3 v = mesh.vertices[i];
//            sb.Append(string.Format("v {0} {1} {2}\n", v.x, v.y, v.z));
//        }
//        sb.Append("\n");

//        // Normals
//        for (int i = 0; i < mesh.normals.Length; i++)
//        {
//            if (EditorUtility.DisplayCancelableProgressBar("Exporting OBJ", "Processing Normals...", (float)i / mesh.normals.Length))
//            {
//                // User canceled the operation
//                EditorUtility.ClearProgressBar();
//                return;
//            }
//            Vector3 n = mesh.normals[i];
//            sb.Append(string.Format("vn {0} {1} {2}\n", n.x, n.y, n.z));
//        }
//        sb.Append("\n");

//        // UVs
//        for (int i = 0; i < mesh.uv.Length; i++)
//        {
//            if (EditorUtility.DisplayCancelableProgressBar("Exporting OBJ", "Processing UVs...", (float)i / mesh.uv.Length))
//            {
//                // User canceled the operation
//                EditorUtility.ClearProgressBar();
//                return;
//            }
//            Vector2 uv = mesh.uv[i];
//            sb.Append(string.Format("vt {0} {1}\n", uv.x, uv.y));
//        }
//        sb.Append("\n");

//        // Triangles
//        int triangleCount = mesh.triangles.Length / 3;
//        for (int i = 0; i < triangleCount; i++)
//        {
//            if (EditorUtility.DisplayCancelableProgressBar("Exporting OBJ", "Processing Triangles...", (float)i / triangleCount))
//            {
//                // User canceled the operation
//                EditorUtility.ClearProgressBar();
//                return;
//            }
//            int idx0 = mesh.triangles[i * 3] + 1;
//            int idx1 = mesh.triangles[i * 3 + 1] + 1;
//            int idx2 = mesh.triangles[i * 3 + 2] + 1;
//            sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", idx0, idx1, idx2));
//        }

//        EditorUtility.ClearProgressBar();

//        File.WriteAllText(path, sb.ToString());
//        Debug.Log("Exported OBJ to " + path);
//    }
//}

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Globalization;
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

        // Set culture to "en-US" for consistent decimal formatting
        CultureInfo cultureInfo = new CultureInfo("en-US");
        System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;

        try
        {
            using (StreamWriter sw = new StreamWriter(path))
            {
                sw.WriteLine("# Exported from Unity");

                // Write object name
                sw.WriteLine("o " + selected.name);

                // Reusable StringBuilder for line construction
                StringBuilder sb = new StringBuilder(64);

                // Vertices
                Vector3[] vertices = mesh.vertices;
                int vertexCount = vertices.Length;
                for (int i = 0; i < vertexCount; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Exporting OBJ", "Processing Vertices...", (float)i / vertexCount))
                    {
                        // User canceled the operation
                        EditorUtility.ClearProgressBar();
                        return;
                    }

                    Vector3 v = vertices[i];
                    sb.Length = 0; // Clear StringBuilder
                    sb.Append("v ")
                      .Append(v.x.ToString(cultureInfo)).Append(" ")
                      .Append(v.y.ToString(cultureInfo)).Append(" ")
                      .Append(v.z.ToString(cultureInfo));
                    sw.WriteLine(sb);
                }

                // UVs
                Vector2[] uvs = mesh.uv;
                int uvCount = uvs.Length;
                for (int i = 0; i < uvCount; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Exporting OBJ", "Processing UVs...", (float)i / uvCount))
                    {
                        // User canceled the operation
                        EditorUtility.ClearProgressBar();
                        return;
                    }

                    Vector2 uv = uvs[i];
                    sb.Length = 0; // Clear StringBuilder
                    sb.Append("vt ")
                      .Append(uv.x.ToString(cultureInfo)).Append(" ")
                      .Append(uv.y.ToString(cultureInfo));
                    sw.WriteLine(sb);
                }

                // Normals
                Vector3[] normals = mesh.normals;
                int normalCount = normals.Length;
                for (int i = 0; i < normalCount; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Exporting OBJ", "Processing Normals...", (float)i / normalCount))
                    {
                        // User canceled the operation
                        EditorUtility.ClearProgressBar();
                        return;
                    }

                    Vector3 n = normals[i];
                    sb.Length = 0; // Clear StringBuilder
                    sb.Append("vn ")
                      .Append(n.x.ToString(cultureInfo)).Append(" ")
                      .Append(n.y.ToString(cultureInfo)).Append(" ")
                      .Append(n.z.ToString(cultureInfo));
                    sw.WriteLine(sb);
                }

                // Triangles (Faces)
                int[] triangles = mesh.triangles;
                int triangleCount = triangles.Length / 3;
                for (int i = 0; i < triangleCount; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Exporting OBJ", "Processing Triangles...", (float)i / triangleCount))
                    {
                        // User canceled the operation
                        EditorUtility.ClearProgressBar();
                        return;
                    }

                    int idx0 = triangles[i * 3] + 1;
                    int idx1 = triangles[i * 3 + 1] + 1;
                    int idx2 = triangles[i * 3 + 2] + 1;

                    sb.Length = 0; // Clear StringBuilder
                    sb.Append("f ")
                      .Append(idx0).Append("/").Append(idx0).Append("/").Append(idx0).Append(" ")
                      .Append(idx1).Append("/").Append(idx1).Append("/").Append(idx1).Append(" ")
                      .Append(idx2).Append("/").Append(idx2).Append("/").Append(idx2);
                    sw.WriteLine(sb);
                }
            }

            Debug.Log("Exported OBJ to " + path);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error exporting OBJ: " + ex.Message);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
}


