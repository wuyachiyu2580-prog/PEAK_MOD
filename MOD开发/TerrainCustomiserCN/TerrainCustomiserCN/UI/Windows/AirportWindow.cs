using System;
using ImGuiNET;
using Photon.Pun;
using Photon.Realtime;
using TerrainCustomiserCN.Managers;
using TerrainCustomiserCN.Map.Serialization;
using TerrainCustomiserCN.Session;
using TerrainCustomiserCN.UI.Modals;
using TerrainCustomiserCN.Utils;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;
using Random = UnityEngine.Random;

namespace TerrainCustomiserCN.UI.Windows
{
	public class AirportWindow : ImGuiWindowBase
	{
		public override string WindowName
		{
			get
			{
				return "地形编辑菜单";
			}
		}

		public override ImGuiWindowFlags WindowFlags
		{
			get
			{
				return ImGuiWindowFlags.AlwaysAutoResize;
			}
		}

		public override void OnCreate()
		{
			base.OnCreate();
			this.bakeLightMap = (PlayerInfoManager.LobbyCanBake && ConfigManager.EnableLightMapBaking.Value && ConfigManager.BakeLightMap.Value);
			this.useRandomSeed = ConfigManager.UseRandomSeed.Value;
			this.seedValue = ConfigManager.SeedToUse.Value;
		}

		protected override void DrawContent()
		{
			bool flag = SessionState.Is(SessionState.State.InAirport) || SessionState.Is(SessionState.State.SelectingSave);
			if (flag)
			{
				this.DrawOptionSelectionMenu();
			}
			else
			{
				bool flag2 = SessionState.Is(SessionState.State.ConfiguringCustomMap) || SessionState.Is(SessionState.State.WaitingToStartCustomMap);
				if (flag2)
				{
					this.DrawPlayConfigMenu();
				}
			}
		}

		private void DrawOptionSelectionMenu()
		{
			bool flag = ImGui.Button("开始");
			if (flag)
			{
				SaveSelectionModal.Open(new Action<string>(this.OnSaveSelected));
				SessionState.Set(SessionState.State.SelectingSave);
			}
			SaveSelectionModal.Draw();
			ImGui.SameLine();
			ImGuiHelpers.DrawButton("编辑器", new Action(this.OnEditorButtonClicked), PhotonNetwork.OfflineMode);
		}

		private void DrawPlayConfigMenu()
		{
			WidgetFactory.FieldWidgetData[] fieldWidgets = new WidgetFactory.FieldWidgetData[]
			{
				new WidgetFactory.FieldWidgetData(typeof(bool), () => this.useRandomSeed, delegate(object v)
				{
					this.SetUseRandomSeedValue((bool)v);
				}, "随机种子", null, false, false),
				new WidgetFactory.FieldWidgetData(typeof(int), () => this.seedValue, delegate(object v)
				{
					this.SetSeedValue(this.seedValue);
				}, "种子", null, false, this.useRandomSeed),
				new WidgetFactory.FieldWidgetData(typeof(bool), () => this.bakeLightMap && PlayerInfoManager.LobbyCanBake, delegate(object v)
				{
					this.SetBakeLightMapValue((bool)v);
				}, "烘焙光照贴图", null, false, !PlayerInfoManager.LobbyCanBake)
			};
			ImGuiHelpers.DrawPropertiesTable("PlayConfigTable", fieldWidgets);
			bool flag = ImGui.BeginListBox("##players");
			if (flag)
			{
				foreach (PhotonPlayer player in PlayerHandler.Instance._playerList.Get())
				{
					ImGui.TextUnformatted(player.NickName);
					PlayerInfo playerInfo;
					bool flag2 = PlayerInfoManager.PlayersInfo.TryGetValue(player, out playerInfo);
					if (flag2)
					{
						ImGui.SameLine();
						ImGui.TextUnformatted(playerInfo.canBake ? "可烘焙" : "不可烘焙");
						bool flag3 = SessionState.Is(SessionState.State.LoadingCustomMap);
						if (flag3)
						{
							ImGui.SameLine();
							ImGui.TextUnformatted(playerInfo.hasMapData ? "已就绪" : "未就绪");
						}
					}
				}
				ImGui.EndListBox();
			}
			ImGuiHelpers.DrawButton("开始", new Action(this.OnPlayClicked), SessionState.Is(SessionState.State.ConfiguringCustomMap));
			ImGui.SameLine();
			bool flag4 = ImGui.Button("取消");
			if (flag4)
			{
				this.OnCancelLoadClicked();
			}
		}

		private void SetUseRandomSeedValue(bool newValue)
		{
			this.useRandomSeed = newValue;
			ConfigManager.UseRandomSeed.Value = newValue;
		}

		private void SetSeedValue(int newValue)
		{
			this.seedValue = newValue;
			ConfigManager.SeedToUse.Value = newValue;
		}

		private void SetBakeLightMapValue(bool newValue)
		{
			this.bakeLightMap = newValue;
			ConfigManager.BakeLightMap.Value = newValue;
		}

		private void OnSaveSelected(string saveFile)
		{
			this.selectedSaveFile = saveFile;
			SessionState.Set(SessionState.State.ConfiguringCustomMap);
		}

		private void OnEditorButtonClicked()
		{
			SessionState.Set(SessionState.State.LoadingEditor);
			Plugin.Instance.LoadWilIsland();
		}

		private void OnPlayClicked()
		{
			SessionState.Set(SessionState.State.WaitingToStartCustomMap);
			byte[] save = MapSerializer.GetSave(this.selectedSaveFile);
			bool flag = this.useRandomSeed;
			if (flag)
			{
				this.seedValue = Random.Range(0, 999999999);
			}
			MapSerializer.MapSyncData mapDataRoomProperty = new MapSerializer.MapSyncData
			{
				bakeLightMap = this.bakeLightMap,
				seed = this.seedValue,
				mapSaveData = save
			};
			NetworkUtils.SetMapDataRoomProperty(mapDataRoomProperty);
		}

		private void OnCancelLoadClicked()
		{
			NetworkManager.SetRoomProperty("mapData", null);
			SessionState.Set(SessionState.State.InAirport);
		}

		private string selectedSaveFile = string.Empty;

		private bool useRandomSeed = false;

		private int seedValue = 0;

		private bool bakeLightMap = false;
	}
}
