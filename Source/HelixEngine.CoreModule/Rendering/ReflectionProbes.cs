using System;

namespace HelixEngine.Rendering
{
    /// <summary>A reflection probe a renderer reflects, and its share (Renderer.GetClosestReflectionProbes).</summary>
    public struct ReflectionProbeBlendInfo : IEquatable<ReflectionProbeBlendInfo>
    {
        public ReflectionProbe probe;
        public float weight;

        public bool Equals(ReflectionProbeBlendInfo other) => probe == other.probe && weight.Equals(other.weight);
        public override bool Equals(object obj) => obj is ReflectionProbeBlendInfo other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(probe, weight);
        public static bool operator ==(ReflectionProbeBlendInfo left, ReflectionProbeBlendInfo right) => left.Equals(right);
        public static bool operator !=(ReflectionProbeBlendInfo left, ReflectionProbeBlendInfo right) => !left.Equals(right);
    }
}
