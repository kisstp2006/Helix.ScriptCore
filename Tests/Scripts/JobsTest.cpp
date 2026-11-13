#include <Support/ScriptHost.h>

#include <Helix/Core/Logging/Log.h>
#include <Helix/Engine/GameObject/Lifecycle.h>
#include <Helix/Engine/Jobs/ManagedJobs.h>
#include <Helix/Testing/Test.h>

#include <cstdio>

using namespace helix;
using namespace helix::engine;
using namespace helix::scriptcore::testing;

namespace
{
    struct Logged
    {
        Array<String> errors;
    };

    void Collect(const LogMessage& message, void* user)
    {
        if (message.level >= LogLevel::Error)
            static_cast<Logged*>(user)->errors.Push(String(message.text));
    }

    // The lines a C# script writes in its Start.
    Array<String> Run(const char* script, Logged* logged = nullptr)
    {
        Calls().Clear();
        if (logged)
            AddLogSink(&Collect, logged);
        GameObject* host = GameObject::Create("Jobs");
        host->AddComponent(CSharp(script).type);
        RunScriptPhase(ScriptMessage::Update);
        DestroyImmediate(host);
        if (logged)
            RemoveLogSink(&Collect, logged);
        Array<String> calls = Move(Calls());
        Calls() = Array<String>();
        for (const String& c : calls)
            std::printf("    %s\n", c.CStr());
        return calls;
    }

    bool Has(const Array<String>& lines, StringView text)
    {
        for (const String& l : lines)
            if (StringView(l).Find(text) != StringView::npos)
                return true;
        return false;
    }
}

HX_TEST(Jobs, JobsOfEachKindRunOnTheWorkers)
{
    HX_REQUIRE(StartScripts());
    Array<String> lines = Run("CsJobs");
    HX_REQUIRE(lines.Size() == 1);
    HX_CHECK(lines[0] == "jobs:fill=5,104;squares=True;threads=True;prefix=1,3,6,10,15;scaled=3;sum=4950;both=6;done=True;disposed=False;"
                        "sub=20,30;slice=40;bytes=16,20;load=40;float3=(1.00, 2.00, 3.00)");
    JobsNative::EndFrame();
    HX_CHECK(JobsNative::LiveAllocations(2) == 0 && JobsNative::LiveAllocations(3) == 0);
}

HX_TEST(Jobs, TheSafetySystemReportsMisuse)
{
    HX_REQUIRE(StartScripts());
    Logged logged;
    Array<String> lines = Run("CsJobSafety", &logged);
    if (lines.Size() == 1 && lines[0] == "safety:off")
        return;
    HX_CHECK(Has(lines, "safety:read=InvalidOperationException: The previously scheduled job WriteJob writes to the Helix.Collections.NativeArray`1[System.Int32] WriteJob.data. You must call JobHandle.Complete() on the job WriteJob, before you can read from the Helix.Collections.NativeArray`1[System.Int32] safely."));
    HX_CHECK(Has(lines, "safety:schedule=InvalidOperationException: The previously scheduled job WriteJob writes to the Helix.Collections.NativeArray`1[System.Int32] WriteJob.data. You are trying to schedule a new job WriteJob, which writes to the same Helix.Collections.NativeArray`1[System.Int32] (via WriteJob.data). To guarantee safety, you must include WriteJob as a dependency of the newly scheduled job."));
    HX_CHECK(Has(lines, "safety:dispose=InvalidOperationException: The previously scheduled job WriteJob writes to the Helix.Collections.NativeArray`1[System.Int32] WriteJob.data. You must call JobHandle.Complete() on the job WriteJob, before you can deallocate the Helix.Collections.NativeArray`1[System.Int32] safely."));
    HX_CHECK(Has(lines, "safety:after=none"));
    HX_CHECK(Has(lines, "safety:disposed=ObjectDisposedException: Helix.Collections.NativeArray`1[System.Int32] has been deallocated, it is not allowed to access it"));
    HX_CHECK(Has(lines, "safety:index=IndexOutOfRangeException: Index 2 is out of range of '2' Length."));
    HX_CHECK(Has(lines, "safety:references=InvalidOperationException: Tests.ReferenceJob.text is not a value type. Job structs may not contain any reference types."));
    HX_CHECK(Has(lines, "safety:free=63"));
    HX_CHECK(Has(lines, "safety:deallocated=7,ObjectDisposedException"));
    HX_CHECK(Has(lines, "safety:leaks=1"));
    // In jobs: a write to a [ReadOnly] container, writes outside a parallel job's range (logged by the job system).
    bool readOnly = false, range = false;
    for (const String& e : logged.errors)
    {
        readOnly = readOnly || StringView(e).Find("has been declared as [ReadOnly] in the job, but you are writing to it.") != StringView::npos;
        range = range || StringView(e).Find("is out of restricted IJobParallelFor range [") != StringView::npos;
    }
    HX_CHECK(readOnly && range);
}

HX_TEST(Jobs, TransformJobsMoveTransformsOfSeveralRoots)
{
    HX_REQUIRE(StartScripts());
    Array<String> lines = Run("CsTransformJobs");
    HX_REQUIRE(lines.Size() == 1);
    HX_CHECK(lines[0] == "transforms:Root0=(0.00, 0.00, 1.00)/(0.00, 0.00, 1.00);Child0=(0.00, 1.00, 2.00)/(0.00, 1.00, 2.00);"
                        "Root1=(10.00, 0.00, 1.00)/(10.00, 0.00, 1.00);Child1=(10.00, 1.00, 2.00)/(10.00, 1.00, 2.00);"
                        "Root2=(20.00, 0.00, 1.00)/(20.00, 0.00, 1.00);Child2=(20.00, 1.00, 2.00)/(20.00, 1.00, 2.00)");
}

HX_TEST(Jobs, NativeArraysInTheMeshAndTextureApi)
{
    HX_REQUIRE(StartScripts());
    Array<String> lines = Run("CsNativeMeshes");
    HX_REQUIRE(lines.Size() == 1 || lines.Size() == 2);
    HX_CHECK(lines[0] == "native:mesh=4,0 1 2 0 2 3,RGBA(10, 20, 30, 40);"
                        "wrongsize=ArgumentException: SetVertices with NativeArray should use struct type that is 12 bytes (3x float) in size;"
                        "stride=16,ArgumentException: Mesh vertex buffer stream 0 size (64) is not a multiple of type Helix.Mathematics.float3 size (12);"
                        "read=4,3,4,1,UInt16;"
                        "readonlywrite=InvalidOperationException: Not allowed to modify MeshData from Mesh.AcquireReadOnlyMeshData; use Mesh.AllocateWritableMeshData.;"
                        "readonlyapply=InvalidOperationException: Only the MeshDataArray from Mesh.AllocateWritableMeshData can be applied to meshes;"
                        "built=4,0 1 2 0 2 3,(2.00, 4.00, 0.00),(2.00, 4.00, 0.00);raw=(5.00, 6.00, 7.00);"
                        "texture=4,RGBA(1, 2, 3, 4),200,4;load=RGBA(255, 0, 0, 0)");
    if (lines.Size() == 2)
        HX_CHECK(lines[1] == "native:disposed=ObjectDisposedException: HelixEngine.Mesh+MeshDataArray has been deallocated, it is not allowed to access it");
    JobsNative::EndFrame();
    HX_CHECK(JobsNative::LiveAllocations(2) == 0 && JobsNative::LiveAllocations(3) == 0);
}

HX_TEST(Jobs, AParallelForOverAMillionFloats)
{
    HX_REQUIRE(StartScripts());
    Array<String> lines = Run("CsJobBenchmark");
    HX_REQUIRE(lines.Size() == 1);
    HX_CHECK(StringView(lines[0]).Find("bench:same=True;workers=") == 0);
}
