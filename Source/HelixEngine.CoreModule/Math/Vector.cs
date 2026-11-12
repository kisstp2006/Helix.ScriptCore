using System;
using System.Globalization;
using HelixEngine.Internal;

namespace HelixEngine
{
    // The members of the vector types (their fields are generated from Helix.Math): arithmetic in C#, the rest through
    // Helix.Math (MathNative), the reference's formats (F2), == approximately equal, Equals exact.
    public partial struct Vector2 : IEquatable<Vector2>, IFormattable
    {
        public const float kEpsilon = 1e-5f;
        public const float kEpsilonNormalSqrt = 1e-15f;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public float this[int index]
        {
            readonly get => index switch { 0 => x, 1 => y, _ => throw new IndexOutOfRangeException("Invalid Vector2 index!") };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Vector2 index!");
                }
            }
        }

        public static Vector2 zero => new(0, 0);
        public static Vector2 one => new(1, 1);
        public static Vector2 up => new(0, 1);
        public static Vector2 down => new(0, -1);
        public static Vector2 left => new(-1, 0);
        public static Vector2 right => new(1, 0);
        public static Vector2 positiveInfinity => new(float.PositiveInfinity, float.PositiveInfinity);
        public static Vector2 negativeInfinity => new(float.NegativeInfinity, float.NegativeInfinity);

        public readonly float sqrMagnitude => x * x + y * y;
        public readonly float magnitude => MathF.Sqrt(sqrMagnitude);
        public readonly Vector2 normalized => Normalize(this);

        public void Normalize() => this = Normalize(this);
        public void Set(float newX, float newY)
        {
            x = newX;
            y = newY;
        }
        public void Scale(Vector2 scale)
        {
            x *= scale.x;
            y *= scale.y;
        }

        public static Vector2 Normalize(Vector2 value)
        {
            float m = value.magnitude;
            return m > kEpsilon ? value / m : zero;
        }
        public static float Dot(Vector2 lhs, Vector2 rhs) => lhs.x * rhs.x + lhs.y * rhs.y;
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        public static float Angle(Vector2 from, Vector2 to)
        {
            float d = MathF.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            return d < kEpsilonNormalSqrt ? 0 : MathF.Acos(Mathf.Clamp(Dot(from, to) / d, -1, 1)) * Mathf.Rad2Deg;
        }
        public static float SignedAngle(Vector2 from, Vector2 to) => Angle(from, to) * Mathf.Sign(from.x * to.y - from.y * to.x);
        public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta)
        {
            Vector2 d = target - current;
            float sq = d.sqrMagnitude;
            if (sq == 0 || (maxDistanceDelta >= 0 && sq <= maxDistanceDelta * maxDistanceDelta))
                return target;
            return current + d / MathF.Sqrt(sq) * maxDistanceDelta;
        }
        public static Vector2 ClampMagnitude(Vector2 vector, float maxLength)
        {
            float sq = vector.sqrMagnitude;
            return sq > maxLength * maxLength ? vector / MathF.Sqrt(sq) * maxLength : vector;
        }
        public static Vector2 Reflect(Vector2 inDirection, Vector2 inNormal) => inDirection - inNormal * (2 * Dot(inNormal, inDirection));
        public static Vector2 Perpendicular(Vector2 inDirection) => new(-inDirection.y, inDirection.x);
        public static Vector2 Scale(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);
        public static Vector2 Min(Vector2 lhs, Vector2 rhs) => new(MathF.Min(lhs.x, rhs.x), MathF.Min(lhs.y, rhs.y));
        public static Vector2 Max(Vector2 lhs, Vector2 rhs) => new(MathF.Max(lhs.x, rhs.x), MathF.Max(lhs.y, rhs.y));
        public static Vector2 SmoothDamp(Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime) =>
            SmoothDamp(current, target, ref currentVelocity, smoothTime, float.PositiveInfinity, Time.deltaTime);
        public static Vector2 SmoothDamp(Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime, float maxSpeed) =>
            SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);
        public static Vector2 SmoothDamp(Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            Vector3 velocity = currentVelocity;
            Vector3 result = MathNative.Vector3SmoothDamp(current, target, ref velocity, smoothTime, maxSpeed, deltaTime);
            currentVelocity = velocity;
            return result;
        }

        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);
        public static Vector2 operator /(Vector2 a, Vector2 b) => new(a.x / b.x, a.y / b.y);
        public static Vector2 operator *(Vector2 a, float d) => new(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new(a.x * d, a.y * d);
        public static Vector2 operator /(Vector2 a, float d) => new(a.x / d, a.y / d);
        public static bool operator ==(Vector2 lhs, Vector2 rhs) => (lhs - rhs).sqrMagnitude < kEpsilon * kEpsilon;
        public static bool operator !=(Vector2 lhs, Vector2 rhs) => !(lhs == rhs);
        public static implicit operator Vector2(Vector3 v) => new(v.x, v.y);
        public static implicit operator Vector3(Vector2 v) => new(v.x, v.y, 0);

        public readonly bool Equals(Vector2 other) => x == other.x && y == other.y;
        public override readonly bool Equals(object other) => other is Vector2 v && Equals(v);
        public override readonly int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "(" + Fmt.F(x, format, "F2", formatProvider) + ", " + Fmt.F(y, format, "F2", formatProvider) + ")";
    }

    public partial struct Vector3 : IEquatable<Vector3>, IFormattable
    {
        public const float kEpsilon = 1e-5f;
        public const float kEpsilonNormalSqrt = 1e-15f;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3(float x, float y) : this(x, y, 0) { }

        public float this[int index]
        {
            readonly get => index switch { 0 => x, 1 => y, 2 => z, _ => throw new IndexOutOfRangeException("Invalid Vector3 index!") };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Vector3 index!");
                }
            }
        }

        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 down => new(0, -1, 0);
        public static Vector3 left => new(-1, 0, 0);
        public static Vector3 right => new(1, 0, 0);
        public static Vector3 forward => new(0, 0, 1);
        public static Vector3 back => new(0, 0, -1);
        public static Vector3 positiveInfinity => new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        public static Vector3 negativeInfinity => new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        public readonly float sqrMagnitude => x * x + y * y + z * z;
        public readonly float magnitude => MathF.Sqrt(sqrMagnitude);
        public readonly Vector3 normalized => Normalize(this);

        public void Normalize() => this = Normalize(this);
        public void Set(float newX, float newY, float newZ)
        {
            x = newX;
            y = newY;
            z = newZ;
        }
        public void Scale(Vector3 scale)
        {
            x *= scale.x;
            y *= scale.y;
            z *= scale.z;
        }

        public static Vector3 Normalize(Vector3 value)
        {
            float m = value.magnitude;
            return m > kEpsilon ? value / m : zero;
        }
        public static float Dot(Vector3 lhs, Vector3 rhs) => lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
        public static Vector3 Cross(Vector3 lhs, Vector3 rhs) =>
            new(lhs.y * rhs.z - lhs.z * rhs.y, lhs.z * rhs.x - lhs.x * rhs.z, lhs.x * rhs.y - lhs.y * rhs.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static float Magnitude(Vector3 vector) => vector.magnitude;
        public static float SqrMagnitude(Vector3 vector) => vector.sqrMagnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => MathNative.Vector3Slerp(a, b, t);
        public static Vector3 SlerpUnclamped(Vector3 a, Vector3 b, float t) => MathNative.Vector3SlerpUnclamped(a, b, t);
        public static float Angle(Vector3 from, Vector3 to)
        {
            float d = MathF.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            return d < kEpsilonNormalSqrt ? 0 : MathF.Acos(Mathf.Clamp(Dot(from, to) / d, -1, 1)) * Mathf.Rad2Deg;
        }
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) => Angle(from, to) * Mathf.Sign(Dot(axis, Cross(from, to)));
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
        {
            Vector3 d = target - current;
            float sq = d.sqrMagnitude;
            if (sq == 0 || (maxDistanceDelta >= 0 && sq <= maxDistanceDelta * maxDistanceDelta))
                return target;
            return current + d / MathF.Sqrt(sq) * maxDistanceDelta;
        }
        public static Vector3 RotateTowards(Vector3 current, Vector3 target, float maxRadiansDelta, float maxMagnitudeDelta) =>
            MathNative.Vector3RotateTowards(current, target, maxRadiansDelta, maxMagnitudeDelta);
        public static Vector3 ClampMagnitude(Vector3 vector, float maxLength)
        {
            float sq = vector.sqrMagnitude;
            return sq > maxLength * maxLength ? vector / MathF.Sqrt(sq) * maxLength : vector;
        }
        public static Vector3 Project(Vector3 vector, Vector3 onNormal)
        {
            float sq = Dot(onNormal, onNormal);
            return sq < Mathf.Epsilon ? zero : onNormal * (Dot(vector, onNormal) / sq);
        }
        public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal) => vector - Project(vector, planeNormal);
        public static Vector3 Reflect(Vector3 inDirection, Vector3 inNormal) => inDirection - inNormal * (2 * Dot(inNormal, inDirection));
        public static void OrthoNormalize(ref Vector3 normal, ref Vector3 tangent) => MathNative.Vector3OrthoNormalize(ref normal, ref tangent);
        public static void OrthoNormalize(ref Vector3 normal, ref Vector3 tangent, ref Vector3 binormal) =>
            MathNative.Vector3OrthoNormalize3(ref normal, ref tangent, ref binormal);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 Min(Vector3 lhs, Vector3 rhs) => new(MathF.Min(lhs.x, rhs.x), MathF.Min(lhs.y, rhs.y), MathF.Min(lhs.z, rhs.z));
        public static Vector3 Max(Vector3 lhs, Vector3 rhs) => new(MathF.Max(lhs.x, rhs.x), MathF.Max(lhs.y, rhs.y), MathF.Max(lhs.z, rhs.z));
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime) =>
            SmoothDamp(current, target, ref currentVelocity, smoothTime, float.PositiveInfinity, Time.deltaTime);
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed) =>
            SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed, float deltaTime) =>
            MathNative.Vector3SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new(a.x / d, a.y / d, a.z / d);
        public static bool operator ==(Vector3 lhs, Vector3 rhs) => (lhs - rhs).sqrMagnitude < kEpsilon * kEpsilon;
        public static bool operator !=(Vector3 lhs, Vector3 rhs) => !(lhs == rhs);

        public readonly bool Equals(Vector3 other) => x == other.x && y == other.y && z == other.z;
        public override readonly bool Equals(object other) => other is Vector3 v && Equals(v);
        public override readonly int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "(" + Fmt.F(x, format, "F2", formatProvider) + ", " + Fmt.F(y, format, "F2", formatProvider) + ", " + Fmt.F(z, format, "F2", formatProvider) + ")";
    }

    public partial struct Vector4 : IEquatable<Vector4>, IFormattable
    {
        public const float kEpsilon = 1e-5f;

        public Vector4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public Vector4(float x, float y, float z) : this(x, y, z, 0) { }
        public Vector4(float x, float y) : this(x, y, 0, 0) { }

        public float this[int index]
        {
            readonly get => index switch { 0 => x, 1 => y, 2 => z, 3 => w, _ => throw new IndexOutOfRangeException("Invalid Vector4 index!") };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    case 3: w = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Vector4 index!");
                }
            }
        }

        public static Vector4 zero => new(0, 0, 0, 0);
        public static Vector4 one => new(1, 1, 1, 1);
        public static Vector4 positiveInfinity => new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        public static Vector4 negativeInfinity => new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        public readonly float sqrMagnitude => x * x + y * y + z * z + w * w;
        public readonly float magnitude => MathF.Sqrt(sqrMagnitude);
        public readonly Vector4 normalized => Normalize(this);

        public void Normalize() => this = Normalize(this);
        public void Set(float newX, float newY, float newZ, float newW)
        {
            x = newX;
            y = newY;
            z = newZ;
            w = newW;
        }
        public void Scale(Vector4 scale)
        {
            x *= scale.x;
            y *= scale.y;
            z *= scale.z;
            w *= scale.w;
        }

        public static Vector4 Normalize(Vector4 a)
        {
            float m = a.magnitude;
            return m > kEpsilon ? a / m : zero;
        }
        public static float Dot(Vector4 a, Vector4 b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        public static float Distance(Vector4 a, Vector4 b) => (a - b).magnitude;
        public static Vector4 Lerp(Vector4 a, Vector4 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Vector4 LerpUnclamped(Vector4 a, Vector4 b, float t) =>
            new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
        public static Vector4 MoveTowards(Vector4 current, Vector4 target, float maxDistanceDelta)
        {
            Vector4 d = target - current;
            float sq = d.sqrMagnitude;
            if (sq == 0 || (maxDistanceDelta >= 0 && sq <= maxDistanceDelta * maxDistanceDelta))
                return target;
            return current + d / MathF.Sqrt(sq) * maxDistanceDelta;
        }
        public static Vector4 Project(Vector4 a, Vector4 b) => b * (Dot(a, b) / Dot(b, b));
        public static Vector4 Scale(Vector4 a, Vector4 b) => new(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);
        public static Vector4 Min(Vector4 lhs, Vector4 rhs) =>
            new(MathF.Min(lhs.x, rhs.x), MathF.Min(lhs.y, rhs.y), MathF.Min(lhs.z, rhs.z), MathF.Min(lhs.w, rhs.w));
        public static Vector4 Max(Vector4 lhs, Vector4 rhs) =>
            new(MathF.Max(lhs.x, rhs.x), MathF.Max(lhs.y, rhs.y), MathF.Max(lhs.z, rhs.z), MathF.Max(lhs.w, rhs.w));

        public static Vector4 operator +(Vector4 a, Vector4 b) => new(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        public static Vector4 operator -(Vector4 a, Vector4 b) => new(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
        public static Vector4 operator -(Vector4 a) => new(-a.x, -a.y, -a.z, -a.w);
        public static Vector4 operator *(Vector4 a, float d) => new(a.x * d, a.y * d, a.z * d, a.w * d);
        public static Vector4 operator *(float d, Vector4 a) => new(a.x * d, a.y * d, a.z * d, a.w * d);
        public static Vector4 operator /(Vector4 a, float d) => new(a.x / d, a.y / d, a.z / d, a.w / d);
        public static bool operator ==(Vector4 lhs, Vector4 rhs) => (lhs - rhs).sqrMagnitude < kEpsilon * kEpsilon;
        public static bool operator !=(Vector4 lhs, Vector4 rhs) => !(lhs == rhs);
        public static implicit operator Vector4(Vector3 v) => new(v.x, v.y, v.z, 0);
        public static implicit operator Vector3(Vector4 v) => new(v.x, v.y, v.z);
        public static implicit operator Vector4(Vector2 v) => new(v.x, v.y, 0, 0);
        public static implicit operator Vector2(Vector4 v) => new(v.x, v.y);

        public readonly bool Equals(Vector4 other) => x == other.x && y == other.y && z == other.z && w == other.w;
        public override readonly bool Equals(object other) => other is Vector4 v && Equals(v);
        public override readonly int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2) ^ (w.GetHashCode() >> 1);
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "(" + Fmt.F(x, format, "F2", formatProvider) + ", " + Fmt.F(y, format, "F2", formatProvider) + ", " + Fmt.F(z, format, "F2", formatProvider) + ", " +
            Fmt.F(w, format, "F2", formatProvider) + ")";
    }

    namespace Internal
    {
        // Numbers as the reference prints them: the given format or the type's default, the invariant culture unless a
        // provider is given.
        static class Fmt
        {
            public static string F(float v, string format, string fallback, IFormatProvider provider) =>
                v.ToString(string.IsNullOrEmpty(format) ? fallback : format, provider ?? CultureInfo.InvariantCulture.NumberFormat);

            public static string I(int v, string format, IFormatProvider provider) =>
                v.ToString(format, provider ?? CultureInfo.InvariantCulture.NumberFormat);
        }
    }
}
