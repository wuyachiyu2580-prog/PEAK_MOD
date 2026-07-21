using System;
using System.Collections.Generic;
using TerrainCustomiserCN.Utils;
using UnityEngine;

namespace TerrainCustomiserCN.UI
{
	public static class EditorGizmos
	{
		public static void ToggleGizmos()
		{
			EditorGizmos.IsEnabled = !EditorGizmos.IsEnabled;
			bool flag = !EditorGizmos.IsEnabled;
			if (flag)
			{
				GizmoUtils.RestoreRendererMaterials(EditorGizmos.rendererMaterialCache);
			}
			else
			{
				EditorGizmos.RefreshGizmos();
			}
		}

		public static void OnSelectionChanged(Transform selected)
		{
			EditorGizmos.Target = selected;
			bool flag = !EditorGizmos.IsEnabled;
			if (!flag)
			{
				GizmoUtils.RestoreRendererMaterials(EditorGizmos.rendererMaterialCache);
				LevelGenStep component = EditorGizmos.Target.gameObject.GetComponent<LevelGenStep>();
				GizmoUtils.TryDrawGizmos(EditorGizmos.Target, component);
				bool flag2 = component != null;
				if (flag2)
				{
					Renderer[] componentsInChildren = EditorGizmos.Target.gameObject.GetComponentsInChildren<Renderer>(true);
					EditorGizmos.rendererMaterialCache = GizmoUtils.ApplyOverlayToRenderers(componentsInChildren);
				}
			}
		}

		public static void RefreshGizmos()
		{
			bool flag = !EditorGizmos.IsEnabled;
			if (!flag)
			{
				GizmoUtils.RestoreRendererMaterials(EditorGizmos.rendererMaterialCache);
				bool flag2 = EditorGizmos.Target == null;
				if (!flag2)
				{
					LevelGenStep component = EditorGizmos.Target.gameObject.GetComponent<LevelGenStep>();
					GizmoUtils.TryDrawGizmos(EditorGizmos.Target, component);
					bool flag3 = component != null;
					if (flag3)
					{
						Renderer[] componentsInChildren = EditorGizmos.Target.gameObject.GetComponentsInChildren<Renderer>(true);
						EditorGizmos.rendererMaterialCache = GizmoUtils.ApplyOverlayToRenderers(componentsInChildren);
					}
				}
			}
		}

		public static bool IsEnabled = false;

		private static List<GizmoUtils.RendererMaterialCache> rendererMaterialCache = new List<GizmoUtils.RendererMaterialCache>();

		private static Transform Target;
	}
}
