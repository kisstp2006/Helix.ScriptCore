using System;
using HelixEngine.Internal;

namespace HelixEngine
{
    // A 4x4 matrix stored by columns (m<row><column>, index = row + column * 4); products in C#, inverses, TRS and
    // projections Helix.Math's.
    public partial struct Matrix4x4 : IEquatable<Matrix4x4>, IFormattable
    {
        public Matrix4x4(Vector4 column0, Vector4 column1, Vector4 column2, Vector4 column3)
        {
            m00 = column0.x; m10 = column0.y; m20 = column0.z; m30 = column0.w;
            m01 = column1.x; m11 = column1.y; m21 = column1.z; m31 = column1.w;
            m02 = column2.x; m12 = column2.y; m22 = column2.z; m32 = column2.w;
            m03 = column3.x; m13 = column3.y; m23 = column3.z; m33 = column3.w;
        }

        public float this[int row, int column]
        {
            readonly get => this[row + column * 4];
            set => this[row + column * 4] = value;
        }

        public float this[int index]
        {
            readonly get
            {
                if ((uint)index > 15)
                    throw new IndexOutOfRangeException("Invalid matrix index!");
                return System.Runtime.CompilerServices.Unsafe.Add(ref System.Runtime.CompilerServices.Unsafe.AsRef(in m00), index);
            }
            set
            {
                if ((uint)index > 15)
                    throw new IndexOutOfRangeException("Invalid matrix index!");
                System.Runtime.CompilerServices.Unsafe.Add(ref m00, index) = value;
            }
        }

        public static Matrix4x4 zero => default;
        public static Matrix4x4 identity => new(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0, 0, 0, 1));

        public readonly Matrix4x4 inverse => MathNative.MatrixInverse(this);
        public readonly float determinant => MathNative.MatrixDeterminant(this);
        public readonly Matrix4x4 transpose
        {
            get
            {
                Matrix4x4 r = default;
                for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 4; column++)
                        r[row, column] = this[column, row];
                return r;
            }
        }
        public readonly bool isIdentity => this == identity;
        public readonly Quaternion rotation => MathNative.MatrixRotation(this);
        public readonly Vector3 lossyScale => MathNative.MatrixLossyScale(this);
        public readonly FrustumPlanes decomposeProjection => MathNative.MatrixDecomposeProjection(this);

        public readonly Vector4 GetColumn(int index) => new(this[0, index], this[1, index], this[2, index], this[3, index]);
        public readonly Vector4 GetRow(int index) => new(this[index, 0], this[index, 1], this[index, 2], this[index, 3]);
        public readonly Vector3 GetPosition() => new(m03, m13, m23);
        public void SetColumn(int index, Vector4 column)
        {
            this[0, index] = column.x;
            this[1, index] = column.y;
            this[2, index] = column.z;
            this[3, index] = column.w;
        }
        public void SetRow(int index, Vector4 row)
        {
            this[index, 0] = row.x;
            this[index, 1] = row.y;
            this[index, 2] = row.z;
            this[index, 3] = row.w;
        }
        public void SetTRS(Vector3 pos, Quaternion q, Vector3 s) => this = TRS(pos, q, s);
        public readonly bool ValidTRS() => MathNative.MatrixValidTRS(this);
        public readonly Plane TransformPlane(Plane plane) => MathNative.MatrixTransformPlane(this, plane);

        public readonly Vector3 MultiplyPoint(Vector3 point)
        {
            Vector4 r = this * new Vector4(point.x, point.y, point.z, 1);
            float w = 1 / r.w;
            return new Vector3(r.x * w, r.y * w, r.z * w);
        }
        public readonly Vector3 MultiplyPoint3x4(Vector3 point) =>
            new(m00 * point.x + m01 * point.y + m02 * point.z + m03, m10 * point.x + m11 * point.y + m12 * point.z + m13,
                m20 * point.x + m21 * point.y + m22 * point.z + m23);
        public readonly Vector3 MultiplyVector(Vector3 vector) =>
            new(m00 * vector.x + m01 * vector.y + m02 * vector.z, m10 * vector.x + m11 * vector.y + m12 * vector.z,
                m20 * vector.x + m21 * vector.y + m22 * vector.z);

        public static Matrix4x4 Translate(Vector3 vector)
        {
            Matrix4x4 m = identity;
            m.m03 = vector.x;
            m.m13 = vector.y;
            m.m23 = vector.z;
            return m;
        }
        public static Matrix4x4 Rotate(Quaternion q) => MathNative.MatrixRotate(q);
        public static Matrix4x4 Scale(Vector3 vector)
        {
            Matrix4x4 m = default;
            m.m00 = vector.x;
            m.m11 = vector.y;
            m.m22 = vector.z;
            m.m33 = 1;
            return m;
        }
        public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s) => MathNative.MatrixTRS(pos, q, s);
        public static Matrix4x4 Perspective(float fov, float aspect, float zNear, float zFar) => MathNative.MatrixPerspective(fov, aspect, zNear, zFar);
        public static Matrix4x4 Ortho(float left, float right, float bottom, float top, float zNear, float zFar) =>
            MathNative.MatrixOrtho(left, right, bottom, top, zNear, zFar);
        public static Matrix4x4 Frustum(float left, float right, float bottom, float top, float zNear, float zFar) =>
            MathNative.MatrixFrustum(left, right, bottom, top, zNear, zFar);
        public static Matrix4x4 Frustum(FrustumPlanes fp) => Frustum(fp.left, fp.right, fp.bottom, fp.top, fp.zNear, fp.zFar);
        public static Matrix4x4 LookAt(Vector3 from, Vector3 to, Vector3 up) => MathNative.MatrixLookAt(from, to, up);
        public static Matrix4x4 Inverse(Matrix4x4 m) => m.inverse;
        public static Matrix4x4 Transpose(Matrix4x4 m) => m.transpose;
        public static float Determinant(Matrix4x4 m) => m.determinant;
        public static bool Inverse3DAffine(Matrix4x4 input, ref Matrix4x4 result) => MathNative.MatrixInverse3DAffine(input, ref result);

        public static Matrix4x4 operator *(Matrix4x4 lhs, Matrix4x4 rhs)
        {
            Matrix4x4 r = default;
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    r[row, column] = lhs[row, 0] * rhs[0, column] + lhs[row, 1] * rhs[1, column] + lhs[row, 2] * rhs[2, column] + lhs[row, 3] * rhs[3, column];
            return r;
        }

        public static Vector4 operator *(Matrix4x4 lhs, Vector4 vector) =>
            new(lhs.m00 * vector.x + lhs.m01 * vector.y + lhs.m02 * vector.z + lhs.m03 * vector.w,
                lhs.m10 * vector.x + lhs.m11 * vector.y + lhs.m12 * vector.z + lhs.m13 * vector.w,
                lhs.m20 * vector.x + lhs.m21 * vector.y + lhs.m22 * vector.z + lhs.m23 * vector.w,
                lhs.m30 * vector.x + lhs.m31 * vector.y + lhs.m32 * vector.z + lhs.m33 * vector.w);

        public static bool operator ==(Matrix4x4 lhs, Matrix4x4 rhs) =>
            lhs.GetColumn(0) == rhs.GetColumn(0) && lhs.GetColumn(1) == rhs.GetColumn(1) && lhs.GetColumn(2) == rhs.GetColumn(2) &&
            lhs.GetColumn(3) == rhs.GetColumn(3);
        public static bool operator !=(Matrix4x4 lhs, Matrix4x4 rhs) => !(lhs == rhs);

        public readonly bool Equals(Matrix4x4 other) =>
            GetColumn(0).Equals(other.GetColumn(0)) && GetColumn(1).Equals(other.GetColumn(1)) && GetColumn(2).Equals(other.GetColumn(2)) &&
            GetColumn(3).Equals(other.GetColumn(3));
        public override readonly bool Equals(object other) => other is Matrix4x4 m && Equals(m);
        public override readonly int GetHashCode() =>
            GetColumn(0).GetHashCode() ^ (GetColumn(1).GetHashCode() << 2) ^ (GetColumn(2).GetHashCode() >> 2) ^ (GetColumn(3).GetHashCode() >> 1);
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        // One row per line, tab separated, five decimals unless a format is given.
        public readonly string ToString(string format, IFormatProvider formatProvider)
        {
            var sb = new System.Text.StringBuilder();
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    if (column > 0)
                        sb.Append('\t');
                    sb.Append(Fmt.F(this[row, column], format, "F5", formatProvider));
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
