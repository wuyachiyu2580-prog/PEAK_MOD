using System;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace PeakMapBrowser
{
    internal static class PeakMapLanguage
    {
        public static string Resolve(string mode, ManualLogSource log)
        {
            string normalized = NormalizeMode(mode);
            if (normalized == "zh" || normalized == "en")
            {
                return normalized;
            }

            string localeCode;
            if (TryGetUnityLocalizationCode(out localeCode))
            {
                return IsChineseCode(localeCode) ? "zh" : "en";
            }

            return IsChineseSystemLanguage(Application.systemLanguage) ? "zh" : "en";
        }

        public static string NormalizeMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return "auto";
            }

            string value = mode.Trim().ToLowerInvariant();
            if (value == "zh" || value == "cn" || value == "zh-cn" || value == "chinese")
            {
                return "zh";
            }
            if (value == "en" || value == "en-us" || value == "english")
            {
                return "en";
            }

            return "auto";
        }

        private static bool TryGetUnityLocalizationCode(out string code)
        {
            code = null;

            try
            {
                Type settingsType = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization");
                if (settingsType == null)
                {
                    return false;
                }

                PropertyInfo selectedLocaleProperty = settingsType.GetProperty("SelectedLocale", BindingFlags.Public | BindingFlags.Static);
                object selectedLocale = selectedLocaleProperty != null ? selectedLocaleProperty.GetValue(null, null) : null;
                if (selectedLocale == null)
                {
                    return false;
                }

                PropertyInfo identifierProperty = selectedLocale.GetType().GetProperty("Identifier", BindingFlags.Public | BindingFlags.Instance);
                object identifier = identifierProperty != null ? identifierProperty.GetValue(selectedLocale, null) : null;
                if (identifier != null)
                {
                    PropertyInfo codeProperty = identifier.GetType().GetProperty("Code", BindingFlags.Public | BindingFlags.Instance);
                    object codeValue = codeProperty != null ? codeProperty.GetValue(identifier, null) : null;
                    if (codeValue != null && !string.IsNullOrWhiteSpace(codeValue.ToString()))
                    {
                        code = codeValue.ToString();
                        return true;
                    }
                }

                PropertyInfo localeNameProperty = selectedLocale.GetType().GetProperty("LocaleName", BindingFlags.Public | BindingFlags.Instance);
                object localeName = localeNameProperty != null ? localeNameProperty.GetValue(selectedLocale, null) : null;
                if (localeName != null && !string.IsNullOrWhiteSpace(localeName.ToString()))
                {
                    code = localeName.ToString();
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool IsChineseCode(string code)
        {
            return !string.IsNullOrWhiteSpace(code)
                && code.Trim().StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsChineseSystemLanguage(SystemLanguage language)
        {
            return language == SystemLanguage.Chinese
                || language == SystemLanguage.ChineseSimplified
                || language == SystemLanguage.ChineseTraditional;
        }
    }
}
