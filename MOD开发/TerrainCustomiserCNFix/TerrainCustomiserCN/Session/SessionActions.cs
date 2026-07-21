using System;
using Photon.Pun;
using TerrainCustomiserCN.Managers;
using Zorro.Core;

namespace TerrainCustomiserCN.Session
{
	internal static class SessionActions
	{
		internal static void OnQuitToMainMenu()
		{
			bool flag = SessionState.Is(SessionState.State.InEditor) && Singleton<EditorManager>.Instance != null;
			if (flag)
			{
				Singleton<EditorManager>.Instance.EndSession();
			}
		}

		internal static void OnLobbyEnter()
		{
			string lobbyData = NetworkManager.GetLobbyData("TC_inCustomMap");
			bool flag = string.IsNullOrEmpty(lobbyData);
			if (!flag)
			{
				bool flag2 = lobbyData == "true";
				if (flag2)
				{
					SessionState.Set(SessionState.State.WaitingForMapData);
				}
			}
		}

		internal static void OnAirportEnter()
		{
			SessionState.Set(SessionState.State.InAirport);
			bool isMasterClient = PhotonNetwork.IsMasterClient;
			if (isMasterClient)
			{
				NetworkManager.SetRoomProperty("mapData", null);
				NetworkManager.SetLobbyData("TC_inCustomMap", "false");
			}
		}

		internal static void OnStartRun()
		{
			bool flag = SessionState.Is(SessionState.State.LoadingEditor);
			if (flag)
			{
				EditorManager.Setup();
			}
			else
			{
				bool flag2 = SessionState.Is(SessionState.State.LoadingCustomMap);
				if (flag2)
				{
					SessionState.Set(SessionState.State.InCustomMap);
					bool isMasterClient = PhotonNetwork.IsMasterClient;
					if (isMasterClient)
					{
						NetworkManager.SetLobbyData("TC_inCustomMap", "true");
					}
				}
			}
		}
	}
}
