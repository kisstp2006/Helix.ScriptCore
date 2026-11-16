using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Helix.Collections;
using Helix.Collections.LowLevel.Unsafe;
using HelixEngine.Internal;
using HelixEngine.Rendering;

namespace HelixEngine
{
    /// <summary>What a compute buffer is for (the values of the reference).</summary>
    [Flags]
    public enum ComputeBufferType
    {
        Default = 0,
        Raw = 1,
        Append = 2,
        Counter = 4,
        Constant = 8,
        Structured = 16,
        IndirectArguments = 256,
    }

    /// <summary>How a compute buffer is written (every mode writes the same way here).</summary>
    public enum ComputeBufferMode
    {
        Immutable = 0,
        Dynamic = 1,
        SubUpdates = 4,
    }

    /// <summary>A graphics buffer by its native id (GraphicsBuffer.bufferHandle).</summary>
    public struct GraphicsBufferHandle : IEquatable<GraphicsBufferHandle>
    {
        internal ulong value;

        public bool Equals(GraphicsBufferHandle other) => value == other.value;
        public override bool Equals(object obj) => obj is GraphicsBufferHandle h && Equals(h);
        public override int GetHashCode() => value.GetHashCode();
        public static bool operator ==(GraphicsBufferHandle a, GraphicsBufferHandle b) => a.value == b.value;
        public static bool operator !=(GraphicsBufferHandle a, GraphicsBufferHandle b) => a.value != b.value;
    }

    // Data in and out of a native buffer by id, from pinned managed memory.
    static class BufferData
    {
        internal static void Check(ulong id, string what)
        {
            if (id == 0 || !GraphicsBufferNative.IsValid(id))
                throw new ObjectDisposedException(what);
        }

        internal static void Set(ulong id, Array data, int managedStart, int bufferStart, int count, string what)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            Check(id, what);
            int element = Marshal.SizeOf(data.GetType().GetElementType());
            if (managedStart < 0 || bufferStart < 0 || count < 0 || managedStart + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(count), $"Bad indices/count arguments (managedBufferStartIndex:{managedStart} {what}StartIndex:{bufferStart} count:{count})");
            GCHandle pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                ulong address = (ulong)pin.AddrOfPinnedObject() + (ulong)(managedStart * element);
                if (!GraphicsBufferNative.SetData(id, address, (uint)(count * element), (uint)(bufferStart * element)))
                    throw new ArgumentException($"The data does not fit the {what}.");
            }
            finally
            {
                pin.Free();
            }
        }

        internal static unsafe void Set<T>(ulong id, NativeArray<T> data, int nativeStart, int bufferStart, int count, string what) where T : struct
        {
            Check(id, what);
            int element = UnsafeUtility.SizeOf<T>();
            if (nativeStart < 0 || bufferStart < 0 || count < 0 || nativeStart + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(count), $"Bad indices/count arguments (nativeBufferStartIndex:{nativeStart} {what}StartIndex:{bufferStart} count:{count})");
            ulong address = (ulong)data.GetUnsafeReadOnlyPtr() + (ulong)(nativeStart * element);
            if (!GraphicsBufferNative.SetData(id, address, (uint)(count * element), (uint)(bufferStart * element)))
                throw new ArgumentException($"The data does not fit the {what}.");
        }

        internal static void Get(ulong id, Array data, int managedStart, int bufferStart, int count, string what)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            Check(id, what);
            int element = Marshal.SizeOf(data.GetType().GetElementType());
            if (managedStart < 0 || bufferStart < 0 || count < 0 || managedStart + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(count), $"Bad indices/count arguments (managedBufferStartIndex:{managedStart} {what}StartIndex:{bufferStart} count:{count})");
            GCHandle pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                ulong address = (ulong)pin.AddrOfPinnedObject() + (ulong)(managedStart * element);
                if (!GraphicsBufferNative.GetData(id, address, (uint)(count * element), (uint)(bufferStart * element)))
                    throw new ArgumentException($"The {what} does not hold that much data.");
            }
            finally
            {
                pin.Free();
            }
        }

        internal static ulong Create(int target, int usage, int count, int stride, string what)
        {
            if (count <= 0)
                throw new ArgumentException($"Attempting to create a zero length {what}", nameof(count));
            if (stride <= 0)
                throw new ArgumentException($"Attempting to create a {what} with a stride of 0", nameof(stride));
            const int fourAligned = 16 | 32 | 256 | 512;
            if ((target & fourAligned) != 0 && stride % 4 != 0)
                throw new ArgumentException($"{what} stride must be a multiple of 4 unless the buffer is only used as a vertex or index buffer", nameof(stride));
            ulong id = GraphicsBufferNative.Create(target, usage, count, stride);
            if (id == 0)
                throw new ArgumentException($"The {what} could not be made.");
            return id;
        }
    }

    /// <summary>A buffer of the GPU that shaders, compute shaders and draws read and write.</summary>
    public sealed class GraphicsBuffer : IDisposable
    {
        [Flags]
        public enum Target
        {
            Vertex = 1,
            Index = 2,
            CopySource = 4,
            CopyDestination = 8,
            Structured = 16,
            Raw = 32,
            Append = 64,
            Counter = 128,
            IndirectArguments = 256,
            Constant = 512,
        }

        [Flags]
        public enum UsageFlags
        {
            None = 0,
            LockBufferForWrite = 1,
        }

        /// <summary>The arguments of DrawProceduralIndirect.</summary>
        public struct IndirectDrawArgs
        {
            public const int size = 16;
            public uint vertexCountPerInstance;
            public uint instanceCount;
            public uint startVertex;
            public uint startInstance;
        }

        /// <summary>The arguments of DrawMeshInstancedIndirect.</summary>
        public struct IndirectDrawIndexedArgs
        {
            public const int size = 20;
            public uint indexCountPerInstance;
            public uint instanceCount;
            public uint startIndex;
            public uint baseVertexIndex;
            public uint startInstance;
        }

        internal ulong m_Ptr;
        string m_Name = "";
        // A buffer native code made and keeps (a render graph's): Release and Dispose leave it be.
        readonly bool m_Borrowed;

        public GraphicsBuffer(Target target, int count, int stride) : this(target, UsageFlags.None, count, stride)
        {
        }

        internal GraphicsBuffer(ulong borrowed)
        {
            m_Ptr = borrowed;
            m_Borrowed = true;
            target = (Target)GraphicsBufferNative.Target(borrowed);
            usageFlags = (UsageFlags)GraphicsBufferNative.UsageFlags(borrowed);
            count = GraphicsBufferNative.Count(borrowed);
            stride = GraphicsBufferNative.Stride(borrowed);
            m_Name = GraphicsBufferNative.Name(borrowed);
            GC.SuppressFinalize(this);
        }

        public GraphicsBuffer(Target target, UsageFlags usageFlags, int count, int stride)
        {
            m_Ptr = BufferData.Create((int)target, (int)usageFlags, count, stride, "graphics buffer");
            this.target = target;
            this.usageFlags = usageFlags;
            this.count = count;
            this.stride = stride;
        }

        ~GraphicsBuffer() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (m_Ptr != 0 && !m_Borrowed)
                GraphicsBufferNative.Release(m_Ptr);
            m_Ptr = 0;
        }

        public void Release() => Dispose();
        public bool IsValid() => m_Ptr != 0 && GraphicsBufferNative.IsValid(m_Ptr);

        public int count { get; }
        public int stride { get; }
        public Target target { get; }
        public UsageFlags usageFlags { get; }
        public GraphicsBufferHandle bufferHandle => new GraphicsBufferHandle { value = m_Ptr };

        public string name
        {
            get => m_Name;
            set
            {
                m_Name = value ?? "";
                if (m_Ptr != 0)
                    GraphicsBufferNative.SetName(m_Ptr, m_Name);
            }
        }

        public void SetData(Array data) => BufferData.Set(m_Ptr, data, 0, 0, data?.Length ?? 0, "graphics buffer");
        public void SetData<T>(List<T> data) where T : struct => SetData(data?.ToArray());
        public void SetData<T>(NativeArray<T> data) where T : struct => BufferData.Set(m_Ptr, data, 0, 0, data.Length, "graphics buffer");
        public void SetData(Array data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count) =>
            BufferData.Set(m_Ptr, data, managedBufferStartIndex, graphicsBufferStartIndex, count, "graphics buffer");
        public void SetData<T>(List<T> data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count) where T : struct =>
            SetData(data?.ToArray(), managedBufferStartIndex, graphicsBufferStartIndex, count);
        public void SetData<T>(NativeArray<T> data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count) where T : struct =>
            BufferData.Set(m_Ptr, data, nativeBufferStartIndex, graphicsBufferStartIndex, count, "graphics buffer");

        public void GetData(Array data) => BufferData.Get(m_Ptr, data, 0, 0, data?.Length ?? 0, "graphics buffer");
        public void GetData(Array data, int managedBufferStartIndex, int computeBufferStartIndex, int count) =>
            BufferData.Get(m_Ptr, data, managedBufferStartIndex, computeBufferStartIndex, count, "graphics buffer");

        public void SetCounterValue(uint counterValue) => GraphicsBufferNative.SetCounterValue(m_Ptr, counterValue);
        public IntPtr GetNativeBufferPtr() => (IntPtr)(long)GraphicsBufferNative.NativeBufferPtr(m_Ptr);
    }

    /// <summary>A buffer compute shaders read and write (a structured graphics buffer underneath).</summary>
    public sealed class ComputeBuffer : IDisposable
    {
        internal ulong m_Ptr;
        string m_Name = "";

        public ComputeBuffer(int count, int stride) : this(count, stride, ComputeBufferType.Default, ComputeBufferMode.Immutable)
        {
        }

        public ComputeBuffer(int count, int stride, ComputeBufferType type) : this(count, stride, type, ComputeBufferMode.Immutable)
        {
        }

        public ComputeBuffer(int count, int stride, ComputeBufferType type, ComputeBufferMode usage)
        {
            m_Ptr = BufferData.Create(TargetOf(type), 0, count, stride, "compute buffer");
            this.count = count;
            this.stride = stride;
        }

        // The graphics buffer targets of a type.
        static int TargetOf(ComputeBufferType type)
        {
            int target = 0;
            if ((type & ComputeBufferType.Raw) != 0)
                target |= (int)GraphicsBuffer.Target.Raw;
            if ((type & ComputeBufferType.Append) != 0)
                target |= (int)(GraphicsBuffer.Target.Append | GraphicsBuffer.Target.Structured);
            if ((type & ComputeBufferType.Counter) != 0)
                target |= (int)(GraphicsBuffer.Target.Counter | GraphicsBuffer.Target.Structured);
            if ((type & ComputeBufferType.Constant) != 0)
                target |= (int)GraphicsBuffer.Target.Constant;
            if ((type & ComputeBufferType.IndirectArguments) != 0)
                target |= (int)GraphicsBuffer.Target.IndirectArguments;
            return target == 0 ? (int)GraphicsBuffer.Target.Structured : target;
        }

        ~ComputeBuffer() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (m_Ptr != 0)
                GraphicsBufferNative.Release(m_Ptr);
            m_Ptr = 0;
        }

        public void Release() => Dispose();
        public bool IsValid() => m_Ptr != 0 && GraphicsBufferNative.IsValid(m_Ptr);

        public int count { get; }
        public int stride { get; }

        public string name
        {
            get => m_Name;
            set
            {
                m_Name = value ?? "";
                if (m_Ptr != 0)
                    GraphicsBufferNative.SetName(m_Ptr, m_Name);
            }
        }

        public void SetData(Array data) => BufferData.Set(m_Ptr, data, 0, 0, data?.Length ?? 0, "compute buffer");
        public void SetData<T>(List<T> data) where T : struct => SetData(data?.ToArray());
        public void SetData<T>(NativeArray<T> data) where T : struct => BufferData.Set(m_Ptr, data, 0, 0, data.Length, "compute buffer");
        public void SetData(Array data, int managedBufferStartIndex, int computeBufferStartIndex, int count) =>
            BufferData.Set(m_Ptr, data, managedBufferStartIndex, computeBufferStartIndex, count, "compute buffer");
        public void SetData<T>(List<T> data, int managedBufferStartIndex, int computeBufferStartIndex, int count) where T : struct =>
            SetData(data?.ToArray(), managedBufferStartIndex, computeBufferStartIndex, count);
        public void SetData<T>(NativeArray<T> data, int nativeBufferStartIndex, int computeBufferStartIndex, int count) where T : struct =>
            BufferData.Set(m_Ptr, data, nativeBufferStartIndex, computeBufferStartIndex, count, "compute buffer");

        public void GetData(Array data) => BufferData.Get(m_Ptr, data, 0, 0, data?.Length ?? 0, "compute buffer");
        public void GetData(Array data, int managedBufferStartIndex, int computeBufferStartIndex, int count) =>
            BufferData.Get(m_Ptr, data, managedBufferStartIndex, computeBufferStartIndex, count, "compute buffer");

        public void SetCounterValue(uint counterValue) => GraphicsBufferNative.SetCounterValue(m_Ptr, counterValue);
        public IntPtr GetNativeBufferPtr() => (IntPtr)(long)GraphicsBufferNative.NativeBufferPtr(m_Ptr);
    }

    public sealed partial class ComputeShader
    {
        /// <summary>The kernel of that name; throws when there is none.</summary>
        public int FindKernel(string name)
        {
            int k = KernelIndex(name);
            if (k < 0)
                throw new ArgumentException($"Kernel '{name}' not found in compute shader '{this.name}'.");
            return k;
        }

        public void GetKernelThreadGroupSizes(int kernelIndex, out uint x, out uint y, out uint z)
        {
            if (kernelIndex < 0 || kernelIndex >= KernelCount())
                throw new ArgumentException($"Kernel index ({kernelIndex}) out of range");
            x = KernelGroupSize(kernelIndex, 0);
            y = KernelGroupSize(kernelIndex, 1);
            z = KernelGroupSize(kernelIndex, 2);
        }

        public void SetBuffer(int kernelIndex, string name, ComputeBuffer buffer) => SetBuffer(kernelIndex, Shader.PropertyToID(name), buffer);
        public void SetBuffer(int kernelIndex, int nameID, ComputeBuffer buffer) => SetBufferId(kernelIndex, nameID, buffer?.m_Ptr ?? 0);
        public void SetBuffer(int kernelIndex, string name, GraphicsBuffer buffer) => SetBuffer(kernelIndex, Shader.PropertyToID(name), buffer);
        public void SetBuffer(int kernelIndex, int nameID, GraphicsBuffer buffer) => SetBufferId(kernelIndex, nameID, buffer?.m_Ptr ?? 0);
        public void SetBuffer(int kernelIndex, string name, GraphicsBufferHandle bufferHandle) => SetBufferId(kernelIndex, Shader.PropertyToID(name), bufferHandle.value);
        public void SetBuffer(int kernelIndex, int nameID, GraphicsBufferHandle bufferHandle) => SetBufferId(kernelIndex, nameID, bufferHandle.value);

        public void DispatchIndirect(int kernelIndex, ComputeBuffer argsBuffer, uint argsOffset = 0) =>
            DispatchIndirectId(kernelIndex, argsBuffer?.m_Ptr ?? throw new ArgumentNullException(nameof(argsBuffer)), argsOffset);
        public void DispatchIndirect(int kernelIndex, GraphicsBuffer argsBuffer, uint argsOffset = 0) =>
            DispatchIndirectId(kernelIndex, argsBuffer?.m_Ptr ?? throw new ArgumentNullException(nameof(argsBuffer)), argsOffset);
    }

    public partial class Shader
    {
        public static void SetGlobalBuffer(string name, ComputeBuffer value) => SetGlobalBufferId(PropertyToID(name), value?.m_Ptr ?? 0);
        public static void SetGlobalBuffer(int nameID, ComputeBuffer value) => SetGlobalBufferId(nameID, value?.m_Ptr ?? 0);
        public static void SetGlobalBuffer(string name, GraphicsBuffer value) => SetGlobalBufferId(PropertyToID(name), value?.m_Ptr ?? 0);
        public static void SetGlobalBuffer(int nameID, GraphicsBuffer value) => SetGlobalBufferId(nameID, value?.m_Ptr ?? 0);
    }

    public partial class Material
    {
        public void SetBuffer(string name, ComputeBuffer value) => SetBufferId(Shader.PropertyToID(name), value?.m_Ptr ?? 0);
        public void SetBuffer(int nameID, ComputeBuffer value) => SetBufferId(nameID, value?.m_Ptr ?? 0);
        public void SetBuffer(string name, GraphicsBuffer value) => SetBufferId(Shader.PropertyToID(name), value?.m_Ptr ?? 0);
        public void SetBuffer(int nameID, GraphicsBuffer value) => SetBufferId(nameID, value?.m_Ptr ?? 0);
    }

    public sealed partial class MaterialPropertyBlock
    {
        public void SetBuffer(string name, ComputeBuffer value) => SetBuffer(Shader.PropertyToID(name), value);
        public void SetBuffer(int nameID, ComputeBuffer value) => PropertyBlockNative.SetBuffer(m_Block, nameID, value?.m_Ptr ?? 0);
        public void SetBuffer(string name, GraphicsBuffer value) => SetBuffer(Shader.PropertyToID(name), value);
        public void SetBuffer(int nameID, GraphicsBuffer value) => PropertyBlockNative.SetBuffer(m_Block, nameID, value?.m_Ptr ?? 0);
    }
}

namespace HelixEngine.Rendering
{
    public partial struct LocalKeyword
    {
        /// <summary>The keyword of that name in the compute shader's space; an error is logged when there is none.</summary>
        public LocalKeyword(ComputeShader shader, string name) => this = KeywordNative.FindInCompute(shader, name);
    }

    public partial class CommandBuffer
    {
        static int NameId(string name) => Shader.PropertyToID(name);

        public void SetGlobalBuffer(string name, ComputeBuffer value) => SetGlobalBuffer(NameId(name), value);
        public void SetGlobalBuffer(int nameID, ComputeBuffer value) => RenderPipelineNative.SetGlobalBuffer(this.Id, nameID, value?.m_Ptr ?? 0);
        public void SetGlobalBuffer(string name, GraphicsBuffer value) => SetGlobalBuffer(NameId(name), value);
        public void SetGlobalBuffer(int nameID, GraphicsBuffer value) => RenderPipelineNative.SetGlobalBuffer(this.Id, nameID, value?.m_Ptr ?? 0);

        void ComputeValue(ComputeShader shader, int nameID, int kind, float[] values)
        {
            if (shader == null)
                throw new ArgumentNullException(nameof(shader));
            RenderPipelineNative.SetComputeValue(this.Id, shader, nameID, kind, values);
        }

        public void SetComputeFloatParam(ComputeShader computeShader, string name, float val) => SetComputeFloatParam(computeShader, NameId(name), val);
        public void SetComputeFloatParam(ComputeShader computeShader, int nameID, float val) => ComputeValue(computeShader, nameID, 0, new[] { val });
        public void SetComputeIntParam(ComputeShader computeShader, string name, int val) => SetComputeIntParam(computeShader, NameId(name), val);
        public void SetComputeIntParam(ComputeShader computeShader, int nameID, int val) => ComputeValue(computeShader, nameID, 1, new[] { (float)val });
        public void SetComputeVectorParam(ComputeShader computeShader, string name, Vector4 val) => SetComputeVectorParam(computeShader, NameId(name), val);
        public void SetComputeVectorParam(ComputeShader computeShader, int nameID, Vector4 val) =>
            ComputeValue(computeShader, nameID, 2, new[] { val.x, val.y, val.z, val.w });
        public void SetComputeMatrixParam(ComputeShader computeShader, string name, Matrix4x4 val) => SetComputeMatrixParam(computeShader, NameId(name), val);
        public void SetComputeMatrixParam(ComputeShader computeShader, int nameID, Matrix4x4 val) => ComputeValue(computeShader, nameID, 3, Floats(new[] { val }));
        public void SetComputeFloatParams(ComputeShader computeShader, string name, params float[] values) => SetComputeFloatParams(computeShader, NameId(name), values);
        public void SetComputeFloatParams(ComputeShader computeShader, int nameID, params float[] values) =>
            ComputeValue(computeShader, nameID, 4, values ?? throw new ArgumentNullException(nameof(values)));
        public void SetComputeIntParams(ComputeShader computeShader, string name, params int[] values) => SetComputeIntParams(computeShader, NameId(name), values);
        public void SetComputeIntParams(ComputeShader computeShader, int nameID, params int[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            float[] f = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
                f[i] = values[i];
            ComputeValue(computeShader, nameID, 5, f);
        }
        public void SetComputeVectorArrayParam(ComputeShader computeShader, string name, Vector4[] values) => SetComputeVectorArrayParam(computeShader, NameId(name), values);
        public void SetComputeVectorArrayParam(ComputeShader computeShader, int nameID, Vector4[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            float[] f = new float[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
            {
                f[i * 4] = values[i].x;
                f[i * 4 + 1] = values[i].y;
                f[i * 4 + 2] = values[i].z;
                f[i * 4 + 3] = values[i].w;
            }
            ComputeValue(computeShader, nameID, 6, f);
        }
        public void SetComputeMatrixArrayParam(ComputeShader computeShader, string name, Matrix4x4[] values) => SetComputeMatrixArrayParam(computeShader, NameId(name), values);
        public void SetComputeMatrixArrayParam(ComputeShader computeShader, int nameID, Matrix4x4[] values) =>
            ComputeValue(computeShader, nameID, 7, Floats(values ?? throw new ArgumentNullException(nameof(values))));

        static float[] Floats(Matrix4x4[] values)
        {
            float[] f = new float[values.Length * 16];
            for (int i = 0; i < values.Length; i++)
                for (int k = 0; k < 16; k++)
                    f[i * 16 + k] = values[i][k];
            return f;
        }

        public void SetComputeTextureParam(ComputeShader computeShader, int kernelIndex, string name, RenderTargetIdentifier rt) =>
            SetComputeTextureParam(computeShader, kernelIndex, NameId(name), rt, 0);
        public void SetComputeTextureParam(ComputeShader computeShader, int kernelIndex, int nameID, RenderTargetIdentifier rt) =>
            SetComputeTextureParam(computeShader, kernelIndex, nameID, rt, 0);
        public void SetComputeTextureParam(ComputeShader computeShader, int kernelIndex, string name, RenderTargetIdentifier rt, int mipLevel) =>
            SetComputeTextureParam(computeShader, kernelIndex, NameId(name), rt, mipLevel);
        public void SetComputeTextureParam(ComputeShader computeShader, int kernelIndex, int nameID, RenderTargetIdentifier rt, int mipLevel)
        {
            if (computeShader == null)
                throw new ArgumentNullException(nameof(computeShader));
            RenderPipelineNative.SetComputeTextureParam(this.Id, computeShader, kernelIndex, nameID, rt.Data, mipLevel);
        }

        public void SetComputeBufferParam(ComputeShader computeShader, int kernelIndex, string name, ComputeBuffer buffer) =>
            SetComputeBufferParam(computeShader, kernelIndex, NameId(name), buffer);
        public void SetComputeBufferParam(ComputeShader computeShader, int kernelIndex, int nameID, ComputeBuffer buffer) =>
            ComputeBufferParam(computeShader, kernelIndex, nameID, buffer?.m_Ptr ?? 0);
        public void SetComputeBufferParam(ComputeShader computeShader, int kernelIndex, string name, GraphicsBuffer buffer) =>
            SetComputeBufferParam(computeShader, kernelIndex, NameId(name), buffer);
        public void SetComputeBufferParam(ComputeShader computeShader, int kernelIndex, int nameID, GraphicsBuffer buffer) =>
            ComputeBufferParam(computeShader, kernelIndex, nameID, buffer?.m_Ptr ?? 0);
        public void SetComputeBufferParam(ComputeShader computeShader, int kernelIndex, string name, GraphicsBufferHandle bufferHandle) =>
            ComputeBufferParam(computeShader, kernelIndex, NameId(name), bufferHandle.value);
        public void SetComputeBufferParam(ComputeShader computeShader, int kernelIndex, int nameID, GraphicsBufferHandle bufferHandle) =>
            ComputeBufferParam(computeShader, kernelIndex, nameID, bufferHandle.value);

        void ComputeBufferParam(ComputeShader computeShader, int kernelIndex, int nameID, ulong buffer)
        {
            if (computeShader == null)
                throw new ArgumentNullException(nameof(computeShader));
            RenderPipelineNative.SetComputeBufferParam(this.Id, computeShader, kernelIndex, nameID, buffer);
        }

        public void EnableKeyword(ComputeShader computeShader, in LocalKeyword keyword) => SetKeyword(computeShader, keyword, true);
        public void DisableKeyword(ComputeShader computeShader, in LocalKeyword keyword) => SetKeyword(computeShader, keyword, false);
        public void SetKeyword(ComputeShader computeShader, in LocalKeyword keyword, bool value)
        {
            if (computeShader == null)
                throw new ArgumentNullException(nameof(computeShader));
            RenderPipelineNative.SetComputeKeyword(this.Id, computeShader, keyword, value);
        }

        public void DispatchCompute(ComputeShader computeShader, int kernelIndex, int threadGroupsX, int threadGroupsY, int threadGroupsZ)
        {
            if (computeShader == null)
                throw new ArgumentNullException(nameof(computeShader));
            RenderPipelineNative.DispatchCompute(this.Id, computeShader, kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
        }

        public void DispatchCompute(ComputeShader computeShader, int kernelIndex, ComputeBuffer indirectBuffer, uint argsOffset) =>
            DispatchIndirect(computeShader, kernelIndex, indirectBuffer?.m_Ptr ?? throw new ArgumentNullException(nameof(indirectBuffer)), argsOffset);
        public void DispatchCompute(ComputeShader computeShader, int kernelIndex, GraphicsBuffer indirectBuffer, uint argsOffset) =>
            DispatchIndirect(computeShader, kernelIndex, indirectBuffer?.m_Ptr ?? throw new ArgumentNullException(nameof(indirectBuffer)), argsOffset);

        void DispatchIndirect(ComputeShader computeShader, int kernelIndex, ulong args, uint argsOffset)
        {
            if (computeShader == null)
                throw new ArgumentNullException(nameof(computeShader));
            RenderPipelineNative.DispatchComputeIndirect(this.Id, computeShader, kernelIndex, args, argsOffset);
        }

        public void SetBufferData(ComputeBuffer buffer, Array data) => SetBufferData(buffer, data, 0, 0, data?.Length ?? 0);
        public void SetBufferData(ComputeBuffer buffer, Array data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count) =>
            BufferDataCommand(buffer?.m_Ptr ?? throw new ArgumentNullException(nameof(buffer)), data, managedBufferStartIndex, graphicsBufferStartIndex, count);
        public void SetBufferData(GraphicsBuffer buffer, Array data) => SetBufferData(buffer, data, 0, 0, data?.Length ?? 0);
        public void SetBufferData(GraphicsBuffer buffer, Array data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count) =>
            BufferDataCommand(buffer?.m_Ptr ?? throw new ArgumentNullException(nameof(buffer)), data, managedBufferStartIndex, graphicsBufferStartIndex, count);
        public void SetBufferData<T>(GraphicsBuffer buffer, List<T> data) where T : struct => SetBufferData(buffer, data?.ToArray());
        public void SetBufferData<T>(ComputeBuffer buffer, List<T> data) where T : struct => SetBufferData(buffer, data?.ToArray());
        public unsafe void SetBufferData<T>(GraphicsBuffer buffer, NativeArray<T> data) where T : struct =>
            RenderPipelineNative.SetBufferData(this.Id, buffer?.m_Ptr ?? throw new ArgumentNullException(nameof(buffer)), (ulong)data.GetUnsafeReadOnlyPtr(),
                                               (uint)(data.Length * UnsafeUtility.SizeOf<T>()), 0);
        public unsafe void SetBufferData<T>(ComputeBuffer buffer, NativeArray<T> data) where T : struct =>
            RenderPipelineNative.SetBufferData(this.Id, buffer?.m_Ptr ?? throw new ArgumentNullException(nameof(buffer)), (ulong)data.GetUnsafeReadOnlyPtr(),
                                               (uint)(data.Length * UnsafeUtility.SizeOf<T>()), 0);

        // The bytes are copied into the command (the array may change after the call).
        void BufferDataCommand(ulong buffer, Array data, int managedStart, int bufferStart, int count)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            int element = Marshal.SizeOf(data.GetType().GetElementType());
            if (managedStart < 0 || bufferStart < 0 || count < 0 || managedStart + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            GCHandle pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                ulong address = (ulong)pin.AddrOfPinnedObject() + (ulong)(managedStart * element);
                RenderPipelineNative.SetBufferData(this.Id, buffer, address, (uint)(count * element), (uint)(bufferStart * element));
            }
            finally
            {
                pin.Free();
            }
        }
    }
}
