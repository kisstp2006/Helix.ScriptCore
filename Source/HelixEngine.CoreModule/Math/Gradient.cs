using System;

namespace HelixEngine
{
    public enum GradientMode
    {
        Blend = 0,
        Fixed = 1,
        PerceptualBlend = 2,
    }

    public struct GradientColorKey
    {
        public Color color;
        public float time;

        public GradientColorKey(Color col, float time)
        {
            color = col;
            this.time = time;
        }
    }

    public struct GradientAlphaKey
    {
        public float alpha;
        public float time;

        public GradientAlphaKey(float alpha, float time)
        {
            this.alpha = alpha;
            this.time = time;
        }
    }

    // Colors along 0..1: color keys and alpha keys blended (or held) between them.
    public class Gradient : IEquatable<Gradient>
    {
        GradientColorKey[] m_ColorKeys = { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) };
        GradientAlphaKey[] m_AlphaKeys = { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) };

        public GradientMode mode { get; set; } = GradientMode.Blend;

        public GradientColorKey[] colorKeys
        {
            get => (GradientColorKey[])m_ColorKeys.Clone();
            set => m_ColorKeys = Sorted(value, k => k.time);
        }

        public GradientAlphaKey[] alphaKeys
        {
            get => (GradientAlphaKey[])m_AlphaKeys.Clone();
            set => m_AlphaKeys = Sorted(value, k => k.time);
        }

        static T[] Sorted<T>(T[] keys, Func<T, float> time)
        {
            if (keys == null || keys.Length == 0)
                throw new ArgumentException("A gradient needs at least one key.");
            if (keys.Length > 8)
                throw new ArgumentException("A gradient takes at most 8 keys of each kind.");
            var copy = (T[])keys.Clone();
            Array.Sort(copy, (a, b) => time(a).CompareTo(time(b)));
            return copy;
        }

        public void SetKeys(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys)
        {
            this.colorKeys = colorKeys;
            this.alphaKeys = alphaKeys;
        }

        public Color Evaluate(float time)
        {
            time = Math.Clamp(time, 0, 1);
            Color c = Sample(m_ColorKeys, time, k => k.time, k => k.color, mode == GradientMode.PerceptualBlend ? PerceptualLerp : (a, b, t) => Color.Lerp(a, b, t));
            float alpha = Sample(m_AlphaKeys, time, k => k.time, k => k.alpha, (a, b, t) => a + (b - a) * t);
            return new Color(c.r, c.g, c.b, alpha);
        }

        TValue Sample<TKey, TValue>(TKey[] keys, float time, Func<TKey, float> at, Func<TKey, TValue> value, Func<TValue, TValue, float, TValue> lerp)
        {
            if (time <= at(keys[0]))
                return value(keys[0]);
            for (int i = 1; i < keys.Length; i++)
                if (time <= at(keys[i]))
                {
                    if (mode == GradientMode.Fixed)
                        return value(keys[i]);
                    float span = at(keys[i]) - at(keys[i - 1]);
                    float t = span > 0 ? (time - at(keys[i - 1])) / span : 1;
                    return lerp(value(keys[i - 1]), value(keys[i]), t);
                }
            return value(keys[keys.Length - 1]);
        }

        // PerceptualBlend: between the keys in Oklab (of the colors made linear), back to the colors' own space.
        static Color PerceptualLerp(Color a, Color b, float t)
        {
            Vector3 x = ToOklab(a), y = ToOklab(b);
            return FromOklab(x + (y - x) * t);
        }

        static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        static float ToGamma(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1 / 2.4f) - 0.055f;

        static Vector3 ToOklab(Color c)
        {
            float r = ToLinear(c.r), g = ToLinear(c.g), b = ToLinear(c.b);
            float l = MathF.Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b);
            float m = MathF.Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b);
            float s = MathF.Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b);
            return new Vector3(0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
                               1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
                               0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
        }

        static Color FromOklab(Vector3 lab)
        {
            float l = lab.x + 0.3963377774f * lab.y + 0.2158037573f * lab.z;
            float m = lab.x - 0.1055613458f * lab.y - 0.0638541728f * lab.z;
            float s = lab.x - 0.0894841775f * lab.y - 1.2914855480f * lab.z;
            l = l * l * l;
            m = m * m * m;
            s = s * s * s;
            float r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
            float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
            float b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
            return new Color(ToGamma(Math.Clamp(r, 0, 1)), ToGamma(Math.Clamp(g, 0, 1)), ToGamma(Math.Clamp(b, 0, 1)), 1);
        }

        public bool Equals(Gradient other)
        {
            if (other is null || other.mode != mode || other.m_ColorKeys.Length != m_ColorKeys.Length || other.m_AlphaKeys.Length != m_AlphaKeys.Length)
                return false;
            for (int i = 0; i < m_ColorKeys.Length; i++)
                if (m_ColorKeys[i].color != other.m_ColorKeys[i].color || m_ColorKeys[i].time != other.m_ColorKeys[i].time)
                    return false;
            for (int i = 0; i < m_AlphaKeys.Length; i++)
                if (m_AlphaKeys[i].alpha != other.m_AlphaKeys[i].alpha || m_AlphaKeys[i].time != other.m_AlphaKeys[i].time)
                    return false;
            return true;
        }

        public override bool Equals(object o) => o is Gradient g && Equals(g);
        public override int GetHashCode() => HashCode.Combine(mode, m_ColorKeys.Length, m_AlphaKeys.Length);
    }
}
