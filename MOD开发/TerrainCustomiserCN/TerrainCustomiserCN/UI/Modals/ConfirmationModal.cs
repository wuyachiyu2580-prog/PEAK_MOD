using System;
using ImGuiNET;

namespace TerrainCustomiserCN.UI.Modals
{
	public static class ConfirmationModal
	{
		public static void Open(string message, Action confirmAction)
		{
			ConfirmationModal.Message = message;
			ConfirmationModal.ConfirmAction = confirmAction;
			ImGui.OpenPopup("确认操作");
		}

		public static void Draw()
		{
			bool flag = ImGui.BeginPopupModal("确认操作", ImGuiWindowFlags.AlwaysAutoResize);
			if (flag)
			{
				ImGui.TextUnformatted(ConfirmationModal.Message);
				bool flag2 = ImGui.Button("确认");
				if (flag2)
				{
					ConfirmationModal.ConfirmAction();
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

		private static Action ConfirmAction;

		private static string Message = string.Empty;
	}
}
