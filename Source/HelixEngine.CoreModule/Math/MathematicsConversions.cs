using Helix.Mathematics;

namespace HelixEngine
{
    // The value types and Helix.Mathematics' types convert implicitly both ways (the same layouts).
    public partial struct Vector2
    {
        public static implicit operator float2(Vector2 v) => new float2(v.x, v.y);
        public static implicit operator Vector2(float2 v) => new Vector2(v.x, v.y);
    }

    public partial struct Vector3
    {
        public static implicit operator float3(Vector3 v) => new float3(v.x, v.y, v.z);
        public static implicit operator Vector3(float3 v) => new Vector3(v.x, v.y, v.z);
    }

    public partial struct Vector4
    {
        public static implicit operator float4(Vector4 v) => new float4(v.x, v.y, v.z, v.w);
        public static implicit operator Vector4(float4 v) => new Vector4(v.x, v.y, v.z, v.w);
    }

    public partial struct Quaternion
    {
        public static implicit operator quaternion(Quaternion q) => new quaternion(q.x, q.y, q.z, q.w);
        public static implicit operator Quaternion(quaternion q) => new Quaternion(q.value.x, q.value.y, q.value.z, q.value.w);
    }

    public partial struct Matrix4x4
    {
        public static implicit operator float4x4(Matrix4x4 m) => new float4x4(m.GetColumn(0), m.GetColumn(1), m.GetColumn(2), m.GetColumn(3));
        public static implicit operator Matrix4x4(float4x4 m) => new Matrix4x4(m.c0, m.c1, m.c2, m.c3);
    }
}
