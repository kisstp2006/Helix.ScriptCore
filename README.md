# Helix.ScriptCore

The C# script API of the Helix engine: the `HelixEngine.*` assemblies. They are made of the engine's classes as
Helix.Bindgen generates them from the engine's tagged headers, the interop runtime of Helix.Bindgen, and the C# of this
repo; scripts are classes deriving from `GameBehaviour` or `ScriptableObject`.

## Contents

| Path | Content |
|---|---|
| `Source/HelixEngine.CoreModule/` | `HelixEngine.CoreModule`: the generated engine classes (`Object`, `GameObject`, `Component`, `Behaviour`, `Transform`, `GameBehaviour`, `ScriptableObject`, `Time`, `Application`, `Screen`, `Debug`, `Resources`, `SceneManagement.SceneManager`, `Shader`, `Material`, `Mesh`, `Texture`, `Texture2D`, `MeshFilter`, `Renderer`, `MeshRenderer`, `Camera`, `Light`, `GeometryUtility`, `ScreenCapture`), `DefaultExecutionOrder`, `MissingReferenceException` |
| `Source/HelixEngine.CoreModule/Rendering/` | What C# has in another shape: the keyword structs (`LocalKeyword`, `GlobalKeyword`, `LocalKeywordSpace`, `ShaderTagId`), `MaterialPropertyBlock` (no Object; its native block lives while it does), the `List<T>`, range and generic overloads of `Mesh`, `Material` and `Renderer`, the descriptors' constructors, the stereo eye overloads of `Camera`, `RenderParams` and `Graphics.RenderMesh` / `DrawMesh` |
| `Source/HelixEngine.CoreModule/Collections/` | `Helix.Collections`: `NativeArray<T>` (`ReadOnly`, sub arrays, reinterpretation), `NativeSlice<T>`, `Allocator` (`Temp`, `TempJob`, `Persistent` from the engine's allocators), the container attributes, `AtomicSafetyHandle`, `UnsafeUtility`, `NativeArrayUnsafeUtility`, `NativeLeakDetection` |
| `Source/HelixEngine.CoreModule/Jobs/` | `Helix.Jobs`: `IJob`, `IJobFor`, `IJobParallelFor` and their `Schedule` / `ScheduleParallel` / `Run`, `JobHandle` (dependencies, `CombineDependencies`, `Complete`), `JobsUtility`; `HelixEngine.Jobs`: `TransformAccessArray`, `TransformAccess`, `IJobParallelForTransform` |
| `Source/HelixEngine.CoreModule/Rendering/MeshData.cs` | The `NativeArray` overloads of `Mesh` and `Mesh.MeshData` / `MeshDataArray` (`AcquireReadOnlyMeshData`, `AllocateWritableMeshData`, `ApplyAndDisposeWritableMeshData`) for jobs; `Texture2D.GetPixelData`, `SetPixelData`, `GetRawTextureData<T>`, `LoadRawTextureData<T>` in `Rendering/Textures.cs` |
| `Source/HelixEngine.CoreModule/Math/MathematicsConversions.cs` | Implicit conversions between the value types and Helix.Mathematics (`Vector3` and `float3`, `Quaternion` and `quaternion`, `Matrix4x4` and `float4x4`, ...) |
| `Source/HelixEngine.CoreModule/Math/` | The value types (`Vector2/3/4`, `Vector2Int/3Int`, `Quaternion`, `Matrix4x4`, `Color`, `Color32`, `Rect`, `RectInt`, `Bounds`, `Ray`, `Plane`): fields generated from Helix.Math, the members in C# (the reference's formats and equality), the rest through Helix.Math; `Mathf` |
| `Source/HelixEngine.CoreModule/Scripting/` | `Object` (ToString, generic `Instantiate`), `Debug` (object and format overloads, exceptions), `Scene`, `AsyncOperation`, `ResourceRequest`, the async operations of `SceneManager` and `Resources`, `Events.HelixAction`, `SendMessage` |
| `Tools/ApiInventory/` | `HelixApiInventory`: the public surface of an assembly against its inventory: `Tests/Api/HelixEngine.csv`, `Tests/Api/Helix.Mathematics.csv` (tests; `--write` rewrites one); repos with assemblies of their own use it too |
| `Source/HelixEngine.CoreModule/Scripting/ScriptBridge.cs` | The C# side of scripts: the start of the engine's C# side, script classes of assemblies registered with their messages, the C# objects of scripts, the loops of the phases |
| `Tests/` | C# and C++ scripts in one engine under the .NET host (on the desktop the tests link the shared engine and run once more on the coreclr starter; on the Web they are a .NET web app in node): the order of messages, both languages on one GameObject, exceptions, `AddComponent<T>`, `SendMessage` both ways, a game module loaded, run and unloaded as a shared library, the value types' documented values, scenarios on the script API in both languages with the same results (objects, shaders and materials, meshes and textures, renderers, cameras and lights, ScriptableObjects), ScriptableObject assets read, written and copied, a golden image drawn through the script API on every device (each D3D11 adapter, OpenGL) with the camera messages; the engine's C# test shaders are built against this repo's Helix.Shaders |

## Scripts

- A script's messages (Awake, OnEnable, Start, FixedUpdate, Update, LateUpdate, OnDisable, OnDestroy, the
  application, hierarchy, visibility and camera messages) are found once per class by reflection: any access, empty
  methods too, also in C# base classes. `DefaultExecutionOrder` orders the class.
- `ScriptableObject` scripts of both languages: `CreateInstance` (a type, `<T>`, a name), Awake and OnEnable when one
  is made, read from an asset (`Resources.Load`) or copied (`Instantiate`), OnDisable and OnDestroy when it is
  destroyed; their fields are serialized as GameBehaviour scripts' are (class 114 documents).
- The engine runs one scheduler for both languages: within one execution order the C++ scripts of a phase run first,
  then the C# ones in one call into C#, which calls each script in its own try (an exception is logged with the
  script and its GameObject, the others still run).
- A C# script is the wrapper of its native GameBehaviour, held strongly while it lives; its constructor runs before
  Awake, as in the reference.
- C++ scripts derive from `HelixEngine::GameBehaviour` of the engine's C++ script API (`<HelixEngine/HelixEngineApi.h>`)
  and use the same names: `transform.position = v;`, `GameObject::Find("Player")`, `GetComponent<T>()`,
  `Debug::Log(...)`. Both languages call the same functions of the engine, so they give the same results; the value
  types are Helix.Math's in C++ and their C# structs share the layout; `Random` is one generator for both.

- Serialized fields, by the reference's rules: public fields and those with `SerializeField` that are not static,
  const, readonly or `NonSerialized`; numbers, bools, chars, enums of 32 bits or less, strings, `Object` references,
  the value types of HelixEngine, `[Serializable]` classes and structs inline (null becomes an object with default
  fields; nesting stops at depth 10 with the reference's warning), fixed size buffers, and one dimensional arrays and
  `List<T>` of those. Each class's fields are found once by reflection and compiled to one delegate over
  Helix.Serialization's C ABI, so C# fields reach every visitor of the engine as C++ fields do.
- `SerializeReference` fields (classes, abstract classes, interfaces, object, and arrays and lists of them) hold
  references to objects of `[Serializable]` classes: null, derived types, one object in several fields and cycles,
  within one host object, in the reference's layout (`rid` fields, a `references` section of version 2 after the
  fields); objects keep their rid across writes.
- `ISerializationCallbackReceiver`: `OnBeforeSerialize` before an object is written, `OnAfterDeserialize` after it is
  read (the host's, then the objects it holds).

- Coroutines: `StartCoroutine` (an enumerator or a method name), `StopCoroutine` (three kinds), `StopAllCoroutines`,
  `destroyCancellationToken`, and the yield instructions (`WaitForSeconds`, `WaitForSecondsRealtime`,
  `WaitForFixedUpdate`, `WaitForEndOfFrame`, `WaitUntil` and `WaitWhile` with their timeouts, custom instructions,
  nested enumerators and coroutines, `AsyncOperation`, `Awaitable`), stepped in C# and resumed by the engine's
  scheduler at the same points as C++ coroutines.
- `Awaitable` and `Awaitable<T>`: pooled, `async` methods may return them; `NextFrameAsync`, `WaitForSecondsAsync`,
  `FixedUpdateAsync`, `EndOfFrameAsync`, `FromAsyncOperation` on the scheduler's points, `BackgroundThreadAsync` and
  `MainThreadAsync` (without threads, on the Web, the background continues on the main thread), cancellation,
  `AwaitableCompletionSource`; the main thread's SynchronizationContext runs posted
  continuations (Tasks') in the Update phase; `Application.exitCancellationToken`.

## Jobs and native containers

- Jobs are structs of plain values and native containers. One generic entry point per job type is made the first time
  it is scheduled; the engine's job system runs it on its workers (`JobsUtility.JobWorkerCount`), the main thread
  helps while it waits in `Complete`.
- Containers live in native memory: `Temp` (the frame's memory, freed at its end), `TempJob` (warned about after four
  frames) and `Persistent`. Copies of a container share its memory; C++ code reads them as spans.
- Development builds check their use (`HELIX_COLLECTIONS_CHECKS=1` turns the checks on elsewhere): use after Dispose,
  the main thread or a job touching a container a scheduled job writes without a dependency, writes to `[ReadOnly]`
  containers, a parallel job writing outside its range, and containers never
  disposed (reported at exit), with the reference's messages.
- `Mesh.MeshData` gives jobs a mesh's vertex and index data where they are: a copy for `AcquireReadOnlyMeshData`, a
  new mesh's data for `AllocateWritableMeshData`, applied to meshes on the main thread.

Release, Ryzen 7 3750H:

| Benchmark | Result |
|---|---|
| One Update phase over 10 000 C# scripts | 0.15-0.18 ms |
| A parallel for over 1 048 576 floats (sqrt and sin each), 7 workers | 3.6 ms (a C# loop on the main thread: 10.8-12.4 ms) |

## Build

```
git clone https://github.com/kisstp2006/Helix.ScriptCore
git -C Helix.ScriptCore submodule update --init
cmake --preset windows-debug
cmake --build --preset windows-debug
ctest --preset windows-debug
```

The .NET 11 SDK is needed (found through `HELIX_DOTNET_ROOT`, `DOTNET_ROOT` or the path).

## License

GPL-3.0, see `LICENSE`.
