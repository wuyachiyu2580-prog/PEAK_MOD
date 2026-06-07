using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TerrainCustomiserCN.TerrainGeneration
{
	public static class LightMapBaker
	{
		public static void RunBake()
		{
			LightVolume lightVolume = LightVolume.Instance();
			lightVolume.lightMap = null;
			Shader.SetGlobalTexture("_LightMap", null);
			Stopwatch stopwatch = Stopwatch.StartNew();
			NativeArray<uint> occupancy = AreaVoxeliser.BuildOccupancyGrid(lightVolume.occluderMask);
			stopwatch.Stop();
			Debug.Log("[TerrainCustomiserCN] Occupancy took: " + stopwatch.ElapsedMilliseconds.ToString());
			stopwatch = Stopwatch.StartNew();
			RenderTexture renderTexture = LightMapBaker.DispatchAndCreateRenderTexture(LightMapBaker.computeShader, occupancy, lightVolume.gridRes, LightMapBaker.rayCount, LightMapBaker.maxSteps, lightVolume.skyColor);
			AreaVoxeliser.DisposeArrays();
			RenderTexture renderTexture2 = lightVolume.RunBlur(renderTexture);
			renderTexture2.name = "LightVolumeRenderTexture";
			lightVolume.SetShaderVars();
			lightVolume.SaveTex(renderTexture2, null);
			stopwatch.Stop();
			Debug.Log("[TerrainCustomiserCN] Visibility took: " + stopwatch.ElapsedMilliseconds.ToString());
		}

		private static RenderTexture DispatchAndCreateRenderTexture(ComputeShader cs, NativeArray<uint> occupancy, Vector3Int gridRes, int rayCount, int maxSteps, Color skyColor)
		{
			int num = cs.FindKernel("SkyVisibilityDDA");
			int num2 = gridRes.x * gridRes.y * gridRes.z;
			ComputeBuffer computeBuffer = new ComputeBuffer(num2, 4);
			computeBuffer.SetData<uint>(occupancy);
			List<GpuLightData> list = LightMapBaker.BuildLights();
			ComputeBuffer computeBuffer2 = new ComputeBuffer(list.Count, 52);
			computeBuffer2.SetData<GpuLightData>(list);
			RenderTexture renderTexture = new RenderTexture(gridRes.x, gridRes.y, 0)
			{
				enableRandomWrite = true,
				format = RenderTextureFormat.ARGBHalf,
				dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
				volumeDepth = gridRes.z,
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			renderTexture.Create();
			LightVolume lightVolume = LightVolume.Instance();
			cs.SetInts("_GridRes", new int[]
			{
				gridRes.x,
				gridRes.y,
				gridRes.z
			});
			cs.SetVector("_GridOffset", lightVolume.gridOffset);
			cs.SetFloat("_RaySpacing", lightVolume.raySpacing);
			cs.SetBuffer(num, "_Occupancy", computeBuffer);
			cs.SetTexture(num, "_LightMap", renderTexture);
			cs.SetInt("_RayCount", rayCount);
			cs.SetInt("_MaxSteps", maxSteps);
			cs.SetVector("_SkyColor", skyColor);
			cs.SetBuffer(num, "_LightBuffer", computeBuffer2);
			cs.SetInt("_LightBufferLength", list.Count);
			int num3 = (gridRes.x + 3) / 4;
			int num4 = (gridRes.y + 3) / 4;
			int num5 = (gridRes.z + 3) / 4;
			cs.Dispatch(num, num3, num4, num5);
			computeBuffer.Release();
			computeBuffer2.Release();
			return renderTexture;
		}

		public static List<GpuLightData> BuildLights()
		{
			LightVolume lightVolume = LightVolume.Instance();
			List<GpuLightData> list = new List<GpuLightData>();
			GameObject gameObject = (lightVolume.sceneParent == null) ? lightVolume.gameObject : lightVolume.sceneParent;
			BakedVolumeLight[] componentsInChildren = gameObject.GetComponentsInChildren<BakedVolumeLight>();
			foreach (BakedVolumeLight bakedVolumeLight in componentsInChildren)
			{
				Vector3 vector = new Vector3(bakedVolumeLight.color.r, bakedVolumeLight.color.g, bakedVolumeLight.color.b);
				BakedVolumeLight.LightModes mode = bakedVolumeLight.mode;
				float num;
				if (mode == BakedVolumeLight.LightModes.Point)
				{
					num = 0f;
				}
				else if (mode == BakedVolumeLight.LightModes.Spot)
				{
					num = bakedVolumeLight.coneSize * 0.017453292f;
				}
				else
				{
					throw new Exception();
				}
				float coneSize = num;
				List<GpuLightData> list2 = list;
				GpuLightData item = default(GpuLightData);
				item.Position = bakedVolumeLight.transform.position;
				item.ConeSize = coneSize;
				item.Direction = bakedVolumeLight.transform.forward;
				item.Radius = bakedVolumeLight.GetRadius();
				item.Color = vector * bakedVolumeLight.intensity;
				item.Falloff = bakedVolumeLight.falloff;
				item.ConeFalloff = bakedVolumeLight.coneFalloff;
				list2.Add(item);
			}
			int count = list.Count;
			bool flag = count == 0;
			if (flag)
			{
				list.Add(default(GpuLightData));
			}
			return list;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct GpuLightData
		{
			public Vector3 Position;

			public float ConeSize;

			public Vector3 Direction;

			public float Radius;

			public Vector3 Color;

			public float Falloff;

			public float ConeFalloff;
		}

		public static ComputeShader computeShader;

		public static int rayCount = 128;

		public static int maxSteps = 64;
	}
}
