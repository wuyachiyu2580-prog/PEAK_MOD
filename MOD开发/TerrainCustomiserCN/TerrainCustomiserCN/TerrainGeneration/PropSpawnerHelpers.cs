using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Photon.Pun;
using TerrainCustomiserCN.Managers;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace TerrainCustomiserCN.TerrainGeneration
{
	public static class PropSpawnerHelpers
	{
		public static bool TrySpawn(PropSpawner.SpawnData spawnData, ref GameObject __result, GameObject[] props, List<PropSpawnerMod> modifiers, List<PropSpawnerConstraintPost> postConstraints, Transform parent, GameObject fallback)
		{
			bool flag = props == null;
			bool result;
			if (flag)
			{
				__result = null;
				result = false;
			}
			else
			{
				GameObject gameObject = props[Random.Range(0, props.Length)];
				PhotonView photonView;
				bool flag2 = gameObject != null && gameObject.TryGetComponent<PhotonView>(out photonView);
				if (flag2)
				{
					Quaternion randomRotationWithUp = PropSpawnerHelpers.GetRandomRotationWithUp(Vector3.up);
					GameObject gameObject2 = Object.Instantiate<GameObject>(gameObject, spawnData.pos, randomRotationWithUp, parent);
					bool flag3 = gameObject2 == null;
					if (flag3)
					{
						Debug.LogError("Failed to instantiate prop " + gameObject.name);
						__result = null;
						result = false;
					}
					else
					{
						PropSpawnerHelpers.RemoveSpineIfLuggage(gameObject2);
						PropSpawnerHelpers.ApplyModifiers(gameObject2, spawnData, modifiers);
						bool flag4 = !PropSpawnerHelpers.CheckAndHandlePostConstraints(gameObject2, spawnData, postConstraints);
						if (flag4)
						{
							__result = null;
							result = false;
						}
						else
						{
							bool flag5 = gameObject2 == null;
							if (flag5)
							{
								__result = null;
								result = false;
							}
							else
							{
								PropSpawnerHelpers.HandleIfBridge(gameObject2);
								PhotonView component = gameObject2.GetComponent<PhotonView>();
								bool offlineMode = PhotonNetwork.OfflineMode;
								if (offlineMode)
								{
									PhotonNetwork.AllocateViewID(component);
								}
								else
								{
									ViewSyncManager.AddPendingView(component);
								}
								__result = gameObject2;
								result = false;
							}
						}
					}
				}
				else
				{
					result = true;
				}
			}
			return result;
		}

		public static Quaternion GetRandomRotationWithUp(Vector3 normal)
		{
			Vector3 vector = Random.onUnitSphere;
			vector.y = 0f;
			vector = Vector3.Cross(normal, Vector3.Cross(normal, vector));
			return Quaternion.LookRotation(vector, normal);
		}

		public static bool CheckAndHandlePostConstraints(GameObject go, PropSpawner.SpawnData spawnData, List<PropSpawnerConstraintPost> postConstraints)
		{
			foreach (PropSpawnerConstraintPost propSpawnerConstraintPost in postConstraints)
			{
				bool mute = propSpawnerConstraintPost.mute;
				if (!mute)
				{
					bool flag = !propSpawnerConstraintPost.CheckConstraint(go, spawnData);
					if (flag)
					{
						Object.DestroyImmediate(go);
						return false;
					}
				}
			}
			return true;
		}

		public static void ApplyModifiers(GameObject go, PropSpawner.SpawnData spawnData, List<PropSpawnerMod> modifiers)
		{
			foreach (PropSpawnerMod propSpawnerMod in modifiers)
			{
				bool mute = propSpawnerMod.mute;
				if (!mute)
				{
					propSpawnerMod.ModifyObject(go, spawnData);
				}
			}
		}

		public static void RemoveSpineIfLuggage(GameObject go)
		{
			Luggage luggage;
			bool flag = go.TryGetComponent<Luggage>(out luggage);
			if (flag)
			{
				SpineCheck component = go.GetComponent<SpineCheck>();
				bool flag2 = component != null;
				if (flag2)
				{
					Object.DestroyImmediate(component);
				}
			}
		}

		public static void HandleIfBridge(GameObject go)
		{
			BreakableBridge @object;
			bool flag = go.TryGetComponent<BreakableBridge>(out @object);
			if (flag)
			{
				CollisionModifier[] componentsInChildren = go.GetComponentsInChildren<CollisionModifier>();
				foreach (CollisionModifier collisionModifier in componentsInChildren)
				{
					collisionModifier.applyEffects = false;
					collisionModifier.onCollide = (Action<Character, CollisionModifier, Collision, Bodypart>)Delegate.Combine(collisionModifier.onCollide, new Action<Character, CollisionModifier, Collision, Bodypart>(@object.OnBridgeCollision));
				}
			}
		}

		public static bool CheckIfBridge(GameObject go)
		{
			BreakableBridge breakableBridge;
			return go.TryGetComponent<BreakableBridge>(out breakableBridge);
		}

		public static Ray[] BuildRays_Sphere(PropSpawner_Sphere spawner, Ray[] outRays, int count)
		{
			Vector3 position = spawner.transform.position;
			for (int i = 0; i < count; i++)
			{
				Vector3 onUnitSphere = Random.onUnitSphere;
				outRays[i] = new Ray(position, onUnitSphere);
			}
			return outRays;
		}

		public static Ray[] BuildRays(PropSpawner spawner, Ray[] outRays, int count, Vector3 rayDir)
		{
			for (int i = 0; i < count; i++)
			{
				outRays[i] = PropSpawnerHelpers.GetSpawnRay(spawner.area, spawner.transform, rayDir);
			}
			return outRays;
		}

		public static Ray GetSpawnRay(Vector2 area, Transform t, Vector3 rayDir)
		{
			Vector2 vector = new Vector2(Random.value, Random.value);
			Vector3 vector2 = t.position + t.right * Mathf.Lerp((0f - area.x) * 0.5f, area.x * 0.5f, vector.x) + t.up * Mathf.Lerp((0f - area.y) * 0.5f, area.y * 0.5f, vector.y);
			return new Ray(vector2, rayDir);
		}

		public static PropSpawner.SpawnData BuildSpawnData(PropSpawner spawner, Ray ray, RaycastHit hit, bool hasHit, int count)
		{
			Vector2 placement = new Vector2(Random.value, Random.value);
			return new PropSpawner.SpawnData
			{
				pos = hit.point,
				normal = hit.normal,
				rayDir = spawner.transform.forward,
				hit = hit,
				spawnerTransform = spawner.transform,
				placement = placement,
				spawnCount = count
			};
		}

		public static readonly HashSet<string> SafeToBatchPropNames = new HashSet<string>
		{
			"LuggageSpawner",
			"Ivy",
			"Geysers",
			"Weed",
			"ExploShrooms",
			"PoisonShrooms",
			"Vines",
			"BeachGrass",
			"Driftwood",
			"Behive",
			"ShittyPiton",
			"Monsteras",
			"FlashPlant",
			"Shrub",
			"Pine",
			"Bushes",
			"Trees",
			"DeadTree",
			"Ice_DeadTree",
			"Palms",
			"Luggage",
			"Cacti",
			"Dynamite",
			"Ferns",
			"Zombie Spawner",
			"Exploding Shrooms",
			"Glow Mushrooms",
			"Spiders",
			"Bounce Shrooms",
			"Shroom Spawners",
			"Root Spawner",
			"Evil Shroom"
		};
	}
}
