using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerrainCustomiserCN.Utils
{
	public static class GizmoUtils
	{
		public static void TryDrawGizmos(Transform target, Component component)
		{
			bool flag = component is PropSpawner;
			if (flag)
			{
				PropSpawner propSpawner = (PropSpawner)component;
				GizmoUtils.DrawPropSpawnerGizmos(component.transform, propSpawner.area, propSpawner.rayLength, propSpawner.rayNearCutoff);
			}
			else
			{
				bool flag2 = component is PropSpawner_Line;
				if (flag2)
				{
					PropSpawner_Line propSpawner_Line = (PropSpawner_Line)component;
					Vector3 to = propSpawner_Line.transform.position + propSpawner_Line.height * 0.5f * propSpawner_Line.transform.up;
					DebugVisualiser.DrawLine(propSpawner_Line.transform.position - propSpawner_Line.height * 0.5f * propSpawner_Line.transform.up, to, Color.white, 0f, false);
				}
				else
				{
					bool flag3 = component is PropSpawner_Sphere;
					if (flag3)
					{
						PropSpawner_Sphere propSpawner_Sphere = (PropSpawner_Sphere)component;
						DebugVisualiser.DrawSphere(propSpawner_Sphere.transform.position, propSpawner_Sphere.rayLength, Color.white, 0f, false);
					}
					else
					{
						bool flag4 = component is SpecialDayZone;
						if (flag4)
						{
							SpecialDayZone specialDayZone = (SpecialDayZone)component;
							DebugVisualiser.DrawBox(specialDayZone.transform.position, specialDayZone.bounds.size, Color.white, 0f, false);
						}
					}
				}
			}
		}

		public static void TryDrawOverlays(Transform target)
		{
		}

		public static void TryRestoreMaterials()
		{
		}

		public static void DrawPropSpawnerGizmos(Transform target, Vector2 area, float rayLength, float rayNearCutoff)
		{
			Vector3 vector = target.position + area.y * 0.5f * target.up;
			Vector3 vector2 = target.position - area.y * 0.5f * target.up;
			Vector3 vector3 = target.position - area.x * 0.5f * target.right;
			Vector3 vector4 = target.position + area.x * 0.5f * target.right;
			DebugVisualiser.DrawLine(vector2, vector, Color.white, 0f, false);
			DebugVisualiser.DrawLine(vector3, vector4, Color.white, 0f, false);
			DebugVisualiser.DrawLine(vector2, vector2 + target.forward * rayLength + target.forward * rayNearCutoff, Color.green, 0f, false);
			DebugVisualiser.DrawLine(vector, vector + target.forward * rayLength + target.forward * rayNearCutoff, Color.green, 0f, false);
			DebugVisualiser.DrawLine(vector3, vector3 + target.forward * rayLength + target.forward * rayNearCutoff, Color.green, 0f, false);
			DebugVisualiser.DrawLine(vector4, vector4 + target.forward * rayLength + target.forward * rayNearCutoff, Color.green, 0f, false);
			DebugVisualiser.DrawBox(target.position + target.forward * rayLength / 2f, Vector3.one, target.rotation, Color.green, 0f, false);
			DebugVisualiser.DrawLine(vector2, vector2 + target.forward * rayNearCutoff, Color.red, 0f, false);
			DebugVisualiser.DrawLine(vector3, vector3 + target.forward * rayNearCutoff, Color.red, 0f, false);
			DebugVisualiser.DrawLine(vector, vector + target.forward * rayNearCutoff, Color.red, 0f, false);
			DebugVisualiser.DrawLine(vector4, vector4 + target.forward * rayNearCutoff, Color.red, 0f, false);
		}

		public static List<Renderer> GetRenderersFromObject(GameObject target)
		{
			List<Renderer> list = new List<Renderer>();
			LODGroup[] componentsInChildren = target.GetComponentsInChildren<LODGroup>(true);
			bool flag = componentsInChildren != null && componentsInChildren.Length != 0;
			bool flag2 = flag;
			List<Renderer> result;
			if (flag2)
			{
				foreach (LODGroup lodgroup in componentsInChildren)
				{
					LOD[] lods = lodgroup.GetLODs();
					foreach (LOD lod in lods)
					{
						foreach (Renderer renderer in lod.renderers)
						{
							bool flag3 = renderer == null;
							if (!flag3)
							{
								list.Add(renderer);
							}
						}
					}
				}
				result = list;
			}
			else
			{
				result = target.GetComponentsInChildren<Renderer>(true).ToList<Renderer>();
			}
			return result;
		}

		public static List<GizmoUtils.RendererMaterialCache> ApplyOverlayToRenderers(List<Renderer> renderers)
		{
			List<GizmoUtils.RendererMaterialCache> list = new List<GizmoUtils.RendererMaterialCache>();
			foreach (Renderer renderer in renderers)
			{
				Material[] sharedMaterials = renderer.sharedMaterials;
				Material material = new Material(GizmoUtils.SelectionOverlayShader);
				list.Add(new GizmoUtils.RendererMaterialCache
				{
					Renderer = renderer,
					OriginalMaterials = sharedMaterials,
					OverlayMaterial = material
				});
				Material[] array = new Material[sharedMaterials.Length + 1];
				Array.Copy(sharedMaterials, array, sharedMaterials.Length);
				array[array.Length - 1] = material;
				renderer.sharedMaterials = array;
			}
			return list;
		}

		public static List<GizmoUtils.RendererMaterialCache> ApplyOverlayToRenderers(Renderer[] renderers)
		{
			List<GizmoUtils.RendererMaterialCache> list = new List<GizmoUtils.RendererMaterialCache>();
			foreach (Renderer renderer in renderers)
			{
				Material[] sharedMaterials = renderer.sharedMaterials;
				Material material = new Material(GizmoUtils.SelectionOverlayShader);
				list.Add(new GizmoUtils.RendererMaterialCache
				{
					Renderer = renderer,
					OriginalMaterials = sharedMaterials,
					OverlayMaterial = material
				});
				Material[] array = new Material[sharedMaterials.Length + 1];
				Array.Copy(sharedMaterials, array, sharedMaterials.Length);
				array[array.Length - 1] = material;
				renderer.sharedMaterials = array;
			}
			return list;
		}

		public static void RestoreRendererMaterials(List<GizmoUtils.RendererMaterialCache> rendererCache)
		{
			foreach (GizmoUtils.RendererMaterialCache rendererMaterialCache in rendererCache)
			{
				bool flag = rendererMaterialCache.Renderer == null;
				if (!flag)
				{
					rendererMaterialCache.Renderer.sharedMaterials = rendererMaterialCache.OriginalMaterials;
					bool flag2 = rendererMaterialCache.OverlayMaterial != null;
					if (flag2)
					{
						Object.Destroy(rendererMaterialCache.OverlayMaterial);
					}
				}
			}
			rendererCache.Clear();
		}

		internal static Shader SelectionOverlayShader;

		public class RendererMaterialCache
		{
			public Renderer Renderer;

			public Material[] OriginalMaterials;

			public Material OverlayMaterial;
		}
	}
}
