using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 夜晚开始时检测两种无寒冷的情况并发出一次性警告：
    ///   1. ShootZombies 的 NightColdEnabled=false → 夜间寒冷被 MOD 关闭
    ///   2. 当前天阶 < 5 → 游戏原生不产生夜间寒冷
    /// 仅在白→夜过渡时检测并显示一次，不频繁提醒。
    /// </summary>
    internal static class NightColdWarning
    {
        // ── 状态 ──
        private static bool _wasDaytime = true;   // 上帧是否白天
        private static float _showTimer;           // 当前通知显示倒计时
        private static bool _shownThisNight;       // 本夜已提醒过

        // ── UI ──
        private static GameObject _noticeObj;
        private static TextMeshProUGUI _noticeText;

        // ── 常量 ──
        private const float ShowDuration = 15f;    // 警告显示 15 秒

        /// <summary>每帧由 Plugin.Update 调用。</summary>
        public static void Tick()
        {
            bool isDaytime = DayNightTracker.IsDaytime;

            // ── 正在显示 → 倒计时后隐藏 ──
            if (_showTimer > 0f)
            {
                _showTimer -= Time.deltaTime;
                if (_showTimer <= 0f)
                    HideNotice();
            }

            // ── 白→夜 过渡：检测并提醒 ──
            if (_wasDaytime && !isDaytime)
            {
                _shownThisNight = false; // 新一夜重置
                TryShowWarning();
            }

            // ── 夜→白 过渡：重置标记 ──
            if (!_wasDaytime && isDaytime)
            {
                _shownThisNight = false;
                HideNotice();
            }

            _wasDaytime = isDaytime;
        }

        private static void TryShowWarning()
        {
            if (_shownThisNight) return;

            bool szOff = IsShootZombiesNightColdOff();
            bool lowAscent = Ascents.currentAscent < 5 && !IsCustomRunColdNightOn();

            if (!szOff && !lowAscent) return;

            _shownThisNight = true;
            ShowNotice(szOff, lowAscent);
            _showTimer = ShowDuration;

            Plugin.Log?.LogInfo($"[DEBUG] [NightColdWarning] Warning shown: SZ_NightColdOff={szOff}, LowAscent={lowAscent}(ascent={Ascents.currentAscent})");
        }

        // ─────────────────────────────────────────────────────────
        // 条件检测
        // ─────────────────────────────────────────────────────────

        /// <summary>NightCold 配置（SZ 1.2.x 或 FogClimb）是否被关闭。</summary>
        private static bool IsShootZombiesNightColdOff()
        {
            // 不在乎来自 SZ 还是 FogClimb，反射拿不到就跳过
            if (!ModIntegration.IsNightColdReflectionReady) return false;
            return !ModIntegration.IsSzNightColdEnabled();
        }

        /// <summary>自定义难度是否手动开启了 ColdNight。</summary>
        private static bool IsCustomRunColdNightOn()
        {
            try
            {
                if (!RunSettings.IsCustomRun) return false;
                return RunSettings.GetValue(RunSettings.SETTINGTYPE.ColdNight, false) == 1;
            }
            catch { return false; }
        }

        // ─────────────────────────────────────────────────────────
        // 屏幕通知
        // ─────────────────────────────────────────────────────────

        private static void ShowNotice(bool szOff, bool lowAscent)
        {
            HideNotice();

            var guiMgr = GUIManager.instance;
            if (guiMgr == null || guiMgr.hudCanvas == null) return;

            _noticeObj = new GameObject("NightColdWarning");
            _noticeObj.transform.SetParent(guiMgr.hudCanvas.transform, false);

            RectTransform rt = _noticeObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -60f);
            rt.sizeDelta = new Vector2(800f, 100f);

            // 背景半透明黑底
            var bg = new GameObject("BG");
            bg.transform.SetParent(_noticeObj.transform, false);
            RectTransform bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(-10f, -5f);
            bgRt.offsetMax = new Vector2(10f, 5f);
            var bgImg = bg.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.7f);
            bgImg.raycastTarget = false;

            _noticeText = _noticeObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = ResolveFont();
            if (font != null) _noticeText.font = font;
            _noticeText.fontSize = 20f;
            _noticeText.color = new Color(1f, 0.35f, 0.3f, 1f);
            _noticeText.alignment = TextAlignmentOptions.Center;
            _noticeText.textWrappingMode = TextWrappingModes.Normal;
            _noticeText.overflowMode = TextOverflowModes.Overflow;
            _noticeText.raycastTarget = false;

            _noticeText.text = BuildMessage(szOff, lowAscent);
        }

        private static string BuildMessage(bool szOff, bool lowAscent)
        {
            bool zh = LanguageHelper.IsChinese;
            string msg = "";

            if (szOff)
            {
                msg += zh
                    ? "⚠ 「夜晚寒冷 / Night Cold」已关闭，灯笼抗寒无效\n→ 请在 ModConfig → Features 中开启（Thanks-FogAndColdControl 或 ShootZombies）"
                    : "⚠ 'Night Cold' is OFF — cold resistance disabled\n→ Enable in ModConfig → Features (Thanks-FogAndColdControl or ShootZombies)";
            }

            if (lowAscent)
            {
                if (msg.Length > 0) msg += "\n";
                int asc = Ascents.currentAscent;
                msg += zh
                    ? $"⚠ 当前天阶 {asc}（需天阶5+）无夜间寒冷，抗寒功能不生效"
                    : $"⚠ Ascent {asc} has no night cold (requires 5+), cold resistance inactive";
            }

            return msg;
        }

        private static void HideNotice()
        {
            if (_noticeObj != null)
            {
                UnityEngine.Object.Destroy(_noticeObj);
                _noticeObj = null;
                _noticeText = null;
            }
        }

        private static TMP_FontAsset ResolveFont()
        {
            var pcl = UnityEngine.Object.FindAnyObjectByType<PlayerConnectionLog>();
            if (pcl != null && pcl.text != null)
                return pcl.text.font;

            var gui = GUIManager.instance;
            if (gui != null && gui.interactNameText != null)
                return gui.interactNameText.font;

            return null;
        }
    }
}
