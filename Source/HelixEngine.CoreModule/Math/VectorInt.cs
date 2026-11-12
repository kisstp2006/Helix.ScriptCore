using System;
using HelixEngine.Internal;

namespace HelixEngine
{
    // The integer vectors: x, y (and z) as properties over the generated private fields.
    public partial struct Vector2Int : IEquatable<Vector2Int>, IFormattable
    {
        public Vector2Int(int x, int y)
        {
            m_X = x;
            m_Y = y;
        }

        public int x { readonly get => m_X; set => m_X = value; }
        public int y { readonly get => m_Y; set => m_Y = value; }

        public int this[int index]
        {
            readonly get => index switch { 0 => x, 1 => y, _ => throw new IndexOutOfRangeException("Invalid Vector2Int index addressed: " + index + "!") };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Vector2Int index addressed: " + index + "!");
                }
            }
        }

        public static Vector2Int zero => new(0, 0);
        public static Vector2Int one => new(1, 1);
        public static Vector2Int up => new(0, 1);
        public static Vector2Int down => new(0, -1);
        public static Vector2Int left => new(-1, 0);
        public static Vector2Int right => new(1, 0);

        public readonly float magnitude => MathF.Sqrt(x * x + y * y);
        public readonly int sqrMagnitude => x * x + y * y;

        public void Set(int x, int y)
        {
            m_X = x;
            m_Y = y;
        }
        public void Scale(Vector2Int scale)
        {
            m_X *= scale.x;
            m_Y *= scale.y;
        }
        public void Clamp(Vector2Int min, Vector2Int max)
        {
            x = Math.Min(max.x, Math.Max(min.x, x));
            y = Math.Min(max.y, Math.Max(min.y, y));
        }

        public static float Distance(Vector2Int a, Vector2Int b) => (a - b).magnitude;
        public static Vector2Int Min(Vector2Int lhs, Vector2Int rhs) => new(Math.Min(lhs.x, rhs.x), Math.Min(lhs.y, rhs.y));
        public static Vector2Int Max(Vector2Int lhs, Vector2Int rhs) => new(Math.Max(lhs.x, rhs.x), Math.Max(lhs.y, rhs.y));
        public static Vector2Int Scale(Vector2Int a, Vector2Int b) => new(a.x * b.x, a.y * b.y);
        public static Vector2Int FloorToInt(Vector2 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        public static Vector2Int CeilToInt(Vector2 v) => new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));
        public static Vector2Int RoundToInt(Vector2 v) => new(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));

        public static Vector2Int operator -(Vector2Int v) => new(-v.x, -v.y);
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new(a.x - b.x, a.y - b.y);
        public static Vector2Int operator *(Vector2Int a, Vector2Int b) => new(a.x * b.x, a.y * b.y);
        public static Vector2Int operator *(int a, Vector2Int b) => new(a * b.x, a * b.y);
        public static Vector2Int operator *(Vector2Int a, int b) => new(a.x * b, a.y * b);
        public static Vector2Int operator /(Vector2Int a, int b) => new(a.x / b, a.y / b);
        public static bool operator ==(Vector2Int lhs, Vector2Int rhs) => lhs.x == rhs.x && lhs.y == rhs.y;
        public static bool operator !=(Vector2Int lhs, Vector2Int rhs) => !(lhs == rhs);
        public static implicit operator Vector2(Vector2Int v) => new(v.x, v.y);
        public static explicit operator Vector3Int(Vector2Int v) => new(v.x, v.y, 0);

        public readonly bool Equals(Vector2Int other) => x == other.x && y == other.y;
        public override readonly bool Equals(object other) => other is Vector2Int v && Equals(v);
        public override readonly int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "(" + Fmt.I(x, format, formatProvider) + ", " + Fmt.I(y, format, formatProvider) + ")";
    }

    public partial struct Vector3Int : IEquatable<Vector3Int>, IFormattable
    {
        public Vector3Int(int x, int y, int z)
        {
            m_X = x;
            m_Y = y;
            m_Z = z;
        }

        public Vector3Int(int x, int y) : this(x, y, 0) { }

        public int x { readonly get => m_X; set => m_X = value; }
        public int y { readonly get => m_Y; set => m_Y = value; }
        public int z { readonly get => m_Z; set => m_Z = value; }

        public int this[int index]
        {
            readonly get => index switch
            {
                0 => x,
                1 => y,
                2 => z,
                _ => throw new IndexOutOfRangeException("Invalid Vector3Int index addressed: " + index + "!"),
            };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Vector3Int index addressed: " + index + "!");
                }
            }
        }

        public static Vector3Int zero => new(0, 0, 0);
        public static Vector3Int one => new(1, 1, 1);
        public static Vector3Int up => new(0, 1, 0);
        public static Vector3Int down => new(0, -1, 0);
        public static Vector3Int left => new(-1, 0, 0);
        public static Vector3Int right => new(1, 0, 0);
        public static Vector3Int forward => new(0, 0, 1);
        public static Vector3Int back => new(0, 0, -1);

        public readonly float magnitude => MathF.Sqrt(x * x + y * y + z * z);
        public readonly int sqrMagnitude => x * x + y * y + z * z;

        public void Set(int x, int y, int z)
        {
            m_X = x;
            m_Y = y;
            m_Z = z;
        }
        public void Scale(Vector3Int scale)
        {
            m_X *= scale.x;
            m_Y *= scale.y;
            m_Z *= scale.z;
        }
        public void Clamp(Vector3Int min, Vector3Int max)
        {
            x = Math.Min(max.x, Math.Max(min.x, x));
            y = Math.Min(max.y, Math.Max(min.y, y));
            z = Math.Min(max.z, Math.Max(min.z, z));
        }

        public static float Distance(Vector3Int a, Vector3Int b) => (a - b).magnitude;
        public static Vector3Int Min(Vector3Int lhs, Vector3Int rhs) => new(Math.Min(lhs.x, rhs.x), Math.Min(lhs.y, rhs.y), Math.Min(lhs.z, rhs.z));
        public static Vector3Int Max(Vector3Int lhs, Vector3Int rhs) => new(Math.Max(lhs.x, rhs.x), Math.Max(lhs.y, rhs.y), Math.Max(lhs.z, rhs.z));
        public static Vector3Int Scale(Vector3Int a, Vector3Int b) => new(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3Int FloorToInt(Vector3 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));
        public static Vector3Int CeilToInt(Vector3 v) => new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y), Mathf.CeilToInt(v.z));
        public static Vector3Int RoundToInt(Vector3 v) => new(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));

        public static Vector3Int operator -(Vector3Int v) => new(-v.x, -v.y, -v.z);
        public static Vector3Int operator +(Vector3Int a, Vector3Int b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3Int operator -(Vector3Int a, Vector3Int b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3Int operator *(Vector3Int a, Vector3Int b) => new(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3Int operator *(int a, Vector3Int b) => new(a * b.x, a * b.y, a * b.z);
        public static Vector3Int operator *(Vector3Int a, int b) => new(a.x * b, a.y * b, a.z * b);
        public static Vector3Int operator /(Vector3Int a, int b) => new(a.x / b, a.y / b, a.z / b);
        public static bool operator ==(Vector3Int lhs, Vector3Int rhs) => lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z;
        public static bool operator !=(Vector3Int lhs, Vector3Int rhs) => !(lhs == rhs);
        public static implicit operator Vector3(Vector3Int v) => new(v.x, v.y, v.z);
        public static explicit operator Vector2Int(Vector3Int v) => new(v.x, v.y);

        public readonly bool Equals(Vector3Int other) => this == other;
        public override readonly bool Equals(object other) => other is Vector3Int v && Equals(v);
        public override readonly int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 4) ^ (z.GetHashCode() >> 28) ^ (z.GetHashCode() << 4);
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "(" + Fmt.I(x, format, formatProvider) + ", " + Fmt.I(y, format, formatProvider) + ", " + Fmt.I(z, format, formatProvider) + ")";
    }
}
