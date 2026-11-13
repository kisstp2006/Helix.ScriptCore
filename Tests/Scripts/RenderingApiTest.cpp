#include <Cpp/CppRenderingScripts.h>
#include <Support/Golden.h>
#include <Support/ScriptHost.h>
#include <Support/TestShaders.h>

#include <Helix/Core/Logging/Log.h>
#include <Helix/Engine/Asset/Asset.h>
#include <Helix/Engine/Asset/Resources.h>
#include <Helix/Engine/GameObject/Lifecycle.h>
#include <Helix/Engine/Rendering/Graphics.h>
#include <Helix/Engine/Rendering/Pipeline/RenderPipeline.h>
#include <Helix/Platform/IO/FileSystem.h>
#include <Helix/Platform/Window/Window.h>
#include <Helix/Testing/Test.h>

#include <cstdio>

using namespace helix;
using namespace helix::engine;
using namespace helix::scriptcore::testing;

namespace
{
    // The C# script and the C++ script of a scenario on one GameObject: their lines, C++ first (it runs first).
    bool SameLines(const char* cs, const char* cpp, StringView expected)
    {
        Calls().Clear();
        GameObject* host = GameObject::Create("Host");
        host->AddComponent(CSharp(cs).type);
        AddScript(host, Cpp(cpp));
        RunScriptPhase(ScriptMessage::Update);
        DestroyImmediate(host);
        Array<String> calls = Move(Calls());
        Calls() = Array<String>();
        if (calls.Size() != 2)
        {
            std::printf("    %u lines\n", u32(calls.Size()));
            for (const String& c : calls)
                std::printf("    %s\n", c.CStr());
            return false;
        }
        bool same = calls[0] == calls[1] && calls[1] == expected;
        if (!same)
            std::printf("    C++: %s\n    C#:  %s\n", calls[0].CStr(), calls[1].CStr());
        return same;
    }

    // The lines of one script's Start (its messages and its own line).
    Array<String> LinesOf(const engine::ScriptClass& script)
    {
        Calls().Clear();
        GameObject* host = GameObject::Create("Host");
        if (script.language == ScriptLanguage::CSharp)
            host->AddComponent(script.type);
        else
            AddScript(host, script);
        RunScriptPhase(ScriptMessage::Update);
        DestroyImmediate(host);
        Array<String> calls = Move(Calls());
        Calls() = Array<String>();
        return calls;
    }

    bool SameList(const Array<String>& a, const Array<String>& b, std::initializer_list<const char*> expected)
    {
        bool same = a.Size() == b.Size() && a.Size() == expected.size();
        usize i = 0;
        for (const char* e : expected)
        {
            same = same && i < a.Size() && a[i] == StringView(e) && b[i] == StringView(e);
            i++;
        }
        if (!same)
        {
            std::printf("    C++:");
            for (const String& c : a)
                std::printf(" [%s]", c.CStr());
            std::printf("\n    C#: ");
            for (const String& c : b)
                std::printf(" [%s]", c.CStr());
            std::printf("\n");
        }
        return same;
    }

    // Runs f on every device: each D3D11 adapter, OpenGL, WebGL 2 where there is a window.
    template <class F>
    u32 ForEachDevice(F&& f)
    {
        u32 count = 0;
        for (rhi::Backend b : {rhi::Backend::D3D11, rhi::Backend::OpenGL, rhi::Backend::WebGL2})
        {
            if ((b == rhi::Backend::WebGL2 && !platform::Window::IsSupported()) || !rhi::Device::IsBackendAvailable(b))
                continue;
            char name[128];
            u32 adapters = 0;
            while (b == rhi::Backend::D3D11 && rhi::Device::AdapterName(b, adapters, name, sizeof(name)))
                adapters++;
            for (i32 adapter = adapters ? 0 : -1; adapter < i32(adapters); adapter++)
            {
                rhi::Device* device = rhi::Device::Create({.backend = b, .adapter = adapter, .vsync = false});
                if (!device)
                    continue;
                name[0] = 0;
                if (adapter >= 0)
                    rhi::Device::AdapterName(b, u32(adapter), name, sizeof(name));
                std::printf("    backend %d %s\n", int(b), name);
                f(*device);
                Graphics::SetDevice(nullptr);
                rhi::Device::Destroy(device);
                count++;
            }
        }
        return count;
    }

    struct Errors
    {
        u32 count = 0;
    };

    void CountErrors(const LogMessage& message, void* user)
    {
        if (message.level >= LogLevel::Error)
            static_cast<Errors*>(user)->count++;
    }
}

HX_TEST(ScriptApi, ShadersMaterialsMeshesAndTexturesInBothLanguages)
{
    HX_REQUIRE(StartScripts());
    engine::testing::TestShaderFolder shaders;
    HX_REQUIRE(shaders.surface != nullptr);
    HX_CHECK(SameLines("CsResources", "CppResources",
                       "res:props=7;same=True;keywords=5;glow=True;smooth=0.25;color=(1.00,0.50,0.25,1.00);on=True;enabled=Glow;layers=4;queue=3005;"
                       "passes=2;block=True/False/0.75;mesh=4/6/1/4/6/(0.00,0.00,-1.00)/(0.00,0.00,0.00)(2.00,2.00,0.00);tex=4x4/4/3/(1.00,0.00,0.00,1.00)/1/0"));
}

HX_TEST(ScriptApi, RenderersCamerasAndLightsInBothLanguages)
{
    HX_REQUIRE(StartScripts());
    engine::testing::TestShaderFolder shaders;
    HX_REQUIRE(shaders.plain != nullptr);
    HX_CHECK(SameLines("CsComponents", "CppComponents",
                       "comp:mesh=True;materials=1;bounds=(0.00,0.00,5.00)(2.00,2.00,0.00);block=True/2.00/True;viewport=(0.50,0.50,10.00);focal=20.78;"
                       "horizontal=98.21;mask=-1;listed=True;planes=6;inside=True;behind=False;reset=60.00/1.00;plane=True(0.00,0.00,-1.00)-0.00/False;"
                       "light=3.00/1/1.00/2"));
}

HX_TEST(ScriptApi, ScriptableObjectsInBothLanguages)
{
    HX_REQUIRE(StartScripts());
    Errors errors;
    AddLogSink(&CountErrors, &errors);
    Array<String> cpp = LinesOf(Cpp("CppDataUser"));
    Array<String> cs = LinesOf(CSharp("CsDataUser"));
    RemoveLogSink(&CountErrors, &errors);
    // Made, copied (the copy's fields before its Awake), by name, by type; the wrong type is an error each.
    HX_CHECK(SameList(cpp, cs,
                      {"so:x.Awake:3", "so:x.OnEnable:3,none", "so:x.Awake:5", "so:x.OnEnable:5,none", "so:x.Awake:3", "so:x.OnEnable:3,none", "so:x.Awake:3",
                       "so:x.OnEnable:3,none", "so:x.OnDisable:5", "so:x.OnDestroy", "so:x.OnDisable:5", "so:x.OnDestroy", "so:x.OnDisable:3", "so:x.OnDestroy",
                       "so:x.OnDisable:3", "so:x.OnDestroy",
                       "soapi:copy=5/Made(Clone);byName=True/3;byType=True;plain=True;wrong=True;found=4"}));
    HX_CHECK(errors.count == 2);
}

HX_TEST(ScriptApi, ScriptableObjectAssetsLoadWriteAndInstantiate)
{
    HX_REQUIRE(StartScripts());
    String root = platform::JoinPath(platform::TempDirectory(), Format("helix-so-{}", Guid::Generate().ToString()));
    platform::CreateDirectories(platform::JoinPath(root, "Resources"));
    struct Case
    {
        const engine::ScriptClass* script;
        const char* guid;
        const char* file;
    };
    const Case cases[] = {{&CSharp("CsData"), "e0000000000000000000000000000031", "StoredCs"}, {&Cpp("CppData"), "e0000000000000000000000000000032", "StoredCpp"}};
    Array<MonoScript*> scripts;
    for (const Case& c : cases)
    {
        Guid guid;
        Guid::TryParse(c.guid, guid);
        MonoScript* asset = MonoScript::FromScriptClass(*c.script);
        RegisterAssetObject(guid, 11500000, asset, true);
        scripts.Push(asset);
        String text("%YAML 1.1\n%TAG !h! tag:helixengine,2026:\n--- !h!114 &11400000\nGameBehaviour:\n  m_ObjectHideFlags: 0\n"
                    "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_Script: {fileID: 11500000, guid: ");
        text.Append(c.guid).Append(", type: 3}\n  m_Name: Stored\n  count: 7\n  label: from file\n");
        platform::WriteFile(platform::JoinPath(root, Format("Resources/{}.asset", c.file)),
                            Span<const u8>(reinterpret_cast<const u8*>(text.Data()), text.Size()));
    }
    auto* source = new FolderAssetSource(root);
    AddAssetSource(source);
    for (const Case& c : cases)
    {
        Calls().Clear();
        Object* o = Resources::Load(c.file);
        HX_REQUIRE(o != nullptr && o->Is<ScriptableObject>());
        auto* so = static_cast<ScriptableObject*>(o);
        HX_CHECK(so->script() == c.script && so->Name() == StringView("Stored"));
        // Awake and OnEnable once the file is read, with its fields.
        HX_CHECK(Calls().Size() == 2 && Calls()[0] == StringView("so:x.Awake:7") && Calls()[1] == StringView("so:x.OnEnable:7,from file"));
        // Written: a 114 document with the script, the name and the fields; read again from a binary file too.
        Array<u8> text;
        String error;
        Object* objects[] = {o};
        HX_REQUIRE(WriteAssetFile(Span<Object* const>(objects, 1), text, error));
        StringView written(reinterpret_cast<const char*>(text.Data()), text.Size());
        HX_CHECK(written.Find("!h!114") != StringView::npos && written.Find("m_Name: Stored") != StringView::npos &&
                 written.Find("count: 7") != StringView::npos && written.Find("label: from file") != StringView::npos &&
                 written.Find(c.guid) != StringView::npos);
        Array<u8> binary;
        HX_REQUIRE(WriteAssetFile(Span<Object* const>(objects, 1), binary, error, true));
        platform::WriteFile(platform::JoinPath(root, Format("Resources/{}Binary.asset", c.file)), Span<const u8>(binary.Data(), binary.Size()));
        RefreshAssets();
        Calls().Clear();
        Object* b = Resources::Load(Format("{}Binary", c.file));
        HX_CHECK(b != nullptr && b != o && b->Is<ScriptableObject>() && static_cast<ScriptableObject*>(b)->script() == c.script);
        HX_CHECK(Calls().Size() == 2 && Calls()[1] == StringView("so:x.OnEnable:7,from file"));
        // A copy: its own Awake and OnEnable with the fields copied.
        Calls().Clear();
        Object* copy = Instantiate(o);
        HX_CHECK(copy != nullptr && copy != o && copy->Is<ScriptableObject>());
        HX_CHECK(Calls().Size() == 2 && Calls()[0] == StringView("so:x.Awake:7") && Calls()[1] == StringView("so:x.OnEnable:7,from file"));
        Calls().Clear();
        DestroyImmediate(copy);
        HX_CHECK(Calls().Size() == 2 && Calls()[0] == StringView("so:x.OnDisable:7") && Calls()[1] == StringView("so:x.OnDestroy"));
        DestroyImmediate(b, true);
        DestroyImmediate(o, true);
    }
    Calls().Clear();
    ResetAssets();
    delete source;
    for (const Case& c : cases)
    {
        platform::RemoveFile(platform::JoinPath(root, Format("Resources/{}.asset", c.file)));
        platform::RemoveFile(platform::JoinPath(root, Format("Resources/{}Binary.asset", c.file)));
    }
    platform::RemoveEmptyDirectory(platform::JoinPath(root, "Resources"));
    platform::RemoveEmptyDirectory(root);
    for (MonoScript* m : scripts)
        DestroyImmediate(m, true);
    Calls().Clear();
}

HX_TEST(ScriptApi, AGoldenImageDrawnThroughTheScriptApi)
{
    HX_REQUIRE(StartScripts());
    engine::testing::TestShaderFolder shaders;
    HX_REQUIRE(Shader::Find("Tests/Lambert") && Shader::Find("Tests/Textured"));
    // The C# script builds the scene in its Start; its drawer queues a mesh in each Update.
    GameObject* host = GameObject::Create("GoldenHost");
    host->AddComponent(CSharp("CsGoldenScene").type);
    RunScriptPhase(ScriptMessage::Update);
    // A frame without a device drops what the drawer queued.
    HX_CHECK(Graphics::QueuedMeshCount() == 1);
    Graphics::RenderFrame();
    HX_CHECK(Graphics::QueuedMeshCount() == 0);
    GameObject* shape = GameObject::Find("GoldenShape");
    HX_REQUIRE(shape != nullptr);
    AddScript(shape, Cpp("CppRendererEvents"));
    constexpr u32 size = 32;
    u32 devices = ForEachDevice([&](rhi::Device& device) {
        rhi::TextureHandle color =
            device.CreateTexture({.format = rhi::Format::RGBA8Unorm, .width = size, .height = size, .usage = rhi::TextureRenderTarget | rhi::TextureSampled});
        rhi::TextureHandle depth = device.CreateTexture({.format = rhi::Format::D32Float, .width = size, .height = size, .usage = rhi::TextureDepthStencil});
        Graphics::SetDevice(&device);
        Graphics::SetTarget({color, depth, size, size, rhi::Format::RGBA8Unorm, rhi::Format::D32Float});
        Graphics::SetAmbientLight(Color(0.25f, 0.25f, 0.25f, 1));
        RunScriptPhase(ScriptMessage::Update);
        HX_CHECK(Graphics::QueuedMeshCount() == 1);
        Calls().Clear();
        Graphics::RenderFrame();
        HX_CHECK(Graphics::QueuedMeshCount() == 0 && Graphics::LastStats().drawCalls == 2);
        // The camera's messages around the drawn renderer's (scripts of both languages, C++ first in a broadcast).
        Array<String> events = Move(Calls());
        Calls() = Array<String>();
        const char* expected[] = {"cs.OnPreCull",  "cs.OnWillRenderObject:GoldenEye", "cpp.OnWillRenderObject:GoldenEye", "cs.OnPreRender",
                                  "cs.OnPostRender", "cpp.OnRenderObject",              "cs.OnRenderObject"};
        bool same = events.Size() == 7;
        for (u32 i = 0; same && i < 7; i++)
            same = events[i] == StringView(expected[i]);
        if (!same)
            for (const String& e : events)
                std::printf("    %s\n", e.CStr());
        HX_CHECK(same);
        Array<u32> pixels;
        pixels.Resize(size * size);
        HX_REQUIRE(device.ReadTexture(color, 0, pixels.Data(), pixels.Size() * 4));
        HX_CHECK(engine::testing::MatchGolden("ScriptApiScene", pixels.Data(), size, size));
        Graphics::ReleaseResources();
        device.Destroy(color);
        device.Destroy(depth);
    });
    std::printf("    %u devices\n", devices);
    DestroyImmediate(host);
}

HX_TEST(ScriptApi, ThePipelineValuesInBothLanguages)
{
    HX_REQUIRE(StartScripts());
    HX_CHECK(SameLines("CsPipelineValues", "CppPipelineValues",
                       "pipe:First//Third/59/True/False/-1/0-5000/-1/4294967295/2500/2501/6/True/2/2/15/3/1.00,2.00/Values/True/True/0/1/True/True"));
}

namespace
{
    // The manager's events in C++.
    struct CppPipelineEvents
    {
        u32 tokens[3] = {};

        CppPipelineEvents()
        {
            tokens[0] = RenderPipelineManager::beginContextRendering().Add(
                [](void*, ScriptableRenderContext&, Span<Camera* const> c) { Calls().Push(Format("cpp.beginContext:{}", c.Size())); });
            tokens[1] = RenderPipelineManager::beginCameraRendering().Add(
                [](void*, ScriptableRenderContext&, Camera* c) { Calls().Push(Format("cpp.beginCamera:{}", c->Name())); });
            tokens[2] = RenderPipelineManager::endCameraRendering().Add(
                [](void*, ScriptableRenderContext&, Camera* c) { Calls().Push(Format("cpp.endCamera:{}", c->Name())); });
        }

        ~CppPipelineEvents()
        {
            RenderPipelineManager::beginContextRendering().Remove(tokens[0]);
            RenderPipelineManager::beginCameraRendering().Remove(tokens[1]);
            RenderPipelineManager::endCameraRendering().Remove(tokens[2]);
        }
    };
}

HX_TEST(ScriptApi, ScriptablePipelinesOfBothLanguagesDrawTheGoldenImage)
{
    HX_REQUIRE(StartScripts());
    engine::testing::TestShaderFolder shaders;
    GameObject* host = GameObject::Create("PipelineHost");
    host->AddComponent(CSharp("CsGoldenScene").type);
    host->AddComponent(CSharp("CsPipelineEvents").type);
    RunScriptPhase(ScriptMessage::Update);
    // A frame without a device drops what the drawer queued.
    Graphics::RenderFrame();
    GameObject* shape = GameObject::Find("GoldenShape");
    HX_REQUIRE(shape != nullptr);
    AddScript(shape, Cpp("CppRendererEvents"));
    CppPipelineEvents events;
    constexpr u32 size = 32;
    for (const char* assetClass : {"CsPipelineAsset", "CppPipelineAsset"})
    {
        std::printf("    %s\n", assetClass);
        Calls().Clear();
        ScriptableObject* asset = ScriptableObject::CreateInstanceByName(assetClass);
        HX_REQUIRE(asset != nullptr);
        u32 devices = ForEachDevice([&](rhi::Device& device) {
            rhi::TextureHandle color = device.CreateTexture(
                {.format = rhi::Format::RGBA8Unorm, .width = size, .height = size, .usage = rhi::TextureRenderTarget | rhi::TextureSampled});
            rhi::TextureHandle depth = device.CreateTexture({.format = rhi::Format::D32Float, .width = size, .height = size, .usage = rhi::TextureDepthStencil});
            Graphics::SetDevice(&device);
            Graphics::SetTarget({color, depth, size, size, rhi::Format::RGBA8Unorm, rhi::Format::D32Float});
            Graphics::SetAmbientLight(Color(0.25f, 0.25f, 0.25f, 1));
            GraphicsSettings::SetDefaultRenderPipeline(asset);
            RunScriptPhase(ScriptMessage::Update);
            Calls().Clear();
            Graphics::RenderFrame();
            HX_CHECK(Graphics::QueuedMeshCount() == 0 && Graphics::LastStats().drawCalls == 2 && RenderPipelineManager::currentAsset() == asset);
            // The pipeline made for the device; the events of both languages (C# heard first: it listened first); no
            // camera messages of the fallback; OnWillRenderObject while culling, OnRenderObject where the pipeline asked.
            Array<String> lines = Move(Calls());
            Calls() = Array<String>();
            const char* expected[] = {"CreatePipeline",
                                      "cs.beginContext:1",
                                      "cpp.beginContext:1",
                                      "cs.beginCamera:GoldenEye",
                                      "cpp.beginCamera:GoldenEye",
                                      "cs.OnWillRenderObject:GoldenEye",
                                      "cpp.OnWillRenderObject:GoldenEye",
                                      "lights:1:1:0.50",
                                      "cpp.OnRenderObject",
                                      "cs.OnRenderObject",
                                      "cs.endCamera:GoldenEye",
                                      "cpp.endCamera:GoldenEye"};
            bool same = lines.Size() == sizeof(expected) / sizeof(expected[0]);
            for (u32 i = 0; same && i < lines.Size(); i++)
                same = lines[i] == StringView(expected[i]);
            if (!same)
                for (const String& l : lines)
                    std::printf("    %s\n", l.CStr());
            HX_CHECK(same);
            Array<u32> pixels;
            pixels.Resize(size * size);
            HX_REQUIRE(device.ReadTexture(color, 0, pixels.Data(), pixels.Size() * 4));
            HX_CHECK(engine::testing::MatchGolden("ScriptApiScene", pixels.Data(), size, size));
            // A device change lets the pipeline go.
            Graphics::ReleaseResources();
            HX_CHECK(Calls().Size() == 1 && Calls()[0] == StringView("Dispose"));
            Calls().Clear();
            device.Destroy(color);
            device.Destroy(depth);
        });
        std::printf("    %u devices\n", devices);
        GraphicsSettings::SetDefaultRenderPipeline(nullptr);
        DestroyImmediate(asset);
    }
    DestroyImmediate(host);
    Calls().Clear();
}
