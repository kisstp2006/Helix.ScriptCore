using System;

namespace HelixEngine.Rendering
{
    /// <summary>
    /// Lighting of every direction as nine spherical harmonics coefficients per color channel (bands 0 to 2): the
    /// engine's SphericalHarmonicsL2 (coefficient i weighs the real basis function Y_i: 0 Y00; 1, 2, 3 y, z, x; 4 to 8
    /// xy, yz, 3z^2 - 1, xz, x^2 - y^2). Lights added are convolved with the cosine lobe: Evaluate gives a surface's
    /// diffuse light.
    /// </summary>
    public struct SphericalHarmonicsL2 : IEquatable<SphericalHarmonicsL2>
    {
        // The coefficients in the reference's order (red, green, blue nine each), reached by the indexer.
#pragma warning disable CS0169
        float shr0, shr1, shr2, shr3, shr4, shr5, shr6, shr7, shr8;
        float shg0, shg1, shg2, shg3, shg4, shg5, shg6, shg7, shg8;
        float shb0, shb1, shb2, shb3, shb4, shb5, shb6, shb7, shb8;
#pragma warning restore CS0169

        const float kY0 = 0.282094792f, kY1 = 0.488602512f, kY2 = 1.092548431f, kY20 = 0.315391565f, kY22 = 0.546274215f;
        static readonly float[] s_Band = { 1.0f, 2.0f / 3.0f, 2.0f / 3.0f, 2.0f / 3.0f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f };

        public unsafe float this[int rgb, int coefficient]
        {
            get
            {
                Check(rgb, coefficient);
                fixed (float* f = &shr0)
                    return f[rgb * 9 + coefficient];
            }
            set
            {
                Check(rgb, coefficient);
                fixed (float* f = &shr0)
                    f[rgb * 9 + coefficient] = value;
            }
        }

        static void Check(int rgb, int coefficient)
        {
            if (rgb < 0 || rgb > 2)
                throw new IndexOutOfRangeException("Invalid RGB index: " + rgb);
            if (coefficient < 0 || coefficient > 8)
                throw new IndexOutOfRangeException("Invalid coefficient index: " + coefficient);
        }

        public void Clear() => this = default;

        /// <summary>The same light from every direction.</summary>
        public void AddAmbientLight(Color color)
        {
            shr0 += color.r / kY0;
            shg0 += color.g / kY0;
            shb0 += color.b / kY0;
        }

        /// <summary>A directional light coming from `direction` (toward the light); a surface facing it gets about the
        /// color times the intensity.</summary>
        public void AddDirectionalLight(Vector3 direction, Color color, float intensity)
        {
            float[] b = Basis(direction.normalized);
            float[] rgb = { color.r * intensity, color.g * intensity, color.b * intensity };
            for (int c = 0; c < 3; c++)
                for (int i = 0; i < 9; i++)
                    this[c, i] += rgb[c] * b[i] * s_Band[i] * MathF.PI;
        }

        /// <summary>The light of each direction.</summary>
        public void Evaluate(Vector3[] directions, Color[] results)
        {
            if (directions == null)
                throw new ArgumentNullException(nameof(directions));
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (directions.Length == 0)
                return;
            if (directions.Length != results.Length)
                throw new ArgumentException("Length of the directions array and the results array must match.");
            for (int k = 0; k < directions.Length; k++)
            {
                float[] b = Basis(directions[k]);
                float r = 0, g = 0, bl = 0;
                for (int i = 0; i < 9; i++)
                {
                    r += this[0, i] * b[i];
                    g += this[1, i] * b[i];
                    bl += this[2, i] * b[i];
                }
                results[k] = new Color(r, g, bl, 1);
            }
        }

        static float[] Basis(Vector3 n) => new[]
        {
            kY0, kY1 * n.y, kY1 * n.z, kY1 * n.x, kY2 * n.x * n.y, kY2 * n.y * n.z, kY20 * (3.0f * n.z * n.z - 1.0f), kY2 * n.x * n.z, kY22 * (n.x * n.x - n.y * n.y),
        };

        public static SphericalHarmonicsL2 operator *(SphericalHarmonicsL2 lhs, float rhs)
        {
            var r = new SphericalHarmonicsL2();
            for (int c = 0; c < 3; c++)
                for (int i = 0; i < 9; i++)
                    r[c, i] = lhs[c, i] * rhs;
            return r;
        }

        public static SphericalHarmonicsL2 operator *(float lhs, SphericalHarmonicsL2 rhs) => rhs * lhs;

        public static SphericalHarmonicsL2 operator +(SphericalHarmonicsL2 lhs, SphericalHarmonicsL2 rhs)
        {
            var r = new SphericalHarmonicsL2();
            for (int c = 0; c < 3; c++)
                for (int i = 0; i < 9; i++)
                    r[c, i] = lhs[c, i] + rhs[c, i];
            return r;
        }

        public static bool operator ==(SphericalHarmonicsL2 lhs, SphericalHarmonicsL2 rhs) => lhs.Equals(rhs);
        public static bool operator !=(SphericalHarmonicsL2 lhs, SphericalHarmonicsL2 rhs) => !lhs.Equals(rhs);

        public bool Equals(SphericalHarmonicsL2 other)
        {
            for (int c = 0; c < 3; c++)
                for (int i = 0; i < 9; i++)
                    if (this[c, i] != other[c, i])
                        return false;
            return true;
        }

        public override bool Equals(object other) => other is SphericalHarmonicsL2 sh && Equals(sh);

        public override int GetHashCode()
        {
            var h = new HashCode();
            for (int c = 0; c < 3; c++)
                for (int i = 0; i < 9; i++)
                    h.Add(this[c, i]);
            return h.ToHashCode();
        }

        internal float[] ToArray()
        {
            var a = new float[27];
            for (int k = 0; k < 27; k++)
                a[k] = this[k / 9, k % 9];
            return a;
        }

        internal static SphericalHarmonicsL2 FromArray(float[] a)
        {
            var sh = new SphericalHarmonicsL2();
            for (int k = 0; a != null && k < 27 && k < a.Length; k++)
                sh[k / 9, k % 9] = a[k];
            return sh;
        }
    }
}

namespace HelixEngine
{
    public static partial class RenderSettings
    {
        /// <summary>The ambient light the shaders read: made from the colors in the Flat and Trilight modes, as set otherwise.</summary>
        public static Rendering.SphericalHarmonicsL2 ambientProbe
        {
            get => Rendering.SphericalHarmonicsL2.FromArray(ambientProbeCoefficients);
            set => ambientProbeCoefficients = value.ToArray();
        }
    }
}
