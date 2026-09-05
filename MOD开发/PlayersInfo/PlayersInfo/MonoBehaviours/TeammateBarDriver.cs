using System;
using System.Reflection;
using DG.Tweening;
using PlayersInfo.Helpers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayersInfo.MonoBehaviours
{
    /// <summary>
    /// 挂在"克隆出来的原版 StaminaBar"上，接管 Update 逻辑：
    /// - 原版 StaminaBar 组件已被销毁（避免读 observedCharacter 冲突）
    /// - 保留原版主体力 UI 引用，队友额外体力仅使用单独的 current/cap 文本
    /// - Update 完整复制 StaminaBar.Update 但把 observedCharacter 换成 Target
    /// 这样复用主体力视觉，同时显式绑定任意队友。
    /// </summary>
    internal class TeammateBarDriver : MonoBehaviour
    {
        public Character Target;

        // UI 字段引用（从原版 bar 复制而来）
        public Image backing;
        public RectTransform fullBar;
        public RectTransform staminaBar;
        public Image staminaGlow;
        public Image extraStaminaGlow;
        public RectTransform maxStaminaBar;
        public RectTransform staminaBarOutline;
        public RectTransform staminaBarOutlineOverflowBar;
        public RectTransform extraBar;
        public RectTransform extraBarStamina;
        public RectTransform extraBarOutline;
        /// <summary>原版 extraBar 初始 sizeDelta（沿用自适应分辨率/UI 缩放）。CreateBar 时从原版 bar 读入。</summary>
        public Vector2 extraBarInitialSize = new Vector2(45f, 45f);
        public float staminaBarOffset;
        public float minStaminaBarWidth = 20f;
        public GameObject shield;
        public GameObject campfire;
        public Color defaultBackingColor;
        public Color outOfStaminaBackingColor;
        public TMP_Text nameLabel;   // 可选：上方显示玩家名

        // 数值文本（仿 StaminaInfo：size/6 显示）
        public TMP_Text staminaValueText;
        public TMP_Text extraValueText;

        // 异常（Afflictions）
        public TeammateBarAffliction[] afflictions;
        public TMP_Text[] afflictionTexts;
        public float minAfflictionWidth = 15f;

        // 附加扩展（我们自己加的）
        public TeammateInventoryRow InventoryRow;

        // 动态状态
        private float desiredStaminaSize;
        private float desiredMaxStaminaSize;
        private float desiredExtraStaminaSize;
        private float cachedExtraStam;
        private float sinTime;
        private float TAU = 6.2831855f;
        private bool outOfStamina;

        // CharacterSyncData 不同步 Character.infiniteStam，但会同步 Affliction_InfiniteStamina。
        // 因此队友条按异常列表识别效果，并冻结生效前最后一份可信主体力显示。
        private bool _wasInfiniteStamina;
        private bool _hasReliableMainStamina;
        private float _lastReliableMainStamina01;
        private float _frozenInfiniteStamina01;
        private float _displayedMainStamina01;

        // 临时体力克隆方案已废弃，改为 fullBar 安全侧外侧文本显示。
        // [ExtraVal] 量纲诊断日志的节流计时。
        private float _nextExtraValLogTime;

        // 数值文本脏检查：避免每帧 ToString + TMP mesh 重建产生 GC。
        private int _lastStaminaShownInt = int.MinValue;
        private int _lastStaminaShownTenth = int.MinValue;
        private bool _lastStaminaWasFloat;
        private int _lastExtraShownInt = int.MinValue;
        private int _lastExtraCapShownInt = int.MinValue;
        private bool _lastExtraShownWithCap = true;
        private float _nextValueRefreshTime;
        private const float ValueRefreshInterval = 0.25f;
        private float _nextAfflictionTextRefreshTime;
        private const float AfflictionTextRefreshInterval = 0.5f;
        private float _nextIdentityRefreshTime;
        private float _nextDiagnosticLogTime;

        // 反射结果缓存：CharacterData.isInvincible 每帧反射 GetValue 会装箱产生 GC，
        // shield 显隐对延迟不敏感，0.25s 探一次足够。
        private bool _cachedInvincible;
        private float _nextInvincibleProbeTime;
        private bool _lastShieldActive;
        private bool _lastCampfireActive;
        private bool _hasShieldState;
        private bool _hasCampfireState;
        private void OnEnable()
        {
            SyncTargetVisualsImmediate();
        }

        /// <summary>切换到新目标时重置内部平滑量，避免从上一个玩家的尺寸插值过去。</summary>
        public void BindTarget(Character c)
        {
            Target = c;
            desiredStaminaSize = 0f;
            desiredMaxStaminaSize = 0f;
            desiredExtraStaminaSize = 0f;
            cachedExtraStam = 0f;
            outOfStamina = false;
            ResetInfiniteStaminaDisplay();
            // 切换目标后立即重新探一次反射 + 强刷一次数值文本，避免显示上一个玩家的状态
            _cachedInvincible = false;
            _nextInvincibleProbeTime = 0f;
            _hasShieldState = false;
            _hasCampfireState = false;
            _lastStaminaShownInt = int.MinValue;
            _lastStaminaShownTenth = int.MinValue;
            _lastStaminaWasFloat = false;
            _lastExtraShownInt = int.MinValue;
            _lastExtraCapShownInt = int.MinValue;
            _lastExtraShownWithCap = true;
            _nextValueRefreshTime = 0f;
            _nextAfflictionTextRefreshTime = 0f;
            _nextIdentityRefreshTime = 0f;
            // 默认字段正序：强制重置 extraBar 显隐，避免克隆时残留 active=true 导致特殊分支
            try
            {
                if (extraBar != null)
                {
                    extraBar.DOKill(false);
                    extraBar.sizeDelta = Vector2.zero;
                    extraBar.gameObject.SetActive(false);
                }
                // 池化 driver 切目标时，上一份的 extraBarStamina / extraBarOutline 宽度会残留
                // 导致新目标 extra=0 时仍显示一大段绿条。这里清零。
                if (extraBarStamina != null)
                {
                    extraBarStamina.DOKill(false);
                    extraBarStamina.sizeDelta = new Vector2(6f, extraBarStamina.sizeDelta.y);
                }
                if (extraBarOutline != null)
                {
                    extraBarOutline.DOKill(false);
                    extraBarOutline.sizeDelta = new Vector2(20f, extraBarOutline.sizeDelta.y);
                }
                cachedExtraStam = 0f;
                desiredExtraStaminaSize = 0f;
            }
            catch { }
            // 切换目标后，将所有 affliction 字段归零，避免显示上个玩家的状态
            if (afflictions != null)
            {
                for (int i = 0; i < afflictions.Length; i++)
                {
                    var a = afflictions[i];
                    if (a == null) continue;
                    a.ResetVisual();
                }
            }
            SyncTargetVisualsImmediate();
            if (nameLabel != null && c != null)
            {
                nameLabel.text = SafeGetName(c);
                if (c.refs != null && c.refs.customization != null)
                    nameLabel.color = c.refs.customization.PlayerColor;
            }
            // 切换新目标时清空数值文本，避免第一帧显示上一个玩家的数值（与 afflictionTexts 处理保持一致）
            if (staminaValueText != null)
            {
                staminaValueText.text = string.Empty;
                if (staminaValueText.gameObject.activeSelf) staminaValueText.gameObject.SetActive(false);
            }
            if (extraValueText != null)
            {
                extraValueText.text = string.Empty;
                if (extraValueText.gameObject.activeSelf) extraValueText.gameObject.SetActive(false);
            }
            if (afflictionTexts != null)
            {
                for (int i = 0; i < afflictionTexts.Length; i++)
                {
                    var tx = afflictionTexts[i];
                    if (tx == null) continue;
                    tx.text = string.Empty;
                    if (tx.gameObject.activeSelf) tx.gameObject.SetActive(false);
                }
            }
            _nextDiagnosticLogTime = 0f;
            PluginLogger.Debug("[PI-DIAG][Bind] driver=" + GetInstanceID()
                + " target=" + SafeGetName(c)
                + " rootSelf=" + gameObject.activeSelf
                + " rootHierarchy=" + gameObject.activeInHierarchy
                + " afflictions=" + (afflictions != null ? afflictions.Length : -1)
                + " texts=" + (afflictionTexts != null ? afflictionTexts.Length : -1));
        }

        private void Update()
        {
            try { DoUpdate(); }
            catch (Exception ex) { PluginLogger.ThrottleError("bar_driver", "TeammateBarDriver Update failed: " + ex.Message); }
        }

        private void DoUpdate()
        {
            if (Target == null || Target.Equals(null) || Target.data == null)
            {
                PluginLogger.ThrottleDebug(
                    "pi_diag_target_" + GetInstanceID(),
                    "[PI-DIAG][DriverSkip] driver=" + GetInstanceID() + " reason=invalid-target",
                    2f);
                return;
            }
            if (fullBar == null || staminaBar == null)
            {
                PluginLogger.ThrottleDebug(
                    "pi_diag_bar_refs_" + GetInstanceID(),
                    "[PI-DIAG][DriverSkip] driver=" + GetInstanceID()
                        + " target=" + SafeGetName(Target)
                        + " reason=missing-bar fullBar=" + (fullBar != null)
                        + " staminaBar=" + (staminaBar != null),
                    2f);
                return;
            }

            // 玩家名与颜色很少变化，低频刷新即可。
            if (nameLabel != null && Time.unscaledTime >= _nextIdentityRefreshTime)
            {
                _nextIdentityRefreshTime = Time.unscaledTime + 0.5f;
                string playerName = SafeGetName(Target);
                if (nameLabel.text != playerName) nameLabel.text = playerName;
                if (Target.refs != null && Target.refs.customization != null)
                {
                    var color = Target.refs.customization.PlayerColor;
                    if (nameLabel.color != color) nameLabel.color = color;
                }
            }

            float fullWidth = fullBar.sizeDelta.x;
            bool targetDead = Target.data.dead;

            float maxStamina01;
            try { maxStamina01 = Mathf.Max(0f, Target.GetMaxStamina()); }
            catch { maxStamina01 = 1f; }
            if (targetDead)
            {
                ResetInfiniteStaminaDisplay();
                _displayedMainStamina01 = 0f;
            }
            else
            {
                _displayedMainStamina01 = ResolveDisplayedMainStamina(
                    Target.data.currentStamina,
                    maxStamina01,
                    HasInfiniteStaminaEffect(Target));
            }

            // === Main Stamina ===
            desiredStaminaSize = Mathf.Max(0f, _displayedMainStamina01 * fullWidth + staminaBarOffset);
            if (!targetDead && Target.data.currentStamina <= 0.005f)
            {
                if (!outOfStamina) { outOfStamina = true; OutOfStaminaPulse(); }
            }
            else outOfStamina = false;

            float dt10 = Time.deltaTime * 10f;
            var sbSize = staminaBar.sizeDelta;
            sbSize.x = Mathf.Lerp(sbSize.x, desiredStaminaSize, dt10);
            staminaBar.sizeDelta = sbSize;

            if (staminaGlow != null)
            {
                var gc = staminaGlow.color;
                float t = Mathf.Clamp01((staminaBar.sizeDelta.x - desiredStaminaSize) * 0.5f);
                sinTime += dt10 * t;
                gc.a = t * 0.4f - Mathf.Abs(Mathf.Sin(sinTime)) * 0.2f;
                staminaGlow.color = gc;
            }

            // === Max Stamina（被饥饿等消耗掉的上限） ===
            desiredMaxStaminaSize = Mathf.Max(0f, maxStamina01 * fullWidth + staminaBarOffset);
            if (maxStaminaBar != null)
            {
                var m = maxStaminaBar.sizeDelta;
                m.x = Mathf.Lerp(m.x, desiredMaxStaminaSize, dt10);
                maxStaminaBar.sizeDelta = m;
                SetActiveIfChanged(maxStaminaBar.gameObject, m.x > minStaminaBarWidth);
            }

            // === Afflictions Outline ===
            float statusSum = 1f;
            try
            {
                if (Target.refs != null && Target.refs.afflictions != null)
                    statusSum = Target.refs.afflictions.statusSum;
            }
            catch { }
            if (staminaBarOutline != null)
            {
                var o = staminaBarOutline.sizeDelta;
                float outlineWidth = 14f + Mathf.Max(1f, statusSum) * fullWidth;
                if (Mathf.Abs(o.x - outlineWidth) > 0.01f)
                {
                    o.x = outlineWidth;
                    staminaBarOutline.sizeDelta = o;
                }
            }
            if (staminaBarOutlineOverflowBar != null)
            {
                SetActiveIfChanged(staminaBarOutlineOverflowBar.gameObject, statusSum > 1.005);
            }

            SetActiveIfChanged(staminaBar.gameObject, staminaBar.sizeDelta.x > minStaminaBarWidth);

            // === Afflictions 数值条（原版 BarAffliction） ===
            if (afflictions != null && Target.data != null)
            {
                CharacterAfflictions ca = null;
                try { ca = Target.refs != null ? Target.refs.afflictions : null; } catch { }
                bool refreshText = Time.unscaledTime >= _nextAfflictionTextRefreshTime;
                if (refreshText)
                    _nextAfflictionTextRefreshTime = Time.unscaledTime + AfflictionTextRefreshInterval;
                for (int i = 0; i < afflictions.Length; i++)
                {
                    var a = afflictions[i];
                    if (a == null) continue;
                    a.UpdateVisual(Target, fullWidth, minAfflictionWidth);
                    float s = AfflictionValueHelper.GetValue(Target, a.afflictionType, a.isPetrify);

                    // 数值文本：百分比+消除时间（≥1% 才展示；太窄就隐藏避免溢出）
                    if (refreshText && afflictionTexts != null && i < afflictionTexts.Length)
                    {
                        var txt = afflictionTexts[i];
                        if (txt != null)
                        {
                            bool showTxt = s > 0.01f && a.width > 18f;
                            if (showTxt)
                            {
                                int pct = Mathf.Clamp(Mathf.RoundToInt(s * 100f), 0, 999);
                                string percentText = pct.ToString();
                                string timeText = AfflictionTimeHelper.FormatTime(
                                    AfflictionTimeHelper.GetReductionTimeRemaining(ca, a.afflictionType));
                                string candidate = string.IsNullOrEmpty(timeText)
                                    ? percentText
                                    : percentText + "(" + timeText + ")";
                                if (txt.GetPreferredValues(candidate).x > a.width)
                                    candidate = percentText;
                                showTxt = txt.GetPreferredValues(candidate).x <= a.width;
                                if (showTxt)
                                {
                                    if (txt.text != candidate) txt.text = candidate;
                                    if (!txt.gameObject.activeSelf) txt.gameObject.SetActive(true);
                                }
                                else if (txt.gameObject.activeSelf)
                                {
                                    txt.gameObject.SetActive(false);
                                }
                            }
                            else
                            {
                                if (txt.gameObject.activeSelf) txt.gameObject.SetActive(false);
                            }
                        }
                    }
                }
            }

            // === Extra Stamina（临时体力）===
            // 已放弃 extraBar 克隆：数值显示在 fullBar 安全侧外（UpdateValueTexts 里按 extraStamina 判显隐）。
            // extraBar/extraBarStamina 已在 Coordinator 里清为 null，以下分支自然不执行。
            float extra = Target.data != null ? Target.data.extraStamina : 0f;
            bool hasExtra = extra > 0.001f;

            if (extraBar != null)
            {
                if (hasExtra)
                {
                    if (!extraBar.gameObject.activeSelf)
                    {
                        extraBar.DOKill(false);
                        // 用原版初始尺寸，适配任意分辨率 / UI 缩放
                        extraBar.sizeDelta = extraBarInitialSize;
                        extraBar.gameObject.SetActive(true);
                    }
            
                    desiredExtraStaminaSize = Mathf.Max(0f, extra * fullWidth);
                    cachedExtraStam = Mathf.Lerp(cachedExtraStam, desiredExtraStaminaSize, dt10);
            
                    if (extraBarOutline != null)
                    {
                        var o = extraBarOutline.sizeDelta;
                        o.x = Mathf.Lerp(o.x, Mathf.Max(20f, desiredExtraStaminaSize + 12f), dt10);
                        extraBarOutline.sizeDelta = o;
                    }
            
                    if (extraStaminaGlow != null)
                    {
                        var c2 = extraStaminaGlow.color;
                        float t = Mathf.Clamp01((extraBar.sizeDelta.x - desiredExtraStaminaSize) * 0.5f);
                        sinTime += dt10 * t;
                        c2.a = t * 0.4f - Mathf.Abs(Mathf.Sin(sinTime)) * 0.2f;
                        extraStaminaGlow.color = c2;
                    }
            
                    if (extraBarStamina != null)
                        extraBarStamina.sizeDelta = new Vector2(Mathf.Max(6f, cachedExtraStam), extraBarStamina.sizeDelta.y);
                }
                else
                {
                    // 没临时体力 → 直接隐藏，不做 tween
                    if (extraBar.gameObject.activeSelf)
                    {
                        extraBar.DOKill(false);
                        extraBar.gameObject.SetActive(false);
                    }
                    cachedExtraStam = 0f;
                    desiredExtraStaminaSize = 0f;
                }
            }

            // === Shield / Campfire ===
            if (shield != null)
            {
                // 反射降频：0.25s 探一次 isInvincible，避免每帧 FieldInfo.GetValue 装箱。
                if (Time.unscaledTime >= _nextInvincibleProbeTime)
                {
                    _nextInvincibleProbeTime = Time.unscaledTime + 0.25f;
                    try { _cachedInvincible = GetIsInvincible(Target.data); }
                    catch { _cachedInvincible = false; }
                }
                if (!_hasShieldState || _lastShieldActive != _cachedInvincible)
                {
                    try { shield.SetActive(_cachedInvincible); } catch { }
                    _lastShieldActive = _cachedInvincible;
                    _hasShieldState = true;
                }
            }
            if (campfire != null)
            {
                try
                {
                    bool canHunger = Target.refs != null && Target.refs.afflictions != null
                        ? Target.refs.afflictions.canGetHungry
                        : true;
                    bool campfireActive = !canHunger;
                    if (!_hasCampfireState || _lastCampfireActive != campfireActive)
                    {
                        campfire.SetActive(campfireActive);
                        _lastCampfireActive = campfireActive;
                        _hasCampfireState = true;
                    }
                }
                catch { }
            }

            if (sinTime > TAU) sinTime -= TAU;

            // 死亡状态下 CharacterData 可能仍保留上一帧的体力/临时体力数值。
            // 保留灰暗的队友条，但清理数值文字，避免死亡过渡时旧值与 0/新值重叠。
            if (targetDead)
                HideValueTexts();
            else if (Time.unscaledTime >= _nextValueRefreshTime)
            {
                _nextValueRefreshTime = Time.unscaledTime + ValueRefreshInterval;
                UpdateValueTexts();
            }

            if (Time.unscaledTime >= _nextDiagnosticLogTime)
            {
                _nextDiagnosticLogTime = Time.unscaledTime + 2f;
                LogRuntimeDiagnostics(fullWidth, statusSum);
            }
        }

        private void LogRuntimeDiagnostics(float fullWidth, float statusSum)
        {
            try
            {
                CharacterAfflictions targetAfflictions = null;
                try
                {
                    targetAfflictions = Target != null && Target.refs != null
                        ? Target.refs.afflictions
                        : null;
                }
                catch { }

                var sb = new System.Text.StringBuilder(1536);
                sb.Append("[PI-DIAG][DriverState] driver=").Append(GetInstanceID())
                    .Append(" target=").Append(SafeGetName(Target))
                    .Append(" rootSelf=").Append(gameObject.activeSelf)
                    .Append(" rootHierarchy=").Append(gameObject.activeInHierarchy)
                    .Append(" enabled=").Append(enabled)
                    .Append(" dead=").Append(Target != null && Target.data != null && Target.data.dead)
                    .Append(" refsAff=").Append(targetAfflictions != null)
                    .Append(" statusArray=").Append(targetAfflictions != null && targetAfflictions.currentStatuses != null
                        ? targetAfflictions.currentStatuses.Length
                        : -1)
                    .Append(" statusSum=").Append(statusSum.ToString("F3"))
                    .Append(" currentStamina=").Append(Target != null && Target.data != null
                        ? Target.data.currentStamina.ToString("F3")
                        : "null")
                    .Append(" fullWidth=").Append(fullWidth.ToString("F1"))
                    .Append(" staminaWidth=").Append(staminaBar != null ? staminaBar.sizeDelta.x.ToString("F1") : "null")
                    .Append(" staminaSelf=").Append(staminaBar != null && staminaBar.gameObject.activeSelf)
                    .Append(" staminaHierarchy=").Append(staminaBar != null && staminaBar.gameObject.activeInHierarchy)
                    .Append(" staminaText=").Append(DescribeText(staminaValueText))
                    .Append(" affCount=").Append(afflictions != null ? afflictions.Length : -1)
                    .Append(" textCount=").Append(afflictionTexts != null ? afflictionTexts.Length : -1);

                if (afflictions != null)
                {
                    for (int i = 0; i < afflictions.Length; i++)
                    {
                        var affliction = afflictions[i];
                        sb.Append(" | aff[").Append(i).Append("]=");
                        if (affliction == null)
                        {
                            sb.Append("null");
                            continue;
                        }

                        float value = AfflictionValueHelper.GetValue(
                            Target,
                            affliction.afflictionType,
                            affliction.isPetrify);
                        sb.Append(affliction.afflictionType)
                            .Append(':').Append(value.ToString("F3"))
                            .Append(" width=").Append(affliction.width.ToString("F1"))
                            .Append(" size=").Append(affliction.size.ToString("F1"))
                            .Append(" self=").Append(affliction.gameObject.activeSelf)
                            .Append(" hierarchy=").Append(affliction.gameObject.activeInHierarchy)
                            .Append(" rtf=").Append(affliction.rtf != null)
                            .Append(" text=").Append(afflictionTexts != null && i < afflictionTexts.Length
                                ? DescribeText(afflictionTexts[i])
                                : "<missing>");
                    }
                }
                PluginLogger.Debug(sb.ToString());
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn(
                    "pi_diag_driver_" + GetInstanceID(),
                    "[PI-DIAG][DriverState] failed for driver=" + GetInstanceID() + ": " + ex,
                    2f);
            }
        }

        private static string DescribeText(TMP_Text text)
        {
            if (text == null) return "<null>";
            string content;
            try { content = string.IsNullOrEmpty(text.text) ? "<empty>" : text.text; }
            catch { content = "<invalid>"; }
            try
            {
                return content
                    + ",self=" + text.gameObject.activeSelf
                    + ",hierarchy=" + text.gameObject.activeInHierarchy
                    + ",enabled=" + text.enabled
                    + ",font=" + (text.font != null ? text.font.name : "null")
                    + ",size=" + text.fontSize.ToString("F1");
            }
            catch
            {
                return content + ",state=<invalid>";
            }
        }

        private void HideValueTexts()
        {
            if (staminaValueText != null)
            {
                if (staminaValueText.text != string.Empty) staminaValueText.text = string.Empty;
                if (staminaValueText.gameObject.activeSelf) staminaValueText.gameObject.SetActive(false);
            }
            if (extraValueText != null)
            {
                if (extraValueText.text != string.Empty) extraValueText.text = string.Empty;
                if (extraValueText.gameObject.activeSelf) extraValueText.gameObject.SetActive(false);
            }
            if (afflictionTexts == null) return;
            for (int i = 0; i < afflictionTexts.Length; i++)
            {
                var text = afflictionTexts[i];
                if (text == null) continue;
                if (text.text != string.Empty) text.text = string.Empty;
                if (text.gameObject.activeSelf) text.gameObject.SetActive(false);
            }
        }

        private static void SetWidthImmediate(RectTransform rect, float width)
        {
            if (rect == null) return;
            var delta = rect.sizeDelta;
            if (Mathf.Abs(delta.x - width) < 0.01f) return;
            delta.x = Mathf.Max(0f, width);
            rect.sizeDelta = delta;
        }

        private void SyncTargetVisualsImmediate()
        {
            try
            {
                if (Target == null || Target.Equals(null) || Target.data == null || fullBar == null)
                    return;

                float fullWidth = fullBar.sizeDelta.x;
                float maxStamina01 = Mathf.Max(0f, Target.GetMaxStamina());
                if (Target.data.dead)
                {
                    ResetInfiniteStaminaDisplay();
                    _displayedMainStamina01 = 0f;
                }
                else
                {
                    _displayedMainStamina01 = ResolveDisplayedMainStamina(
                        Target.data.currentStamina,
                        maxStamina01,
                        HasInfiniteStaminaEffect(Target));
                }
                desiredStaminaSize = Mathf.Max(0f, _displayedMainStamina01 * fullWidth + staminaBarOffset);
                desiredMaxStaminaSize = Mathf.Max(0f, maxStamina01 * fullWidth + staminaBarOffset);
                desiredExtraStaminaSize = Mathf.Max(0f, Target.data.extraStamina * fullWidth);
                cachedExtraStam = desiredExtraStaminaSize;
                SetWidthImmediate(staminaBar, desiredStaminaSize);
                SetWidthImmediate(maxStaminaBar, desiredMaxStaminaSize);

                if (afflictions != null)
                {
                    for (int i = 0; i < afflictions.Length; i++)
                        if (afflictions[i] != null)
                            afflictions[i].SyncVisualImmediate(Target, fullWidth, minAfflictionWidth);
                }

                if (staminaBarOutline != null)
                {
                    float statusSum = 1f;
                    try
                    {
                        if (Target.refs != null && Target.refs.afflictions != null)
                            statusSum = Target.refs.afflictions.statusSum;
                    }
                    catch { }
                    SetWidthImmediate(staminaBarOutline, 14f + Mathf.Max(1f, statusSum) * fullWidth);
                }

            }
            catch { }
        }

        private float ResolveDisplayedMainStamina(float rawStamina, float maxStamina, bool infiniteStamina)
        {
            float reasonableCurrent = Mathf.Clamp(rawStamina, 0f, maxStamina);
            if (infiniteStamina)
            {
                if (!_wasInfiniteStamina)
                {
                    _frozenInfiniteStamina01 = _hasReliableMainStamina
                        ? _lastReliableMainStamina01
                        : reasonableCurrent;
                    PluginLogger.Debug("[InfiniteStam] mate=" + SafeGetName(Target)
                        + " start raw=" + rawStamina.ToString("F3")
                        + " max=" + maxStamina.ToString("F3")
                        + " frozen=" + _frozenInfiniteStamina01.ToString("F3")
                        + " cached=" + _hasReliableMainStamina);
                }
                _wasInfiniteStamina = true;
                return _frozenInfiniteStamina01;
            }

            if (_wasInfiniteStamina)
            {
                PluginLogger.Debug("[InfiniteStam] mate=" + SafeGetName(Target)
                    + " end raw=" + rawStamina.ToString("F3"));
            }
            _wasInfiniteStamina = false;
            _frozenInfiniteStamina01 = 0f;
            _lastReliableMainStamina01 = reasonableCurrent;
            _hasReliableMainStamina = true;
            return reasonableCurrent;
        }

        private void ResetInfiniteStaminaDisplay()
        {
            _wasInfiniteStamina = false;
            _hasReliableMainStamina = false;
            _lastReliableMainStamina01 = 0f;
            _frozenInfiniteStamina01 = 0f;
            _displayedMainStamina01 = 0f;
        }

        private static bool HasInfiniteStaminaEffect(Character target)
        {
            try
            {
                if (target == null || target.Equals(null)) return false;
                if (target.infiniteStam) return true;
                if (target.refs == null || target.refs.afflictions == null) return false;
                Peak.Afflictions.Affliction ignored;
                return target.refs.afflictions.HasAfflictionType(
                    Peak.Afflictions.Affliction.AfflictionType.InfiniteStamina,
                    out ignored);
            }
            catch
            {
                return false;
            }
        }

        private static void SetActiveIfChanged(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        private void UpdateValueTexts()
        {
            if (Target == null || Target.data == null) return;

            bool round = true;
            try { round = PlayersInfoPlugin.CfgRoundStamina == null || PlayersInfoPlugin.CfgRoundStamina.Value; } catch { }

            // 直接用 normalized 数值 × 100，分辨率无关（避免 2K/4K 下 size/6 算出两倍数）
            float mainStam = _displayedMainStamina01 * 100f;
            float extraStam = Target.data.extraStamina * 100f;
            float extraCap = ExtraStaminaValueHelper.GetCap01(Target) * 100f;
            bool showExtraCap = true;
            try
            {
                showExtraCap = PlayersInfoPlugin.CfgShowExtraStaminaCap == null
                    || PlayersInfoPlugin.CfgShowExtraStaminaCap.Value;
            }
            catch { }

            if (staminaValueText != null)
            {
                float w = fullBar != null ? fullBar.sizeDelta.x : 0f;
                if (w < 15f)
                {
                    if (staminaValueText.gameObject.activeSelf) staminaValueText.gameObject.SetActive(false);
                }
                else
                {
                    bool useFloat = !(round || w < 30f);
                    if (useFloat)
                    {
                        int v10 = Mathf.RoundToInt(mainStam * 10f);
                        if (v10 != _lastStaminaShownTenth || !_lastStaminaWasFloat)
                        {
                            staminaValueText.text = (v10 / 10f).ToString("F1");
                            _lastStaminaShownTenth = v10;
                            _lastStaminaWasFloat = true;
                        }
                    }
                    else
                    {
                        int v = Mathf.RoundToInt(mainStam);
                        if (v != _lastStaminaShownInt || _lastStaminaWasFloat)
                        {
                            staminaValueText.text = v.ToString();
                            _lastStaminaShownInt = v;
                            _lastStaminaWasFloat = false;
                        }
                    }
                    if (!staminaValueText.gameObject.activeSelf) staminaValueText.gameObject.SetActive(true);
                }
            }

            if (extraValueText != null)
            {
                int v = Mathf.Clamp(Mathf.RoundToInt(extraStam), 0, 100);
                int cap = Mathf.Clamp(Mathf.RoundToInt(extraCap), 0, 100);
                if (v != _lastExtraShownInt || cap != _lastExtraCapShownInt
                    || showExtraCap != _lastExtraShownWithCap)
                {
                    extraValueText.text = showExtraCap
                        ? v.ToString() + "/" + cap.ToString()
                        : v.ToString();
                    _lastExtraShownInt = v;
                    _lastExtraCapShownInt = cap;
                    _lastExtraShownWithCap = showExtraCap;
                    float preferredWidth = extraValueText.GetPreferredValues(extraValueText.text).x + 6f;
                    var textSize = extraValueText.rectTransform.sizeDelta;
                    if (Mathf.Abs(textSize.x - preferredWidth) > 0.1f)
                    {
                        textSize.x = preferredWidth;
                        extraValueText.rectTransform.sizeDelta = textSize;
                    }
                }
                if (!extraValueText.gameObject.activeSelf) extraValueText.gameObject.SetActive(true);
                if (PluginLogger.DebugEnabled && Time.unscaledTime >= _nextExtraValLogTime)
                {
                    _nextExtraValLogTime = Time.unscaledTime + 3f;
                    try
                    {
                        float rawMain = Target.data.currentStamina;
                        float rawExtra = Target.data.extraStamina;
                        string nm = SafeGetName(Target);
                        PluginLogger.Debug("[ExtraVal] mate=" + nm
                            + " rawExtra=" + rawExtra.ToString("F4")
                            + " shown=" + extraValueText.text
                            + " rawMain=" + rawMain.ToString("F4"));
                    }
                    catch { }
                }
            }
        }

        private void OutOfStaminaPulse()
        {
            if (backing == null) return;
            try
            {
                backing.color = outOfStaminaBackingColor;
                backing.DOColor(defaultBackingColor, 0.5f);
            }
            catch { }
        }

        private static FieldInfo _fiIsInvincible;
        private static bool _fiResolved;
        private static bool GetIsInvincible(CharacterData data)
        {
            if (data == null) return false;
            if (!_fiResolved)
            {
                _fiResolved = true;
                try { _fiIsInvincible = typeof(CharacterData).GetField("isInvincible", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); } catch { }
            }
            if (_fiIsInvincible == null) return false;
            try { return (bool)_fiIsInvincible.GetValue(data); } catch { return false; }
        }

        private static string SafeGetName(Character c)
        {
            if (c == null) return string.Empty;
            try
            {
                if (c.photonView != null && c.photonView.Owner != null)
                {
                    string nick = c.photonView.Owner.NickName;
                    if (!string.IsNullOrEmpty(nick)) return nick;
                }
            }
            catch { }
            try { return string.IsNullOrEmpty(c.characterName) ? c.name : c.characterName; }
            catch { return "Player"; }
        }

    }
}
