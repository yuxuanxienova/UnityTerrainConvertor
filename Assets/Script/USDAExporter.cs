// Export selected Mesh to .usda (USD ASCII) - by yuxuan

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Globalization;
using System.Text;

public class USDAExporter : EditorWindow
{
    private bool exportInROSCoordinateFrame = false;

    [MenuItem("Tools/Export Selected to USDA")]
    static void Init()
    {
        USDAExporter window = (USDAExporter)GetWindow(typeof(USDAExporter));
        window.titleContent = new GUIContent("Export Selected Mesh To .usda file");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Export Selected Mesh To .usda file", EditorStyles.boldLabel);

        EditorGUILayout.Space();
        exportInROSCoordinateFrame = EditorGUILayout.Toggle("Export In ROS Coordinate", exportInROSCoordinateFrame);

        EditorGUILayout.Space();
        if (GUILayout.Button("Export"))
        {
            ExportSelectedToUSDA();
        }
    }

    private void ExportSelectedToUSDA()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("No GameObject selected.");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Export USDA", "", selected.name + ".usda", "usda");
        if (string.IsNullOrEmpty(path))
            return;

        MeshFilter mf = selected.GetComponent<MeshFilter>();
        if (mf == null)
        {
            Debug.LogError("Selected GameObject does not have a MeshFilter.");
            return;
        }

        Mesh mesh = mf.sharedMesh;
        if (mesh == null)
        {
            Debug.LogError("MeshFilter has no sharedMesh.");
            return;
        }

        // Ensure culture formatting with '.' for decimals
        CultureInfo cultureInfo = new CultureInfo("en-US");
        System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;

        try
        {
            using (StreamWriter sw = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                WriteUsdHeader(sw);
                WriteMeshPrim(sw, selected.name, mesh);
                WriteWorldFooter(sw);
            }

            Debug.Log("Exported USDA to " + path);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error exporting USDA: " + ex.Message);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void WriteUsdHeader(StreamWriter sw)
    {
        sw.WriteLine("#usda 1.0");
        sw.WriteLine("(");
        sw.WriteLine("    defaultPrim = \"World\"");
        sw.WriteLine(")");
        sw.WriteLine();
        sw.WriteLine("def Xform \"World\" {");
        sw.WriteLine();
    }

    private void WriteWorldFooter(StreamWriter sw)
    {
        sw.WriteLine("}");
    }

    private void WriteMeshPrim(StreamWriter sw, string primName, Mesh mesh)
    {
        // Collect mesh data
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = mesh.uv;
        int[] triangles = mesh.triangles; // Unity triangles are 0-based, winding is clockwise in left-handed? We'll manage by flipping if ROS frame is used

        int vertexCount = vertices.Length;
        int triCount = triangles.Length / 3;

        if (vertexCount == 0 || triCount == 0)
        {
            Debug.LogWarning("Mesh has no geometry to export.");
            return;
        }

        // Prepare transformed arrays
        Vector3 minExtent = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maxExtent = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        // points
        Vector3[] points = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Exporting USDA", "Processing Points...", (float)i / vertexCount))
            {
                EditorUtility.ClearProgressBar();
                return;
            }
            Vector3 p = vertices[i];
            if (exportInROSCoordinateFrame)
            {
                p = ExtensionMethods.VecUnity2Ros(p);
            }
            points[i] = p;
            if (p.x < minExtent.x) minExtent.x = p.x;
            if (p.y < minExtent.y) minExtent.y = p.y;
            if (p.z < minExtent.z) minExtent.z = p.z;
            if (p.x > maxExtent.x) maxExtent.x = p.x;
            if (p.y > maxExtent.y) maxExtent.y = p.y;
            if (p.z > maxExtent.z) maxExtent.z = p.z;
        }

        // normals
        Vector3[] outNormals = null;
        if (normals != null && normals.Length == vertexCount)
        {
            outNormals = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Exporting USDA", "Processing Normals...", (float)i / vertexCount))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }
                Vector3 n = normals[i];
                if (exportInROSCoordinateFrame)
                {
                    n = ExtensionMethods.NormUnity2Ros(n);
                }
                outNormals[i] = n;
            }
        }

        // uvs (st)
        Vector2[] outUvs = null;
        if (uvs != null && uvs.Length == vertexCount)
        {
            outUvs = new Vector2[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Exporting USDA", "Processing UVs...", (float)i / vertexCount))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }
                Vector2 uv = uvs[i];
                if (exportInROSCoordinateFrame)
                {
                    uv = ExtensionMethods.UVUnity2Ros(uv);
                }
                outUvs[i] = uv;
            }
        }

        // faceVertexCounts and faceVertexIndices
        int faceCount = triCount; // each triangle is a face with 3 verts
        int[] faceCounts = new int[faceCount];
        int[] faceIndices = new int[triangles.Length];
        for (int i = 0; i < faceCount; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Exporting USDA", "Processing Faces...", (float)i / faceCount))
            {
                EditorUtility.ClearProgressBar();
                return;
            }
            faceCounts[i] = 3;
            // flip winding if exporting to ROS to preserve front-face
            int i0 = triangles[i * 3 + 0];
            int i1 = triangles[i * 3 + 1];
            int i2 = triangles[i * 3 + 2];
            if (exportInROSCoordinateFrame)
            {
                int temp = i1; i1 = i2; i2 = temp;
            }
            faceIndices[i * 3 + 0] = i0;
            faceIndices[i * 3 + 1] = i1;
            faceIndices[i * 3 + 2] = i2;
        }

        // Write USD Mesh prim with API schemas metadata
        sw.WriteLine("def Mesh \"" + primName + "\" (");
        sw.WriteLine("    prepend apiSchemas = [\"UsdPhysicsCollisionAPI\", \"UsdPhysicsMeshCollisionAPI\"]");
        sw.WriteLine(")");
        sw.WriteLine("{");

        // extent
        sw.WriteLine("    uniform float3[] extent = [");
        sw.WriteLine("        (" + FormatFloat(minExtent.x) + ", " + FormatFloat(minExtent.y) + ", " + FormatFloat(minExtent.z) + "),");
        sw.WriteLine("        (" + FormatFloat(maxExtent.x) + ", " + FormatFloat(maxExtent.y) + ", " + FormatFloat(maxExtent.z) + ")");
        sw.WriteLine("    ]");

        // points
        sw.WriteLine("    point3f[] points = [");
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 p = points[i];
            string suffix = (i == points.Length - 1) ? string.Empty : ",";
            sw.WriteLine("        (" + FormatFloat(p.x) + ", " + FormatFloat(p.y) + ", " + FormatFloat(p.z) + ")" + suffix);
        }
        sw.WriteLine("    ]");

        // face counts
        sw.WriteLine("    int[] faceVertexCounts = [");
        for (int i = 0; i < faceCounts.Length; i++)
        {
            string suffix = (i == faceCounts.Length - 1) ? string.Empty : ",";
            sw.WriteLine("        " + faceCounts[i] + suffix);
        }
        sw.WriteLine("    ]");

        // face indices
        sw.WriteLine("    int[] faceVertexIndices = [");
        for (int i = 0; i < faceIndices.Length; i++)
        {
            string suffix = (i == faceIndices.Length - 1) ? string.Empty : ",";
            sw.WriteLine("        " + faceIndices[i] + suffix);
        }
        sw.WriteLine("    ]");

        // Physics collision attributes (enabled and mesh approximation)
        sw.WriteLine("    bool physics:collisionEnabled = 1");
        sw.WriteLine("    token physics:approximation = \"triangleMesh\"");

        // normals (vertex interpolation)
        if (outNormals != null)
        {
            sw.WriteLine("    normal3f[] normals = [");
            for (int i = 0; i < outNormals.Length; i++)
            {
                Vector3 n = outNormals[i];
                string suffix = (i == outNormals.Length - 1) ? string.Empty : ",";
                sw.WriteLine("        (" + FormatFloat(n.x) + ", " + FormatFloat(n.y) + ", " + FormatFloat(n.z) + ")" + suffix);
            }
            sw.WriteLine("    ]");
            sw.WriteLine("    uniform token normals:interpolation = \"vertex\"");
        }

        // UVs as primvar st (vertex interpolation)
        if (outUvs != null)
        {
            sw.WriteLine("    texCoord2f[] primvars:st = [");
            for (int i = 0; i < outUvs.Length; i++)
            {
                Vector2 uv = outUvs[i];
                string suffix = (i == outUvs.Length - 1) ? string.Empty : ",";
                sw.WriteLine("        (" + FormatFloat(uv.x) + ", " + FormatFloat(uv.y) + ")" + suffix);
            }
            sw.WriteLine("    ]");
            sw.WriteLine("    uniform token primvars:st:interpolation = \"vertex\"");
        }

        sw.WriteLine("}");
    }

    private string FormatFloat(float v)
    {
        return v.ToString("0.########", CultureInfo.InvariantCulture);
    }
}


