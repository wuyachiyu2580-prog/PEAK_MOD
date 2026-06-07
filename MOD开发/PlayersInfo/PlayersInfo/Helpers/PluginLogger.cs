using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace PlayersInfo.Helpers
{
    /// <summary>
    /// 统一日志入口 + 节流。所有模块只用 PluginLogger.Info/Warn/Error，不要直接 Debug.Log。
    /// ThrottleInfo 在同 key 的 intervalSec 秒内只打一次，避免刷屏。
    /// </summary>
    internal static class PluginLogger
    {
        public static ManualLogSource Log;

        private static readonly Dictionary<string, float> _throttle = new Dictionary<string, float>();

        public static void Info(string msg) => Log?.LogInfo("[PlayersInfo] " + msg);
        public static bool DebugEnabled => global::PlayersInfo.PlayersInfoPlugin.CfgDebugLogging != null && global::PlayersInfo.PlayersInfoPlugin.CfgDebugLogging.Value;
        public static void Debug(string msg)
        {
            if (DebugEnabled) Info(msg);
        }
        public static void Warn(string msg) => Log?.LogWarning("[PlayersInfo] " + msg);
        public static void Error(string msg) => Log?.LogError("[PlayersInfo] " + msg);

        /// <summary>同 key 节流：intervalSec 秒内只记一条。</summary>
        public static void ThrottleInfo(string key, string msg, float intervalSec = 5f)
        {
            float now = Time.unscaledTime;
            if (_throttle.TryGetValue(key, out float last) && now - last < intervalSec) return;
            _throttle[key] = now;
            Log?.LogInfo("[PlayersInfo] " + msg);
        }

        public static void ThrottleDebug(string key, string msg, float intervalSec = 5f)
        {
            if (!DebugEnabled) return;
            ThrottleInfo(key, msg, intervalSec);
        }

        public static void ThrottleWarn(string key, string msg, float intervalSec = 5f)
        {
            float now = Time.unscaledTime;
            if (_throttle.TryGetValue(key, out float last) && now - last < intervalSec) return;
            _throttle[key] = now;
            Log?.LogWarning("[PlayersInfo] " + msg);
        }

        public static void ThrottleError(string key, string msg, float intervalSec = 5f)
        {
            float now = Time.unscaledTime;
            if (_throttle.TryGetValue(key, out float last) && now - last < intervalSec) return;
            _throttle[key] = now;
            Log?.LogError("[PlayersInfo] " + msg);
        }

        public static void ClearThrottle() => _throttle.Clear();
    }
}
