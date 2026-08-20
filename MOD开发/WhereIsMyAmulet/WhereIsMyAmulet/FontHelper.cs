using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace WhereIsMyAmulet
{
    internal static class FontHelper
    {
        public static TMP_FontAsset GetLabelFont(AmuletLabelFont choice)
        {
            string source;
            TMP_FontAsset selected = GetFontForChoice(choice, out source);
            if (selected == null)
            {
                selected = GetGameDefaultFont();
                source += " -> GameDefault fallback";
            }

            return selected;
        }

        private static TMP_FontAsset GetFontForChoice(AmuletLabelFont choice, out string source)
        {
            switch (choice)
            {
                case AmuletLabelFont.TmpDefault:
                    source = "TmpDefault";
                    return TMP_Settings.defaultFontAsset;
                case AmuletLabelFont.KoreanBinggrae:
                    source = "KoreanBinggrae";
                    return FindFontAsset("Korean Binggrae-Bold SDF");
                case AmuletLabelFont.Crazk:
                    source = "Crazk";
                    return FindFontAsset("CRAZK___ SDF");
                case AmuletLabelFont.Auto:
                    source = "Auto(GameDefault)";
                    return GetGameDefaultFont() ?? TMP_Settings.defaultFontAsset;
                default:
                    source = "GameDefault";
                    return GetGameDefaultFont();
            }
        }

        private static TMP_FontAsset GetGameDefaultFont()
        {
            return FontFallbackSwapper.instance == null ? null : FontFallbackSwapper.instance.mainBaseFont;
        }

        private static TMP_FontAsset FindFontAsset(string assetName)
        {
            IEnumerable<TMP_FontAsset> loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            IEnumerable<TMP_FontAsset> resourceFonts;
            try
            {
                resourceFonts = Resources.LoadAll<TMP_FontAsset>(string.Empty);
            }
            catch
            {
                resourceFonts = Enumerable.Empty<TMP_FontAsset>();
            }

            return loadedFonts.Concat(resourceFonts)
                .Where(font => font != null)
                .Distinct()
                .FirstOrDefault(font => string.Equals(NormalizeFontName(font.name), NormalizeFontName(assetName),
                    System.StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeFontName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            int cloneSuffix = name.IndexOf(" (Clone)", System.StringComparison.OrdinalIgnoreCase);
            return (cloneSuffix < 0 ? name : name.Substring(0, cloneSuffix)).Trim();
        }

    }
}
