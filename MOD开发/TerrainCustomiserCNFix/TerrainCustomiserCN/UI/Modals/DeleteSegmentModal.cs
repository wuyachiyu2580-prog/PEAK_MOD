using System;
using ImGuiNET;
using TerrainCustomiserCN.Map;

namespace TerrainCustomiserCN.UI.Modals
{
	public static class DeleteSegmentModal
	{
		public static void Open(MapData.SegmentVariant targetSegment)
		{
			DeleteSegmentModal.TargetSegment = targetSegment;
			ImGui.OpenPopup("删除分段");
		}

		public static void Draw()
		{
			bool flag = ImGui.BeginPopupModal("删除分段", ImGuiWindowFlags.AlwaysAutoResize);
			if (flag)
			{
				ImGui.TextUnformatted("删除你的自定义分段？");
				bool flag2 = ImGui.Button("是");
				if (flag2)
				{
					bool flag3 = DeleteSegmentModal.TargetSegment != null;
					if (flag3)
					{
						MapManager.DeleteSegmentVariant(DeleteSegmentModal.TargetSegment);
					}
					ImGui.CloseCurrentPopup();
				}
				ImGui.SameLine();
				bool flag4 = ImGui.Button("否");
				if (flag4)
				{
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
		}

		private static MapData.SegmentVariant TargetSegment;
	}
}
