using System;
using System.Collections.Generic;
using ImGuiNET;
using TerrainCustomiserCN.Managers;
using TerrainCustomiserCN.Map;
using TerrainCustomiserCN.UI.Modals;
using UnityEngine;
using Zorro.Core;

namespace TerrainCustomiserCN.UI.Windows
{
	public class EditorWindow : ImGuiWindowBase
	{
		public override string WindowName
		{
			get
			{
				return "编辑器";
			}
		}

		public override ImGuiWindowFlags WindowFlags
		{
			get
			{
				return ImGuiWindowFlags.AlwaysAutoResize;
			}
		}

		public override void OnCreate()
		{
			base.OnCreate();
			this.IsOpen = true;
			Singleton<EditorManager>.Instance.SetCurrentSegment(MapData.biomeSections[0].activeBiome.segments[0]);
			this.SetupHierarchy();
		}

		private void SetupHierarchy()
		{
			HierarchyView hierarchyView = this.hierarchy;
			hierarchyView.OnSelectionChanged = (Action<Transform>)Delegate.Combine(hierarchyView.OnSelectionChanged, new Action<Transform>(delegate(Transform t)
			{
				WindowsManager.Get<InspectorWindow>().SetTarget(t);
			}));
			this.hierarchy.ShouldDrawNode = ((Transform t) => t.GetComponent<LevelGenStep>() != null || t.GetComponent<PropGrouper>() != null || (t.GetComponents<Component>().Length == 1 && t.parent.GetComponent<PropGrouper>() != null));
			HierarchyView.ContextNode contextNode = this.hierarchy.AddContextMenu("创建生成器", (Transform t) => t == null || t.GetComponent<LevelGenStep>() == null);
			using (List<Type>.Enumerator enumerator = Singleton<EditorManager>.Instance.levelGenStepTypes.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					Type item = enumerator.Current;
					contextNode.AddContextItem(item.Name, delegate(Transform t)
					{
						Singleton<EditorManager>.Instance.CreateObjectWithComponent(item.Name, t, item, Camera.main.transform.position, Camera.main.transform.rotation);
					}, null);
				}
			}
			this.hierarchy.AddContextItem("创建分组器", delegate(Transform t)
			{
				Singleton<EditorManager>.Instance.CreateObjectWithComponent("PropGrouper", t, typeof(PropGrouper), Camera.main.transform.position, Camera.main.transform.rotation);
			}, (Transform t) => t == null || t.GetComponent<LevelGenStep>() == null);
			this.hierarchy.AddContextItem("复制", delegate(Transform t)
			{
				Singleton<EditorManager>.Instance.DuplicateObject(t);
			}, (Transform t) => t != null);
			this.hierarchy.AddContextItem("重命名", delegate(Transform t)
			{
				RenameModal.RequestOpen(t);
			}, (Transform t) => t != null);
			this.hierarchy.AddContextItem("删除", delegate(Transform t)
			{
				Singleton<EditorManager>.Instance.DestroyObject(t.gameObject);
			}, (Transform t) => t != null);
		}

		protected override void DrawContent()
		{
			this.DrawControlsList();
			this.DrawBiomeTable();
			ImGui.Separator();
			this.DrawHierarchy();
		}

		private void DrawControlsList()
		{
			ImGuiHelpers.DrawSmallButton("生成", new Action(Singleton<EditorManager>.Instance.GenerateCurrentSegment), true);
			ImGui.SameLine();
			ImGuiHelpers.DrawSmallButton("保存", new Action(SaveModal.Open), true);
			ImGui.SameLine();
			ImGuiHelpers.DrawSmallButton("加载", new Action(LoadModal.Open), true);
			ImGui.SameLine();
			ImGuiHelpers.DrawSmallButton("相机", new Action(WindowsManager.Get<FreeCamWindow>().ToggleOpen), true);
			ImGui.SameLine();
			ImGuiHelpers.DrawSmallButton("辅助线", new Action(EditorGizmos.ToggleGizmos), true);
			SaveModal.Draw();
			LoadModal.Draw();
		}

		private void SetSelectedSegment(MapData.BiomeSegment biomeSegment)
		{
			this.selectedSegmentNodeInstanceId = biomeSegment.transform.GetInstanceID();
			this.hierarchyTargetSegment = biomeSegment;
			Singleton<EditorManager>.Instance.SetCurrentSegment(biomeSegment);
		}

		private void DrawBiomeTable()
		{
			bool flag = ImGui.BeginTable("Table_Biomes", 3);
			if (flag)
			{
				ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthFixed, 100f);
				ImGui.TableSetupColumn("类型", ImGuiTableColumnFlags.WidthFixed, 60f);
				ImGui.TableSetupColumn("按钮", ImGuiTableColumnFlags.WidthFixed, 50f);
				foreach (MapData.BiomeSection biomeSection in MapData.biomeSections)
				{
					this.DrawBiomeNode(biomeSection);
				}
				ImGui.EndTable();
			}
		}

		private void DrawBiomeNode(MapData.BiomeSection biomeSection)
		{
			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			int instanceID = biomeSection.activeBiome.transform.GetInstanceID();
			ImGui.PushID(instanceID);
			ImGui.SetNextItemAllowOverlap();
			ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.SpanAllColumns;
			ImGui.PushStyleColor(ImGuiCol.Header, Color.black);
			ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Color.black);
			bool flag = ImGui.TreeNodeEx(DisplayNameTranslator.Dynamic(biomeSection.activeBiome.name, "Biome") + "##" + instanceID, flags);
			ImGui.PopStyleColor(2);
			ImGui.TableNextColumn();
			ImGui.TextUnformatted(DisplayNameTranslator.Dynamic(biomeSection.name, "BiomeSection"));
			ImGui.TableNextColumn();
			bool flag2 = ImGui.SmallButton("切换");
			if (flag2)
			{
				SwapBiomeModal.Open(biomeSection);
			}
			SwapBiomeModal.Draw();
			bool flag3 = flag;
			if (flag3)
			{
				foreach (MapData.BiomeSegment biomeSegment in biomeSection.activeBiome.segments)
				{
					this.DrawSegmentNode(biomeSegment);
				}
				ImGui.TreePop();
			}
			ImGui.PopID();
		}

		private void DrawSegmentNode(MapData.BiomeSegment biomeSegment)
		{
			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			int instanceID = biomeSegment.transform.GetInstanceID();
			ImGui.PushID(instanceID);
			ImGui.SetNextItemAllowOverlap();
			ImGuiTreeNodeFlags imGuiTreeNodeFlags = ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.SpanAllColumns;
			bool flag = instanceID == this.selectedSegmentNodeInstanceId;
			if (flag)
			{
				imGuiTreeNodeFlags |= ImGuiTreeNodeFlags.Selected;
			}
			bool flag2 = ImGui.TreeNodeEx(DisplayNameTranslator.Dynamic(biomeSegment.name, "BiomeSegment") + "##" + instanceID, imGuiTreeNodeFlags);
			bool flag3 = ImGui.IsItemClicked();
			if (flag3)
			{
				this.SetSelectedSegment(biomeSegment);
			}
			ImGui.TableNextColumn();
			ImGui.TextUnformatted((biomeSegment.activeVariant != null) ? DisplayNameTranslator.Dynamic(biomeSegment.activeVariant.name, "SegmentVariant") : "无");
			ImGui.TableNextColumn();
			MapData.SegmentVariant customVariant = biomeSegment.customVariant;
			ImGuiHelpers.DrawSmallButton("建", delegate
			{
				CreateVariantModal.Open(biomeSegment);
			}, customVariant == null);
			bool flag4 = customVariant != null;
			if (flag4)
			{
				ImGui.SameLine();
				bool flag5 = ImGui.SmallButton("删");
				if (flag5)
				{
					DeleteSegmentModal.Open(customVariant);
				}
			}
			CreateVariantModal.Draw();
			DeleteSegmentModal.Draw();
			ImGui.PopID();
		}

		private void DrawHierarchy()
		{
			bool flag = ImGui.BeginChild("Hierarchy", new Vector2(ImGui.GetContentRegionAvail().x, 220f));
			if (flag)
			{
				bool flag2 = this.hierarchyTargetSegment == null || this.hierarchyTargetSegment.customVariant == null;
				if (flag2)
				{
					ImGui.TextUnformatted("没有自定义变体。");
					ImGui.EndChild();
				}
				else
				{
					foreach (object obj in this.hierarchyTargetSegment.customVariant.transform)
					{
						Transform t = (Transform)obj;
						this.hierarchy.DrawNode(t);
					}
					bool openRequested = RenameModal.OpenRequested;
					if (openRequested)
					{
						RenameModal.Open();
					}
					RenameModal.Draw();
					bool flag3 = ImGui.BeginPopupContextWindow("HierarchyWindowContextMenuPopup", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverExistingPopup | ImGuiPopupFlags.NoOpenOverItems);
					if (flag3)
					{
						this.hierarchy.DrawContextNodes(null);
						ImGui.EndPopup();
					}
					ImGui.EndChild();
				}
			}
		}

		public HierarchyView hierarchy = new HierarchyView();

		private int selectedSegmentNodeInstanceId;

		private MapData.BiomeSegment hierarchyTargetSegment;
	}
}
