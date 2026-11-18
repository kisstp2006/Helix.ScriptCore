using System.Collections.Generic;
using System.Reflection;
using HelixEngine;
using HelixEngine.Events;

namespace Tests
{
    // The canvas UI's foundations from C#: RectTransform (replacing a Transform, anchors, corners, the tracker),
    // Sprite.Create with its UVs, HelixEvent (listeners and persistent calls by name).
    public class CsCanvasFoundation : GameBehaviour
    {
        public HelixEvent<bool> toggled = new HelixEvent<bool>();
        int m_Dimensions;

        void OnRectTransformDimensionsChange() => m_Dimensions++;

        void RectTransforms()
        {
            var g = new GameObject("rect");
            var r = g.AddComponent<RectTransform>();
            r.sizeDelta = new Vector2(200, 100);
            var child = new GameObject("child").AddComponent<RectTransform>();
            child.SetParent(r, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.sizeDelta = new Vector2(-20, -20);
            var corners = new Vector3[4];
            child.GetLocalCorners(corners);
            var tracker = new DrivenRectTransformTracker();
            tracker.Add(this, child, DrivenTransformProperties.SizeDelta);
            bool driven = child.drivenByObject == this;
            tracker.Clear();
            Notes.Note(this, $"rect:{g.transform is RectTransform}:{child.rect.width}:{child.rect.height}:{corners[0].x}:{corners[2].y}:{driven}:{child.drivenByObject == null}");
            Notes.Note(this, $"offsets:{child.offsetMin.x}:{child.offsetMax.y}:{RectTransformUtility.WorldToScreenPoint(null, new Vector3(3, 4, 5)).y}");
            DestroyImmediate(g);
        }

        void Sprites()
        {
            var t = new Texture2D(64, 32);
            Sprite s = Sprite.Create(t, new Rect(16, 8, 32, 16), new Vector2(0.5f, 0.5f), 32, 0, SpriteMeshType.FullRect, new Vector4(4, 2, 6, 3));
            Vector4 outer = HelixEngine.Sprites.DataUtility.GetOuterUV(s);
            Notes.Note(this, $"sprite:{s.rect.width}:{s.pivot.x}:{s.border.z}:{outer.x}:{outer.w}:{s.vertices.Length}:{s.triangles.Length}");
            DestroyImmediate(s);
            DestroyImmediate(t);
        }

        public void Hide(bool hidden) => Notes.Note(this, $"hide:{hidden}");

        void Events()
        {
            var heard = new List<string>();
            HelixAction<bool> a = v => heard.Add($"a{v}");
            toggled.AddListener(a);
            toggled.AddListener(v => heard.Add($"b{v}"));
            toggled.Invoke(true);
            toggled.RemoveListener(a);
            toggled.Invoke(false);
            // A persistent call made as scene files make them: this script's Hide with the event's argument.
            var calls = typeof(HelixEventBase).GetField("m_PersistentCalls", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(toggled);
            var list = (System.Collections.IList)calls.GetType().GetField("m_Calls", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(calls);
            object call = System.Activator.CreateInstance(list.GetType().GetGenericArguments()[0], true);
            call.GetType().GetField("m_Target", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(call, this);
            call.GetType().GetField("m_MethodName", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(call, "Hide");
            call.GetType().GetField("m_Mode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(call, PersistentListenerMode.EventDefined);
            list.Add(call);
            toggled.RemoveAllListeners();
            toggled.Invoke(true);
            toggled.SetPersistentListenerState(0, HelixEventCallState.Off);
            toggled.Invoke(true);
            Notes.Note(this, $"events:{string.Join(",", heard)}:{toggled.GetPersistentEventCount()}:{toggled.GetPersistentMethodName(0)}:{toggled.GetPersistentTarget(0) == this}");
            Notes.Note(this, $"valid:{HelixEventBase.GetValidMethodInfo(this, "Hide", new[] { typeof(bool) }) != null}:{HelixEventBase.GetValidMethodInfo(this, "Hide", new[] { typeof(int) }) == null}");
        }

        void Canvases()
        {
            int will = 0, pre = 0;
            Canvas.WillRenderCanvases onWill = () => will++, onPre = () => pre++;
            Canvas.preWillRenderCanvases += onPre;
            Canvas.willRenderCanvases += onWill;
            var g = new GameObject("canvas");
            Canvas canvas = g.AddComponent<Canvas>();
            var item = new GameObject("item").AddComponent<RectTransform>();
            item.SetParent(g.transform, false);
            CanvasRenderer r = item.gameObject.AddComponent<CanvasRenderer>();
            var v = new UIVertex[4];
            for (int i = 0; i < 4; i++)
            {
                v[i] = UIVertex.simpleVert;
                v[i].position = new Vector3(i == 2 || i == 3 ? 10 : 0, i == 1 || i == 2 ? 10 : 0, 0);
            }
            r.SetVertices(v, 4);
            r.SetMaterial(Canvas.GetDefaultCanvasMaterial(), null);
            r.SetColor(new Color(1, 0, 0, 0.5f));
            CanvasGroup group = item.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.5f;
            Canvas.ForceUpdateCanvases();
            Canvas.willRenderCanvases -= onWill;
            Canvas.ForceUpdateCanvases();
            Canvas.preWillRenderCanvases -= onPre;
            ICanvasRaycastFilter filter = group;
            Notes.Note(this, $"canvas:{g.transform is RectTransform}:{canvas.isRootCanvas}:{canvas.renderMode}:{will}:{pre}:{r.absoluteDepth}:{r.GetInheritedAlpha()}:{filter.IsRaycastLocationValid(Vector2.zero, null)}");
            var positions = new List<Vector3>();
            var colors = new List<Color32>();
            var uv0 = new List<Vector4>();
            var uv1 = new List<Vector4>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var indices = new List<int>();
            var tri = new List<UIVertex> { v[0], v[1], v[2], v[2], v[3], v[0] };
            CanvasRenderer.SplitUIVertexStreams(tri, positions, colors, uv0, uv1, normals, tangents, indices);
            var back = new List<UIVertex>();
            CanvasRenderer.CreateUIVertexStream(back, positions, colors, uv0, uv1, normals, tangents, indices);
            Notes.Note(this, $"streams:{positions.Count}:{indices.Count}:{back.Count}:{back[4].position.x}:{Canvas.GetDefaultCanvasMaterial().shader.name}");
            DestroyImmediate(g);
        }

        void Start()
        {
            RectTransforms();
            Sprites();
            Events();
            Canvases();
        }
    }
}
