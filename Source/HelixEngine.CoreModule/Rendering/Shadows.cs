using System;
using Helix.Collections;

namespace HelixEngine.Rendering
{
    /// <summary>The culling of a shadow split: its sphere (xyz the center, w the radius), its planes (the positive side
    /// inside), the matrix and near plane of its view. Laid out as the native struct.</summary>
    public unsafe struct ShadowSplitData : IEquatable<ShadowSplitData>
    {
        public const int maximumCullingPlaneCount = 10;

        Vector4 m_CullingSphere;
        Matrix4x4 m_CullingMatrix;
        float m_CullingNearPlane;
        float m_ShadowCascadeBlendCullingFactor;
        int m_CullingPlaneCount;
        int m_Reserved;
        fixed float m_CullingPlanes[maximumCullingPlaneCount * 4];

        public Vector4 cullingSphere { get => m_CullingSphere; set => m_CullingSphere = value; }
        public Matrix4x4 cullingMatrix { get => m_CullingMatrix; set => m_CullingMatrix = value; }
        public float cullingNearPlane { get => m_CullingNearPlane; set => m_CullingNearPlane = value; }
        public float shadowCascadeBlendCullingFactor { get => m_ShadowCascadeBlendCullingFactor; set => m_ShadowCascadeBlendCullingFactor = value; }

        public int cullingPlaneCount
        {
            get => m_CullingPlaneCount;
            set
            {
                if (value < 0 || value > maximumCullingPlaneCount)
                    throw new ArgumentOutOfRangeException(nameof(value), $"Value should range from 0 to {maximumCullingPlaneCount}, was {value}");
                m_CullingPlaneCount = value;
            }
        }

        public Plane GetCullingPlane(int index)
        {
            if (index < 0 || index >= cullingPlaneCount)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index should range from 0 to {cullingPlaneCount - 1}, was {index}");
            return new Plane(new Vector3(m_CullingPlanes[index * 4], m_CullingPlanes[index * 4 + 1], m_CullingPlanes[index * 4 + 2]), m_CullingPlanes[index * 4 + 3]);
        }

        public void SetCullingPlane(int index, Plane plane)
        {
            if (index < 0 || index >= cullingPlaneCount)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index should range from 0 to {cullingPlaneCount - 1}, was {index}");
            m_CullingPlanes[index * 4] = plane.normal.x;
            m_CullingPlanes[index * 4 + 1] = plane.normal.y;
            m_CullingPlanes[index * 4 + 2] = plane.normal.z;
            m_CullingPlanes[index * 4 + 3] = plane.distance;
        }

        public bool Equals(ShadowSplitData other)
        {
            if (m_CullingSphere != other.m_CullingSphere || m_CullingMatrix != other.m_CullingMatrix || m_CullingNearPlane != other.m_CullingNearPlane ||
                m_ShadowCascadeBlendCullingFactor != other.m_ShadowCascadeBlendCullingFactor || m_CullingPlaneCount != other.m_CullingPlaneCount)
                return false;
            for (int i = 0; i < m_CullingPlaneCount * 4; i++)
                if (m_CullingPlanes[i] != other.m_CullingPlanes[i])
                    return false;
            return true;
        }

        public override bool Equals(object obj) => obj is ShadowSplitData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_CullingSphere, m_CullingMatrix, m_CullingNearPlane, m_CullingPlaneCount);
        public static bool operator ==(ShadowSplitData left, ShadowSplitData right) => left.Equals(right);
        public static bool operator !=(ShadowSplitData left, ShadowSplitData right) => !left.Equals(right);
    }

    /// <summary>The splits of a light in a ShadowCastersCullingInfos' split buffer; a split whose bit is set is not culled.
    /// Laid out as the native struct.</summary>
    public struct LightShadowCasterCullingInfo : IEquatable<LightShadowCasterCullingInfo>
    {
        public RangeInt splitRange;
        public BatchCullingProjectionType projectionType;
        public ushort splitExclusionMask;
#pragma warning disable CS0169 // The native struct's padding.
        ushort m_Reserved;
#pragma warning restore CS0169

        public bool Equals(LightShadowCasterCullingInfo other) =>
            splitRange.start == other.splitRange.start && splitRange.length == other.splitRange.length && projectionType == other.projectionType &&
            splitExclusionMask == other.splitExclusionMask;

        public override bool Equals(object obj) => obj is LightShadowCasterCullingInfo other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(splitRange.start, splitRange.length, projectionType, splitExclusionMask);
    }

    /// <summary>Per visible light (by index) its splits.</summary>
    public struct ShadowCastersCullingInfos : IEquatable<ShadowCastersCullingInfos>
    {
        public NativeArray<LightShadowCasterCullingInfo> perLightInfos;
        public NativeArray<ShadowSplitData> splitBuffer;

        public bool Equals(ShadowCastersCullingInfos other) => perLightInfos.Equals(other.perLightInfos) && splitBuffer.Equals(other.splitBuffer);
        public override bool Equals(object obj) => obj is ShadowCastersCullingInfos other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(perLightInfos.Length, splitBuffer.Length);
    }

    /// <summary>A renderer list of a visible light's shadow casters (those CullShadowCasters kept for the split).</summary>
    public struct ShadowDrawingSettings : IEquatable<ShadowDrawingSettings>
    {
        public CullingResults cullingResults;
        public int lightIndex;
        public int splitIndex;
        public ShadowObjectsFilter objectsFilter;
        public uint batchLayerMask;
        public bool useRenderingLayerMaskTest;
        internal BatchCullingProjectionType projectionType;

        public ShadowDrawingSettings(CullingResults cullingResults, int lightIndex, BatchCullingProjectionType projectionType = BatchCullingProjectionType.Unknown)
        {
            this.cullingResults = cullingResults;
            this.lightIndex = lightIndex;
            splitIndex = 0;
            objectsFilter = ShadowObjectsFilter.AllObjects;
            batchLayerMask = uint.MaxValue;
            useRenderingLayerMaskTest = false;
            this.projectionType = projectionType;
        }

        public bool Equals(ShadowDrawingSettings other) =>
            cullingResults == other.cullingResults && lightIndex == other.lightIndex && splitIndex == other.splitIndex && objectsFilter == other.objectsFilter &&
            batchLayerMask == other.batchLayerMask && useRenderingLayerMaskTest == other.useRenderingLayerMaskTest;

        public override bool Equals(object obj) => obj is ShadowDrawingSettings other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(cullingResults, lightIndex, splitIndex, objectsFilter, batchLayerMask);
        public static bool operator ==(ShadowDrawingSettings left, ShadowDrawingSettings right) => left.Equals(right);
        public static bool operator !=(ShadowDrawingSettings left, ShadowDrawingSettings right) => !left.Equals(right);
    }
}
