#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace GPFS.Editor
{
    public static class Debounce
    {
        private static readonly Dictionary<string, double> lastApplyTime = new();
        private static readonly Dictionary<string, Action> pending = new();
        private static bool scheduled;

        public static void Request(string key, double seconds, Action action, bool force = false)
        {
            if (force)
            {
                Execute(key, seconds, action);
                return;
            }

            pending[key] = action;
            Schedule();
        }

        private static void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += Flush;
        }

        private static void Flush()
        {
            scheduled = false;
            foreach (var key in pending.Keys)
                Execute(key, 0, pending[key]);

            pending.Clear();
        }

        public static void Cancel(string key)
        {
            pending.Remove(key);
            lastApplyTime.Remove(key);
        }

        private static void Execute(string key, double seconds, Action action)
        {
            double now = EditorApplication.timeSinceStartup;

            if (seconds > 0 &&
                lastApplyTime.TryGetValue(key, out var last) &&
                now - last < seconds)
                return;

            lastApplyTime[key] = now;
            action?.Invoke();
        }
    }
}
#endif
