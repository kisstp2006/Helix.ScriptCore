using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using HelixEngine.Internal;

namespace HelixEngine
{
    /// <summary>
    /// An awaitable of the engine: the waits of the frame (NextFrameAsync, WaitForSecondsAsync, FixedUpdateAsync,
    /// EndOfFrameAsync, FromAsyncOperation) on the coroutine scheduler's resume points, thread switches, and async
    /// methods returning Awaitable. Instances are pooled: await each one once.
    /// </summary>
    [AsyncMethodBuilder(typeof(Awaitable.AwaitableAsyncMethodBuilder))]
    public class Awaitable : IEnumerator
    {
        // Where its continuation runs.
        internal enum Resume
        {
            // The thread kind the awaiting method was on.
            Captured,
            MainThread,
            Background,
        }

        readonly object m_Lock = new();
        Action m_Continuation;
        bool m_Completed;
        bool m_AwaitedOnMain;
        bool m_Pooled;
        // A thread switch: completed when its continuation is given (so the continuation never runs on the thread it
        // leaves).
        bool m_Switch;
        Exception m_Exception;
        CancellationTokenRegistration m_Registration;
        internal ulong m_Wait;
        internal AsyncOperation m_Operation;
        internal Resume m_Resume;

        static readonly ConcurrentBag<Awaitable> s_Pool = new();

        internal Awaitable() { }

        internal static Awaitable Rent()
        {
            if (!s_Pool.TryTake(out Awaitable a))
                a = new Awaitable();
            a.Clear();
            a.m_Pooled = true;
            return a;
        }

        void Clear()
        {
            m_Continuation = null;
            m_Completed = false;
            m_Exception = null;
            m_Registration = default;
            m_Wait = 0;
            m_Operation = null;
            m_Resume = Resume.Captured;
            m_Switch = false;
        }

        /// <summary>The awaitable ran to completion (or was canceled).</summary>
        public bool IsCompleted
        {
            get
            {
                lock (m_Lock)
                    return m_Completed;
            }
        }

        /// <summary>Ends the wait: the awaiter gets OperationCanceledException.</summary>
        public void Cancel()
        {
            if (IsCompleted)
                return;
            if (AwaitableScheduler.IsMainThread)
            {
                AwaitableScheduler.StopWait(this);
                Complete(new OperationCanceledException());
            }
            else
                AwaitableScheduler.Post(Cancel);
        }

        public Awaiter GetAwaiter() => new Awaiter(this);

        // A coroutine may yield an awaitable: it waits while it is not completed.
        object IEnumerator.Current => null;
        bool IEnumerator.MoveNext() => !IsCompleted;
        void IEnumerator.Reset() { }

        internal void WatchCancellation(CancellationToken token)
        {
            if (!token.CanBeCanceled)
                return;
            if (token.IsCancellationRequested)
            {
                Complete(new OperationCanceledException(token));
                return;
            }
            m_Registration = token.Register(static a => ((Awaitable)a).Cancel(), this);
        }

        internal bool TryComplete(Exception e)
        {
            Action continuation;
            lock (m_Lock)
            {
                if (m_Completed)
                    return false;
                m_Completed = true;
                m_Exception = e;
                continuation = m_Continuation;
                m_Continuation = null;
            }
            m_Registration.Dispose();
            if (continuation != null)
                Dispatch(continuation);
            return true;
        }

        internal void Complete(Exception e) => TryComplete(e);

        void Dispatch(Action continuation)
        {
            bool main = m_Resume switch
            {
                Resume.MainThread => true,
                Resume.Background => false,
                _ => m_AwaitedOnMain,
            };
            if (main == AwaitableScheduler.IsMainThread)
                continuation();
            else if (main)
                AwaitableScheduler.Post(continuation);
            else
                ThreadPool.UnsafeQueueUserWorkItem(static c => ((Action)c)(), continuation);
        }

        internal void OnCompleted(Action continuation)
        {
            bool now;
            lock (m_Lock)
            {
                m_AwaitedOnMain = AwaitableScheduler.IsMainThread;
                if (m_Switch && !m_Completed)
                    m_Completed = true;
                now = m_Completed;
                if (!now)
                    m_Continuation = continuation;
            }
            if (now)
                Dispatch(continuation);
        }

        // The result of an await: the exception it ended with; pooled instances go back to the pool.
        internal void GetResult()
        {
            Exception e;
            lock (m_Lock)
                e = m_Exception;
            if (m_Pooled)
            {
                m_Pooled = false;
                s_Pool.Add(this);
            }
            if (e != null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e).Throw();
        }

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            readonly Awaitable m_Awaitable;
            internal Awaiter(Awaitable awaitable) => m_Awaitable = awaitable;
            public bool IsCompleted => m_Awaitable.IsCompleted;
            public void OnCompleted(Action continuation) => m_Awaitable.OnCompleted(continuation);
            public void UnsafeOnCompleted(Action continuation) => m_Awaitable.OnCompleted(continuation);
            public void GetResult() => m_Awaitable.GetResult();
        }

        // ------------------------------------------------------------------------------------------- the waits

        /// <summary>Resumes after the next frame's Update (main thread only).</summary>
        public static Awaitable NextFrameAsync(CancellationToken cancellationToken = default) => AwaitableScheduler.Wait(1, 0, cancellationToken);

        /// <summary>Resumes once the seconds of scaled time passed, after Update (main thread only).</summary>
        public static Awaitable WaitForSecondsAsync(float seconds, CancellationToken cancellationToken = default) =>
            AwaitableScheduler.Wait(2, seconds, cancellationToken);

        /// <summary>Resumes at the end of the next fixed update step.</summary>
        public static Awaitable FixedUpdateAsync(CancellationToken cancellationToken = default) => AwaitableScheduler.Wait(4, 0, cancellationToken);

        /// <summary>Resumes at the end of the frame, after rendering.</summary>
        public static Awaitable EndOfFrameAsync(CancellationToken cancellationToken = default) => AwaitableScheduler.Wait(5, 0, cancellationToken);

        /// <summary>Resumes when the operation is done.</summary>
        public static Awaitable FromAsyncOperation(AsyncOperation op, CancellationToken cancellationToken = default)
        {
            if (op == null || op.isDone)
            {
                Awaitable done = Rent();
                done.Complete(null);
                return done;
            }
            return AwaitableScheduler.Wait(6, 0, cancellationToken, op);
        }

        /// <summary>Continues on a thread pool thread (at once when already on one).</summary>
        public static Awaitable BackgroundThreadAsync()
        {
            Awaitable a = Rent();
            a.m_Resume = Resume.Background;
            if (!AwaitableScheduler.IsMainThread)
                a.Complete(null);
            else
                a.m_Switch = true;
            return a;
        }

        /// <summary>Continues on the main thread (at once when on it, else in the next frame's Update).</summary>
        public static Awaitable MainThreadAsync()
        {
            Awaitable a = Rent();
            a.m_Resume = Resume.MainThread;
            if (AwaitableScheduler.IsMainThread)
                a.Complete(null);
            else
                a.m_Switch = true;
            return a;
        }

        /// <summary>The builder of async methods returning Awaitable.</summary>
        public struct AwaitableAsyncMethodBuilder
        {
            Awaitable m_Task;
            Action m_MoveNext;

            public static AwaitableAsyncMethodBuilder Create() => default;

            public Awaitable Task => m_Task ??= Awaitable.Rent();

            public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => stateMachine.MoveNext();

            public void SetStateMachine(IAsyncStateMachine stateMachine) => m_MoveNext = stateMachine.MoveNext;

            public void SetResult() => Task.Complete(null);

            public void SetException(Exception exception) => Task.Complete(exception);

            public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
                where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine => awaiter.OnCompleted(MoveNextOf(ref stateMachine));

            public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
                where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine => awaiter.UnsafeOnCompleted(MoveNextOf(ref stateMachine));

            // The state machine boxed once (its copy of this builder keeps the box's MoveNext).
            Action MoveNextOf<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
            {
                if (m_MoveNext != null)
                    return m_MoveNext;
                _ = Task;
                IAsyncStateMachine box = stateMachine;
                box.SetStateMachine(box);
                return m_MoveNext = box.MoveNext;
            }
        }

        /// <summary>The builder of async methods returning Awaitable&lt;T&gt;.</summary>
        public struct AwaitableAsyncMethodBuilder<T>
        {
            Awaitable<T> m_Task;
            Action m_MoveNext;

            public static AwaitableAsyncMethodBuilder<T> Create() => default;

            public Awaitable<T> Task => m_Task ??= new Awaitable<T>(Awaitable.Rent());

            public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => stateMachine.MoveNext();

            public void SetStateMachine(IAsyncStateMachine stateMachine) => m_MoveNext = stateMachine.MoveNext;

            public void SetResult(T result) => Task.SetResult(result);

            public void SetException(Exception exception) => Task.m_Core.Complete(exception);

            public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
                where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine => awaiter.OnCompleted(MoveNextOf(ref stateMachine));

            public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
                where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine => awaiter.UnsafeOnCompleted(MoveNextOf(ref stateMachine));

            Action MoveNextOf<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
            {
                if (m_MoveNext != null)
                    return m_MoveNext;
                _ = Task;
                IAsyncStateMachine box = stateMachine;
                box.SetStateMachine(box);
                return m_MoveNext = box.MoveNext;
            }
        }
    }

    /// <summary>An awaitable with a result (async methods returning Awaitable&lt;T&gt;, completion sources).</summary>
    [AsyncMethodBuilder(typeof(Awaitable.AwaitableAsyncMethodBuilder<>))]
    public class Awaitable<T>
    {
        internal readonly Awaitable m_Core;
        internal T m_Result;

        internal Awaitable(Awaitable core) => m_Core = core;

        public bool IsCompleted => m_Core.IsCompleted;
        public void Cancel() => m_Core.Cancel();
        public Awaiter GetAwaiter() => new Awaiter(this);

        internal void SetResult(T result)
        {
            m_Result = result;
            m_Core.Complete(null);
        }

        internal bool TrySetResult(T result)
        {
            if (m_Core.IsCompleted)
                return false;
            m_Result = result;
            return m_Core.TryComplete(null);
        }

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            readonly Awaitable<T> m_Awaitable;
            internal Awaiter(Awaitable<T> awaitable) => m_Awaitable = awaitable;
            public bool IsCompleted => m_Awaitable.IsCompleted;
            public void OnCompleted(Action continuation) => m_Awaitable.m_Core.OnCompleted(continuation);
            public void UnsafeOnCompleted(Action continuation) => m_Awaitable.m_Core.OnCompleted(continuation);

            public T GetResult()
            {
                T result = m_Awaitable.m_Result;
                m_Awaitable.m_Core.GetResult();
                return result;
            }
        }

    }

    /// <summary>An Awaitable completed by script code.</summary>
    public class AwaitableCompletionSource
    {
        public Awaitable Awaitable { get; private set; } = new Awaitable();

        public void SetResult()
        {
            if (!TrySetResult())
                throw new InvalidOperationException("the awaitable is already completed");
        }

        public void SetCanceled()
        {
            if (!TrySetCanceled())
                throw new InvalidOperationException("the awaitable is already completed");
        }

        public void SetException(Exception exception)
        {
            if (!TrySetException(exception))
                throw new InvalidOperationException("the awaitable is already completed");
        }

        public bool TrySetResult() => Awaitable.TryComplete(null);
        public bool TrySetCanceled() => Awaitable.TryComplete(new OperationCanceledException());
        public bool TrySetException(Exception exception) => Awaitable.TryComplete(exception ?? throw new ArgumentNullException(nameof(exception)));

        /// <summary>A new Awaitable for the next completion.</summary>
        public void Reset() => Awaitable = new Awaitable();
    }

    /// <summary>An Awaitable&lt;T&gt; completed by script code.</summary>
    public class AwaitableCompletionSource<T>
    {
        public Awaitable<T> Awaitable { get; private set; } = new Awaitable<T>(new Awaitable());

        public void SetResult(T value)
        {
            if (!TrySetResult(value))
                throw new InvalidOperationException("the awaitable is already completed");
        }

        public void SetCanceled()
        {
            if (!TrySetCanceled())
                throw new InvalidOperationException("the awaitable is already completed");
        }

        public void SetException(Exception exception)
        {
            if (!TrySetException(exception))
                throw new InvalidOperationException("the awaitable is already completed");
        }

        public bool TrySetResult(T value) => Awaitable.TrySetResult(value);
        public bool TrySetCanceled() => Awaitable.m_Core.TryComplete(new OperationCanceledException());
        public bool TrySetException(Exception exception) => Awaitable.m_Core.TryComplete(exception ?? throw new ArgumentNullException(nameof(exception)));

        public void Reset() => Awaitable = new Awaitable<T>(new Awaitable());
    }

    public partial class Application
    {
        static CancellationTokenSource s_Exit;

        /// <summary>Canceled when the application quits.</summary>
        public static CancellationToken exitCancellationToken
        {
            get
            {
                if (s_Exit == null)
                {
                    s_Exit = new CancellationTokenSource();
                    quitting += () => s_Exit.Cancel();
                }
                return s_Exit.Token;
            }
        }
    }
}

namespace HelixEngine.Internal
{
    /// <summary>
    /// The engine side of awaitables: their waits on the coroutine scheduler (main thread), the main thread's
    /// SynchronizationContext and its queue, run once per frame in the Update phase (ScriptRunDelayedTasks).
    /// </summary>
    internal static unsafe class AwaitableScheduler
    {
        static delegate* unmanaged[Cdecl]<uint, ulong, uint, double, ulong, ulong> s_Wait;
        static delegate* unmanaged[Cdecl]<ulong, void> s_Stop;
        static uint s_Language = uint.MaxValue;
        static int s_MainThread;
        static readonly ConcurrentQueue<(SendOrPostCallback, object)> s_Posted = new();
        static MainThreadContext s_Context;

        public static bool IsMainThread => Environment.CurrentManagedThreadId == s_MainThread;

        public static void Start(Func<string, IntPtr> export)
        {
            s_MainThread = Environment.CurrentManagedThreadId;
            var register = (delegate* unmanaged[Cdecl]<void*, void*, void*, uint>)export("HxCoroutines_RegisterLanguage");
            s_Wait = (delegate* unmanaged[Cdecl]<uint, ulong, uint, double, ulong, ulong>)export("HxCoroutines_Wait");
            s_Stop = (delegate* unmanaged[Cdecl]<ulong, void>)export("HxCoroutines_Stop");
            var addRunner = (delegate* unmanaged[Cdecl]<void*, void>)export("HxEngine_AddDelayedTaskRunner");
            if (register != null)
                s_Language = register((delegate* unmanaged[Cdecl]<ulong, ulong, CoroutineYield*, void>)&Resume,
                                      (delegate* unmanaged[Cdecl]<ulong, ulong, byte>)&PollWait, (delegate* unmanaged[Cdecl]<ulong, void>)&Release);
            if (addRunner != null)
                addRunner((delegate* unmanaged[Cdecl]<void>)&RunPosted);
            s_Context = new MainThreadContext();
            SynchronizationContext.SetSynchronizationContext(s_Context);
        }

        internal static Awaitable Wait(uint wait, double seconds, CancellationToken token, AsyncOperation operation = null)
        {
            if (!IsMainThread)
                throw new InvalidOperationException("the waits of Awaitable can only be started on the main thread");
            Awaitable a = Awaitable.Rent();
            a.m_Operation = operation;
            if (s_Wait == null || s_Language == uint.MaxValue)
            {
                a.Complete(null);
                return a;
            }
            ulong state = (ulong)GCHandle.ToIntPtr(GCHandle.Alloc(a));
            a.m_Wait = s_Wait(s_Language, state, wait, seconds, 0);
            a.WatchCancellation(token);
            return a;
        }

        internal static void StopWait(Awaitable a)
        {
            ulong wait = a.m_Wait;
            a.m_Wait = 0;
            if (wait != 0 && s_Stop != null)
                s_Stop(wait);
        }

        internal static void Post(Action action) => s_Posted.Enqueue((static s => ((Action)s)(), action));
        internal static void Post(SendOrPostCallback callback, object state) => s_Posted.Enqueue((callback, state));

        // The queued work of this frame (work queued while it runs waits for the next frame).
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void RunPosted()
        {
            int count = s_Posted.Count;
            for (int i = 0; i < count && s_Posted.TryDequeue(out (SendOrPostCallback Callback, object State) work); i++)
            {
                try
                {
                    work.Callback(work.State);
                }
                catch (Exception e)
                {
                    ScriptBridge.ReportCoroutine(null, e);
                }
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Resume(ulong state, ulong handle, CoroutineYield* y)
        {
            *y = default;
            if (GCHandle.FromIntPtr((IntPtr)state).Target is Awaitable a)
            {
                a.m_Wait = 0;
                try
                {
                    a.Complete(null);
                }
                catch (Exception e)
                {
                    ScriptBridge.ReportCoroutine(null, e);
                }
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static byte PollWait(ulong state, ulong handle) =>
            GCHandle.FromIntPtr((IntPtr)state).Target is Awaitable a && a.m_Operation != null && !a.m_Operation.isDone ? (byte)1 : (byte)0;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Release(ulong state) => GCHandle.FromIntPtr((IntPtr)state).Free();

        // The main thread's context: posted work runs in the next frame's Update phase.
        sealed class MainThreadContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state) => AwaitableScheduler.Post(d, state);

            public override void Send(SendOrPostCallback d, object state)
            {
                if (IsMainThread)
                {
                    d(state);
                    return;
                }
                using var done = new ManualResetEventSlim();
                AwaitableScheduler.Post(s =>
                {
                    try
                    {
                        d(s);
                    }
                    finally
                    {
                        done.Set();
                    }
                }, state);
                done.Wait();
            }

            public override SynchronizationContext CreateCopy() => this;
        }
    }
}
