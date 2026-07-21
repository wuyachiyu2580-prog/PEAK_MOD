using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImGuiNET;
using TerrainCustomiserCN.Managers;
using TerrainCustomiserCN.UI.Windows;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerrainCustomiserCN.UI
{
	public static class WidgetFactory
	{
		public static void DrawField(object obj, WidgetFactory.FieldData fieldData)
		{
			FieldInfo f = fieldData.fieldInfo;
			WidgetFactory.DrawField(f.FieldType, () => f.GetValue(obj), delegate(object v)
			{
				f.SetValue(obj, v);
			}, f.Name, fieldData.rangeAttribute);
		}

		public static void DrawField(WidgetFactory.FieldWidgetData fieldWidgetData)
		{
			WidgetFactory.DrawField(fieldWidgetData.Type, fieldWidgetData.Get, fieldWidgetData.Set, fieldWidgetData.Name, fieldWidgetData.RangeAttribute);
		}

		public static void DrawField(Type t, Func<object> get, Action<object> set, string id, RangeAttribute rangeAttribute = null)
		{
			ImGui.PushID(id);
			bool flag = t == typeof(int);
			if (flag)
			{
				WidgetFactory.DrawInt(() => (int)get(), delegate(int v)
				{
					set(v);
				});
			}
			else
			{
				bool flag2 = t == typeof(float);
				if (flag2)
				{
					bool flag3 = rangeAttribute != null;
					if (flag3)
					{
						WidgetFactory.DrawSliderFloat(() => (float)get(), delegate(float v)
						{
							set(v);
						}, rangeAttribute.min, rangeAttribute.max);
					}
					else
					{
						WidgetFactory.DrawFloat(() => (float)get(), delegate(float v)
						{
							set(v);
						});
					}
				}
				else
				{
					bool flag4 = t == typeof(string);
					if (flag4)
					{
						WidgetFactory.DrawString(() => (string)get(), delegate(string v)
						{
							set(v);
						});
					}
					else
					{
						bool flag5 = t == typeof(Vector2);
						if (flag5)
						{
							WidgetFactory.DrawVector2(() => (Vector2)get(), delegate(Vector2 v)
							{
								set(v);
							});
						}
						else
						{
							bool flag6 = t == typeof(Vector2Int);
							if (flag6)
							{
								WidgetFactory.DrawVector2Int(() => (Vector2Int)get(), delegate(Vector2Int v)
								{
									set(v);
								});
							}
							else
							{
								bool flag7 = t == typeof(Vector3);
								if (flag7)
								{
									WidgetFactory.DrawVector3(() => (Vector3)get(), delegate(Vector3 v)
									{
										set(v);
									});
								}
								else
								{
									bool flag8 = t == typeof(Vector3Int);
									if (flag8)
									{
										WidgetFactory.DrawVector3Int(() => (Vector3Int)get(), delegate(Vector3Int v)
										{
											set(v);
										});
									}
									else
									{
										bool flag9 = t == typeof(Vector4);
										if (flag9)
										{
											WidgetFactory.DrawVector4(() => (Vector4)get(), delegate(Vector4 v)
											{
												set(v);
											});
										}
										else
										{
											bool flag10 = t == typeof(Bounds);
											if (flag10)
											{
												WidgetFactory.DrawBounds(() => (Bounds)get(), delegate(Bounds v)
												{
													set(v);
												});
											}
											else
											{
												bool flag11 = t == typeof(bool);
												if (flag11)
												{
													WidgetFactory.DrawBool(() => (bool)get(), delegate(bool v)
													{
														set(v);
													});
												}
												else
												{
													bool isEnum = t.IsEnum;
													if (isEnum)
													{
														WidgetFactory.DrawEnum(() => (Enum)get(), delegate(Enum v)
														{
															set(v);
														});
													}
													else
													{
														bool flag12 = t == typeof(Color);
														if (flag12)
														{
															WidgetFactory.DrawColor(() => (Color)get(), delegate(Color v)
															{
																set(v);
															});
														}
														else
														{
															bool flag13 = t == typeof(GameObject);
															if (flag13)
															{
																WidgetFactory.DrawGameObject(() => (GameObject)get(), delegate(GameObject v)
																{
																	set(v);
																});
															}
															else
															{
																bool flag14 = t == typeof(Material);
																if (flag14)
																{
																	WidgetFactory.DrawMaterial(() => (Material)get(), delegate(Material v)
																	{
																		set(v);
																	});
																}
																else
																{
																	bool flag15 = t == typeof(Transform);
																	if (flag15)
																	{
																		WidgetFactory.DrawTransform(() => (Transform)get(), delegate(Transform v)
																		{
																			set(v);
																		});
																	}
																	else
																	{
																		bool isArray = t.IsArray;
																		if (isArray)
																		{
																			WidgetFactory.DrawArray(t.GetElementType(), () => (Array)get(), delegate(Array a)
																			{
																				set(a);
																			}, id);
																		}
																		else
																		{
																			bool flag16 = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
																			if (flag16)
																			{
																				WidgetFactory.DrawList(t.GetGenericArguments()[0], () => (IList)get(), id);
																			}
																			else
																			{
																				bool flag17 = t.IsClass && !typeof(Object).IsAssignableFrom(t);
																				if (flag17)
																				{
																					WidgetFactory.DrawClass(get, set, t);
																				}
																				else
																				{
																					ImGui.TextDisabled(t.ToString() + " 未实现");
																				}
																			}
																		}
																	}
																}
															}
														}
													}
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
			ImGui.PopID();
		}

		private static void DrawClass(Func<object> get, Action<object> set, Type classType)
		{
			object obj = get();
			bool flag = obj == null;
			if (flag)
			{
				bool flag2 = ImGui.SmallButton("创建");
				if (flag2)
				{
					obj = Activator.CreateInstance(classType);
					set(obj);
				}
			}
			else
			{
				Type type = obj.GetType();
				Type objectType = obj.GetType();
				bool flag3 = ImGui.TreeNode(DisplayNameTranslator.TypeName(objectType, "WidgetFactory.DrawClass") + "##" + objectType.FullName);
				if (flag3)
				{
					List<WidgetFactory.FieldWidgetData> list = new List<WidgetFactory.FieldWidgetData>();
					foreach (FieldInfo fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
					{
						WidgetFactory.FieldWidgetData item = new WidgetFactory.FieldWidgetData(WidgetFactory.GetFieldData(fieldInfo), obj);
						list.Add(item);
					}
					ImGuiHelpers.DrawPropertiesTable(obj.GetType().Name, list.ToArray());
					ImGui.TreePop();
				}
			}
		}

		private static void DrawInt(Func<int> get, Action<int> set)
		{
			int obj = get();
			bool flag = ImGui.InputInt("##v", ref obj);
			if (flag)
			{
				set(obj);
			}
		}

		private static void DrawFloat(Func<float> get, Action<float> set)
		{
			float obj = get();
			bool flag = ImGui.InputFloat("##v", ref obj);
			if (flag)
			{
				set(obj);
			}
		}

		private static void DrawString(Func<string> get, Action<string> set)
		{
			string text = get();
			bool flag = text == null;
			if (flag)
			{
				text = "";
			}
			bool flag2 = ImGui.InputText("##v", ref text, 32U);
			if (flag2)
			{
				set(text);
			}
		}

		private static void DrawSliderFloat(Func<float> get, Action<float> set, float min, float max)
		{
			float obj = get();
			bool flag = ImGui.SliderFloat("##v", ref obj, min, max);
			if (flag)
			{
				set(obj);
			}
		}

		private static void DrawVector2(Func<Vector2> get, Action<Vector2> set)
		{
			Vector2 obj = get();
			bool flag = ImGui.InputFloat2("##v", ref obj);
			if (flag)
			{
				set(obj);
			}
		}

		private static void DrawVector2Int(Func<Vector2Int> get, Action<Vector2Int> set)
		{
			Vector2Int vector2Int = get();
			int[] array = new int[]
			{
				vector2Int.x,
				vector2Int.y
			};
			bool flag = ImGui.InputInt2("##v", ref array[0]);
			if (flag)
			{
				set(new Vector2Int(array[0], array[1]));
			}
		}

		public static void DrawVector3(Func<Vector3> get, Action<Vector3> set)
		{
			Vector3 obj = get();
			bool flag = ImGui.InputFloat3("##v", ref obj);
			if (flag)
			{
				set(obj);
			}
		}

		private static void DrawVector3Int(Func<Vector3Int> get, Action<Vector3Int> set)
		{
			Vector3Int vector3Int = get();
			int[] array = new int[]
			{
				vector3Int.x,
				vector3Int.y,
				vector3Int.z
			};
			bool flag = ImGui.InputInt3("##v", ref array[0]);
			if (flag)
			{
				set(new Vector3Int(array[0], array[1], array[2]));
			}
		}

		public static void DrawVector4(Func<Vector4> get, Action<Vector4> set)
		{
			Vector4 obj = get();
			bool flag = ImGui.InputFloat4("##v", ref obj);
			if (flag)
			{
				set(obj);
			}
		}

		public static void DrawBounds(Func<Bounds> get, Action<Bounds> set)
		{
			Bounds obj = get();
			Vector3 center = obj.center;
			Vector3 extents = obj.extents;
			ImGui.TextUnformatted("中心");
			ImGui.SameLine();
			ImGui.SetNextItemWidth(-1f);
			bool flag = ImGui.InputFloat3("##vc", ref center);
			if (flag)
			{
				obj.center = center;
				set(obj);
			}
			ImGui.TextUnformatted("范围");
			ImGui.SameLine();
			ImGui.SetNextItemWidth(-1f);
			bool flag2 = ImGui.InputFloat3("##ve", ref extents);
			if (flag2)
			{
				obj.extents = extents;
				set(obj);
			}
		}

		private static void DrawBool(Func<bool> get, Action<bool> set)
		{
			bool obj = get();
			bool flag = ImGui.Checkbox("##v", ref obj);
			if (flag)
			{
				set(obj);
			}
		}

		private static void DrawEnum(Func<Enum> get, Action<Enum> set)
		{
			Enum @enum = get();
			int value = Convert.ToInt32(@enum);
			Type type = @enum.GetType();
			string[] names = Enum.GetNames(type);
			string[] displayNames = names.Select((string name) => DisplayNameTranslator.EnumName(type, name)).ToArray();
			bool flag = ImGui.Combo("##v", ref value, displayNames, displayNames.Length);
			if (flag)
			{
				set((Enum)Enum.ToObject(type, value));
			}
		}

		public static void DrawCombo(int selectedIndex, Func<string[]> get, Action<int> set)
		{
			string[] array = get();
			string[] displayOptions = array.Select((string option) => DisplayNameTranslator.Dynamic(option, "Combo")).ToArray();
			bool flag = ImGui.Combo("##v", ref selectedIndex, displayOptions, displayOptions.Length);
			if (flag)
			{
				set(selectedIndex);
			}
		}

		private static void DrawColor(Func<Color> get, Action<Color> set)
		{
			Vector4 vector = get();
			bool flag = ImGui.ColorEdit4("##v", ref vector);
			if (flag)
			{
				set(vector);
			}
		}

		private static void DrawGameObject(Func<GameObject> get, Action<GameObject> set)
		{
			GameObject gameObject = get();
			string text = (gameObject != null) ? gameObject.name : "无";
			ImGui.TextUnformatted(text);
			ImGui.SameLine();
			bool flag = ImGui.SmallButton("更改");
			if (flag)
			{
				ResourceWindow.Show<GameObject>(get, set);
				WindowsManager.Get<ResourceWindow>().Open();
			}
		}

		public static void DrawMaterial(Func<Material> get, Action<Material> set)
		{
			Material material = get();
			string text = (material != null) ? material.name : "无";
			ImGui.TextUnformatted(text);
			ImGui.SameLine();
			bool flag = ImGui.SmallButton("更改");
			if (flag)
			{
				ResourceWindow.Show<Material>(get, set);
				WindowsManager.Get<ResourceWindow>().Open();
			}
		}

		private static void DrawTransform(Func<Transform> get, Action<Transform> set)
		{
			Transform transform = get();
			string label = (transform != null) ? transform.name : "无";
			ImGui.SetNextItemAllowOverlap();
			bool flag = ImGui.Selectable(label);
			if (flag)
			{
			}
			ImGui.SameLine();
			bool flag2 = ImGui.SmallButton("更改");
			if (flag2)
			{
				ResourceWindow.Show<Transform>(get, set);
				WindowsManager.Get<ResourceWindow>().Open();
			}
		}

		private static void DrawArray(Type elementType, Func<Array> get, Action<Array> set, string name)
		{
			Array array = get();
			bool flag = array == null;
			if (flag)
			{
				ImGui.TextUnformatted(DisplayNameTranslator.Field(name, "Array"));
				ImGui.SameLine();
				bool flag2 = ImGui.SmallButton(DisplayNameTranslator.Ui("Create"));
				if (flag2)
				{
					array = Array.CreateInstance(elementType, 0);
					set(array);
				}
			}
			else
			{
				int num = -1;
				bool flag3 = ImGui.TreeNode("Array_" + name, string.Format("{0} [{1}]", DisplayNameTranslator.Field(name, "Array"), array.Length));
				if (flag3)
				{
					for (int i = 0; i < array.Length; i++)
					{
						ImGui.PushID(i);
						int index = i;
						WidgetFactory.DrawField(elementType, () => array.GetValue(index), delegate(object v)
						{
							array.SetValue(v, index);
						}, index.ToString(), null);
						ImGui.SameLine();
						ImGui.PushID("Remove");
						bool flag4 = ImGui.SmallButton(DisplayNameTranslator.Ui("Remove"));
						if (flag4)
						{
							num = index;
						}
						ImGui.PopID();
						ImGui.PopID();
					}
					bool flag5 = ImGui.SmallButton(DisplayNameTranslator.Ui("Add"));
					if (flag5)
					{
						WidgetFactory.AddElement(array, set);
					}
					ImGui.TreePop();
				}
				bool flag6 = num >= 0;
				if (flag6)
				{
					WidgetFactory.RemoveAt(array, set, num);
				}
			}
		}

		private static void DrawList(Type elementType, Func<IList> get, string name)
		{
			IList list = get();
			bool flag = list == null;
			if (flag)
			{
				ImGui.TextUnformatted(DisplayNameTranslator.Field(name, "List"));
				ImGui.SameLine();
				bool flag2 = ImGui.SmallButton(DisplayNameTranslator.Ui("Create"));
				if (flag2)
				{
					Type type = typeof(List<>).MakeGenericType(new Type[]
					{
						elementType
					});
					list = (IList)Activator.CreateInstance(type);
				}
			}
			else
			{
				int num = -1;
				bool flag3 = ImGui.TreeNode("List_" + name, string.Format("{0} [{1}]", DisplayNameTranslator.Field(name, "List"), list.Count));
				if (flag3)
				{
					for (int i = 0; i < list.Count; i++)
					{
						ImGui.PushID(i);
						int index = i;
						WidgetFactory.DrawField(elementType, () => list[index], delegate(object v)
						{
							list[index] = v;
						}, index.ToString(), null);
						ImGui.SameLine();
						ImGui.PushID("Remove");
						bool flag4 = ImGui.SmallButton(DisplayNameTranslator.Ui("Remove"));
						if (flag4)
						{
							num = index;
						}
						ImGui.PopID();
						ImGui.PopID();
					}
					ContextMenu.CreateMenu(list, elementType);
					bool flag5 = ImGui.SmallButton(DisplayNameTranslator.Ui("Add"));
					if (flag5)
					{
						bool isAbstract = elementType.IsAbstract;
						if (isAbstract)
						{
							ContextMenu.GetTypes(elementType);
							ImGui.OpenPopup("WidgetContextMenu");
						}
						else
						{
							bool flag6 = elementType == typeof(string);
							if (flag6)
							{
								list.Add(string.Empty);
							}
							else
							{
								object value = Activator.CreateInstance(elementType);
								list.Add(value);
							}
						}
					}
					ImGui.TreePop();
				}
				bool flag7 = num >= 0;
				if (flag7)
				{
					list.RemoveAt(num);
				}
			}
		}

		private static void RemoveAt(Array array, Action<Array> set, int index)
		{
			int length = array.Length;
			Type elementType = array.GetType().GetElementType();
			Array array2 = Array.CreateInstance(elementType, length - 1);
			bool flag = index > 0;
			if (flag)
			{
				Array.Copy(array, 0, array2, 0, index);
			}
			bool flag2 = index < length - 1;
			if (flag2)
			{
				Array.Copy(array, index + 1, array2, index, length - index - 1);
			}
			set(array2);
		}

		private static void AddElement(Array array, Action<Array> set)
		{
			int length = array.Length;
			Type elementType = array.GetType().GetElementType();
			Array array2 = Array.CreateInstance(elementType, length + 1);
			Array.Copy(array, array2, length);
			set(array2);
		}

		public static WidgetFactory.FieldData[] GetSpecialFieldData(Type t)
		{
			WidgetFactory.FieldData[] fieldDataFromType = WidgetFactory.GetFieldDataFromType(t);
			bool flag = t == typeof(PropSpawner) || t == typeof(PropSpawner_Sphere);
			if (flag)
			{
				WidgetFactory.FieldData fieldData = (from d in fieldDataFromType
				where d.name == "currentSpawns"
				select d).FirstOrDefault<WidgetFactory.FieldData>();
				bool flag2 = fieldData != null;
				if (flag2)
				{
					fieldData.widgetMode = WidgetFactory.WidgetMode.Disabled;
				}
				foreach (WidgetFactory.FieldData fieldData2 in fieldDataFromType)
				{
					bool flag3 = WidgetFactory.spawnerHideFieldData.Contains(fieldData2.name);
					if (flag3)
					{
						fieldData2.widgetMode = WidgetFactory.WidgetMode.Hidden;
					}
				}
			}
			return fieldDataFromType;
		}

		public static WidgetFactory.FieldData[] GetFieldDataFromType(Type t)
		{
			FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			WidgetFactory.FieldData[] array = new WidgetFactory.FieldData[fields.Length];
			for (int i = 0; i < fields.Length; i++)
			{
				FieldInfo fieldInfo = fields[i];
				RangeAttribute customAttribute = fieldInfo.GetCustomAttribute<RangeAttribute>();
				array[i] = new WidgetFactory.FieldData
				{
					name = fieldInfo.Name,
					fieldInfo = fieldInfo,
					rangeAttribute = customAttribute,
					widgetMode = WidgetFactory.WidgetMode.Default,
					layout = ((fieldInfo.FieldType.IsArray || (fieldInfo.FieldType.IsGenericType && fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(List<>)) || (fieldInfo.FieldType.IsClass && !typeof(Object).IsAssignableFrom(fieldInfo.FieldType))) ? WidgetFactory.FieldLayout.Full : WidgetFactory.FieldLayout.Inline)
				};
			}
			return array;
		}

		public static WidgetFactory.FieldData GetFieldData(FieldInfo fieldInfo)
		{
			RangeAttribute customAttribute = fieldInfo.GetCustomAttribute<RangeAttribute>();
			return new WidgetFactory.FieldData
			{
				name = fieldInfo.Name,
				fieldInfo = fieldInfo,
				rangeAttribute = customAttribute,
				widgetMode = WidgetFactory.WidgetMode.Default,
				layout = ((fieldInfo.FieldType.IsArray || (fieldInfo.FieldType.IsGenericType && fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(List<>)) || (fieldInfo.FieldType.IsClass && !typeof(Object).IsAssignableFrom(fieldInfo.FieldType))) ? WidgetFactory.FieldLayout.Full : WidgetFactory.FieldLayout.Inline)
			};
		}

		private static FieldInfo[] GetTypeFields(Type t)
		{
			return t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		private static string[] spawnerHideFieldData = new string[]
		{
			"_deferredSteps",
			"validationConstraints",
			"_propSpawnData",
			"_madeDummyData",
			"<ValidationState>k__BackingField",
			"<spawnedProps>k__BackingField"
		};

		public static Dictionary<Type, WidgetFactory.FieldData[]> TypeFieldDataDictionary = new Dictionary<Type, WidgetFactory.FieldData[]>();

		public class FieldWidgetData
		{
			public FieldWidgetData(Type type, Func<object> get, Action<object> set, string name, RangeAttribute rangeAttribute = null, bool isHidden = false, bool isDisabled = false)
			{
				this.Type = type;
				this.Get = get;
				this.Set = set;
				this.Name = name;
				this.RangeAttribute = rangeAttribute;
				this.IsHidden = isHidden;
				this.IsDisabled = isDisabled;
			}

			public FieldWidgetData()
			{
			}

			public FieldWidgetData(WidgetFactory.FieldData fieldData, object ownerObject)
			{
				this.Type = fieldData.fieldInfo.FieldType;
				this.Get = (() => fieldData.fieldInfo.GetValue(ownerObject));
				this.Set = delegate(object v)
				{
					fieldData.fieldInfo.SetValue(ownerObject, v);
				};
				this.Name = fieldData.name;
				this.RangeAttribute = fieldData.rangeAttribute;
				this.IsHidden = (fieldData.widgetMode == WidgetFactory.WidgetMode.Hidden);
				this.IsDisabled = (fieldData.widgetMode == WidgetFactory.WidgetMode.Disabled);
			}

			public virtual void Draw()
			{
				WidgetFactory.DrawField(this.Type, this.Get, this.Set, this.Name, this.RangeAttribute);
			}

			public string Name;

			public Type Type;

			public Func<object> Get;

			public Action<object> Set;

			public RangeAttribute RangeAttribute = null;

			public bool IsHidden = false;

			public bool IsDisabled = false;
		}

		public class ComboWidgetData : WidgetFactory.FieldWidgetData
		{
			public ComboWidgetData(string name, Func<int> getIndex, Action<int> setIndex, Func<string[]> getOptions)
			{
				this.Name = name;
				this.GetIndex = getIndex;
				this.SetIndex = setIndex;
				this.GetOptions = getOptions;
			}

			public override void Draw()
			{
				int num = this.GetIndex();
				string[] array = this.GetOptions();
				bool flag = array == null || array.Length == 0;
				if (!flag)
				{
					bool flag2 = num < 0 || num >= array.Length;
					if (flag2)
					{
						num = 0;
					}
					ImGui.PushID(this.Name);
					string[] displayOptions = array.Select((string option) => DisplayNameTranslator.Dynamic(option, this.Name)).ToArray();
					bool flag3 = ImGui.Combo("##v", ref num, displayOptions, displayOptions.Length);
					if (flag3)
					{
						this.SetIndex(num);
					}
					ImGui.PopID();
				}
			}

			public Func<int> GetIndex;

			public Action<int> SetIndex;

			public Func<string[]> GetOptions;
		}

		public class FieldData
		{
			public string name;

			public FieldInfo fieldInfo;

			public RangeAttribute rangeAttribute;

			public WidgetFactory.FieldLayout layout;

			public WidgetFactory.WidgetMode widgetMode;
		}

		public enum WidgetMode
		{
			Default,
			Disabled,
			Hidden
		}

		public enum FieldLayout
		{
			Inline,
			Full
		}
	}
}
