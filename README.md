# Helix.ScriptCore

The C# script API of the Helix engine: the `HelixEngine.*` assemblies. They are made of the engine's classes as
Helix.Bindgen generates them from the engine's tagged headers, the interop runtime of Helix.Bindgen, and the C# of this
repo; scripts are classes deriving from `GameBehaviour`.

## Contents

| Path | Content |
|---|---|
| `Source/HelixEngine.CoreModule/` | `HelixEngine.CoreModule`: the generated engine classes (`Object`, `GameObject`, `Component`, `Behaviour`, `GameBehaviour`), `DefaultExecutionOrder`, `MissingReferenceException` |
| `Source/HelixEngine.CoreModule/Scripting/ScriptBridge.cs` | The C# side of scripts: the start of the engine's C# side, script classes of assemblies registered with their messages, the C# objects of scripts, the loops of the phases |
| `Tests/` | C# and C++ scripts in one engine under the .NET host (the tests link the shared engine): the order of messages, both languages on one GameObject, exceptions, `AddComponent<T>`, `SendMessage` both ways, a game module loaded, run and unloaded as a shared library |

## Scripts

- A script's messages (Awake, OnEnable, Start, FixedUpdate, Update, LateUpdate, OnDisable, OnDestroy, the
  application, hierarchy and visibility messages) are found once per class by reflection: any access, empty methods
  too, also in C# base classes. `DefaultExecutionOrder` orders the class.
- The engine runs one scheduler for both languages: within one execution order the C++ scripts of a phase run first,
  then the C# ones in one call into C#, which calls each script in its own try (an exception is logged with the
  script and its GameObject, the others still run).
- A C# script is the wrapper of its native GameBehaviour, held strongly while it lives; its constructor runs before
  Awake, as in the reference.

Release, Ryzen 7 3750H:

| Benchmark | Result |
|---|---|
| One Update phase over 10 000 C# scripts | 0.15-0.18 ms |

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
