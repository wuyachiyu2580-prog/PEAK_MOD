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
        private StaminaBar _origBar;
        private bool _barGroupFixed;

        private float _nextRefreshTime;
        private const float RefreshInterval = 0.25f;
        private float _nextDistLogTime;
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
                _origBar = GUIManager.instance.bar;
                FixBarGroupOnce();
            }
            catch (Exception ex)
            {
                PluginLogger.Error("BarsCoordinator.Init failed: " + ex.Message);
            }
        }

        /// <summary>参考 PeakStats 的 FixBarGroup：扩大 bar 的父容器 sizeDelta 和 VLG spacing。</summary>
        private void FixBarGroupOnce()
        {
            if (_barGroupFixed) return;
            try
            {
                var parentRt = _origBar.transform.parent as RectTransform;
                if (parentRt == null) return;
                parentRt.sizeDelta = new Vector2(600f, 600f);
                var pos = parentRt.anchoredPosition;
                pos.y = 353f;
                parentRt.anchoredPosition = pos;
                var vlg = parentRt.GetComponent<VerticalLayoutGroup>();
                if (vlg != null) vlg.spacing = 25f;
                _barGroupFixed = true;
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("fix_bargroup", "FixBarGroup failed: " + ex.Message);
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
            if (PlayersInfoPlugin.CfgModEnabled == null || !PlayersInfoPlugin.CfgModEnabled.Value)
            {
                HideAll();
                return;
            }

            var tracker = TeamRosterTracker.Instance;
            if (tracker == null) return;

            var local = Character.localCharacter;
            if (local == null || local.Equals(null)) { HideAll(); return; }

            int maxN = PlayersInfoPlugin.CfgMaxNearbyCount != null ? PlayersInfoPlugin.CfgMaxNearbyCount.Value : 3;
            float range = PlayersInfoPlugin.CfgNearbyRange != null ? PlayersInfoPlugin.CfgNearbyRange.Value : 30f;
            if (maxN <= 0) { HideAll(); return; }

            // 按距离升序收集：用 Character.Center 而不是 transform.position
            // （PEAK 的 Character.transform 是逻辑根，一直在原点）
            // 灵魂状态（本地角色死亡）下 local.Center 会跟观战镜头跳动 → 距离排序剧变→条抜动
            // 解决：死亡时取消距离排序，按 ViewID 稳定展示
            _scratch.Clear();
            s_visibleScratch.Clear();
            s_visibleById.Clear();
            s_seenStableIds.Clear();
            // 预算当前已显示集合，供距离滞回使用（O(1) 查询）
            s_displayOrderSet.Clear();
            for (int doi = 0; doi < _displayOrder.Count; doi++) s_displayOrderSet.Add(_displayOrder[doi]);
            var mates = tracker.Teammates;
            bool localDead = (local.data != null && local.data.dead);
            Vector3 lp = local.Center;
            for (int i = 0; i < mates.Count; i++)
            {
                var c = mates[i];
                if (c == null || c.Equals(null)) continue;
                if (c.data == null) continue;
                if (c.photonView == null) continue;
                // Owner 短暂为 null 不再剔除（网络抖动期间）：依靠 GetStableCharacterId 的 viewId→actor 缓存
                // 把同一玩家识别成同一 stableId，避免被当作新玩家造成体力条切换。
                float d = localDead
                    ? c.photonView.ViewID   // 死亡时用 ViewID 做稳定序
                    : Vector3.Distance(lp, c.Center);
                if (!localDead && range > 0f)
                {
                    // 已显示玩家用宽松阈值 range+HysteresisMargin，避免边缘抹动 → 体力条反复闪烁
                    int sidForRange = GetStableCharacterId(c);
                    float effectiveRange = s_displayOrderSet.Contains(sidForRange)
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
            }
            // 网络波动保留：本帧未出现但仍在 _displayOrder 里的玩家，若失踪时间未超 RetainOnLossDelay，
            // 用上次的 Character 引用补回 s_visibleScratch / s_visibleById。这样 ResolveDisplayOrder
            // 看到的成员集合不变，HasSameMembers 仍为 true，不会触发 ApplyDisplayOrder 立即重建。
            float retainNow = Time.unscaledTime;
            for (int doi = 0; doi < _displayOrder.Count; doi++)
            {
                int sid = _displayOrder[doi];
                if (s_visibleById.ContainsKey(sid))
                {
                    s_retainedById.Remove(sid);
                    continue;
                }
                Character retainedCh = null;
                if (s_retainedById.TryGetValue(sid, out var entry))
                {
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

            int showCount = s_visibleScratch.Count;
            ResolveDisplayOrder(s_visibleScratch);

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

            // 确保 pool 足够（最多到 maxN）
            while (_pool.Count < showCount)
            {
                var drv = CreateBar();
                if (drv == null) break;
                _pool.Add(drv);
            }

            // 绑定前 showCount 个，其余隐藏
            for (int i = 0; i < _pool.Count; i++)
            {
                var drv = _pool[i];
                if (drv == null) continue;
                if (i < showCount)
                {
                    if (i >= _displayOrder.Count || !s_visibleById.TryGetValue(_displayOrder[i], out var c) || c == null)
                    {
                        if (drv.gameObject.activeSelf) drv.gameObject.SetActive(false);
                        continue;
                    }
                    if (!drv.gameObject.activeSelf) drv.gameObject.SetActive(true);
                    // 目标变化时才重绑，避免每 0.25s 都重置
                    if (drv.Target != c) drv.BindTarget(c);
                    // 子组件 target 同步
                    if (drv.InventoryRow != null) drv.InventoryRow.Target = c;
                }
                else
                {
                    if (drv.gameObject.activeSelf) drv.gameObject.SetActive(false);
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
                var cloneTransform = UnityEngine.Object.Instantiate(origTransform, origTransform.parent);
                cloneTransform.name = "TeammateBar_Clone";
                cloneTransform.SetAsFirstSibling();

                var cloneGo = cloneTransform.gameObject;
                // 清理：原版条上被 LocalStaminaBarPatch 动态添加的 PI_Local* 子节点会被 Instantiate 深拷进来，
                // 和我们下面自己要加的文本重叠 → 全部删除
                CleanupClonedPatchArtifacts(cloneTransform);
                var origCompOnClone = cloneGo.GetComponent<StaminaBar>();
                var driver = cloneGo.AddComponent<TeammateBarDriver>();

                if (origCompOnClone != null)
                {
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
                    driver.afflictions = origCompOnClone.afflictions;

                    // 温和放弃：extraBar 克隆方案太脂肩（原版 extraBar 在 BarGroup 下和 Bar 同级，
                    // 克隆后坐标换算、parent 选择、sibling order 都有坑）。
                    // 改为贴在 fullBar 左端外侧放临时体力数值。
                    // driver.extraBar / extraBarStamina / extraBarOutline 继承自 origCompOnClone，指向的是原版节点，
                    // 清一下避免 DoUpdate 误操作到原版 UI。
                    driver.extraBar = null;
                    driver.extraBarStamina = null;
                    driver.extraBarOutline = null;
                    driver.extraStaminaGlow = null;
                
                    // 销毁原版组件，防止它读 observedCharacter 与我们冲突
                    UnityEngine.Object.Destroy(origCompOnClone);
                }
                else
                {
                    // 若原版组件不在（异常情况），手动抓 afflictions
                    driver.afflictions = cloneGo.GetComponentsInChildren<BarAffliction>(true);
                }

                // 名字标签
                driver.nameLabel = TryAddNameLabel(cloneGo);

                // 数值文本（仿 StaminaInfo）
                bool showValue = PlayersInfoPlugin.CfgShowStaminaValue == null || PlayersInfoPlugin.CfgShowStaminaValue.Value;
                if (showValue && driver.staminaBar != null)
                    driver.staminaValueText = AddValueText(driver.staminaBar.gameObject, "StaminaValue");
                // 临时体力数字改为贴在 fullBar 左端外侧（有值才显）
                if (showValue && driver.fullBar != null)
                    driver.extraValueText = AddSideText(driver.fullBar, "PI_MateExtraValue", false, new Color(0.55f, 1f, 0.35f));

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

                // 附加：物品栏（作为克隆体子节点，同一行放在名字右边）
                const float NameWidth = 140f;   // 名字标签固定宽
                const float RowGap = 8f;        // 名字与物品栏间隔
                const float RowY = 34f;         // 同行 Y 偏移（bar 上方）
                if (driver.fullBar != null)
                {
                    var hostRect = cloneTransform as RectTransform;
                    if (PlayersInfoPlugin.CfgEnableInventoryRow != null && PlayersInfoPlugin.CfgEnableInventoryRow.Value)
                    {
                        float invWidth = Mathf.Max(180f, driver.fullBar.rect.width);
                        driver.InventoryRow = TeammateInventoryRow.Build(hostRect, invWidth, 22f, 1.5f);
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
                if (rightSide)
                {
                    rt.anchorMin = new Vector2(1f, 0.5f);
                    rt.anchorMax = new Vector2(1f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(8f, 0f);
                }
                else
                {
                    rt.anchorMin = new Vector2(0f, 0.5f);
                    rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.anchoredPosition = new Vector2(-8f, 0f);
                }
                rt.sizeDelta = new Vector2(90f, 22f);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.font = FontHelper.GetChineseCapable();
                tmp.fontSize = fontSize;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = rightSide ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
                tmp.color = color;
                tmp.raycastTarget = false;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
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
                if (drv != null && drv.gameObject != null) UnityEngine.Object.Destroy(drv.gameObject);
            }
            _pool.Clear();
            _displayOrder.Clear();
            _pendingOrder.Clear();
            _pendingOrderSince = -1f;
            // 跨场景/配置重建时清掉网络波动相关缓存，避免脏数据残留
            s_retainedById.Clear();
            s_viewIdToActor.Clear();
        }

        public void OnConfigChanged()
        {
            // 最简单：全清重建（配置变更不频繁）
            ClearAll();
            _nextRefreshTime = 0f;
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
