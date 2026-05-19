using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayersInfo.Helpers
{
    /// <summary>
    /// 队友名册跟踪器。不轮询每帧 Character.AllCharacters，而是增量维护：
    /// - 启动时填充一次
    /// - 每 0.5s 低频补偿扫描（弥补中途加入/退出 Photon 事件可能错过的情况）
    /// - 对外只暴露 Teammates（不含 localCharacter）、事件 OnRosterChanged
    /// 解决 PeakStats 中途加入时显示所有人的 bug：每次变化时重新过滤 localCharacter。
    /// </summary>
    internal class TeamRosterTracker : MonoBehaviour
    {
        public static TeamRosterTracker Instance { get; private set; }

        private readonly List<Character> _teammates = new List<Character>();
        private readonly HashSet<int> _teammateViewIds = new HashSet<int>();
        private float _nextScanTime;
        private const float ScanInterval = 0.5f;

        /// <summary>队友列表（不含本机 localCharacter，不含已销毁对象）。</summary>
        public IReadOnlyList<Character> Teammates => _teammates;

        /// <summary>名册发生增/删时触发（Add/Remove 合并后单次触发）。</summary>
        public event Action OnRosterChanged;

        public static TeamRosterTracker EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PlayersInfo.TeamRosterTracker");
            GameObject.DontDestroyOnLoad(go);
            Instance = go.AddComponent<TeamRosterTracker>();
            return Instance;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScanTime) return;
            _nextScanTime = Time.unscaledTime + ScanInterval;
            SafeScan();
        }

        private void SafeScan()
        {
            try
            {
                bool changed = false;
                var local = Character.localCharacter;

                // 先清理已销毁/已变为本地的项
                for (int i = _teammates.Count - 1; i >= 0; i--)
                {
                    var c = _teammates[i];
                    if (c == null || c.Equals(null) || c == local || !c.IsPlayerControlled)
                    {
                        int vid = (c != null && c.photonView != null) ? c.photonView.ViewID : -1;
                        _teammates.RemoveAt(i);
                        if (vid >= 0) _teammateViewIds.Remove(vid);
                        changed = true;
                    }
                }

                // 增量添加
                var all = Character.AllCharacters;
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        var c = all[i];
                        if (c == null || c.Equals(null)) continue;
                        if (c == local) continue;
                        if (!c.IsPlayerControlled) continue;
                        if (c.photonView == null) continue;
                        int vid = c.photonView.ViewID;
                        if (_teammateViewIds.Contains(vid)) continue;
                        _teammates.Add(c);
                        _teammateViewIds.Add(vid);
                        changed = true;
                    }
                }

                if (changed)
                {
                    try { OnRosterChanged?.Invoke(); }
                    catch (Exception ex) { PluginLogger.ThrottleError("roster_event", "OnRosterChanged handler failed: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleError("roster_scan", "TeamRosterTracker scan failed: " + ex.Message);
            }
        }

        /// <summary>供 Patch/外部显式触发扫描（比如收到 Photon 加入/退出事件时）。</summary>
        public void RequestRescan()
        {
            _nextScanTime = 0f;
        }

        /// <summary>场景切换时调用，清空名册并触发一次变化事件。</summary>
        public void ClearForSceneReload()
        {
            _teammates.Clear();
            _teammateViewIds.Clear();
            _nextScanTime = 0f;
            try { OnRosterChanged?.Invoke(); } catch { }
        }
    }
}
