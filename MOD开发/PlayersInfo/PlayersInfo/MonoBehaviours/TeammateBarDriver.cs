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
    /// - 保留原版所有 UI 引用（fullBar/staminaBar/extraBar/extraStaminaGlow ...）
    /// - Update 完整复制 StaminaBar.Update 但把 observedCharacter 换成 Target
    /// 这样既完整复用原版视觉（含 extraBar 临时体力泡泡+光晕），又能指向任意队友。
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
        public BarAffliction[] afflictions;
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

        // 临时体力克隆方案已废弃，改为 fullBar 左端外侧文本显示。
        // [ExtraVal] 量纲诊断日志的节流计时。
        private float _nextExtraValLogTime;

        // 数值文本脏检查：避免每帧 ToString + TMP mesh 重建产生 GC。
        // _lastStamWasFloat 用于在 round/F1 两种模式之间切换时强制刷一次。
        private int _lastStaminaShownInt = int.MinValue;
        private int _lastStaminaShownTenth = int.MinValue;   // F1 模式下缓存 value*10 取整
        private bool _lastStaminaWasFloat;
        private int _lastExtraShownInt = int.MinValue;

        // 反射结果缓存：CharacterData.isInvincible 每帧反射 GetValue 会装箱产生 GC，
        // shield 显隐对延迟不敏感，0.25s 探一次足够。
        private bool _cachedInvincible;
        private float _nextInvincibleProbeTime;

        /// <summary>切换到新目标时重置内部平滑量，避免从上一个玩家的尺寸插值过去。</summary>
        public void BindTarget(Character c)
        {
            Target = c;
            desiredStaminaSize = 0f;
            desiredMaxStaminaSize = 0f;
            desiredExtraStaminaSize = 0f;
            cachedExtraStam = 0f;
            outOfStamina = false;
            // 切换目标后立即重新探一次反射 + 强刷一次数值文本，避免显示上一个玩家的状态
            _cachedInvincible = false;
            _nextInvincibleProbeTime = 0f;
            _lastStaminaShownInt = int.MinValue;
            _lastStaminaShownTenth = int.MinValue;
            _lastStaminaWasFloat = false;
            _lastExtraShownInt = int.MinValue;
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
                    a.size = 0f;
                    try { a.width = 0f; } catch { }
                    if (a.gameObject != null) a.gameObject.SetActive(false);
                }
            }
            // 新目标当前的满幅值预算，便于有 extra 时立即同步，不从 0 lerp
            try
            {
                if (c != null && c.data != null && fullBar != null)
                {
                    float fw = fullBar.sizeDelta.x;
                    desiredStaminaSize = Mathf.Max(0f, c.data.currentStamina * fw + staminaBarOffset);
                    desiredExtraStaminaSize = Mathf.Max(0f, c.data.extraStamina * fw);
                    // 如果克隆 bar 的 extraBar 当前还在显示，把 cachedExtraStam 直接置为新值，避免从 0 爬起
                    if (extraBar != null && extraBar.gameObject.activeSelf)
                        cachedExtraStam = desiredExtraStaminaSize;
                }
            }
            catch { }
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
        }

        private void Update()
        {
            try { DoUpdate(); }
            catch (Exception ex) { PluginLogger.ThrottleError("bar_driver", "TeammateBarDriver Update failed: " + ex.Message); }
        }

        private void DoUpdate()
        {
            if (Target == null || Target.Equals(null) || Target.data == null) return;
            if (fullBar == null || staminaBar == null) return;

            // 名字 + 颜色低频刷新（避免每帧 alloc）
            if (nameLabel != null && Target.refs != null && Target.refs.customization != null)
            {
                var color = Target.refs.customization.PlayerColor;
                if (nameLabel.color != color) nameLabel.color = color;
            }

            float fullWidth = fullBar.sizeDelta.x;

            // === Main Stamina ===
            desiredStaminaSize = Mathf.Max(0f, Target.data.currentStamina * fullWidth + staminaBarOffset);
            if (Target.data.currentStamina <= 0.005f)
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
            try
            {
                desiredMaxStaminaSize = Mathf.Max(0f, Target.GetMaxStamina() * fullWidth + staminaBarOffset);
            }
            catch { desiredMaxStaminaSize = fullWidth; }
            if (maxStaminaBar != null)
            {
                var m = maxStaminaBar.sizeDelta;
                m.x = Mathf.Lerp(m.x, desiredMaxStaminaSize, dt10);
                maxStaminaBar.sizeDelta = m;
                maxStaminaBar.gameObject.SetActive(m.x > minStaminaBarWidth);
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
                o.x = 14f + Mathf.Max(1f, statusSum) * fullWidth;
                staminaBarOutline.sizeDelta = o;
            }
            if (staminaBarOutlineOverflowBar != null)
            {
                staminaBarOutlineOverflowBar.gameObject.SetActive(statusSum > 1.005);
            }

            staminaBar.gameObject.SetActive(staminaBar.sizeDelta.x > minStaminaBarWidth);

            // === Afflictions 数值条（原版 BarAffliction） ===
            if (afflictions != null && Target.refs != null && Target.refs.afflictions != null)
            {
                var ca = Target.refs.afflictions;
                for (int i = 0; i < afflictions.Length; i++)
                {
                    var a = afflictions[i];
                    if (a == null) continue;
                    float s = 0f;
                    try { s = ca.GetCurrentStatus(a.afflictionType); } catch { }
                    float target = fullWidth * s;
                    if (s > 0.01f)
                    {
                        if (target < minAfflictionWidth) target = minAfflictionWidth;
                        a.size = target;
                        if (!a.gameObject.activeSelf) a.gameObject.SetActive(true);
                    }
                    else
                    {
                        a.size = 0f;
                        if (a.gameObject.activeSelf) a.gameObject.SetActive(false);
                    }
                    // 复刻 BarAffliction.UpdateAffliction 的 lerp（它只用自己的 width/size，可安全调用）
                    try { a.width = Mathf.Lerp(a.width, a.size, Mathf.Min(Time.deltaTime * 10f, 0.1f)); } catch { }

                    // 数值文本：百分比+消除时间（≥1% 才展示；太窄就隐藏避免溢出）
                    if (afflictionTexts != null && i < afflictionTexts.Length)
                    {
                        var txt = afflictionTexts[i];
                        if (txt != null)
                        {
                            bool showTxt = s > 0.01f && a.width > 18f;
                            if (showTxt)
                            {
                                int pct = Mathf.Clamp(Mathf.RoundToInt(s * 100f), 0, 999);
                                string tm = (a.width > 60f)
                                    ? AfflictionTimeHelper.FormatTime(AfflictionTimeHelper.GetReductionTimeRemaining(ca, a.afflictionType))
                                    : string.Empty;
                                txt.text = string.IsNullOrEmpty(tm) ? pct.ToString() : (pct + "(" + tm + ")");
                                if (!txt.gameObject.activeSelf) txt.gameObject.SetActive(true);
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
            // 已放弃 extraBar 克隆：数值显示在 fullBar 左端外（UpdateValueTexts 里按 extraStamina 判显隐）。
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
                try { shield.SetActive(_cachedInvincible); } catch { }
            }
            if (campfire != null)
            {
                try
                {
                    bool canHunger = Target.refs != null && Target.refs.afflictions != null
                        ? Target.refs.afflictions.canGetHungry
                        : true;
                    campfire.SetActive(!canHunger);
                }
                catch { }
            }

            if (sinTime > TAU) sinTime -= TAU;

            // === 数值文本（仿 StaminaInfo） ===
            UpdateValueTexts();
        }

        private void UpdateValueTexts()
        {
            if (Target == null || Target.data == null) return;

            bool round = true;
            try { round = PlayersInfoPlugin.CfgRoundStamina == null || PlayersInfoPlugin.CfgRoundStamina.Value; } catch { }

            // 直接用 normalized 数值 × 100，分辨率无关（避免 2K/4K 下 size/6 算出两倍数）
            float mainStam = Target.data.currentStamina * 100f;
            float extraStam = Target.data.extraStamina * 100f;

            if (staminaValueText != null)
            {
                // staminaBar 渲染宽度 太窄就隐藏，避免文字溢出
                float w = staminaBar != null ? staminaBar.sizeDelta.x : 0f;
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
                // 临时体力数字：按 normalized 值判显隐，不再依赖克隆 extraBar。
                // 阈值 0.5 避免因浮点残留显 0 这种尴尬表现。
                if (extraStam < 0.5f)
                {
                    if (extraValueText.gameObject.activeSelf) extraValueText.gameObject.SetActive(false);
                }
                else
                {
                    int v = Mathf.RoundToInt(extraStam);
                    if (v != _lastExtraShownInt)
                    {
                        extraValueText.text = "+" + v.ToString();
                        _lastExtraShownInt = v;
                    }
                    if (!extraValueText.gameObject.activeSelf) extraValueText.gameObject.SetActive(true);
                    // 量纲诊断：打出真实 raw 值 vs 显示数字，方便骨石问题是代码算错还是道具描述差倽
                    if (PluginLogger.DebugEnabled && Time.unscaledTime >= _nextExtraValLogTime)
                    {
                        _nextExtraValLogTime = Time.unscaledTime + 3f;
                        try
                        {
                            float rawMain = Target.data.currentStamina;
                            float rawExtra = Target.data.extraStamina;
                            string nm = SafeGetName(Target);
                            PluginLogger.Debug("[ExtraVal] mate=" + nm + " rawExtra=" + rawExtra.ToString("F4") + " shown=+" + Mathf.Round(rawExtra * 100f) + " rawMain=" + rawMain.ToString("F4"));
                        }
                        catch { }
                    }
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
