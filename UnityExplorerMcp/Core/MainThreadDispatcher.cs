using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace UnityExplorerMcp.Core
{
    internal static class MainThreadDispatcher
    {
        private static SynchronizationContext _mainContext;
        private static bool _initialized = false;

        public static void InitializeFromMainThread()
        {
            _mainContext = SynchronizationContext.Current;
            _initialized = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _mainContext = SynchronizationContext.Current;
            _initialized = true;
        }

        public static object Execute(Func<object> func, int timeoutMs = 10000)
        {
            if (!_initialized)
            {
                _mainContext = SynchronizationContext.Current;
                _initialized = true;
            }

            var mre = new ManualResetEventSlim(false);
            object result = null;
            Exception exception = null;

            _mainContext.Post(_ =>
            {
                try { result = func(); }
                catch (Exception ex) { exception = ex; }
                finally { mre.Set(); }
            }, null);

            if (!mre.Wait(timeoutMs))
                throw new TimeoutException("Main thread execution timed out after " + timeoutMs + "ms");

            if (exception != null)
                throw exception;

            return result;
        }

        public static bool ExecuteAsync(Action action)
        {
            if (!_initialized)
            {
                _mainContext = SynchronizationContext.Current;
                _initialized = true;
            }
            if (_mainContext == null) return false;
            _mainContext.Post(_ => action(), null);
            return true;
        }
    }
}
