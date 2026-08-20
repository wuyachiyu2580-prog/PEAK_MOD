using System;
using System.Collections.Generic;
using PlayersInfo.Helpers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayersInfo.MonoBehaviours
{
    /// <summary>
    /// 队友体力条协调器：仿 PeakStats 做法克隆 GUIManager.bar 到同一个 VLG 容器。
    /// 与 PeakStats 不同：
    ///   1) 采用 Pool 复用，数量上限受 CfgMaxNearbyCount 约束
    ///   2) 按 Vector3.Distance 每 0.25s 取最近 N 个绑定，其余隐藏
    ///   3) 每条 bar 挂 TeammateBarDriver 接管 Update，正确显示异常、临时体力、数值
    /// </summary>
    internal class TeammateBarsCoordinator : MonoBehaviour
    {
        public static TeammateBarsCoordinator Instance { get; private set; }

        private readonly List<TeammateBarDriver> _pool = new List<TeammateBarDriver>();
        private readonly Dictionary<int, TeammateBarDriver> _driversByStableId = new Dictionary<int, TeammateBarDriver>();
        private readonly Dictionary<TeammateBarDriver, int> _stableIdByDriver = new Dictionary<TeammateBarDriver, int>();
        private readonly List<int> _driverCleanupScratch = new List<int>();
        private StaminaBar _origBar;
        private bool _layoutInitialized;

        private float _nextRefreshTime;
        private const float RefreshInterval = 0.25f;
        private float _nextDistLogTime;
        private float _nextDiagnosticLogTime;
        private const float ReorderDelay = 0.75f;

        // 复用的临时列表
        private static readonly List<KeyValuePair<float, Character>> _scratch = new List<KeyValuePair<float, Character>>();
        private static readonly List<Character> s_visibleScratch = new List<Character>();
        private static readonly Dictionary<int, Character> s_visibleById = new Dictionary<int, Character>();
        private static readonly HashSet<int> s_seenStableIds = new HashSet<int>();
        // 距离滞回：已在 _displayOrder 里的玩家走出 range+HysteresisMargin 才被剔除，
        // 避免距离恶在 NearbyRange 边缘抹动造成体力条反复进出、Bind/Hide 闪烁。
        private const float HysteresisMargin = 5f;
        private static readonly HashSet<int> s_displayOrderSet = new HashSet<int>();
        private static readonly HashSet<int> s_currentStableIds = new HashSet<int>();

        // 网络波动加固：玩家因 Photon 重连 / 跨段传送 / Owner 短暂为 null 而临时从 mates 列表丢失时，
        // 维持已显示状态 RetainOnLossDelay 秒，避免体力条莫名其妙跳/闪一下。
        private const float RetainOnLossDelay = 1.5f;
        private struct RetainedEntry { public Character ch; public float lostTime; }
        private static readonly Dictionary<int, RetainedEntry> s_retainedById = new Dictionary<int, RetainedEntry>();
        // stableId 防漂缓存：viewID → actorNumber，让 Owner 短暂为 null 时仍能识别为同一玩家，
        // 不会 fallback 到 ViewID 造成 stableId 漂移 → 误判为新玩家 → 体力条整条切换。
        private static readonly Dictionary<int, int> s_viewIdToActor = new Dictionary<int, int>();

        private readonly List<int> _displayOrder = new List<int>();
        private readonly List<int> _pendingOrder = new List<int>();
        private readonly List<int> _stableOrderScratch = new List<int>();
        private float _pendingOrderSince = -1f;

        // 缓存排序 Comparison，避免每次 RefreshNearby 都新建委托产生 GC
        private static readonly Comparison<KeyValuePair<float, Character>> s_distCmp =
            (a, b) => a.Key.CompareTo(b.Key);

        public static TeammateBarsCoordinator EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PlayersInfo.BarsCoordinator");
            Instance = go.AddComponent<TeammateBarsCoordinator>();
            return Instance;
        }

        private void OnDestroy()
        {
            if (TeamRosterTracker.Instance != null)
                TeamRosterTracker.Instance.OnRosterChanged -= HandleRosterChanged;
            if (Instance == this) Instance = null;
        }

        public void Init()
        {
            try
            {
                if (GUIManager.instance == null || GUIManager.instance.bar == null)
                {
                    PluginLogger.ThrottleWarn("bars_init", "GUIManager.instance.bar not ready, skip init.");
                    return;
                }
                if (!object.ReferenceEquals(_origBar, GUIManager.instance.bar))
                {
                    _origBar = GUIManager.instance.bar;
                    _layoutInitialized = false;
                }
                FixBarGroupOnce();
            }
            catch (Exception ex)
            {
                PluginLogger.Error("BarsCoordinator.Init failed: " + ex.Message);
            }
        }

        /// <summary>参考 PeakStats 的 FixBarGroup：扩大 bar 的父容器，并应用 PlayersInfo 的位置配置。</summary>
        private void FixBarGroupOnce()
        {
            try
            {
                if (_layoutInitialized) return;
                var parentRt = _origBar.transform.parent as RectTransform;
                if (parentRt == null) return;
                parentRt.sizeDelta = new Vector2(600f, 600f);
                ApplyConfiguredAnchor(parentRt);
                var vlg = parentRt.GetComponent<VerticalLayoutGroup>();
                if (vlg != null) vlg.spacing = 25f;
                _layoutInitialized = true;
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("fix_bargroup", "FixBarGroup failed: " + ex.Message);
            }
        }

        /// <summary>
        /// PEAK 的本地 extraBar 原本是 BarGroup 的兄弟节点。BarGroup 被 PlayersInfo
        /// 改成可容纳队友条后，VerticalLayoutGroup 会把它排成额外的一行。将它挂到
        /// 本地 fullBar 下方，避免它被排到右侧或布局底部。队友条不使用这段布局。
        /// </summary>
        private void ConfigureLocalExtraBar()
        {
            try
            {
                if (_origBar == null || _origBar.extraBar == null || _origBar.fullBar == null)
                    return;

                var extra = _origBar.extraBar;
                PluginLogger.Info("[LocalExtraLayout] extraParent=" + GetParentName(extra)
                    + " outlineParent=" + GetParentName(_origBar.extraBarOutline)
                    + " staminaParent=" + GetParentName(_origBar.extraBarStamina)
                    + " iconParent=" + (_origBar.extraStaminaIcon != null
                        ? GetParentName(_origBar.extraStaminaIcon.rectTransform)
                        : "null"));
                if (extra.parent != _origBar.fullBar)
                    extra.SetParent(_origBar.fullBar, false);

                // 额外条整体从主条左下角开始，外层高度负责把内部视觉内容放到主条下方。
                // 不再沿用原版右侧布局的内部锚点，否则 outline 会留在主条这一行。
                extra.anchorMin = new Vector2(0f, 0f);
                extra.anchorMax = new Vector2(0f, 0f);
                extra.pivot = new Vector2(0f, 1f);
                extra.anchoredPosition = new Vector2(0f, -2f);

                ConfigureLocalExtraChild(_origBar.extraBarOutline, extra);
                ConfigureLocalExtraChild(_origBar.extraBarStamina, extra);
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("local_extra_layout", "ConfigureLocalExtraBar failed: " + ex.Message);
            }
        }

        private static void ConfigureLocalExtraChild(RectTransform child, RectTransform parent)
        {
            if (child == null || parent == null) return;

            if (child.parent != parent)
                child.SetParent(parent, false);

            child.anchorMin = new Vector2(0f, 0f);
            child.anchorMax = new Vector2(0f, 1f);
            child.pivot = new Vector2(0f, 0.5f);
            child.anchoredPosition = Vector2.zero;
        }

        private static string GetParentName(RectTransform child)
        {
            if (child == null) return "null";
            return child.parent != null ? child.parent.name : "<root>";
        }

        private static void ApplyConfiguredAnchor(RectTransform rt)
        {
            var anchor = PlayersInfoPlugin.CfgAnchor != null
                ? PlayersInfoPlugin.CfgAnchor.Value
                : PlayersInfoPlugin.HudAnchor.BottomLeft;
            float offsetX = PlayersInfoPlugin.CfgOffsetX != null ? PlayersInfoPlugin.CfgOffsetX.Value : 0f;
            float offsetY = PlayersInfoPlugin.CfgOffsetY != null ? PlayersInfoPlugin.CfgOffsetY.Value : 0f;
            const float Margin = 20f;

            switch (anchor)
            {
                case PlayersInfoPlugin.HudAnchor.TopRight:
                    rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-Margin + offsetX, -Margin + offsetY);
                    break;
                case PlayersInfoPlugin.HudAnchor.BottomLeft:
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                    rt.anchoredPosition = new Vector2(Margin + offsetX, Margin + offsetY);
                    break;
                case PlayersInfoPlugin.HudAnchor.BottomRight:
                    rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                    rt.anchoredPosition = new Vector2(-Margin + offsetX, Margin + offsetY);
                    break;
                default:
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(Margin + offsetX, -Margin + offsetY);
                    break;
            }
        }

        public void AttachToTracker(TeamRosterTracker tracker)
        {
            if (tracker == null) return;
            tracker.OnRosterChanged -= HandleRosterChanged;
            tracker.OnRosterChanged += HandleRosterChanged;
            _nextRefreshTime = 0f; // 立即刷
        }

        private void HandleRosterChanged()
        {
            _nextRefreshTime = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime) return;
            _nextRefreshTime = Time.unscaledTime + RefreshInterval;
            try { RefreshNearby(); }
            catch (Exception ex) { PluginLogger.ThrottleError("bars_refresh", "RefreshNearby failed: " + ex.Message); }
        }

        private void RefreshNearby()
        {
            if (_origBar == null) { Init(); if (_origBar == null) return; }
            if (PlayersInfoPlugin.CfgModEnabled == null || !PlayersInfoPlugin.CfgModEnabled.Value
                || (PlayersInfoPlugin.CfgEnableStaminaBar != null && !PlayersInfoPlugin.CfgEnableStaminaBar.Value))
            {
                HideAll();
                return;
            }
            FixBarGroupOnce();

            var tracker = TeamRosterTracker.Instance;
            if (tracker == null) return;

            var local = Character.localCharacter;
            var displayCharacter = DisplayCharacterHelper.GetObservedOrLocal();
            if (local == null || local.Equals(null) || displayCharacter == null || displayCharacter.Equals(null))
            {
                HideAll();
                return;
            }

            bool spectating = displayCharacter != local;
            bool useLocalCenter = PlayersInfoPlugin.CfgSpectatorNearbyCenter != null
                && PlayersInfoPlugin.CfgSpectatorNearbyCenter.Value
                    == PlayersInfoPlugin.SpectatorNearbyCenterMode.LocalCharacter;
            var focus = spectating && useLocalCenter ? local : displayCharacter;

            int maxN = PlayersInfoPlugin.CfgMaxNearbyCount != null ? PlayersInfoPlugin.CfgMaxNearbyCount.Value : 3;
            float range = PlayersInfoPlugin.CfgNearbyRange != null ? PlayersInfoPlugin.CfgNearbyRange.Value : 30f;
            if (maxN <= 0) { HideAll(); return; }

            // 按距离升序收集：用 Character.Center 而不是 transform.position
            // （PEAK 的 Character.transform 是逻辑根，一直在原点）
            _scratch.Clear();
            s_visibleScratch.Clear();
            s_visibleById.Clear();
            s_seenStableIds.Clear();
            // 预算当前已显示集合，供距离滞回使用（O(1) 查询）
            s_displayOrderSet.Clear();
            for (int doi = 0; doi < _displayOrder.Count; doi++) s_displayOrderSet.Add(_displayOrder[doi]);
            var mates = tracker.Teammates;
            Vector3 focusPosition = focus.Center;
            s_currentStableIds.Clear();
            for (int i = 0; i < mates.Count; i++)
            {
                var c = mates[i];
                if (c == null || c.Equals(null)) continue;
                if (c.data == null) continue;
                if (c.photonView == null) continue;
                int currentStableId = GetStableCharacterId(c);
                if (currentStableId == int.MinValue) continue;
                s_currentStableIds.Add(currentStableId);
                // 默认本地体力条已经显示观战目标，队友列表不重复显示它。
                // 开启本地中心后，观战目标属于本地附近玩家，可正常列入附近列表。
                if (c == displayCharacter && !useLocalCenter) continue;
                // Owner 短暂为 null 不再剔除（网络抖动期间）：依靠 GetStableCharacterId 的 viewId→actor 缓存
                // 把同一玩家识别成同一 stableId，避免被当作新玩家造成体力条切换。
                float d = Vector3.Distance(focusPosition, c.Center);
                if (range > 0f)
                {
                    // 已显示玩家用宽松阈值 range+HysteresisMargin，避免边缘抹动 → 体力条反复闪烁
                    float effectiveRange = s_displayOrderSet.Contains(currentStableId)
                        ? range + HysteresisMargin
                        : range;
                    if (d > effectiveRange) continue;
                }
                _scratch.Add(new KeyValuePair<float, Character>(d, c));
            }
            _scratch.Sort(s_distCmp);
            for (int i = 0; i < _scratch.Count && s_visibleScratch.Count < maxN; i++)
            {
                var c = _scratch[i].Value;
                int stableId = GetStableCharacterId(c);
                if (stableId == int.MinValue) continue;
                if (!s_seenStableIds.Add(stableId)) continue;
                s_visibleScratch.Add(c);
                s_visibleById[stableId] = c;
                s_retainedById[stableId] = new RetainedEntry { ch = c, lostTime = -1f };
            }
            // 网络波动保留：本帧未出现但仍在 _displayOrder 里的玩家，若失踪时间未超 RetainOnLossDelay，
            // 用上次的 Character 引用补回 s_visibleScratch / s_visibleById。这样 ResolveDisplayOrder
            // 看到的成员集合不变，HasSameMembers 仍为 true，不会触发 ApplyDisplayOrder 立即重建。
            float retainNow = Time.unscaledTime;
            for (int doi = 0; doi < _displayOrder.Count; doi++)
            {
                int sid = _displayOrder[doi];
                if (s_visibleById.TryGetValue(sid, out var visibleCh))
                {
                    s_retainedById[sid] = new RetainedEntry { ch = visibleCh, lostTime = -1f };
                    continue;
                }
                if (s_visibleScratch.Count >= maxN)
                {
                    s_retainedById.Remove(sid);
                    continue;
                }
                Character retainedCh = null;
                if (s_retainedById.TryGetValue(sid, out var entry))
                {
                    if (entry.lostTime < 0f)
                    {
                        entry.lostTime = retainNow;
                        s_retainedById[sid] = entry;
                    }
                    // 已有 entry：只判未超时则保留，超时则 Remove 后不再重新 cache
                    // （避免玩家真离开后 mates 里还能反查到 → 反复重建 entry → lostTime 被刷新 → 体力条永远消不掉）
                    if (retainNow - entry.lostTime <= RetainOnLossDelay && entry.ch != null && !entry.ch.Equals(null))
                        retainedCh = entry.ch;
                    else
                        s_retainedById.Remove(sid);
                }
                else
                {
                    // 首次失踪：从 mates 列表里反查 Character 引用做快照（mates 此时可能已无该玩家，则放弃保留）
                    for (int mi = 0; mi < mates.Count; mi++)
                    {
                        var mc = mates[mi];
                        if (mc == null || mc.Equals(null) || mc.photonView == null) continue;
                        if (GetStableCharacterId(mc) == sid)
                        {
                            retainedCh = mc;
                            s_retainedById[sid] = new RetainedEntry { ch = mc, lostTime = retainNow };
                            break;
                        }
                    }
                }
                if (retainedCh != null)
                {
                    s_visibleScratch.Add(retainedCh);
                    s_visibleById[sid] = retainedCh;
                }
            }

            ResolveDisplayOrder(s_visibleScratch);
            s_displayOrderSet.Clear();
            for (int i = 0; i < _displayOrder.Count; i++) s_displayOrderSet.Add(_displayOrder[i]);
            EnsureDriversForDisplayOrder(s_visibleById);
            ApplyDriverSiblingOrder();

            // 距离调试日志：节流输出自己 → 各队友 的距离
            if (PluginLogger.DebugEnabled && Time.unscaledTime >= _nextDistLogTime)
            {
                _nextDistLogTime = Time.unscaledTime + 2f;
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[Dist] local->mates(").Append(_scratch.Count).Append(", range=").Append(range.ToString("F0")).Append("): ");
                    for (int i = 0; i < _scratch.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        var kv = _scratch[i];
                        string nm = kv.Value != null ? (kv.Value.characterName ?? kv.Value.name) : "?";
                        sb.Append(nm).Append("=").Append(kv.Key.ToString("F1")).Append("m");
                    }
                    PluginLogger.Debug(sb.ToString());
                }
                catch { }
            }

            CleanupUntrackedDrivers();

            // 按 stableId 显示和绑定，Driver 不在不同玩家之间交换。
            for (int i = 0; i < _pool.Count; i++)
            {
                var drv = _pool[i];
                if (drv == null) continue;
                int stableId = GetDriverStableId(drv);
                if (stableId != int.MinValue && s_displayOrderSet.Contains(stableId)
                    && s_visibleById.TryGetValue(stableId, out var c) && c != null)
                {
                    if (!drv.gameObject.activeSelf) drv.gameObject.SetActive(true);
                    if (drv.Target != c) drv.BindTarget(c);
                    if (drv.InventoryRow != null) drv.InventoryRow.Target = c;
                }
                else
                {
                    if (drv.gameObject.activeSelf) drv.gameObject.SetActive(false);
                }
            }

            if (Time.unscaledTime >= _nextDiagnosticLogTime)
            {
                _nextDiagnosticLogTime = Time.unscaledTime + 2f;
                LogCoordinatorDiagnostics(tracker.Teammates.Count, focus, s_visibleScratch.Count);
            }
        }

        private void EnsureDriversForDisplayOrder(Dictionary<int, Character> visibleById)
        {
            for (int i = 0; i < _displayOrder.Count; i++)
            {
                int stableId = _displayOrder[i];
                if (_driversByStableId.ContainsKey(stableId)) continue;
                if (!visibleById.TryGetValue(stableId, out var target) || target == null) continue;

                var driver = CreateBar();
                if (driver == null) continue;
                _driversByStableId[stableId] = driver;
                _stableIdByDriver[driver] = stableId;
                _pool.Add(driver);
                driver.BindTarget(target);
                PluginLogger.Info("[PI-DIAG][DriverCreated] stableId=" + stableId
                    + " driver=" + driver.GetInstanceID()
                    + " target=" + GetCharacterDebugName(target)
                    + " rootSelf=" + driver.gameObject.activeSelf
                    + " rootHierarchy=" + driver.gameObject.activeInHierarchy
                    + " afflictions=" + (driver.afflictions != null ? driver.afflictions.Length : -1)
                    + " texts=" + (driver.afflictionTexts != null ? driver.afflictionTexts.Length : -1));
            }
        }

        private void LogCoordinatorDiagnostics(int rosterCount, Character focus, int visibleCount)
        {
            try
            {
                var sb = new System.Text.StringBuilder(512);
                sb.Append("[PI-DIAG][Coordinator] roster=").Append(rosterCount)
                    .Append(" candidates=").Append(_scratch.Count)
                    .Append(" visible=").Append(visibleCount)
                    .Append(" order=").Append(_displayOrder.Count)
                    .Append(" pool=").Append(_pool.Count)
                    .Append(" focus=").Append(GetCharacterDebugName(focus))
                    .Append(" originalSelf=").Append(_origBar != null && _origBar.gameObject.activeSelf)
                    .Append(" originalHierarchy=").Append(_origBar != null && _origBar.gameObject.activeInHierarchy);

                for (int i = 0; i < _pool.Count; i++)
                {
                    var driver = _pool[i];
                    if (driver == null)
                    {
                        sb.Append(" | driver[").Append(i).Append("]=null");
                        continue;
                    }

                    sb.Append(" | driver[").Append(i).Append("] id=").Append(driver.GetInstanceID())
                        .Append(" stable=").Append(GetDriverStableId(driver))
                        .Append(" sibling=").Append(driver.transform.GetSiblingIndex())
                        .Append(" self=").Append(driver.gameObject.activeSelf)
                        .Append(" hierarchy=").Append(driver.gameObject.activeInHierarchy)
                        .Append(" enabled=").Append(driver.enabled)
                        .Append(" target=").Append(GetCharacterDebugName(driver.Target))
                        .Append(" aff=").Append(driver.afflictions != null ? driver.afflictions.Length : -1)
                        .Append(" txt=").Append(driver.afflictionTexts != null ? driver.afflictionTexts.Length : -1);
                }
                PluginLogger.Info(sb.ToString());
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("pi_diag_coordinator", "[PI-DIAG][Coordinator] failed: " + ex);
            }
        }

        private static string GetCharacterDebugName(Character character)
        {
            if (character == null || character.Equals(null)) return "<null>";
            try
            {
                if (character.photonView != null && character.photonView.Owner != null
                    && !string.IsNullOrEmpty(character.photonView.Owner.NickName))
                    return character.photonView.Owner.NickName;
            }
            catch { }
            try { return character.characterName ?? character.name ?? "<unnamed>"; }
            catch { return "<invalid>"; }
        }

        private void ApplyDriverSiblingOrder()
        {
            int siblingIndex = 0;
            for (int i = 0; i < _displayOrder.Count; i++)
            {
                if (!_driversByStableId.TryGetValue(_displayOrder[i], out var driver) || driver == null)
                    continue;
                if (driver.transform.GetSiblingIndex() != siblingIndex)
                    driver.transform.SetSiblingIndex(siblingIndex);
                siblingIndex++;
            }
        }

        private int GetDriverStableId(TeammateBarDriver driver)
        {
            if (driver == null) return int.MinValue;
            return _stableIdByDriver.TryGetValue(driver, out var stableId) ? stableId : int.MinValue;
        }

        private void CleanupUntrackedDrivers()
        {
            _driverCleanupScratch.Clear();
            foreach (var pair in _driversByStableId)
            {
                int stableId = pair.Key;
                if (s_currentStableIds.Contains(stableId) || s_retainedById.ContainsKey(stableId))
                    continue;
                _driverCleanupScratch.Add(stableId);
            }

            for (int i = 0; i < _driverCleanupScratch.Count; i++)
            {
                int stableId = _driverCleanupScratch[i];
                if (!_driversByStableId.TryGetValue(stableId, out var driver)) continue;
                _driversByStableId.Remove(stableId);
                if (driver != null)
                {
                    _stableIdByDriver.Remove(driver);
                    _pool.Remove(driver);
                    if (driver.gameObject != null && driver.gameObject.activeSelf)
                        driver.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(driver.gameObject);
                }
            }
        }

        private void HideAll()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                var drv = _pool[i];
                if (drv != null && drv.gameObject != null && drv.gameObject.activeSelf)
                    drv.gameObject.SetActive(false);
            }
        }

        /// <summary>创建一条克隆 bar（未绑定目标）。</summary>
        private TeammateBarDriver CreateBar()
        {
            try
            {
                var origTransform = _origBar.transform;
                string afflictionTemplateSource;
                var afflictionTemplates = ResolveAfflictionTemplates(
                    _origBar,
                    out afflictionTemplateSource);
                if (afflictionTemplates.Length == 0)
                {
                    // GUIManager.Start can run before StaminaBar.Start has populated its runtime
                    // array. Do not create a permanently incomplete driver; the regular roster
                    // refresh will retry once the original HUD hierarchy is ready.
                    PluginLogger.ThrottleWarn(
                        "affliction_templates_not_ready",
                        "Original affliction templates are not ready; delaying teammate bar creation.");
                    return null;
                }

                PluginLogger.ThrottleInfo(
                    "affliction_template_source",
                    "Teammate affliction templates: source=" + afflictionTemplateSource
                        + " count=" + afflictionTemplates.Length,
                    30f);
                var cloneTransform = UnityEngine.Object.Instantiate(origTransform, origTransform.parent);
                cloneTransform.name = "TeammateBar_Clone";
                cloneTransform.SetAsFirstSibling();

                var cloneGo = cloneTransform.gameObject;
                // Build while hidden. Destroy() is deferred, so an active clone could otherwise
                // render copied local texts or stale affliction visuals for the remainder of the frame.
                cloneGo.SetActive(false);
                // 清理：原版条上被 LocalStaminaBarPatch 动态添加的 PI_Local* 子节点会被 Instantiate 深拷进来，
                // 和我们下面自己要加的文本重叠 → 全部删除
                CleanupClonedPatchArtifacts(cloneTransform);
                var origCompOnClone = cloneGo.GetComponent<StaminaBar>();
                var driver = cloneGo.AddComponent<TeammateBarDriver>();

                if (origCompOnClone != null)
                {
                    // Disable immediately. Destroy() is deferred until the end of the frame, and an
                    // enabled vanilla component can otherwise reactivate cloned extra-bar visuals.
                    origCompOnClone.enabled = false;
                    driver.backing = origCompOnClone.backing;
                    driver.fullBar = origCompOnClone.fullBar;
                    driver.staminaBar = origCompOnClone.staminaBar;
                    driver.staminaGlow = origCompOnClone.staminaGlow;
                    driver.extraStaminaGlow = origCompOnClone.extraStaminaGlow;
                    driver.maxStaminaBar = origCompOnClone.maxStaminaBar;
                    driver.staminaBarOutline = origCompOnClone.staminaBarOutline;
                    driver.staminaBarOutlineOverflowBar = origCompOnClone.staminaBarOutlineOverflowBar;
                    driver.extraBar = origCompOnClone.extraBar;
                    driver.extraBarStamina = origCompOnClone.extraBarStamina;
                    driver.extraBarOutline = origCompOnClone.extraBarOutline;
                    driver.staminaBarOffset = origCompOnClone.staminaBarOffset;
                    driver.minStaminaBarWidth = origCompOnClone.minStaminaBarWidth;
                    driver.minAfflictionWidth = origCompOnClone.minAfflictionWidth;
                    driver.shield = origCompOnClone.shield;
                    driver.campfire = origCompOnClone.campfire;
                    driver.defaultBackingColor = origCompOnClone.defaultBackingColor;
                    driver.outOfStaminaBackingColor = origCompOnClone.outOfStaminaBackingColor;
                    // StaminaBar.afflictions is populated from the bar group's children at runtime.
                    // References outside origTransform are not remapped when only the bar is cloned,
                    // so converting origCompOnClone.afflictions directly can destroy the local HUD's
                    // original BarAffliction components. Build an owned set for this clone instead.
                    driver.afflictions = BuildClonedAfflictions(
                        afflictionTemplates,
                        cloneTransform,
                        origTransform);

                    // 队友仅显示额外体力数值。直接移除克隆范围内的额外条视觉，
                    // 避免延迟销毁的原版 StaminaBar 或其他子组件再次把它激活。
                    RemoveClonedExtraBarVisuals(origCompOnClone, cloneTransform);

                    // 队友不绘制额外体力条，避免 driver 操作到克隆或原版的 extraBar。
                    driver.extraBar = null;
                    driver.extraBarStamina = null;
                    driver.extraBarOutline = null;
                    driver.extraStaminaGlow = null;
                
                    // 销毁原版组件，防止它读 observedCharacter 与我们冲突
                    UnityEngine.Object.Destroy(origCompOnClone);
                }
                else
                {
                    // 异常回退也走相同的所有权筛选，避免重新引入重复或石化组件。
                    driver.afflictions = BuildClonedAfflictions(
                        afflictionTemplates,
                        cloneTransform,
                        origTransform);
                }

                // 名字标签
                driver.nameLabel = TryAddNameLabel(cloneGo);

                // 主体力数值仍保留；额外体力图形条不克隆，只保留右侧数值。
                bool showValue = PlayersInfoPlugin.CfgShowStaminaValue == null || PlayersInfoPlugin.CfgShowStaminaValue.Value;
                // 主体力数值必须挂在绿色填充层，而不是 FullBar 整体节点。
                // FullBar 只负责外层布局，挂在那里时文本可能被绿色填充层遮住。
                if (showValue && driver.staminaBar != null)
                    driver.staminaValueText = AddValueText(driver.staminaBar.gameObject, "StaminaValue");
                if (showValue && driver.fullBar != null)
                    driver.extraValueText = AddSideText(driver.fullBar, "PI_MateExtraValue", ShouldPlaceExtraTextOnRight(), new Color(0.55f, 1f, 0.35f));

                // 异常状态百分比文本：在每个 BarAffliction 上叠一个 TMP_Text
                if (driver.afflictions != null && driver.afflictions.Length > 0)
                {
                    driver.afflictionTexts = new TMP_Text[driver.afflictions.Length];
                    for (int ai = 0; ai < driver.afflictions.Length; ai++)
                    {
                        var a = driver.afflictions[ai];
                        if (a == null) continue;
                        driver.afflictionTexts[ai] = AddAfflictionText(a.gameObject, "AfflictionPct_" + ai);
                    }
                }

                ValidateClonedBarArtifacts(cloneTransform,
                    driver.afflictions != null ? driver.afflictions.Length : 0);

                // 附加：物品栏（作为克隆体子节点，同一行放在名字右边）
                const float NameWidth = 140f;   // 名字标签固定宽
                const float RowGap = 8f;        // 名字与物品栏间隔
                const float RowY = 34f;         // 同行 Y 偏移（bar 上方）
                if (driver.fullBar != null)
                {
                    var hostRect = cloneTransform as RectTransform;
                    var inventoryMode = PlayersInfoPlugin.CfgInventoryDisplayMode != null
                        ? PlayersInfoPlugin.CfgInventoryDisplayMode.Value
                        : PlayersInfoPlugin.TeammateInventoryDisplayMode.ContentsOnly;
                    if (inventoryMode != PlayersInfoPlugin.TeammateInventoryDisplayMode.Disabled)
                    {
                        float invWidth = Mathf.Max(180f, driver.fullBar.rect.width);
                        bool showJetpackFuel = inventoryMode == PlayersInfoPlugin.TeammateInventoryDisplayMode.ContentsAndJetpackFuel;
                        float rowHeight = showJetpackFuel ? 28f : 22f;
                        driver.InventoryRow = TeammateInventoryRow.Build(hostRect, invWidth, rowHeight, 1.5f, showJetpackFuel);
                        var irt = driver.InventoryRow.GetComponent<RectTransform>();
                        irt.anchorMin = new Vector2(0f, 0.5f);
                        irt.anchorMax = new Vector2(0f, 0.5f);
                        irt.pivot = new Vector2(0f, 0.5f);
                        irt.anchoredPosition = new Vector2(NameWidth + RowGap, RowY);
                    }
                }

                // 名字放在物品栏左边同一行
                if (driver.nameLabel != null)
                {
                    var nrt = driver.nameLabel.rectTransform;
                    nrt.sizeDelta = new Vector2(NameWidth, 22f);
                    nrt.anchoredPosition = new Vector2(0f, RowY);
                }

                return driver;
            }
            catch (Exception ex)
            {
                PluginLogger.Error("CreateBar failed: " + ex.Message);
                return null;
            }
        }

        private static void RemoveClonedExtraBarVisuals(StaminaBar cloneBar, Transform cloneRoot)
        {
            if (cloneBar == null || cloneRoot == null) return;

            var visualRoots = new List<GameObject>(5);
            AddOwnedVisual(visualRoots, cloneBar.extraBar, cloneRoot);
            AddOwnedVisual(visualRoots, cloneBar.extraBarStamina, cloneRoot);
            AddOwnedVisual(visualRoots, cloneBar.extraBarOutline, cloneRoot);
            AddOwnedVisual(visualRoots, cloneBar.extraStaminaIcon, cloneRoot);
            AddOwnedVisual(visualRoots, cloneBar.extraStaminaGlow, cloneRoot);

            // Some PEAK layouts leave the runtime field pointing at the original sibling while a
            // renamed copy still exists inside the cloned tree. Catch both the original field names
            // and clone-owned nodes explicitly named for extra stamina.
            var originalNames = new HashSet<string>(StringComparer.Ordinal);
            AddVisualName(originalNames, cloneBar.extraBar);
            AddExplicitExtraVisualName(originalNames, cloneBar.extraBarStamina);
            AddExplicitExtraVisualName(originalNames, cloneBar.extraBarOutline);
            AddExplicitExtraVisualName(originalNames, cloneBar.extraStaminaIcon);
            AddExplicitExtraVisualName(originalNames, cloneBar.extraStaminaGlow);

            var descendants = cloneRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                var child = descendants[i];
                if (child == null || child == cloneRoot)
                    continue;
                bool hasKnownName = originalNames.Contains(child.name);
                bool hasExplicitExtraName = !string.IsNullOrEmpty(child.name)
                    && child.name.IndexOf("extra", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!hasKnownName && !hasExplicitExtraName) continue;
                AddOwnedVisual(visualRoots, child, cloneRoot);
            }

            for (int i = 0; i < visualRoots.Count; i++)
            {
                var visual = visualRoots[i];
                if (visual == null) continue;
                if (visual.activeSelf) visual.SetActive(false);
                UnityEngine.Object.Destroy(visual);
            }
        }

        private static void AddOwnedVisual(List<GameObject> visuals, Component component, Transform cloneRoot)
        {
            if (component != null) AddOwnedVisual(visuals, component.transform, cloneRoot);
        }

        private static void AddOwnedVisual(List<GameObject> visuals, Transform transform, Transform cloneRoot)
        {
            if (transform == null || cloneRoot == null || !transform.IsChildOf(cloneRoot)) return;
            var gameObject = transform.gameObject;
            for (int i = 0; i < visuals.Count; i++)
            {
                var existing = visuals[i];
                if (existing == null) continue;
                if (gameObject == existing || transform.IsChildOf(existing.transform)) return;
                if (existing.transform.IsChildOf(transform))
                {
                    visuals.RemoveAt(i);
                    i--;
                }
            }
            visuals.Add(gameObject);
        }

        private static void AddVisualName(HashSet<string> names, Component component)
        {
            if (component != null && !string.IsNullOrEmpty(component.name)) names.Add(component.name);
        }

        private static void AddExplicitExtraVisualName(HashSet<string> names, Component component)
        {
            if (component == null || string.IsNullOrEmpty(component.name)) return;
            if (component.name.IndexOf("extra", StringComparison.OrdinalIgnoreCase) >= 0)
                names.Add(component.name);
        }

        private static TeammateBarAffliction[] BuildClonedAfflictions(
            BarAffliction[] templates,
            Transform cloneRoot,
            Transform originalBarRoot)
        {
            if (cloneRoot == null)
                return new TeammateBarAffliction[0];

            if (templates == null || templates.Length == 0)
            {
                CleanupClonedPatchArtifacts(cloneRoot);
                return new TeammateBarAffliction[0];
            }

            var localSources = new List<BarAffliction>(
                cloneRoot.GetComponentsInChildren<BarAffliction>(true));
            int initialLocalSourceCount = localSources.Count;
            var ownedSources = new List<BarAffliction>(templates.Length);

            try
            {
                var templateLog = new System.Text.StringBuilder(768);
                templateLog.Append("[PI-DIAG][AfflictionTemplates] count=").Append(templates.Length)
                    .Append(" cloneLocalBefore=").Append(initialLocalSourceCount);
                for (int i = 0; i < templates.Length; i++)
                {
                    var template = templates[i];
                    templateLog.Append(" | ").Append(i).Append(':');
                    if (template == null)
                    {
                        templateLog.Append("null");
                        continue;
                    }
                    templateLog.Append(template.afflictionType)
                        .Append(" petrify=").Append(template.isPetrify)
                        .Append(" name=").Append(template.name)
                        .Append(" self=").Append(template.gameObject.activeSelf)
                        .Append(" hierarchy=").Append(template.gameObject.activeInHierarchy)
                        .Append(" rtf=").Append(template.rtf != null ? template.rtf.name : "null")
                        .Append(" icon=").Append(template.icon != null ? template.icon.name : "null");
                }
                PluginLogger.Info(templateLog.ToString());
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("[PI-DIAG][AfflictionTemplates] logging failed: " + ex.Message);
            }

            // PEAK uses the petrify BarAffliction as the cap segment of the separate extra-stamina
            // row. Teammates represent that cap through current/cap text only, so keeping this
            // visual would place a cloned segment over the local player's stamina row.
            for (int i = localSources.Count - 1; i >= 0; i--)
            {
                var source = localSources[i];
                if (source == null || !source.isPetrify || !source.transform.IsChildOf(cloneRoot))
                    continue;
                source.enabled = false;
                source.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(source.gameObject);
                localSources.RemoveAt(i);
            }

            for (int i = 0; i < templates.Length; i++)
            {
                var template = templates[i];
                if (template == null || template.isPetrify) continue;

                BarAffliction source = null;
                for (int j = 0; j < localSources.Count; j++)
                {
                    var candidate = localSources[j];
                    if (candidate == null
                        || candidate.afflictionType != template.afflictionType
                        || candidate.isPetrify != template.isPetrify)
                        continue;

                    source = candidate;
                    localSources.RemoveAt(j);
                    break;
                }

                if (source == null)
                    source = CloneAfflictionTemplate(template, cloneRoot, originalBarRoot, i);

                if (source != null && source.transform.IsChildOf(cloneRoot))
                {
                    RepairAfflictionOwnership(source, cloneRoot);
                    ownedSources.Add(source);
                }
            }

            CleanupClonedPatchArtifacts(cloneRoot);
            DisableUnownedAfflictions(cloneRoot, ownedSources);
            var converted = ConvertAfflictions(ownedSources.ToArray(), cloneRoot);
            try
            {
                var buildLog = new System.Text.StringBuilder(768);
                buildLog.Append("[PI-DIAG][AfflictionBuild] templates=").Append(templates.Length)
                    .Append(" cloneLocalBefore=").Append(initialLocalSourceCount)
                    .Append(" owned=").Append(ownedSources.Count)
                    .Append(" converted=").Append(converted.Length)
                    .Append(" remainingVanilla=")
                    .Append(cloneRoot.GetComponentsInChildren<BarAffliction>(true).Length);
                for (int i = 0; i < converted.Length; i++)
                {
                    var affliction = converted[i];
                    buildLog.Append(" | ").Append(i).Append(':');
                    if (affliction == null)
                    {
                        buildLog.Append("null");
                        continue;
                    }
                    buildLog.Append(affliction.afflictionType)
                        .Append(" petrify=").Append(affliction.isPetrify)
                        .Append(" name=").Append(affliction.name)
                        .Append(" self=").Append(affliction.gameObject.activeSelf)
                        .Append(" hierarchy=").Append(affliction.gameObject.activeInHierarchy)
                        .Append(" rtf=").Append(affliction.rtf != null ? affliction.rtf.name : "null");
                }
                PluginLogger.Info(buildLog.ToString());
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("[PI-DIAG][AfflictionBuild] logging failed: " + ex.Message);
            }
            return converted;
        }

        private static BarAffliction[] ResolveAfflictionTemplates(
            StaminaBar originalBar,
            out string sourceKind)
        {
            sourceKind = "none";
            if (originalBar == null || originalBar.transform == null)
                return new BarAffliction[0];

            var result = new List<BarAffliction>();
            var seen = new HashSet<BarAffliction>();
            bool usedRuntimeArray = AddOriginalAfflictionTemplates(
                result,
                seen,
                originalBar.afflictions);

            // StaminaBar.Start fills afflictions from this parent. GUIManager.Start can precede it
            // on clients, so scan the serialized hierarchy as a timing-independent fallback. The
            // scan also completes a partially initialized runtime array.
            var parent = originalBar.transform.parent;
            bool usedParentScan = parent != null && AddOriginalAfflictionTemplates(
                result,
                seen,
                parent.GetComponentsInChildren<BarAffliction>(true));

            if (usedRuntimeArray && usedParentScan) sourceKind = "runtime-array+parent-scan";
            else if (usedRuntimeArray) sourceKind = "runtime-array";
            else if (usedParentScan) sourceKind = "parent-scan";
            return result.ToArray();
        }

        private static bool AddOriginalAfflictionTemplates(
            List<BarAffliction> result,
            HashSet<BarAffliction> seen,
            BarAffliction[] candidates)
        {
            if (candidates == null || candidates.Length == 0) return false;
            bool added = false;
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null
                    || candidate.transform == null
                    || IsInsideTeammateClone(candidate.transform)
                    || !seen.Add(candidate))
                    continue;

                result.Add(candidate);
                added = true;
            }
            return added;
        }

        private static bool IsInsideTeammateClone(Transform transform)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (string.Equals(current.name, "TeammateBar_Clone", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void RepairAfflictionOwnership(BarAffliction source, Transform cloneRoot)
        {
            if (source == null || cloneRoot == null) return;

            if (source.rtf == null || !source.rtf.IsChildOf(cloneRoot))
                source.rtf = source.transform as RectTransform;

            if (source.icon != null && source.icon.transform.IsChildOf(cloneRoot)) return;
            source.icon = null;
            var images = source.gameObject.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null || !images[i].transform.IsChildOf(cloneRoot)) continue;
                source.icon = images[i];
                if (string.Equals(images[i].name, "Icon", StringComparison.OrdinalIgnoreCase)) break;
            }
        }

        private static void DisableUnownedAfflictions(
            Transform cloneRoot,
            List<BarAffliction> ownedSources)
        {
            if (cloneRoot == null) return;
            var owned = new HashSet<BarAffliction>(ownedSources);
            var all = cloneRoot.GetComponentsInChildren<BarAffliction>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var source = all[i];
                if (source == null || owned.Contains(source)) continue;
                source.enabled = false;

                bool sharesOwnedObject = false;
                var sameObject = source.gameObject.GetComponents<BarAffliction>();
                for (int j = 0; j < sameObject.Length; j++)
                {
                    if (sameObject[j] != null && owned.Contains(sameObject[j]))
                    {
                        sharesOwnedObject = true;
                        break;
                    }
                }

                bool standaloneVisual = source.transform != cloneRoot
                    && source.gameObject.GetComponent<StaminaBar>() == null
                    && !sharesOwnedObject;
                if (standaloneVisual && source.gameObject.activeSelf)
                    source.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(source);
            }
        }

        private static BarAffliction CloneAfflictionTemplate(
            BarAffliction template,
            Transform cloneRoot,
            Transform originalBarRoot,
            int index)
        {
            try
            {
                var cloneObject = UnityEngine.Object.Instantiate(template.gameObject, cloneRoot, false);
                cloneObject.name = "PI_TeammateAffliction_" + index + "_" + template.gameObject.name;

                var sourceRect = template.transform as RectTransform;
                var cloneRect = cloneObject.transform as RectTransform;
                if (sourceRect != null && cloneRect != null && originalBarRoot != null)
                {
                    // Preserve the template's position relative to the original stamina bar even
                    // when the vanilla affliction is a sibling elsewhere in the bar group.
                    cloneRect.localPosition = originalBarRoot.InverseTransformPoint(sourceRect.position);
                    cloneRect.localRotation = Quaternion.Inverse(originalBarRoot.rotation) * sourceRect.rotation;
                    cloneRect.sizeDelta = sourceRect.sizeDelta;
                }

                var source = cloneObject.GetComponent<BarAffliction>();
                if (source == null) return null;

                if (source.rtf == null || !source.rtf.IsChildOf(cloneObject.transform))
                    source.rtf = cloneRect != null ? cloneRect : cloneObject.GetComponent<RectTransform>();

                if (source.icon == null || !source.icon.transform.IsChildOf(cloneObject.transform))
                {
                    source.icon = null;
                    var images = cloneObject.GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < images.Length; i++)
                    {
                        if (images[i] == null) continue;
                        if (template.icon != null && images[i].name == template.icon.name)
                        {
                            source.icon = images[i];
                            break;
                        }
                        if (source.icon == null) source.icon = images[i];
                    }
                }

                return source;
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn(
                    "clone_affliction_" + index,
                    "CloneAfflictionTemplate failed: " + ex.Message);
                return null;
            }
        }

        private static TeammateBarAffliction[] ConvertAfflictions(
            BarAffliction[] sources,
            Transform ownershipRoot)
        {
            if (ownershipRoot == null || sources == null || sources.Length == 0)
                return new TeammateBarAffliction[0];

            var result = new List<TeammateBarAffliction>(sources.Length);
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source == null || !source.transform.IsChildOf(ownershipRoot))
                {
                    if (source != null)
                    {
                        PluginLogger.ThrottleWarn(
                            "foreign_affliction_reference",
                            "Skipped a BarAffliction outside the teammate clone to protect the local HUD.");
                    }
                    continue;
                }

                var target = source.gameObject.GetComponent<TeammateBarAffliction>();
                if (target == null) target = source.gameObject.AddComponent<TeammateBarAffliction>();
                source.enabled = false;
                target.Initialize(source);
                result.Add(target);
                UnityEngine.Object.Destroy(source);
            }
            return result.ToArray();
        }

        private static bool ShouldPlaceExtraTextOnRight()
        {
            var anchor = PlayersInfoPlugin.CfgAnchor != null
                ? PlayersInfoPlugin.CfgAnchor.Value
                : PlayersInfoPlugin.HudAnchor.BottomLeft;
            return anchor == PlayersInfoPlugin.HudAnchor.TopLeft
                || anchor == PlayersInfoPlugin.HudAnchor.BottomLeft;
        }

        private void SyncExtraTextPlacement()
        {
            bool rightSide = ShouldPlaceExtraTextOnRight();
            for (int i = 0; i < _pool.Count; i++)
            {
                var drv = _pool[i];
                if (drv == null || drv.extraValueText == null) continue;
                ConfigureSideText(drv.extraValueText.rectTransform, rightSide);
                drv.extraValueText.alignment = rightSide ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
            }
        }

        private static void ConfigureSideText(RectTransform rt, bool rightSide)
        {
            if (rt == null) return;
            if (rightSide)
            {
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(40f, 0f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(24f, 0f);
            }
            rt.sizeDelta = new Vector2(120f, 24f);
        }

        /// <summary>在主 bar（或其他 RectTransform）两端外侧添加一个数值文本。
        /// rightSide=true 贴右端外，false 贴左端外。默认隐藏。</summary>
        private TMP_Text AddSideText(RectTransform host, string name, bool rightSide, Color color, float fontSize = 16f)
        {
            try
            {
                if (host == null) return null;
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(host, false);
                var rt = go.GetComponent<RectTransform>();
                ConfigureSideText(rt, rightSide);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.font = FontHelper.GetChineseCapable();
                tmp.fontSize = fontSize;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = rightSide ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
                tmp.color = color;
                tmp.raycastTarget = false;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                TmpOutlineHelper.Apply(tmp, TmpOutlineHelper.DefaultWidth, new Color32(0, 0, 0, 220));
                tmp.text = string.Empty;
                go.SetActive(false);
                return tmp;
            }
            catch (System.Exception ex)
            {
                PluginLogger.ThrottleWarn("side_text_" + name, "AddSideText failed: " + ex.Message);
                return null;
            }
        }

        private TMP_Text TryAddNameLabel(GameObject cloneGo)
        {
            try
            {
                var nameGo = new GameObject("TeammateNameLabel", typeof(RectTransform));
                nameGo.transform.SetParent(cloneGo.transform, false);
                var rt = nameGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(240f, 22f);
                rt.anchoredPosition = new Vector2(0f, 52f);
                var tmp = nameGo.AddComponent<TextMeshProUGUI>();
                // 用游戏自带含中文的字体，避免中文变豆腐块：优先没 AscentUI 的 font，其次 heroDayText
                tmp.font = FontHelper.GetChineseCapable();
                tmp.fontSize = 18f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.raycastTarget = false;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                TmpOutlineHelper.Apply(tmp, TmpOutlineHelper.DefaultWidth, new Color32(0, 0, 0, 200));
                tmp.text = string.Empty;
                return tmp;
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("name_label", "AddNameLabel failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>删掉克隆体上由 LocalStaminaBarPatch 深拷而来的 TMP 子节点（名字以 PI_Local 开头）。</summary>
        private static void CleanupClonedPatchArtifacts(Transform root)
        {
            if (root == null) return;
            try
            {
                // 包含未激活的节点一起遍历
                var all = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t == null || t == root) continue;
                    var n = t.name;
                    if (!string.IsNullOrEmpty(n) && n.StartsWith("PI_Local"))
                    {
                        if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
                        UnityEngine.Object.Destroy(t.gameObject);
                    }
                }
            }
            catch (System.Exception ex)
            {
                PluginLogger.ThrottleWarn("clone_cleanup", "CleanupClonedPatchArtifacts failed: " + ex.Message);
            }
        }
        
        /// <summary>如果原版 StaminaBar 组件里 extraBar* 字段未赋值，按名字在子节点里找回来。</summary>
        private static void TryBackfillExtraBarFields(TeammateBarDriver driver, Transform root, StaminaBar origBar)
        {
            if (driver == null || root == null) return;
            try
            {
                // 关键：原版 StaminaBar.extraBar 在运行时可能仍指向 **原版 bar** 的子节点（Instantiate 的引用重绑不生效），
                // 这样所有克隆条的 driver 都操作同一个原版 extraBar，互相抢占 → 真正原因！
                // 对所有引用字段统一做 IsChildOf 校验，走后面的按名字重找
                if (driver.extraBar != null && !driver.extraBar.IsChildOf(root)) driver.extraBar = null;
                if (driver.extraBarStamina != null && !driver.extraBarStamina.IsChildOf(root)) driver.extraBarStamina = null;
                if (driver.extraBarOutline != null && !driver.extraBarOutline.IsChildOf(root)) driver.extraBarOutline = null;
                if (driver.extraStaminaGlow != null && !driver.extraStaminaGlow.transform.IsChildOf(root)) driver.extraStaminaGlow = null;
                if (driver.staminaGlow != null && !driver.staminaGlow.transform.IsChildOf(root)) driver.staminaGlow = null;
                if (driver.fullBar != null && !driver.fullBar.IsChildOf(root)) driver.fullBar = null;
                if (driver.staminaBar != null && !driver.staminaBar.IsChildOf(root)) driver.staminaBar = null;
                if (driver.maxStaminaBar != null && !driver.maxStaminaBar.IsChildOf(root)) driver.maxStaminaBar = null;
                if (driver.staminaBarOutline != null && !driver.staminaBarOutline.IsChildOf(root)) driver.staminaBarOutline = null;
                if (driver.staminaBarOutlineOverflowBar != null && !driver.staminaBarOutlineOverflowBar.IsChildOf(root)) driver.staminaBarOutlineOverflowBar = null;
                if (driver.backing != null && !driver.backing.transform.IsChildOf(root)) driver.backing = null;
                if (driver.shield != null && !driver.shield.transform.IsChildOf(root)) driver.shield = null;
                if (driver.campfire != null && !driver.campfire.transform.IsChildOf(root)) driver.campfire = null;

                var all = root.GetComponentsInChildren<RectTransform>(true);
                // 首次诊断：dump 所有子节点名字，看原版 extraBar 究竟叫什么（只打一条综合日志）
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[CloneDump] ").Append(root.name).Append(" tree:");
                    for (int di = 0; di < all.Length; di++)
                    {
                        var t = all[di];
                        if (t == null) continue;
                        sb.Append(" ").Append(t.name);
                    }
                    PluginLogger.Debug(sb.ToString());
                }
                catch { }
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t == null || t == root) continue;
                    var n = t.name;
                    if (string.IsNullOrEmpty(n)) continue;
                    string nl = n.ToLowerInvariant();
                    // 临时体力相关：宽松匹配所有名字含"extra"的
                    bool isExtra = nl.Contains("extra");
                    if (isExtra)
                    {
                        // outline
                        if (driver.extraBarOutline == null && nl.Contains("outline"))
                            driver.extraBarOutline = t;
                        // 内层充盈条（涉及 stamina）
                        else if (driver.extraBarStamina == null && nl.Contains("stamina"))
                            driver.extraBarStamina = t;
                        // 外层泡泡容器（它本身，不含 outline / stamina）
                        else if (driver.extraBar == null && !nl.Contains("outline") && !nl.Contains("glow"))
                            driver.extraBar = t;
                    }
                    // 主体结构
                    if (driver.fullBar == null && nl == "fullbar") driver.fullBar = t;
                    if (driver.staminaBar == null && (nl == "staminabar" || nl == "stamina")) driver.staminaBar = t;
                    if (driver.maxStaminaBar == null && nl.Contains("maxstamina")) driver.maxStaminaBar = t;
                    if (driver.staminaBarOutline == null && (nl == "staminabaroutline" || nl == "outline")) driver.staminaBarOutline = t;
                }
                // extraStaminaGlow 按 Image 类型找一个含 "glow" 的子节点
                if (driver.extraStaminaGlow == null)
                {
                    var imgs = root.GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < imgs.Length; i++)
                    {
                        var img = imgs[i];
                        if (img == null) continue;
                        var n = img.name ?? string.Empty;
                        var nl = n.ToLowerInvariant();
                        if (nl.Contains("extra") && nl.Contains("glow")) { driver.extraStaminaGlow = img; break; }
                    }
                }

                // 关键兜底：原版 bar 克隆树里根本没有 extraBar 子节点（我们 dump 确认过），
                // 原因是原版 extraBar 挂在 bar prefab 外的独立节点上。
                // 优先克隆原版 extraBar 子树（风格一致），失败再自建。
                if (driver.extraBar == null || driver.extraBarStamina == null)
                {
                    BuildExtraBarFromOrig(driver, root, origBar);
                }
                PluginLogger.Debug("[CloneExtra] extraBar=" + (driver.extraBar != null ? driver.extraBar.name : "null")
                                  + " extraBarStamina=" + (driver.extraBarStamina != null ? driver.extraBarStamina.name : "null")
                                  + " extraBarOutline=" + (driver.extraBarOutline != null ? driver.extraBarOutline.name : "null")
                                  + " fullBar=" + (driver.fullBar != null ? driver.fullBar.name : "null")
                                  + " staminaBar=" + (driver.staminaBar != null ? driver.staminaBar.name : "null"));
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("backfill_extra", "TryBackfillExtraBarFields failed: " + ex.Message);
            }
        }

        /// <summary>优先克隆原版 StaminaBar.extraBar 的整棵子树（风格一致），原版无值时再授权自建。</summary>
        private static void BuildExtraBarFromOrig(TeammateBarDriver driver, Transform root, StaminaBar origBar)
        {
            if (driver == null || root == null) return;
            try
            {
                // 预检查原版可用性
                if (origBar != null && origBar.extraBar != null)
                {
                    var origExtra = origBar.extraBar;
                    var origParent = origExtra.parent;

                    // 诊断日志：先把原版 parent 打出来，方便确认挂点是否谱
                    PluginLogger.Debug("[ExtraBuild] origParent=" + (origParent != null ? origParent.name : "null")
                        + " origBar=" + origBar.name
                        + " origBar.fullBar=" + (origBar.fullBar != null ? origBar.fullBar.name : "null")
                        + " origExtra.size=" + origExtra.sizeDelta
                        + " origExtra.pos=" + origExtra.anchoredPosition);

                    // 按原版 parent 选择克隆树里的逻辑对应父（保持相对位置稳定）
                    // 关键发现：原版 extraBar 不在 Bar 子树！而是和 Bar 一起作为 BarGroup 的子（兄弟）。
                    // 所以原版结构是：BarGroup → [Bar, ExtraBar]，extraBar.anchoredPosition 位于 BarGroup 坐标系。
                    // 克隆时我们要让 PI_ExtraBar 跟随具体的克隆 bar 一起移动，所以挂在 root 内部更安全，
                    // 位置用 world space 换算：最终位置 = 原版 ExtraBar 相对原版 Bar 的位移 + 克隆 Bar 的 world 位置。
                    Transform targetParent = root;

                    var cloneGo = UnityEngine.Object.Instantiate(origExtra.gameObject, targetParent, false);
                    cloneGo.name = "PI_ExtraBar";
                    cloneGo.SetActive(false);

                    var cloneRt = cloneGo.GetComponent<RectTransform>();
                    // 原样复制锡定与尺寸，确保与原版对齐
                    cloneRt.anchorMin = origExtra.anchorMin;
                    cloneRt.anchorMax = origExtra.anchorMax;
                    cloneRt.pivot = origExtra.pivot;
                    cloneRt.sizeDelta = origExtra.sizeDelta;
                    cloneRt.localScale = origExtra.localScale;
                    cloneRt.localRotation = origExtra.localRotation;

                    // world space 校准位置：
                    // 期望 PI_ExtraBar 的 world pos = 克隆 Bar 的 world pos + （原版 ExtraBar world pos - 原版 Bar world pos）
                    try
                    {
                        Vector3 delta = origExtra.position - origBar.transform.position;
                        cloneGo.transform.position = root.position + delta;
                    }
                    catch
                    {
                        cloneRt.anchoredPosition = origExtra.anchoredPosition;
                    }

                    driver.extraBar = cloneRt;
                    driver.extraBarInitialSize = origExtra.sizeDelta;

                    // 按原版字段的子节点名，在克隆子树里再绑 extraBarStamina / extraBarOutline
                    string stamName = origBar.extraBarStamina != null ? origBar.extraBarStamina.name : null;
                    string outlineName = origBar.extraBarOutline != null ? origBar.extraBarOutline.name : null;
                    string glowName = origBar.extraStaminaGlow != null ? origBar.extraStaminaGlow.name : null;
                    var subRts = cloneRt.GetComponentsInChildren<RectTransform>(true);
                    for (int i = 0; i < subRts.Length; i++)
                    {
                        var t = subRts[i];
                        if (t == null || t == cloneRt) continue;
                        if (driver.extraBarStamina == null && stamName != null && t.name == stamName) driver.extraBarStamina = t;
                        if (driver.extraBarOutline == null && outlineName != null && t.name == outlineName) driver.extraBarOutline = t;
                    }
                    if (glowName != null)
                    {
                        var imgs = cloneRt.GetComponentsInChildren<Image>(true);
                        for (int i = 0; i < imgs.Length; i++)
                        {
                            if (imgs[i] != null && imgs[i].name == glowName) { driver.extraStaminaGlow = imgs[i]; break; }
                        }
                    }

                    PluginLogger.Debug("[ExtraBuild] cloned from orig under " + targetParent.name
                        + " stam=" + (driver.extraBarStamina != null ? driver.extraBarStamina.name : "null")
                        + " outline=" + (driver.extraBarOutline != null ? driver.extraBarOutline.name : "null"));
                    return;
                }
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("extra_clone", "BuildExtraBarFromOrig clone failed: " + ex.Message);
            }

            // 原版无值 → 授权自建简版
            BuildSimpleExtraBar(driver);
        }

        /// <summary>自建一个简版 extraBar（泡泡容器 + 内层绿色充盈条），仅当原版 extraBar 不可用时授权使用。</summary>
        private static void BuildSimpleExtraBar(TeammateBarDriver driver)
        {
            if (driver == null || driver.fullBar == null) return;
            try
            {
                // 宿主放在 fullBar 下，锡定到 fullBar 右端，跳往右延伸
                var parent = driver.fullBar;

                // 外层泡泡容器 PI_ExtraBar
                var bubble = new GameObject("PI_ExtraBar", typeof(RectTransform));
                bubble.transform.SetParent(parent, false);
                var bubRt = (RectTransform)bubble.transform;
                bubRt.anchorMin = new Vector2(1f, 0.5f);
                bubRt.anchorMax = new Vector2(1f, 0.5f);
                bubRt.pivot = new Vector2(0f, 0.5f);
                bubRt.anchoredPosition = new Vector2(6f, 0f);
                bubRt.sizeDelta = new Vector2(45f, 45f);
                bubble.SetActive(false); // 默认隐藏，由 DoUpdate 按 hasExtra 控制

                // 内层充盈条 PI_ExtraStamina（stretch y，宽由 driver 按 extra*fullWidth 推）
                var stam = new GameObject("PI_ExtraStamina", typeof(RectTransform), typeof(Image));
                stam.transform.SetParent(bubble.transform, false);
                var stamRt = (RectTransform)stam.transform;
                stamRt.anchorMin = new Vector2(0f, 0f);
                stamRt.anchorMax = new Vector2(0f, 1f);
                stamRt.pivot = new Vector2(0f, 0.5f);
                stamRt.anchoredPosition = new Vector2(0f, 0f);
                stamRt.sizeDelta = new Vector2(6f, 0f);
                var img = stam.GetComponent<Image>();
                img.color = new Color(0.54f, 0.91f, 0.27f, 1f); // 绿色，与主体力条风格接近
                img.raycastTarget = false;

                driver.extraBar = bubRt;
                driver.extraBarStamina = stamRt;
                // 初始尺寸同步给 driver，避免用硬码 45x45
                driver.extraBarInitialSize = new Vector2(45f, 45f);

                PluginLogger.Debug("[ExtraBuild] PI_ExtraBar built under " + parent.name);
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("extra_build", "BuildSimpleExtraBar failed: " + ex.Message);
            }
        }

        /// <summary>在 BarAffliction 上叠一个百分比文本，尺寸跟随 parent（stretch）。</summary>
        private TMP_Text AddAfflictionText(GameObject hostGo, string label)
        {
            try
            {
                if (hostGo == null) return null;
                CleanupExistingTeammateAfflictionTexts(hostGo.transform);
                var go = new GameObject("PI_" + label, typeof(RectTransform));
                go.transform.SetParent(hostGo.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.font = FontHelper.GetChineseCapable();
                tmp.fontSize = 16f;
                // 不用 Bold：默认字体+outline 已经足够清晰，Bold 在小条上会涂抹
                tmp.fontStyle = FontStyles.Normal;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                TmpOutlineHelper.Apply(tmp, TmpOutlineHelper.DefaultWidth, new Color32(0, 0, 0, 255));
                tmp.text = string.Empty;
                return tmp;
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("aff_text", "AddAfflictionText failed: " + ex.Message);
                return null;
            }
        }

        private static void CleanupExistingTeammateAfflictionTexts(Transform host)
        {
            if (host == null) return;
            var all = host.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var child = all[i];
                if (child == null || child == host || string.IsNullOrEmpty(child.name)) continue;
                if (!child.name.StartsWith("PI_AfflictionPct_", StringComparison.Ordinal)) continue;
                if (child.gameObject.activeSelf) child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static void ValidateClonedBarArtifacts(Transform cloneRoot, int expectedAfflictions)
        {
            if (cloneRoot == null) return;
            int activeLocalTexts = 0;
            int teammateTexts = 0;
            var transforms = cloneRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var child = transforms[i];
                if (child == null || string.IsNullOrEmpty(child.name)) continue;
                if (child.name.StartsWith("PI_Local", StringComparison.Ordinal)
                    && child.gameObject.activeSelf)
                    activeLocalTexts++;
                if (child.name.StartsWith("PI_AfflictionPct_", StringComparison.Ordinal)
                    && child.gameObject.activeSelf)
                    teammateTexts++;
            }

            int enabledVanilla = 0;
            var vanilla = cloneRoot.GetComponentsInChildren<BarAffliction>(true);
            for (int i = 0; i < vanilla.Length; i++)
                if (vanilla[i] != null && vanilla[i].enabled) enabledVanilla++;

            int ownedCount = cloneRoot.GetComponentsInChildren<TeammateBarAffliction>(true).Length;
            bool valid = activeLocalTexts == 0
                && enabledVanilla == 0
                && teammateTexts == expectedAfflictions
                && ownedCount == expectedAfflictions;
            string summary = "[CloneValidation] localActive=" + activeLocalTexts
                + " teammateTexts=" + teammateTexts
                + " vanillaEnabled=" + enabledVanilla
                + " owned=" + ownedCount
                + " expected=" + expectedAfflictions;
            if (valid) PluginLogger.ThrottleInfo("clone_validation_ok", "[PI-DIAG]" + summary, 5f);
            else PluginLogger.ThrottleWarn("clone_validation", summary);
        }

        /// <summary>仿 StaminaInfo.AddTextObject：在指定条形 GameObject 上加一个居中显示数值的 TMP_Text。</summary>
        private TMP_Text AddValueText(GameObject hostGo, string label)
        {
            try
            {
                var host = hostGo;
                if (host == null) return null;
                // 确保 host 启用，以便 TMP 初始化
                host.SetActive(true);

                var go = new GameObject("PI_" + label, typeof(RectTransform));
                go.transform.SetParent(host.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var tmp = go.AddComponent<TextMeshProUGUI>();
                // 字体统一走含中文的字体访问器
                tmp.font = FontHelper.GetChineseCapable();
                tmp.fontSize = 20f;
                tmp.fontStyle = FontStyles.Normal;   // 不用 Bold，避免过粗
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                TmpOutlineHelper.Apply(tmp, TmpOutlineHelper.DefaultWidth, new Color32(0, 0, 0, 255));
                tmp.text = string.Empty;
                tmp.transform.localScale = Vector3.one;
                return tmp;
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("value_text", "AddValueText failed: " + ex.Message);
                return null;
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                var drv = _pool[i];
                if (drv != null && drv.gameObject != null)
                {
                    if (drv.gameObject.activeSelf) drv.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(drv.gameObject);
                }
            }
            _pool.Clear();
            _driversByStableId.Clear();
            _stableIdByDriver.Clear();
            _displayOrder.Clear();
            _pendingOrder.Clear();
            _pendingOrderSince = -1f;
            _layoutInitialized = false;
            // 跨场景/配置重建时清掉网络波动相关缓存，避免脏数据残留
            s_retainedById.Clear();
            s_viewIdToActor.Clear();
            s_currentStableIds.Clear();
            s_displayOrderSet.Clear();
        }

        public void OnConfigChanged()
        {
            // 最简单：全清重建（配置变更不频繁）
            ClearAll();
            _nextRefreshTime = 0f;
        }

        public void RefreshLayout()
        {
            _layoutInitialized = false;
            FixBarGroupOnce();
            SyncExtraTextPlacement();
        }

        private static int GetStableCharacterId(Character c)
        {
            if (c == null || c.Equals(null) || c.photonView == null) return int.MinValue;
            int viewId = c.photonView.ViewID;
            try
            {
                if (c.photonView.Owner != null)
                {
                    int actor = c.photonView.Owner.ActorNumber;
                    // 缓存 viewId→actor 映射，给 Owner 短暂为 null 时回查
                    s_viewIdToActor[viewId] = actor;
                    return actor;
                }
                // Owner 暂时为 null（Photon 网络抖动）：从缓存回查 ActorNumber，避免 stableId 漂移到 ViewID
                if (s_viewIdToActor.TryGetValue(viewId, out int cachedActor))
                    return cachedActor;
            }
            catch { }
            return viewId;
        }

        private void ResolveDisplayOrder(List<Character> desiredCharacters)
        {
            var desiredIds = new List<int>(desiredCharacters.Count);
            for (int i = 0; i < desiredCharacters.Count; i++)
            {
                int stableId = GetStableCharacterId(desiredCharacters[i]);
                if (stableId != int.MinValue) desiredIds.Add(stableId);
            }

            if (PlayersInfoPlugin.CfgTeammateSortMode == null
                || PlayersInfoPlugin.CfgTeammateSortMode.Value == PlayersInfoPlugin.TeammateSortMode.Stable)
            {
                ApplyStableOrder(desiredIds);
            }

            if (!HasSameMembers(_displayOrder, desiredIds))
            {
                ApplyDisplayOrder(desiredIds);
                return;
            }

            if (HasSameSequence(_displayOrder, desiredIds))
            {
                _pendingOrder.Clear();
                _pendingOrderSince = -1f;
                return;
            }

            if (!HasSameSequence(_pendingOrder, desiredIds))
            {
                _pendingOrder.Clear();
                _pendingOrder.AddRange(desiredIds);
                _pendingOrderSince = Time.unscaledTime;
                return;
            }

            if (_pendingOrderSince < 0f)
            {
                _pendingOrderSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _pendingOrderSince >= ReorderDelay)
                ApplyDisplayOrder(desiredIds);
        }

        private void ApplyStableOrder(List<int> desiredIds)
        {
            if (desiredIds.Count < 2 || _displayOrder.Count == 0) return;

            _stableOrderScratch.Clear();
            for (int i = 0; i < _displayOrder.Count; i++)
            {
                int id = _displayOrder[i];
                if (!desiredIds.Contains(id)) continue;
                _stableOrderScratch.Add(id);
            }

            for (int i = 0; i < desiredIds.Count; i++)
            {
                int id = desiredIds[i];
                if (!_stableOrderScratch.Contains(id)) _stableOrderScratch.Add(id);
            }

            desiredIds.Clear();
            desiredIds.AddRange(_stableOrderScratch);
        }

        private void ApplyDisplayOrder(List<int> desiredIds)
        {
            _displayOrder.Clear();
            _displayOrder.AddRange(desiredIds);
            _pendingOrder.Clear();
            _pendingOrderSince = -1f;
        }

        private static bool HasSameMembers(List<int> left, List<int> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (!right.Contains(left[i])) return false;
            }
            return true;
        }

        private static bool HasSameSequence(List<int> left, List<int> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i]) return false;
            }
            return true;
        }
    }
}
