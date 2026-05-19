using System;
using BepInEx.Logging;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 语言检测与双语辅助。三层检测：PlayerPrefs → LocalizedText → Application.systemLanguage
    /// </summary>
    internal static class LanguageHelper
    {
        private static bool _isChinese;
        public static bool IsChinese
        {
            get => _isChinese;
            set => _isChinese = value;
        }

        /// <summary>根据当前语言返回中文或英文文本。</summary>
        public static string L(string en, string zh) => _isChinese ? zh : en;

        /// <summary>
        /// 综合检测游戏语言。方法1/2 检测到确定值时直接返回，
        /// 方法3（系统语言）仅在前两者都无数据时作为兜底。
        /// </summary>
        public static bool DetectChineseLanguage()
        {
            ManualLogSource log = Plugin.Log;

            // Method 1: PlayerPrefs — 游戏自身存储的语言设置（最权威）
            try
            {
                bool hasKey = PlayerPrefs.HasKey("LanguageSetting");
                log?.LogInfo($"[DEBUG] Lang: PlayerPrefs.HasKey('LanguageSetting')={hasKey}");
                if (hasKey)
                {
                    int langVal = PlayerPrefs.GetInt("LanguageSetting", -1);
                    log?.LogInfo($"[DEBUG] Lang: PlayerPrefs.GetInt={langVal}");
                    if (langVal >= 0)
                    {
                        // 有效 int 值：9=Chinese，其他=非 Chinese → 直接确定
                        bool isCh = langVal == 9;
                        log?.LogInfo($"[DEBUG] Lang: → {(isCh ? "Chinese" : "non-Chinese")} (PlayerPrefs int={langVal})");
                        return isCh;
                    }

                    string langStr = PlayerPrefs.GetString("LanguageSetting", string.Empty);
                    log?.LogInfo($"[DEBUG] Lang: PlayerPrefs.GetString='{langStr}'");
                    if (!string.IsNullOrEmpty(langStr))
                    {
                        bool isCh = IsChineseLanguageName(langStr);
                        log?.LogInfo($"[DEBUG] Lang: → {(isCh ? "Chinese" : "non-Chinese")} (PlayerPrefs string='{langStr}')");
                        return isCh;
                    }
                }
            }
            catch (Exception ex) { log?.LogError($"[DEBUG] Lang: Exception (M1): {ex.Message}"); }

            // Method 2: LocalizedText.CURRENT_LANGUAGE — 通过 ReflectionCache 访问
            try
            {
                if (ReflectionCache.LocalizedTextLanguageField != null)
                {
                    object val = ReflectionCache.LocalizedTextLanguageField.GetValue(null);
                    string name = val?.ToString() ?? string.Empty;
                    log?.LogInfo($"[DEBUG] Lang: CURRENT_LANGUAGE='{name}' type={val?.GetType().Name}");
                    if (!string.IsNullOrEmpty(name))
                    {
                        bool isCh = IsChineseLanguageName(name);
                        log?.LogInfo($"[DEBUG] Lang: → {(isCh ? "Chinese" : "non-Chinese")} (LocalizedText='{name}')");
                        return isCh;
                    }
                }
            }
            catch (Exception ex) { log?.LogError($"[DEBUG] Lang: Exception (M2): {ex.Message}"); }

            // Method 3: System language — 仅当游戏语言无法确定时才用操作系统语言兜底
            try
            {
                SystemLanguage sysLang = Application.systemLanguage;
                log?.LogInfo($"[DEBUG] Lang: Application.systemLanguage={sysLang} (fallback)");
                if (sysLang == SystemLanguage.Chinese
                    || sysLang == SystemLanguage.ChineseSimplified
                    || sysLang == SystemLanguage.ChineseTraditional)
                {
                    log?.LogInfo("[DEBUG] Lang: → Chinese (systemLanguage fallback)");
                    return true;
                }
            }
            catch (Exception ex) { log?.LogError($"[DEBUG] Lang: Exception (M3): {ex.Message}"); }

            log?.LogInfo("[DEBUG] Lang: no method detected Chinese, defaulting to English");
            return false;
        }

        private static bool IsChineseLanguageName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name.IndexOf("SimplifiedChinese", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("TraditionalChinese", StringComparison.OrdinalIgnoreCase) >= 0
                || name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("中文", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
