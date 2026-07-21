using System;
using ImGuiNET;
using UnityEngine;

namespace TerrainCustomiserCN.UI.Modals
{
	public static class RenameModal
	{
		public static void RequestOpen(Transform t)
		{
			RenameModal.OpenRequested = true;
			RenameModal.TransformRequested = t;
		}

		public static void Open()
		{
			bool flag = RenameModal.TransformRequested == null;
			if (flag)
			{
				RenameModal.OpenRequested = false;
			}
			else
			{
				RenameModal.Selected = RenameModal.TransformRequested;
				RenameModal.RenameBuffer = RenameModal.Selected.name;
				RenameModal.OpenRequested = false;
				ImGui.OpenPopup("重命名对象");
			}
		}

		public static void Draw()
		{
			bool flag = ImGui.BeginPopupModal("重命名对象", ImGuiWindowFlags.NoResize);
			if (flag)
			{
				ImGui.SetWindowSize(RenameModal.ModalSize);
				WidgetFactory.FieldWidgetData[] array = new WidgetFactory.FieldWidgetData[1];
				array[0] = new WidgetFactory.FieldWidgetData(typeof(string), () => RenameModal.RenameBuffer, delegate(object v)
				{
					RenameModal.RenameBuffer = (string)v;
				}, "名称", null, false, false);
				WidgetFactory.FieldWidgetData[] fieldWidgets = array;
				ImGuiHelpers.DrawPropertiesTable("RenameModalTable", fieldWidgets);
				bool flag2 = ImGui.Button("应用");
				if (flag2)
				{
					RenameModal.Selected.name = RenameModal.RenameBuffer;
					ImGui.CloseCurrentPopup();
					RenameModal.RenameBuffer = string.Empty;
				}
				ImGui.SameLine();
				bool flag3 = ImGui.Button("取消");
				if (flag3)
				{
					ImGui.CloseCurrentPopup();
					RenameModal.RenameBuffer = string.Empty;
				}
				ImGui.EndPopup();
			}
		}

		private static Vector2 ModalSize = new Vector2(275f, 150f);

		private static string RenameBuffer = string.Empty;

		private static Transform Selected;

		public static bool OpenRequested = false;

		private static Transform TransformRequested;
	}
}
