using System;
using System.Runtime.CompilerServices;
using TerrainCustomiserCN.TerrainGeneration;
using TerrainCustomiserCN.Utils;
using UnityEngine;

namespace TerrainCustomiserCN.Managers
{
	internal static class BundleManager
	{
		internal static void Setup()
		{
			byte[] selectionoverlay = Resource1.selectionoverlay;
			BundleManager.DebugShaderBundle = AssetBundle.LoadFromMemory(selectionoverlay);
			GizmoUtils.SelectionOverlayShader = BundleManager.DebugShaderBundle.LoadAsset<Shader>("SelectionOverlay.shader");
			byte[] visibilitycompute = Resource1.visibilitycompute;
			BundleManager.VisibilityComputeBundle = AssetBundle.LoadFromMemory(visibilitycompute);
			LightMapBaker.computeShader = BundleManager.VisibilityComputeBundle.LoadAsset<ComputeShader>("SkyVisibilityDDA");
		}

		internal static void UnloadAll()
		{
			BundleManager.DebugShaderBundle.Unload(true);
			BundleManager.VisibilityComputeBundle.Unload(true);
		}

		internal static AssetBundle DebugShaderBundle;

		internal static AssetBundle VisibilityComputeBundle;
	}
}
