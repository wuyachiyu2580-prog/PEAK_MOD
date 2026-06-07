using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Steamworks;
using TerrainCustomiserCN.Session;

namespace TerrainCustomiserCN.Patches
{
	internal class SessionPatches
	{
		[HarmonyPatch(typeof(MainMenu), "Start")]
		private class MainMenuStartPatch
		{
			private static void Postfix(MainMenu __instance)
			{
				SessionState.Set(SessionState.State.InMainMenu);
			}
		}

		[HarmonyPatch(typeof(RunManager), "StartRun")]
		public class RunManagerStartRunPatch
		{
			private static void Postfix()
			{
				SessionActions.OnStartRun();
			}
		}

		[HarmonyPatch(typeof(PauseMenuMainPage), "Quit")]
		private class PauseMenQuitPatch
		{
			private static void Postfix()
			{
				SessionActions.OnQuitToMainMenu();
			}
		}

		[HarmonyPatch(typeof(SteamLobbyHandler), "OnLobbyEnter")]
		private static class OnLobbyEnterPatch
		{
			private static void Postfix(SteamLobbyHandler __instance, LobbyEnter_t param)
			{
				bool flag = param.m_EChatRoomEnterResponse == 1U;
				if (flag)
				{
					SessionActions.OnLobbyEnter();
				}
			}
		}
	}
}
