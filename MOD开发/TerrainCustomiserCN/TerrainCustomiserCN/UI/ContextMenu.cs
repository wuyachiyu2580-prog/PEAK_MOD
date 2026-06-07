using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ImGuiNET;

namespace TerrainCustomiserCN.UI
{
	public static class ContextMenu
	{
		public static void GetTypes(Type type)
		{
			ContextMenu.types = ContextMenu.GetConcreteDerivedTypes(type);
		}

		public static void CreateMenu(IList list, Type type)
		{
			bool flag = ImGui.BeginPopupContextItem("WidgetContextMenu");
			if (flag)
			{
				for (int i = 0; i < ContextMenu.types.Length; i++)
				{
					Type type2 = ContextMenu.types[i];
					bool flag2 = ImGui.Selectable(DisplayNameTranslator.TypeName(type2, "ContextMenu") + "##" + type2.FullName);
					if (flag2)
					{
						object value = Activator.CreateInstance(type2);
						list.Add(value);
						ContextMenu.types = null;
						break;
					}
				}
				ImGui.EndPopup();
			}
		}

		private static Type[] GetConcreteDerivedTypes(Type baseType)
		{
			return (from t in AppDomain.CurrentDomain.GetAssemblies().SelectMany((Assembly a) => a.GetTypes())
			where baseType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface
			select t).ToArray<Type>();
		}

		private static Type[] types;
	}
}
