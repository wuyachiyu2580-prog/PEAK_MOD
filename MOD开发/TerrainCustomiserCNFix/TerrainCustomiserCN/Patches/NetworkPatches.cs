using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Photon.Realtime;
using TerrainCustomiserCN.Managers;
using PhotonPlayer = Photon.Realtime.Player;

namespace TerrainCustomiserCN.Patches
{
	internal class NetworkPatches
	{
		[HarmonyPatch(typeof(GameHandler), "Awake")]
		private class GameHandlerAwakePatch
		{
			private static void Postfix(GameHandler __instance)
			{
				NetworkManager.Setup();
			}
		}

		[HarmonyPatch(typeof(GameUtils), "OnPlayerLeftRoom")]
		private class OnPlayerLeftRoomPatch
		{
			private static void Postfix(PhotonPlayer newPlayer)
			{
				PlayerInfoManager.RemovePlayerInfo(newPlayer);
			}
		}
	}
}
