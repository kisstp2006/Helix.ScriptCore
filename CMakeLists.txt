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
helix_dependency_dir(mathematics_dir Mathematics)
file(GLOB_RECURSE core_module_sources CONFIGURE_DEPENDS "${core_module_dir}/*.cs" "${core_module_dir}/*.csproj" "${mathematics_dir}/Source/*.cs"
     "${mathematics_dir}/Source/*.csproj")
list(FILTER core_module_sources EXCLUDE REGEX "/(bin|obj)/")
helix_dependency_dir(engine_dir Engine)
file(GLOB_RECURSE engine_headers CONFIGURE_DEPENDS "${engine_dir}/Source/Helix/Engine/*.h")
if(TARGET helix_scripting_runtime)
    set(runtime_arguments "-p:HelixScriptingRuntime=${HELIX_SCRIPTING_RUNTIME}")
endif()
get_filename_component(core_module_bin "${core_module_dll}" DIRECTORY)
add_custom_command(
    OUTPUT "${core_module_dll}" "${core_module_bin}/Helix.Mathematics.dll"
    COMMAND "${HELIX_DOTNET}" build "${core_module_dir}/HelixEngine.CoreModule.csproj" -c Release --artifacts-path "${scriptcore_artifacts}"
            --nologo -v quiet "-p:HelixEngineBindings=${HELIX_ENGINE_BINDINGS_CS}" "-p:HelixInteropFile=${HELIX_BINDGEN_RUNTIME}" ${runtime_arguments}
            "-p:HelixMathematicsDir=${mathematics_dir}"
    DEPENDS ${core_module_sources} ${engine_headers} "${HELIX_BINDGEN_RUNTIME}" "${HELIX_ENGINE_BINDINGS_CS}"
    COMMENT "Building HelixEngine.CoreModule")
add_custom_target(helix_core_module ALL DEPENDS "${core_module_dll}")
# The engine library makes the generated C#; the host's managed side is referenced.
add_dependencies(helix_core_module helix_engine)
if(TARGET helix_scripting_runtime)
    add_dependencies(helix_core_module helix_scripting_runtime)
endif()
set(HELIX_CORE_MODULE "${core_module_dll}" CACHE INTERNAL "")
# The assemblies HelixEngine.CoreModule references, next to it.
set(HELIX_CORE_MODULE_REFERENCES "${core_module_bin}/Helix.Mathematics.dll" CACHE INTERNAL "")
# Helix::ScriptCore for the repos that take the assembly (HELIX_CORE_MODULE): linking it builds the assembly first.
add_library(helix_scriptcore INTERFACE)
add_library(Helix::ScriptCore ALIAS helix_scriptcore)
add_dependencies(helix_scriptcore helix_core_module)

# Tests: C# scripts (an assembly of test scripts) and C++ scripts (the test program's own game module) in one engine.
# On the desktop they link the shared engine, as the player does, and load a game module of their own (a shared
# library); on the Web they are a .NET web app run in node (no shared libraries: the loaded module is the desktop's
# test). Android builds the assemblies only.
if(HELIX_PLATFORM STREQUAL "windows" OR HELIX_PLATFORM STREQUAL "linux")
    helix_add_test_suite(helix_scriptcore_tests EXCLUDE "/(Managed|Module)/" DEPS Helix::EngineShared)
    helix_add_benchmark_suite(helix_scriptcore_bench DEPS Helix::EngineShared)
elseif(HELIX_PLATFORM STREQUAL "web")
    helix_add_test_suite(helix_scriptcore_tests DOTNET EXCLUDE "/(Managed|Module)/" DEPS Helix::Engine)
endif()
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
        "${engine_source}/Helix/Engine/GameObject/GameObject.h" "${engine_source}/Helix/Engine/Script/GameBehaviour.h"
        "${engine_source}/Helix/Engine/Script/ScriptableObject.h")
    foreach(target IN ITEMS helix_scriptcore_tests helix_scriptcore_bench)
        if(TARGET ${target})
            add_dependencies(${target} helix_scriptcore_test_scripts)
            target_include_directories(${target} PRIVATE "${tests_dir}")
            target_compile_definitions(${target} PRIVATE "HX_TEST_RUNTIME_ASSEMBLY=\"${HELIX_SCRIPTING_RUNTIME}\""
                                                         "HX_TEST_CORE_MODULE=\"${core_module_dll}\"" "HX_TEST_SCRIPTS=\"${script_tests_dll}\"")
        endif()
    endforeach()
    if(TARGET helix_scriptcore_tests)
        # The app's assemblies on the Web.
        set_property(TARGET helix_scriptcore_tests APPEND PROPERTY HELIX_DOTNET_ASSEMBLIES "${core_module_dll}" ${HELIX_CORE_MODULE_REFERENCES} "${script_tests_dll}")
        if(TARGET helix_scriptcore_tests_web)
            add_dependencies(helix_scriptcore_tests_web helix_scriptcore_test_scripts helix_core_module)
        endif()
        helix_bindgen(helix_scriptcore_tests MODULE ScriptCoreTests NAMESPACE ScriptCoreTests KIND NativeGame
                      HEADERS "${tests_dir}/Cpp/CppScripts.h" "${tests_dir}/Cpp/CppRenderingScripts.h" REFERENCES ${engine_binding_headers} REFERENCE_NAMESPACE HelixEngine INCLUDE_ROOTS "${tests_dir}" "${engine_source}")
    endif()
    if(TARGET helix_scriptcore_tests AND TARGET helix_engine_shared)
        # A game module loaded at run time: its file name is its module name.
        add_library(helix_scriptcore_test_module SHARED "${tests_dir}/Module/ModuleScripts.h")
        set_target_properties(helix_scriptcore_test_module PROPERTIES OUTPUT_NAME ScriptCoreModule PREFIX "" LINKER_LANGUAGE CXX)
        target_link_libraries(helix_scriptcore_test_module PRIVATE Helix::EngineShared)
        target_include_directories(helix_scriptcore_test_module PRIVATE "${tests_dir}")
        helix_apply_options(helix_scriptcore_test_module)
        helix_bindgen(helix_scriptcore_test_module MODULE ScriptCoreModule NAMESPACE ScriptCoreModule KIND NativeGame
                      HEADERS "${tests_dir}/Module/ModuleScripts.h" REFERENCES ${engine_binding_headers} REFERENCE_NAMESPACE HelixEngine INCLUDE_ROOTS "${tests_dir}" "${engine_source}")
        add_dependencies(helix_scriptcore_tests helix_scriptcore_test_module)
        target_compile_definitions(helix_scriptcore_tests PRIVATE "HX_TEST_MODULE=\"$<TARGET_FILE:helix_scriptcore_test_module>\"")
    endif()
    if(TARGET helix_scriptcore_tests)

        # The engine's C# test shaders (its Tests/Shaders) built against this repo's Helix.Shaders and embedded as the
        # engine's tests embed them, so its Support/TestShaders.h and Support/Golden.h serve these tests too (golden
        # images of their own in Tests/Golden).
        helix_dependency_dir(shaders_dir Shaders)
        set(test_shaders_dir "${scriptcore_artifacts}/bin/HelixEngine.TestShaders/release")
        file(GLOB_RECURSE test_shader_sources CONFIGURE_DEPENDS "${engine_dir}/Tests/Shaders/*.cs" "${engine_dir}/Tests/Shaders/*.csproj"
             "${engine_dir}/Shaders/*.cs" "${engine_dir}/Shaders/*.csproj")
        list(FILTER test_shader_sources EXCLUDE REGEX "/(bin|obj)/")
        add_custom_command(
            OUTPUT "${test_shaders_dir}/HelixEngine.TestShaders.dll" "${test_shaders_dir}/HelixEngine.TestShaders.pdb"
            COMMAND "${HELIX_DOTNET}" build "${engine_dir}/Tests/Shaders/HelixEngine.TestShaders.csproj" -c Release --artifacts-path "${scriptcore_artifacts}"
                    --nologo -v quiet "-p:HelixShadersDir=${shaders_dir}"
            DEPENDS ${test_shader_sources}
            COMMENT "Building the engine's C# test shaders")
        set(tests_generated "${CMAKE_CURRENT_BINARY_DIR}/Generated")
        set(test_embedded "${tests_generated}/TestShadersAssembly.h" "${tests_generated}/TestShadersSymbols.h")
        add_custom_command(
            OUTPUT ${test_embedded}
            COMMAND "${CMAKE_COMMAND}" -E make_directory "${tests_generated}"
            COMMAND "${CMAKE_COMMAND}" "-DINPUT=${test_shaders_dir}/HelixEngine.TestShaders.dll" "-DOUTPUT=${tests_generated}/TestShadersAssembly.h"
                    -DNAME=kTestShadersAssembly -P "${HELIX_EMBED_FILE}"
            COMMAND "${CMAKE_COMMAND}" "-DINPUT=${test_shaders_dir}/HelixEngine.TestShaders.pdb" "-DOUTPUT=${tests_generated}/TestShadersSymbols.h"
                    -DNAME=kTestShadersSymbols -P "${HELIX_EMBED_FILE}"
            DEPENDS "${test_shaders_dir}/HelixEngine.TestShaders.dll" "${HELIX_EMBED_FILE}"
            COMMENT "Embedding the engine's C# test shaders")
        target_sources(helix_scriptcore_tests PRIVATE ${test_embedded})
        target_include_directories(helix_scriptcore_tests PRIVATE "${tests_generated}" "${engine_dir}/Tests")
        target_compile_definitions(helix_scriptcore_tests PRIVATE "HELIX_ENGINE_GOLDEN_DIR=\"${tests_dir}/Golden\"")
    endif()
endif()

# The public surface of HelixEngine.CoreModule against the inventory (Tests/Api/HelixEngine.csv): a member without a
# row, or a row without its member, fails. Tools/ApiInventory --write rewrites the inventory from the assembly.
# The tool is built for repos above too (their own inventories); its test of this assembly runs here.
if(HELIX_BUILD_TESTS)
    set(inventory_dir "${CMAKE_CURRENT_SOURCE_DIR}/Tools/ApiInventory")
    set(inventory_dll "${scriptcore_artifacts}/bin/ApiInventory/release/HelixApiInventory.dll")
    file(GLOB inventory_sources CONFIGURE_DEPENDS "${inventory_dir}/*.cs" "${inventory_dir}/*.csproj")
    add_custom_command(
        OUTPUT "${inventory_dll}"
        COMMAND "${HELIX_DOTNET}" build "${inventory_dir}/ApiInventory.csproj" -c Release --artifacts-path "${scriptcore_artifacts}" --nologo -v quiet
        DEPENDS ${inventory_sources}
        COMMENT "Building the API inventory tool")
    add_custom_target(helix_api_inventory ALL DEPENDS "${inventory_dll}")
    add_dependencies(helix_api_inventory helix_core_module)
    set(HELIX_API_INVENTORY "${inventory_dll}" CACHE INTERNAL "")
endif()
if(HELIX_BUILD_TESTS AND PROJECT_IS_TOP_LEVEL)
    add_test(NAME helix_scriptcore_api COMMAND "${HELIX_DOTNET}" "${inventory_dll}" --assembly "${core_module_dll}" --reference "${HELIX_SCRIPTING_RUNTIME}"
             --inventory "${CMAKE_CURRENT_SOURCE_DIR}/Tests/Api/HelixEngine.csv")
    # Helix.Mathematics (built with the core module) against its inventory.
    add_test(NAME helix_scriptcore_api_mathematics COMMAND "${HELIX_DOTNET}" "${inventory_dll}" --assembly "${core_module_bin}/Helix.Mathematics.dll"
             --inventory "${CMAKE_CURRENT_SOURCE_DIR}/Tests/Api/Helix.Mathematics.csv")
endif()

# The script tests once more on the coreclr starter (the desktop's shared framework through coreclr_initialize, as
# Android hosts it).
if(TEST helix_scriptcore_tests AND (HELIX_PLATFORM STREQUAL "windows" OR HELIX_PLATFORM STREQUAL "linux"))
    add_test(NAME helix_scriptcore_tests_coreclr COMMAND helix_scriptcore_tests)
    set_tests_properties(helix_scriptcore_tests_coreclr PROPERTIES ENVIRONMENT "HELIX_DOTNET_RUNTIME=coreclr")
endif()

# The rendering tests once more with the persistent blocks, and with a render thread (every golden image the same).
if(TARGET helix_scriptcore_tests AND (HELIX_PLATFORM STREQUAL "windows" OR HELIX_PLATFORM STREQUAL "linux"))
    add_test(NAME helix_scriptcore_tests_batching COMMAND helix_scriptcore_tests)
    set_tests_properties(helix_scriptcore_tests_batching PROPERTIES ENVIRONMENT "HELIX_RENDER_BATCHING=1")
    add_test(NAME helix_scriptcore_tests_render_thread COMMAND helix_scriptcore_tests)
    set_tests_properties(helix_scriptcore_tests_render_thread PROPERTIES ENVIRONMENT "HELIX_RENDER_BATCHING=1;HELIX_RENDER_THREAD=1")
endif()

# The .NET runtime is never unloaded: LeakSanitizer is told about its modules (as in Helix.Scripting's tests).
if(TEST helix_scriptcore_tests AND HELIX_ENABLE_ASAN AND HELIX_PLATFORM STREQUAL "linux")
    helix_dependency_dir(scripting_dir Scripting)
    set_tests_properties(helix_scriptcore_tests PROPERTIES
                         ENVIRONMENT "LSAN_OPTIONS=suppressions=${scripting_dir}/Tests/Support/LeakSuppressions.txt")
    # The run on the coreclr starter keeps its starter and takes the suppressions too.
    if(TEST helix_scriptcore_tests_coreclr)
        set_tests_properties(helix_scriptcore_tests_coreclr PROPERTIES ENVIRONMENT "HELIX_DOTNET_RUNTIME=coreclr;LSAN_OPTIONS=suppressions=${scripting_dir}/Tests/Support/LeakSuppressions.txt")
    endif()
    # The rendering runs keep their settings and take the suppressions too.
    if(TEST helix_scriptcore_tests_batching)
        set_tests_properties(helix_scriptcore_tests_batching PROPERTIES
                             ENVIRONMENT "HELIX_RENDER_BATCHING=1;LSAN_OPTIONS=suppressions=${scripting_dir}/Tests/Support/LeakSuppressions.txt")
        set_tests_properties(helix_scriptcore_tests_render_thread PROPERTIES
                             ENVIRONMENT "HELIX_RENDER_BATCHING=1;HELIX_RENDER_THREAD=1;LSAN_OPTIONS=suppressions=${scripting_dir}/Tests/Support/LeakSuppressions.txt")
    endif()
endif()

