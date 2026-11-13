using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Helix.Collections;
using Helix.Collections.LowLevel.Unsafe;
using HelixEngine.Internal;
using HelixEngine.Rendering;

namespace HelixEngine
{
    // The NativeArray overloads of Mesh and its data for jobs (Mesh.MeshData).
    public partial class Mesh
    {
        public void SetVertices<T>(NativeArray<T> inVertices) where T : struct =>
            SetVertices(As<T, Vector3>(inVertices, 12, "SetVertices with NativeArray should use struct type that is 12 bytes (3x float) in size"));
        public void SetVertices<T>(NativeArray<T> inVertices, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) where T : struct =>
            SetVertices(Sub(inVertices, start, length));
        public void SetNormals<T>(NativeArray<T> inNormals) where T : struct =>
            SetNormals(As<T, Vector3>(inNormals, 12, "SetNormals with NativeArray should use struct type that is 12 bytes (3x float) in size"));
        public void SetNormals<T>(NativeArray<T> inNormals, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) where T : struct =>
            SetNormals(Sub(inNormals, start, length));
        public void SetTangents<T>(NativeArray<T> inTangents) where T : struct =>
            SetTangents(As<T, Vector4>(inTangents, 16, "SetTangents with NativeArray should use struct type that is 16 bytes (4x float) in size"));
        public void SetTangents<T>(NativeArray<T> inTangents, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) where T : struct =>
            SetTangents(Sub(inTangents, start, length));

        public void SetColors<T>(NativeArray<T> inColors) where T : struct
        {
            int size = UnsafeUtility.SizeOf<T>();
            if (size == 16)
                SetColors(inColors.Reinterpret<Color>(16));
            else if (size == 4)
                SetColors(inColors.Reinterpret<Color32>(4));
            else
                throw new ArgumentException("SetColors with NativeArray should use struct type that is 16 bytes (4x float) or 4 bytes (4x unorm) in size");
        }

        public void SetColors<T>(NativeArray<T> inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) where T : struct =>
            SetColors(Sub(inColors, start, length));

        public void SetUVs<T>(int channel, NativeArray<T> uvs) where T : struct
        {
            Channel(channel);
            switch (UnsafeUtility.SizeOf<T>())
            {
                case 8: SetUVs(channel, uvs.Reinterpret<Vector2>(8)); break;
                case 12: SetUVs(channel, uvs.Reinterpret<Vector3>(12)); break;
                case 16: SetUVs(channel, uvs.Reinterpret<Vector4>(16)); break;
                default: throw new ArgumentException("SetUVs with NativeArray should use struct type that is 8, 12 or 16 bytes (2x, 3x or 4x float) in size");
            }
        }

        public void SetUVs<T>(int channel, NativeArray<T> uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) where T : struct =>
            SetUVs(channel, Sub(uvs, start, length));

        public void SetIndices<T>(NativeArray<T> indices, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0) where T : struct
        {
            int size = UnsafeUtility.SizeOf<T>();
            if (size == 4)
                SetIndices(indices.Reinterpret<int>(4), topology, submesh, calculateBounds, baseVertex);
            else if (size == 2)
            {
                NativeArray<ushort> shorts = indices.Reinterpret<ushort>(2);
                var wide = new NativeArray<int>(shorts.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < shorts.Length; i++)
                    wide[i] = shorts[i];
                SetIndices(wide, topology, submesh, calculateBounds, baseVertex);
                wide.Dispose();
            }
            else
                throw new ArgumentException("SetIndices with NativeArray should use type is 2 or 4 bytes in size");
        }

        public void SetIndices<T>(NativeArray<T> indices, int indicesStart, int indicesLength, MeshTopology topology, int submesh, bool calculateBounds = true,
                                  int baseVertex = 0) where T : struct =>
            SetIndices(Sub(indices, indicesStart, indicesLength), topology, submesh, calculateBounds, baseVertex);

        public void SetVertexBufferParams(int vertexCount, NativeArray<VertexAttributeDescriptor> attributes) => SetVertexBufferParamsNative(vertexCount, attributes);

        public void SetVertexBufferData<T>(NativeArray<T> data, int dataStart, int meshBufferStart, int count, int stream = 0, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            where T : struct
        {
            int size = UnsafeUtility.SizeOf<T>();
            SetVertexBufferRaw(data.Reinterpret<byte>(size), dataStart * size, meshBufferStart * size, count * size, stream, flags);
        }

        public void SetIndexBufferData<T>(NativeArray<T> data, int dataStart, int meshBufferStart, int count, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            where T : struct
        {
            int size = UnsafeUtility.SizeOf<T>();
            SetIndexBufferRaw(data.Reinterpret<byte>(size), dataStart * size, meshBufferStart * size, count * size, flags);
        }

        public void SetSubMeshes<T>(NativeArray<T> desc, MeshUpdateFlags flags = MeshUpdateFlags.Default) where T : struct =>
            SetSubMeshes(As<T, SubMeshDescriptor>(desc, UnsafeUtility.SizeOf<SubMeshDescriptor>(),
                                                  $"SetSubMeshes with NativeArray should use struct type that is {UnsafeUtility.SizeOf<SubMeshDescriptor>()} bytes in size"), flags);
        public void SetSubMeshes<T>(NativeArray<T> desc, int start, int count, MeshUpdateFlags flags = MeshUpdateFlags.Default) where T : struct =>
            SetSubMeshes(Sub(desc, start, count), flags);

        static NativeArray<U> As<T, U>(NativeArray<T> a, int size, string message) where T : struct where U : struct
        {
            if (UnsafeUtility.SizeOf<T>() != size)
                throw new ArgumentException(message);
            return a.Reinterpret<U>(size);
        }

        static NativeArray<T> Sub<T>(NativeArray<T> a, int start, int length) where T : struct
        {
            if (start < 0 || length < 0 || start + length > a.Length)
                throw new ArgumentOutOfRangeException(nameof(start), $"Bad start/length arguments ({start}, {length}) for an array of {a.Length}");
            return a.GetSubArray(start, length);
        }

        // ------------------------------------------------------------------------------------------------- MeshData

        /// <summary>
        /// One mesh's data for jobs: from AcquireReadOnlyMeshData (a copy taken then) or AllocateWritableMeshData (filled
        /// in jobs, then ApplyAndDisposeWritableMeshData). Valid until its MeshDataArray is disposed.
        /// </summary>
        [NativeContainer]
        public unsafe struct MeshData
        {
            [NativeDisableUnsafePtrRestriction] internal ulong m_Ptr;
            internal int m_ReadOnly;
            internal AtomicSafetyHandle m_Safety;

            public int vertexCount { get { Read(); return MeshDataNative.VertexCount(m_Ptr); } }
            public int vertexBufferCount { get { Read(); return MeshDataNative.VertexBufferCount(m_Ptr); } }
            public IndexFormat indexFormat { get { Read(); return MeshDataNative.GetIndexFormat(m_Ptr); } }

            public int subMeshCount
            {
                get { Read(); return MeshDataNative.SubMeshCount(m_Ptr); }
                set { Write(); MeshDataNative.SetSubMeshCount(m_Ptr, value); }
            }

            public bool HasVertexAttribute(VertexAttribute attr) { Read(); return MeshDataNative.HasVertexAttribute(m_Ptr, attr); }
            public int GetVertexAttributeDimension(VertexAttribute attr) { Read(); return MeshDataNative.GetVertexAttributeDimension(m_Ptr, attr); }
            public VertexAttributeFormat GetVertexAttributeFormat(VertexAttribute attr) { Read(); return MeshDataNative.GetVertexAttributeFormat(m_Ptr, attr); }
            public int GetVertexAttributeStream(VertexAttribute attr) { Read(); return MeshDataNative.GetVertexAttributeStream(m_Ptr, attr); }
            public int GetVertexAttributeOffset(VertexAttribute attr) { Read(); return MeshDataNative.GetVertexAttributeOffset(m_Ptr, attr); }
            public int GetVertexBufferStride(int stream) { Read(); return MeshDataNative.GetVertexBufferStride(m_Ptr, stream); }

            /// <summary>The bytes of a vertex stream as T where they are (writes go to the data).</summary>
            public NativeArray<T> GetVertexData<T>(int stream = 0) where T : struct
            {
                Read();
                if (stream < 0 || stream >= 4)
                    throw new ArgumentOutOfRangeException(nameof(stream), $"{nameof(stream)} out of bounds, should be below 4 but was {stream}");
                return View<T>(MeshDataNative.VertexDataAddress(m_Ptr, stream), MeshDataNative.VertexDataSize(m_Ptr, stream), $"Mesh vertex buffer stream {stream}");
            }

            /// <summary>The index bytes as T (ushort or uint by the index format) where they are.</summary>
            public NativeArray<T> GetIndexData<T>() where T : struct
            {
                Read();
                return View<T>(MeshDataNative.IndexDataAddress(m_Ptr), MeshDataNative.IndexDataSize(m_Ptr), "Mesh index buffer");
            }

            NativeArray<T> View<T>(ulong address, int bytes, string what) where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                if (bytes % size != 0)
                    throw new ArgumentException($"{what} size ({bytes}) is not a multiple of type {typeof(T)} size ({size})");
                var view = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((void*)address, bytes / size, Allocator.None);
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref view, m_Safety);
                return view;
            }

            public void GetVertices(NativeArray<Vector3> outVertices) => Attribute(VertexAttribute.Position, outVertices, 3, nameof(outVertices));
            public void GetNormals(NativeArray<Vector3> outNormals) => Attribute(VertexAttribute.Normal, outNormals, 3, nameof(outNormals));
            public void GetTangents(NativeArray<Vector4> outTangents) => Attribute(VertexAttribute.Tangent, outTangents, 4, nameof(outTangents));
            public void GetColors(NativeArray<Color> outColors) => Attribute(VertexAttribute.Color, outColors, 4, nameof(outColors));

            public void GetColors(NativeArray<Color32> outColors)
            {
                Read();
                int n = MeshDataNative.VertexCount(m_Ptr);
                Match(outColors.Length, n, nameof(outColors));
                var floats = (Color*)NativeMemory.Alloc((nuint)Math.Max(n, 1), (nuint)sizeof(Color));
                MeshDataNative.CopyAttribute(m_Ptr, VertexAttribute.Color, (ulong)floats, 4);
                var to = (Color32*)outColors.GetUnsafePtr();
                for (int i = 0; i < n; i++)
                    to[i] = floats[i];
                NativeMemory.Free(floats);
            }

            public void GetUVs(int channel, NativeArray<Vector2> outUVs) => Attribute(Uv(channel), outUVs, 2, nameof(outUVs));
            public void GetUVs(int channel, NativeArray<Vector3> outUVs) => Attribute(Uv(channel), outUVs, 3, nameof(outUVs));
            public void GetUVs(int channel, NativeArray<Vector4> outUVs) => Attribute(Uv(channel), outUVs, 4, nameof(outUVs));

            public void GetIndices(NativeArray<ushort> outIndices, int submesh, bool applyBaseVertex = true) => Indices(outIndices, true, submesh, applyBaseVertex);
            public void GetIndices(NativeArray<int> outIndices, int submesh, bool applyBaseVertex = true) => Indices(outIndices, false, submesh, applyBaseVertex);

            void Indices<T>(NativeArray<T> outIndices, bool shorts, int submesh, bool applyBaseVertex) where T : struct
            {
                Read();
                int count = MeshDataNative.CopyIndices(m_Ptr, 0, 0, shorts, submesh, applyBaseVertex);
                if (count < 0)
                    throw new IndexOutOfRangeException($"Specified submesh ({submesh}) is out of range. Must be greater or equal to 0 and less than subMeshCount ({MeshDataNative.SubMeshCount(m_Ptr)}).");
                if (outIndices.Length < count)
                    throw new ArgumentException($"Index array size ({outIndices.Length}) is smaller than the submesh index count ({count})", nameof(outIndices));
                MeshDataNative.CopyIndices(m_Ptr, (ulong)outIndices.GetUnsafePtr(), outIndices.Length, shorts, submesh, applyBaseVertex);
            }

            void Attribute<T>(VertexAttribute attribute, NativeArray<T> output, int components, string name) where T : struct
            {
                Read();
                int n = MeshDataNative.VertexCount(m_Ptr);
                Match(output.Length, n, name);
                MeshDataNative.CopyAttribute(m_Ptr, attribute, (ulong)output.GetUnsafePtr(), components);
            }

            static void Match(int length, int vertexCount, string name)
            {
                if (length != vertexCount)
                    throw new ArgumentException($"Output array size ({length}) must match the vertex count ({vertexCount})", name);
            }

            static VertexAttribute Uv(int channel)
            {
                if (channel < 0 || channel > 7)
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, "The uv index is invalid. Must be in the range 0 to 7.");
                return VertexAttribute.TexCoord0 + channel;
            }

            public void SetVertexBufferParams(int vertexCount, params VertexAttributeDescriptor[] attributes)
            {
                if (attributes == null)
                    throw new ArgumentNullException(nameof(attributes));
                var a = new NativeArray<VertexAttributeDescriptor>(attributes, Allocator.Temp);
                SetVertexBufferParams(vertexCount, a);
                a.Dispose();
            }

            public void SetVertexBufferParams(int vertexCount, NativeArray<VertexAttributeDescriptor> attributes)
            {
                Write();
                MeshDataNative.SetVertexBufferParams(m_Ptr, vertexCount, attributes);
            }

            public void SetIndexBufferParams(int indexCount, IndexFormat format)
            {
                Write();
                MeshDataNative.SetIndexBufferParams(m_Ptr, indexCount, format);
            }

            public SubMeshDescriptor GetSubMesh(int index)
            {
                Read();
                return MeshDataNative.GetSubMesh(m_Ptr, index);
            }

            public void SetSubMesh(int index, SubMeshDescriptor desc, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            {
                Write();
                MeshDataNative.SetSubMesh(m_Ptr, index, desc, flags);
            }

            void Read()
            {
                if (m_Ptr == 0)
                    throw new InvalidOperationException("The MeshData is not valid: it comes from no MeshDataArray.");
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            }

            void Write()
            {
                Read();
                if (m_ReadOnly != 0)
                    throw new InvalidOperationException("Not allowed to modify MeshData from Mesh.AcquireReadOnlyMeshData; use Mesh.AllocateWritableMeshData.");
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            }
        }

        /// <summary>The MeshData of several meshes; disposed by Dispose (read only ones) or ApplyAndDisposeWritableMeshData.</summary>
        [NativeContainer]
        public unsafe struct MeshDataArray : IDisposable
        {
            [NativeDisableUnsafePtrRestriction] internal ulong* m_Ptrs;
            internal int m_Length;
            internal int m_ReadOnly;
            internal AtomicSafetyHandle m_Safety;

            static readonly int s_StaticSafetyId = AtomicSafetyHandle.NewStaticSafetyId<MeshDataArray>();

            public int Length => m_Length;

            public MeshData this[int index]
            {
                get
                {
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                    if ((uint)index >= (uint)m_Length)
                        throw new IndexOutOfRangeException($"Index {index} is out of range of '{m_Length}' Length.");
                    return new MeshData { m_Ptr = m_Ptrs[index], m_ReadOnly = m_ReadOnly, m_Safety = m_Safety };
                }
            }

            internal MeshDataArray(int count, bool readOnly)
            {
                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count), "Mesh count can not be negative");
                m_Length = count;
                m_ReadOnly = readOnly ? 1 : 0;
                m_Ptrs = (ulong*)NativeMemory.AllocZeroed((nuint)Math.Max(count, 1), sizeof(ulong));
                m_Safety = AtomicSafetyHandle.Create();
                if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
                    AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, s_StaticSafetyId);
                LeakTracker.Track(m_Ptrs, Allocator.Persistent);
            }

            public void Dispose()
            {
                if (m_Ptrs == null)
                    throw new ObjectDisposedException(null, "The Mesh.MeshDataArray has already been disposed");
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
                for (int i = 0; i < m_Length; i++)
                    MeshDataNative.Release(m_Ptrs[i]);
                LeakTracker.Untrack(m_Ptrs);
                NativeMemory.Free(m_Ptrs);
                AtomicSafetyHandle.Release(m_Safety);
                m_Ptrs = null;
                m_Length = 0;
            }
        }

        public static MeshDataArray AcquireReadOnlyMeshData(Mesh mesh) => AcquireReadOnlyMeshData(new[] { mesh });

        public static unsafe MeshDataArray AcquireReadOnlyMeshData(Mesh[] meshes)
        {
            if (meshes == null)
                throw new ArgumentNullException(nameof(meshes), "Mesh array is null");
            foreach (Mesh m in meshes)
                if (m == null)
                    throw new ArgumentNullException(nameof(meshes), "Some of the meshes are null");
            var data = new MeshDataArray(meshes.Length, true);
            for (int i = 0; i < meshes.Length; i++)
                data.m_Ptrs[i] = MeshDataNative.AcquireReadOnly(meshes[i]);
            return data;
        }

        public static MeshDataArray AcquireReadOnlyMeshData(List<Mesh> meshes)
        {
            if (meshes == null)
                throw new ArgumentNullException(nameof(meshes), "Mesh list is null");
            return AcquireReadOnlyMeshData(meshes.ToArray());
        }

        public static unsafe MeshDataArray AllocateWritableMeshData(int meshCount)
        {
            var data = new MeshDataArray(meshCount, false);
            for (int i = 0; i < meshCount; i++)
                data.m_Ptrs[i] = MeshDataNative.AllocateWritable();
            return data;
        }

        public static void ApplyAndDisposeWritableMeshData(MeshDataArray data, Mesh mesh, MeshUpdateFlags flags = MeshUpdateFlags.Default)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh), "Mesh is null");
            if (data.Length != 1)
                throw new InvalidOperationException($"{data.Length} mesh data elements were passed, but 1 mesh was passed");
            Apply(data, new[] { mesh }, flags);
        }

        public static void ApplyAndDisposeWritableMeshData(MeshDataArray data, Mesh[] meshes, MeshUpdateFlags flags = MeshUpdateFlags.Default)
        {
            if (meshes == null)
                throw new ArgumentNullException(nameof(meshes), "Mesh array is null");
            if (data.Length != meshes.Length)
                throw new InvalidOperationException($"{data.Length} mesh data elements were passed, but {meshes.Length} meshes were passed");
            Apply(data, meshes, flags);
        }

        public static void ApplyAndDisposeWritableMeshData(MeshDataArray data, List<Mesh> meshes, MeshUpdateFlags flags = MeshUpdateFlags.Default)
        {
            if (meshes == null)
                throw new ArgumentNullException(nameof(meshes), "Mesh list is null");
            ApplyAndDisposeWritableMeshData(data, meshes.ToArray(), flags);
        }

        static unsafe void Apply(MeshDataArray data, Mesh[] meshes, MeshUpdateFlags flags)
        {
            if (data.m_Ptrs == null)
                throw new ObjectDisposedException(null, "The Mesh.MeshDataArray has already been disposed");
            if (data.m_ReadOnly != 0)
                throw new InvalidOperationException("Only the MeshDataArray from Mesh.AllocateWritableMeshData can be applied to meshes");
            AtomicSafetyHandle.CheckWriteAndThrow(data.m_Safety);
            foreach (Mesh m in meshes)
                if (m == null)
                    throw new ArgumentNullException(nameof(meshes), "Some of the meshes are null");
            for (int i = 0; i < meshes.Length; i++)
                MeshDataNative.Apply(data.m_Ptrs[i], meshes[i], flags);
            data.Dispose();
        }
    }
}
