using System;
using ImGuiNET;
using TerrainCustomiserCN.Map;
using TerrainCustomiserCN.Map.Serialization;
using UnityEngine;

namespace TerrainCustomiserCN.UI.Modals
{
	public static class LoadModal
	{
		public static void Open()
		{
			MapSerializer.RefreshSaveList();
			ImGui.OpenPopup("加载存档");
		}

		public static void Draw()
		{
			bool flag = ImGui.BeginPopupModal("加载存档", ImGuiWindowFlags.NoResize);
			if (flag)
			{
				ImGui.SetWindowSize(LoadModal.ModalSize);
				ImGui.SetNextItemWidth(-1f);
				bool flag2 = ImGui.BeginListBox("##saves");
				if (flag2)
				{
					for (int i = 0; i < MapSerializer.SaveFiles.Count; i++)
					{
						bool selected = i == LoadModal.selectedSaveIndex;
						string saveName = MapSerializer.SaveFiles[i];
						bool flag3 = ImGui.Selectable(DisplayNameTranslator.Dynamic(saveName, "SaveFile") + "##" + saveName, selected);
						if (flag3)
						{
							LoadModal.selectedSaveIndex = i;
						}
					}
					ImGui.EndListBox();
				}
				bool flag4 = ImGui.Button("加载");
				if (flag4)
				{
					MapSerializer.MapSaveData save = MapSerializer.LoadSave(LoadModal.selectedSaveIndex);
					MapLoader.ApplySave(save);
					ImGui.CloseCurrentPopup();
				}
				ImGui.SameLine();
				bool flag5 = ImGui.Button("取消");
				if (flag5)
				{
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
		}

		private static Vector2 ModalSize = new Vector2(275f, 200f);

		private static int selectedSaveIndex = 0;
	}
}
