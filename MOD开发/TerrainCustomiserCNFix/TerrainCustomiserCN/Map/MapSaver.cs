using System;
using System.IO;
using System.Runtime.CompilerServices;
using Sirenix.Serialization;
using TerrainCustomiserCN.Map.Serialization;
using TerrainCustomiserCN.Utils;
using Zorro.Core;

namespace TerrainCustomiserCN.Map
{
	public static class MapSaver
	{
		public static void SaveTerrain(string saveName)
		{
			Singleton<MapHandler>.Instance.GetComponent<PropGrouper>().ClearAll();
			MapSerializer.MapSaveData mapSaveData = new MapSerializer.MapSaveData
			{
				MapName = saveName
			};
			for (int i = 0; i < MapData.biomeSections.Count; i++)
			{
				MapData.BiomeOption activeBiome = MapData.biomeSections[i].activeBiome;
				bool flag = !activeBiome.segmentHasCustomVariant;
				if (!flag)
				{
					for (int j = 0; j < activeBiome.segments.Count; j++)
					{
						bool flag2 = activeBiome.segments[j].customVariant != null;
						if (flag2)
						{
							MapSerializer.SegmentSaveData item = new MapSerializer.SegmentSaveData
							{
								SectionIndex = i,
								SegmentIndex = j,
								BiomeType = activeBiome.biomeType,
								CustomVariant = MapSerializer.BuildObjectData(activeBiome.segments[j].customVariant.transform)
							};
							mapSaveData.Segments.Add(item);
						}
					}
				}
			}
			Directory.CreateDirectory(MapSerializer.SavePath);
			string path = Path.Combine(MapSerializer.SavePath, saveName + ".json");
			using (MemoryStream memoryStream = new MemoryStream())
			{
				JsonDataWriter jsonDataWriter = new JsonDataWriter(memoryStream, TerrainCustomiserSerialization.CreateSerializationContext(), false);
				SerializationUtility.SerializeValue<MapSerializer.MapSaveData>(mapSaveData, jsonDataWriter);
				File.WriteAllBytes(path, memoryStream.ToArray());
			}
		}
	}
}
