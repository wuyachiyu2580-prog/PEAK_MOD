using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace DreamyAscent.Helpers
{
    internal static class DaLog
    {
        private const string Prefix = "[DreamyAscent] ";
        private static readonly HashSet<string> s_once = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> s_throttle = new Dictionary<string, float>(StringComparer.Ordinal);

        public static ManualLogSource Log { get; set; }

        public static void Info(string message)
        {
            Write(message, LogLevel.Info);
        }

        public static void Warn(string message)
        {
            Write(message, LogLevel.Warning);
        }

        public static void Error(string message)
        {
            Write(message, LogLevel.Error);
        }

        public static void Error(string message, Exception ex)
        {
            Write(message + " :: " + ex, LogLevel.Error);
        }

        public static void OnceWarn(string key, string message)
        {
            if (s_once.Add(key))
            {
                Warn(message);
            }
        }

        public static void ThrottleInfo(string key, string message, float intervalSeconds = 5f)
        {
            if (ShouldWriteThrottled(key, intervalSeconds))
            {
                Info(message);
            }
        }

        private static void Write(string message, LogLevel level)
        {
            string finalMessage = Prefix + message;

            if (Log != null)
            {
                switch (level)
                {
                    case LogLevel.Warning:
                        Log.LogWarning(finalMessage);
                        return;
                    case LogLevel.Error:
                        Log.LogError(finalMessage);
                        return;
                    default:
                        Log.LogInfo(finalMessage);
                        return;
                }
            }

            switch (level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(finalMessage);
                    break;
                case LogLevel.Error:
                    Debug.LogError(finalMessage);
                    break;
                default:
                    Debug.Log(finalMessage);
                    break;
            }
        }

        private static bool ShouldWriteThrottled(string key, float intervalSeconds)
        {
            float now = Time.unscaledTime;
            if (s_throttle.TryGetValue(key, out float lastTime) && now - lastTime < intervalSeconds)
            {
                return false;
            }

            s_throttle[key] = now;
            return true;
        }

        private enum LogLevel
        {
            Info,
            Warning,
            Error
        }
    }
}


