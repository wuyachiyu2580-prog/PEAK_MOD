using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Photon.Pun;
using TerrainCustomiserCN.Managers;
using TerrainCustomiserCN.Session;
using TerrainCustomiserCN.UI.Windows;

namespace TerrainCustomiserCN.Patches
{
	internal class AirportPatches
	{
		[HarmonyPatch(typeof(GameUtils), "OnEnable")]
		private class GameUtilsOnEnablePatch
		{
			private static void Postfix(GameUtils __instance)
			{
				bool inAirport = __instance.m_inAirport;
				if (inAirport)
				{
					SessionActions.OnAirportEnter();
				}
			}
		}

		[HarmonyPatch(typeof(BoardingPass), "Initialize")]
		private class BoardingPassInitializePatch
		{
			private static void Postfix(BoardingPass __instance)
			{
				WindowsManager.Register<AirportWindow>();
			}
		}

		[HarmonyPatch(typeof(BoardingPass), "OnOpen")]
		private class BoardingPassOnOpenPatch
		{
			private static void Postfix()
			{
				bool isMasterClient = PhotonNetwork.IsMasterClient;
				if (isMasterClient)
				{
					WindowsManager.Get<AirportWindow>().Open();
				}
			}
		}

		[HarmonyPatch(typeof(BoardingPass), "OnClose")]
		private class BoardingPassOnClosePatch
		{
			private static void Postfix()
			{
				bool isMasterClient = PhotonNetwork.IsMasterClient;
				if (isMasterClient)
				{
					WindowsManager.Get<AirportWindow>().Close();
				}
			}
		}

		[HarmonyPatch(typeof(AirportCheckInKiosk), "BeginIslandLoadRPC")]
		private class BeginIslandLoadRPCPatch
		{
			private static void Postfix()
			{
				WindowsManager.Unregister<AirportWindow>();
			}
		}
	}
}
