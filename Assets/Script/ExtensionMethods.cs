//Utils for unity ros conversion and Extracting Terrain Mesh-by yuxuanxie
using System;
using System.Collections.Generic;
using UnityEngine;

public static class ExtensionMethods
{
    public static Vector3 VecRos2Unity(this Vector3 vector3_ros)
    {
        return new Vector3(-vector3_ros.y, vector3_ros.z, vector3_ros.x);
    }

    public static Vector3 VecUnity2Ros(this Vector3 vector3_unity)
    {
        return new Vector3(vector3_unity.z, -vector3_unity.x, vector3_unity.y);
    }
    public static Vector3 EulerRos2Unity(this Vector3 vector3_ros)
    {
        return new Vector3(vector3_ros.y, -vector3_ros.z, -vector3_ros.x);
    }

    public static Vector3 EulerUnity2Ros(this Vector3 vector3_unity)
    {
        return new Vector3(-vector3_unity.z, vector3_unity.x, -vector3_unity.y);
    }
    public static Quaternion QuaternionRos2Unity(this Quaternion qua_ros)
    {
        return new Quaternion(qua_ros.y, -qua_ros.z, -qua_ros.x, qua_ros.w);
    }

    public static Quaternion QuaternionUnity2Ros(this Quaternion qua_unity)
    {
        return new Quaternion(-qua_unity.z, qua_unity.x, -qua_unity.y, qua_unity.w);
    }
    public static Vector3 NormUnity2Ros(Vector3 n)
    {
        // Normal vectors should be transformed the same way as positions, 
        // but typically re-normalized if there's any scale involved
        Vector3 rosN = VecUnity2Ros(n);
        return rosN.normalized;
    }

    // In many cases UVs don’t need to be changed, because they’re a 2D coordinate
    // that depends on your texture layout, not your 3D coordinate system.
    // However, if you want to flip the V coordinate (e.g., Unity vs. ROS difference),
    // you might do something like this:
    public static Vector2 UVUnity2Ros(Vector2 uv)
    {
        // Example: Flip the V coordinate (1 - v)
        return new Vector2(uv.x, 1.0f - uv.y);
    }

    enum Resolution { Full = 0, Half, Quarter, Eighth, Sixteenth }

    public static Mesh TerrainToMesh(Terrain terrainObj)
    {
        Resolution Resolution = Resolution.Half;
        TerrainData terrainData = terrainObj.terrainData;
        int w = terrainData.heightmapResolution;
        int h = terrainData.heightmapResolution;
        Vector3 meshScale = terrainData.size;
        int tRes = (int)Mathf.Pow(2, (int)Resolution);
        meshScale = new Vector3(meshScale.x / (w - 1) * tRes, meshScale.y, meshScale.z / (h - 1) * tRes);
        Vector2 uvScale = new Vector2(1.0f / (w - 1), 1.0f / (h - 1));
        float[,] tData = terrainData.GetHeights(0, 0, w, h);

        w = (w - 1) / tRes ;
        h = (h - 1) / tRes ;
        Vector3[] tVertices = new Vector3[w * h];
        Vector2[] tUV = new Vector2[w * h];
        int[] tPolys = new int[(w - 1) * (h - 1) * 6];

        // Build vertices and UVs using standard orientation (x, height, z)
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                tVertices[x * w + y] = Vector3.Scale(meshScale, new Vector3(y, tData[x * tRes, y * tRes], x));
                tUV[x * w + y] = Vector2.Scale(new Vector2(y * tRes, x * tRes), uvScale);
            }
        }

        int index = 0;
        // Build triangle indices (winding order may be adjusted if needed)
        for (int y = 0; y < h - 1; y++)
        {
            for (int x = 0; x < w - 1; x++)
            {
                // Triangle 1
                tPolys[index++] = (x * w) + y;
                tPolys[index++] = ((x + 1) * w) + y;
                tPolys[index++] = (x * w) + y + 1;

                // Triangle 2
                tPolys[index++] = ((x + 1) * w) + y;
                tPolys[index++] = ((x + 1) * w) + y + 1;
                tPolys[index++] = (x * w) + y + 1;
            }
        }

        Mesh terrainMesh = new Mesh();
        terrainMesh.vertices = tVertices;
        terrainMesh.uv = tUV;
        terrainMesh.triangles = tPolys;
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();

        return terrainMesh;
    }
}
