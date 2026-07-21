using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using UnityEngine;

namespace TerrainCustomiserCN.UI.Windows
{
	public class ResourceWindow : ImGuiWindowBase
	{
		public override string WindowName
		{
			get
			{
				return "资源";
			}
		}

		public override ImGuiWindowFlags WindowFlags
		{
			get
			{
				return ImGuiWindowFlags.AlwaysAutoResize;
			}
		}

		public static void Show<T>(Func<T> get, Action<T> set) where T : UnityEngine.Object
		{
			ResourceWindow._get = (() => get());
			ResourceWindow._set = delegate(UnityEngine.Object o)
			{
				set((T)((object)o));
			};
			ResourceWindow.RebuildList<T>();
			T t = get();
			ResourceWindow._selectedIndex = ((t != null) ? ResourceWindow._items.IndexOf(t) : -1);
		}

		private static void RebuildList<T>() where T : UnityEngine.Object
		{
			ResourceWindow._items.Clear();
			T[] array = Resources.FindObjectsOfTypeAll<T>();
			bool flag = typeof(T) == typeof(GameObject);
			if (flag)
			{
				foreach (GameObject gameObject in array.Cast<GameObject>())
				{
					bool flag2 = gameObject == null;
					if (!flag2)
					{
						bool flag3 = gameObject.transform.parent == null && !gameObject.scene.IsValid();
						if (flag3)
						{
							ResourceWindow._items.Add(gameObject);
						}
					}
				}
			}
			else
			{
				bool flag4 = typeof(T) == typeof(Transform);
				if (flag4)
				{
					ResourceWindow.GetTransformsFromRoot(GameObject.Find("Custom").transform);
				}
				else
				{
					foreach (T t in array)
					{
						bool flag5 = t == null;
						if (!flag5)
						{
							ResourceWindow._items.Add(t);
						}
					}
				}
			}
			ResourceWindow._items.Sort((UnityEngine.Object a, UnityEngine.Object b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
		}

		private static void GetTransformsFromRoot(Transform t)
		{
			ResourceWindow._items.Add(t);
			for (int i = 0; i < t.childCount; i++)
			{
				ResourceWindow.GetTransformsFromRoot(t.GetChild(i));
			}
		}

		protected override void DrawContent()
		{
			WidgetFactory.FieldWidgetData[] array = new WidgetFactory.FieldWidgetData[1];
			array[0] = new WidgetFactory.FieldWidgetData(typeof(string), () => ResourceWindow.filterBuffer, delegate(object v)
			{
				ResourceWindow.filterBuffer = (string)v;
				}, "筛选", null, false, false);
			WidgetFactory.FieldWidgetData[] fieldWidgets = array;
			ImGuiHelpers.DrawPropertiesTable("ResourceWindowTable", fieldWidgets);
			bool flag = ImGui.BeginListBox("##resource_list", new Vector2(300f, 200f));
			if (flag)
			{
				for (int i = 0; i < ResourceWindow._items.Count; i++)
				{
					UnityEngine.Object @object = ResourceWindow._items[i];
					string rawName = (@object != null) ? @object.name : string.Empty;
					string displayName = (@object != null) ? DisplayNameTranslator.Dynamic(rawName, "Resource") : "<空>";
					// 筛选同时匹配资源原名和中文显示名，方便按两种语言查找。
					bool flag2 = !ResourceWindow.MatchesFilter(rawName, displayName, ResourceWindow.filterBuffer);
					if (!flag2)
					{
						bool flag3 = ResourceWindow._selectedIndex == i;
						string label = ResourceWindow.BuildSelectableLabel(displayName, rawName, i);
						bool flag4 = ImGui.Selectable(label, flag3, ImGuiSelectableFlags.AllowDoubleClick);
						if (flag4)
						{
							ResourceWindow._selectedIndex = i;
							bool flag5 = ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
							if (flag5)
							{
								bool flag6 = @object != null && ResourceWindow._set != null;
								if (flag6)
								{
									ResourceWindow._set(@object);
								}
								ResourceWindow._get = null;
								ResourceWindow._set = null;
								this.IsOpen = false;
							}
						}
						bool flag7 = flag3;
						if (flag7)
						{
							ImGui.SetItemDefaultFocus();
						}
					}
				}
				ImGui.EndListBox();
			}
			bool flag8 = ImGui.Button("取消");
			if (flag8)
			{
				this.IsOpen = false;
			}
		}

		private static bool MatchesFilter(string rawName, string displayName, string filter)
		{
			if (string.IsNullOrEmpty(filter))
			{
				return true;
			}
			return ResourceWindow.ContainsText(rawName, filter) || ResourceWindow.ContainsText(displayName, filter);
		}

		private static bool ContainsText(string value, string filter)
		{
			return !string.IsNullOrEmpty(value) && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static string BuildSelectableLabel(string displayName, string rawName, int index)
		{
			if (string.IsNullOrEmpty(displayName))
			{
				displayName = "<空>";
			}
			// 中文名可能重复，隐藏 ID 保留原始资源名和序号，避免 ImGui 选项冲突。
			return displayName + "##resource_" + index.ToString() + "_" + rawName;
		}

		private static int _selectedIndex = -1;

		private static readonly List<UnityEngine.Object> _items = new List<UnityEngine.Object>();

		private static string filterBuffer = string.Empty;

		private static Func<UnityEngine.Object> _get;

		private static Action<UnityEngine.Object> _set;
	}
}
