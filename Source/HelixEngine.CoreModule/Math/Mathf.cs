using System;
using HelixEngine.Internal;

namespace HelixEngine
{
    // The math functions of the reference: C# for the arithmetic (the same formulas as Helix.Math's mathf), Helix.Math
    // for noise, half floats, color temperature, color spaces and SmoothDamp.
    public struct Mathf
    {
        public const float PI = MathF.PI;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = PI * 2f / 360f;
        public const float Rad2Deg = 1f / Deg2Rad;
        // The smallest positive float.
        public static readonly float Epsilon = float.Epsilon;

        public static float Sin(float f) => MathF.Sin(f);
        public static float Cos(float f) => MathF.Cos(f);
        public static float Tan(float f) => MathF.Tan(f);
        public static float Asin(float f) => MathF.Asin(f);
        public static float Acos(float f) => MathF.Acos(f);
        public static float Atan(float f) => MathF.Atan(f);
        public static float Atan2(float y, float x) => MathF.Atan2(y, x);
        public static float Sqrt(float f) => MathF.Sqrt(f);
        public static float Abs(float f) => MathF.Abs(f);
        public static int Abs(int value) => Math.Abs(value);
        public static float Pow(float f, float p) => MathF.Pow(f, p);
        public static float Exp(float power) => MathF.Exp(power);
        public static float Log(float f, float p) => MathF.Log(f) / MathF.Log(p);
        public static float Log(float f) => MathF.Log(f);
        public static float Log10(float f) => MathF.Log10(f);
        public static float Ceil(float f) => MathF.Ceiling(f);
        public static float Floor(float f) => MathF.Floor(f);
        public static float Round(float f) => MathF.Round(f);
        public static int CeilToInt(float f) => (int)MathF.Ceiling(f);
        public static int FloorToInt(float f) => (int)MathF.Floor(f);
        public static int RoundToInt(float f) => (int)MathF.Round(f);
        public static float Sign(float f) => f >= 0f ? 1f : -1f;

        public static float Min(float a, float b) => a < b ? a : b;
        public static float Min(params float[] values)
        {
            if (values.Length == 0)
                return 0;
            float m = values[0];
            for (int i = 1; i < values.Length; i++)
                m = values[i] < m ? values[i] : m;
            return m;
        }
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Min(params int[] values)
        {
            if (values.Length == 0)
                return 0;
            int m = values[0];
            for (int i = 1; i < values.Length; i++)
                m = values[i] < m ? values[i] : m;
            return m;
        }
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(params float[] values)
        {
            if (values.Length == 0)
                return 0;
            float m = values[0];
            for (int i = 1; i < values.Length; i++)
                m = values[i] > m ? values[i] : m;
            return m;
        }
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Max(params int[] values)
        {
            if (values.Length == 0)
                return 0;
            int m = values[0];
            for (int i = 1; i < values.Length; i++)
                m = values[i] > m ? values[i] : m;
            return m;
        }

        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
        public static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float value) => a != b ? Clamp01((value - a) / (b - a)) : 0f;
        public static float LerpAngle(float a, float b, float t)
        {
            float delta = Repeat(b - a, 360);
            if (delta > 180)
                delta -= 360;
            return a + delta * Clamp01(t);
        }
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (MathF.Abs(target - current) <= maxDelta)
                return target;
            return current + Sign(target - current) * maxDelta;
        }
        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float deltaAngle = DeltaAngle(current, target);
            if (-maxDelta < deltaAngle && deltaAngle < maxDelta)
                return target;
            return MoveTowards(current, current + deltaAngle, maxDelta);
        }
        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }
        public static bool Approximately(float a, float b) => MathF.Abs(b - a) < MathF.Max(1e-6f * MathF.Max(MathF.Abs(a), MathF.Abs(b)), Epsilon * 8);
        public static float Repeat(float t, float length) => Clamp(t - MathF.Floor(t / length) * length, 0f, length);
        public static float PingPong(float t, float length)
        {
            t = Repeat(t, length * 2f);
            return length - MathF.Abs(t - length);
        }
        public static float DeltaAngle(float current, float target)
        {
            float delta = Repeat(target - current, 360f);
            if (delta > 180f)
                delta -= 360f;
            return delta;
        }

        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime) =>
            SmoothDamp(current, target, ref currentVelocity, smoothTime, Infinity, Time.deltaTime);
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed) =>
            SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime) =>
            MathNative.SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
        public static float SmoothDampAngle(float current, float target, ref float currentVelocity, float smoothTime) =>
            SmoothDampAngle(current, target, ref currentVelocity, smoothTime, Infinity, Time.deltaTime);
        public static float SmoothDampAngle(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed) =>
            SmoothDampAngle(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);
        public static float SmoothDampAngle(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            target = current + DeltaAngle(current, target);
            return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
        }

        public static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
        public static int NextPowerOfTwo(int value)
        {
            if (value <= 0)
                return 0;
            uint u = (uint)value - 1;
            u |= u >> 1;
            u |= u >> 2;
            u |= u >> 4;
            u |= u >> 8;
            u |= u >> 16;
            return (int)(u + 1);
        }
        public static int ClosestPowerOfTwo(int value)
        {
            if (value <= 0)
                return 0;
            int next = NextPowerOfTwo(value);
            int prev = next >> 1;
            return value - prev < next - value ? prev : next;
        }

        public static float GammaToLinearSpace(float value) => MathNative.GammaToLinearSpace(value);
        public static float LinearToGammaSpace(float value) => MathNative.LinearToGammaSpace(value);
        public static Color CorrelatedColorTemperatureToRGB(float kelvin) => MathNative.CorrelatedColorTemperatureToRGB(kelvin);
        public static ushort FloatToHalf(float val) => MathNative.FloatToHalf(val);
        public static float HalfToFloat(ushort val) => MathNative.HalfToFloat(val);
        public static float PerlinNoise(float x, float y) => MathNative.PerlinNoise(x, y);
        public static float PerlinNoise1D(float x) => MathNative.PerlinNoise1D(x);
    }
}
