using System;
using ImGuiNET;

namespace TerrainCustomiserCN.UI
{
	public static class ImGuiHelpers
	{
		public static void DrawPropertiesTable(string tableName, WidgetFactory.FieldWidgetData[] fieldWidgets)
		{
			bool flag = ImGui.BeginTable("Table_" + tableName, 2);
			if (flag)
			{
				ImGui.TableSetupColumn(DisplayNameTranslator.Ui("Name"), ImGuiTableColumnFlags.WidthFixed, 100f);
				ImGui.TableSetupColumn(DisplayNameTranslator.Ui("Value"), ImGuiTableColumnFlags.WidthStretch);
				foreach (WidgetFactory.FieldWidgetData fieldWidgetData in fieldWidgets)
				{
					bool isHidden = fieldWidgetData.IsHidden;
					if (!isHidden)
					{
						bool isDisabled = fieldWidgetData.IsDisabled;
						if (isDisabled)
						{
							ImGui.BeginDisabled();
						}
						ImGui.TableNextColumn();
						ImGui.TextUnformatted(DisplayNameTranslator.Field(fieldWidgetData.Name, tableName));
						ImGui.TableNextColumn();
						ImGui.SetNextItemWidth(-1f);
						fieldWidgetData.Draw();
						ImGui.TableNextRow();
						bool isDisabled2 = fieldWidgetData.IsDisabled;
						if (isDisabled2)
						{
							ImGui.EndDisabled();
						}
					}
				}
				ImGui.EndTable();
			}
		}

		public static void DrawFullLayoutField(WidgetFactory.FieldWidgetData fieldWidget)
		{
			bool isHidden = fieldWidget.IsHidden;
			if (!isHidden)
			{
				ImGui.SetNextItemWidth(-1f);
				bool isDisabled = fieldWidget.IsDisabled;
				if (isDisabled)
				{
					ImGui.BeginDisabled();
				}
				WidgetFactory.DrawField(fieldWidget);
				bool isDisabled2 = fieldWidget.IsDisabled;
				if (isDisabled2)
				{
					ImGui.EndDisabled();
				}
			}
		}

		public static void DrawButton(string label, Action onClick, bool enabled = true)
		{
			bool flag = !enabled;
			if (flag)
			{
				ImGui.BeginDisabled();
			}
			bool flag2 = ImGui.Button(label);
			if (flag2)
			{
				onClick();
			}
			bool flag3 = !enabled;
			if (flag3)
			{
				ImGui.EndDisabled();
			}
		}

		public static void DrawSmallButton(string label, Action onClick, bool enabled = true)
		{
			bool flag = !enabled;
			if (flag)
			{
				ImGui.BeginDisabled();
			}
			bool flag2 = ImGui.SmallButton(label);
			if (flag2)
			{
				onClick();
			}
			bool flag3 = !enabled;
			if (flag3)
			{
				ImGui.EndDisabled();
			}
		}
	}
}
