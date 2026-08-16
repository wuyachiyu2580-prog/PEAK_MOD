using System;
using HarmonyLib;
using PlayersInfo.Helpers;
using TMPro;
using UnityEngine;

namespace PlayersInfo.Patches
{
    /// <summary>
    /// 给本地玩家原版 StaminaBar（GUIManager.instance.bar）叠加数值文本：
    ///   - 主体力值
    ///   - 临时体力值
    ///   - 每个异常状态的百分比（与克隆的队友条显示风格保持一致）
    ///
    /// 做法参考 com.github.chuxiaaaa.StaminaInfo：
    /// Postfix StaminaBar.Update，仅处理本地玩家那一条（GUIManager.instance.bar）。
    /// 克隆的队友条上的原版 StaminaBar 组件已被 Destroy，不会进这里。
    /// </summary>
    [HarmonyPatch(typeof(StaminaBar), "Update")]
    internal static class LocalStaminaBarPatch
    {
        private static TMP_Text _staminaValueText;
        private static TMP_Text _extraValueText;
        private static TMP_Text _hungerCountdownText;
        private static TMP_Text[] _afflictionTexts;
        private static StaminaBar _initedFor;
        private static float _nextValueRefreshTime;
        private static float _nextAfflictionRefreshTime;
        private static float _nextHungerCalculationTime;
        private static string _cachedHungerTime = string.Empty;
        private const float ValueRefreshInterval = 0.15f;
        private const float AfflictionRefreshInterval = 0.5f;

        [HarmonyPostfix]
        private static void Postfix(StaminaBar __instance)
        {
            try
            {
                if (__instance == null) return;
                if (GUIManager.instance == null || GUIManager.instance.bar == null) return;
                // 只处理本地玩家自己这条原版 bar
                if (!object.ReferenceEquals(__instance, GUIManager.instance.bar)) return;

                // 主开关
                if (PlayersInfoPlugin.CfgModEnabled != null && !PlayersInfoPlugin.CfgModEnabled.Value)
                {
                    HideAll();
                    return;
                }

                // StaminaBar 实例换了（换关卡/重建 HUD）→ 重置
                if (!object.ReferenceEquals(_initedFor, __instance))
                {
                    ResetForSceneReload();
                }

                EnsureInit(__instance);

                bool showValue = PlayersInfoPlugin.CfgShowStaminaValue == null
                                 || PlayersInfoPlugin.CfgShowStaminaValue.Value;

                // 原版 StaminaBar 使用 observedCharacter；文字也必须读取同一角色。
                // sizeDelta.x 只用来判断宽度足够不足显示，避免文字溢出
                Character displayCharacter = DisplayCharacterHelper.GetObservedOrLocal();
                float mainStam01 = 0f, extraStam01 = 0f;
                try
                {
                    if (displayCharacter != null && displayCharacter.data != null)
                    {
                        mainStam01 = displayCharacter.data.currentStamina;
                        extraStam01 = displayCharacter.data.extraStamina;
                    }
                }
                catch { }

                bool displayDead = displayCharacter != null
                                 && displayCharacter.data != null
                                 && displayCharacter.data.dead;
                bool refreshValues = Time.unscaledTime >= _nextValueRefreshTime;
                if (refreshValues)
                    _nextValueRefreshTime = Time.unscaledTime + ValueRefreshInterval;

                // 主体力
                if (_staminaValueText != null)
                {
                    if (refreshValues && showValue && displayCharacter != null && !displayDead && __instance.staminaBar != null)
                        UpdateValueText(_staminaValueText, mainStam01 * 100f, __instance.staminaBar.sizeDelta.x);
                    else if (!showValue || displayCharacter == null || displayDead)
                        SetActive(_staminaValueText, false);
                }

                if (refreshValues || displayDead || displayCharacter == null)
                    UpdateHungerCountdown(__instance, displayCharacter, displayDead);

                // 临时体力
                if (_extraValueText != null)
                {
                    bool extraActive = __instance.extraBar != null && __instance.extraBar.gameObject.activeSelf;
                    if (refreshValues && showValue && displayCharacter != null && !displayDead && extraActive && __instance.extraBarStamina != null)
                    {
                        float extraCap01 = ExtraStaminaValueHelper.GetCap01(displayCharacter);
                        UpdateExtraValueText(_extraValueText, extraStam01 * 100f, extraCap01 * 100f,
                            __instance.extraBarStamina.sizeDelta.x);
                    }
                    else if (!showValue || displayCharacter == null || displayDead || !extraActive)
                        SetActive(_extraValueText, false);
                }

                // 异常百分比
                bool refreshAfflictions = Time.unscaledTime >= _nextAfflictionRefreshTime;
                if (refreshAfflictions)
                    _nextAfflictionRefreshTime = Time.unscaledTime + AfflictionRefreshInterval;
                if (refreshAfflictions && __instance.afflictions != null && _afflictionTexts != null)
                {
                    Character displayStatusCharacter = displayCharacter;
                    CharacterAfflictions ca = null;
                    try
                    {
                        if (displayStatusCharacter != null && displayStatusCharacter.refs != null)
                            ca = displayStatusCharacter.refs.afflictions;
                    }
                    catch { }

                    int n = Mathf.Min(__instance.afflictions.Length, _afflictionTexts.Length);
                    for (int i = 0; i < n; i++)
                    {
                        var a = __instance.afflictions[i];
                        var txt = _afflictionTexts[i];
                        if (a == null || txt == null) continue;

                        float s = AfflictionValueHelper.GetValue(displayStatusCharacter, a);

                        bool show = showValue && displayStatusCharacter != null && !displayDead
                                  && s > 0.01f && a.width > 18f;
                        if (show)
                        {
                            int pct = Mathf.Clamp(Mathf.RoundToInt(s * 100f), 0, 999);
                            string percentText = pct.ToString();
                            string tm = AfflictionTimeHelper.FormatTime(
                                AfflictionTimeHelper.GetReductionTimeRemaining(ca, a.afflictionType));
                            string candidate = string.IsNullOrEmpty(tm)
                                ? percentText
                                : percentText + "(" + tm + ")";
                            if (txt.GetPreferredValues(candidate).x > a.width)
                                candidate = percentText;
                            if (txt.GetPreferredValues(candidate).x <= a.width)
                            {
                                if (txt.text != candidate) txt.text = candidate;
                                SetActive(txt, true);
                            }
                            else
                            {
                                SetActive(txt, false);
                            }
                        }
                        else
                        {
                            SetActive(txt, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleError("local_bar_patch", "LocalStaminaBarPatch failed: " + ex.Message);
            }
        }

        /// <summary>场景切换时调用：引用会随 HUD 被销毁，下次 Postfix 再按新实例重建。</summary>
        public static void ResetForSceneReload()
        {
            _staminaValueText = null;
            _extraValueText = null;
            _hungerCountdownText = null;
            _afflictionTexts = null;
            _initedFor = null;
            _nextValueRefreshTime = 0f;
            _nextAfflictionRefreshTime = 0f;
            _nextHungerCalculationTime = 0f;
            _cachedHungerTime = string.Empty;
        }

        private static void EnsureInit(StaminaBar bar)
        {
            if (object.ReferenceEquals(_initedFor, bar) && _staminaValueText != null) return;
            try
            {
                if (bar.staminaBar != null && _staminaValueText == null)
                    _staminaValueText = AddStretchText(bar.staminaBar.gameObject, "PI_LocalStaminaValue", 20f, false);

                if (bar.extraBarStamina != null && _extraValueText == null)
                    _extraValueText = AddStretchText(bar.extraBarStamina.gameObject, "PI_LocalExtraStaminaValue", 20f, false);

                if (bar.fullBar != null && _hungerCountdownText == null)
                    _hungerCountdownText = AddFloatingText(bar.fullBar.gameObject, "PI_LocalHungerCountdown", 14f);

                if (bar.afflictions != null && _afflictionTexts == null)
                {
                    _afflictionTexts = new TMP_Text[bar.afflictions.Length];
                    for (int i = 0; i < bar.afflictions.Length; i++)
                    {
                        var a = bar.afflictions[i];
                        if (a == null) continue;
                        _afflictionTexts[i] = AddStretchText(a.gameObject, "PI_LocalAffPct_" + i, 16f, true);
                    }
                }

                _initedFor = bar;
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("local_bar_init", "LocalStaminaBarPatch.EnsureInit failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 显示体力数值：value01 是 0~1 normalized 体力，乘 100 得游戏数字；
        /// widthPx 是条子当前渲染宽度，宽度太小时隐藏避免文字溢出。
        /// 直接用 normalized×100可无视分辨率（2K / 4K）UI 缩放。
        /// </summary>
        private static void UpdateValueText(TMP_Text txt, float value01Mul100, float widthPx)
        {
            bool round = true;
            try { round = PlayersInfoPlugin.CfgRoundStamina == null || PlayersInfoPlugin.CfgRoundStamina.Value; } catch { }

            // 宽度太小就隐藏（没地方画文字）
            if (widthPx < 15f)
            {
                SetActive(txt, false);
                return;
            }

            if (round || widthPx < 30f)
                txt.text = Mathf.Round(value01Mul100).ToString();
            else
                txt.text = value01Mul100.ToString("F1");
            SetActive(txt, true);
        }

        private static void UpdateExtraValueText(TMP_Text txt, float currentPercent, float capPercent, float widthPx)
        {
            if (widthPx < 15f)
            {
                SetActive(txt, false);
                return;
            }

            int current = Mathf.Clamp(Mathf.RoundToInt(currentPercent), 0, 100);
            int cap = Mathf.Clamp(Mathf.RoundToInt(capPercent), 0, 100);
            txt.text = "+" + current.ToString() + "/" + cap.ToString();
            SetActive(txt, true);
        }

        private static void UpdateHungerCountdown(StaminaBar bar, Character displayCharacter, bool displayDead)
        {
            if (_hungerCountdownText == null || _staminaValueText == null || bar == null
                || displayCharacter == null || displayDead
                || !DisplayCharacterHelper.IsLocalDisplay(displayCharacter))
            {
                SetActive(_hungerCountdownText, false);
                return;
            }

            if (Time.unscaledTime >= _nextHungerCalculationTime)
            {
                _nextHungerCalculationTime = Time.unscaledTime + AfflictionRefreshInterval;
                float seconds = AfflictionTimeHelper.GetHungerTickTimeRemaining(displayCharacter);
                _cachedHungerTime = seconds > 0f ? AfflictionTimeHelper.FormatTime(seconds) : string.Empty;
            }

            if (string.IsNullOrEmpty(_cachedHungerTime) || !_staminaValueText.gameObject.activeSelf)
            {
                SetActive(_hungerCountdownText, false);
                return;
            }

            string suffix = " (" + _cachedHungerTime + ")";
            float width = bar.staminaBar != null ? bar.staminaBar.sizeDelta.x : 0f;
            float preferred = _staminaValueText.GetPreferredValues(_staminaValueText.text + suffix).x;
            if (preferred <= width)
            {
                string combined = _staminaValueText.text + suffix;
                if (_staminaValueText.text != combined) _staminaValueText.text = combined;
                SetActive(_hungerCountdownText, false);
            }
            else
            {
                string floating = "(" + _cachedHungerTime + ")";
                if (_hungerCountdownText.text != floating) _hungerCountdownText.text = floating;
                float floatingWidth = _hungerCountdownText.GetPreferredValues(floating).x + 8f;
                var rt = _hungerCountdownText.rectTransform;
                var size = rt.sizeDelta;
                if (Mathf.Abs(size.x - floatingWidth) > 0.1f)
                {
                    size.x = floatingWidth;
                    rt.sizeDelta = size;
                }
                SetActive(_hungerCountdownText, true);
            }
        }

        private static void SetActive(TMP_Text txt, bool active)
        {
            if (txt == null) return;
            if (txt.gameObject != null && txt.gameObject.activeSelf != active)
                txt.gameObject.SetActive(active);
        }

        private static void HideAll()
        {
            SetActive(_staminaValueText, false);
            SetActive(_extraValueText, false);
            SetActive(_hungerCountdownText, false);
            if (_afflictionTexts != null)
            {
                for (int i = 0; i < _afflictionTexts.Length; i++) SetActive(_afflictionTexts[i], false);
            }
        }

        /// <summary>在 host 上叠一个 stretch 全覆盖的居中 TMP_Text（透明背景，仅文字）。</summary>
        private static TMP_Text AddStretchText(GameObject host, string name, float fontSize, bool bold)
        {
            try
            {
                if (host == null) return null;
                // TMP 初始化需要 host 处于 active 状态
                if (!host.activeSelf) host.SetActive(true);

                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(host.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.font = FontHelper.GetChineseCapable();
                tmp.fontSize = fontSize;
                // 统一不用 Bold，和队友条对齐。
                tmp.fontStyle = FontStyles.Normal;
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
                PluginLogger.ThrottleWarn("local_bar_text", "LocalStaminaBarPatch.AddStretchText failed: " + ex.Message);
                return null;
            }
        }

        private static TMP_Text AddFloatingText(GameObject host, string name, float fontSize)
        {
            var text = AddStretchText(host, name, fontSize, false);
            if (text == null) return null;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 20f);
            rt.anchoredPosition = new Vector2(0f, -20f);
            text.color = new Color(1f, 0.9f, 0.25f, 1f);
            return text;
        }

    }
}
