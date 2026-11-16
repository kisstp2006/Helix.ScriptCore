using System;
using Helix.Collections;
using HelixEngine;
using HelixEngine.Rendering;
using HelixEngine.Rendering.RendererUtils;

// The shadow API in C#: a directional light's cascades, its casters culled per split, the shadow lists drawn; the light
// index map of the per object light lists; spot and point light matrices, the global depth bias; reflection probes.
namespace Tests
{
    public class CsShadowPipeline : RenderPipeline
    {
        // Every second frame the second split's planes leave the caster out.
        int m_Frame;

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            bool awayFromCasters = m_Frame++ % 2 == 1;
            foreach (Camera camera in cameras)
            {
                if (camera.name != "ShadowEye" || !camera.TryGetCullingParameters(out ScriptableCullingParameters parameters))
                    continue;
                parameters.shadowDistance = 40;
                CullingResults culling = context.Cull(ref parameters);
                context.SetupCameraProperties(camera);
                NativeArray<VisibleLight> visible = culling.visibleLights;
                int sun = -1, spot = -1, point = -1;
                for (int i = 0; i < visible.Length; i++)
                {
                    LightType type = visible[i].lightType;
                    if (type == LightType.Directional)
                        sun = i;
                    else if (type == LightType.Spot)
                        spot = i;
                    else if (type == LightType.Point)
                        point = i;
                }
                bool bounds = culling.GetShadowCasterBounds(sun, out Bounds b);
                var splits = new NativeArray<ShadowSplitData>(2, Allocator.Temp);
                var views = new Matrix4x4[2];
                var projections = new Matrix4x4[2];
                bool computed = true;
                for (int i = 0; i < 2; i++)
                {
                    computed &= culling.ComputeDirectionalShadowMatricesAndCullingPrimitives(sun, i, 2, new Vector3(0.25f, 0.5f, 0.75f), 256, 0.5f,
                                                                                            out views[i], out projections[i], out ShadowSplitData split);
                    if (awayFromCasters && i == 1)
                        split.SetCullingPlane(0, new Plane(new Vector3(1, 0, 0), -1000));
                    splits[i] = split;
                }
                var infos = new NativeArray<LightShadowCasterCullingInfo>(visible.Length, Allocator.Temp);
                infos[sun] = new LightShadowCasterCullingInfo { splitRange = new RangeInt(0, 2), projectionType = BatchCullingProjectionType.Orthographic };
                context.CullShadowCasters(culling, new ShadowCastersCullingInfos { perLightInfos = infos, splitBuffer = splits });
                for (int i = 0; i < 2; i++)
                {
                    var settings = new ShadowDrawingSettings(culling, sun, BatchCullingProjectionType.Orthographic) { splitIndex = i };
                    var cmd = new CommandBuffer { name = "Shadows" };
                    cmd.SetViewProjectionMatrices(views[i], projections[i]);
                    cmd.DrawRendererList(context.CreateShadowRendererList(ref settings));
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Release();
                }
                // The light index map: the three lights in the caster's light list, then all left out of it.
                NativeArray<int> map = culling.GetLightIndexMap(Allocator.Temp);
                string lights = $"{map.Length}:{map[0]}:{culling.lightIndexCount}";
                for (int i = 0; i < map.Length; i++)
                    map[i] = -1;
                culling.SetLightIndexMap(map);
                lights += $":{culling.lightIndexCount}";
                map.Dispose();
                // A spot light's cone and a point light's face: the axis in the middle; fovBias 0 keeps 90 degrees.
                bool spotOk = culling.ComputeSpotShadowMatricesAndCullingPrimitives(spot, out Matrix4x4 spotView, out Matrix4x4 spotProjection,
                                                                                   out ShadowSplitData spotSplit);
                Vector4 axis = spotProjection * (spotView * new Vector4(0, 0, 0, 1));
                spotOk &= Mathf.Abs(axis.x / axis.w) < 1e-4f && spotSplit.cullingPlaneCount == 5;
                bool pointOk = culling.ComputePointShadowMatricesAndCullingPrimitives(point, CubemapFace.PositiveX, 0, out Matrix4x4 pointView,
                                                                                     out Matrix4x4 pointProjection, out ShadowSplitData pointSplit);
                pointOk &= Mathf.Abs(pointProjection.m11 - 1) < 1e-4f && pointSplit.cullingSphere.w == 5;
                var bias = new CommandBuffer { name = "Bias" };
                bias.SetGlobalDepthBias(1, 2);
                bias.SetGlobalDepthBias(0, 0);
                context.ExecuteCommandBuffer(bias);
                bias.Release();
                // The probe around the caster: seen, counted and reflected.
                NativeArray<VisibleReflectionProbe> probes = culling.visibleReflectionProbes;
                string probeName = probes.Length > 0 && probes[0].reflectionProbe != null ? probes[0].reflectionProbe.name : "-";
                var closest = new System.Collections.Generic.List<ReflectionProbeBlendInfo>();
                GameObject.Find("ShadowCaster").GetComponent<Renderer>().GetClosestReflectionProbes(closest);
                string more = $"{spotOk}:{pointOk}:{parameters.numIterationsEnclosingSphere}:{probes.Length}:{probeName}:{probes[0].importance}:" +
                              $"{culling.lightAndReflectionProbeIndexCount}:{closest.Count}:{closest[0].weight}";
                ShadowSplitData first = splits[0];
                Notes.NoteRaw($"shadow:{bounds}:{Mathf.Round(b.size.x)}:{computed}:{first.cullingPlaneCount}:{first.cullingSphere.w > 0}:" +
                              $"{splits[1].cullingSphere.w > first.cullingSphere.w}:{lights}:{more}");
                splits.Dispose();
                infos.Dispose();
                context.Submit();
            }
        }
    }

    public class CsShadowPipelineAsset : RenderPipelineAsset
    {
        protected override RenderPipeline CreatePipeline() => new CsShadowPipeline();
    }

    public class CsShadowScene : GameBehaviour
    {
        void Start()
        {
            var eye = new GameObject("ShadowEye");
            var camera = eye.AddComponent<Camera>();
            camera.fieldOfView = 60;
            camera.farClipPlane = 40;
            eye.transform.position = new Vector3(0, 0, -10);
            var sun = new GameObject("ShadowSun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(50, 0, 0);
            var spot = new GameObject("ShadowSpot").AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 60;
            spot.range = 10;
            spot.transform.position = new Vector3(0, 0, -5);
            var point = new GameObject("ShadowPoint").AddComponent<Light>();
            point.type = LightType.Point;
            point.range = 5;
            point.transform.position = new Vector3(0, 0, -3);
            var probe = new GameObject("ShadowProbe").AddComponent<ReflectionProbe>();
            probe.size = new Vector3(4, 4, 4);
            probe.importance = 3;
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            var caster = new GameObject("ShadowCaster");
            caster.AddComponent<MeshFilter>().sharedMesh = mesh;
            caster.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Tests/Surface"));
        }
    }
}
