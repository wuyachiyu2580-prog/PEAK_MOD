using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Photon.Pun;
using UnityEngine;

namespace TerrainCustomiserCN.Managers
{
	internal static class ViewSyncManager
	{
		internal static void AddPendingView(PhotonView view)
		{
			bool flag = ViewSyncManager.pendingViews.Contains(view);
			if (!flag)
			{
				ViewSyncManager.pendingViews.Add(view);
			}
		}

		internal static void ReceivePendingIds(int[] ids)
		{
			ViewSyncManager.pendingIds = ids;
			ViewSyncManager.HandlePendingViews();
		}

		private static void ClearPending()
		{
			ViewSyncManager.pendingViews.Clear();
			ViewSyncManager.pendingIds = null;
		}

		internal static void HandlePendingViews()
		{
			bool isMasterClient = PhotonNetwork.IsMasterClient;
			if (isMasterClient)
			{
				int[] value = ViewSyncManager.AssignPendingViews();
				NetworkManager.SetRoomProperty("propViews", value);
				ViewSyncManager.ClearPending();
			}
			else
			{
				bool flag = ViewSyncManager.pendingIds != null && ViewSyncManager.pendingIds.Length != 0;
				if (flag)
				{
					ViewSyncManager.ApplyPendingIds();
					ViewSyncManager.ClearPending();
				}
			}
		}

		private static int[] AssignPendingViews()
		{
			int[] array = new int[ViewSyncManager.pendingViews.Count];
			for (int i = 0; i < ViewSyncManager.pendingViews.Count; i++)
			{
				int num = PhotonNetwork.AllocateViewID(false);
				ViewSyncManager.pendingViews[i].ViewID = num;
				array[i] = num;
			}
			return array;
		}

		private static void ApplyPendingIds()
		{
			bool flag = ViewSyncManager.pendingIds.Length != ViewSyncManager.pendingViews.Count;
			if (flag)
			{
				Debug.LogError(string.Format("[TCCN] ID Count mismatch! Map will be out of sync! Views: {0}, Ids: {1}.", ViewSyncManager.pendingViews.Count, ViewSyncManager.pendingIds.Length));
			}
			else
			{
				for (int i = 0; i < ViewSyncManager.pendingIds.Length; i++)
				{
					bool flag2 = ViewSyncManager.pendingViews[i] != null && ViewSyncManager.pendingViews[i].gameObject != null;
					if (flag2)
					{
						ViewSyncManager.pendingViews[i].ViewID = ViewSyncManager.pendingIds[i];
					}
				}
			}
		}

		private static List<PhotonView> pendingViews = new List<PhotonView>();

		private static int[] pendingIds;
	}
}
