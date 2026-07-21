using System;
using System.Runtime.CompilerServices;
using Photon.Pun;
using Photon.Realtime;
using PhotonCustomPropsUtils;
using Sirenix.Serialization;
using Steamworks;
using TerrainCustomiserCN.Session;
using TerrainCustomiserCN.Utils;

namespace TerrainCustomiserCN.Managers
{
	internal static class NetworkManager
	{
		internal static void Setup()
		{
			NetworkManager._manager = PhotonCustomPropsUtilsPlugin.GetManager("com.snosz.terraincustomiser");
			NetworkManager._steamLobbyHandler = GameHandler.GetService<SteamLobbyHandler>();
			NetworkManager.RegisterEvents();
		}

		public static void SetRoomProperty(string propertyKey, object value)
		{
			NetworkManager._manager.SetRoomProperty(propertyKey, value);
		}

		public static void SetPlayerProperty(string propertyKey, object value)
		{
			NetworkManager._manager.SetPlayerProperty(propertyKey, value);
		}

		internal static void SetLobbyData(string propertyKey, string value)
		{
			bool flag = NetworkManager._steamLobbyHandler != null;
			if (flag)
			{
				SteamMatchmaking.SetLobbyData(NetworkManager._steamLobbyHandler.LobbySteamId, propertyKey, value);
			}
		}

		internal static string GetLobbyData(string propertyKey)
		{
			bool flag = NetworkManager._steamLobbyHandler != null;
			string result;
			if (flag)
			{
				result = SteamMatchmaking.GetLobbyData(NetworkManager._steamLobbyHandler.LobbySteamId, propertyKey);
			}
			else
			{
				result = null;
			}
			return result;
		}

		private static void RegisterEvents()
		{
			NetworkManager._manager.RegisterRoomProperty<byte[]>("mapData", RoomEventType.All, delegate(byte[] val)
			{
				NetworkManager.OnMapDataChanged(val);
			});
			NetworkManager._manager.RegisterRoomProperty<int[]>("propViews", RoomEventType.All, delegate(int[] val)
			{
				NetworkManager.OnPropViewsChanged(val);
			});
			NetworkManager._manager.RegisterOnJoinedRoom(delegate(Photon.Realtime.Player localPlayer)
			{
				NetworkManager.OnLocalPlayerJoinedRoom();
			});
			NetworkManager._manager.RegisterPlayerProperty<byte[]>("playerInfo", PlayerEventType.All, delegate(Photon.Realtime.Player targetPlayer, byte[] value)
			{
				NetworkManager.OnPlayerInfoChanged(targetPlayer, value);
			});
		}

		private static void OnMapDataChanged(byte[] val)
		{
			bool flag = val == null;
			if (flag)
			{
				PlayerInfoManager.currentMapSyncData = null;
				PlayerInfoManager.LocalPlayerInfo.hasMapData = false;
			}
			else
			{
				PlayerInfoManager.currentMapSyncData = NetworkUtils.DecompressMapSyncData(val);
				PlayerInfoManager.LocalPlayerInfo.hasMapData = true;
				bool flag2 = SessionState.Is(SessionState.State.WaitingForMapData);
				if (flag2)
				{
					SessionState.Set(SessionState.State.WaitingToStartCustomMap);
					MapHandler.InitializeMap();
				}
				else
				{
					SessionState.Set(SessionState.State.WaitingToStartCustomMap);
				}
			}
			PlayerInfoManager.SendLocalPlayerInfo();
		}

		private static void OnPropViewsChanged(int[] val)
		{
			bool isMasterClient = PhotonNetwork.IsMasterClient;
			if (!isMasterClient)
			{
				ViewSyncManager.ReceivePendingIds(val);
			}
		}

		private static void OnLocalPlayerJoinedRoom()
		{
			byte[] value = SerializationUtility.SerializeValue(PlayerInfoManager.LocalPlayerInfo, DataFormat.JSON, TerrainCustomiserSerialization.CreateSerializationContext());
			NetworkManager.SetPlayerProperty("playerInfo", value);
		}

		private static void OnPlayerInfoChanged(Photon.Realtime.Player targetPlayer, byte[] val)
		{
			PlayerInfoManager.HandlePlayerInfoChanged(targetPlayer, val);
			bool flag = PhotonNetwork.IsMasterClient && SessionState.Is(SessionState.State.WaitingToStartCustomMap);
			if (flag)
			{
				bool lobbyHasMapData = PlayerInfoManager.LobbyHasMapData;
				if (lobbyHasMapData)
				{
					Plugin.Instance.LoadWilIsland();
				}
			}
		}

		private static PhotonScopedManager _manager;

		private static SteamLobbyHandler _steamLobbyHandler;

		public static class PropertyKeys
		{
			public const string MapData = "mapData";

			public const string PropViews = "propViews";

			public const string PlayerInfo = "playerInfo";

			public const string InCustomMap = "TC_inCustomMap";
		}
	}
}
