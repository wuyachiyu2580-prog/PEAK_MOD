using System;
using System.Linq;
using ImGuiNET;
using TerrainCustomiserCN.Managers;
using TerrainCustomiserCN.Map;
using UnityEngine;
using Zorro.Core;

namespace TerrainCustomiserCN.UI.Modals
{
	public static class SwapBiomeModal
	{
		public static void Open(MapData.BiomeSection biomeSection)
		{
			SwapBiomeModal.TargetSection = biomeSection;
			SwapBiomeModal.BiomeOptionNames = (from x in biomeSection.biomes
			select x.name).ToArray<string>();
			SwapBiomeModal.SelectedBiomeIndex = biomeSection.biomes.FindIndex((MapData.BiomeOption x) => x.isActive);
			ImGui.OpenPopup("切换生物群系");
		}

		public static void Draw()
		{
			bool flag = ImGui.BeginPopupModal("切换生物群系", ImGuiWindowFlags.NoResize);
			if (flag)
			{
				ImGui.SetWindowSize(SwapBiomeModal.ModalSize);
				WidgetFactory.FieldWidgetData[] array = new WidgetFactory.FieldWidgetData[1];
				array[0] = new WidgetFactory.ComboWidgetData("生物群系", () => SwapBiomeModal.SelectedBiomeIndex, delegate(int v)
				{
					SwapBiomeModal.SelectedBiomeIndex = v;
				}, () => SwapBiomeModal.BiomeOptionNames);
				WidgetFactory.FieldWidgetData[] fieldWidgets = array;
				ImGuiHelpers.DrawPropertiesTable("SwapBiomeModalTable", fieldWidgets);
				bool flag2 = ImGui.Button("应用");
				if (flag2)
				{
					bool segmentHasCustomVariant = SwapBiomeModal.TargetSection.activeBiome.segmentHasCustomVariant;
					if (segmentHasCustomVariant)
					{
						ConfirmationModal.Open("该生物群系的自定义变体将被删除。是否继续？", delegate
						{
							SwapBiomeModal.OverrideConfirmed = true;
						});
					}
					else
					{
						Singleton<EditorManager>.Instance.ChangeBiome(SwapBiomeModal.TargetSection, SwapBiomeModal.TargetSection.biomes[SwapBiomeModal.SelectedBiomeIndex]);
						ImGui.CloseCurrentPopup();
					}
				}
				ConfirmationModal.Draw();
				bool overrideConfirmed = SwapBiomeModal.OverrideConfirmed;
				if (overrideConfirmed)
				{
					Singleton<EditorManager>.Instance.ChangeBiome(SwapBiomeModal.TargetSection, SwapBiomeModal.TargetSection.biomes[SwapBiomeModal.SelectedBiomeIndex]);
					SwapBiomeModal.OverrideConfirmed = false;
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

		private static Vector2 ModalSize = new Vector2(275f, 100f);

		private static MapData.BiomeSection TargetSection;

		private static string[] BiomeOptionNames;

		private static int SelectedBiomeIndex = 0;

		private static bool OverrideConfirmed = false;
	}
}
