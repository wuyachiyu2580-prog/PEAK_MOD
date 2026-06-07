using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using TerrainCustomiserCN.Managers;
using UnityEngine;
using Zorro.Core;

namespace TerrainCustomiserCN.UI.Windows
{
	public class InspectorWindow : ImGuiWindowBase
	{
		public override string WindowName
		{
			get
			{
				return "检查器";
			}
		}

		public override Vector2 InitialWindowSize
		{
			get
			{
				return new Vector2(300f, 600f);
			}
		}

		public override void OnCreate()
		{
			base.OnCreate();
			this.IsOpen = true;
			foreach (Type item in Singleton<EditorManager>.Instance.levelGenStepTypes)
			{
				InspectorWindow.allowedTypes.Add(item);
			}
		}

		public void SetTarget(Transform targetTransform)
		{
			bool flag = targetTransform == null;
			if (flag)
			{
				this.targetGO = null;
			}
			else
			{
				this.targetGO = targetTransform.gameObject;
				Component[] components = this.targetGO.GetComponents<Component>();
				foreach (Component component in components)
				{
					Type type = component.GetType();
					bool flag2 = !InspectorWindow.allowedTypes.Contains(type);
					if (!flag2)
					{
						bool flag3 = !WidgetFactory.TypeFieldDataDictionary.ContainsKey(type);
						if (flag3)
						{
							WidgetFactory.FieldData[] specialFieldData = WidgetFactory.GetSpecialFieldData(type);
							WidgetFactory.TypeFieldDataDictionary.Add(type, specialFieldData);
						}
					}
				}
			}
		}

		private void DrawTransformComponent(Transform targetTransform)
		{
			bool flag = ImGui.TreeNodeEx("变换", ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.DefaultOpen);
			if (flag)
			{
				WidgetFactory.FieldWidgetData[] fieldWidgets = new WidgetFactory.FieldWidgetData[]
				{
					new WidgetFactory.FieldWidgetData(typeof(Vector3), () => targetTransform.position, delegate(object v)
					{
						targetTransform.position = (Vector3)v;
					}, "位置", null, false, false),
					new WidgetFactory.FieldWidgetData(typeof(Vector3), () => targetTransform.eulerAngles, delegate(object v)
					{
						targetTransform.eulerAngles = (Vector3)v;
					}, "旋转", null, false, false)
				};
				ImGuiHelpers.DrawPropertiesTable("transformTable", fieldWidgets);
				ImGui.TreePop();
			}
		}

		protected override void DrawContent()
		{
			bool flag = this.targetGO == null;
			if (!flag)
			{
				Component[] components = this.targetGO.GetComponents<Component>();
				Component[] array = components;
				for (int i = 0; i < array.Length; i++)
				{
					Component component = array[i];
					Type type = component.GetType();
					bool flag2 = !InspectorWindow.allowedTypes.Contains(type);
					if (!flag2)
					{
						bool flag3 = type == typeof(Transform);
						if (flag3)
						{
							this.DrawTransformComponent(this.targetGO.transform);
						}
						else
						{
							WidgetFactory.FieldData[] array2 = WidgetFactory.TypeFieldDataDictionary[type];
							bool flag4 = array2 == null;
							if (!flag4)
							{
								bool flag5 = ImGui.TreeNodeEx(DisplayNameTranslator.TypeName(type, "Inspector") + "##" + type.FullName, ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.DefaultOpen);
								if (flag5)
								{
									IEnumerable<WidgetFactory.FieldData> enumerable = from f in array2
									where f.layout == WidgetFactory.FieldLayout.Inline
									select f;
									IEnumerable<WidgetFactory.FieldData> enumerable2 = from f in array2
									where f.layout == WidgetFactory.FieldLayout.Full
									select f;
									List<WidgetFactory.FieldWidgetData> list = new List<WidgetFactory.FieldWidgetData>();
									using (IEnumerator<WidgetFactory.FieldData> enumerator = enumerable.GetEnumerator())
									{
										while (enumerator.MoveNext())
										{
											WidgetFactory.FieldData tableField = enumerator.Current;
											WidgetFactory.FieldWidgetData item = new WidgetFactory.FieldWidgetData(tableField.fieldInfo.FieldType, () => tableField.fieldInfo.GetValue(component), delegate(object v)
											{
												tableField.fieldInfo.SetValue(component, v);
											}, tableField.name, tableField.rangeAttribute, tableField.widgetMode == WidgetFactory.WidgetMode.Hidden, tableField.widgetMode == WidgetFactory.WidgetMode.Disabled);
											list.Add(item);
										}
									}
									ImGuiHelpers.DrawPropertiesTable(component.name, list.ToArray());
									using (IEnumerator<WidgetFactory.FieldData> enumerator2 = enumerable2.GetEnumerator())
									{
										while (enumerator2.MoveNext())
										{
											WidgetFactory.FieldData data = enumerator2.Current;
											WidgetFactory.FieldWidgetData fieldWidget = new WidgetFactory.FieldWidgetData(data.fieldInfo.FieldType, () => data.fieldInfo.GetValue(component), delegate(object v)
											{
												data.fieldInfo.SetValue(component, v);
											}, data.name, data.rangeAttribute, data.widgetMode == WidgetFactory.WidgetMode.Hidden, data.widgetMode == WidgetFactory.WidgetMode.Disabled);
											ImGuiHelpers.DrawFullLayoutField(fieldWidget);
										}
									}
									ImGui.TreePop();
								}
							}
						}
					}
				}
			}
		}

		private GameObject targetGO;

		public static List<Type> allowedTypes = new List<Type>
		{
			typeof(PropGrouper),
			typeof(Transform),
			typeof(RockMaterialSwapper),
			typeof(SpecialDayZone)
		};
	}
}
