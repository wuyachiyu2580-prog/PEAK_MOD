using System;
using System.Collections.Generic;
using System.IO;
using UBImGui;
using UnityEngine;

namespace TerrainCustomiserCN.Managers
{
	internal static class ChineseFontManager
	{
		private const int FontSizeInPixels = 16;

		private static readonly string[] FontFileNames = new string[]
		{
			"simhei.ttf",
			"NotoSansSC-VF.ttf",
			"msyh.ttc",
			"simsun.ttc"
		};

		private static ImGuiFontAsset _fontAsset;

		private static bool _loggedMissingFont;

		internal static ImGuiFontAsset AppendChineseFont(ImGuiFontAsset originalFontAsset)
		{
			string fontPath = FindFontPath();
			if (fontPath == null)
			{
				if (!ChineseFontManager._loggedMissingFont)
				{
					Debug.LogWarning("[TCCN] 找不到可用的中文字体，ImGui 中文界面可能无法正确显示。");
					ChineseFontManager._loggedMissingFont = true;
				}
				return originalFontAsset;
			}

			if (ChineseFontManager._fontAsset)
			{
				return ChineseFontManager._fontAsset;
			}

			List<FontSettings> fontSettings = new List<FontSettings>
			{
				new FontSettings
				{
					fontPath = fontPath,
					sizeInPixels = ChineseFontManager.FontSizeInPixels,
					glyphRanges = FontSettings.ScriptGlyphRanges.Default | FontSettings.ScriptGlyphRanges.ChineseFull
				}
			};
			if (originalFontAsset && originalFontAsset.fontSettings != null)
			{
				fontSettings.AddRange(originalFontAsset.fontSettings);
			}

			ChineseFontManager._fontAsset = ScriptableObject.CreateInstance<ImGuiFontAsset>();
			ChineseFontManager._fontAsset.fontSettings = fontSettings.ToArray();
			return ChineseFontManager._fontAsset;
		}

		private static string FindFontPath()
		{
			string fontsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
			foreach (string fontFileName in ChineseFontManager.FontFileNames)
			{
				string fontPath = Path.Combine(fontsPath, fontFileName);
				if (File.Exists(fontPath))
				{
					return fontPath;
				}
			}
			return null;
		}
	}
}
