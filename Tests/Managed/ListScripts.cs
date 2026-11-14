using System;
using Helix.Collections;
using HelixEngine;
using HelixEngine.Experimental.Rendering;
using HelixEngine.Rendering;
using HelixEngine.Rendering.RendererUtils;

// Renderer lists, native render passes, procedural draws, readbacks and pipeline settings through the script API.
namespace Tests
{
    public class CsListGraphicsSettings : IRenderPipelineGraphicsSettings
    {
        public int version => 3;
    }

    public class CsListGlobalSettings : RenderPipelineGlobalSettings
    {
        readonly System.Collections.Generic.List<IRenderPipelineGraphicsSettings> m_List = new() { new CsListGraphicsSettings() };
        protected override System.Collections.Generic.List<IRenderPipelineGraphicsSettings> settingsList => m_List;
    }

    // One way of drawing per frame, set by CsListScene: lists, a list with a state, a native pass, a procedural draw, lists with readbacks.
    public class CsListPipeline : RenderPipeline
    {
        public static int mode;
        public static RenderTexture copy;
        static readonly ShaderTagId s_Unlit = new ShaderTagId("SRPDefaultUnlit");

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (Camera camera in cameras)
            {
                if (camera.name != "ListEye" || !camera.TryGetCullingParameters(out ScriptableCullingParameters parameters))
                    continue;
                CullingResults culling = context.Cull(ref parameters);
                context.SetupCameraProperties(camera);
                var cmd = new CommandBuffer { name = "List" };
                cmd.ClearRenderTarget(true, true, Color.black);
                if (mode == 0 || mode >= 4)
                {
                    var desc = new RendererListDesc(s_Unlit, culling, camera) { sortingCriteria = SortingCriteria.CommonOpaque, renderQueueRange = RenderQueueRange.opaque };
                    RendererList list = context.CreateRendererList(desc);
                    if (mode == 0)
                        Notes.NoteRaw($"list:{list.isValid}:{context.QueryRendererListStatus(list)}:{RendererList.nullRendererList.isValid}:" +
                                      $"{new RendererListDesc(s_Unlit, culling, null).IsValid()}:{GraphicsSettings.GetRenderPipelineSettings<CsListGraphicsSettings>()?.version}");
                    cmd.DrawRendererList(list);
                }
                else if (mode == 1)
                {
                    var sorting = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
                    var param = new RendererListParams(culling, new DrawingSettings(s_Unlit, sorting), new FilteringSettings(RenderQueueRange.opaque));
                    var noColor = new RenderStateBlock(RenderStateMask.Blend);
                    BlendState blend = noColor.blendState;
                    RenderTargetBlendState target = blend.blendState0;
                    target.writeMask = 0;
                    blend.blendState0 = target;
                    noColor.blendState = blend;
                    param.isPassTagName = true;
                    var tags = new NativeArray<ShaderTagId>(1, Allocator.Temp);
                    tags[0] = s_Unlit;
                    var states = new NativeArray<RenderStateBlock>(1, Allocator.Temp);
                    states[0] = noColor;
                    param.tagValues = tags;
                    param.stateBlocks = states;
                    RendererList list = context.CreateRendererList(ref param);
                    tags.Dispose();
                    states.Dispose();
                    cmd.DrawRendererList(list);
                    try
                    {
                        var bad = new RendererListParams(culling, new DrawingSettings(s_Unlit, sorting), new FilteringSettings()) { tagValues = new NativeArray<ShaderTagId>() };
                        context.CreateRendererList(ref bad);
                    }
                    catch (ArgumentException)
                    {
                        Notes.NoteRaw("params:mismatch");
                    }
                }
                else if (mode == 3)
                {
                    // The full screen triangle's corners are clip positions: no camera matrices.
                    cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    cmd.DrawProcedural(Matrix4x4.identity, new Material(Shader.Find("Hidden/BlitCopy")), 0, MeshTopology.Triangles, 3);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Release();
                if (mode == 2)
                {
                    var attachments = new NativeArray<AttachmentDescriptor>(2, Allocator.Temp);
                    var target = new AttachmentDescriptor(GraphicsFormat.R8G8B8A8_UNorm);
                    target.ConfigureTarget(BuiltinRenderTextureType.CameraTarget, false, true);
                    target.ConfigureClear(Color.green);
                    attachments[0] = target;
                    attachments[1] = new AttachmentDescriptor(GraphicsFormat.R8G8B8A8_UNorm);
                    context.BeginRenderPass(camera.pixelWidth, camera.pixelHeight, 1, attachments);
                    var colors = new NativeArray<int>(1, Allocator.Temp);
                    colors[0] = 1;
                    context.BeginSubPass(colors);
                    context.EndSubPass();
                    colors[0] = 0;
                    context.BeginSubPass(colors);
                    context.EndSubPass();
                    context.EndRenderPass();
                    colors.Dispose();
                    attachments.Dispose();
                }
                if (copy != null)
                {
                    var blit = new CommandBuffer { name = "Copy" };
                    blit.Blit(BuiltinRenderTextureType.CameraTarget, copy);
                    if (mode == 4)
                        blit.RequestAsyncReadback(copy, r => Notes.NoteRaw($"cmdcb:{r.hasError}:{r.GetData<Color32>()[8 * 16 + 4].r}"));
                    context.ExecuteCommandBuffer(blit);
                    blit.Release();
                }
                context.Submit();
            }
        }
    }

    public class CsListPipelineAsset : RenderPipelineAsset
    {
        protected override RenderPipeline CreatePipeline() => new CsListPipeline();
        public override Type pipelineType => typeof(CsListPipeline);
    }

    public class CsListScene : GameBehaviour
    {
        NativeArray<byte> m_Into;
        AsyncGPUReadbackRequest m_Request;
        RenderTexture m_Copy;

        void Start()
        {
            var eye = new GameObject("ListEye");
            var cam = eye.AddComponent<Camera>();
            eye.transform.position = new Vector3(0, 0, -10);
            cam.orthographic = true;
            cam.orthographicSize = 2;
            var red = new Material(Shader.Find("Tests/Unlit"));
            red.SetColor("color", Color.red);
            var shape = new GameObject("ListShape");
            shape.AddComponent<MeshFilter>().sharedMesh = Text.Quad();
            shape.AddComponent<MeshRenderer>().sharedMaterial = red;
            shape.transform.position = new Vector3(-1, 0, 0);
            shape.transform.localScale = new Vector3(0.75f, 0.75f, 1);
            m_Copy = new RenderTexture(16, 16, 0);
            CsListPipeline.copy = m_Copy;
            CsListPipeline.mode = -1;
            GraphicsSettings.RegisterRenderPipelineSettings<CsListPipeline>(ScriptableObject.CreateInstance<CsListGlobalSettings>());
            Notes.NoteRaw($"settings:{GraphicsSettings.GetSettingsForRenderPipeline<CsListPipeline>() != null}:" +
                          $"{GraphicsSettings.TryGetRenderPipelineSettings(out CsListGraphicsSettings s)}:{s?.version}");
        }

        void Update()
        {
            CsListPipeline.mode++;
            if (CsListPipeline.mode == 4)
            {
                m_Into = new NativeArray<byte>(16 * 16 * 4, Allocator.Persistent);
                m_Request = AsyncGPUReadback.Request(m_Copy, 0, r => Notes.NoteRaw($"cb:{r.done}:{r.width}x{r.height}:{r.layerDataSize}:{r.GetData<Color32>()[8 * 16 + 4].r}"));
                AsyncGPUReadback.RequestIntoNativeArray(ref m_Into, m_Copy);
                Notes.NoteRaw($"rb:{m_Request.done}:{AsyncGPUReadback.Request(Texture2D.whiteTexture).hasError}");
            }
            else if (CsListPipeline.mode == 5)
            {
                Notes.NoteRaw($"done:{m_Request.done}:{m_Request.hasError}:{m_Request.width}");
                m_Request.WaitForCompletion();
            }
            else if (CsListPipeline.mode == 6)
            {
                Notes.NoteRaw($"into:{m_Into[(8 * 16 + 4) * 4]}:{m_Into[(8 * 16 + 12) * 4]}");
                m_Into.Dispose();
                GraphicsSettings.UnregisterRenderPipelineSettings<CsListPipeline>();
                Notes.NoteRaw($"unregistered:{GraphicsSettings.GetRenderPipelineSettings<CsListGraphicsSettings>() == null}");
            }
        }

        void OnDestroy()
        {
            CsListPipeline.copy = null;
            if (m_Copy != null)
                DestroyImmediate(m_Copy);
        }
    }
}
