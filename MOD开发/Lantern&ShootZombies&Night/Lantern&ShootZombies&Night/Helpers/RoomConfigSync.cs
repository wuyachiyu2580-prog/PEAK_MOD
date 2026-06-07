using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 主机配置同步：通过 Photon 房间自定义属性，将主机的 Config 值同步到所有客机。
    /// 客机在应用主机配置前会保存本地快照，离开房间时自动恢复，不会覆盖客机的 .cfg 文件。
    /// </summary>
    internal static class RoomConfigSync
    {
        private const string PropertyKey = "LSN.HostConfig";
        private const string VersionKey = "LSN.CfgVer";
        private const float PublishInterval = 5f;
        private const float PollInterval = 5f;

        // ── 状态字段 ──────────────────────────────────────────────
        private static bool _applyingPayload;

        /// <summary>是否正在应用主机配置（供 PresetManager 读取以防止重入）。</summary>
        public static bool IsApplyingPayload => _applyingPayload;
        private static string _activeRoomName = string.Empty;
        private static string _lastPublishedPayload = string.Empty;
        private static string _lastPublishedHash = string.Empty;
        private static string _cachedPayload = string.Empty;
        private static string _cachedPayloadHash = string.Empty;
        private static string _lastAppliedPayload = string.Empty;
        private static string _localBackupPayload = string.Empty;
        private static bool _hasLocalBackup;
        private static bool _wasMasterClient;
        private static float _lastPublishTime = -10f;
        private static float _lastPollTime = -10f;
        private static bool _configDirty = true;
        private static bool _callbacksRegistered;
        private static CallbackProxy _callbackProxy;
        private static float _lastImmediatePublishTime = -10f;

        // ── Public API ────────────────────────────────────────────

        /// <summary>在 Plugin.Awake 中调用，注册 Photon 回调并订阅配置变更。</summary>
        public static void Initialize(Plugin plugin)
        {
            try
            {
                if (!_callbacksRegistered)
                {
                    if (_callbackProxy == null)
                        _callbackProxy = new CallbackProxy();
                    PhotonNetwork.AddCallbackTarget(_callbackProxy);
                    _callbacksRegistered = true;
                }

                SubscribeConfigChanges(plugin);
                Plugin.Log?.LogInfo($"[RoomConfigSync] Initialized (publishInterval={PublishInterval}s, pollInterval={PollInterval}s)");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[RoomConfigSync] Initialize failed: {ex.Message}");
            }
        }

        /// <summary>在 Plugin.Update 中每帧调用。</summary>
        public static void UpdateSync()
        {
            try
            {
                if (!HasOnlineRoom())
                {
                    RestoreBackup();
                    ResetState(true);
                    return;
                }

                string roomName = PhotonNetwork.CurrentRoom.Name ?? string.Empty;
                bool roomChanged = false;

                if (!string.Equals(_activeRoomName, roomName, StringComparison.Ordinal))
                {
                    RestoreBackup();
                    ResetState(false);
                    _activeRoomName = roomName;
                    roomChanged = true;
                }

                if (PhotonNetwork.IsMasterClient)
                {
                    // 刚成为主机 -> 恢复本地配置并标记脏
                    if (!_wasMasterClient)
                    {
                        RestoreBackup();
                        MarkDirty(true);
                    }

                    // 定时发布
                    if ((_configDirty || string.IsNullOrEmpty(_lastPublishedPayload))
                        && Time.unscaledTime - _lastPublishTime >= PublishInterval)
                    {
                        PublishHostConfig(string.IsNullOrEmpty(_lastPublishedPayload));
                    }
                }
                else
                {
                    // 客机：先备份本地配置
                    CaptureBackup();

                    // 房间变更或首次 -> 立即应用
                    if (roomChanged || string.IsNullOrEmpty(_lastAppliedPayload))
                        ApplyHostConfigIfNeeded();

                    // 定时轮询
                    if (Time.unscaledTime - _lastPollTime >= PollInterval)
                    {
                        _lastPollTime = Time.unscaledTime;
                        ApplyHostConfigIfNeeded();
                    }
                }

                _wasMasterClient = PhotonNetwork.IsMasterClient;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[RoomConfigSync] UpdateSync error: {ex.Message}");
            }
        }

        /// <summary>在 Plugin.OnDestroy 中调用，清理回调。</summary>
        public static void Cleanup()
        {
            try
            {
                RestoreBackup();
                if (_callbacksRegistered && _callbackProxy != null)
                {
                    PhotonNetwork.RemoveCallbackTarget(_callbackProxy);
                    _callbacksRegistered = false;
                }
                UnsubscribeConfigChanges();
            }
            catch { }
        }

        // ── Photon 回调代理 ───────────────────────────────────────

        private sealed class CallbackProxy : IInRoomCallbacks
        {
            public void OnRoomPropertiesUpdate(Hashtable changed)
            {
                if (changed != null && changed.ContainsKey(PropertyKey)
                    && HasOnlineRoom() && !PhotonNetwork.IsMasterClient)
                {
                    _lastPollTime = Time.unscaledTime;
                    ApplyHostConfigIfNeeded();
                }
            }

            public void OnMasterClientSwitched(Photon.Realtime.Player newMaster)
            {
                string newName = newMaster?.NickName ?? "unknown";
                int newActor = newMaster?.ActorNumber ?? -1;
                bool iAmNewMaster = PhotonNetwork.IsMasterClient;
                Plugin.Log?.LogInfo($"[RoomConfigSync] OnMasterClientSwitched: new master='{newName}' (actor={newActor}), iAmMaster={iAmNewMaster}");
                _lastPollTime = -10f;
                if (iAmNewMaster)
                {
                    RestoreBackup();
                    MarkDirty(true);
                    Plugin.Log?.LogInfo("[RoomConfigSync] Became master: restored local backup, will republish config");
                }
                else
                {
                    CaptureBackup();
                    ApplyHostConfigIfNeeded();
                    Plugin.Log?.LogInfo("[RoomConfigSync] Became client: captured local backup, applying new host config");
                }
            }

            public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer) { }
            public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer) { }
            public void OnPlayerPropertiesUpdate(Photon.Realtime.Player target, Hashtable changed) { }
        }

        // ── 主机发布 ─────────────────────────────────────────────

        private static void PublishHostConfig(bool force)
        {
            if (_applyingPayload || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
                return;

            string payload = GetOrBuildPayload();
            string hash = _cachedPayloadHash;
            if (string.IsNullOrEmpty(payload))
            {
                _configDirty = false;
                _lastPublishTime = Time.unscaledTime;
                return;
            }

            if (!force && string.Equals(hash, _lastPublishedHash, StringComparison.Ordinal))
            {
                _configDirty = false;
                _lastPublishTime = Time.unscaledTime;
                return;
            }

            var props = new Hashtable
            {
                { PropertyKey, payload },
                { VersionKey, hash }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            _lastPublishedPayload = payload;
            _lastPublishedHash = hash;
            _lastAppliedPayload = payload;
            _configDirty = false;
            _lastPublishTime = Time.unscaledTime;

            Plugin.Log?.LogInfo($"[RoomConfigSync] Host published config ({payload.Length} chars, hash={hash})");
        }

        // ── 客机接收 ─────────────────────────────────────────────

        private static void ApplyHostConfigIfNeeded()
        {
            if (_applyingPayload || PhotonNetwork.IsMasterClient)
                return;

            // 快速路径：先比较哈希，一致则跳过反序列化
            string remoteHash;
            if (TryGetRoomProperty(VersionKey, out remoteHash)
                && !string.IsNullOrEmpty(remoteHash)
                && string.Equals(remoteHash, _lastPublishedHash, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(_lastAppliedPayload))
            {
                return;
            }

            string payload;
            if (!TryGetRoomProperty(PropertyKey, out payload) || string.IsNullOrEmpty(payload))
                return;

            if (string.Equals(payload, _lastAppliedPayload, StringComparison.Ordinal))
                return;

            ApplyPayload(payload);
            _lastAppliedPayload = payload;
            _lastPublishedHash = remoteHash ?? "";
            int fieldCount = 0;
            foreach (char c in payload) if (c == '|') fieldCount++;
            fieldCount++; // 最后一段没 | 分隔
            Plugin.Log?.LogInfo($"[RoomConfigSync] Applied host config: {fieldCount} fields, {payload.Length} chars, hash={remoteHash ?? ""}");
        }

        private static bool TryGetRoomProperty(string key, out string value)
        {
            value = null;
            var room = PhotonNetwork.CurrentRoom;
            if (room?.CustomProperties == null)
                return false;

            value = room.CustomProperties[key] as string;
            return !string.IsNullOrEmpty(value);
        }

        // ── 备份/恢复 ────────────────────────────────────────────

        private static void CaptureBackup()
        {
            if (!_hasLocalBackup)
            {
                _localBackupPayload = BuildPayload();
                _hasLocalBackup = !string.IsNullOrEmpty(_localBackupPayload);
                if (_hasLocalBackup)
                    Plugin.Log?.LogInfo("[RoomConfigSync] Local config backup captured");
            }
        }

        private static void RestoreBackup()
        {
            if (_hasLocalBackup && !string.IsNullOrEmpty(_localBackupPayload))
            {
                ApplyPayload(_localBackupPayload);
                Plugin.Log?.LogInfo("[RoomConfigSync] Local config restored from backup");
            }
            _localBackupPayload = string.Empty;
            _hasLocalBackup = false;
        }

        // ── 序列化（带缓存 + 哈希）────────────────────────────────

        /// <summary>获取缓存的 payload，仅在脏标记时重建。</summary>
        private static string GetOrBuildPayload()
        {
            if (_configDirty || string.IsNullOrEmpty(_cachedPayload))
            {
                _cachedPayload = BuildPayload();
                _cachedPayloadHash = ComputeHash(_cachedPayload);
            }
            return _cachedPayload;
        }

        private static string BuildPayload()
        {
            var sb = new StringBuilder(256);
            // Lantern
            Append(sb, "LanternMaxFuel", Plugin.LanternMaxFuel?.Value.ToString());
            AppendBool(sb, "EnableWarmthReduction", Plugin.EnableWarmthReduction);
            AppendFloat(sb, "LanternWarmthMultiplier", Plugin.LanternWarmthMultiplier);
            // Restore (仅打僵尸)
            AppendBool(sb, "EnableWarmthRestore", Plugin.EnableWarmthRestore);
            AppendInt(sb, "HitRestoreWarmth", Plugin.HitRestoreWarmth);
            AppendInt(sb, "RestoreRadius", Plugin.RestoreRadius);
            AppendFloat(sb, "HitRestoreCooldown", Plugin.HitRestoreCooldown);
            Append(sb, "ReserveWarmthMax", Plugin.ReserveWarmthMax?.Value.ToString());
            // AutoRefill
            AppendBool(sb, "AutoRefillEnabled", Plugin.AutoRefillEnabled);
            AppendFloat(sb, "AutoRefillCapPercent", Plugin.AutoRefillCapPercent);
            AppendFloat(sb, "AutoRefillRate", Plugin.AutoRefillRate);
            AppendBool(sb, "AutoRefillDaytimeOnly", Plugin.AutoRefillDaytimeOnly);
            AppendBool(sb, "AutoRefillRequireHold", Plugin.AutoRefillRequireHold);
            // BugleUltimate
            AppendBool(sb, "BugleUltimateEnabled", Plugin.BugleUltimateEnabled);
            AppendFloat(sb, "BugleUltimateCooldown", Plugin.BugleUltimateCooldown);
            AppendFloat(sb, "BugleUltimateRadius", Plugin.BugleUltimateRadius);
            AppendFloat(sb, "BugleUltimateRestore", Plugin.BugleUltimateRestore);
            // ItemSpawn
            AppendBool(sb, "PurgeExtraLanterns", Plugin.PurgeExtraLanterns);
            AppendBool(sb, "StrayBugleCleanupEnabled", Plugin.StrayBugleCleanupEnabled);
            AppendFloat(sb, "StrayBugleDistance", Plugin.StrayBugleDistance);
            AppendFloat(sb, "StrayBugleGracePeriod", Plugin.StrayBugleGracePeriod);
            // Campfire
            AppendBool(sb, "EnableCampfireRefuel", Plugin.EnableCampfireRefuel);
            // DrainMultiplier
            AppendFloat(sb, "FlashlightDrainMultiplier", Plugin.FlashlightDrainMultiplier);
            AppendFloat(sb, "CompanionDrainMultiplier", Plugin.CompanionDrainMultiplier);
            AppendFloat(sb, "SoloDrainMultiplier", Plugin.SoloDrainMultiplier);
            AppendInt(sb, "ProximityGracePeriod", Plugin.ProximityGracePeriod);
            // Upgrade
            AppendBool(sb, "EnableUpgradeSystem", Plugin.EnableUpgradeSystem);
            AppendString(sb, "UpgradeLevelCostsCsv", Plugin.UpgradeLevelCostsCsv);
            AppendString(sb, "UpgradeCapacityBonusCsv", Plugin.UpgradeCapacityBonusCsv);
            AppendString(sb, "UpgradeEfficiencyBonusCsv", Plugin.UpgradeEfficiencyBonusCsv);
            AppendFloat(sb, "UpgradePassiveTickInterval", Plugin.UpgradePassiveTickInterval);
            AppendInt(sb, "UpgradePassivePointsPerTick", Plugin.UpgradePassivePointsPerTick);
            AppendInt(sb, "UpgradeHitPoints", Plugin.UpgradeHitPoints);
            AppendInt(sb, "UpgradeCampfirePoints", Plugin.UpgradeCampfirePoints);
            AppendInt(sb, "UpgradeBuglePoints", Plugin.UpgradeBuglePoints);
            return sb.ToString();
        }

        /// <summary>计算 payload 短哈希（MD5 前 8 字符）。</summary>
        private static string ComputeHash(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return string.Empty;
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(payload));
                // 前 4 字节 = 8 个 hex 字符，足够避免偶然碰撞
                return BitConverter.ToString(hash, 0, 4).Replace("-", "");
            }
        }

        private static void ApplyPayload(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return;
            var dict = ParsePayload(payload);
            if (dict.Count == 0) return;

            _applyingPayload = true;
            UnsubscribeConfigChanges();
            try
            {
                // Lantern
                ApplyEnum(dict, "LanternMaxFuel", Plugin.LanternMaxFuel);
                ApplyBool(dict, "EnableWarmthReduction", Plugin.EnableWarmthReduction);
                ApplyFloat(dict, "LanternWarmthMultiplier", Plugin.LanternWarmthMultiplier);
                // Restore (仅打僵尸)
                ApplyBool(dict, "EnableWarmthRestore", Plugin.EnableWarmthRestore);
                ApplyInt(dict, "HitRestoreWarmth", Plugin.HitRestoreWarmth);
                ApplyInt(dict, "RestoreRadius", Plugin.RestoreRadius);
                ApplyFloat(dict, "HitRestoreCooldown", Plugin.HitRestoreCooldown);
                ApplyEnum(dict, "ReserveWarmthMax", Plugin.ReserveWarmthMax);
                // AutoRefill
                ApplyBool(dict, "AutoRefillEnabled", Plugin.AutoRefillEnabled);
                ApplyFloat(dict, "AutoRefillCapPercent", Plugin.AutoRefillCapPercent);
                ApplyFloat(dict, "AutoRefillRate", Plugin.AutoRefillRate);
                ApplyBool(dict, "AutoRefillDaytimeOnly", Plugin.AutoRefillDaytimeOnly);
                ApplyBool(dict, "AutoRefillRequireHold", Plugin.AutoRefillRequireHold);
                // BugleUltimate
                ApplyBool(dict, "BugleUltimateEnabled", Plugin.BugleUltimateEnabled);
                ApplyFloat(dict, "BugleUltimateCooldown", Plugin.BugleUltimateCooldown);
                ApplyFloat(dict, "BugleUltimateRadius", Plugin.BugleUltimateRadius);
                ApplyFloat(dict, "BugleUltimateRestore", Plugin.BugleUltimateRestore);
                // ItemSpawn
                ApplyBool(dict, "PurgeExtraLanterns", Plugin.PurgeExtraLanterns);
                ApplyBool(dict, "StrayBugleCleanupEnabled", Plugin.StrayBugleCleanupEnabled);
                ApplyFloat(dict, "StrayBugleDistance", Plugin.StrayBugleDistance);
                ApplyFloat(dict, "StrayBugleGracePeriod", Plugin.StrayBugleGracePeriod);
                // Campfire
                ApplyBool(dict, "EnableCampfireRefuel", Plugin.EnableCampfireRefuel);
                // DrainMultiplier
                ApplyFloat(dict, "FlashlightDrainMultiplier", Plugin.FlashlightDrainMultiplier);
                ApplyFloat(dict, "CompanionDrainMultiplier", Plugin.CompanionDrainMultiplier);
                ApplyFloat(dict, "SoloDrainMultiplier", Plugin.SoloDrainMultiplier);
                ApplyInt(dict, "ProximityGracePeriod", Plugin.ProximityGracePeriod);
                // Upgrade
                ApplyBool(dict, "EnableUpgradeSystem", Plugin.EnableUpgradeSystem);
                ApplyString(dict, "UpgradeLevelCostsCsv", Plugin.UpgradeLevelCostsCsv);
                ApplyString(dict, "UpgradeCapacityBonusCsv", Plugin.UpgradeCapacityBonusCsv);
                ApplyString(dict, "UpgradeEfficiencyBonusCsv", Plugin.UpgradeEfficiencyBonusCsv);
                ApplyFloat(dict, "UpgradePassiveTickInterval", Plugin.UpgradePassiveTickInterval);
                ApplyInt(dict, "UpgradePassivePointsPerTick", Plugin.UpgradePassivePointsPerTick);
                ApplyInt(dict, "UpgradeHitPoints", Plugin.UpgradeHitPoints);
                ApplyInt(dict, "UpgradeCampfirePoints", Plugin.UpgradeCampfirePoints);
                ApplyInt(dict, "UpgradeBuglePoints", Plugin.UpgradeBuglePoints);
            }
            finally
            {
                _applyingPayload = false;
                SubscribeConfigChanges(Plugin.Instance);
            }
        }

        private static Dictionary<string, string> ParsePayload(string payload)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(payload)) return dict;

            foreach (var pair in payload.Split('|'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && eq < pair.Length - 1)
                    dict[pair.Substring(0, eq)] = pair.Substring(eq + 1);
            }
            return dict;
        }

        // ── Append helpers ──────────────────────────────────────

        private static void Append(StringBuilder sb, string key, string value)
        {
            if (sb.Length > 0) sb.Append('|');
            sb.Append(key).Append('=').Append(value ?? string.Empty);
        }

        private static void AppendBool(StringBuilder sb, string key, ConfigEntry<bool> entry)
        {
            Append(sb, key, entry != null && entry.Value ? "1" : "0");
        }

        private static void AppendFloat(StringBuilder sb, string key, ConfigEntry<float> entry)
        {
            Append(sb, key, (entry != null ? entry.Value : 0f).ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendInt(StringBuilder sb, string key, ConfigEntry<int> entry)
        {
            Append(sb, key, (entry != null ? entry.Value : 0).ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder sb, string key, ConfigEntry<string> entry)
        {
            // payload 用 | 和 = 分隔，需要转义
            string v = entry?.Value ?? string.Empty;
            v = v.Replace("|", "\u0001").Replace("=", "\u0002");
            Append(sb, key, v);
        }

        // ── Apply helpers ───────────────────────────────────────

        private static void ApplyBool(Dictionary<string, string> dict, string key, ConfigEntry<bool> entry)
        {
            string val;
            if (entry != null && dict.TryGetValue(key, out val))
            {
                if (val == "1") entry.Value = true;
                else if (val == "0") entry.Value = false;
            }
        }

        private static void ApplyFloat(Dictionary<string, string> dict, string key, ConfigEntry<float> entry)
        {
            string val;
            float f;
            if (entry != null && dict.TryGetValue(key, out val)
                && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
            {
                entry.Value = f;
            }
        }

        private static void ApplyInt(Dictionary<string, string> dict, string key, ConfigEntry<int> entry)
        {
            string val;
            int i;
            if (entry != null && dict.TryGetValue(key, out val)
                && int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out i))
            {
                entry.Value = i;
            }
        }

        private static void ApplyString(Dictionary<string, string> dict, string key, ConfigEntry<string> entry)
        {
            string val;
            if (entry != null && dict.TryGetValue(key, out val))
            {
                // 反转义
                entry.Value = val.Replace("\u0001", "|").Replace("\u0002", "=");
            }
        }

        private static void ApplyEnum<T>(Dictionary<string, string> dict, string key, ConfigEntry<T> entry)
            where T : struct
        {
            string val;
            T parsed;
            if (entry != null && dict.TryGetValue(key, out val) && Enum.TryParse(val, out parsed))
                entry.Value = parsed;
        }

        // ── 配置变更监听 ────────────────────────────────────────

        private static void SubscribeConfigChanges(Plugin plugin)
        {
            if (plugin?.Config != null)
                plugin.Config.SettingChanged += OnSettingChanged;
        }

        private static void UnsubscribeConfigChanges()
        {
            if (Plugin.Instance?.Config != null)
                Plugin.Instance.Config.SettingChanged -= OnSettingChanged;
        }

        private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (_applyingPayload) return;

            // 预设切换 → 批量设置配置值
            if (!PresetManager.IsApplying && Plugin.ActivePreset != null
                && ReferenceEquals(e.ChangedSetting, Plugin.ActivePreset))
            {
                Plugin.Log?.LogInfo($"[RoomConfigSync] Preset change detected: {Plugin.ActivePreset.Value}, applying...");
                PresetManager.ApplyPreset(Plugin.ActivePreset.Value);
            }

            if (HasOnlineRoom() && PhotonNetwork.IsMasterClient)
            {
                MarkDirty(false);
                // 非预设变更：立即广播（1秒节流，避免高频修改导致消息泛滥）
                if (!PresetManager.IsApplying && Time.unscaledTime - _lastImmediatePublishTime >= 1f)
                {
                    _lastImmediatePublishTime = Time.unscaledTime;
                    Plugin.Log?.LogInfo($"[RoomConfigSync] Host setting changed: '{e.ChangedSetting?.Definition?.Key ?? "?"}' → republish");
                    PublishHostConfig(true);
                }
            }
        }

        // ── 工具方法 ────────────────────────────────────────────

        private static bool HasOnlineRoom()
        {
            return PhotonNetwork.InRoom
                && PhotonNetwork.CurrentRoom != null
                && !PhotonNetwork.OfflineMode;
        }

        private static void MarkDirty(bool immediate)
        {
            _configDirty = true;
            if (immediate)
                _lastPublishTime = -10f;
        }

        private static void ResetState(bool full)
        {
            _lastPublishedPayload = string.Empty;
            _lastAppliedPayload = string.Empty;
            _lastPublishTime = -10f;
            _lastPollTime = -10f;
            _configDirty = true;
            _wasMasterClient = false;

            if (full)
                _activeRoomName = string.Empty;
        }
    }
}
