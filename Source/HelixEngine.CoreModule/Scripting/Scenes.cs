using System;
using System.Collections.Generic;
using HelixEngine.Internal;
using HelixEngine.SceneManagement;

namespace HelixEngine.Events
{
    // Callbacks of events (the reference's names, with Helix in place of the engine's).
    public delegate void HelixAction();
    public delegate void HelixAction<T0>(T0 arg0);
    public delegate void HelixAction<T0, T1>(T0 arg0, T1 arg1);
    public delegate void HelixAction<T0, T1, T2>(T0 arg0, T1 arg1, T2 arg2);
    public delegate void HelixAction<T0, T1, T2, T3>(T0 arg0, T1 arg1, T2 arg2, T3 arg3);
}

namespace HelixEngine.SceneManagement
{
    // A loaded scene: a value; its members ask the engine (an unloaded scene answers empty values).
    public partial struct Scene : IEquatable<Scene>
    {
        public readonly int handle => (int)m_Handle.value;
        public readonly string name => SceneNative.SceneName(this);
        public readonly string path => SceneNative.ScenePath(this);
        public readonly int buildIndex => SceneNative.SceneBuildIndex(this);
        public readonly bool isLoaded => SceneNative.SceneIsLoaded(this);
        public readonly bool isDirty => SceneNative.SceneIsDirty(this);
        public bool isSubScene
        {
            readonly get => SceneNative.SceneIsSubScene(this);
            set => SceneNative.SceneSetIsSubScene(this, value);
        }
        public readonly int rootCount => SceneNative.SceneRootCount(this);

        public readonly bool IsValid() => SceneNative.SceneIsValid(this);
        public readonly GameObject[] GetRootGameObjects() => SceneNative.SceneRoots(this);
        public readonly void GetRootGameObjects(List<GameObject> rootGameObjects)
        {
            if (rootGameObjects == null)
                throw new ArgumentNullException(nameof(rootGameObjects));
            rootGameObjects.Clear();
            rootGameObjects.AddRange(SceneNative.SceneRoots(this));
        }

        public static bool operator ==(Scene lhs, Scene rhs) => lhs.m_Handle.value == rhs.m_Handle.value;
        public static bool operator !=(Scene lhs, Scene rhs) => !(lhs == rhs);
        public readonly bool Equals(Scene other) => this == other;
        public override readonly bool Equals(object other) => other is Scene s && this == s;
        public override readonly int GetHashCode() => handle;
    }

    public partial class SceneManager
    {
        public static AsyncOperation LoadSceneAsync(string sceneName) => LoadSceneAsync(sceneName, new LoadSceneParameters());
        public static AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode) =>
            LoadSceneAsync(sceneName, new LoadSceneParameters { loadSceneMode = mode });
        public static AsyncOperation LoadSceneAsync(string sceneName, LoadSceneParameters parameters) =>
            AsyncOperation.Of(SceneNative.LoadSceneAsync(sceneName, parameters));
        public static AsyncOperation LoadSceneAsync(int sceneBuildIndex) => LoadSceneAsync(sceneBuildIndex, new LoadSceneParameters());
        public static AsyncOperation LoadSceneAsync(int sceneBuildIndex, LoadSceneMode mode) =>
            LoadSceneAsync(sceneBuildIndex, new LoadSceneParameters { loadSceneMode = mode });
        public static AsyncOperation LoadSceneAsync(int sceneBuildIndex, LoadSceneParameters parameters) =>
            AsyncOperation.Of(SceneNative.LoadSceneAsync(sceneBuildIndex, parameters));
        public static AsyncOperation UnloadSceneAsync(Scene scene) => UnloadSceneAsync(scene, UnloadSceneOptions.None);
        public static AsyncOperation UnloadSceneAsync(Scene scene, UnloadSceneOptions options) => AsyncOperation.Of(SceneNative.UnloadSceneAsync(scene, options));
        public static AsyncOperation UnloadSceneAsync(string sceneName) => UnloadSceneAsync(sceneName, UnloadSceneOptions.None);
        public static AsyncOperation UnloadSceneAsync(string sceneName, UnloadSceneOptions options) =>
            AsyncOperation.Of(SceneNative.UnloadSceneAsync(sceneName, options));
        public static AsyncOperation UnloadSceneAsync(int sceneBuildIndex) => UnloadSceneAsync(sceneBuildIndex, UnloadSceneOptions.None);
        public static AsyncOperation UnloadSceneAsync(int sceneBuildIndex, UnloadSceneOptions options) =>
            AsyncOperation.Of(SceneNative.UnloadSceneAsync(sceneBuildIndex, options));
    }
}

namespace HelixEngine
{
    // An operation in progress (a scene or asset load, an unload); completed is raised once it is done (at once for a
    // handler added after that).
    public class AsyncOperation
    {
        internal readonly uint m_Id;
        Action<AsyncOperation> m_Completed;
        bool m_Watched;

        static readonly Dictionary<uint, WeakReference<AsyncOperation>> s_Watched = new();
        static bool s_Subscribed;

        internal AsyncOperation(uint id) => m_Id = id;

        internal static AsyncOperation Of(uint id) => id == 0 ? null : new AsyncOperation(id);

        public bool isDone => SceneNative.IsDone(m_Id);
        public float progress => SceneNative.Progress(m_Id);
        public int priority
        {
            get => SceneNative.Priority(m_Id);
            set => SceneNative.SetPriority(m_Id, value);
        }
        public bool allowSceneActivation
        {
            get => SceneNative.AllowSceneActivation(m_Id);
            set => SceneNative.SetAllowSceneActivation(m_Id, value);
        }

        public event Action<AsyncOperation> completed
        {
            add
            {
                if (isDone)
                {
                    value?.Invoke(this);
                    return;
                }
                m_Completed += value;
                Watch();
            }
            remove => m_Completed -= value;
        }

        void Watch()
        {
            if (m_Watched)
                return;
            m_Watched = true;
            if (!s_Subscribed)
            {
                s_Subscribed = true;
                SceneNative.completed += OnCompleted;
            }
            s_Watched[m_Id] = new WeakReference<AsyncOperation>(this);
            SceneNative.Watch(m_Id);
        }

        static void OnCompleted(uint id)
        {
            if (!s_Watched.Remove(id, out WeakReference<AsyncOperation> w) || !w.TryGetTarget(out AsyncOperation op))
                return;
            Action<AsyncOperation> handlers = op.m_Completed;
            op.m_Completed = null;
            handlers?.Invoke(op);
        }
    }

    // Resources.LoadAsync: the asset once it is loaded.
    public class ResourceRequest : AsyncOperation
    {
        internal ResourceRequest(uint id) : base(id) { }

        public Object asset => SceneNative.Asset(m_Id);
    }

    public partial class Resources
    {
        public static ResourceRequest LoadAsync(string path) => LoadAsync(path, typeof(Object));
        public static ResourceRequest LoadAsync(string path, Type type)
        {
            uint id = SceneNative.LoadAsync(path, type);
            return id == 0 ? null : new ResourceRequest(id);
        }
        public static ResourceRequest LoadAsync<T>(string path) where T : Object => LoadAsync(path, typeof(T));
        public static AsyncOperation UnloadUnusedAssets() => AsyncOperation.Of(SceneNative.UnloadUnusedAssets());
    }
}
