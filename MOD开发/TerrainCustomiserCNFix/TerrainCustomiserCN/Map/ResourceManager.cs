using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TerrainCustomiserCN.Map
{
	public static class ResourceManager
	{
		public static void GetAllResources()
		{
			ResourceManager.GetAllGameObjectResources();
			ResourceManager.GetAllMaterialResources();
		}

		private static void GetAllGameObjectResources()
		{
			ResourceManager.GameObjectResources.Clear();
			GameObject[] array = Resources.FindObjectsOfTypeAll<GameObject>();
			foreach (GameObject gameObject in array)
			{
				bool flag = gameObject == null;
				if (!flag)
				{
					bool flag2 = gameObject.transform.parent == null && !gameObject.scene.IsValid();
					if (flag2)
					{
						ResourceManager.GameObjectResources.Add(gameObject);
					}
				}
			}
		}

		private static void GetAllMaterialResources()
		{
			ResourceManager.MaterialResources.Clear();
			Material[] array = Resources.FindObjectsOfTypeAll<Material>();
			foreach (Material material in array)
			{
				bool flag = material == null;
				if (!flag)
				{
					ResourceManager.MaterialResources.Add(material);
				}
			}
		}

		public static List<GameObject> GameObjectResources = new List<GameObject>();

		public static List<Material> MaterialResources = new List<Material>();
	}
}
