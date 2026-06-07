using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TerrainCustomiserCN.Map.Serialization;
using UnityEngine;
using Zorro.Core;

namespace TerrainCustomiserCN.Map
{
	public static class MapLoader
	{
		public static void ApplySave(MapSerializer.MapSaveData save)
		{
			Singleton<MapHandler>.Instance.GetComponent<PropGrouper>().ClearAll();
			foreach (MapData.BiomeSection biomeSection in MapData.biomeSections)
			{
				foreach (MapData.BiomeOption biomeOption in biomeSection.biomes)
				{
					bool segmentHasCustomVariant = biomeOption.segmentHasCustomVariant;
					if (segmentHasCustomVariant)
					{
						foreach (MapData.BiomeSegment biomeSegment in biomeOption.segments)
						{
							bool flag = biomeSegment.customVariant != null;
							if (flag)
							{
								MapManager.DeleteSegmentVariant(biomeSegment.customVariant);
							}
						}
					}
				}
			}
			using (List<MapSerializer.SegmentSaveData>.Enumerator enumerator4 = save.Segments.GetEnumerator())
			{
				while (enumerator4.MoveNext())
				{
					MapSerializer.SegmentSaveData seg = enumerator4.Current;
					bool flag2 = seg.SectionIndex < 0;
					if (!flag2)
					{
						MapData.BiomeSection biomeSection2 = MapData.biomeSections[seg.SectionIndex];
						MapData.BiomeOption biomeOption2 = biomeSection2.biomes.FirstOrDefault((MapData.BiomeOption b) => b.biomeType == seg.BiomeType);
						bool flag3 = biomeOption2 == null;
						if (!flag3)
						{
							bool flag4 = biomeSection2.activeBiome != biomeOption2;
							if (flag4)
							{
								MapManager.ChangeBiome(biomeSection2, biomeOption2);
							}
							MapData.BiomeSegment biomeSegment2 = biomeOption2.segments[seg.SegmentIndex];
							bool flag5 = biomeSegment2.customVariant != null;
							if (flag5)
							{
								MapManager.DeleteSegmentVariant(biomeSegment2.customVariant);
							}
							biomeSegment2.activeVariant.transform.gameObject.SetActive(false);
							GameObject gameObject = MapLoader.RebuildObjectData(seg.CustomVariant);
							gameObject.name = "Custom";
							gameObject.transform.SetParent(biomeSegment2.transform, false);
							MapData.SegmentVariant segmentVariant = new MapData.SegmentVariant
							{
								name = "Custom",
								isCustom = true,
								transform = gameObject.transform,
								segment = biomeSegment2
							};
							biomeSegment2.variants.Add(segmentVariant);
							segmentVariant.transform.gameObject.SetActive(true);
							bool flag6 = gameObject.GetComponent<BiomeVariant>() == null;
							if (flag6)
							{
								gameObject.AddComponent<BiomeVariant>();
							}
							bool flag7 = gameObject.GetComponent<PropGrouper>() == null;
							if (flag7)
							{
								gameObject.AddComponent<PropGrouper>();
							}
							foreach (object obj in gameObject.transform)
							{
								Transform transform = (Transform)obj;
								MapLoader.EnableCustomObject(transform.gameObject);
							}
						}
					}
				}
			}
			MapSerializer.GameObjectReferences.Clear();
			MapSerializer.testgos.Clear();
		}

		private static void EnableCustomObject(GameObject go)
		{
			go.SetActive(true);
			foreach (object obj in go.transform)
			{
				Transform transform = (Transform)obj;
				MapLoader.EnableCustomObject(transform.gameObject);
			}
		}

		public static GameObject RebuildObjectData(MapSerializer.ObjectData data)
		{
			GameObject gameObject = MapSerializer.testgos.Find((GameObject x) => x.name == data.Id);
			bool flag = gameObject != null;
			GameObject gameObject2;
			if (flag)
			{
				gameObject2 = gameObject;
				gameObject2.name = data.Name;
			}
			else
			{
				gameObject2 = new GameObject(data.Name);
				gameObject2.SetActive(false);
			}
			MapSerializer.GameObjectReferences[data.Id] = gameObject2;
			gameObject2.transform.localPosition = data.Transform.Position;
			gameObject2.transform.localEulerAngles = data.Transform.Rotation;
			gameObject2.transform.localScale = data.Transform.Scale;
			foreach (MapSerializer.ComponentData componentData in data.Components)
			{
				bool flag2 = componentData.Type == typeof(Transform);
				if (!flag2)
				{
					Component obj = gameObject2.AddComponent(componentData.Type);
					foreach (MapSerializer.FieldData fieldData in componentData.Fields)
					{
						FieldInfo field = componentData.Type.GetField(fieldData.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						bool flag3 = field == null;
						if (!flag3)
						{
							object value = fieldData.Value;
							try
							{
								field.SetValue(obj, value);
							}
							catch
							{
							}
						}
					}
				}
			}
			foreach (MapSerializer.ObjectData data2 in data.Children)
			{
				GameObject gameObject3 = MapLoader.RebuildObjectData(data2);
				gameObject3.transform.SetParent(gameObject2.transform, false);
			}
			return gameObject2;
		}
	}
}
