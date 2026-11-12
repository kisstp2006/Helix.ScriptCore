using System;
using HelixEngine.Internal;

namespace HelixEngine
{
    // Rotations: x, y, z, w; Euler angles in degrees rotate around Z, then X, then Y (as the reference). The
    // products are arithmetic in C#, the rest Helix.Math's.
    public partial struct Quaternion : IEquatable<Quaternion>, IFormattable
    {
        public const float kEpsilon = 1e-6f;

        public Quaternion(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public float this[int index]
        {
            readonly get => index switch
            {
                0 => x,
                1 => y,
                2 => z,
                3 => w,
                _ => throw new IndexOutOfRangeException("Invalid Quaternion index!"),
            };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    case 3: w = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Quaternion index!");
                }
            }
        }

        public static Quaternion identity => new(0, 0, 0, 1);

        public Vector3 eulerAngles
        {
            readonly get => MathNative.QuaternionEulerAngles(this);
            set => this = Euler(value);
        }

        public readonly Quaternion normalized => Normalize(this);

        public void Normalize() => this = Normalize(this);
        public void Set(float newX, float newY, float newZ, float newW)
        {
            x = newX;
            y = newY;
            z = newZ;
            w = newW;
        }
        public void SetFromToRotation(Vector3 fromDirection, Vector3 toDirection) => this = FromToRotation(fromDirection, toDirection);
        public void SetLookRotation(Vector3 view) => this = LookRotation(view, Vector3.up);
        public void SetLookRotation(Vector3 view, Vector3 up) => this = LookRotation(view, up);
        public readonly void ToAngleAxis(out float angle, out Vector3 axis)
        {
            angle = 0;
            axis = default;
            MathNative.QuaternionToAngleAxis(this, ref angle, ref axis);
        }

        public static Quaternion Euler(float x, float y, float z) => MathNative.QuaternionEuler(x, y, z);
        public static Quaternion Euler(Vector3 euler) => MathNative.QuaternionEuler(euler.x, euler.y, euler.z);
        public static Quaternion AngleAxis(float angle, Vector3 axis) => MathNative.QuaternionAngleAxis(angle, axis);
        public static Quaternion LookRotation(Vector3 forward) => MathNative.QuaternionLookRotation(forward, Vector3.up);
        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards) => MathNative.QuaternionLookRotation(forward, upwards);
        public static Quaternion FromToRotation(Vector3 fromDirection, Vector3 toDirection) => MathNative.QuaternionFromToRotation(fromDirection, toDirection);
        public static Quaternion Inverse(Quaternion rotation) => new(-rotation.x, -rotation.y, -rotation.z, rotation.w);
        public static Quaternion Normalize(Quaternion q) => MathNative.QuaternionNormalize(q);
        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        public static float Angle(Quaternion a, Quaternion b) => MathNative.QuaternionAngle(a, b);
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => MathNative.QuaternionSlerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t) => MathNative.QuaternionSlerpUnclamped(a, b, t);
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => MathNative.QuaternionLerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Quaternion LerpUnclamped(Quaternion a, Quaternion b, float t) => MathNative.QuaternionLerpUnclamped(a, b, t);
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta) => MathNative.QuaternionRotateTowards(from, to, maxDegreesDelta);

        public static Quaternion operator *(Quaternion lhs, Quaternion rhs) =>
            new(lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y, lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z,
                lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x, lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);

        public static Vector3 operator *(Quaternion rotation, Vector3 point)
        {
            float x2 = rotation.x * 2, y2 = rotation.y * 2, z2 = rotation.z * 2;
            float xx = rotation.x * x2, yy = rotation.y * y2, zz = rotation.z * z2;
            float xy = rotation.x * y2, xz = rotation.x * z2, yz = rotation.y * z2;
            float wx = rotation.w * x2, wy = rotation.w * y2, wz = rotation.w * z2;
            return new Vector3((1 - (yy + zz)) * point.x + (xy - wz) * point.y + (xz + wy) * point.z,
                               (xy + wz) * point.x + (1 - (xx + zz)) * point.y + (yz - wx) * point.z,
                               (xz - wy) * point.x + (yz + wx) * point.y + (1 - (xx + yy)) * point.z);
        }

        public static bool operator ==(Quaternion lhs, Quaternion rhs) => Dot(lhs, rhs) > 1 - kEpsilon;
        public static bool operator !=(Quaternion lhs, Quaternion rhs) => !(lhs == rhs);

        public readonly bool Equals(Quaternion other) => x == other.x && y == other.y && z == other.z && w == other.w;
        public override readonly bool Equals(object other) => other is Quaternion q && Equals(q);
        public override readonly int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2) ^ (w.GetHashCode() >> 1);
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "(" + Fmt.F(x, format, "F5", formatProvider) + ", " + Fmt.F(y, format, "F5", formatProvider) + ", " + Fmt.F(z, format, "F5", formatProvider) + ", " +
            Fmt.F(w, format, "F5", formatProvider) + ")";
    }
}
