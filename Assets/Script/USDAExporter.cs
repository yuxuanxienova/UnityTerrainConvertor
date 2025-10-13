// Export selected Mesh to .usda (USD ASCII) - by yuxuan

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Globalization;
using System.Text;

public class USDAExporter : EditorWindow
{
    private bool exportInROSCoordinateFrame = true;
    private bool exportMaterialsSeparately = true;
    private string lastExportDir = null;

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
        exportMaterialsSeparately = EditorGUILayout.Toggle("Export Materials Separately", exportMaterialsSeparately);

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
                lastExportDir = Path.GetDirectoryName(path);
                var nodes = new System.Collections.Generic.List<(Transform tr, Mesh mesh, Renderer renderer)>();
                var matToName = new System.Collections.Generic.Dictionary<Material, string>();
                CollectHierarchyMeshesAndMaterials(selected.transform, nodes, matToName);

                string materialsPath = null;
                if (matToName.Count > 0 && exportMaterialsSeparately)
                {
                    string dir = lastExportDir;
                    string baseName = Path.GetFileNameWithoutExtension(path);
                    materialsPath = Path.Combine(dir, baseName + "_Looks.usda");
                    WriteMaterialsFile(materialsPath, matToName);
                }

                WriteUsdHeader(sw);

                // Looks scope (inline or reference)
                if (matToName.Count > 0)
                {
                    if (exportMaterialsSeparately)
                    {
                        string ind0 = "    ";
                        sw.WriteLine(ind0 + "def Scope \"Looks\" (");
                        sw.WriteLine(ind0 + "    references = @" + Path.GetFileName(materialsPath) + "@</Looks>");
                        sw.WriteLine(ind0 + ")");
                        sw.WriteLine(ind0 + "{");
                        sw.WriteLine(ind0 + "}");
                        sw.WriteLine();
                    }
                    else
                    {
                        WriteMaterialsInline(sw, matToName, "    ");
                    }
                }

                // Meshes with bindings
                foreach (var node in nodes)
                {
                    string primName = MakePrimNameFromPath(node.tr, selected.transform);
                    string binding = null;
                    if (node.renderer != null)
                    {
                        var um = node.renderer.sharedMaterial;
                        if (um != null && matToName.TryGetValue(um, out var matName))
                            binding = "/World/Looks/" + matName;
                    }
                    WriteMeshPrim(sw, primName, node.mesh, "    ", node.tr, binding);
                }

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

    private void ExportHierarchyMeshes(StreamWriter sw, Transform root, string indent) {}

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

    private void WriteMeshPrim(StreamWriter sw, string primName, Mesh mesh, string indent, Transform tr, string materialBindingPath)
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

        // doubleSided first (match fixed file ordering)
        sw.WriteLine(ind1 + "uniform bool doubleSided = 1");

        // extent single-line
        sw.WriteLine(ind1 + "uniform float3[] extent = [(" +
            FormatFloat(minExtent.x) + ", " + FormatFloat(minExtent.y) + ", " + FormatFloat(minExtent.z) + "), (" +
            FormatFloat(maxExtent.x) + ", " + FormatFloat(maxExtent.y) + ", " + FormatFloat(maxExtent.z) + ")] ");

        // face counts single-line
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(ind1).Append("int[] faceVertexCounts = [");
            for (int i = 0; i < faceCounts.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(faceCounts[i]);
            }
            sb.Append("]");
            sw.WriteLine(sb.ToString());
        }

        // face indices single-line
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(ind1).Append("int[] faceVertexIndices = [");
            for (int i = 0; i < faceIndices.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(faceIndices[i]);
            }
            sb.Append("]");
            sw.WriteLine(sb.ToString());
        }

        // material binding (visual) before normals
        if (!string.IsNullOrEmpty(materialBindingPath))
        {
            sw.WriteLine(ind1 + "rel material:binding = <" + materialBindingPath + ">");
        }

        // normals single-line (vertex interpolation)
        if (outNormals != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(ind1).Append("normal3f[] normals = [");
            for (int i = 0; i < outNormals.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                Vector3 n = outNormals[i];
                sb.Append("(").Append(FormatFloat(n.x)).Append(", ").Append(FormatFloat(n.y)).Append(", ").Append(FormatFloat(n.z)).Append(")");
            }
            sb.Append("]");
            sw.WriteLine(sb.ToString());
            sw.WriteLine(ind1 + "uniform token normals:interpolation = \"vertex\"");
        }

        // physics flags
        sw.WriteLine(ind1 + "uniform token physics:approximation = \"none\"");
        sw.WriteLine(ind1 + "bool physics:collisionEnabled = 1");

        // points single-line
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(ind1).Append("point3f[] points = [");
            for (int i = 0; i < points.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                Vector3 p = points[i];
                sb.Append("(").Append(FormatFloat(p.x)).Append(", ").Append(FormatFloat(p.y)).Append(", ").Append(FormatFloat(p.z)).Append(")");
            }
            sb.Append("]");
            sw.WriteLine(sb.ToString());
        }

        // UVs as primvar st (faceVarying) single-line
        if (outUvs != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(ind1).Append("texCoord2f[] primvars:st = [");
            for (int i = 0; i < faceIndices.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                Vector2 uv = outUvs[faceIndices[i]];
                sb.Append("(").Append(FormatFloat(uv.x)).Append(", ").Append(FormatFloat(uv.y)).Append(")");
            }
            sb.Append("] (");
            sb.Append("\n").Append(ind2).Append("interpolation = \"faceVarying\"");
            sb.Append("\n").Append(ind1).Append(")");
            sw.WriteLine(sb.ToString());
            sw.WriteLine(ind1 + "uniform token primvars:st:interpolation = \"faceVarying\"");
        }

        // no subdivision
        sw.WriteLine(ind1 + "uniform token subdivisionScheme = \"none\"");

        sw.WriteLine(ind0 + "}");
    }

    private string FormatFloat(float v)
    {
        return v.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private void CollectHierarchyMeshesAndMaterials(Transform root, System.Collections.Generic.List<(Transform tr, Mesh mesh, Renderer renderer)> nodes, System.Collections.Generic.Dictionary<Material, string> matToName)
    {
        foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
        {
            MeshFilter mf = tr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                continue;
            Renderer rend = tr.GetComponent<Renderer>();
            nodes.Add((tr, mf.sharedMesh, rend));

            if (rend != null)
            {
                var m = rend.sharedMaterial;
                if (m != null && !matToName.ContainsKey(m))
                {
                    string name = SanitizePrimToken(m.name);
                    if (string.IsNullOrEmpty(name)) name = "Material";
                    int suffix = 1;
                    var existing = new System.Collections.Generic.HashSet<string>(matToName.Values);
                    string baseName = name;
                    while (existing.Contains(name))
                    {
                        name = baseName + "_" + suffix.ToString();
                        suffix++;
                    }
                    matToName[m] = name;
                }
            }
        }
    }

    private void WriteMaterialsInline(StreamWriter sw, System.Collections.Generic.Dictionary<Material, string> matToName, string indent)
    {
        string ind0 = indent;
        string ind1 = ind0 + "    ";
        string ind2 = ind1 + "    ";
        sw.WriteLine(ind0 + "def Scope \"Looks\" {");
        foreach (var kv in matToName)
        {
            var unityMat = kv.Key;
            var usdName = kv.Value;
            // Base color factor (URP/HDRP use _BaseColor)
            Color baseColor = Color.white;
            if (unityMat != null)
            {
                if (unityMat.HasProperty("_BaseColor")) baseColor = unityMat.GetColor("_BaseColor");
                else if (unityMat.HasProperty("_Color")) baseColor = unityMat.color;
            }
            // Prepare texture files
            string relTexBase = "assets";
            EnsureDir(Path.Combine(lastExportDir ?? "", relTexBase));
            string albedoRel = GetTextureRelativePathAny(unityMat, "_MainTex", "_BaseMap");
            string normalRel = GetTextureRelativePath(unityMat, "_BumpMap");
            string roughRel = GetTextureRelativePath(unityMat, "_RoughnessMap");
            string metallicGlossRel = GetTextureRelativePath(unityMat, "_MetallicGlossMap");
            float metallicScalar = 0f;
            try { if (unityMat.HasProperty("_Metallic")) metallicScalar = unityMat.GetFloat("_Metallic"); } catch {}
            bool useGlossAsRough = string.IsNullOrEmpty(roughRel) && !string.IsNullOrEmpty(metallicGlossRel);
            // UV tiling/offset
            Vector2 tiling = Vector2.one;
            Vector2 offset = Vector2.zero;
            try
            {
                if (unityMat.HasProperty("_MainTex")) { tiling = unityMat.GetTextureScale("_MainTex"); offset = unityMat.GetTextureOffset("_MainTex"); }
                else if (unityMat.HasProperty("_BaseMap")) { tiling = unityMat.GetTextureScale("_BaseMap"); offset = unityMat.GetTextureOffset("_BaseMap"); }
            }
            catch {}
            // Material type (only connect opacity for Transparent/Cutout)
            string renderType = string.Empty;
            try { renderType = unityMat.GetTag("RenderType", false, ""); } catch {}
            bool isCutout = renderType == "TransparentCutout" || renderType == "AlphaTest" || (unityMat.shader != null && unityMat.shader.name.Contains("Cutout"));
            bool isTransparent = renderType == "Transparent" || unityMat.renderQueue >= 3000;
            sw.WriteLine(ind1 + "def Material \"" + usdName + "\" {");
            sw.WriteLine(ind2 + "token outputs:surface.connect = </World/Looks/" + usdName + "/PreviewSurface.outputs:surface>");
            // Primvar st
            sw.WriteLine(ind2 + "def Shader \"Primvar_st\" {");
            sw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
            sw.WriteLine(ind2 + "    uniform token info:id = \"UsdPrimvarReader_float2\"");
            sw.WriteLine(ind2 + "    string inputs:varname = \"st\"");
            sw.WriteLine(ind2 + "    float2 outputs:result");
            sw.WriteLine(ind2 + "}");
            // UV Transform (tiling, offset)
            sw.WriteLine(ind2 + "def Shader \"UVTransform\" {");
            sw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
            sw.WriteLine(ind2 + "    uniform token info:id = \"UsdTransform2d\"");
            sw.WriteLine(ind2 + "    float2 inputs:in.connect = </World/Looks/" + usdName + "/Primvar_st.outputs:result>");
            sw.WriteLine(ind2 + "    float inputs:rotation = 0");
            sw.WriteLine(ind2 + "    float2 inputs:scale = (" + FormatFloat(tiling.x) + ", " + FormatFloat(tiling.y) + ")");
            sw.WriteLine(ind2 + "    float2 inputs:translation = (" + FormatFloat(offset.x) + ", " + FormatFloat(offset.y) + ")");
            sw.WriteLine(ind2 + "    float2 outputs:result");
            sw.WriteLine(ind2 + "}");
            // Albedo texture
            if (!string.IsNullOrEmpty(albedoRel))
            {
                sw.WriteLine(ind2 + "def Shader \"AlbedoTex\" {");
                sw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
                sw.WriteLine(ind2 + "    uniform token info:id = \"UsdUVTexture\"");
                sw.WriteLine(ind2 + "    asset inputs:file = @" + albedoRel + "@");
                sw.WriteLine(ind2 + "    token inputs:sourceColorSpace = \"sRGB\"");
                sw.WriteLine(ind2 + "    float2 inputs:st.connect = </World/Looks/" + usdName + "/UVTransform.outputs:result>");
                sw.WriteLine(ind2 + "    token inputs:wrapS = \"repeat\"");
                sw.WriteLine(ind2 + "    token inputs:wrapT = \"repeat\"");
                // Multiply by base color factor
                sw.WriteLine(ind2 + "    float3 inputs:scale = (" + FormatFloat(baseColor.r) + ", " + FormatFloat(baseColor.g) + ", " + FormatFloat(baseColor.b) + ")");
                sw.WriteLine(ind2 + "    float3 outputs:rgb");
                sw.WriteLine(ind2 + "    float outputs:a");
                sw.WriteLine(ind2 + "}");
            }
            // Normal texture
            if (!string.IsNullOrEmpty(normalRel))
            {
                sw.WriteLine(ind2 + "def Shader \"NormalTex\" {");
                sw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
                sw.WriteLine(ind2 + "    uniform token info:id = \"UsdUVTexture\"");
                sw.WriteLine(ind2 + "    asset inputs:file = @" + normalRel + "@");
                sw.WriteLine(ind2 + "    token inputs:sourceColorSpace = \"raw\"");
                sw.WriteLine(ind2 + "    float2 inputs:st.connect = </World/Looks/" + usdName + "/UVTransform.outputs:result>");
                sw.WriteLine(ind2 + "    float3 inputs:scale = (2, 2, 2)");
                sw.WriteLine(ind2 + "    float3 inputs:bias = (-1, -1, -1)");
                sw.WriteLine(ind2 + "    token inputs:wrapS = \"repeat\"");
                sw.WriteLine(ind2 + "    token inputs:wrapT = \"repeat\"");
                sw.WriteLine(ind2 + "    float3 outputs:rgb");
                sw.WriteLine(ind2 + "}");
            }
            // Roughness texture
            if (!string.IsNullOrEmpty(roughRel) || useGlossAsRough)
            {
                sw.WriteLine(ind2 + "def Shader \"RoughnessTex\" {");
                sw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
                sw.WriteLine(ind2 + "    uniform token info:id = \"UsdUVTexture\"");
                sw.WriteLine(ind2 + "    asset inputs:file = @" + (useGlossAsRough ? metallicGlossRel : roughRel) + "@");
                sw.WriteLine(ind2 + "    token inputs:sourceColorSpace = \"raw\"");
                sw.WriteLine(ind2 + "    float2 inputs:st.connect = </World/Looks/" + usdName + "/UVTransform.outputs:result>");
                sw.WriteLine(ind2 + "    token inputs:wrapS = \"repeat\"");
                sw.WriteLine(ind2 + "    token inputs:wrapT = \"repeat\"");
                if (useGlossAsRough)
                {
                    sw.WriteLine(ind2 + "    float inputs:scale = -1");
                    sw.WriteLine(ind2 + "    float inputs:bias = 1");
                    sw.WriteLine(ind2 + "    float outputs:a");
                }
                else
                {
                    sw.WriteLine(ind2 + "    float outputs:r");
                }
                sw.WriteLine(ind2 + "}");
            }
            // Metallic from MetallicGlossMap R channel (if present)
            if (!string.IsNullOrEmpty(metallicGlossRel))
            {
                sw.WriteLine(ind2 + "def Shader \"MetallicTex\" {");
                sw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
                sw.WriteLine(ind2 + "    uniform token info:id = \"UsdUVTexture\"");
                sw.WriteLine(ind2 + "    asset inputs:file = @" + metallicGlossRel + "@");
                sw.WriteLine(ind2 + "    token inputs:sourceColorSpace = \"raw\"");
                sw.WriteLine(ind2 + "    float2 inputs:st.connect = </World/Looks/" + usdName + "/UVTransform.outputs:result>");
                sw.WriteLine(ind2 + "    token inputs:wrapS = \"repeat\"");
                sw.WriteLine(ind2 + "    token inputs:wrapT = \"repeat\"");
                sw.WriteLine(ind2 + "    float outputs:r");
                sw.WriteLine(ind2 + "}");
            }
            sw.WriteLine(ind2 + "def Shader \"PreviewSurface\" {");
            sw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
            sw.WriteLine(ind2 + "    uniform token info:id = \"UsdPreviewSurface\"");
            if (!string.IsNullOrEmpty(albedoRel))
                sw.WriteLine(ind2 + "    color3f inputs:diffuseColor.connect = </World/Looks/" + usdName + "/AlbedoTex.outputs:rgb>");
            else
                sw.WriteLine(ind2 + "    color3f inputs:diffuseColor = (" + FormatFloat(baseColor.r) + ", " + FormatFloat(baseColor.g) + ", " + FormatFloat(baseColor.b) + ")");
            if (!string.IsNullOrEmpty(albedoRel) && (isCutout || isTransparent))
            {
                sw.WriteLine(ind2 + "    float inputs:opacity.connect = </World/Looks/" + usdName + "/AlbedoTex.outputs:a>");
                if (isCutout)
                {
                    float cutoff = 0.5f;
                    if (unityMat.HasProperty("_Cutoff")) cutoff = unityMat.GetFloat("_Cutoff");
                    sw.WriteLine(ind2 + "    float inputs:opacityThreshold = " + FormatFloat(cutoff));
                }
            }
            if (!string.IsNullOrEmpty(normalRel))
                sw.WriteLine(ind2 + "    normal3f inputs:normal.connect = </World/Looks/" + usdName + "/NormalTex.outputs:rgb>");
            if (!string.IsNullOrEmpty(roughRel))
                sw.WriteLine(ind2 + "    float inputs:roughness.connect = </World/Looks/" + usdName + "/RoughnessTex.outputs:r>");
            else if (useGlossAsRough)
                sw.WriteLine(ind2 + "    float inputs:roughness.connect = </World/Looks/" + usdName + "/RoughnessTex.outputs:a>");
            else
                sw.WriteLine(ind2 + "    float inputs:roughness = 0.5");
            // Defaults inspired by reference scene
            if (string.IsNullOrEmpty(albedoRel) || !(isCutout || isTransparent))
                sw.WriteLine(ind2 + "    float inputs:opacity = 1");
            sw.WriteLine(ind2 + "    float inputs:specular = 0.5");
            sw.WriteLine(ind2 + "    float inputs:ior = 1.5");
            if (!string.IsNullOrEmpty(metallicGlossRel))
                sw.WriteLine(ind2 + "    float inputs:metallic.connect = </World/Looks/" + usdName + "/MetallicTex.outputs:r>");
            else
                sw.WriteLine(ind2 + "    float inputs:metallic = " + FormatFloat(metallicScalar));
            sw.WriteLine(ind2 + "    token outputs:surface");
            sw.WriteLine(ind2 + "}");
            sw.WriteLine(ind1 + "}");
        }
        sw.WriteLine(ind0 + "}");
        sw.WriteLine();
    }

    private void WriteMaterialsFile(string filePath, System.Collections.Generic.Dictionary<Material, string> matToName)
    {
        using (StreamWriter msw = new StreamWriter(filePath, false, new UTF8Encoding(false)))
        {
            msw.WriteLine("#usda 1.0");
            msw.WriteLine();
            msw.WriteLine("def Scope \"Looks\" {");
            string ind1 = "    ";
            string ind2 = ind1 + "    ";
            foreach (var kv in matToName)
            {
                var unityMat = kv.Key;
                var usdName = kv.Value;
                // Base color factor
                Color baseColor = Color.white;
                if (unityMat != null)
                {
                    if (unityMat.HasProperty("_BaseColor")) baseColor = unityMat.GetColor("_BaseColor");
                    else if (unityMat.HasProperty("_Color")) baseColor = unityMat.color;
                }
                // UV tiling/offset
                Vector2 tiling = Vector2.one;
                Vector2 offset = Vector2.zero;
                try
                {
                    if (unityMat.HasProperty("_MainTex")) { tiling = unityMat.GetTextureScale("_MainTex"); offset = unityMat.GetTextureOffset("_MainTex"); }
                    else if (unityMat.HasProperty("_BaseMap")) { tiling = unityMat.GetTextureScale("_BaseMap"); offset = unityMat.GetTextureOffset("_BaseMap"); }
                }
                catch {}
                EnsureDir(Path.Combine(Path.GetDirectoryName(filePath) ?? "", "assets"));
                string albedoRel = GetTextureRelativePathAny(unityMat, "_MainTex", "_BaseMap");
                string normalRel = GetTextureRelativePath(unityMat, "_BumpMap");
                string roughRel = GetTextureRelativePath(unityMat, "_RoughnessMap");
                string metallicGlossRel = GetTextureRelativePath(unityMat, "_MetallicGlossMap");
                float metallicScalar = 0f;
                try { if (unityMat.HasProperty("_Metallic")) metallicScalar = unityMat.GetFloat("_Metallic"); } catch {}
                bool useGlossAsRough = string.IsNullOrEmpty(roughRel) && !string.IsNullOrEmpty(metallicGlossRel);
                // Material type
                string renderType = string.Empty;
                try { renderType = unityMat.GetTag("RenderType", false, ""); } catch {}
                bool isCutout = renderType == "TransparentCutout" || renderType == "AlphaTest" || (unityMat.shader != null && unityMat.shader.name.Contains("Cutout"));
                bool isTransparent = renderType == "Transparent" || unityMat.renderQueue >= 3000;
                msw.WriteLine(ind1 + "def Material \"" + usdName + "\" {");
                msw.WriteLine(ind2 + "token outputs:surface.connect = </Looks/" + usdName + "/PreviewSurface.outputs:surface>");
                // Primvar st
                msw.WriteLine(ind2 + "def Shader \"Primvar_st\" {");
                msw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
                msw.WriteLine(ind2 + "    uniform token info:id = \"UsdPrimvarReader_float2\"");
                msw.WriteLine(ind2 + "    string inputs:varname = \"st\"");
                msw.WriteLine(ind2 + "    float2 outputs:result");
                msw.WriteLine(ind2 + "}");
                // UV Transform
                msw.WriteLine(ind2 + "def Shader \"UVTransform\" {");
                msw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
                msw.WriteLine(ind2 + "    uniform token info:id = \"UsdTransform2d\"");
                msw.WriteLine(ind2 + "    float2 inputs:in.connect = </Looks/" + usdName + "/Primvar_st.outputs:result>");
                msw.WriteLine(ind2 + "    float inputs:rotation = 0");
                msw.WriteLine(ind2 + "    float2 inputs:scale = (" + FormatFloat(tiling.x) + ", " + FormatFloat(tiling.y) + ")");
                msw.WriteLine(ind2 + "    float2 inputs:translation = (" + FormatFloat(offset.x) + ", " + FormatFloat(offset.y) + ")");
                msw.WriteLine(ind2 + "    float2 outputs:result");
                msw.WriteLine(ind2 + "}");
                if (!string.IsNullOrEmpty(albedoRel))
                {
                    msw.WriteLine(ind2 + "def Shader \"AlbedoTex\" {");
                    msw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
                    msw.WriteLine(ind2 + "    uniform token info:id = \"UsdUVTexture\"");
                    msw.WriteLine(ind2 + "    asset inputs:file = @" + albedoRel + "@");
                    msw.WriteLine(ind2 + "    token inputs:sourceColorSpace = \"sRGB\"");
                    msw.WriteLine(ind2 + "    float2 inputs:st.connect = </Looks/" + usdName + "/UVTransform.outputs:result>");
                    msw.WriteLine(ind2 + "    token inputs:wrapS = \"repeat\"");
                    msw.WriteLine(ind2 + "    token inputs:wrapT = \"repeat\"");
                    msw.WriteLine(ind2 + "    float3 inputs:scale = (" + FormatFloat(baseColor.r) + ", " + FormatFloat(baseColor.g) + ", " + FormatFloat(baseColor.b) + ")");
                    msw.WriteLine(ind2 + "    float3 outputs:rgb");
                    msw.WriteLine(ind2 + "    float outputs:a");
                    msw.WriteLine(ind2 + "}");
                }
                if (!string.IsNullOrEmpty(normalRel))
                {
                    msw.WriteLine(ind2 + "def Shader \"NormalTex\" {");
                    msw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
                    msw.WriteLine(ind2 + "    uniform token info:id = \"UsdUVTexture\"");
                    msw.WriteLine(ind2 + "    asset inputs:file = @" + normalRel + "@");
                    msw.WriteLine(ind2 + "    token inputs:sourceColorSpace = \"raw\"");
                    msw.WriteLine(ind2 + "    float2 inputs:st.connect = </Looks/" + usdName + "/UVTransform.outputs:result>");
                    msw.WriteLine(ind2 + "    float3 inputs:scale = (2, 2, 2)");
                    msw.WriteLine(ind2 + "    float3 inputs:bias = (-1, -1, -1)");
                    msw.WriteLine(ind2 + "    token inputs:wrapS = \"repeat\"");
                    msw.WriteLine(ind2 + "    token inputs:wrapT = \"repeat\"");
                    msw.WriteLine(ind2 + "    float3 outputs:rgb");
                    msw.WriteLine(ind2 + "}");
                }
                if (!string.IsNullOrEmpty(roughRel) || useGlossAsRough)
                {
                    msw.WriteLine(ind2 + "def Shader \"RoughnessTex\" {");
                    msw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
                    msw.WriteLine(ind2 + "    uniform token info:id = \"UsdUVTexture\"");
                    msw.WriteLine(ind2 + "    asset inputs:file = @" + (useGlossAsRough ? metallicGlossRel : roughRel) + "@");
                    msw.WriteLine(ind2 + "    token inputs:sourceColorSpace = \"raw\"");
                    msw.WriteLine(ind2 + "    float2 inputs:st.connect = </Looks/" + usdName + "/UVTransform.outputs:result>");
                    msw.WriteLine(ind2 + "    token inputs:wrapS = \"repeat\"");
                    msw.WriteLine(ind2 + "    token inputs:wrapT = \"repeat\"");
                    if (useGlossAsRough)
                    {
                        msw.WriteLine(ind2 + "    float inputs:scale = -1");
                        msw.WriteLine(ind2 + "    float inputs:bias = 1");
                        msw.WriteLine(ind2 + "    float outputs:a");
                    }
                    else
                    {
                        msw.WriteLine(ind2 + "    float outputs:r");
                    }
                    msw.WriteLine(ind2 + "}");
                }
                if (!string.IsNullOrEmpty(metallicGlossRel))
                {
                    msw.WriteLine(ind2 + "def Shader \"MetallicTex\" {");
                    msw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
                    msw.WriteLine(ind2 + "    uniform token info:id = \"UsdUVTexture\"");
                    msw.WriteLine(ind2 + "    asset inputs:file = @" + metallicGlossRel + "@");
                    msw.WriteLine(ind2 + "    token inputs:sourceColorSpace = \"raw\"");
                    msw.WriteLine(ind2 + "    float2 inputs:st.connect = </Looks/" + usdName + "/UVTransform.outputs:result>");
                    msw.WriteLine(ind2 + "    token inputs:wrapS = \"repeat\"");
                    msw.WriteLine(ind2 + "    token inputs:wrapT = \"repeat\"");
                    msw.WriteLine(ind2 + "    float outputs:r");
                    msw.WriteLine(ind2 + "}");
                }
                msw.WriteLine(ind2 + "def Shader \"PreviewSurface\" {");
                msw.WriteLine(ind2 + "    uniform token info:implementationSource = \"id\"");
                msw.WriteLine(ind2 + "    uniform token info:id = \"UsdPreviewSurface\"");
                if (!string.IsNullOrEmpty(albedoRel))
                    msw.WriteLine(ind2 + "    color3f inputs:diffuseColor.connect = </Looks/" + usdName + "/AlbedoTex.outputs:rgb>");
                else
                    msw.WriteLine(ind2 + "    color3f inputs:diffuseColor = (" + FormatFloat(baseColor.r) + ", " + FormatFloat(baseColor.g) + ", " + FormatFloat(baseColor.b) + ")");
                if (!string.IsNullOrEmpty(albedoRel) && (isCutout || isTransparent))
                {
                    msw.WriteLine(ind2 + "    float inputs:opacity.connect = </Looks/" + usdName + "/AlbedoTex.outputs:a>");
                    if (isCutout)
                    {
                        float cutoff = 0.5f;
                        if (unityMat.HasProperty("_Cutoff")) cutoff = unityMat.GetFloat("_Cutoff");
                        msw.WriteLine(ind2 + "    float inputs:opacityThreshold = " + FormatFloat(cutoff));
                    }
                }
                if (!string.IsNullOrEmpty(normalRel))
                    msw.WriteLine(ind2 + "    normal3f inputs:normal.connect = </Looks/" + usdName + "/NormalTex.outputs:rgb>");
                if (!string.IsNullOrEmpty(roughRel))
                    msw.WriteLine(ind2 + "    float inputs:roughness.connect = </Looks/" + usdName + "/RoughnessTex.outputs:r>");
                else if (useGlossAsRough)
                    msw.WriteLine(ind2 + "    float inputs:roughness.connect = </Looks/" + usdName + "/RoughnessTex.outputs:a>");
                else
                    msw.WriteLine(ind2 + "    float inputs:roughness = 0.5");
                if (string.IsNullOrEmpty(albedoRel) || !(isCutout || isTransparent))
                    msw.WriteLine(ind2 + "    float inputs:opacity = 1");
                msw.WriteLine(ind2 + "    float inputs:specular = 0.5");
                msw.WriteLine(ind2 + "    float inputs:ior = 1.5");
                if (!string.IsNullOrEmpty(metallicGlossRel))
                    msw.WriteLine(ind2 + "    float inputs:metallic.connect = </Looks/" + usdName + "/MetallicTex.outputs:r>");
                else
                    msw.WriteLine(ind2 + "    float inputs:metallic = " + FormatFloat(metallicScalar));
                msw.WriteLine(ind2 + "    token outputs:surface");
                msw.WriteLine(ind2 + "}");
                msw.WriteLine(ind1 + "}");
            }
            msw.WriteLine("}");
        }
    }

    private void EnsureDir(string dir)
    {
        if (string.IsNullOrEmpty(dir)) return;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }

    private string GetTextureRelativePath(Material mat, string propName)
    {
        if (mat == null || !mat.HasProperty(propName)) return null;
        var tex = mat.GetTexture(propName) as Texture2D;
        if (tex == null) return null;
        string src = AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(src) || !File.Exists(src))
        {
            Debug.LogWarning("Cannot locate source texture file for material property " + propName + ". Skipping.");
            return null;
        }
        string fileName = Path.GetFileName(src);
        string rel = Path.Combine("assets", fileName).Replace('\\', '/');
        string dst = Path.Combine(lastExportDir ?? "", rel);
        EnsureDir(Path.GetDirectoryName(dst));
        try
        {
            File.Copy(src, dst, true);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Failed to copy texture to export assets: " + ex.Message);
        }
        return rel;
    }

    private string GetTextureRelativePathAny(Material mat, params string[] propNames)
    {
        if (mat == null || propNames == null) return null;
        foreach (var p in propNames)
        {
            var r = GetTextureRelativePath(mat, p);
            if (!string.IsNullOrEmpty(r)) return r;
        }
        return null;
    }
}


