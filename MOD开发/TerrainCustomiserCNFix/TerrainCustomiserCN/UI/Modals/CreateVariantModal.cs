using System;
using System.Linq;
using ImGuiNET;
using TerrainCustomiserCN.Map;
using UnityEngine;

namespace TerrainCustomiserCN.UI.Modals
{
	public static class CreateVariantModal
	{
		public static void Open(MapData.BiomeSegment biomeSegment)
		{
			CreateVariantModal.TargetSegment = biomeSegment;
			bool flag = biomeSegment.variantSelectionType == MapData.VariantSelectionType.BiomeVariant;
			if (flag)
			{
				CreateVariantModal.VariantOptionNames = (from x in biomeSegment.variants
				select x.name).ToArray<string>();
				CreateVariantModal.SelectedVariantIndex = biomeSegment.variants.FindIndex((MapData.SegmentVariant x) => x.isActive);
			}
			else
			{
				CreateVariantModal.VariantOptionNames = (from x in biomeSegment.variantObjects
				select x.name).ToArray<string>();
				CreateVariantModal.SelectedVariantIndex = biomeSegment.variantObjects.FindIndex((MapData.SegmentVariant x) => x.isActive);
			}
			CreateVariantModal.HasOptions = (CreateVariantModal.VariantOptionNames.Length != 0);
			ImGui.OpenPopup("创建变体");
		}

		public static void Draw()
		{
			bool flag = ImGui.BeginPopupModal("创建变体", ImGuiWindowFlags.NoResize);
			if (flag)
			{
				ImGui.SetWindowSize(CreateVariantModal.ModalSize);
				WidgetFactory.FieldWidgetData[] array = new WidgetFactory.FieldWidgetData[1];
				array[0] = new WidgetFactory.ComboWidgetData("变体模板", () => CreateVariantModal.SelectedVariantIndex, delegate(int v)
				{
					CreateVariantModal.SelectedVariantIndex = v;
				}, () => CreateVariantModal.VariantOptionNames);
				WidgetFactory.FieldWidgetData[] fieldWidgets = array;
				bool flag2 = !CreateVariantModal.HasOptions;
				if (flag2)
				{
					ImGui.BeginDisabled();
				}
				ImGuiHelpers.DrawPropertiesTable("CreateVariantModalTable", fieldWidgets);
				bool flag3 = !CreateVariantModal.HasOptions;
				if (flag3)
				{
					ImGui.EndDisabled();
				}
				bool flag4 = ImGui.Button("创建");
				if (flag4)
				{
					MapManager.CreateVariant(CreateVariantModal.TargetSegment, CreateVariantModal.HasOptions ? CreateVariantModal.SelectedVariantIndex : -1);
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

		private static Vector2 ModalSize = new Vector2(275f, 100f);

		private static MapData.BiomeSegment TargetSegment;

		private static string[] VariantOptionNames;

		private static int SelectedVariantIndex = 0;

		private static bool HasOptions = false;
	}
}
