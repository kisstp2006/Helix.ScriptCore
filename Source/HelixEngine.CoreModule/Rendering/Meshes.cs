using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HelixEngine.Rendering;

namespace HelixEngine.Rendering
{
    public partial struct VertexAttributeDescriptor : IEquatable<VertexAttributeDescriptor>
    {
        public VertexAttributeDescriptor(VertexAttribute attribute = VertexAttribute.Position, VertexAttributeFormat format = VertexAttributeFormat.Float32,
                                         int dimension = 3, int stream = 0)
        {
            this.attribute = attribute;
            this.format = format;
            this.dimension = dimension;
            this.stream = stream;
        }

        public override string ToString() => $"(attr={attribute} fmt={format} dim={dimension} stream={stream})";
        public override int GetHashCode() => HashCode.Combine(attribute, format, dimension, stream);
        public override bool Equals(object other) => other is VertexAttributeDescriptor d && Equals(d);
        public bool Equals(VertexAttributeDescriptor other) =>
            attribute == other.attribute && format == other.format && dimension == other.dimension && stream == other.stream;
        public static bool operator ==(VertexAttributeDescriptor lhs, VertexAttributeDescriptor rhs) => lhs.Equals(rhs);
        public static bool operator !=(VertexAttributeDescriptor lhs, VertexAttributeDescriptor rhs) => !lhs.Equals(rhs);
    }

    public partial struct SubMeshDescriptor
    {
        public SubMeshDescriptor(int indexStart, int indexCount, MeshTopology topology = MeshTopology.Triangles)
        {
            this.indexStart = indexStart;
            this.indexCount = indexCount;
            this.topology = topology;
            bounds = default;
            baseVertex = 0;
            firstVertex = 0;
            vertexCount = 0;
        }

        public override string ToString() =>
            $"(topo={topology} indices={indexStart},{indexCount} vertices={firstVertex},{vertexCount} basevtx={baseVertex} bounds={bounds})";
    }
}

namespace HelixEngine
{
    // The List, range and generic overloads of Mesh.
    public partial class Mesh
    {
        public void GetVertices(List<Vector3> vertices) => Fill(vertices, this.vertices);
        public void GetNormals(List<Vector3> normals) => Fill(normals, this.normals);
        public void GetTangents(List<Vector4> tangents) => Fill(tangents, this.tangents);
        public void GetColors(List<Color> colors) => Fill(colors, this.colors);
        public void GetColors(List<Color32> colors) => Fill(colors, colors32);
        public void GetUVs(int channel, List<Vector2> uvs) => Fill(uvs, GetUVs2(Channel(channel)));
        public void GetUVs(int channel, List<Vector3> uvs) => Fill(uvs, GetUVs3(Channel(channel)));
        public void GetUVs(int channel, List<Vector4> uvs) => Fill(uvs, GetUVs4(Channel(channel)));

        public void SetVertices(List<Vector3> inVertices) => SetVertices(Array(inVertices));
        public void SetVertices(Vector3[] inVertices, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetVertices(Range(inVertices, start, length));
        public void SetVertices(List<Vector3> inVertices, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetVertices(Range(inVertices, start, length));
        public void SetNormals(List<Vector3> inNormals) => SetNormals(Array(inNormals));
        public void SetNormals(Vector3[] inNormals, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetNormals(Range(inNormals, start, length));
        public void SetNormals(List<Vector3> inNormals, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetNormals(Range(inNormals, start, length));
        public void SetTangents(List<Vector4> inTangents) => SetTangents(Array(inTangents));
        public void SetTangents(Vector4[] inTangents, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetTangents(Range(inTangents, start, length));
        public void SetTangents(List<Vector4> inTangents, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetTangents(Range(inTangents, start, length));
        public void SetColors(List<Color> inColors) => SetColors(Array(inColors));
        public void SetColors(Color[] inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetColors(Range(inColors, start, length));
        public void SetColors(List<Color> inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetColors(Range(inColors, start, length));
        public void SetColors(List<Color32> inColors) => SetColors(Array(inColors));
        public void SetColors(Color32[] inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetColors(Range(inColors, start, length));
        public void SetColors(List<Color32> inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetColors(Range(inColors, start, length));
        public void SetUVs(int channel, List<Vector2> uvs) => SetUVs(channel, Array(uvs));
        public void SetUVs(int channel, List<Vector3> uvs) => SetUVs(channel, Array(uvs));
        public void SetUVs(int channel, List<Vector4> uvs) => SetUVs(channel, Array(uvs));
        public void SetUVs(int channel, Vector2[] uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetUVs(channel, Range(uvs, start, length));
        public void SetUVs(int channel, Vector3[] uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetUVs(channel, Range(uvs, start, length));
        public void SetUVs(int channel, Vector4[] uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetUVs(channel, Range(uvs, start, length));
        public void SetUVs(int channel, List<Vector2> uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetUVs(channel, Range(uvs, start, length));
        public void SetUVs(int channel, List<Vector3> uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetUVs(channel, Range(uvs, start, length));
        public void SetUVs(int channel, List<Vector4> uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetUVs(channel, Range(uvs, start, length));

        public void SetTriangles(List<int> triangles, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetTriangles(Array(triangles), submesh, calculateBounds, baseVertex);
        public void SetTriangles(int[] triangles, int trianglesStart, int trianglesLength, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetTriangles(Range(triangles, trianglesStart, trianglesLength), submesh, calculateBounds, baseVertex);
        public void SetTriangles(List<int> triangles, int trianglesStart, int trianglesLength, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetTriangles(Range(triangles, trianglesStart, trianglesLength), submesh, calculateBounds, baseVertex);
        public void SetTriangles(ushort[] triangles, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetTriangles(Widen(triangles), submesh, calculateBounds, baseVertex);
        public void SetTriangles(List<ushort> triangles, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetTriangles(Widen(Array(triangles)), submesh, calculateBounds, baseVertex);
        public void SetTriangles(ushort[] triangles, int trianglesStart, int trianglesLength, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetTriangles(Widen(Range(triangles, trianglesStart, trianglesLength)), submesh, calculateBounds, baseVertex);
        public void SetTriangles(List<ushort> triangles, int trianglesStart, int trianglesLength, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetTriangles(Widen(Range(triangles, trianglesStart, trianglesLength)), submesh, calculateBounds, baseVertex);
        public void GetTriangles(List<int> triangles, int submesh, bool applyBaseVertex = true) => Fill(triangles, GetTriangles(submesh, applyBaseVertex));
        public void GetTriangles(List<ushort> triangles, int submesh, bool applyBaseVertex = true) => Fill(triangles, Narrow(GetTriangles(submesh, applyBaseVertex)));

        public void SetIndices(int[] indices, int indicesStart, int indicesLength, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetIndices(Range(indices, indicesStart, indicesLength), topology, submesh, calculateBounds, baseVertex);
        public void SetIndices(List<int> indices, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetIndices(Array(indices), topology, submesh, calculateBounds, baseVertex);
        public void SetIndices(List<int> indices, int indicesStart, int indicesLength, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetIndices(Range(indices, indicesStart, indicesLength), topology, submesh, calculateBounds, baseVertex);
        public void SetIndices(ushort[] indices, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetIndices(Widen(indices), topology, submesh, calculateBounds, baseVertex);
        public void SetIndices(ushort[] indices, int indicesStart, int indicesLength, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetIndices(Widen(Range(indices, indicesStart, indicesLength)), topology, submesh, calculateBounds, baseVertex);
        public void SetIndices(List<ushort> indices, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetIndices(Widen(Array(indices)), topology, submesh, calculateBounds, baseVertex);
        public void SetIndices(List<ushort> indices, int indicesStart, int indicesLength, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0) =>
            SetIndices(Widen(Range(indices, indicesStart, indicesLength)), topology, submesh, calculateBounds, baseVertex);
        public void GetIndices(List<int> indices, int submesh, bool applyBaseVertex = true) => Fill(indices, GetIndices(submesh, applyBaseVertex));
        public void GetIndices(List<ushort> indices, int submesh, bool applyBaseVertex = true) => Fill(indices, Narrow(GetIndices(submesh, applyBaseVertex)));

        public void SetVertexBufferParams(int vertexCount, params VertexAttributeDescriptor[] attributes) => SetVertexBufferParamsNative(vertexCount, attributes);

        /// <summary>The attributes into the array; their count (at most the array's length).</summary>
        public int GetVertexAttributes(VertexAttributeDescriptor[] attributes)
        {
            if (attributes == null)
                throw new ArgumentNullException(nameof(attributes));
            VertexAttributeDescriptor[] all = GetVertexAttributes();
            int n = Math.Min(all.Length, attributes.Length);
            System.Array.Copy(all, attributes, n);
            return n;
        }

        public int GetVertexAttributes(List<VertexAttributeDescriptor> attributes)
        {
            if (attributes == null)
                throw new ArgumentNullException(nameof(attributes));
            Fill(attributes, GetVertexAttributes());
            return attributes.Count;
        }

        public void SetVertexBufferData<T>(T[] data, int dataStart, int meshBufferStart, int count, int stream = 0, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            where T : struct
        {
            int size = Marshal.SizeOf<T>();
            SetVertexBufferRaw(Bytes<T>(data), dataStart * size, meshBufferStart * size, count * size, stream, flags);
        }

        public void SetVertexBufferData<T>(List<T> data, int dataStart, int meshBufferStart, int count, int stream = 0, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            where T : struct => SetVertexBufferData(Array(data), dataStart, meshBufferStart, count, stream, flags);

        public void SetIndexBufferData<T>(T[] data, int dataStart, int meshBufferStart, int count, MeshUpdateFlags flags = MeshUpdateFlags.Default) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            SetIndexBufferRaw(Bytes<T>(data), dataStart * size, meshBufferStart * size, count * size, flags);
        }

        public void SetIndexBufferData<T>(List<T> data, int dataStart, int meshBufferStart, int count, MeshUpdateFlags flags = MeshUpdateFlags.Default) where T : struct =>
            SetIndexBufferData(Array(data), dataStart, meshBufferStart, count, flags);

        public void SetSubMeshes(List<SubMeshDescriptor> desc, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetSubMeshes(Array(desc), flags);
        public void SetSubMeshes(SubMeshDescriptor[] desc, int start, int count, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetSubMeshes(Range(desc, start, count), flags);
        public void SetSubMeshes(List<SubMeshDescriptor> desc, int start, int count, MeshUpdateFlags flags = MeshUpdateFlags.Default) => SetSubMeshes(Range(desc, start, count), flags);

        static int Channel(int channel)
        {
            if (channel < 0 || channel > 7)
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "The uv index is invalid. Must be in the range 0 to 7.");
            return channel;
        }

        static T[] Array<T>(List<T> list)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));
            return list.ToArray();
        }

        static T[] Range<T>(T[] a, int start, int length)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));
            if (start < 0 || length < 0 || start + length > a.Length)
                throw new ArgumentOutOfRangeException(nameof(start), $"Bad start/length arguments ({start}, {length}) for an array of {a.Length}");
            return a.AsSpan(start, length).ToArray();
        }

        static T[] Range<T>(List<T> list, int start, int length)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));
            if (start < 0 || length < 0 || start + length > list.Count)
                throw new ArgumentOutOfRangeException(nameof(start), $"Bad start/length arguments ({start}, {length}) for a list of {list.Count}");
            return list.GetRange(start, length).ToArray();
        }

        static int[] Widen(ushort[] a)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));
            var r = new int[a.Length];
            for (int i = 0; i < a.Length; i++)
                r[i] = a[i];
            return r;
        }

        static ushort[] Narrow(int[] a)
        {
            var r = new ushort[a.Length];
            for (int i = 0; i < a.Length; i++)
                r[i] = (ushort)a[i];
            return r;
        }

        static byte[] Bytes<T>(T[] data) where T : struct
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            return MemoryMarshal.AsBytes(data.AsSpan()).ToArray();
        }

        static void Fill<T>(List<T> to, T[] from)
        {
            if (to == null)
                throw new ArgumentNullException(nameof(to));
            to.Clear();
            if (from != null)
                to.AddRange(from);
        }
    }
}
