using UnityEngine;

namespace PlayersInfo.Helpers
{
    internal static class LanguageHelper
    {
        public static bool IsChinese { get; set; }
        public static string L(string en, string zh) => IsChinese ? zh : en;
        public static bool DetectChineseLanguage()
        {
            // CURRENT_LANGUAGE is authoritative, including Traditional Chinese.
            try
            {
                return LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.SimplifiedChinese ||
                    LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.TraditionalChinese;
            }
            catch
            {
                return Application.systemLanguage == SystemLanguage.Chinese ||
                    Application.systemLanguage == SystemLanguage.ChineseSimplified ||
                    Application.systemLanguage == SystemLanguage.ChineseTraditional;
            }
        }
    }
}
