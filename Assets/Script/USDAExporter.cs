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

        // We will export all MeshFilters under the selected hierarchy; no single-mesh requirement

        // Ensure culture formatting with '.' for decimals
        CultureInfo cultureInfo = new CultureInfo("en-US");
        System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;

        try
        {
            using (StreamWriter sw = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                WriteUsdHeader(sw);
                // Export selected and all child MeshFilters
                ExportHierarchyMeshes(sw, selected.transform, "    ");
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

    private void ExportHierarchyMeshes(StreamWriter sw, Transform root, string indent)
    {
        foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
        {
            MeshFilter mf = tr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                continue;

            string primName = MakePrimNameFromPath(tr, root);
            WriteMeshPrim(sw, primName, mf.sharedMesh, indent, tr);
        }
    }

    private string MakePrimNameFromPath(Transform node, Transform root)
    {
        System.Collections.Generic.List<string> items = new System.Collections.Generic.List<string>();
        Transform cur = node;
        while (cur != null && cur != root.parent)
        {
            items.Add(SanitizePrimToken(cur.name));
            if (cur == root) break;
            cur = cur.parent;
        }
        items.Reverse();
        return string.Join("_", items);
    }

    private string SanitizePrimToken(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Prim";
        StringBuilder sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }
        if (sb.Length > 0 && (sb[0] >= '0' && sb[0] <= '9'))
            sb.Insert(0, '_');
        return sb.ToString();
    }

    private void WriteMeshPrim(StreamWriter sw, string primName, Mesh mesh, string indent, Transform tr)
    {
        string ind0 = indent;              // inside World
        string ind1 = ind0 + "    ";      // inside Mesh body
        string ind2 = ind1 + "    ";      // array elements

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
            Vector3 p = (tr != null) ? tr.TransformPoint(vertices[i]) : vertices[i];
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
                Vector3 n = (tr != null) ? tr.TransformDirection(normals[i]).normalized : normals[i];
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
        sw.WriteLine(ind0 + "def Mesh \"" + primName + "\" (");
        sw.WriteLine(ind1 + "prepend apiSchemas = [\"PhysicsCollisionAPI\", \"PhysicsMeshCollisionAPI\"]");
        sw.WriteLine(ind0 + ")");
        sw.WriteLine(ind0 + "{");

        // extent
        sw.WriteLine(ind1 + "uniform float3[] extent = [");
        sw.WriteLine(ind2 + "(" + FormatFloat(minExtent.x) + ", " + FormatFloat(minExtent.y) + ", " + FormatFloat(minExtent.z) + "),");
        sw.WriteLine(ind2 + "(" + FormatFloat(maxExtent.x) + ", " + FormatFloat(maxExtent.y) + ", " + FormatFloat(maxExtent.z) + ")");
        sw.WriteLine(ind1 + "]");

        // points
        sw.WriteLine(ind1 + "point3f[] points = [");
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 p = points[i];
            string suffix = (i == points.Length - 1) ? string.Empty : ",";
            sw.WriteLine(ind2 + "(" + FormatFloat(p.x) + ", " + FormatFloat(p.y) + ", " + FormatFloat(p.z) + ")" + suffix);
        }
        sw.WriteLine(ind1 + "]");

        // face counts
        sw.WriteLine(ind1 + "int[] faceVertexCounts = [");
        for (int i = 0; i < faceCounts.Length; i++)
        {
            string suffix = (i == faceCounts.Length - 1) ? string.Empty : ",";
            sw.WriteLine(ind2 + faceCounts[i] + suffix);
        }
        sw.WriteLine(ind1 + "]");

        // face indices
        sw.WriteLine(ind1 + "int[] faceVertexIndices = [");
        for (int i = 0; i < faceIndices.Length; i++)
        {
            string suffix = (i == faceIndices.Length - 1) ? string.Empty : ",";
            sw.WriteLine(ind2 + faceIndices[i] + suffix);
        }
        sw.WriteLine(ind1 + "]");

        // Physics collision attributes (enabled and mesh approximation)
        sw.WriteLine("    bool physics:collisionEnabled = 1");
        sw.WriteLine("    uniform token physics:approximation = \"triangleMesh\"");

        // normals (vertex interpolation)
        if (outNormals != null)
        {
            sw.WriteLine(ind1 + "normal3f[] normals = [");
            for (int i = 0; i < outNormals.Length; i++)
            {
                Vector3 n = outNormals[i];
                string suffix = (i == outNormals.Length - 1) ? string.Empty : ",";
                sw.WriteLine(ind2 + "(" + FormatFloat(n.x) + ", " + FormatFloat(n.y) + ", " + FormatFloat(n.z) + ")" + suffix);
            }
            sw.WriteLine(ind1 + "]");
            sw.WriteLine(ind1 + "uniform token normals:interpolation = \"vertex\"");
        }

        // UVs as primvar st (vertex interpolation)
        if (outUvs != null)
        {
            sw.WriteLine(ind1 + "texCoord2f[] primvars:st = [");
            for (int i = 0; i < outUvs.Length; i++)
            {
                Vector2 uv = outUvs[i];
                string suffix = (i == outUvs.Length - 1) ? string.Empty : ",";
                sw.WriteLine(ind2 + "(" + FormatFloat(uv.x) + ", " + FormatFloat(uv.y) + ")" + suffix);
            }
            sw.WriteLine(ind1 + "]");
            sw.WriteLine(ind1 + "uniform token primvars:st:interpolation = \"vertex\"");
        }

        // Physics collision attributes (enabled and mesh approximation)
        sw.WriteLine(ind1 + "bool physics:collisionEnabled = 1");
        sw.WriteLine(ind1 + "uniform token physics:approximation = \"none\"");
        sw.WriteLine(ind1 + "uniform token subdivisionScheme = \"none\"");
        sw.WriteLine(ind1 + "uniform bool doubleSided = 1");

        sw.WriteLine(ind0 + "}");
    }

    private string FormatFloat(float v)
    {
        return v.ToString("0.########", CultureInfo.InvariantCulture);
    }
}


