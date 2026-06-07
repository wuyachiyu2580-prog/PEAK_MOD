using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;
using TerrainCustomiserCN.Managers;
using UnityEngine;
using Zorro.Core;

namespace TerrainCustomiserCN
{
	[BepInDependency("com.snosz.ubimgui")]
	[BepInDependency("com.snosz.photoncustompropsutils")]
	// 单个客户端不能同时加载中英文两个版本；地图和联机数据兼容由原始网络键与序列化绑定器保证。
	[BepInIncompatibility("com.snosz.terraincustomiser")]
	[BepInPlugin("com.wuyachiyu.terraincustomisercn", "TerrainCustomiserCN", "0.1.2")]
	public class Plugin : BaseUnityPlugin
	{
		private void Awake()
		{
			Plugin.Instance = this;
			ConfigManager.Setup();
			BundleManager.Setup();
			PlayerInfoManager.Setup();
			// Harmony 补丁仍沿用原版行为，只替换 UI 展示和必要的兼容入口。
			Plugin._harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
			WindowsManager.Setup();
		}

		private void OnDestroy()
		{
			Plugin._harmony.UnpatchSelf();
			BundleManager.UnloadAll();
			UnityEngine.Object.Destroy(Singleton<EditorManager>.Instance);
		}

		public void LoadWilIsland()
		{
			// 保持原版载入 WilIsland 的 RPC 参数，避免影响主客机交叉游玩。
			AirportCheckInKiosk airportCheckInKiosk = UnityEngine.Object.FindFirstObjectByType<AirportCheckInKiosk>();
			bool flag = airportCheckInKiosk == null;
			if (flag)
			{
				Debug.LogError("[TCCN] 未找到机场登机台");
			}
			else
			{
				int ascentIndex = GUIManager.instance.boardingPass.ascentIndex;
				airportCheckInKiosk.photonView.RPC("BeginIslandLoadRPC", 0, new object[]
				{
					"WilIsland",
					ascentIndex,
					RunSettings.GetSerializedRunSettings()
				});
			}
		}

		public static Plugin Instance;

		private static Harmony _harmony;
	}
}
