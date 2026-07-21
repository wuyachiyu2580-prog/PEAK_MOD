using System;
using ImGuiNET;
using TerrainCustomiserCN.Map;

namespace TerrainCustomiserCN.UI.Modals
{
	public static class SaveModal
	{
		public static void Open()
		{
			ImGui.OpenPopup("保存地图");
		}

		public static void Draw()
		{
			bool flag = ImGui.BeginPopupModal("保存地图");
			if (flag)
			{
				WidgetFactory.FieldWidgetData[] array = new WidgetFactory.FieldWidgetData[1];
				array[0] = new WidgetFactory.FieldWidgetData(typeof(string), () => SaveModal.saveName, delegate(object v)
				{
					SaveModal.saveName = (string)v;
				}, "存档名", null, false, false);
				WidgetFactory.FieldWidgetData[] fieldWidgets = array;
				ImGuiHelpers.DrawPropertiesTable("saveModalTable", fieldWidgets);
				bool flag2 = ImGui.Button("保存");
				if (flag2)
				{
					MapSaver.SaveTerrain(SaveModal.saveName);
					ImGui.CloseCurrentPopup();
				}
				ImGui.SameLine();
				bool flag3 = ImGui.Button("取消");
				if (flag3)
				{
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
		}

		private static string saveName = "新存档";
	}
}
