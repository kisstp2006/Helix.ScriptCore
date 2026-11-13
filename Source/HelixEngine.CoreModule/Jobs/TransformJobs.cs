using System;
using Helix.Jobs;
using HelixEngine.Internal;

namespace HelixEngine.Jobs
{
    /// <summary>Transforms for IJobParallelForTransform (native memory: dispose it).</summary>
    public struct TransformAccessArray : IDisposable
    {
        internal ulong m_Array;

        public TransformAccessArray(int capacity, int desiredJobCount = -1)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "capacity must be >= 0");
            m_Array = TransformAccessNative.Create(capacity);
        }

        public TransformAccessArray(Transform[] transforms, int desiredJobCount = -1)
        {
            m_Array = TransformAccessNative.Create(transforms?.Length ?? 0);
            SetTransforms(transforms);
        }

        public static void Allocate(int capacity, int desiredJobCount, out TransformAccessArray array) => array = new TransformAccessArray(capacity, desiredJobCount);

        public bool isCreated => m_Array != 0;

        public void Dispose()
        {
            CheckCreated();
            TransformAccessNative.Destroy(m_Array);
            m_Array = 0;
        }

        public int length
        {
            get
            {
                CheckCreated();
                return TransformAccessNative.Length(m_Array);
            }
        }

        public int capacity
        {
            get
            {
                CheckCreated();
                return TransformAccessNative.Capacity(m_Array);
            }
            set
            {
                CheckCreated();
                TransformAccessNative.SetCapacity(m_Array, value);
            }
        }

        public Transform this[int index]
        {
            get
            {
                CheckIndex(index);
                return TransformAccessNative.Get(m_Array, index);
            }
            set
            {
                CheckIndex(index);
                TransformAccessNative.Set(m_Array, index, value);
            }
        }

        public void Add(Transform transform)
        {
            CheckCreated();
            TransformAccessNative.Add(m_Array, transform);
        }

        public void Add(EntityId entityId)
        {
            CheckCreated();
            TransformAccessNative.AddId(m_Array, entityId);
        }

        public void RemoveAtSwapBack(int index)
        {
            CheckIndex(index);
            TransformAccessNative.RemoveAtSwapBack(m_Array, index);
        }

        public void SetTransforms(Transform[] transforms)
        {
            CheckCreated();
            TransformAccessNative.Clear(m_Array);
            if (transforms != null)
                foreach (Transform t in transforms)
                    TransformAccessNative.Add(m_Array, t);
        }

        void CheckCreated()
        {
            if (m_Array == 0)
                throw new InvalidOperationException("TransformAccessArray is not valid: it has not been created or it has been disposed.");
        }

        void CheckIndex(int index)
        {
            CheckCreated();
            if ((uint)index >= (uint)TransformAccessNative.Length(m_Array))
                throw new IndexOutOfRangeException("Index out of range");
        }
    }

    /// <summary>A transform as a job sees it (the transforms of one root are on one worker).</summary>
    public struct TransformAccess
    {
        internal ulong m_Hierarchy;
        internal uint m_Node;

        internal TransformAccess(ulong hierarchy, uint node)
        {
            m_Hierarchy = hierarchy;
            m_Node = node;
        }

        public bool isValid => TransformAccessNative.IsValid(m_Hierarchy, m_Node);

        public Vector3 position
        {
            get => localToWorldMatrix.GetPosition();
            set => TransformAccessNative.SetWorldPositionAndRotation(m_Hierarchy, m_Node, value, rotation);
        }

        public Quaternion rotation
        {
            get => TransformAccessNative.WorldRotation(m_Hierarchy, m_Node);
            set => TransformAccessNative.SetWorldPositionAndRotation(m_Hierarchy, m_Node, position, value);
        }

        public Vector3 localPosition
        {
            get => TransformAccessNative.LocalPosition(m_Hierarchy, m_Node);
            set => TransformAccessNative.SetLocal(m_Hierarchy, m_Node, value, localRotation, localScale);
        }

        public Quaternion localRotation
        {
            get => TransformAccessNative.LocalRotation(m_Hierarchy, m_Node);
            set => TransformAccessNative.SetLocal(m_Hierarchy, m_Node, localPosition, value, localScale);
        }

        public Vector3 localScale
        {
            get => TransformAccessNative.LocalScale(m_Hierarchy, m_Node);
            set => TransformAccessNative.SetLocal(m_Hierarchy, m_Node, localPosition, localRotation, value);
        }

        public Matrix4x4 localToWorldMatrix => TransformAccessNative.LocalToWorld(m_Hierarchy, m_Node);
        public Matrix4x4 worldToLocalMatrix => localToWorldMatrix.inverse;

        public void GetPositionAndRotation(out Vector3 position, out Quaternion rotation)
        {
            position = this.position;
            rotation = this.rotation;
        }

        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) => TransformAccessNative.SetWorldPositionAndRotation(m_Hierarchy, m_Node, position, rotation);

        public void GetLocalPositionAndRotation(out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = this.localPosition;
            localRotation = this.localRotation;
        }

        public void SetLocalPositionAndRotation(Vector3 localPosition, Quaternion localRotation) =>
            TransformAccessNative.SetLocal(m_Hierarchy, m_Node, localPosition, localRotation, localScale);
    }

    /// <summary>A job run for each transform of a TransformAccessArray; the transforms of one root run on one worker.</summary>
    public interface IJobParallelForTransform
    {
        void Execute(int index, TransformAccess transform);
    }

    public static class IJobParallelForTransformExtensions
    {
        public static void EarlyJobInit<T>() where T : struct, IJobParallelForTransform => JobInfo<T>.Init();

        public static JobHandle Schedule<T>(this T jobData, TransformAccessArray transforms, JobHandle dependsOn = default) where T : struct, IJobParallelForTransform =>
            ScheduleByRef(ref jobData, transforms, dependsOn);

        public static JobHandle ScheduleByRef<T>(ref this T jobData, TransformAccessArray transforms, JobHandle dependsOn = default)
            where T : struct, IJobParallelForTransform
        {
            if (!transforms.isCreated)
                throw new InvalidOperationException("TransformAccessArray is not valid: it has not been created or it has been disposed.");
            return JobScheduler.Schedule(ref jobData, JobProducer<T>.Execute, JobScheduler.KindTransform, transforms.length, 1, true, dependsOn, transforms.m_Array);
        }

        /// <summary>Reads only: the transforms may run in any batches.</summary>
        public static JobHandle ScheduleReadOnly<T>(this T jobData, TransformAccessArray transforms, int batchSize, JobHandle dependsOn = default)
            where T : struct, IJobParallelForTransform => ScheduleByRef(ref jobData, transforms, dependsOn);

        public static JobHandle ScheduleReadOnlyByRef<T>(ref this T jobData, TransformAccessArray transforms, int batchSize, JobHandle dependsOn = default)
            where T : struct, IJobParallelForTransform => ScheduleByRef(ref jobData, transforms, dependsOn);

        public static void RunReadOnly<T>(this T jobData, TransformAccessArray transforms) where T : struct, IJobParallelForTransform => RunReadOnlyByRef(ref jobData, transforms);

        public static void RunReadOnlyByRef<T>(ref this T jobData, TransformAccessArray transforms) where T : struct, IJobParallelForTransform
        {
            JobInfo<T>.Init();
            int n = transforms.length;
            for (int i = 0; i < n; i++)
            {
                Transform t = transforms[i];
                jobData.Execute(i, new TransformAccess(TransformAccessNative.HierarchyOf(t), TransformAccessNative.NodeOf(t)));
            }
        }

        unsafe struct JobProducer<T> where T : struct, IJobParallelForTransform
        {
            internal static readonly IntPtr Execute = (IntPtr)(delegate*<byte*, uint, uint, void>)&Run;

            // One call per item: (hierarchy, node, index) of the native side's grouping.
            static void Run(byte* block, uint begin, uint end)
            {
                T job = JobScheduler.BatchCopy<T>(block, 0, (uint)int.MaxValue);
                byte* items = *(byte**)(block + JobScheduler.ItemsOffset);
                for (uint i = begin; i < end; i++)
                {
                    byte* item = items + i * 16;
                    job.Execute(*(int*)(item + 12), new TransformAccess(*(ulong*)item, *(uint*)(item + 8)));
                }
            }
        }
    }
}
