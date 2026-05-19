using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 房主专属：清理"无主 + 远离所有玩家 + 持续 60s"的流浪号角。
    ///
    /// 目的：防止单爬玩家把号角扔野外锁死房主 F8 补刷逻辑
    /// （F8 刷号角要求"全场无号角"）。
    ///
    /// 规则：
    /// - 5s 扫描一次场景 BugleSFX
    /// - 无人持有（holderCharacter == null）
    /// - 到所有活玩家最近距离 > StrayBugleDistance（默认 100m）
    /// - 连续处于该状态 >= StrayBugleGracePeriod（默认 60s）→ Destroy
    /// - 仅房主执行；房主切换时清空所有计时器
    /// </summary>
    internal static class StrayBugleCleaner
    {
        private const float ScanInterval = 5f;

        // TransferOwnership 重发节流：同一 viewID 至少间隔 15s 才重请，累计 5 次仍不成功 → 拉黑
        // 背景：StrayBugleCleaner 父机不响应时每 5s 一次重发干喂网络，
        // 典型场景是号角 owner 断线/卡死，重试再多也没用
        private const float OwnershipRetryInterval = 15f;
        private const int OwnershipMaxRetries = 5;

        private static float _lastScanTime = -999f;
        private static readonly Dictionary<int, float> _strayStart = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> _transferLastTime = new Dictionary<int, float>();
        private static readonly Dictionary<int, int> _transferAttemptCount = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> _transferLastOwnerActor = new Dictionary<int, int>();
        private static readonly HashSet<int> _transferBlacklist = new HashSet<int>();
        private static bool _lastWasMaster;

        public static void Tick()
        {
            if (Plugin.StrayBugleCleanupEnabled == null || !Plugin.StrayBugleCleanupEnabled.Value) return;
            if (!PhotonNetwork.IsConnected) return;

            // 房主切换 → 重置计时器 + 所有权重发状态
            bool isMaster = PhotonNetwork.IsMasterClient;
            if (isMaster != _lastWasMaster)
            {
                _lastWasMaster = isMaster;
                if (_strayStart.Count > 0 || _transferAttemptCount.Count > 0 || _transferBlacklist.Count > 0)
                {
                    _strayStart.Clear();
                    _transferLastTime.Clear();
                    _transferAttemptCount.Clear();
                    _transferLastOwnerActor.Clear();
                    _transferBlacklist.Clear();
                    Plugin.Log?.LogInfo($"[StrayBugleCleaner] master-state changed (now master={isMaster}) → reset timers & ownership retry state");
                }
            }
            if (!isMaster) return;

            if (Time.time - _lastScanTime < ScanInterval) return;
            _lastScanTime = Time.time;

            float distThreshold = Plugin.StrayBugleDistance.Value;
            float grace = Plugin.StrayBugleGracePeriod.Value;

            BugleSFX[] all = UnityEngine.Object.FindObjectsByType<BugleSFX>(FindObjectsSortMode.None);
            HashSet<int> seenIds = new HashSet<int>();

            foreach (var bugle in all)
            {
                if (bugle == null || bugle.photonView == null) continue;
                int vId = bugle.photonView.ViewID;
                seenIds.Add(vId);

                Item item = bugle.GetComponent<Item>();

                // 有人拿着 → 重置
                if (item == null || item.holderCharacter != null)
                {
                    _strayStart.Remove(vId);
                    continue;
                }

                // 算到所有活玩家最近距离
                float minDist = float.MaxValue;
                int liveCount = 0;
                Vector3 bPos = bugle.transform.position;
                if (Character.AllCharacters != null)
                {
                    foreach (var ch in Character.AllCharacters)
                    {
                        if (ch == null || ch.data == null || ch.data.dead) continue;
                        liveCount++;
                        float d = Vector3.Distance(ch.Center, bPos);
                        if (d < minDist) minDist = d;
                    }
                }

                // 没活玩家（刚进房间玩家未初始化完 / 所有人死了）→ 跳过判定，避免 minDist=MaxValue 误触发
                if (liveCount == 0)
                {
                    continue;
                }

                if (minDist <= distThreshold)
                {
                    _strayStart.Remove(vId);
                    continue;
                }

                // 进入 stray 状态
                if (!_strayStart.TryGetValue(vId, out float start))
                {
                    _strayStart[vId] = Time.time;
                    Plugin.Log?.LogInfo($"[StrayBugleCleaner] bugle ViewID={vId} stray started (minDist={minDist:F0}m, threshold={distThreshold:F0}m)");
                    continue;
                }

                float elapsed = Time.time - start;
                if (elapsed < grace) continue;

                // 满足销毁条件 — 但 PUN 要求 Destroy 者 、IsMine=true，
                // 号角往往被最后拾过的玩家 own。先请求所有权，下一次扫描再销毁。
                var pv = bugle.photonView;
                if (!pv.IsMine)
                {
                    // 记录当前 owner，便于排查 TransferOwnership 静默失败（每次扫描都重发但父机不响应）
                    var owner = pv.Owner;
                    string ownerInfo = owner == null
                        ? "null(scene?)"
                        : $"{owner.NickName}#{owner.ActorNumber}{(owner.IsInactive ? "[INACTIVE]" : "")}";
                    int ownerActor = owner?.ActorNumber ?? -1;

                    // 黑名单：累计失败太多 → 直接跳，不再烦网络
                    if (_transferBlacklist.Contains(vId))
                    {
                        continue;
                    }

                    // 如果 owner 变了（对方可能重新上线/ PUN cleanup 接管），重置计数，值得重试
                    int lastOwnerActor;
                    if (_transferLastOwnerActor.TryGetValue(vId, out lastOwnerActor) && lastOwnerActor != ownerActor)
                    {
                        _transferAttemptCount[vId] = 0;
                        Plugin.Log?.LogInfo($"[StrayBugleCleaner] ViewID={vId} owner changed ({lastOwnerActor}→{ownerActor}), reset retry count");
                    }

                    // 时间节流：间隔 15s 才重请
                    float lastAttempt;
                    if (_transferLastTime.TryGetValue(vId, out lastAttempt)
                        && Time.time - lastAttempt < OwnershipRetryInterval)
                    {
                        continue; // 静默等待，不打日志避免刷屏
                    }

                    // 尝试次数达上限 → 拉黑
                    int attempts;
                    _transferAttemptCount.TryGetValue(vId, out attempts);
                    if (attempts >= OwnershipMaxRetries)
                    {
                        _transferBlacklist.Add(vId);
                        Plugin.Log?.LogWarning($"[StrayBugleCleaner] ViewID={vId} reached max retries ({OwnershipMaxRetries}), blacklisted (currentOwner={ownerInfo})");
                        continue;
                    }

                    try
                    {
                        pv.TransferOwnership(PhotonNetwork.LocalPlayer);
                        _transferLastTime[vId] = Time.time;
                        _transferAttemptCount[vId] = attempts + 1;
                        _transferLastOwnerActor[vId] = ownerActor;
                        Plugin.Log?.LogInfo($"[StrayBugleCleaner] requested ownership for ViewID={vId}, currentOwner={ownerInfo}, attempt={attempts + 1}/{OwnershipMaxRetries} (will destroy next scan)");
                    }
                    catch (Exception ex)
                    {
                        _transferLastTime[vId] = Time.time;
                        _transferAttemptCount[vId] = attempts + 1;
                        _transferLastOwnerActor[vId] = ownerActor;
                        Plugin.Log?.LogWarning($"[StrayBugleCleaner] TransferOwnership failed ViewID={vId}, currentOwner={ownerInfo}, attempt={attempts + 1}: {ex.Message}");
                    }
                    continue; // 本次不销毁，等所有权转过来
                }

                // 拿到所有权了 → 销毁 + 清重发状态
                _transferLastTime.Remove(vId);
                _transferAttemptCount.Remove(vId);
                _transferLastOwnerActor.Remove(vId);

                try
                {
                    PhotonNetwork.Destroy(bugle.gameObject);
                    _strayStart.Remove(vId);
                    Plugin.Log?.LogInfo($"[StrayBugleCleaner] destroyed stray bugle ViewID={vId} (minDist={minDist:F0}m, stray={elapsed:F0}s)");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[StrayBugleCleaner] Destroy failed for ViewID={vId}: {ex.Message}");
                }
            }

            // 清理已消失的号角条目（含所有权重发状态）
            if (_strayStart.Count > 0 || _transferAttemptCount.Count > 0 || _transferBlacklist.Count > 0)
            {
                List<int> toRemove = null;
                foreach (var kv in _strayStart)
                {
                    if (!seenIds.Contains(kv.Key))
                    {
                        if (toRemove == null) toRemove = new List<int>();
                        toRemove.Add(kv.Key);
                    }
                }
                if (toRemove != null)
                {
                    foreach (var k in toRemove)
                    {
                        _strayStart.Remove(k);
                        _transferLastTime.Remove(k);
                        _transferAttemptCount.Remove(k);
                        _transferLastOwnerActor.Remove(k);
                        _transferBlacklist.Remove(k);
                    }
                }
            }
        }
    }
}
