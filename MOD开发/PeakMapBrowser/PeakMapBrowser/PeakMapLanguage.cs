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
            return ResolveDetailed(mode, log).Language;
        }

        public static PeakMapLanguageResult ResolveDetailed(string mode, ManualLogSource log)
        {
            string normalized = NormalizeMode(mode);
            if (normalized == "zh" || normalized == "en")
            {
                return new PeakMapLanguageResult(normalized, "Config", normalized, normalized);
            }

            string gameLanguage;
            if (TryGetGameLocalizedTextLanguage(out gameLanguage))
            {
                return new PeakMapLanguageResult(IsChineseGameLanguage(gameLanguage) ? "zh" : "en", "Game.LocalizedText.CURRENT_LANGUAGE", gameLanguage, normalized);
            }

            string localeCode;
            string localeSource;
            if (TryGetUnityLocalizationCode(out localeCode, out localeSource))
            {
                return new PeakMapLanguageResult(IsChineseCode(localeCode) ? "zh" : "en", localeSource, localeCode, normalized);
            }

            string systemLanguage = Application.systemLanguage.ToString();
            return new PeakMapLanguageResult(IsChineseSystemLanguage(Application.systemLanguage) ? "zh" : "en", "SystemLanguageFallback", systemLanguage, normalized);
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

        private static bool TryGetUnityLocalizationCode(out string code, out string source)
        {
            code = null;
            source = null;

            try
            {
                Type settingsType = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization");
                if (settingsType == null)
                {
                    return false;
                }

                PropertyInfo selectedLocaleProperty = settingsType.GetProperty("SelectedLocale", BindingFlags.Public | BindingFlags.Static);
                object selectedLocale = selectedLocaleProperty != null ? selectedLocaleProperty.GetValue(null, null) : null;
                if (TryExtractLocaleCode(selectedLocale, out code))
                {
                    source = "UnityLocalization.SelectedLocale";
                    return true;
                }

                PropertyInfo selectedLocaleAsyncProperty = settingsType.GetProperty("SelectedLocaleAsync", BindingFlags.Public | BindingFlags.Static);
                object selectedLocaleAsync = selectedLocaleAsyncProperty != null ? selectedLocaleAsyncProperty.GetValue(null, null) : null;
                if (TryExtractAsyncLocaleCode(selectedLocaleAsync, out code))
                {
                    source = "UnityLocalization.SelectedLocaleAsync";
                    return true;
                }

                MethodInfo getSelectedLocaleMethod = settingsType.GetMethod("GetSelectedLocale", BindingFlags.Public | BindingFlags.Static);
                object methodLocale = getSelectedLocaleMethod != null ? getSelectedLocaleMethod.Invoke(null, null) : null;
                if (TryExtractLocaleCode(methodLocale, out code))
                {
                    source = "UnityLocalization.GetSelectedLocale";
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryGetGameLocalizedTextLanguage(out string language)
        {
            language = null;

            try
            {
                Type localizedTextType = FindType("LocalizedText", "Assembly-CSharp");
                if (localizedTextType == null)
                {
                    return false;
                }

                FieldInfo currentLanguageField = localizedTextType.GetField("CURRENT_LANGUAGE", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                object currentLanguage = currentLanguageField != null ? currentLanguageField.GetValue(null) : null;
                if (currentLanguage == null)
                {
                    return false;
                }

                language = currentLanguage.ToString();
                if (string.IsNullOrWhiteSpace(language))
                {
                    language = Convert.ToInt32(currentLanguage).ToString();
                }

                return !string.IsNullOrWhiteSpace(language);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryExtractAsyncLocaleCode(object selectedLocaleAsync, out string code)
        {
            code = null;
            if (selectedLocaleAsync == null)
            {
                return false;
            }

            try
            {
                PropertyInfo isDoneProperty = selectedLocaleAsync.GetType().GetProperty("IsDone", BindingFlags.Public | BindingFlags.Instance);
                object isDoneValue = isDoneProperty != null ? isDoneProperty.GetValue(selectedLocaleAsync, null) : null;
                if (isDoneValue is bool && !(bool)isDoneValue)
                {
                    return false;
                }

                PropertyInfo resultProperty = selectedLocaleAsync.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                object result = resultProperty != null ? resultProperty.GetValue(selectedLocaleAsync, null) : null;
                return TryExtractLocaleCode(result, out code);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryExtractLocaleCode(object locale, out string code)
        {
            code = null;
            if (locale == null)
            {
                return false;
            }

            try
            {
                PropertyInfo identifierProperty = locale.GetType().GetProperty("Identifier", BindingFlags.Public | BindingFlags.Instance);
                object identifier = identifierProperty != null ? identifierProperty.GetValue(locale, null) : null;
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

                PropertyInfo localeNameProperty = locale.GetType().GetProperty("LocaleName", BindingFlags.Public | BindingFlags.Instance);
                object localeName = localeNameProperty != null ? localeNameProperty.GetValue(locale, null) : null;
                if (localeName != null && !string.IsNullOrWhiteSpace(localeName.ToString()))
                {
                    code = localeName.ToString();
                    return true;
                }

                string value = locale.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    code = value;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static Type FindType(string typeName, string assemblyName)
        {
            Type direct = Type.GetType(typeName + ", " + assemblyName);
            if (direct != null)
            {
                return direct;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                AssemblyName name = assemblies[i].GetName();
                if (!string.Equals(name.Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Type type = assemblies[i].GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static bool IsChineseCode(string code)
        {
            return !string.IsNullOrWhiteSpace(code)
                && code.Trim().StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsChineseGameLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return false;
            }

            string value = language.Trim();
            return string.Equals(value, "SimplifiedChinese", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Chinese", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "ChineseSimplified", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "9", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsChineseSystemLanguage(SystemLanguage language)
        {
            return language == SystemLanguage.Chinese
                || language == SystemLanguage.ChineseSimplified
                || language == SystemLanguage.ChineseTraditional;
        }
    }

    internal sealed class PeakMapLanguageResult
    {
        public readonly string Language;
        public readonly string Source;
        public readonly string RawValue;
        public readonly string Mode;

        public PeakMapLanguageResult(string language, string source, string rawValue, string mode)
        {
            Language = language;
            Source = source;
            RawValue = rawValue;
            Mode = mode;
        }
    }
}
