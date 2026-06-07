using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Zorro.Core;

namespace TerrainCustomiserCN.Map
{
	public static class MapData
	{
		public static void BuildMapData()
		{
			MapData.biomeSections.Clear();
			MapHandler instance = Singleton<MapHandler>.Instance;
			MapData.GetBiomeSections(instance);
		}

		private static void GetBiomeSections(MapHandler mapHandler)
		{
			foreach (object obj in mapHandler.transform)
			{
				Transform transform = (Transform)obj;
				bool flag = transform.GetComponent<BiomeSelector>() != null;
				if (flag)
				{
					MapData.BiomeSection biomeSection = new MapData.BiomeSection
					{
						name = transform.name,
						transform = transform
					};
					MapData.GetBiomeOptions(biomeSection);
					MapData.biomeSections.Add(biomeSection);
				}
			}
		}

		private static void GetBiomeOptions(MapData.BiomeSection biomeSection)
		{
			foreach (object obj in biomeSection.transform)
			{
				Transform transform = (Transform)obj;
				Biome biome;
				bool flag = transform.TryGetComponent<Biome>(out biome);
				if (flag)
				{
					MapData.BiomeOption biomeOption = new MapData.BiomeOption
					{
						name = transform.name,
						transform = transform,
						biomeType = biome.biomeType,
						section = biomeSection
					};
					MapData.GetBiomeSegments(biomeOption);
					biomeSection.biomes.Add(biomeOption);
				}
			}
		}

		private static void GetBiomeSegments(MapData.BiomeOption biomeOption)
		{
			foreach (object obj in biomeOption.transform)
			{
				Transform transform = (Transform)obj;
				bool flag = transform.name.Contains("Segment");
				if (flag)
				{
					MapData.BiomeSegment biomeSegment = new MapData.BiomeSegment
					{
						name = transform.name,
						transform = transform,
						biome = biomeOption
					};
					VariationSwapper componentInChildren = biomeSegment.transform.GetComponentInChildren<VariationSwapper>(true);
					biomeSegment.variantSelectionType = ((componentInChildren != null) ? MapData.VariantSelectionType.BiomeVariant : MapData.VariantSelectionType.VariantObject);
					MapData.GetSegmentVariants(biomeSegment);
					biomeOption.segments.Add(biomeSegment);
				}
			}
		}

		private static void GetSegmentVariants(MapData.BiomeSegment biomeSegment)
		{
			bool flag = biomeSegment.variantSelectionType == MapData.VariantSelectionType.BiomeVariant;
			if (flag)
			{
				VariationSwapper componentInChildren = biomeSegment.transform.GetComponentInChildren<VariationSwapper>(true);
				foreach (VariationSwapper.Variation variation in componentInChildren.Variations)
				{
					MapData.SegmentVariant segmentVariant = new MapData.SegmentVariant
					{
						name = variation.parent.name,
						transform = variation.parent.transform,
						segment = biomeSegment,
						isInitial = variation.parent.activeSelf
					};
					bool isInitial = segmentVariant.isInitial;
					if (isInitial)
					{
						biomeSegment.initialVariant = segmentVariant;
					}
					biomeSegment.variants.Add(segmentVariant);
				}
			}
			else
			{
				MapManager.PrepareDefaultVariant(biomeSegment);
				VariantObject[] componentsInChildren = biomeSegment.transform.GetComponentsInChildren<VariantObject>(true);
				foreach (VariantObject variantObject in componentsInChildren)
				{
					MapData.SegmentVariant item = new MapData.SegmentVariant
					{
						name = variantObject.name,
						transform = variantObject.transform,
						segment = biomeSegment
					};
					biomeSegment.variantObjects.Add(item);
				}
			}
		}

		public static List<MapData.BiomeSection> biomeSections = new List<MapData.BiomeSection>();

		public enum VariantSelectionType
		{
			BiomeVariant,
			VariantObject
		}

		public class BiomeSection
		{
			public MapData.BiomeOption activeBiome
			{
				get
				{
					return this.biomes.Find((MapData.BiomeOption x) => x.isActive);
				}
			}

			public string name;

			public Transform transform;

			public List<MapData.BiomeOption> biomes = new List<MapData.BiomeOption>();
		}

		public class BiomeOption
		{
			public bool isActive
			{
				get
				{
					return this.transform.gameObject.activeSelf;
				}
			}

			public bool segmentHasCustomVariant
			{
				get
				{
					return this.segments.Find((MapData.BiomeSegment x) => x.customVariant != null) != null;
				}
			}

			public string name;

			public Transform transform;

			public Biome.BiomeType biomeType;

			public MapData.BiomeSection section;

			public List<MapData.BiomeSegment> segments = new List<MapData.BiomeSegment>();
		}

		public class BiomeSegment
		{
			public MapData.SegmentVariant customVariant
			{
				get
				{
					return this.variants.Find((MapData.SegmentVariant x) => x.isCustom);
				}
			}

			public MapData.SegmentVariant activeVariant
			{
				get
				{
					return this.variants.Find((MapData.SegmentVariant x) => x.isActive);
				}
			}

			public string name;

			public Transform transform;

			public MapData.BiomeOption biome;

			public MapData.VariantSelectionType variantSelectionType = MapData.VariantSelectionType.VariantObject;

			public List<MapData.SegmentVariant> variants = new List<MapData.SegmentVariant>();

			public List<MapData.SegmentVariant> variantObjects = new List<MapData.SegmentVariant>();

			public MapData.SegmentVariant initialVariant;
		}

		public class SegmentVariant
		{
			public bool isActive
			{
				get
				{
					return this.transform.gameObject.activeSelf;
				}
			}

			public string name;

			public Transform transform;

			public MapData.BiomeSegment segment;

			public bool isCustom = false;

			public bool isInitial = false;
		}
	}
}
