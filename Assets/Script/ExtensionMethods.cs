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


}
