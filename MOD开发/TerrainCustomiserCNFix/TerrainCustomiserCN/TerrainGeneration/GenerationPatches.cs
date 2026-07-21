using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Photon.Pun;
using TerrainCustomiserCN.Managers;
using TerrainCustomiserCN.Map;
using TerrainCustomiserCN.Map.Serialization;
using TerrainCustomiserCN.Session;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Zorro.Core;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace TerrainCustomiserCN.TerrainGeneration
{
	public static class GenerationPatches
	{
		public static bool CanBatch(string spawnerName, bool raycastPosition, bool syncTransforms)
		{
			bool flag = !raycastPosition;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = !syncTransforms || PropSpawnerHelpers.SafeToBatchPropNames.Contains(spawnerName);
				result = (!flag2 || true);
			}
			return result;
		}

		public static bool PassedConstraints(PropSpawner spawner, PropSpawner.SpawnData sd)
		{
			for (int i = 0; i < spawner.constraints.Count; i++)
			{
				bool flag = !spawner.constraints[i].CheckConstraint(sd);
				if (flag)
				{
					return false;
				}
			}
			return true;
		}

		public static void ExecuteJob(NativeArray<RaycastCommand> commands, NativeArray<RaycastHit> results, Ray[] rays, QueryParameters queryParams, int batchCount, float rayLength)
		{
			for (int i = 0; i < batchCount; i++)
			{
				commands[i] = new RaycastCommand(rays[i].origin, rays[i].direction, queryParams, rayLength);
			}
			RaycastCommand.ScheduleBatch(commands, results, 4, default(JobHandle)).Complete();
		}

		public static int ProcessResults(NativeArray<RaycastHit> results, Ray[] rays, PropSpawner spawner, int successes, int nrOfSpawns)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			while (num3 < results.Length && successes < nrOfSpawns)
			{
				RaycastHit hit = results[num3];
				bool flag = hit.collider == null;
				if (!flag)
				{
					num++;
					PropSpawner.SpawnData spawnData = PropSpawnerHelpers.BuildSpawnData(spawner, rays[num3], hit, true, successes);
					bool flag2 = !GenerationPatches.PassedConstraints(spawner, spawnData);
					if (!flag2)
					{
						GameObject gameObject = spawner.Spawn(spawnData);
						bool flag3 = gameObject != null;
						if (flag3)
						{
							successes++;
							num2++;
							spawner.AllSpawnData[gameObject] = spawnData;
						}
					}
				}
				num3++;
			}
			return num2;
		}

		[HarmonyPatch]
		public static class RemoveDebugLogsPatch
		{
			[HarmonyTranspiler]
			[HarmonyPatch(typeof(JungleVine), "Awake")]
			[HarmonyPatch(typeof(JungleVine), "PickTreePlatforms")]
			[HarmonyPatch(typeof(SpawnConnectingBridge), "CheckCondition")]
			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				return instructions;
			}
		}

		[HarmonyPatch(typeof(Looker), "ToggleLookers")]
		internal static class ToggleLookersPatch
		{
			public static bool Prefix(Looker __instance)
			{
				Looker[] componentsInChildren = __instance.transform.parent.GetComponentsInChildren<Looker>();
				int num = Random.Range(0, componentsInChildren.Length);
				bool flag = Random.value < 0.95f;
				if (flag)
				{
					num = -1;
				}
				Looker[] array = componentsInChildren;
				foreach (Looker looker in array)
				{
					bool flag2 = looker.view == null;
					if (flag2)
					{
						looker.view = looker.GetComponent<PhotonView>();
					}
					bool flag3 = looker.transform.GetSiblingIndex() != num;
					if (flag3)
					{
						looker.view.RPC("RPCA_DisableLooker", (RpcTarget)3, Array.Empty<object>());
					}
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(PropGrouper), "RunAll")]
		public static class PropGrouperRunAllPatch
		{
			private static void AddToStepList(DeferredStepTiming key, IDeferredStep stepToAdd)
			{
				bool flag = !GenerationPatches.PropGrouperRunAllPatch.deferredSteps.ContainsKey(key);
				if (flag)
				{
					GenerationPatches.PropGrouperRunAllPatch.deferredSteps.Add(key, new List<IDeferredStep>());
				}
				GenerationPatches.PropGrouperRunAllPatch.deferredSteps[key].Add(stepToAdd);
			}

			private static void ExecuteAndClearDeferredStepsFor(DeferredStepTiming key)
			{
				bool flag = GenerationPatches.PropGrouperRunAllPatch.deferredSteps.ContainsKey(key);
				if (flag)
				{
					foreach (IDeferredStep deferredStep in GenerationPatches.PropGrouperRunAllPatch.deferredSteps[key])
					{
						deferredStep.DeferredGo();
					}
					GenerationPatches.PropGrouperRunAllPatch.deferredSteps[key].Clear();
				}
			}

			public static bool Prefix(PropGrouper __instance, bool updateLightmap = true)
			{
				bool flag = !SessionState.CanGenerate;
				bool result;
				if (flag)
				{
					result = true;
				}
				else
				{
					GenerationPatches.PropGrouperRunAllPatch.deferredSteps.Clear();
					__instance.ClearAll();
					LevelGenStep[] componentsInChildren = __instance.GetComponentsInChildren<LevelGenStep>();
					List<LevelGenStep> list = new List<LevelGenStep>();
					List<LevelGenStep> list2 = new List<LevelGenStep>();
					for (int i = 0; i < componentsInChildren.Length; i++)
					{
						PropGrouper.PropGrouperTiming timing = componentsInChildren[i].GetComponentInParent<PropGrouper>().timing;
						if (timing == PropGrouper.PropGrouperTiming.Early)
						{
							list.Add(componentsInChildren[i]);
						}
						else if (timing == PropGrouper.PropGrouperTiming.Late)
						{
							list2.Add(componentsInChildren[i]);
						}
					}
					foreach (LevelGenStep levelGenStep in list)
					{
						levelGenStep.Execute();
						bool flag2 = levelGenStep.DeferredTiming != DeferredStepTiming.None;
						if (flag2)
						{
							GenerationPatches.PropGrouperRunAllPatch.AddToStepList(levelGenStep.DeferredTiming, levelGenStep.ConstructDeferred(levelGenStep));
						}
					}
					PhotonNetwork.NetworkingClient.LoadBalancingPeer.SendOutgoingCommands();
					bool flag3 = ConfigManager.EnableLightMapBaking.Value && updateLightmap;
					if (flag3)
					{
						LightMapBaker.RunBake();
					}
					PhotonNetwork.NetworkingClient.LoadBalancingPeer.SendOutgoingCommands();
					GenerationPatches.PropGrouperRunAllPatch.ExecuteAndClearDeferredStepsFor(DeferredStepTiming.AfterCurrentGroupTiming);
					foreach (LevelGenStep levelGenStep2 in list2)
					{
						levelGenStep2.Execute();
					}
					result = false;
				}
				return result;
			}

			public static Dictionary<DeferredStepTiming, List<IDeferredStep>> deferredSteps = new Dictionary<DeferredStepTiming, List<IDeferredStep>>();
		}

		[HarmonyPatch(typeof(PropSpawner_Sphere), "SpawnNew")]
		public static class PropSpawnerSphereAddPatch
		{
			private static bool Prefix(PropSpawner_Sphere __instance, bool executeDeferredImmediately)
			{
				bool flag = !SessionState.CanGenerate;
				bool result;
				if (flag)
				{
					result = true;
				}
				else
				{
					bool flag2 = __instance.spawnChance < Random.value;
					if (flag2)
					{
						result = false;
					}
					else
					{
						bool flag3 = !GenerationPatches.CanBatch(__instance.gameObject.name, __instance.rayCastSpawn, __instance.syncTransforms);
						if (flag3)
						{
							result = true;
						}
						else
						{
							int num = __instance.nrOfSpawns;
							bool randomSpawns = __instance.randomSpawns;
							if (randomSpawns)
							{
								num = Random.Range(__instance.minSpawnCount, __instance.nrOfSpawns);
							}
							int num2 = 50000;
							int num3 = 0;
							int num4 = 256;
							bool flag4 = GenerationPatches.PropSpawnerSphereAddPatch.rays.Length < num4;
							if (flag4)
							{
								GenerationPatches.PropSpawnerSphereAddPatch.rays = new Ray[num4];
							}
							LayerMask mask = HelperFunctions.GetMask(__instance.layerType);
							QueryParameters @default = QueryParameters.Default;
							@default.layerMask = mask.value;
							@default.hitTriggers = QueryTriggerInteraction.Ignore;
							NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(num4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
							NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(num4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
							while (num3 < num && num2 > 0)
							{
								GenerationPatches.PropSpawnerSphereAddPatch.rays = PropSpawnerHelpers.BuildRays_Sphere(__instance, GenerationPatches.PropSpawnerSphereAddPatch.rays, num4);
								GenerationPatches.ExecuteJob(commands, results, GenerationPatches.PropSpawnerSphereAddPatch.rays, @default, num4, __instance.rayLength);
								num2 -= num4;
								int num5 = 0;
								int num6 = 0;
								while (num6 < num4 && num3 < num)
								{
									RaycastHit hit = results[num6];
									bool flag5 = !hit.transform;
									if (!flag5)
									{
										PropSpawner.SpawnData spawnData = new PropSpawner.SpawnData
										{
											pos = hit.point,
											normal = hit.normal,
											rayDir = GenerationPatches.PropSpawnerSphereAddPatch.rays[num6].direction,
											hit = hit,
											spawnerTransform = __instance.transform
										};
										bool flag6 = true;
										for (int i = 0; i < __instance.constraints.Count; i++)
										{
											bool flag7 = !__instance.constraints[i].CheckConstraint(spawnData);
											if (flag7)
											{
												flag6 = false;
												break;
											}
										}
										bool flag8 = !flag6;
										if (!flag8)
										{
											GameObject gameObject = GenerationPatches.PropSpawnerSphereAddPatch.Spawn(__instance, spawnData);
											bool flag9 = gameObject != null;
											if (flag9)
											{
												__instance.spawnedProps.Add(gameObject);
												num3++;
												num5++;
											}
										}
									}
									num6++;
								}
								bool flag10 = num <= 10 && num5 == 0;
								if (flag10)
								{
									break;
								}
							}
							commands.Dispose();
							results.Dispose();
							foreach (PostSpawnBehavior postSpawnBehavior in __instance.postSpawnBehaviors)
							{
								bool flag11 = !postSpawnBehavior.mute;
								if (flag11)
								{
									bool flag12 = executeDeferredImmediately || postSpawnBehavior.DeferredTiming != DeferredStepTiming.AfterCurrentGroupTiming;
									if (flag12)
									{
										postSpawnBehavior.RunBehavior(__instance.spawnedProps);
									}
									else
									{
										__instance._deferredSteps.Add(postSpawnBehavior.ConstructDeferred(__instance));
									}
								}
							}
							result = false;
						}
					}
				}
				return result;
			}

			private static GameObject Spawn(PropSpawner_Sphere spawner, PropSpawner.SpawnData spawnData)
			{
				GameObject gameObject = HelperFunctions.SpawnPrefab(spawner.props[Random.Range(0, spawner.props.Length)], spawnData.pos, HelperFunctions.GetRandomRotationWithUp(Vector3.up), spawner.transform);
				bool flag = gameObject == null;
				GameObject result;
				if (flag)
				{
					result = null;
				}
				else
				{
					for (int i = 0; i < spawner.modifiers.Count; i++)
					{
						spawner.modifiers[i].ModifyObject(gameObject, spawnData);
					}
					for (int j = 0; j < spawner.postConstraints.Count; j++)
					{
						bool flag2 = !spawner.postConstraints[j].CheckConstraint(gameObject, spawnData);
						if (flag2)
						{
							Object.DestroyImmediate(gameObject);
							return null;
						}
					}
					result = gameObject;
				}
				return result;
			}

			private static Ray[] rays = new Ray[0];
		}

		[HarmonyPatch(typeof(PropSpawner), "SpawnNew")]
		public static class PropSpawnerSpawnNewPatch
		{
			private static bool Prefix(PropSpawner __instance, bool executeDeferredImmediately)
			{
				bool flag = !SessionState.CanGenerate;
				bool result;
				if (flag)
				{
					result = true;
				}
				else
				{
					bool flag2 = __instance.chanceToUseSpawner < 0.999f && Random.value > __instance.chanceToUseSpawner;
					if (flag2)
					{
						result = false;
					}
					else
					{
						bool flag3 = !GenerationPatches.CanBatch(__instance.gameObject.name, __instance.raycastPosition, __instance.syncTransforms);
						if (flag3)
						{
							result = true;
						}
						else
						{
							int num = __instance.nrOfSpawns;
							bool randomSpawns = __instance.randomSpawns;
							if (randomSpawns)
							{
								num = Random.Range(__instance.minSpawnCount, __instance.nrOfSpawns);
							}
							int num2 = 25000;
							int num3 = 0;
							int num4 = 256;
							LayerMask mask = HelperFunctions.GetMask(__instance.layerType);
							bool flag4 = GenerationPatches.PropSpawnerSpawnNewPatch.rays.Length < num4;
							if (flag4)
							{
								GenerationPatches.PropSpawnerSpawnNewPatch.rays = new Ray[num4];
							}
							Vector3 normalized = (__instance.transform.forward + __instance.rayDirectionOffset).normalized;
							QueryParameters @default = QueryParameters.Default;
							@default.layerMask = mask.value;
							@default.hitTriggers = QueryTriggerInteraction.Ignore;
							NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(num4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
							NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(num4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
							while (num3 < num && num2 > 0)
							{
								GenerationPatches.PropSpawnerSpawnNewPatch.rays = PropSpawnerHelpers.BuildRays(__instance, GenerationPatches.PropSpawnerSpawnNewPatch.rays, num4, normalized);
								GenerationPatches.ExecuteJob(commands, results, GenerationPatches.PropSpawnerSpawnNewPatch.rays, @default, num4, __instance.rayLength);
								num2 -= num4;
								int num5 = GenerationPatches.ProcessResults(results, GenerationPatches.PropSpawnerSpawnNewPatch.rays, __instance, num3, num);
								num3 += num5;
							}
							commands.Dispose();
							results.Dispose();
							Physics.SyncTransforms();
							__instance.currentSpawns = __instance.transform.childCount;
							__instance.SpawnDecor();
							GenerationPatches.PropSpawnerSpawnNewPatch.HandlePostSpawnBehaviors(__instance, executeDeferredImmediately);
							result = false;
						}
					}
				}
				return result;
			}

			private static void HandlePostSpawnBehaviors(PropSpawner __instance, bool executeDeferredImmediately)
			{
				foreach (PostSpawnBehavior postSpawnBehavior in __instance.postSpawnBehaviors)
				{
					bool flag = !postSpawnBehavior.mute;
					if (flag)
					{
						bool flag2 = executeDeferredImmediately || postSpawnBehavior.DeferredTiming != DeferredStepTiming.AfterCurrentGroupTiming;
						if (flag2)
						{
							postSpawnBehavior.RunBehavior(__instance.SpawnedProps);
						}
						else
						{
							__instance._deferredSteps.Add(postSpawnBehavior.ConstructDeferred(__instance));
						}
					}
				}
			}

			private static Ray[] rays = new Ray[0];
		}

		[HarmonyPatch(typeof(PropSpawner), "Execute")]
		public static class PropSpawnerExecutePatch
		{
			private static bool Prefix(PropSpawner __instance)
			{
				bool flag = __instance.props == null || __instance.props.Length == 0;
				bool result;
				if (flag)
				{
					Debug.LogError("[TCCN] PropSpawner " + __instance.name + " has null or empty props array.");
					result = false;
				}
				else
				{
					result = true;
				}
				return result;
			}
		}

		[HarmonyPatch(typeof(PropSpawner_Sphere), "Execute")]
		public static class PropSpawner_SphereExecutePatch
		{
			private static bool Prefix(PropSpawner_Sphere __instance)
			{
				bool flag = __instance.props == null || __instance.props.Length == 0;
				bool result;
				if (flag)
				{
					Debug.LogError("[TCCN] PropSpawner_Sphere " + __instance.name + " has null or empty props array.");
					result = false;
				}
				else
				{
					result = true;
				}
				return result;
			}
		}

		[HarmonyPatch(typeof(PropSpawner_Line), "Execute")]
		public static class PropSpawner_LineExecutePatch
		{
			private static bool Prefix(PropSpawner_Line __instance)
			{
				bool flag = __instance.props == null || __instance.props.Length == 0;
				bool result;
				if (flag)
				{
					Debug.LogError("[TCCN] PropSpawner_Line " + __instance.name + " has null or empty props array.");
					result = false;
				}
				else
				{
					result = true;
				}
				return result;
			}
		}

		[HarmonyPatch(typeof(PropSpawner), "Spawn")]
		private class PropSpawnerSpawnPatch
		{
			private static bool Prefix(PropSpawner __instance, PropSpawner.SpawnData spawnData, ref GameObject __result)
			{
				bool flag = !SessionState.CanGenerate;
				bool result;
				if (flag)
				{
					result = true;
				}
				else
				{
					bool flag2 = __instance.props == null;
					if (flag2)
					{
						result = true;
					}
					else
					{
						bool flag3 = PropSpawnerHelpers.TrySpawn(spawnData, ref __result, __instance.props, __instance.modifiers, __instance.postConstraints, __instance.transform, __instance.gameObject);
						result = flag3;
					}
				}
				return result;
			}
		}

		[HarmonyPatch(typeof(PropSpawner_Sphere), "Spawn")]
		private class PropSpawner_SphereSpawnPatch
		{
			private static bool Prefix(PropSpawner_Sphere __instance, PropSpawner.SpawnData spawnData, ref GameObject __result)
			{
				bool flag = !SessionState.CanGenerate;
				bool result;
				if (flag)
				{
					result = true;
				}
				else
				{
					bool flag2 = __instance.props == null;
					if (flag2)
					{
						result = true;
					}
					else
					{
						bool flag3 = PropSpawnerHelpers.TrySpawn(spawnData, ref __result, __instance.props, __instance.modifiers, __instance.postConstraints, __instance.transform, __instance.gameObject);
						result = flag3;
					}
				}
				return result;
			}
		}

		[HarmonyPatch(typeof(PropSpawner_Line), "Spawn")]
		private class PropSpawner_LineSpawnPatch
		{
			private static bool Prefix(PropSpawner_Line __instance, PropSpawner.SpawnData spawnData, ref GameObject __result)
			{
				bool flag = !SessionState.CanGenerate;
				bool result;
				if (flag)
				{
					result = true;
				}
				else
				{
					bool flag2 = __instance.props == null;
					if (flag2)
					{
						result = true;
					}
					else
					{
						bool flag3 = PropSpawnerHelpers.TrySpawn(spawnData, ref __result, __instance.props, __instance.modifiers, __instance.postConstraints, __instance.transform, __instance.gameObject);
						result = flag3;
					}
				}
				return result;
			}
		}

		[HarmonyPatch(typeof(PropSpawner_Sphere), "Clear")]
		private class PropSpawner_SphereClearPatch
		{
			private static void Postfix(PropSpawner_Sphere __instance)
			{
				bool flag = !SessionState.CanGenerate;
				if (!flag)
				{
					__instance.spawnedProps.Clear();
				}
			}
		}

		[HarmonyPatch(typeof(MapHandler), "InitializeMap")]
		private static class MapHandlerInitializeMapPatch
		{
			private static bool Prefix()
			{
				bool flag = SessionState.Is(SessionState.State.WaitingForMapData);
				bool result;
				if (flag)
				{
					result = false;
				}
				else
				{
					bool flag2 = SessionState.Is(SessionState.State.LoadingEditor) || SessionState.Is(SessionState.State.WaitingToStartCustomMap);
					if (flag2)
					{
						MapData.BuildMapData();
						ResourceManager.GetAllResources();
					}
					bool flag3 = !SessionState.Is(SessionState.State.WaitingToStartCustomMap);
					if (flag3)
					{
						result = true;
					}
					else
					{
						GenerationPatches.MapHandlerInitializeMapPatch.LoadCustomMap();
						result = true;
					}
				}
				return result;
			}

			private static void LoadCustomMap()
			{
				SessionState.Set(SessionState.State.LoadingCustomMap);
				GenerationPatches.MapHandlerInitializeMapPatch.AddGroupersToSegments();
				MapSerializer.MapSaveData save = MapSerializer.LoadFromBytes(PlayerInfoManager.currentMapSyncData.mapSaveData);
				Random.InitState(PlayerInfoManager.currentMapSyncData.seed);
				MapLoader.ApplySave(save);
				PropGrouper component = Singleton<MapHandler>.Instance.GetComponent<PropGrouper>();
				bool flag = PlayerInfoManager.LocalPlayerInfo.canBake && PlayerInfoManager.currentMapSyncData.bakeLightMap;
				component.RunAll(flag);
				GenerationPatches.MapHandlerInitializeMapPatch.HandleRemainingPhotonViews();
				FakeItemManager.Instance.RefreshList();
			}

			private static void AddGroupersToSegments()
			{
				List<MapHandler.MapSegment> list = Singleton<MapHandler>.Instance.segments.ToList<MapHandler.MapSegment>();
				list.AddRange(Singleton<MapHandler>.Instance.variantSegments);
				foreach (MapHandler.MapSegment mapSegment in list)
				{
					bool flag = mapSegment.segmentParent.GetComponent<PropGrouper>() == null;
					if (flag)
					{
						mapSegment.segmentParent.gameObject.AddComponent<PropGrouper>();
					}
				}
			}

			private static void HandleRemainingPhotonViews()
			{
				List<PhotonView> allUnassignedViews = GenerationPatches.MapHandlerInitializeMapPatch.GetAllUnassignedViews();
				foreach (PhotonView view in allUnassignedViews)
				{
					ViewSyncManager.AddPendingView(view);
				}
				ViewSyncManager.HandlePendingViews();
			}

			public static List<PhotonView> GetAllUnassignedViews()
			{
				PhotonView[] componentsInChildren = Singleton<MapHandler>.Instance.gameObject.GetComponentsInChildren<PhotonView>(true);
				List<PhotonView> list = new List<PhotonView>();
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					bool flag = componentsInChildren[i].ViewID == 0;
					if (flag)
					{
						list.Add(componentsInChildren[i]);
					}
				}
				return list;
			}
		}
	}
}
