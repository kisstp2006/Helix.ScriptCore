using System;
using System.Collections;
using System.Collections.Generic;
using HelixEngine.Internal;

namespace HelixEngine
{
    // A 2D rectangle by its corner and size; setting xMin and the other edges moves that edge only.
    public partial struct Rect : IEquatable<Rect>, IFormattable
    {
        public Rect(float x, float y, float width, float height)
        {
            m_XMin = x;
            m_YMin = y;
            m_Width = width;
            m_Height = height;
        }

        public Rect(Vector2 position, Vector2 size) : this(position.x, position.y, size.x, size.y) { }
        public Rect(Rect source) : this(source.m_XMin, source.m_YMin, source.m_Width, source.m_Height) { }

        public static Rect zero => new(0, 0, 0, 0);

        public float x { readonly get => m_XMin; set => m_XMin = value; }
        public float y { readonly get => m_YMin; set => m_YMin = value; }
        public float width { readonly get => m_Width; set => m_Width = value; }
        public float height { readonly get => m_Height; set => m_Height = value; }
        public Vector2 position
        {
            readonly get => new(m_XMin, m_YMin);
            set
            {
                m_XMin = value.x;
                m_YMin = value.y;
            }
        }
        public Vector2 size
        {
            readonly get => new(m_Width, m_Height);
            set
            {
                m_Width = value.x;
                m_Height = value.y;
            }
        }
        public Vector2 center
        {
            readonly get => new(x + m_Width / 2f, y + m_Height / 2f);
            set
            {
                m_XMin = value.x - m_Width / 2f;
                m_YMin = value.y - m_Height / 2f;
            }
        }
        public Vector2 min
        {
            readonly get => new(xMin, yMin);
            set
            {
                xMin = value.x;
                yMin = value.y;
            }
        }
        public Vector2 max
        {
            readonly get => new(xMax, yMax);
            set
            {
                xMax = value.x;
                yMax = value.y;
            }
        }
        public float xMin
        {
            readonly get => m_XMin;
            set
            {
                float oldxmax = xMax;
                m_XMin = value;
                m_Width = oldxmax - m_XMin;
            }
        }
        public float yMin
        {
            readonly get => m_YMin;
            set
            {
                float oldymax = yMax;
                m_YMin = value;
                m_Height = oldymax - m_YMin;
            }
        }
        public float xMax { readonly get => m_Width + m_XMin; set => m_Width = value - m_XMin; }
        public float yMax { readonly get => m_Height + m_YMin; set => m_Height = value - m_YMin; }

        public void Set(float x, float y, float width, float height)
        {
            m_XMin = x;
            m_YMin = y;
            m_Width = width;
            m_Height = height;
        }

        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax) => new(xmin, ymin, xmax - xmin, ymax - ymin);
        public static Vector2 NormalizedToPoint(Rect rectangle, Vector2 normalizedRectCoordinates) =>
            new(Mathf.Lerp(rectangle.x, rectangle.xMax, normalizedRectCoordinates.x), Mathf.Lerp(rectangle.y, rectangle.yMax, normalizedRectCoordinates.y));
        public static Vector2 PointToNormalized(Rect rectangle, Vector2 point) =>
            new(Mathf.InverseLerp(rectangle.x, rectangle.xMax, point.x), Mathf.InverseLerp(rectangle.y, rectangle.yMax, point.y));

        public readonly bool Contains(Vector2 point) => point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;
        public readonly bool Contains(Vector3 point) => point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;
        public readonly bool Contains(Vector3 point, bool allowInverse)
        {
            if (!allowInverse)
                return Contains(point);
            bool xAxis = (width < 0f && point.x <= xMin && point.x > xMax) || (width >= 0f && point.x >= xMin && point.x < xMax);
            bool yAxis = (height < 0f && point.y <= yMin && point.y > yMax) || (height >= 0f && point.y >= yMin && point.y < yMax);
            return xAxis && yAxis;
        }

        static Rect OrderMinMax(Rect rect)
        {
            if (rect.xMin > rect.xMax)
            {
                float temp = rect.xMin;
                rect.xMin = rect.xMax;
                rect.xMax = temp;
            }
            if (rect.yMin > rect.yMax)
            {
                float temp = rect.yMin;
                rect.yMin = rect.yMax;
                rect.yMax = temp;
            }
            return rect;
        }

        public readonly bool Overlaps(Rect other) => other.xMax > xMin && other.xMin < xMax && other.yMax > yMin && other.yMin < yMax;
        public readonly bool Overlaps(Rect other, bool allowInverse)
        {
            Rect self = this;
            if (allowInverse)
            {
                self = OrderMinMax(self);
                other = OrderMinMax(other);
            }
            return self.Overlaps(other);
        }

        public static bool operator ==(Rect lhs, Rect rhs) => lhs.x == rhs.x && lhs.y == rhs.y && lhs.width == rhs.width && lhs.height == rhs.height;
        public static bool operator !=(Rect lhs, Rect rhs) => !(lhs == rhs);

        public readonly bool Equals(Rect other) => this == other;
        public override readonly bool Equals(object other) => other is Rect r && Equals(r);
        public override readonly int GetHashCode() => x.GetHashCode() ^ (width.GetHashCode() << 2) ^ (y.GetHashCode() >> 2) ^ (height.GetHashCode() >> 1);
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "(x:" + Fmt.F(x, format, "F2", formatProvider) + ", y:" + Fmt.F(y, format, "F2", formatProvider) + ", width:" + Fmt.F(width, format, "F2", formatProvider) +
            ", height:" + Fmt.F(height, format, "F2", formatProvider) + ")";
    }

    // A rectangle of cells: xMin inclusive, xMax exclusive (y alike).
    public partial struct RectInt : IEquatable<RectInt>, IFormattable
    {
        public RectInt(int xMin, int yMin, int width, int height)
        {
            m_XMin = xMin;
            m_YMin = yMin;
            m_Width = width;
            m_Height = height;
        }

        public RectInt(Vector2Int position, Vector2Int size) : this(position.x, position.y, size.x, size.y) { }

        public static RectInt zero => new(0, 0, 0, 0);

        public int x { readonly get => m_XMin; set => m_XMin = value; }
        public int y { readonly get => m_YMin; set => m_YMin = value; }
        public int width { readonly get => m_Width; set => m_Width = value; }
        public int height { readonly get => m_Height; set => m_Height = value; }
        public readonly Vector2 center => new(x + m_Width / 2f, y + m_Height / 2f);
        public Vector2Int min
        {
            readonly get => new(xMin, yMin);
            set
            {
                xMin = value.x;
                yMin = value.y;
            }
        }
        public Vector2Int max
        {
            readonly get => new(xMax, yMax);
            set
            {
                xMax = value.x;
                yMax = value.y;
            }
        }
        public Vector2Int position
        {
            readonly get => new(m_XMin, m_YMin);
            set
            {
                m_XMin = value.x;
                m_YMin = value.y;
            }
        }
        public Vector2Int size
        {
            readonly get => new(m_Width, m_Height);
            set
            {
                m_Width = value.x;
                m_Height = value.y;
            }
        }
        public int xMin
        {
            readonly get => Math.Min(m_XMin, m_XMin + m_Width);
            set
            {
                int oldxmax = xMax;
                m_XMin = value;
                m_Width = oldxmax - m_XMin;
            }
        }
        public int yMin
        {
            readonly get => Math.Min(m_YMin, m_YMin + m_Height);
            set
            {
                int oldymax = yMax;
                m_YMin = value;
                m_Height = oldymax - m_YMin;
            }
        }
        public int xMax { readonly get => Math.Max(m_XMin, m_XMin + m_Width); set => m_Width = value - m_XMin; }
        public int yMax { readonly get => Math.Max(m_YMin, m_YMin + m_Height); set => m_Height = value - m_YMin; }

        public void SetMinMax(Vector2Int minPosition, Vector2Int maxPosition)
        {
            min = minPosition;
            max = maxPosition;
        }
        public void ClampToBounds(RectInt bounds)
        {
            position = new Vector2Int(Math.Max(Math.Min(bounds.xMax, position.x), bounds.xMin), Math.Max(Math.Min(bounds.yMax, position.y), bounds.yMin));
            size = new Vector2Int(Math.Min(bounds.xMax - position.x, size.x), Math.Min(bounds.yMax - position.y, size.y));
        }
        public readonly bool Contains(Vector2Int position) => position.x >= xMin && position.y >= yMin && position.x < xMax && position.y < yMax;
        public readonly bool Overlaps(RectInt other) => other.xMin < xMax && other.xMax > xMin && other.yMin < yMax && other.yMax > yMin;

        public readonly PositionEnumerator allPositionsWithin => new(min, max);

        // Every cell within, row by row.
        public struct PositionEnumerator : IEnumerator<Vector2Int>
        {
            readonly Vector2Int _min, _max;
            Vector2Int _current;

            public PositionEnumerator(Vector2Int min, Vector2Int max)
            {
                _min = _current = min;
                _max = max;
                Reset();
            }

            public PositionEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                if (_current.y >= _max.y)
                    return false;
                _current.x++;
                if (_current.x >= _max.x)
                {
                    _current.x = _min.x;
                    if (_current.x >= _max.x)
                        return false;
                    _current.y++;
                    if (_current.y >= _max.y)
                        return false;
                }
                return true;
            }

            public void Reset()
            {
                _current = _min;
                _current.x--;
            }

            public readonly Vector2Int Current => _current;
            readonly object IEnumerator.Current => Current;
            readonly void IDisposable.Dispose() { }
        }

        public readonly bool Equals(RectInt other) => m_XMin == other.m_XMin && m_YMin == other.m_YMin && m_Width == other.m_Width && m_Height == other.m_Height;
        public override readonly bool Equals(object other) => other is RectInt r && Equals(r);
        public override readonly int GetHashCode() => HashCode.Combine(m_XMin, m_YMin, m_Width, m_Height);
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "(x:" + Fmt.I(x, format, formatProvider) + ", y:" + Fmt.I(y, format, formatProvider) + ", width:" + Fmt.I(width, format, formatProvider) + ", height:" +
            Fmt.I(height, format, formatProvider) + ")";
    }

    // An axis aligned box by its center and extents (half the size).
    public partial struct Bounds : IEquatable<Bounds>, IFormattable
    {
        public Bounds(Vector3 center, Vector3 size)
        {
            m_Center = center;
            m_Extents = size * 0.5f;
        }

        public Vector3 center { readonly get => m_Center; set => m_Center = value; }
        public Vector3 size { readonly get => m_Extents * 2f; set => m_Extents = value * 0.5f; }
        public Vector3 extents { readonly get => m_Extents; set => m_Extents = value; }
        public Vector3 min { readonly get => center - extents; set => SetMinMax(value, max); }
        public Vector3 max { readonly get => center + extents; set => SetMinMax(min, value); }

        public void SetMinMax(Vector3 min, Vector3 max)
        {
            extents = (max - min) * 0.5f;
            center = min + extents;
        }
        public void Encapsulate(Vector3 point) => SetMinMax(Vector3.Min(min, point), Vector3.Max(max, point));
        public void Encapsulate(Bounds bounds)
        {
            Encapsulate(bounds.center - bounds.extents);
            Encapsulate(bounds.center + bounds.extents);
        }
        public void Expand(float amount)
        {
            amount *= 0.5f;
            extents += new Vector3(amount, amount, amount);
        }
        public void Expand(Vector3 amount) => extents += amount * 0.5f;
        public readonly bool Intersects(Bounds bounds) =>
            min.x <= bounds.max.x && max.x >= bounds.min.x && min.y <= bounds.max.y && max.y >= bounds.min.y && min.z <= bounds.max.z && max.z >= bounds.min.z;
        public readonly bool Contains(Vector3 point)
        {
            Vector3 mn = min, mx = max;
            return point.x >= mn.x && point.x <= mx.x && point.y >= mn.y && point.y <= mx.y && point.z >= mn.z && point.z <= mx.z;
        }
        public readonly Vector3 ClosestPoint(Vector3 point) => Vector3.Min(Vector3.Max(point, min), max);
        public readonly float SqrDistance(Vector3 point) => (ClosestPoint(point) - point).sqrMagnitude;
        public readonly bool IntersectRay(Ray ray) => IntersectRay(ray, out _);
        public readonly bool IntersectRay(Ray ray, out float distance)
        {
            float tmin = float.NegativeInfinity, tmax = float.PositiveInfinity;
            Vector3 mn = min, mx = max, o = ray.origin, d = ray.direction;
            distance = 0;
            for (int i = 0; i < 3; i++)
            {
                if (MathF.Abs(d[i]) < 1e-8f)
                {
                    if (o[i] < mn[i] || o[i] > mx[i])
                        return false;
                    continue;
                }
                float inv = 1f / d[i];
                float t1 = (mn[i] - o[i]) * inv, t2 = (mx[i] - o[i]) * inv;
                if (t1 > t2)
                    (t1, t2) = (t2, t1);
                tmin = MathF.Max(tmin, t1);
                tmax = MathF.Min(tmax, t2);
                if (tmin > tmax)
                    return false;
            }
            if (tmax < 0f)
                return false;
            distance = tmin > 0f ? tmin : 0f;
            return true;
        }

        public static bool operator ==(Bounds lhs, Bounds rhs) => lhs.center == rhs.center && lhs.extents == rhs.extents;
        public static bool operator !=(Bounds lhs, Bounds rhs) => !(lhs == rhs);

        public readonly bool Equals(Bounds other) => center.Equals(other.center) && extents.Equals(other.extents);
        public override readonly bool Equals(object other) => other is Bounds b && Equals(b);
        public override readonly int GetHashCode() => center.GetHashCode() ^ (extents.GetHashCode() << 2);
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "Center: " + m_Center.ToString(format, formatProvider) + ", Extents: " + m_Extents.ToString(format, formatProvider);
    }

    // A ray: its direction is normalized.
    public partial struct Ray : IFormattable
    {
        public Ray(Vector3 origin, Vector3 direction)
        {
            m_Origin = origin;
            m_Direction = direction.normalized;
        }

        public Vector3 origin { readonly get => m_Origin; set => m_Origin = value; }
        public Vector3 direction { readonly get => m_Direction; set => m_Direction = value.normalized; }

        public readonly Vector3 GetPoint(float distance) => m_Origin + m_Direction * distance;

        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "Origin: " + m_Origin.ToString(format, formatProvider) + ", Dir: " + m_Direction.ToString(format, formatProvider);
    }

    // The points p with Dot(normal, p) + distance == 0.
    public partial struct Plane : IFormattable
    {
        public Plane(Vector3 inNormal, Vector3 inPoint)
        {
            m_Normal = Vector3.Normalize(inNormal);
            m_Distance = -Vector3.Dot(m_Normal, inPoint);
        }

        public Plane(Vector3 inNormal, float d)
        {
            m_Normal = Vector3.Normalize(inNormal);
            m_Distance = d;
        }

        public Plane(Vector3 a, Vector3 b, Vector3 c)
        {
            m_Normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            m_Distance = -Vector3.Dot(m_Normal, a);
        }

        public Vector3 normal { readonly get => m_Normal; set => m_Normal = value; }
        public float distance { readonly get => m_Distance; set => m_Distance = value; }
        public readonly Plane flipped => new(-m_Normal, -m_Distance);

        public void SetNormalAndPosition(Vector3 inNormal, Vector3 inPoint) => this = new Plane(inNormal, inPoint);
        public void Set3Points(Vector3 a, Vector3 b, Vector3 c) => this = new Plane(a, b, c);
        public void Flip()
        {
            m_Normal = -m_Normal;
            m_Distance = -m_Distance;
        }
        public void Translate(Vector3 translation) => m_Distance += Vector3.Dot(m_Normal, translation);
        public static Plane Translate(Plane plane, Vector3 translation) => new(plane.m_Normal, plane.m_Distance + Vector3.Dot(plane.m_Normal, translation));
        public readonly Vector3 ClosestPointOnPlane(Vector3 point) => point - m_Normal * GetDistanceToPoint(point);
        public readonly float GetDistanceToPoint(Vector3 point) => Vector3.Dot(m_Normal, point) + m_Distance;
        public readonly bool GetSide(Vector3 point) => GetDistanceToPoint(point) > 0f;
        public readonly bool SameSide(Vector3 inPt0, Vector3 inPt1)
        {
            float d0 = GetDistanceToPoint(inPt0), d1 = GetDistanceToPoint(inPt1);
            return (d0 > 0f && d1 > 0f) || (d0 <= 0f && d1 <= 0f);
        }
        public readonly bool Raycast(Ray ray, out float enter)
        {
            float vdot = Vector3.Dot(ray.direction, m_Normal);
            float ndot = -Vector3.Dot(ray.origin, m_Normal) - m_Distance;
            if (Mathf.Approximately(vdot, 0f))
            {
                enter = 0f;
                return false;
            }
            enter = ndot / vdot;
            return enter > 0f;
        }

        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "(normal:" + m_Normal.ToString(format, formatProvider) + ", distance:" + Fmt.F(m_Distance, format, "F2", formatProvider) + ")";
    }
}
