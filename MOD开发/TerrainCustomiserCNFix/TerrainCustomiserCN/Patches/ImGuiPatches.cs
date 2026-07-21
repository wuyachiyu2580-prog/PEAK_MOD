using HarmonyLib;
using ImGuiNET;
using TerrainCustomiserCN.Managers;
using UBImGui;

namespace TerrainCustomiserCN.Patches
{
	internal class ImGuiPatches
	{
		[HarmonyPatch(typeof(ImGuiTextures), "BuildFontAtlas")]
		private class BuildFontAtlasPatch
		{
			private static void Prefix(ImGuiIOPtr io, ref ImGuiFontAsset fontAsset)
			{
				fontAsset = ChineseFontManager.AppendChineseFont(fontAsset);
			}
		}
	}
}
