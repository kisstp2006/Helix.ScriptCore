cmake_minimum_required(VERSION 3.28)
project(Helix.ScriptCore LANGUAGES CXX)

if(NOT COMMAND helix_add_library)
    include("${CMAKE_CURRENT_SOURCE_DIR}/External/Helix.CMake/Helix.cmake")
endif()

helix_require(Engine)
helix_require(Scripting)

# HelixEngine.CoreModule: the engine's generated C# (HELIX_ENGINE_BINDINGS_CS, made with the engine library), the
# interop runtime of Helix.Bindgen and the C# of this repo, built with the .NET SDK of the build host.
find_program(HELIX_DOTNET NAMES dotnet HINTS "$ENV{HELIX_DOTNET_ROOT}" "$ENV{DOTNET_ROOT}" REQUIRED)
set(scriptcore_artifacts "${CMAKE_BINARY_DIR}/HelixScriptCore")
set(core_module_dir "${CMAKE_CURRENT_SOURCE_DIR}/Source/HelixEngine.CoreModule")
set(core_module_dll "${scriptcore_artifacts}/bin/HelixEngine.CoreModule/release/HelixEngine.CoreModule.dll")
file(GLOB_RECURSE core_module_sources CONFIGURE_DEPENDS "${core_module_dir}/*.cs" "${core_module_dir}/*.csproj")
list(FILTER core_module_sources EXCLUDE REGEX "/(bin|obj)/")
helix_dependency_dir(engine_dir Engine)
file(GLOB_RECURSE engine_headers CONFIGURE_DEPENDS "${engine_dir}/Source/Helix/Engine/*.h")
if(TARGET helix_scripting_runtime)
    set(runtime_arguments "-p:HelixScriptingRuntime=${HELIX_SCRIPTING_RUNTIME}")
endif()
add_custom_command(
    OUTPUT "${core_module_dll}"
    COMMAND "${HELIX_DOTNET}" build "${core_module_dir}/HelixEngine.CoreModule.csproj" -c Release --artifacts-path "${scriptcore_artifacts}"
            --nologo -v quiet "-p:HelixEngineBindings=${HELIX_ENGINE_BINDINGS_CS}" "-p:HelixInteropFile=${HELIX_BINDGEN_RUNTIME}" ${runtime_arguments}
    DEPENDS ${core_module_sources} ${engine_headers} "${HELIX_BINDGEN_RUNTIME}"
    COMMENT "Building HelixEngine.CoreModule")
add_custom_target(helix_core_module ALL DEPENDS "${core_module_dll}")
# The engine library makes the generated C#; the host's managed side is referenced.
add_dependencies(helix_core_module helix_engine)
if(TARGET helix_scripting_runtime)
    add_dependencies(helix_core_module helix_scripting_runtime)
endif()
set(HELIX_CORE_MODULE "${core_module_dll}" CACHE INTERNAL "")

# Tests: C# scripts (an assembly of test scripts) and C++ scripts (a game module of the test program) in one engine,
# run by the .NET host of the desktop.
helix_add_test_suite(helix_scriptcore_tests EXCLUDE "/Managed/" DEPS Helix::Engine Helix::Scripting)
helix_add_benchmark_suite(helix_scriptcore_bench DEPS Helix::Engine Helix::Scripting)
if(TARGET helix_scriptcore_tests OR TARGET helix_scriptcore_bench)
    include("${HELIX_BINDGEN_ROOT}/CMake/HelixBindgen.cmake")
    set(tests_dir "${CMAKE_CURRENT_SOURCE_DIR}/Tests")
    set(script_tests_dll "${scriptcore_artifacts}/bin/HelixEngine.ScriptTests/release/HelixEngine.ScriptTests.dll")
    file(GLOB_RECURSE script_tests_sources CONFIGURE_DEPENDS "${tests_dir}/Managed/*.cs" "${tests_dir}/Managed/*.csproj")
    list(FILTER script_tests_sources EXCLUDE REGEX "/(bin|obj)/")
    add_custom_command(
        OUTPUT "${script_tests_dll}"
        COMMAND "${HELIX_DOTNET}" build "${tests_dir}/Managed/HelixEngine.ScriptTests.csproj" -c Release --artifacts-path "${scriptcore_artifacts}"
                --nologo -v quiet "-p:HelixCoreModule=${core_module_dll}" ${runtime_arguments}
        DEPENDS ${script_tests_sources} "${core_module_dll}"
        COMMENT "Building the C# test scripts")
    add_custom_target(helix_scriptcore_test_scripts DEPENDS "${script_tests_dll}")
    add_dependencies(helix_scriptcore_test_scripts helix_core_module)
    set(engine_source "${engine_dir}/Source")
    set(engine_binding_headers "${engine_source}/Helix/Engine/Object/EntityId.h" "${engine_source}/Helix/Engine/Object/Object.h"
        "${engine_source}/Helix/Engine/GameObject/GameObject.h" "${engine_source}/Helix/Engine/Script/GameBehaviour.h")
    foreach(target IN ITEMS helix_scriptcore_tests helix_scriptcore_bench)
        if(TARGET ${target})
            add_dependencies(${target} helix_scriptcore_test_scripts)
            target_include_directories(${target} PRIVATE "${tests_dir}")
            target_compile_definitions(${target} PRIVATE "HX_TEST_RUNTIME_ASSEMBLY=\"${HELIX_SCRIPTING_RUNTIME}\""
                                                         "HX_TEST_CORE_MODULE=\"${core_module_dll}\"" "HX_TEST_SCRIPTS=\"${script_tests_dll}\"")
        endif()
    endforeach()
    if(TARGET helix_scriptcore_tests)
        helix_bindgen(helix_scriptcore_tests MODULE ScriptCoreTests NAMESPACE ScriptCoreTests KIND NativeGame
                      HEADERS "${tests_dir}/Cpp/CppScripts.h" REFERENCES ${engine_binding_headers} INCLUDE_ROOTS "${tests_dir}" "${engine_source}")
    endif()
endif()
