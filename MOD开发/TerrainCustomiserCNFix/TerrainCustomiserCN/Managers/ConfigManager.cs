using System;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;

namespace TerrainCustomiserCN.Managers
{
	internal static class ConfigManager
	{
		internal static void Setup()
		{
			ConfigManager.ConfigFile = Plugin.Instance.Config;
			ConfigManager.ConfigFile.SaveOnConfigSet = true;
			ConfigManager.EnableLightMapBaking = ConfigManager.ConfigFile.Bind<bool>("General", "EnableLightMapBaking", true, "生成用于部分游戏机制的光照贴图。若游戏崩溃，请关闭此项。");
			ConfigManager.BakeLightMap = ConfigManager.ConfigFile.Bind<bool>("UI", "BakeLightMap", false, "保存机场界面中“烘焙光照贴图”复选框的上次状态。");
			ConfigManager.UseRandomSeed = ConfigManager.ConfigFile.Bind<bool>("UI", "UseRandomSeed", true, "保存机场界面中“随机种子”复选框的上次状态。");
			ConfigManager.SeedToUse = ConfigManager.ConfigFile.Bind<int>("UI", "SeedToUse", 42, "保存机场界面中“种子”输入框的上次状态。");
			bool flag = !ConfigManager.EnableLightMapBaking.Value;
			if (flag)
			{
				ConfigManager.BakeLightMap.Value = false;
			}
		}

		public static ConfigFile ConfigFile;

		public static ConfigEntry<bool> EnableLightMapBaking;

		public static ConfigEntry<bool> BakeLightMap;

		public static ConfigEntry<bool> UseRandomSeed;

		public static ConfigEntry<int> SeedToUse;
	}
}
