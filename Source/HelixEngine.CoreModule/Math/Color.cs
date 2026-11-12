using System;
using HelixEngine.Internal;

namespace HelixEngine
{
    // Colors: r, g, b, a in 0..1 for LDR; == approximately equal (as Vector4), Equals exact; "RGBA(r, g, b, a)" with
    // three decimals. HSV and the color spaces are Helix.Math's.
    public partial struct Color : IEquatable<Color>, IFormattable
    {
        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public Color(float r, float g, float b) : this(r, g, b, 1) { }

        public float this[int index]
        {
            readonly get => index switch
            {
                0 => r,
                1 => g,
                2 => b,
                3 => a,
                _ => throw new IndexOutOfRangeException("Invalid Color index(" + index + ")!"),
            };
            set
            {
                switch (index)
                {
                    case 0: r = value; break;
                    case 1: g = value; break;
                    case 2: b = value; break;
                    case 3: a = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Color index(" + index + ")!");
                }
            }
        }

        public static Color aliceBlue => new(0.9411765f, 0.9725491f, 1, 1);
        public static Color antiqueWhite => new(0.9803922f, 0.9215687f, 0.8431373f, 1);
        public static Color aquamarine => new(0.4980392f, 1, 0.8313726f, 1);
        public static Color azure => new(0.9411765f, 1, 1, 1);
        public static Color beige => new(0.9607844f, 0.9607844f, 0.8627452f, 1);
        public static Color bisque => new(1, 0.8941177f, 0.7686275f, 1);
        public static Color black => new(0, 0, 0, 1);
        public static Color blanchedAlmond => new(1, 0.9215687f, 0.8039216f, 1);
        public static Color blue => new(0, 0, 1, 1);
        public static Color blueViolet => new(0.5411765f, 0.1686275f, 0.8862746f, 1);
        public static Color brown => new(0.6470588f, 0.1647059f, 0.1647059f, 1);
        public static Color burlywood => new(0.8705883f, 0.7215686f, 0.5294118f, 1);
        public static Color cadetBlue => new(0.372549f, 0.6196079f, 0.627451f, 1);
        public static Color chartreuse => new(0.4980392f, 1, 0, 1);
        public static Color chocolate => new(0.8235295f, 0.4117647f, 0.1176471f, 1);
        public static Color clear => new(0, 0, 0, 0);
        public static Color coral => new(1, 0.4980392f, 0.3137255f, 1);
        public static Color cornflowerBlue => new(0.3921569f, 0.5843138f, 0.9294118f, 1);
        public static Color cornsilk => new(1, 0.9725491f, 0.8627452f, 1);
        public static Color crimson => new(0.8627452f, 0.07843138f, 0.2352941f, 1);
        public static Color cyan => new(0, 1, 1, 1);
        public static Color darkBlue => new(0, 0, 0.5450981f, 1);
        public static Color darkCyan => new(0, 0.5450981f, 0.5450981f, 1);
        public static Color darkGoldenRod => new(0.7215686f, 0.5254902f, 0.04313726f, 1);
        public static Color darkGray => new(0.6627451f, 0.6627451f, 0.6627451f, 1);
        public static Color darkGreen => new(0, 0.3921569f, 0, 1);
        public static Color darkKhaki => new(0.7411765f, 0.7176471f, 0.4196079f, 1);
        public static Color darkMagenta => new(0.5450981f, 0, 0.5450981f, 1);
        public static Color darkOliveGreen => new(0.3333333f, 0.4196079f, 0.1843137f, 1);
        public static Color darkOrange => new(1, 0.5490196f, 0, 1);
        public static Color darkOrchid => new(0.6f, 0.1960784f, 0.8000001f, 1);
        public static Color darkRed => new(0.5450981f, 0, 0, 1);
        public static Color darkSalmon => new(0.9137256f, 0.5882353f, 0.4784314f, 1);
        public static Color darkSeaGreen => new(0.5607843f, 0.7372549f, 0.5607843f, 1);
        public static Color darkSlateBlue => new(0.282353f, 0.2392157f, 0.5450981f, 1);
        public static Color darkSlateGray => new(0.1843137f, 0.3098039f, 0.3098039f, 1);
        public static Color darkTurquoise => new(0, 0.8078432f, 0.8196079f, 1);
        public static Color darkViolet => new(0.5803922f, 0, 0.8274511f, 1);
        public static Color deepPink => new(1, 0.07843138f, 0.5764706f, 1);
        public static Color deepSkyBlue => new(0, 0.7490196f, 1, 1);
        public static Color dimGray => new(0.4117647f, 0.4117647f, 0.4117647f, 1);
        public static Color dodgerBlue => new(0.1176471f, 0.5647059f, 1, 1);
        public static Color firebrick => new(0.6980392f, 0.1333333f, 0.1333333f, 1);
        public static Color floralWhite => new(1, 0.9803922f, 0.9411765f, 1);
        public static Color forestGreen => new(0.1333333f, 0.5450981f, 0.1333333f, 1);
        public static Color gainsboro => new(0.8627452f, 0.8627452f, 0.8627452f, 1);
        public static Color ghostWhite => new(0.9725491f, 0.9725491f, 1, 1);
        public static Color gold => new(1, 0.8431373f, 0, 1);
        public static Color goldenRod => new(0.854902f, 0.6470588f, 0.1254902f, 1);
        public static Color gray => new(0.5f, 0.5f, 0.5f, 1);
        public static Color gray1 => new(0.1f, 0.1f, 0.1f, 1);
        public static Color gray2 => new(0.2f, 0.2f, 0.2f, 1);
        public static Color gray3 => new(0.3f, 0.3f, 0.3f, 1);
        public static Color gray4 => new(0.4f, 0.4f, 0.4f, 1);
        public static Color gray5 => new(0.5f, 0.5f, 0.5f, 1);
        public static Color gray6 => new(0.6f, 0.6f, 0.6f, 1);
        public static Color gray7 => new(0.7f, 0.7f, 0.7f, 1);
        public static Color gray8 => new(0.8f, 0.8f, 0.8f, 1);
        public static Color gray9 => new(0.9f, 0.9f, 0.9f, 1);
        public static Color green => new(0, 1, 0, 1);
        public static Color greenYellow => new(0.6784314f, 1, 0.1843137f, 1);
        public static Color grey => new(0.5f, 0.5f, 0.5f, 1);
        public static Color honeydew => new(0.9411765f, 1, 0.9411765f, 1);
        public static Color hotPink => new(1, 0.4117647f, 0.7058824f, 1);
        public static Color indianRed => new(0.8039216f, 0.3607843f, 0.3607843f, 1);
        public static Color indigo => new(0.2941177f, 0, 0.509804f, 1);
        public static Color ivory => new(1, 1, 0.9411765f, 1);
        public static Color khaki => new(0.9411765f, 0.9019608f, 0.5490196f, 1);
        public static Color lavender => new(0.9019608f, 0.9019608f, 0.9803922f, 1);
        public static Color lavenderBlush => new(1, 0.9411765f, 0.9607844f, 1);
        public static Color lawnGreen => new(0.4862745f, 0.9882354f, 0, 1);
        public static Color lemonChiffon => new(1, 0.9803922f, 0.8039216f, 1);
        public static Color lightBlue => new(0.6784314f, 0.8470589f, 0.9019608f, 1);
        public static Color lightCoral => new(0.9411765f, 0.5019608f, 0.5019608f, 1);
        public static Color lightCyan => new(0.8784314f, 1, 1, 1);
        public static Color lightGoldenRod => new(0.9333334f, 0.8666667f, 0.509804f, 1);
        public static Color lightGoldenRodYellow => new(0.9803922f, 0.9803922f, 0.8235295f, 1);
        public static Color lightGray => new(0.8274511f, 0.8274511f, 0.8274511f, 1);
        public static Color lightGreen => new(0.5647059f, 0.9333334f, 0.5647059f, 1);
        public static Color lightPink => new(1, 0.7137255f, 0.7568628f, 1);
        public static Color lightSalmon => new(1, 0.627451f, 0.4784314f, 1);
        public static Color lightSeaGreen => new(0.1254902f, 0.6980392f, 0.6666667f, 1);
        public static Color lightSkyBlue => new(0.5294118f, 0.8078432f, 0.9803922f, 1);
        public static Color lightSlateBlue => new(0.5176471f, 0.4392157f, 1, 1);
        public static Color lightSlateGray => new(0.4666667f, 0.5333334f, 0.6f, 1);
        public static Color lightSteelBlue => new(0.6901961f, 0.7686275f, 0.8705883f, 1);
        public static Color lightYellow => new(1, 1, 0.8784314f, 1);
        public static Color limeGreen => new(0.1960784f, 0.8039216f, 0.1960784f, 1);
        public static Color linen => new(0.9803922f, 0.9411765f, 0.9019608f, 1);
        public static Color magenta => new(1, 0, 1, 1);
        public static Color maroon => new(0.6901961f, 0.1882353f, 0.3764706f, 1);
        public static Color mediumAquamarine => new(0.4f, 0.8039216f, 0.6666667f, 1);
        public static Color mediumBlue => new(0, 0, 0.8039216f, 1);
        public static Color mediumOrchid => new(0.7294118f, 0.3333333f, 0.8274511f, 1);
        public static Color mediumPurple => new(0.5764706f, 0.4392157f, 0.8588236f, 1);
        public static Color mediumSeaGreen => new(0.2352941f, 0.7019608f, 0.4431373f, 1);
        public static Color mediumSlateBlue => new(0.482353f, 0.4078432f, 0.9333334f, 1);
        public static Color mediumSpringGreen => new(0, 0.9803922f, 0.6039216f, 1);
        public static Color mediumTurquoise => new(0.282353f, 0.8196079f, 0.8000001f, 1);
        public static Color mediumVioletRed => new(0.7803922f, 0.08235294f, 0.5215687f, 1);
        public static Color midnightBlue => new(0.09803922f, 0.09803922f, 0.4392157f, 1);
        public static Color mintCream => new(0.9607844f, 1, 0.9803922f, 1);
        public static Color mistyRose => new(1, 0.8941177f, 0.882353f, 1);
        public static Color moccasin => new(1, 0.8941177f, 0.7098039f, 1);
        public static Color navajoWhite => new(1, 0.8705883f, 0.6784314f, 1);
        public static Color navyBlue => new(0, 0, 0.5019608f, 1);
        public static Color oldLace => new(0.9921569f, 0.9607844f, 0.9019608f, 1);
        public static Color olive => new(0.5019608f, 0.5019608f, 0, 1);
        public static Color oliveDrab => new(0.4196079f, 0.5568628f, 0.1372549f, 1);
        public static Color orange => new(1, 0.6470588f, 0, 1);
        public static Color orangeRed => new(1, 0.2705882f, 0, 1);
        public static Color orchid => new(0.854902f, 0.4392157f, 0.8392158f, 1);
        public static Color paleGoldenRod => new(0.9333334f, 0.909804f, 0.6666667f, 1);
        public static Color paleGreen => new(0.5960785f, 0.9843138f, 0.5960785f, 1);
        public static Color paleTurquoise => new(0.6862745f, 0.9333334f, 0.9333334f, 1);
        public static Color paleVioletRed => new(0.8588236f, 0.4392157f, 0.5764706f, 1);
        public static Color papayaWhip => new(1, 0.937255f, 0.8352942f, 1);
        public static Color peachPuff => new(1, 0.854902f, 0.7254902f, 1);
        public static Color peru => new(0.8039216f, 0.5215687f, 0.2470588f, 1);
        public static Color pink => new(1, 0.7529413f, 0.7960785f, 1);
        public static Color plum => new(0.8666667f, 0.627451f, 0.8666667f, 1);
        public static Color powderBlue => new(0.6901961f, 0.8784314f, 0.9019608f, 1);
        public static Color purple => new(0.627451f, 0.1254902f, 0.9411765f, 1);
        public static Color rebeccaPurple => new(0.4f, 0.2f, 0.6f, 1);
        public static Color red => new(1, 0, 0, 1);
        public static Color rosyBrown => new(0.7372549f, 0.5607843f, 0.5607843f, 1);
        public static Color royalBlue => new(0.254902f, 0.4117647f, 0.882353f, 1);
        public static Color saddleBrown => new(0.5450981f, 0.2705882f, 0.07450981f, 1);
        public static Color salmon => new(0.9803922f, 0.5019608f, 0.4470589f, 1);
        public static Color sandyBrown => new(0.9568628f, 0.6431373f, 0.3764706f, 1);
        public static Color seaGreen => new(0.1803922f, 0.5450981f, 0.3411765f, 1);
        public static Color seashell => new(1, 0.9607844f, 0.9333334f, 1);
        public static Color sienna => new(0.627451f, 0.3215686f, 0.1764706f, 1);
        public static Color silver => new(0.7529413f, 0.7529413f, 0.7529413f, 1);
        public static Color skyBlue => new(0.5294118f, 0.8078432f, 0.9215687f, 1);
        public static Color slateBlue => new(0.4156863f, 0.3529412f, 0.8039216f, 1);
        public static Color slateGray => new(0.4392157f, 0.5019608f, 0.5647059f, 1);
        public static Color snow => new(1, 0.9803922f, 0.9803922f, 1);
        public static Color softBlue => new(0.1882353f, 0.682353f, 0.7490196f, 1);
        public static Color softGreen => new(0.5490196f, 0.7882354f, 0.1411765f, 1);
        public static Color softRed => new(0.8627452f, 0.1921569f, 0.1960784f, 1);
        public static Color softYellow => new(1, 0.9333334f, 0.5490196f, 1);
        public static Color springGreen => new(0, 1, 0.4980392f, 1);
        public static Color steelBlue => new(0.2745098f, 0.509804f, 0.7058824f, 1);
        public static Color tan => new(0.8235295f, 0.7058824f, 0.5490196f, 1);
        public static Color teal => new(0, 0.5019608f, 0.5019608f, 1);
        public static Color thistle => new(0.8470589f, 0.7490196f, 0.8470589f, 1);
        public static Color tomato => new(1, 0.3882353f, 0.2784314f, 1);
        public static Color turquoise => new(0.2509804f, 0.8784314f, 0.8156863f, 1);
        public static Color violet => new(0.9333334f, 0.509804f, 0.9333334f, 1);
        public static Color violetRed => new(0.8156863f, 0.1254902f, 0.5647059f, 1);
        public static Color wheat => new(0.9607844f, 0.8705883f, 0.7019608f, 1);
        public static Color white => new(1, 1, 1, 1);
        public static Color whiteSmoke => new(0.9607844f, 0.9607844f, 0.9607844f, 1);
        public static Color yellow => new(1, 0.921568632f, 0.0156862754f, 1);
        public static Color yellowGreen => new(0.6039216f, 0.8039216f, 0.1960784f, 1);
        public static Color yellowNice => new(1, 0.921568632f, 0.0156862754f, 1);

        public readonly float grayscale => 0.299f * r + 0.587f * g + 0.114f * b;
        public readonly float maxColorComponent => MathF.Max(MathF.Max(r, g), b);
        public readonly Color linear => new(MathNative.GammaToLinearSpace(r), MathNative.GammaToLinearSpace(g), MathNative.GammaToLinearSpace(b), a);
        public readonly Color gamma => new(MathNative.LinearToGammaSpace(r), MathNative.LinearToGammaSpace(g), MathNative.LinearToGammaSpace(b), a);

        public static Color Lerp(Color a, Color b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Color LerpUnclamped(Color a, Color b, float t) => new(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        public static void RGBToHSV(Color rgbColor, out float H, out float S, out float V)
        {
            H = S = V = 0;
            MathNative.ColorRGBToHSV(rgbColor, ref H, ref S, ref V);
        }
        public static Color HSVToRGB(float H, float S, float V) => MathNative.ColorHSVToRGB(H, S, V, true);
        public static Color HSVToRGB(float H, float S, float V, bool hdr) => MathNative.ColorHSVToRGB(H, S, V, hdr);

        public static Color operator +(Color a, Color b) => new(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static Color operator -(Color a, Color b) => new(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
        public static Color operator *(Color a, Color b) => new(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        public static Color operator *(Color a, float b) => new(a.r * b, a.g * b, a.b * b, a.a * b);
        public static Color operator *(float b, Color a) => new(a.r * b, a.g * b, a.b * b, a.a * b);
        public static Color operator /(Color a, float b) => new(a.r / b, a.g / b, a.b / b, a.a / b);
        public static bool operator ==(Color lhs, Color rhs) => (Vector4)lhs == (Vector4)rhs;
        public static bool operator !=(Color lhs, Color rhs) => !(lhs == rhs);
        public static implicit operator Vector4(Color c) => new(c.r, c.g, c.b, c.a);
        public static implicit operator Color(Vector4 v) => new(v.x, v.y, v.z, v.w);

        public readonly bool Equals(Color other) => r == other.r && g == other.g && b == other.b && a == other.a;
        public override readonly bool Equals(object other) => other is Color c && Equals(c);
        public override readonly int GetHashCode() => ((Vector4)this).GetHashCode();
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "RGBA(" + Fmt.F(r, format, "F3", formatProvider) + ", " + Fmt.F(g, format, "F3", formatProvider) + ", " + Fmt.F(b, format, "F3", formatProvider) + ", " +
            Fmt.F(a, format, "F3", formatProvider) + ")";
    }

    // 8 bit colors; conversions to and from Color round to the nearest step.
    public partial struct Color32 : IEquatable<Color32>, IFormattable
    {
        public Color32(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public byte this[int index]
        {
            readonly get => index switch
            {
                0 => r,
                1 => g,
                2 => b,
                3 => a,
                _ => throw new IndexOutOfRangeException("Invalid Color32 index(" + index + ")!"),
            };
            set
            {
                switch (index)
                {
                    case 0: r = value; break;
                    case 1: g = value; break;
                    case 2: b = value; break;
                    case 3: a = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Color32 index(" + index + ")!");
                }
            }
        }

        public static Color32 Lerp(Color32 a, Color32 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Color32 LerpUnclamped(Color32 a, Color32 b, float t) =>
            new((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), (byte)(a.a + (b.a - a.a) * t));

        public static implicit operator Color32(Color c)
        {
            static byte Step(float v) => (byte)MathF.Round(Mathf.Clamp01(v) * 255);
            return new Color32(Step(c.r), Step(c.g), Step(c.b), Step(c.a));
        }
        public static implicit operator Color(Color32 c) => new(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);

        public readonly bool Equals(Color32 other) => r == other.r && g == other.g && b == other.b && a == other.a;
        public override readonly bool Equals(object other) => other is Color32 c && Equals(c);
        public override readonly int GetHashCode() => r | (g << 8) | (b << 16) | (a << 24);
        public override readonly string ToString() => ToString(null, null);
        public readonly string ToString(string format) => ToString(format, null);
        public readonly string ToString(string format, IFormatProvider formatProvider) =>
            "RGBA(" + Fmt.I(r, format, formatProvider) + ", " + Fmt.I(g, format, formatProvider) + ", " + Fmt.I(b, format, formatProvider) + ", " +
            Fmt.I(a, format, formatProvider) + ")";
    }
}
