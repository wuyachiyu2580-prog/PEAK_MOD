using System;
using System.Globalization;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 灯笼升级系统。升级点数通过被动累积 + 事件奖励获得：
    /// - 被动：每 PassiveTickInterval 秒 + PassivePointsPerTick
    /// - 打僵尸：+HitPoints / 点篝火：+CampfirePoints / 号角大招：+BuglePoints
    /// 等级数值（点数/容量/效率）全部走 ConfigEntry CSV。
    /// </summary>
    internal static class LanternUpgradeSystem
    {
        // ── 常量 ──────────────────────────────────────────────────
        public const int MaxLevel = 5;

        // Photon player property keys
        internal const string KeyPoints = "LSN.UPts";
        internal const string KeyCapLv = "LSN.UCap";
        internal const string KeyEffLv = "LSN.UEff";

        // Photon 升级事件
        internal const byte UpgradeEventCode = 42;
        private const byte UpgradeCapacity = 0;
        private const byte UpgradeEfficiency = 1;
        private const byte UpgradeAuto = 2;

        // ── 状态 ──────────────────────────────────────────────────
        private static int _capacityLevel;
        private static int _efficiencyLevel;
        private static int _points;
        private static bool _menuOpen;

        // 被动累积计时器
        private static float _passiveTimer;

        // CSV 解析缓存（避免频繁 split）
        private static string _cachedCostCsv;
        private static int[] _cachedCosts;
        private static string _cachedCapCsv;
        private static float[] _cachedCapBonus;
        private static string _cachedEffCsv;
        private static float[] _cachedEffBonus;

        // 日志节流：Master rejected upgrade 同一玩家+同 type+同 pts/10 档位只打一次，避免自动升级轮询刷屏
        private static int _lastRejectActor = -1;
        private static byte _lastRejectType = 255;
        private static int _lastRejectPtsBucket = -1;

        // ── Public Properties ─────────────────────────────────────

        public static int CapacityLevel => _capacityLevel;
        public static int EfficiencyLevel => _efficiencyLevel;
        public static int Points => _points;
        public static bool IsMenuOpen => _menuOpen;

        /// <summary>容量乘数。Lv0=1.0x → Lv5=1+CapBonus[4]。</summary>
        public static float CapacityMultiplier
        {
            get
            {
                if (_capacityLevel <= 0) return 1f;
                float[] arr = GetCapacityBonus();
                int idx = Mathf.Clamp(_capacityLevel - 1, 0, arr.Length - 1);
                return 1f + arr[idx];
            }
        }

        /// <summary>效率乘数（消耗降低）。Lv0=1.0x → Lv5=1-EffBonus[4]。</summary>
        public static float EfficiencyMultiplier
        {
            get
            {
                if (_efficiencyLevel <= 0) return 1f;
                float[] arr = GetEfficiencyBonus();
                int idx = Mathf.Clamp(_efficiencyLevel - 1, 0, arr.Length - 1);
                return Mathf.Max(0.01f, 1f - arr[idx]);
            }
        }

        // ── 升级成本 ──────────────────────────────────────────────

        /// <summary>下一级容量升级所需点数。</summary>
        public static int GetCapacityCost()
        {
            if (_capacityLevel >= MaxLevel) return int.MaxValue;
            int[] costs = GetLevelCosts();
            return _capacityLevel < costs.Length ? costs[_capacityLevel] : int.MaxValue;
        }

        /// <summary>下一级效率升级所需点数。</summary>
        public static int GetEfficiencyCost()
        {
            if (_efficiencyLevel >= MaxLevel) return int.MaxValue;
            int[] costs = GetLevelCosts();
            return _efficiencyLevel < costs.Length ? costs[_efficiencyLevel] : int.MaxValue;
        }

        // ── CSV 解析 ──

        private static int[] GetLevelCosts()
        {
            string csv = Plugin.UpgradeLevelCostsCsv != null ? Plugin.UpgradeLevelCostsCsv.Value : "50,100,150,200,250";
            if (csv != _cachedCostCsv || _cachedCosts == null)
            {
                _cachedCostCsv = csv;
                _cachedCosts = ParseIntCsv(csv, new[] { 50, 100, 150, 200, 250 });
            }
            return _cachedCosts;
        }

        private static float[] GetCapacityBonus()
        {
            string csv = Plugin.UpgradeCapacityBonusCsv != null ? Plugin.UpgradeCapacityBonusCsv.Value : "0.2,0.4,0.6,0.8,1.0";
            if (csv != _cachedCapCsv || _cachedCapBonus == null)
            {
                _cachedCapCsv = csv;
                _cachedCapBonus = ParseFloatCsv(csv, new[] { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f });
            }
            return _cachedCapBonus;
        }

        private static float[] GetEfficiencyBonus()
        {
            string csv = Plugin.UpgradeEfficiencyBonusCsv != null ? Plugin.UpgradeEfficiencyBonusCsv.Value : "0.1,0.2,0.3,0.4,0.5";
            if (csv != _cachedEffCsv || _cachedEffBonus == null)
            {
                _cachedEffCsv = csv;
                _cachedEffBonus = ParseFloatCsv(csv, new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f });
            }
            return _cachedEffBonus;
        }

        private static int[] ParseIntCsv(string csv, int[] fallback)
        {
            if (string.IsNullOrWhiteSpace(csv)) return fallback;
            string[] parts = csv.Split(',');
            int[] result = new int[Mathf.Max(parts.Length, fallback.Length)];
            for (int i = 0; i < result.Length; i++) result[i] = i < fallback.Length ? fallback[i] : 999999;
            for (int i = 0; i < parts.Length && i < result.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                    result[i] = v;
            }
            return result;
        }

        private static float[] ParseFloatCsv(string csv, float[] fallback)
        {
            if (string.IsNullOrWhiteSpace(csv)) return fallback;
            string[] parts = csv.Split(',');
            float[] result = new float[Mathf.Max(parts.Length, fallback.Length)];
            for (int i = 0; i < result.Length; i++) result[i] = i < fallback.Length ? fallback[i] : 0f;
            for (int i = 0; i < parts.Length && i < result.Length; i++)
            {
                if (float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    result[i] = v;
            }
            return result;
        }

        // ── 点数 ──────────────────────────────────────────────────

        /// <summary>入口：Plugin.Update 每帧调用。被动累积点数。</summary>
        public static void Tick()
        {
            if (Plugin.EnableUpgradeSystem == null || !Plugin.EnableUpgradeSystem.Value) return;
        
            float interval = Plugin.UpgradePassiveTickInterval != null ? Plugin.UpgradePassiveTickInterval.Value : 60f;
            if (interval <= 0f) return;
        
            _passiveTimer += Time.deltaTime;
            if (_passiveTimer < interval) return;
            _passiveTimer = 0f;
        
            int gain = Plugin.UpgradePassivePointsPerTick != null ? Plugin.UpgradePassivePointsPerTick.Value : 1;
            if (gain > 0) AddPoints(gain, "Passive");
        }
        
        /// <summary>事件奖励入口（Hit / Campfire / Bugle）。</summary>
        public static void AddEvent(string source)
        {
            if (Plugin.EnableUpgradeSystem == null || !Plugin.EnableUpgradeSystem.Value) return;
            int gain = 0;
            switch (source)
            {
                case "Hit": gain = Plugin.UpgradeHitPoints != null ? Plugin.UpgradeHitPoints.Value : 1; break;
                case "Campfire": gain = Plugin.UpgradeCampfirePoints != null ? Plugin.UpgradeCampfirePoints.Value : 5; break;
                case "Bugle": gain = Plugin.UpgradeBuglePoints != null ? Plugin.UpgradeBuglePoints.Value : 3; break;
            }
            if (gain > 0) AddPoints(gain, source);
        }
        
        /// <summary>增加升级点数。自动升级：容量、效率交替（容量优先）。</summary>
        public static void AddPoints(int amount, string source = null)
        {
            if (amount <= 0 || Plugin.EnableUpgradeSystem == null || !Plugin.EnableUpgradeSystem.Value) return;
            int before = _points;
            _points += amount;
            string srcStr = source ?? "n/a";
            Plugin.Log?.LogInfo($"[DEBUG] [Upgrade] AddPoints +{amount} (src={srcStr}) → {_points} (was {before})");
            SyncToNetwork();

            // 自动升级：容量与效率交替（等级低的先升，相同时容量优先）
            if (IsHostAuthoritative())
            {
                // 客机：发送自动升级请求给房主
                SendUpgradeRequest(UpgradeAuto);
            }
            else
            {
                // 单机或房主：直接执行
                bool upgraded = true;
                while (upgraded)
                {
                    upgraded = false;
                    if (_capacityLevel <= _efficiencyLevel
                        && _capacityLevel < MaxLevel && _points >= GetCapacityCost())
                        upgraded = TryUpgradeCapacity();
                    else if (_efficiencyLevel < MaxLevel && _points >= GetEfficiencyCost())
                        upgraded = TryUpgradeEfficiency();
                }
            }
        }

        // ── 升级操作 ──────────────────────────────────────────────

        /// <summary>尝试升级容量。成功返回 true。</summary>
        public static bool TryUpgradeCapacity()
        {
            if (_capacityLevel >= MaxLevel)
            {
                Plugin.Log?.LogInfo("[DEBUG] [Upgrade] Capacity upgrade BLOCKED: already MAX level");
                return false;
            }
            int cost = GetCapacityCost();
            if (_points < cost)
            {
                Plugin.Log?.LogInfo($"[DEBUG] [Upgrade] Capacity upgrade BLOCKED: points={_points} < cost={cost}");
                return false;
            }

            _points -= cost;
            _capacityLevel++;
            ApplyEfficiencyDrainSource();
            ApplyCapacityToExistingLanterns();
            SyncToNetwork();
            Plugin.Log?.LogInfo($"[Upgrade] Capacity → Lv{_capacityLevel} (×{CapacityMultiplier:F1})");
            return true;
        }

        /// <summary>尝试升级效率。成功返回 true。</summary>
        public static bool TryUpgradeEfficiency()
        {
            if (_efficiencyLevel >= MaxLevel)
            {
                Plugin.Log?.LogInfo("[DEBUG] [Upgrade] Efficiency upgrade BLOCKED: already MAX level");
                return false;
            }
            int cost = GetEfficiencyCost();
            if (_points < cost)
            {
                Plugin.Log?.LogInfo($"[DEBUG] [Upgrade] Efficiency upgrade BLOCKED: points={_points} < cost={cost}");
                return false;
            }

            _points -= cost;
            _efficiencyLevel++;
            ApplyEfficiencyDrainSource();
            SyncToNetwork();
            Plugin.Log?.LogInfo($"[Upgrade] Efficiency → Lv{_efficiencyLevel} (×{EfficiencyMultiplier:F2})");
            return true;
        }

        // ── 菜单控制 ─────────────────────────────────────────────

        public static void ToggleMenu() { _menuOpen = !_menuOpen; }

        // ── 房主权威模式 ─────────────────────────────────────────

        /// <summary>是否处于房主权威模式（在线房间 + 非房主）。</summary>
        public static bool IsHostAuthoritative()
        {
            return PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode && !PhotonNetwork.IsMasterClient;
        }

        /// <summary>向房主发送升级请求事件。</summary>
        public static void RequestUpgrade(byte upgradeType)
        {
            if (!IsHostAuthoritative())
            {
                // 单机或房主：直接执行
                if (upgradeType == UpgradeCapacity) TryUpgradeCapacity();
                else if (upgradeType == UpgradeEfficiency) TryUpgradeEfficiency();
                return;
            }
            SendUpgradeRequest(upgradeType);
        }

        private static void SendUpgradeRequest(byte upgradeType)
        {
            PhotonNetwork.RaiseEvent(UpgradeEventCode, new byte[] { upgradeType },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
                SendOptions.SendReliable);
            Plugin.Log?.LogInfo($"[DEBUG] [Upgrade] Sent upgrade request type={upgradeType} to master");
        }

        /// <summary>房主处理客机的升级请求事件。</summary>
        internal static void HandleUpgradeEvent(EventData photonEvent)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (photonEvent.Code != UpgradeEventCode) return;

            int senderActor = photonEvent.Sender;
            byte[] data = photonEvent.CustomData as byte[];
            if (data == null || data.Length == 0) return;
            byte upgradeType = data[0];

            Photon.Realtime.Player player = null;
            if (PhotonNetwork.CurrentRoom?.Players != null)
                PhotonNetwork.CurrentRoom.Players.TryGetValue(senderActor, out player);
            if (player == null) return;

            var props = player.CustomProperties ?? new Hashtable();
            int pts = (props.ContainsKey(KeyPoints) && props[KeyPoints] is int) ? (int)props[KeyPoints] : 0;
            int capLv = (props.ContainsKey(KeyCapLv) && props[KeyCapLv] is int) ? (int)props[KeyCapLv] : 0;
            int effLv = (props.ContainsKey(KeyEffLv) && props[KeyEffLv] is int) ? (int)props[KeyEffLv] : 0;
            capLv = Mathf.Clamp(capLv, 0, MaxLevel);
            effLv = Mathf.Clamp(effLv, 0, MaxLevel);

            bool changed = false;

            switch (upgradeType)
            {
                case UpgradeAuto:
                    bool up = true;
                    while (up)
                    {
                        up = false;
                        int[] costs = GetLevelCosts();
                        int capCost = capLv < costs.Length ? costs[capLv] : int.MaxValue;
                        int effCost = effLv < costs.Length ? costs[effLv] : int.MaxValue;
                        if (capLv <= effLv && capLv < MaxLevel && pts >= capCost)
                        { pts -= capCost; capLv++; up = true; changed = true; }
                        else if (effLv < MaxLevel && pts >= effCost)
                        { pts -= effCost; effLv++; up = true; changed = true; }
                    }
                    break;
                case UpgradeCapacity:
                    {
                        int[] costs = GetLevelCosts();
                        int capCost = capLv < costs.Length ? costs[capLv] : int.MaxValue;
                        if (capLv < MaxLevel && pts >= capCost)
                        { pts -= capCost; capLv++; changed = true; }
                    }
                    break;
                case UpgradeEfficiency:
                    {
                        int[] costs = GetLevelCosts();
                        int effCost = effLv < costs.Length ? costs[effLv] : int.MaxValue;
                        if (effLv < MaxLevel && pts >= effCost)
                        { pts -= effCost; effLv++; changed = true; }
                    }
                    break;
            }

            if (changed)
            {
                player.SetCustomProperties(new Hashtable
                {
                    { KeyPoints, pts },
                    { KeyCapLv, capLv },
                    { KeyEffLv, effLv }
                });
                // 成功升级 → 重置 reject 节流状态，下次不够分再 reject 能正常打一条
                _lastRejectActor = -1;
                _lastRejectType = 255;
                _lastRejectPtsBucket = -1;
                Plugin.Log?.LogInfo($"[Upgrade] Master approved upgrade for #{senderActor}: pts={pts}, cap=Lv{capLv}, eff=Lv{effLv}");
            }
            else
            {
                // 同一玩家+同 type+同 pts/10 档位只打一次；成功升级后字段会被 reset（看 changed 分支）
                int bucket = pts / 10;
                if (_lastRejectActor != senderActor || _lastRejectType != upgradeType || _lastRejectPtsBucket != bucket)
                {
                    _lastRejectActor = senderActor;
                    _lastRejectType = upgradeType;
                    _lastRejectPtsBucket = bucket;
                    int gapToNextBucket = (bucket + 1) * 10 - pts;
                    Plugin.Log?.LogInfo($"[Upgrade] Master rejected upgrade for #{senderActor} type={upgradeType}: insufficient pts={pts} (silenced until +{gapToNextBucket} more pts or upgrade succeeds)");
                }
            }
        }

        /// <summary>非房主客机定期从 CustomProperties 同步升级状态（房主批准后更新）。</summary>
        public static void SyncFromNetworkIfNeeded()
        {
            if (!IsHostAuthoritative()) return;
            if (PhotonNetwork.LocalPlayer?.CustomProperties == null) return;
            var props = PhotonNetwork.LocalPlayer.CustomProperties;
            object val;
            int oldPoints = _points, oldCap = _capacityLevel, oldEff = _efficiencyLevel;
            if (props.TryGetValue(KeyPoints, out val) && val is int) _points = (int)val;
            if (props.TryGetValue(KeyCapLv, out val) && val is int)
            {
                int newLv = Mathf.Clamp((int)val, 0, MaxLevel);
                if (newLv != _capacityLevel)
                {
                    _capacityLevel = newLv;
                    ApplyEfficiencyDrainSource();
                    ApplyCapacityToExistingLanterns();
                }
            }
            if (props.TryGetValue(KeyEffLv, out val) && val is int)
            {
                int newLv = Mathf.Clamp((int)val, 0, MaxLevel);
                if (newLv != _efficiencyLevel) { _efficiencyLevel = newLv; ApplyEfficiencyDrainSource(); }
            }
            // 联机客机：房主批准升级后 CustomProperties 过来触发本地升级。
            // 日志分级：
            //   - cap/eff 真变化 → Info（升级成功，高价值）
            //   - 仅 pts 变化且差值 ≥ 3 → Info（如号角大招 +3 这种批量加分）
            //   - 仅 pts 变化且差值 ≤ 2 → DEBUG（乐观更新 ±1 的 ping-pong 抖动，噪音）
            bool levelChanged = (oldCap != _capacityLevel) || (oldEff != _efficiencyLevel);
            int ptsDiff = Mathf.Abs(_points - oldPoints);
            if (levelChanged || ptsDiff >= 3)
            {
                Plugin.Log?.LogInfo($"[Upgrade] Client synced from master: pts={oldPoints}→{_points}, cap=Lv{oldCap}→Lv{_capacityLevel}, eff=Lv{oldEff}→Lv{_efficiencyLevel}");
            }
            else if (ptsDiff > 0)
            {
                Plugin.Log?.LogInfo($"[DEBUG] [Upgrade] Client synced (minor pts jitter): {oldPoints}→{_points}");
            }
        }

        // ── 效率加成同步到消耗系统 ───────────────────────────────

        /// <summary>将当前效率等级应用为燃料消耗倍率源。</summary>
        public static void ApplyEfficiencyDrainSource()
        {
            if (_efficiencyLevel > 0)
                LanternHelper.SetDrainSource("upgrade_efficiency", EfficiencyMultiplier);
            else
                LanternHelper.RemoveDrainSource("upgrade_efficiency");
        }

        /// <summary>
        /// 将当前容量等级应用到场上所有本机拥有的灯笼（刷新 startingFuel）。
        /// 升级刚发生时调用，确保已存在的灯立即生效新容量，而非等重新 Awake。
        /// </summary>
        public static void ApplyCapacityToExistingLanterns()
        {
            try
            {
                int val = Plugin.LanternMaxFuel != null ? (int)Plugin.LanternMaxFuel.Value : 0;
                if (val == 0) return; // GameDefault 模式不覆盖
                float baseFuel = val > 0 ? (float)val : 99999f;
                float newMax = baseFuel * CapacityMultiplier;

                var lanterns = UnityEngine.Object.FindObjectsByType<Lantern>(FindObjectsSortMode.None);
                if (lanterns == null || lanterns.Length == 0) return;

                int refreshed = 0;
                foreach (var lt in lanterns)
                {
                    if (lt == null) continue;
                    if (LanternHelper.IsSpecialLantern(lt)) continue;
                    // 非本机拥有的灯由对方自己刷新，这里只处理自己的
                    if (lt.photonView != null && !lt.photonView.IsMine) continue;
                    float oldMax = lt.startingFuel;
                    if (Mathf.Approximately(oldMax, newMax)) continue;
                    lt.startingFuel = newMax;
                    refreshed++;
                    Plugin.Log?.LogInfo($"[DEBUG] [Upgrade] Refresh lantern startingFuel: {oldMax:F1} → {newMax:F1} (capLv={_capacityLevel})");
                }
                if (refreshed > 0)
                    Plugin.Log?.LogInfo($"[Upgrade] Capacity applied to {refreshed} lantern(s), newMax={newMax:F1}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogWarning($"[DEBUG] [Upgrade] ApplyCapacityToExistingLanterns failed: {ex.Message}");
            }
        }

        // ── 网络同步 ─────────────────────────────────────────────

        /// <summary>将升级数据写入 Photon 玩家属性。</summary>
        private static void SyncToNetwork()
        {
            try
            {
                if (!PhotonNetwork.IsConnected || PhotonNetwork.LocalPlayer == null) return;
                var props = new Hashtable
                {
                    { KeyPoints, _points },
                    { KeyCapLv, _capacityLevel },
                    { KeyEffLv, _efficiencyLevel }
                };
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogWarning($"[DEBUG] [Upgrade] SyncToNetwork failed: {ex.Message}");
            }
        }

        /// <summary>从 Photon 玩家属性加载升级数据（加入房间时调用）。</summary>
        public static void LoadFromNetwork()
        {
            try
            {
                if (!PhotonNetwork.IsConnected || PhotonNetwork.LocalPlayer == null) return;
                var props = PhotonNetwork.LocalPlayer.CustomProperties;
                if (props == null) return;

                object val;
                if (props.TryGetValue(KeyPoints, out val) && val is int) _points = (int)val;
                if (props.TryGetValue(KeyCapLv, out val) && val is int) _capacityLevel = Mathf.Clamp((int)val, 0, MaxLevel);
                if (props.TryGetValue(KeyEffLv, out val) && val is int) _efficiencyLevel = Mathf.Clamp((int)val, 0, MaxLevel);

                ApplyEfficiencyDrainSource();
                if (_capacityLevel > 0) ApplyCapacityToExistingLanterns();
                Plugin.Log?.LogInfo($"[Upgrade] Loaded from network: pts={_points}, cap=Lv{_capacityLevel}, eff=Lv{_efficiencyLevel}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogWarning($"[DEBUG] [Upgrade] LoadFromNetwork failed: {ex.Message}");
            }
        }

        /// <summary>重置所有升级状态（新游戏时调用）。</summary>
        public static void Reset()
        {
            _capacityLevel = 0;
            _efficiencyLevel = 0;
            _points = 0;
            _menuOpen = false;
            LanternHelper.RemoveDrainSource("upgrade_efficiency");
            Plugin.Log?.LogInfo("[DEBUG] [Upgrade] Reset: all upgrade state cleared");
        }
    }
}
