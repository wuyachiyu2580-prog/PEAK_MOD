using System;
using System.Collections.Generic;
using ImGuiNET;
using UnityEngine;

namespace TerrainCustomiserCN.UI
{
	public class HierarchyView
	{
		public Transform Selected { get; private set; }

		public void DrawNode(Transform t)
		{
			bool flag = t == null;
			if (!flag)
			{
				bool flag2 = this.ShouldDrawNode != null && !this.ShouldDrawNode(t);
				if (!flag2)
				{
					ImGuiTreeNodeFlags imGuiTreeNodeFlags = ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanFullWidth;
					bool flag3 = this.Selected == t;
					if (flag3)
					{
						imGuiTreeNodeFlags |= ImGuiTreeNodeFlags.Selected;
					}
					ImGui.PushID(t.GetInstanceID());
					bool flag4 = t.childCount == 0;
					if (flag4)
					{
						imGuiTreeNodeFlags |= ImGuiTreeNodeFlags.Leaf;
					}
					bool flag5 = ImGui.TreeNodeEx("", imGuiTreeNodeFlags, DisplayNameTranslator.Dynamic(t.name, "HierarchyNode"));
					bool flag6 = ImGui.IsItemClicked() || ImGui.IsItemClicked(ImGuiMouseButton.Right);
					if (flag6)
					{
						this.SetSelected(t);
					}
					bool flag7 = ImGui.IsItemClicked(ImGuiMouseButton.Right);
					if (flag7)
					{
						ImGui.OpenPopup("HierarchyItemContextMenuPopup");
					}
					bool flag8 = ImGui.BeginPopup("HierarchyItemContextMenuPopup");
					if (flag8)
					{
						this.DrawContextNodes(t);
						ImGui.EndPopup();
					}
					bool flag9 = flag5;
					if (flag9)
					{
						for (int i = 0; i < t.childCount; i++)
						{
							this.DrawNode(t.GetChild(i));
						}
						ImGui.TreePop();
					}
					ImGui.PopID();
				}
			}
		}

		public void SetSelected(Transform t)
		{
			bool flag = this.Selected == t;
			if (!flag)
			{
				this.Selected = t;
				Action<Transform> onSelectionChanged = this.OnSelectionChanged;
				if (onSelectionChanged != null)
				{
					onSelectionChanged(t);
				}
			}
		}

		public HierarchyView.ContextNode AddContextMenu(string label, Func<Transform, bool> canUseNode)
		{
			HierarchyView.ContextNode contextNode = new HierarchyView.ContextNode
			{
				Name = label,
				CanUseNode = canUseNode
			};
			this.ContextNodes.Add(contextNode);
			return contextNode;
		}

		public void AddContextItem(string label, Action<Transform> action, Func<Transform, bool> canUseNode)
		{
			this.ContextNodes.Add(new HierarchyView.ContextNode
			{
				Name = label,
				Action = action,
				CanUseNode = canUseNode
			});
		}

		public void DrawContextNodes(Transform t)
		{
			foreach (HierarchyView.ContextNode contextNode in this.ContextNodes)
			{
				bool flag = true;
				bool flag2 = contextNode.CanUseNode != null;
				if (flag2)
				{
					flag = contextNode.CanUseNode(t);
				}
				bool flag3 = !flag;
				if (flag3)
				{
					ImGui.BeginDisabled();
				}
				this.DrawContextNode(contextNode, t);
				bool flag4 = !flag;
				if (flag4)
				{
					ImGui.EndDisabled();
				}
			}
		}

		private void DrawContextNode(HierarchyView.ContextNode node, Transform t)
		{
			bool flag = node.Children.Count > 0;
			if (flag)
			{
				this.DrawMenuNode(node, t);
			}
			else
			{
				this.DrawMenuItemNode(node, t);
			}
		}

		private void DrawMenuItemNode(HierarchyView.ContextNode node, Transform t)
		{
			bool flag = ImGui.MenuItem(node.DisplayName);
			if (flag)
			{
				Action<Transform> action = node.Action;
				if (action != null)
				{
					action(t);
				}
			}
		}

		private void DrawMenuNode(HierarchyView.ContextNode node, Transform t)
		{
			bool flag = ImGui.BeginMenu(node.DisplayName);
			if (flag)
			{
				foreach (HierarchyView.ContextNode node2 in node.Children)
				{
					this.DrawContextNode(node2, t);
				}
				ImGui.EndMenu();
			}
		}

		public Action<Transform> OnSelectionChanged;

		public Func<Transform, bool> ShouldDrawNode;

		public List<HierarchyView.ContextNode> ContextNodes = new List<HierarchyView.ContextNode>();

		public class ContextNode
		{
			public void AddContextItem(string label, Action<Transform> action, Func<Transform, bool> canUseNode)
			{
				this.Children.Add(new HierarchyView.ContextNode
				{
					Name = label,
					Action = action,
					CanUseNode = canUseNode
				});
			}

			public string Name;

			public string DisplayName
			{
				get
				{
					return DisplayNameTranslator.Context(this.Name, "HierarchyContext") + "##" + this.Name;
				}
			}

			public List<HierarchyView.ContextNode> Children = new List<HierarchyView.ContextNode>();

			public Action<Transform> Action;

			public Func<Transform, bool> CanUseNode;
		}
	}
}
