using System;
using UnityEngine;

namespace WhySoLaggy
{
    /// <summary>
    /// 可拖拽性能浮窗（1.0.3 新增）。仅在 ShowDashboard=true 时生效。
    /// 显示：当前 FPS、平均帧时间、最近分配速率、最近 abuse 摘要、watched RPC 摘要。
    /// 关闭时零开销。
    /// </summary>
    internal static class PerformanceDashboard
    {
        public static bool ShowDashboard = false;

        private static Rect _windowRect = new Rect(16f, 16f, 380f, 220f);
        private const int WindowId = 0x1A9ED;

        private static string _lastAlert = "";
        private static float _lastAlertTime = -1f;

        /// <summary>由 AbuseLogger.Alert 间接调用，向 Dashboard 推送最近一条 alert。</summary>
        public static void ReportAlert(string msg)
        {
            _lastAlert = msg ?? "";
            _lastAlertTime = Time.realtimeSinceStartup;
        }

        public static void DrawGUI()
        {
            if (!ShowDashboard) return;
            try
            {
                _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "WhySoLaggy");
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] Dashboard draw failed: {ex.Message}");
            }
        }

        private static void DrawWindow(int id)
        {
            float frameMs = FpsTracker.CurrentFrameMs;
            float fps = frameMs > 0.01f ? 1000f / frameMs : 0f;
            float avgMs = FpsTracker.GetWindowAvgMs();
            float allocKBps = FpsTracker.GetAllocRateKBps();

            GUILayout.BeginVertical();
            GUILayout.Label($"FPS: {fps:F1}  |  Frame: {frameMs:F1}ms  |  AvgMs(win): {avgMs:F1}");
            if (FpsTracker.EnableMemoryMonitor)
                GUILayout.Label($"AllocRate: {allocKBps:F1} KB/s");

            GUILayout.Space(4f);
            GUILayout.Label("Watched RPC (current period):");
            GUILayout.Label("  " + RpcMonitor.GetRecentWatchedSummary(8));

            GUILayout.Space(4f);
            if (!string.IsNullOrEmpty(_lastAlert))
            {
                float ago = _lastAlertTime < 0 ? 0 : (Time.realtimeSinceStartup - _lastAlertTime);
                GUILayout.Label($"Last alert ({ago:F0}s ago):");
                GUILayout.Label("  " + _lastAlert);
            }
            else
            {
                GUILayout.Label("No alerts yet");
            }

            GUILayout.EndVertical();

            GUI.DragWindow();
        }
    }
}
