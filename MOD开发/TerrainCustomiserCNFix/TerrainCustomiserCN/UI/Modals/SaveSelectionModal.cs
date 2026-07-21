using System;
using System.Collections.Generic;
using ImGuiNET;
using TerrainCustomiserCN.Map.Serialization;
using TerrainCustomiserCN.Session;
using UnityEngine;

namespace TerrainCustomiserCN.UI.Modals
{
	public static class SaveSelectionModal
	{
		public static void Open(Action<string> onSelectAction)
		{
			SaveSelectionModal.SaveFiles = MapSerializer.GetSaveFileNames(true);
			if (SaveSelectionModal.SelectedSaveIndex >= SaveSelectionModal.SaveFiles.Count)
			{
				SaveSelectionModal.SelectedSaveIndex = 0;
			}
			SaveSelectionModal.OnSelectAction = onSelectAction;
			ImGui.OpenPopup("选择存档");
		}

		public static void Draw()
		{
			bool flag = ImGui.BeginPopupModal("选择存档", ImGuiWindowFlags.NoResize);
			if (flag)
			{
				ImGui.SetWindowSize(SaveSelectionModal.ModalSize);
				WidgetFactory.FieldWidgetData[] array = new WidgetFactory.FieldWidgetData[1];
				array[0] = new WidgetFactory.FieldWidgetData(typeof(string), () => SaveSelectionModal.FilterInput, delegate(object v)
				{
					SaveSelectionModal.FilterInput = (string)v;
				}, "筛选", null, false, false);
				WidgetFactory.FieldWidgetData[] fieldWidgets = array;
				ImGuiHelpers.DrawPropertiesTable("SaveSelectionModalTable", fieldWidgets);
				ImGui.SetNextItemWidth(-1f);
				bool flag2 = ImGui.BeginListBox("##saves");
				if (flag2)
				{
					for (int i = 0; i < SaveSelectionModal.SaveFiles.Count; i++)
					{
						string saveName = SaveSelectionModal.SaveFiles[i];
						bool flag3 = !string.IsNullOrEmpty(SaveSelectionModal.FilterInput) && saveName.IndexOf(SaveSelectionModal.FilterInput, StringComparison.OrdinalIgnoreCase) < 0;
						if (!flag3)
						{
							bool selected = i == SaveSelectionModal.SelectedSaveIndex;
							string displayName = DisplayNameTranslator.Dynamic(saveName, "SaveFile");
							bool flag4 = MapSerializer.IsOldBackup(saveName);
							if (flag4)
							{
								displayName += " [只读备份]";
							}
							bool selectedChanged = ImGui.Selectable(displayName + "##" + saveName, selected);
							if (selectedChanged)
							{
								SaveSelectionModal.SelectedSaveIndex = i;
							}
						}
					}
					ImGui.EndListBox();
				}
				bool flag5 = ImGui.Button("选择");
				if (flag5)
				{
					bool flag6 = SaveSelectionModal.SelectedSaveIndex >= 0 && SaveSelectionModal.SelectedSaveIndex < SaveSelectionModal.SaveFiles.Count;
					if (flag6)
					{
						SaveSelectionModal.OnSelectAction(SaveSelectionModal.SaveFiles[SaveSelectionModal.SelectedSaveIndex]);
					}
					ImGui.CloseCurrentPopup();
				}
				ImGui.SameLine();
				bool flag7 = ImGui.Button("取消");
				if (flag7)
				{
					ImGui.CloseCurrentPopup();
					SessionState.Set(SessionState.State.InAirport);
				}
				ImGui.EndPopup();
			}
		}

		private static Vector2 ModalSize = new Vector2(275f, 225f);

		private static int SelectedSaveIndex = 0;

		private static Action<string> OnSelectAction;

		private static string FilterInput = string.Empty;

		private static List<string> SaveFiles = new List<string>();
	}
}
