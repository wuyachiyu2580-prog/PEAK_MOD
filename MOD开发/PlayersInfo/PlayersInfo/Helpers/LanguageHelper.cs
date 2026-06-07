using System;
using BepInEx.Logging;
using UnityEngine;

namespace PlayersInfo.Helpers
{
    /// <summary>
    /// 语言检测与双语辅助。PlayerPrefs('LanguageSetting') 优先，否则 Application.systemLanguage 兜底。
    /// 9 = Chinese（PEAK 内部语言枚举）。
    /// </summary>
    internal static class LanguageHelper
    {
        public static bool IsChinese { get; set; }

        public static string L(string en, string zh) => IsChinese ? zh : en;

        public static bool DetectChineseLanguage()
        {
            ManualLogSource log = PluginLogger.Log;

            try
            {
                if (PlayerPrefs.HasKey("LanguageSetting"))
                {
                    int langVal = PlayerPrefs.GetInt("LanguageSetting", -1);
                    if (langVal >= 0)
                    {
                        bool isCh = langVal == 9;
                        log?.LogInfo($"[PlayersInfo] Lang: PlayerPrefs int={langVal} → {(isCh ? "Chinese" : "non-Chinese")}");
                        return isCh;
                    }
                    string langStr = PlayerPrefs.GetString("LanguageSetting", string.Empty);
                    if (!string.IsNullOrEmpty(langStr))
                    {
                        bool isCh = IsChineseLanguageName(langStr);
                        log?.LogInfo($"[PlayersInfo] Lang: PlayerPrefs string='{langStr}' → {(isCh ? "Chinese" : "non-Chinese")}");
                        return isCh;
                    }
                }
            }
            catch (Exception ex) { log?.LogError($"[PlayersInfo] Lang detect (prefs) failed: {ex.Message}"); }

            try
            {
                SystemLanguage sys = Application.systemLanguage;
                if (sys == SystemLanguage.Chinese
                    || sys == SystemLanguage.ChineseSimplified
                    || sys == SystemLanguage.ChineseTraditional)
                {
                    log?.LogInfo("[PlayersInfo] Lang: systemLanguage fallback → Chinese");
                    return true;
                }
            }
            catch (Exception ex) { log?.LogError($"[PlayersInfo] Lang detect (sys) failed: {ex.Message}"); }

            return false;
        }

        private static bool IsChineseLanguageName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0
                || name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf("中文", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
