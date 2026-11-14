using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HelixEngine.Internal;

namespace HelixEngine.Rendering
{
    /// <summary>What LOD selection needs of a camera (ScriptableCullingParameters.lodParameters).</summary>
    public struct LODParameters : IEquatable<LODParameters>
    {
        public bool isOrthographic { get; set; }
        public Vector3 cameraPosition { get; set; }
        public float fieldOfView { get; set; }
        public float orthoSize { get; set; }
        public int cameraPixelHeight { get; set; }

        public bool Equals(LODParameters other) =>
            isOrthographic == other.isOrthographic && cameraPosition.Equals(other.cameraPosition) && fieldOfView.Equals(other.fieldOfView) &&
            orthoSize.Equals(other.orthoSize) && cameraPixelHeight == other.cameraPixelHeight;
        public override bool Equals(object obj) => obj is LODParameters other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(isOrthographic, cameraPosition, fieldOfView, orthoSize, cameraPixelHeight);
        public static bool operator ==(LODParameters left, LODParameters right) => left.Equals(right);
        public static bool operator !=(LODParameters left, LODParameters right) => !left.Equals(right);
    }
}

namespace HelixEngine
{
    /// <summary>A level of a LODGroup: drawn while the group's height on the screen is at least the transition height.</summary>
    public struct LOD
    {
        public float screenRelativeTransitionHeight;
        public float fadeTransitionWidth;
        public Renderer[] renderers;

        public LOD(float screenRelativeTransitionHeight, Renderer[] renderers)
        {
            this.screenRelativeTransitionHeight = screenRelativeTransitionHeight;
            fadeTransitionWidth = 0;
            this.renderers = renderers;
        }
    }

    public sealed partial class LODGroup
    {
        public LOD[] GetLODs()
        {
            var lods = new LOD[lodCount];
            for (int i = 0; i < lods.Length; i++)
            {
                Vector2 values = CullingNative.LODValues(this, i);
                lods[i] = new LOD(values.x, CullingNative.LODRenderers(this, i)) { fadeTransitionWidth = values.y };
            }
            return lods;
        }

        public void SetLODs(LOD[] lods)
        {
            if (lods == null)
                throw new ArgumentNullException(nameof(lods));
            CullingNative.BeginLODs();
            foreach (LOD l in lods)
            {
                CullingNative.AddLOD(l.screenRelativeTransitionHeight, l.fadeTransitionWidth);
                if (l.renderers != null)
                    foreach (Renderer r in l.renderers)
                        CullingNative.AddLODRenderer(r);
            }
            CullingNative.ApplyLODs(this);
        }
    }

    /// <summary>A sphere of a CullingGroup.</summary>
    public struct BoundingSphere
    {
        public Vector3 position;
        public float radius;

        public BoundingSphere(Vector3 pos, float rad)
        {
            position = pos;
            radius = rad;
        }

        public BoundingSphere(Vector4 packedSphere)
        {
            position = new Vector3(packedSphere.x, packedSphere.y, packedSphere.z);
            radius = packedSphere.w;
        }
    }

    /// <summary>A sphere's change: visibility and distance band before and now.</summary>
    public struct CullingGroupEvent
    {
        const byte kIsVisibleMask = 0x80;
        const byte kDistanceMask = 0x7F;

        int m_Index;
        byte m_PrevState;
        byte m_ThisState;

        internal CullingGroupEvent(int index, byte prevState, byte thisState)
        {
            m_Index = index;
            m_PrevState = prevState;
            m_ThisState = thisState;
        }

        public int index => m_Index;
        public bool isVisible => (m_ThisState & kIsVisibleMask) != 0;
        public bool wasVisible => (m_PrevState & kIsVisibleMask) != 0;
        public bool hasBecomeVisible => isVisible && !wasVisible;
        public bool hasBecomeInvisible => !isVisible && wasVisible;
        public int currentDistance => m_ThisState & kDistanceMask;
        public int previousDistance => m_PrevState & kDistanceMask;
    }

    /// <summary>Spheres tested against a camera's view each time it culls; changes reach onStateChanged.</summary>
    public unsafe class CullingGroup : IDisposable
    {
        public delegate void StateChanged(CullingGroupEvent sphere);

        ulong m_Id;
        GCHandle m_Self;
        GCHandle m_Spheres;

        public CullingGroup()
        {
            m_Self = GCHandle.Alloc(this, GCHandleType.Weak);
            m_Id = CullingNative.CreateGroup((ulong)(delegate* unmanaged[Cdecl]<ulong, int, byte, byte, void>)&OnStateChanged,
                                             (ulong)GCHandle.ToIntPtr(m_Self));
        }

        ~CullingGroup()
        {
            if (m_Id != 0)
                Debug.LogError("A CullingGroup was not disposed; call Dispose when it is no longer used.");
        }

        public StateChanged onStateChanged { get; set; }

        public bool enabled
        {
            get => CullingNative.GroupEnabled(Id);
            set => CullingNative.SetGroupEnabled(Id, value);
        }

        public Camera targetCamera
        {
            get => CullingNative.GroupCamera(Id);
            set => CullingNative.SetGroupCamera(Id, value);
        }

        public void Dispose()
        {
            if (m_Id == 0)
                return;
            CullingNative.DisposeGroup(m_Id);
            m_Id = 0;
            if (m_Spheres.IsAllocated)
                m_Spheres.Free();
            if (m_Self.IsAllocated)
                m_Self.Free();
            GC.SuppressFinalize(this);
        }

        /// <summary>The array stays the caller's: the group reads it each time the camera culls.</summary>
        public void SetBoundingSpheres(BoundingSphere[] array)
        {
            if (m_Spheres.IsAllocated)
                m_Spheres.Free();
            ulong address = 0;
            if (array != null)
            {
                m_Spheres = GCHandle.Alloc(array, GCHandleType.Pinned);
                address = (ulong)m_Spheres.AddrOfPinnedObject();
            }
            CullingNative.SetBoundingSpheres(Id, address, array?.Length ?? 0);
        }

        public void SetBoundingSphereCount(int count) => CullingNative.SetBoundingSphereCount(Id, count);

        public void EraseSwapBack(int index) => CullingNative.EraseSwapBack(Id, index);

        public static void EraseSwapBack<T>(int index, T[] myArray, ref int size)
        {
            size--;
            myArray[index] = myArray[size];
        }

        public int QueryIndices(bool visible, int[] result, int firstIndex) => Query(0, visible, 0, result, firstIndex);
        public int QueryIndices(bool visible, int distanceIndex, int[] result, int firstIndex) => Query(1, visible, distanceIndex, result, firstIndex);
        public int QueryIndices(int distanceIndex, int[] result, int firstIndex) => Query(2, false, distanceIndex, result, firstIndex);

        public bool IsVisible(int index) => CullingNative.IsVisible(Id, index);
        public int GetDistance(int index) => CullingNative.GetDistance(Id, index);

        public void SetBoundingDistances(float[] distances) => CullingNative.SetBoundingDistances(Id, distances ?? Array.Empty<float>());
        public void SetDistanceReferencePoint(Vector3 point) => CullingNative.SetReferencePoint(Id, point);
        public void SetDistanceReferencePoint(Transform transform) => CullingNative.SetReferenceTransform(Id, transform);

        ulong Id => m_Id != 0 ? m_Id : throw new ObjectDisposedException(nameof(CullingGroup));

        int Query(int mode, bool visible, int distanceIndex, int[] result, int firstIndex)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            fixed (int* p = result)
                return CullingNative.QueryIndices(Id, mode, visible, distanceIndex, (ulong)p, result.Length, firstIndex);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void OnStateChanged(ulong self, int index, byte prevState, byte thisState)
        {
            try
            {
                if (GCHandle.FromIntPtr((IntPtr)self).Target is CullingGroup group)
                    group.onStateChanged?.Invoke(new CullingGroupEvent(index, prevState, thisState));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
