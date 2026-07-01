using System;
using System.Threading.Tasks;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;

namespace hp55games.Mobile.Core
{
    /// <summary>
    /// Fire-and-forget helper for async work started from void entry points
    /// (Unity lifecycle methods, UI button callbacks).
    /// Catches all exceptions and routes them through ILog when available,
    /// falling back to Debug.LogException so nothing is ever silently swallowed.
    /// </summary>
    public static class AsyncUtils
    {
        /// <param name="task">The task to run fire-and-forget.</param>
        /// <param name="log">Optional ILog instance for structured error output.</param>
        /// <param name="context">Caller name shown in the error message, e.g. nameof(MyClass).</param>
        public static async void FireAndForget(Task task, ILog log = null, string context = null)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                if (log != null)
                {
                    var label = string.IsNullOrEmpty(context) ? "AsyncUtils" : context;
                    log.Error($"[{label}] Unhandled exception: {ex}");
                }
                else
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
