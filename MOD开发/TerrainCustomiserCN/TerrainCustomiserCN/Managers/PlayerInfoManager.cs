using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Photon.Pun;
using Photon.Realtime;
using Sirenix.Serialization;
using TerrainCustomiserCN.Map.Serialization;
using TerrainCustomiserCN.Utils;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

namespace TerrainCustomiserCN.Managers
{
	internal static class PlayerInfoManager
	{
		internal static void Setup()
		{
			PlayerInfoManager.LocalPlayerInfo = new PlayerInfo
			{
				canBake = ConfigManager.EnableLightMapBaking.Value
			};
		}

		public static void HandlePlayerInfoChanged(PhotonPlayer targetPlayer, byte[] data)
		{
			PlayerInfo playerInfo = SerializationUtility.DeserializeValue<PlayerInfo>(data, DataFormat.JSON, TerrainCustomiserSerialization.CreateDeserializationContext());
			PlayerInfo playerInfo2;
			bool flag = PlayerInfoManager.PlayersInfo.TryGetValue(targetPlayer, out playerInfo2);
			if (flag)
			{
				PlayerInfoManager.UpdatePlayerInfo(targetPlayer, playerInfo);
			}
			else
			{
				PlayerInfoManager.AddPlayerInfo(targetPlayer, playerInfo);
			}
		}

		public static void SendLocalPlayerInfo()
		{
			byte[] value = SerializationUtility.SerializeValue(PlayerInfoManager.LocalPlayerInfo, DataFormat.JSON, TerrainCustomiserSerialization.CreateSerializationContext());
			NetworkManager.SetPlayerProperty("playerInfo", value);
		}

		public static void AddPlayerInfo(PhotonPlayer targetPlayer, PlayerInfo playerInfo)
		{
			PlayerInfoManager.PlayersInfo.Add(targetPlayer, playerInfo);
			bool isMasterClient = PhotonNetwork.IsMasterClient;
			if (isMasterClient)
			{
				PlayerInfoManager.LobbyCanBake = PlayerInfoManager.PlayersInfo.All((KeyValuePair<PhotonPlayer, PlayerInfo> x) => x.Value.canBake);
				PlayerInfoManager.LobbyHasMapData = PlayerInfoManager.PlayersInfo.All((KeyValuePair<PhotonPlayer, PlayerInfo> x) => x.Value.hasMapData);
			}
		}

		public static void RemovePlayerInfo(PhotonPlayer targetPlayer)
		{
			bool flag = PlayerInfoManager.PlayersInfo.Remove(targetPlayer);
			bool flag2 = flag && PhotonNetwork.IsMasterClient;
			if (flag2)
			{
				PlayerInfoManager.LobbyCanBake = PlayerInfoManager.PlayersInfo.All((KeyValuePair<PhotonPlayer, PlayerInfo> x) => x.Value.canBake);
				PlayerInfoManager.LobbyHasMapData = PlayerInfoManager.PlayersInfo.All((KeyValuePair<PhotonPlayer, PlayerInfo> x) => x.Value.hasMapData);
			}
		}

		public static void UpdatePlayerInfo(PhotonPlayer targetPlayer, PlayerInfo playerInfo)
		{
			PlayerInfoManager.PlayersInfo[targetPlayer] = playerInfo;
			bool isMasterClient = PhotonNetwork.IsMasterClient;
			if (isMasterClient)
			{
				PlayerInfoManager.LobbyCanBake = PlayerInfoManager.PlayersInfo.All((KeyValuePair<PhotonPlayer, PlayerInfo> x) => x.Value.canBake);
				PlayerInfoManager.LobbyHasMapData = PlayerInfoManager.PlayersInfo.All((KeyValuePair<PhotonPlayer, PlayerInfo> x) => x.Value.hasMapData);
			}
		}

		public static Dictionary<PhotonPlayer, PlayerInfo> PlayersInfo = new Dictionary<PhotonPlayer, PlayerInfo>();

		public static PlayerInfo LocalPlayerInfo;

		internal static MapSerializer.MapSyncData currentMapSyncData;

		public static bool LobbyCanBake = false;

		public static bool LobbyHasMapData = false;
	}
}
