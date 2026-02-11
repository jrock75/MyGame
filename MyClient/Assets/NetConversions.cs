public static class NetConversions
{
    public static UnityEngine.Vector3 ToUnity(System.Numerics.Vector3 v)
    {
        return new UnityEngine.Vector3(v.X, v.Y, v.Z);
    }

    public static System.Numerics.Vector3 ToNumerics(UnityEngine.Vector3 v)
    {
        return new System.Numerics.Vector3(v.x, v.y, v.z);
    }

    public static UnityEngine.Quaternion ToUnity(System.Numerics.Quaternion q)
    {
        return new UnityEngine.Quaternion(q.X, q.Y, q.Z, q.W);
    }

    public static System.Numerics.Quaternion ToNumerics(UnityEngine.Quaternion q)
    {
        return new System.Numerics.Quaternion(q.x, q.y, q.z, q.w);
    }
}
