using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Photon.Pun;
using Sirenix.Serialization;
using TerrainCustomiserCN.Utils;
using UnityEngine;

namespace TerrainCustomiserCN.Map.Serialization
{
	public static class MapSerializer
	{
		public static string GetIdFromGameObject(GameObject go)
		{
			KeyValuePair<string, GameObject> keyValuePair = MapSerializer.GameObjectReferences.FirstOrDefault((KeyValuePair<string, GameObject> x) => x.Value == go);
			bool flag = keyValuePair.Value == null;
			string result;
			if (flag)
			{
				string text = Guid.NewGuid().ToString("N");
				MapSerializer.GameObjectReferences.Add(text, go);
				result = text;
			}
			else
			{
				result = keyValuePair.Key;
			}
			return result;
		}

		public static string GetPathFromGameObject(GameObject go)
		{
			Transform transform = go.transform;
			bool flag = transform == null;
			string result;
			if (flag)
			{
				result = null;
			}
			else
			{
				StringBuilder stringBuilder = new StringBuilder(transform.name);
				while (transform.parent != null)
				{
					transform = transform.parent;
					stringBuilder.Insert(0, transform.name + "/");
				}
				result = stringBuilder.ToString();
			}
			return result;
		}

		private static string ResolveSavePath()
		{
			return Path.Combine(Application.persistentDataPath, "TerrainCustomiser", "Map Saves");
		}

		public static void RefreshSaveList()
		{
			bool flag = !Directory.Exists(MapSerializer.SavePath);
			if (flag)
			{
				return;
			}
			MapSerializer.SaveFiles.Clear();
			MapSerializer.SaveFiles.AddRange(MapSerializer.GetSaveFileNames(false));
		}

		public static List<string> GetSaveFileNames(bool includeOldBackups)
		{
			List<string> list = new List<string>();
			bool flag = !Directory.Exists(MapSerializer.SavePath);
			if (flag)
			{
				return list;
			}
			IEnumerable<string> enumerable = Directory.GetFiles(MapSerializer.SavePath, "*.json").Where((string path) => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Select(new Func<string, string>(Path.GetFileName));
			list.AddRange(enumerable);
			bool flag2 = includeOldBackups;
			if (flag2)
			{
				IEnumerable<string> enumerable2 = Directory.GetFiles(MapSerializer.SavePath, "*.json.old").Where((string path) => path.EndsWith(".json.old", StringComparison.OrdinalIgnoreCase)).Select(new Func<string, string>(Path.GetFileName));
				list.AddRange(enumerable2);
			}
			list.Sort(StringComparer.OrdinalIgnoreCase);
			return list;
		}

		public static bool IsOldBackup(string saveFile)
		{
			return saveFile != null && saveFile.EndsWith(".json.old", StringComparison.OrdinalIgnoreCase);
		}

		public static byte[] GetSave(int selectedSaveIndex)
		{
			return MapSerializer.GetSave(MapSerializer.SaveFiles[selectedSaveIndex]);
		}

		public static byte[] GetSave(string saveFile)
		{
			string path = Path.Combine(MapSerializer.SavePath, saveFile);
			bool flag = !File.Exists(path);
			byte[] result;
			if (flag)
			{
				result = null;
			}
			else
			{
				byte[] array = File.ReadAllBytes(path);
				result = array;
			}
			return result;
		}

		public static MapSerializer.MapSaveData LoadSave(int selectedSaveIndex)
		{
			byte[] save = MapSerializer.GetSave(selectedSaveIndex);
			return SerializationUtility.DeserializeValue<MapSerializer.MapSaveData>(save, DataFormat.JSON, TerrainCustomiserSerialization.CreateDeserializationContext());
		}

		public static MapSerializer.MapSaveData LoadFromBytes(byte[] bytes)
		{
			return SerializationUtility.DeserializeValue<MapSerializer.MapSaveData>(bytes, DataFormat.JSON, TerrainCustomiserSerialization.CreateDeserializationContext());
		}

		public static void SaveTerrain()
		{
		}

		public static MapSerializer.ObjectData BuildObjectData(Transform t)
		{
			MapSerializer.ObjectData objectData = new MapSerializer.ObjectData
			{
				Id = MapSerializer.GetIdFromGameObject(t.gameObject),
				Name = t.gameObject.name,
				Transform = new MapSerializer.TransformData
				{
					Position = t.localPosition,
					Rotation = t.localEulerAngles,
					Scale = t.localScale
				}
			};
			Component[] components = t.GetComponents<Component>();
			foreach (Component component in components)
			{
				bool flag = component == null;
				if (!flag)
				{
					bool flag2 = component is Transform;
					if (!flag2)
					{
						bool flag3 = component is PhotonView;
						if (flag3)
						{
							PhotonView photonView = (PhotonView)component;
							photonView.ViewID = 0;
							photonView.sceneViewId = 0;
						}
						objectData.Components.Add(MapSerializer.SerializeComponent(component));
					}
				}
			}
			for (int j = 0; j < t.childCount; j++)
			{
				objectData.Children.Add(MapSerializer.BuildObjectData(t.GetChild(j)));
			}
			return objectData;
		}

		private static MapSerializer.ComponentData SerializeComponent(Component comp)
		{
			Type type = comp.GetType();
			MapSerializer.ComponentData componentData = new MapSerializer.ComponentData
			{
				Type = type
			};
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (FieldInfo fieldInfo in fields)
			{
				bool isStatic = fieldInfo.IsStatic;
				if (!isStatic)
				{
					bool isNotSerialized = fieldInfo.IsNotSerialized;
					if (!isNotSerialized)
					{
						bool flag = !fieldInfo.IsPublic && fieldInfo.GetCustomAttribute<SerializeField>() == null;
						if (!flag)
						{
							object value = fieldInfo.GetValue(comp);
							componentData.Fields.Add(new MapSerializer.FieldData
							{
								Name = fieldInfo.Name,
								Type = fieldInfo.FieldType,
								Value = value
							});
						}
					}
				}
			}
			return componentData;
		}

		internal static readonly string SavePath = MapSerializer.ResolveSavePath();

		internal static List<string> SaveFiles = new List<string>();

		public static Dictionary<string, GameObject> GameObjectReferences = new Dictionary<string, GameObject>();

		public static List<GameObject> testgos = new List<GameObject>();

		public class ObjectData
		{
			public string Id;

			public string Name;

			public MapSerializer.TransformData Transform;

			public List<MapSerializer.ComponentData> Components = new List<MapSerializer.ComponentData>();

			public List<MapSerializer.ObjectData> Children = new List<MapSerializer.ObjectData>();
		}

		public class TransformData
		{
			public Vector3 Position;

			public Vector3 Rotation;

			public Vector3 Scale;
		}

		public class ComponentData
		{
			public Type Type;

			public List<MapSerializer.FieldData> Fields = new List<MapSerializer.FieldData>();
		}

		public class FieldData
		{
			public string Name;

			public Type Type;

			public object Value;
		}

		public class MapSaveData
		{
			public string MapName;

			public List<MapSerializer.SegmentSaveData> Segments = new List<MapSerializer.SegmentSaveData>();
		}

		public class MapSyncData
		{
			public int seed;

			public bool bakeLightMap;

			public byte[] mapSaveData;
		}

		public class SegmentSaveData
		{
			public int SectionIndex;

			public int SegmentIndex;

			public Biome.BiomeType BiomeType;

			public MapSerializer.ObjectData CustomVariant;
		}
	}
}
