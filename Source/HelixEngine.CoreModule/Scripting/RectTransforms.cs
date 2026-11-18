using System;

namespace HelixEngine
{
    public partial class RectTransform
    {
        public delegate void ReapplyDrivenProperties(RectTransform driven);

        static event ReapplyDrivenProperties s_Reapply;
        // Hooked to the native event while there are listeners.
        static readonly Action<RectTransform> s_Forward = r => s_Reapply?.Invoke(r);

        /// <summary>Raised when a driven RectTransform's driven properties must be set again (layout components
        /// listen and mark their layout for a rebuild).</summary>
        public static event ReapplyDrivenProperties reapplyDrivenProperties
        {
            add
            {
                if (s_Reapply == null)
                    reapplyDrivenPropertiesInternal += s_Forward;
                s_Reapply += value;
            }
            remove
            {
                s_Reapply -= value;
                if (s_Reapply == null)
                    reapplyDrivenPropertiesInternal -= s_Forward;
            }
        }

        /// <summary>Bottom left, top left, top right, bottom right, in its own space (z 0).</summary>
        public void GetLocalCorners(Vector3[] fourCornersArray)
        {
            if (fourCornersArray == null || fourCornersArray.Length < 4)
            {
                Debug.LogError("Calling GetLocalCorners with an array that is null or has less than 4 elements.");
                return;
            }
            GetLocalCorners(fourCornersArray.AsSpan());
        }

        public void GetLocalCorners(Span<Vector3> fourCornersArray)
        {
            if (fourCornersArray.Length < 4)
            {
                Debug.LogError("Calling GetLocalCorners with a span of less than 4 elements.");
                return;
            }
            Vector3[] c = LocalCornersInternal();
            for (int i = 0; i < 4; i++)
                fourCornersArray[i] = c[i];
        }

        /// <summary>The corners through its localToWorldMatrix.</summary>
        public void GetWorldCorners(Vector3[] fourCornersArray)
        {
            if (fourCornersArray == null || fourCornersArray.Length < 4)
            {
                Debug.LogError("Calling GetWorldCorners with an array that is null or has less than 4 elements.");
                return;
            }
            GetWorldCorners(fourCornersArray.AsSpan());
        }

        public void GetWorldCorners(Span<Vector3> fourCornersArray)
        {
            if (fourCornersArray.Length < 4)
            {
                Debug.LogError("Calling GetWorldCorners with a span of less than 4 elements.");
                return;
            }
            Vector3[] c = WorldCornersInternal();
            for (int i = 0; i < 4; i++)
                fourCornersArray[i] = c[i];
        }
    }

    /// <summary>The RectTransforms a driver (a layout component) controls: claimed by Add, released by Clear.</summary>
    public struct DrivenRectTransformTracker
    {
        System.Collections.Generic.List<RectTransform> m_Tracked;

        public static void StopRecordingUndo() { }

        public static void StartRecordingUndo() { }

        public void Add(Object driver, RectTransform rectTransform, DrivenTransformProperties drivenProperties)
        {
            if (rectTransform == null)
                return;
            m_Tracked ??= new System.Collections.Generic.List<RectTransform>();
            DrivenTransformProperties now = rectTransform.drivenByObject == driver ? rectTransform.drivenPropertiesInternal | drivenProperties : drivenProperties;
            rectTransform.SetDriven(driver, now);
            m_Tracked.Add(rectTransform);
        }

        public void Clear()
        {
            if (m_Tracked == null)
                return;
            foreach (RectTransform r in m_Tracked)
                if (r != null)
                    r.SetDriven(null, DrivenTransformProperties.None);
            m_Tracked.Clear();
        }

        [Obsolete("revertValues has no effect; use Clear().")]
        public void Clear(bool revertValues) => Clear();
    }

    public static partial class RectTransformUtility
    {
        /// <summary>The rectangle on the screen through a camera (Camera.main by default; none: its world
        /// rectangle).</summary>
        public static Rect GetScreenRect(this RectTransform rectTransform) => GetScreenRectInternal(rectTransform, Camera.main);

        public static Rect GetScreenRect(this RectTransform rectTransform, Camera camera) => GetScreenRectInternal(rectTransform, camera);
    }
}
