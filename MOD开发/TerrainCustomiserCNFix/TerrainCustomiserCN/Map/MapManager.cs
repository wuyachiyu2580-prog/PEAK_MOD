using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;
using Object = UnityEngine.Object;

namespace TerrainCustomiserCN.Map
{
	public static class MapManager
	{
		public static void ChangeBiome(MapData.BiomeSection biomeSection, MapData.BiomeOption targetBiome)
		{
			bool segmentHasCustomVariant = biomeSection.activeBiome.segmentHasCustomVariant;
			if (segmentHasCustomVariant)
			{
				foreach (MapData.BiomeSegment biomeSegment in biomeSection.activeBiome.segments)
				{
					MapData.SegmentVariant customVariant = biomeSegment.customVariant;
					bool flag = customVariant != null;
					if (flag)
					{
						MapManager.DeleteSegmentVariant(customVariant);
					}
				}
			}
			int index = Singleton<MapHandler>.Instance.biomes.IndexOf(biomeSection.activeBiome.biomeType);
			Singleton<MapHandler>.Instance.biomes[index] = targetBiome.biomeType;
			biomeSection.activeBiome.transform.gameObject.SetActive(false);
			targetBiome.transform.gameObject.SetActive(true);
		}

		public static void DeleteSegmentVariant(MapData.SegmentVariant targetVariant)
		{
			MapData.BiomeSegment segment = targetVariant.segment;
			segment.variants.Remove(targetVariant);
			Object.Destroy(targetVariant.transform.gameObject);
			targetVariant = null;
			segment.initialVariant.transform.gameObject.SetActive(true);
		}

		public static void AssignNewPhotonIds(PhotonView[] views)
		{
			foreach (PhotonView photonView in views)
			{
				photonView.ViewID = 0;
				PhotonNetwork.AllocateViewID(photonView);
			}
		}

		public static void CreateVariant(MapData.BiomeSegment biomeSegment, int variantIndex)
		{
			biomeSegment.transform.GetComponentInParent<PropGrouper>().ClearAll();
			biomeSegment.activeVariant.transform.gameObject.SetActive(false);
			bool flag = biomeSegment.variantSelectionType == MapData.VariantSelectionType.BiomeVariant;
			if (flag)
			{
				MapData.SegmentVariant segmentVariant = biomeSegment.variants[variantIndex];
				Transform transform = Object.Instantiate<Transform>(segmentVariant.transform);
				transform.transform.SetParent(biomeSegment.transform, false);
				transform.name = "Custom";
				MapData.SegmentVariant segmentVariant2 = new MapData.SegmentVariant
				{
					name = "Custom",
					isCustom = true,
					transform = transform,
					segment = biomeSegment
				};
				biomeSegment.variants.Add(segmentVariant2);
				PhotonView[] componentsInChildren = transform.GetComponentsInChildren<PhotonView>(true);
				MapManager.AssignNewPhotonIds(componentsInChildren);
				segmentVariant2.transform.gameObject.SetActive(true);
			}
			else
			{
				Transform transform2 = Object.Instantiate<Transform>(biomeSegment.initialVariant.transform);
				transform2.transform.SetParent(biomeSegment.transform, false);
				transform2.name = "Custom";
				MapData.SegmentVariant segmentVariant3 = new MapData.SegmentVariant
				{
					name = "Custom",
					isCustom = true,
					transform = transform2,
					segment = biomeSegment
				};
				biomeSegment.variants.Add(segmentVariant3);
				VariantObject[] componentsInChildren2 = transform2.GetComponentsInChildren<VariantObject>(true);
				bool flag2 = variantIndex != -1;
				if (flag2)
				{
					MapData.SegmentVariant segmentVariant4 = biomeSegment.variantObjects[variantIndex];
					for (int i = 0; i < componentsInChildren2.Length; i++)
					{
						bool flag3 = componentsInChildren2[i].name != segmentVariant4.name;
						if (flag3)
						{
							Object.Destroy(componentsInChildren2[i].gameObject);
						}
						else
						{
							componentsInChildren2[i].gameObject.SetActive(true);
						}
					}
				}
				List<GameObject> list = new List<GameObject>();
				MapManager.GetAllInactiveSelfGameObjects(segmentVariant3.transform, list);
				foreach (GameObject gameObject in list)
				{
					Object.Destroy(gameObject);
				}
				PhotonView[] componentsInChildren3 = transform2.GetComponentsInChildren<PhotonView>(true);
				MapManager.AssignNewPhotonIds(componentsInChildren3);
				segmentVariant3.transform.gameObject.SetActive(true);
			}
		}

		private static void GetAllInactiveSelfGameObjects(Transform target, List<GameObject> results)
		{
			foreach (object obj in target)
			{
				Transform transform = (Transform)obj;
				bool flag = transform.GetComponent<EnablingSubstep>();
				if (!flag)
				{
					bool flag2 = !transform.gameObject.activeSelf;
					if (flag2)
					{
						results.Add(transform.gameObject);
					}
					else
					{
						MapManager.GetAllInactiveSelfGameObjects(transform, results);
					}
				}
			}
		}

		public static void PrepareDefaultVariant(MapData.BiomeSegment biomeSegment)
		{
			bool flag = biomeSegment.variantSelectionType == MapData.VariantSelectionType.VariantObject;
			if (flag)
			{
				GameObject gameObject = new GameObject("Default");
				gameObject.transform.SetParent(biomeSegment.transform);
				List<Transform> list = new List<Transform>();
				foreach (object obj in biomeSegment.transform)
				{
					Transform transform = (Transform)obj;
					bool flag2 = transform.gameObject.GetComponent<PropGrouper>() != null;
					if (flag2)
					{
						list.Add(transform);
					}
				}
				foreach (Transform transform2 in list)
				{
					transform2.SetParent(gameObject.transform);
				}
				DesertRockSpawner componentInChildren = biomeSegment.transform.GetComponentInChildren<DesertRockSpawner>(true);
				bool flag3 = componentInChildren != null;
				if (flag3)
				{
					componentInChildren.transform.SetParent(biomeSegment.transform);
				}
				Transform transform3 = TransformExtensions.FindChildRecursive(biomeSegment.transform, "GroundMesh");
				bool flag4 = transform3 != null;
				if (flag4)
				{
					transform3.SetParent(biomeSegment.transform);
				}
				gameObject.gameObject.AddComponent<PropGrouper>();
				gameObject.gameObject.AddComponent<BiomeVariant>();
				MapData.SegmentVariant segmentVariant = new MapData.SegmentVariant
				{
					name = "Default",
					isCustom = false,
					transform = gameObject.transform,
					segment = biomeSegment,
					isInitial = true
				};
				biomeSegment.variants.Add(segmentVariant);
				biomeSegment.initialVariant = segmentVariant;
			}
		}

		public static void DisableInactiveSegments()
		{
			foreach (MapHandler.MapSegment mapSegment in Singleton<MapHandler>.Instance.variantSegments)
			{
				mapSegment.segmentParent.SetActive(false);
				bool flag = mapSegment.segmentCampfire != null;
				if (flag)
				{
					mapSegment.segmentCampfire.SetActive(false);
				}
			}
		}
	}
}
