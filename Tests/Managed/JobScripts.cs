using System;
using System.Collections.Generic;
using Helix.Collections;
using Helix.Collections.LowLevel.Unsafe;
using Helix.Jobs;
using Helix.Jobs.LowLevel.Unsafe;
using Helix.Mathematics;
using HelixEngine;
using HelixEngine.Jobs;
using HelixEngine.Rendering;

// The C# job system and the core containers: jobs of each kind on the workers, dependencies, the safety system.
namespace Tests
{
    struct FillJob : IJob
    {
        public NativeArray<int> data;
        public int value;
        public void Execute()
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = value + i;
        }
    }

    struct SquareJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> input;
        public NativeArray<long> output;
        [NativeSetThreadIndex] public int threadIndex;
        public NativeArray<int> threads;
        public void Execute(int index)
        {
            output[index] = (long)input[index] * input[index];
            threads[index] = threadIndex;
        }
    }

    struct PrefixJob : IJobFor
    {
        public NativeArray<int> data;
        public void Execute(int index)
        {
            if (index > 0)
                data[index] += data[index - 1];
        }
    }

    struct ScaleJob : IJobFor
    {
        public NativeArray<float> data;
        public float factor;
        public void Execute(int index) => data[index] *= factor;
    }

    struct SumJob : IJob
    {
        [ReadOnly] public NativeArray<int> input;
        public NativeArray<int> sum;
        public void Execute()
        {
            int s = 0;
            foreach (int v in input)
                s += v;
            sum[0] = s;
        }
    }

    struct WriteJob : IJob
    {
        public NativeArray<int> data;
        public void Execute() => data[0] = 1;
    }

    struct ReadOnlyWriteJob : IJob
    {
        [ReadOnly] public NativeArray<int> data;
        public void Execute() => data[0] = 1;
    }

    struct NeighbourJob : IJobParallelFor
    {
        public NativeArray<int> data;
        public void Execute(int index) => data[(index + 1) % data.Length] = index;
    }

    struct FreeNeighbourJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<int> data;
        public void Execute(int index) => data[index] = index;
    }

    struct ReferenceJob : IJob
    {
        public string text;
        public void Execute() { }
    }

    struct DeallocateJob : IJob
    {
        [DeallocateOnJobCompletion] public NativeArray<int> data;
        public NativeArray<int> result;
        public void Execute() => result[0] = data[0];
    }

    struct MoveJob : IJobParallelForTransform
    {
        public float3 offset;
        public NativeArray<float3> positions;
        public void Execute(int index, TransformAccess transform)
        {
            transform.localPosition = (float3)transform.localPosition + offset;
            positions[index] = transform.position;
        }
    }

    public class CsJobs : GameBehaviour
    {
        static string Fail(Action a)
        {
            try
            {
                a();
                return "none";
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        void Start()
        {
            var notes = new List<string>();
            // IJob.
            var data = new NativeArray<int>(100, Allocator.TempJob);
            new FillJob { data = data, value = 5 }.Schedule().Complete();
            notes.Add("fill=" + data[0] + "," + data[99]);

            // IJobParallelFor on the workers, a thread index each.
            var input = new NativeArray<int>(10000, Allocator.TempJob);
            for (int i = 0; i < input.Length; i++)
                input[i] = i;
            var output = new NativeArray<long>(10000, Allocator.TempJob);
            var threads = new NativeArray<int>(10000, Allocator.TempJob);
            JobHandle square = new SquareJob { input = input, output = output, threads = threads }.Schedule(input.Length, 64);
            square.Complete();
            bool squares = true;
            int maxThread = 0;
            for (int i = 0; i < input.Length; i++)
            {
                squares &= output[i] == (long)i * i;
                maxThread = Math.Max(maxThread, threads[i]);
            }
            notes.Add("squares=" + squares + ";threads=" + (maxThread < JobsUtility.ThreadIndexCount));

            // IJobFor: in order on one worker; in parallel.
            var prefix = new NativeArray<int>(new[] { 1, 2, 3, 4, 5 }, Allocator.TempJob);
            new PrefixJob { data = prefix }.Schedule(prefix.Length, default).Complete();
            var floats = new NativeArray<float>(1000, Allocator.TempJob);
            for (int i = 0; i < floats.Length; i++)
                floats[i] = 1;
            new ScaleJob { data = floats, factor = 3 }.ScheduleParallel(floats.Length, 32, default).Complete();
            notes.Add("prefix=" + string.Join(",", prefix.ToArray()) + ";scaled=" + floats[999]);

            // Dependencies: a reader after a writer, combined handles, Dispose after a job.
            var sum = new NativeArray<int>(1, Allocator.TempJob);
            JobHandle fill = new FillJob { data = data, value = 0 }.Schedule();
            JobHandle sumJob = new SumJob { input = data, sum = sum }.Schedule(fill);
            JobHandle both = JobHandle.CombineDependencies(sumJob, new ScaleJob { data = floats, factor = 2 }.Schedule(floats.Length, default));
            both.Complete();
            notes.Add("sum=" + sum[0] + ";both=" + floats[0] + ";done=" + both.IsCompleted);
            JobHandle disposed = input.Dispose(default(JobHandle));
            disposed.Complete();
            notes.Add("disposed=" + input.IsCreated);

            // Temp memory, sub arrays, slices, reinterpretation.
            var temp = new NativeArray<int>(new[] { 10, 20, 30, 40 }, Allocator.Temp);
            NativeArray<int> sub = temp.GetSubArray(1, 2);
            NativeSlice<int> slice = temp.Slice(2);
            NativeArray<byte> bytes = temp.Reinterpret<byte>(4);
            notes.Add("sub=" + sub[0] + "," + sub[1] + ";slice=" + slice[1] + ";bytes=" + bytes.Length + "," + bytes[4] + ";load=" + temp.ReinterpretLoad<short>(3));
            temp.Dispose();

            // Mathematics in a job's data.
            var points = new NativeArray<float3>(2, Allocator.Temp);
            points[0] = new float3(1, 2, 3);
            Vector3 v = points[0];
            notes.Add("float3=" + v);
            points.Dispose();

            output.Dispose();
            threads.Dispose();
            prefix.Dispose();
            floats.Dispose();
            sum.Dispose();
            data.Dispose();
            Notes.NoteRaw("jobs:" + string.Join(";", notes));
        }
    }

    // Each error the safety system reports (development builds).
    public class CsJobSafety : GameBehaviour
    {
        static string Fail(Action a)
        {
            try
            {
                a();
                return "none";
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        void Start()
        {
            if (!CollectionChecksProbe.Enabled)
            {
                Notes.NoteRaw("safety:off");
                return;
            }
            var data = new NativeArray<int>(4, Allocator.TempJob);
            JobHandle write = new WriteJob { data = data }.Schedule();
            Notes.NoteRaw("safety:read=" + Fail(() => { int x = data[0]; }));
            Notes.NoteRaw("safety:schedule=" + Fail(() => new WriteJob { data = data }.Schedule()));
            Notes.NoteRaw("safety:dispose=" + Fail(() => data.Dispose()));
            JobHandle after = new SumJob { input = data, sum = new NativeArray<int>(1, Allocator.Temp) }.Schedule(write);
            after.Complete();
            Notes.NoteRaw("safety:after=" + Fail(() => { int x = data[0]; }));
            var copy = data;
            data.Dispose();
            Notes.NoteRaw("safety:disposed=" + Fail(() => { int x = copy[0]; }));
            Notes.NoteRaw("safety:index=" + Fail(() =>
            {
                var a = new NativeArray<int>(2, Allocator.Temp);
                a[2] = 1;
            }));
            Notes.NoteRaw("safety:references=" + Fail(() => new ReferenceJob().Schedule()));
            var ro = new NativeArray<int>(1, Allocator.TempJob);
            new ReadOnlyWriteJob { data = ro }.Schedule().Complete();
            var range = new NativeArray<int>(64, Allocator.TempJob);
            new NeighbourJob { data = range }.Schedule(range.Length, 8).Complete();
            new FreeNeighbourJob { data = range }.Schedule(range.Length, 8).Complete();
            Notes.NoteRaw("safety:free=" + range[63]);
            var owned = new NativeArray<int>(new[] { 7 }, Allocator.TempJob);
            var result = new NativeArray<int>(1, Allocator.TempJob);
            new DeallocateJob { data = owned, result = result }.Schedule().Complete();
            Notes.NoteRaw("safety:deallocated=" + result[0] + "," + Fail(() => { int x = owned[0]; }));
            ro.Dispose();
            range.Dispose();
            result.Dispose();
            var leak = new NativeArray<int>(1, Allocator.Persistent);
            Notes.NoteRaw("safety:leaks=" + LeakProbe.Report());
        }
    }

    // IJobParallelForTransform over several roots.
    public class CsTransformJobs : GameBehaviour
    {
        void Start()
        {
            var transforms = new TransformAccessArray(6);
            var roots = new List<GameObject>();
            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject("Root" + i);
                root.transform.position = new Vector3(i * 10, 0, 0);
                var child = new GameObject("Child" + i);
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(0, 1, 0);
                transforms.Add(root.transform);
                transforms.Add(child.transform);
                roots.Add(root);
            }
            var positions = new NativeArray<float3>(transforms.length, Allocator.TempJob);
            new MoveJob { offset = new float3(0, 0, 1), positions = positions }.Schedule(transforms).Complete();
            var line = new List<string>();
            for (int i = 0; i < transforms.length; i++)
                line.Add(transforms[i].name + "=" + transforms[i].position + "/" + (Vector3)positions[i]);
            positions.Dispose();
            transforms.Dispose();
            foreach (GameObject g in roots)
                DestroyImmediate(g);
            Notes.NoteRaw("transforms:" + string.Join(";", line));
        }
    }

    // Reads a mesh copy in a job.
    struct ReadMeshJob : IJob
    {
        [ReadOnly] public Mesh.MeshDataArray meshes;
        public NativeArray<float> result;
        public void Execute()
        {
            Mesh.MeshData data = meshes[0];
            var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            data.GetVertices(vertices);
            var indices = new NativeArray<int>(6, Allocator.Temp);
            data.GetIndices(indices, 0);
            float sum = 0;
            for (int i = 0; i < vertices.Length; i++)
                sum += vertices[i].x + vertices[i].y;
            result[0] = sum;
            result[1] = indices[5];
            result[2] = data.GetVertexData<byte>(0).Length / data.GetVertexBufferStride(0);
            vertices.Dispose();
            indices.Dispose();
        }
    }

    // Builds a mesh in a job.
    struct BuildMeshJob : IJob
    {
        public Mesh.MeshDataArray meshes;
        public void Execute()
        {
            Mesh.MeshData data = meshes[0];
            data.SetVertexBufferParams(4, new VertexAttributeDescriptor(VertexAttribute.Position));
            data.SetIndexBufferParams(6, IndexFormat.UInt16);
            NativeArray<float3> positions = data.GetVertexData<float3>();
            positions[0] = new float3(0, 0, 0);
            positions[1] = new float3(2, 0, 0);
            positions[2] = new float3(2, 4, 0);
            positions[3] = new float3(0, 4, 0);
            NativeArray<ushort> indices = data.GetIndexData<ushort>();
            ushort[] quad = { 0, 1, 2, 0, 2, 3 };
            for (int i = 0; i < 6; i++)
                indices[i] = quad[i];
            data.subMeshCount = 1;
            data.SetSubMesh(0, new SubMeshDescriptor(0, 6));
        }
    }

    // NativeArrays in the engine API: Mesh and Texture2D overloads, Mesh.MeshData in jobs.
    public class CsNativeMeshes : GameBehaviour
    {
        void Start()
        {
            var line = new List<string>();
            var mesh = new Mesh();
            var positions = new NativeArray<float3>(4, Allocator.Temp);
            positions[0] = new float3(0, 0, 0);
            positions[1] = new float3(1, 0, 0);
            positions[2] = new float3(1, 1, 0);
            positions[3] = new float3(0, 1, 0);
            mesh.SetVertices(positions);
            var quad = new NativeArray<ushort>(new ushort[] { 0, 1, 2, 0, 2, 3 }, Allocator.Temp);
            mesh.SetIndices(quad, MeshTopology.Triangles, 0);
            var colors = new NativeArray<Color32>(4, Allocator.Temp);
            colors[2] = new Color32(10, 20, 30, 40);
            mesh.SetColors(colors);
            line.Add("mesh=" + mesh.vertexCount + "," + string.Join(" ", mesh.triangles) + "," + mesh.colors32[2]);
            line.Add("wrongsize=" + Fail(() => mesh.SetVertices(new NativeArray<float2>(4, Allocator.Temp))));
            Mesh.MeshDataArray probe = Mesh.AcquireReadOnlyMeshData(mesh);
            line.Add("stride=" + probe[0].GetVertexBufferStride(0) + "," + Fail(() => probe[0].GetVertexData<float3>()));
            probe.Dispose();

            Mesh.MeshDataArray read = Mesh.AcquireReadOnlyMeshData(mesh);
            var result = new NativeArray<float>(3, Allocator.TempJob);
            new ReadMeshJob { meshes = read, result = result }.Schedule().Complete();
            line.Add("read=" + result[0] + "," + result[1] + "," + result[2] + "," + read[0].subMeshCount + "," + read[0].indexFormat);
            Mesh.MeshDataArray readOnly = read;
            line.Add("readonlywrite=" + Fail(() => readOnly[0].SetIndexBufferParams(3, IndexFormat.UInt32)));
            line.Add("readonlyapply=" + Fail(() => Mesh.ApplyAndDisposeWritableMeshData(readOnly, mesh)));
            read.Dispose();
            // A second Dispose (of a copy) is caught by the safety checks only (development builds).
            string disposed = CollectionChecksProbe.Enabled ? Fail(() => readOnly.Dispose()) : null;
            result.Dispose();

            Mesh.MeshDataArray write = Mesh.AllocateWritableMeshData(1);
            new BuildMeshJob { meshes = write }.Schedule().Complete();
            var built = new Mesh();
            Mesh.ApplyAndDisposeWritableMeshData(write, built);
            line.Add("built=" + built.vertexCount + "," + string.Join(" ", built.triangles) + "," + built.vertices[2] + "," + built.bounds.size);

            var raw = new Mesh();
            raw.SetVertexBufferParams(2, new NativeArray<VertexAttributeDescriptor>(new[] { new VertexAttributeDescriptor(VertexAttribute.Position) }, Allocator.Temp));
            var rawData = new NativeArray<float3>(2, Allocator.Temp);
            rawData[1] = new float3(5, 6, 7);
            raw.SetVertexBufferData(rawData, 0, 0, 2);
            line.Add("raw=" + raw.vertices[1]);

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
            pixels[3] = new Color32(1, 2, 3, 4);
            var bytes = new NativeArray<byte>(16, Allocator.Temp);
            bytes[0] = 200;
            Color32 written = texture.GetPixels32()[3];
            texture.SetPixelData(bytes, 0);
            line.Add("texture=" + pixels.Length + "," + written + "," + texture.GetPixels32()[0].r + "," + texture.GetRawTextureData<uint>().Length);
            texture.LoadRawTextureData(new NativeArray<uint>(new uint[] { 0xff, 0, 0, 0 }, Allocator.Temp));
            line.Add("load=" + texture.GetPixels32()[0]);

            DestroyImmediate(mesh);
            DestroyImmediate(built);
            DestroyImmediate(raw);
            DestroyImmediate(texture);
            Notes.NoteRaw("native:" + string.Join(";", line));
            if (disposed != null)
                Notes.NoteRaw("native:disposed=" + disposed);
        }

        static string Fail(Action a)
        {
            try
            {
                a();
                return "none";
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }
    }

    struct BenchJob : IJobParallelFor
    {
        public NativeArray<float> values;
        public void Execute(int index)
        {
            float v = values[index];
            values[index] = math.sqrt(v * v + 1f) * 0.5f + math.sin(v);
        }
    }

    // A parallel for over a million floats against the same loop on the main thread (timings printed, results compared).
    public class CsJobBenchmark : GameBehaviour
    {
        void Start()
        {
            const int n = 1 << 20, rounds = 10;
            var values = new NativeArray<float>(n, Allocator.TempJob);
            var managed = new float[n];
            for (int i = 0; i < n; i++)
                values[i] = managed[i] = i * 0.001f;
            new BenchJob { values = values }.Schedule(n, 4096).Complete();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int r = 0; r < rounds; r++)
                new BenchJob { values = values }.Schedule(n, 4096).Complete();
            double jobs = watch.Elapsed.TotalMilliseconds / rounds;
            for (int i = 0; i < n; i++)
                managed[i] = math.sqrt(managed[i] * managed[i] + 1f) * 0.5f + math.sin(managed[i]);
            watch.Restart();
            for (int r = 0; r < rounds; r++)
                for (int i = 0; i < n; i++)
                    managed[i] = math.sqrt(managed[i] * managed[i] + 1f) * 0.5f + math.sin(managed[i]);
            double loop = watch.Elapsed.TotalMilliseconds / rounds;
            bool same = true;
            for (int i = 0; i < n && same; i++)
                same = values[i] == managed[i];
            values.Dispose();
            Notes.NoteRaw($"bench:same={same};workers={JobsUtility.JobWorkerCount}");
            Debug.Log($"parallel for over {n} floats: {jobs:F2} ms in jobs, {loop:F2} ms in a C# loop ({loop / jobs:F1}x)");
        }
    }

    static class CollectionChecksProbe
    {
        internal static bool Enabled => (bool)typeof(AtomicSafetyHandle).Assembly.GetType("Helix.Collections.LowLevel.Unsafe.CollectionChecks")
            .GetField("Enabled", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
    }

    static class LeakProbe
    {
        internal static int Report() => (int)typeof(AtomicSafetyHandle).Assembly.GetType("Helix.Collections.LowLevel.Unsafe.LeakTracker")
            .GetMethod("Report", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
    }
}
