using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TerrainCustomiserCN.TerrainGeneration
{
	public static class AreaVoxeliser
	{
		public static NativeArray<uint> BuildOccupancyGrid(LayerMask occluderMask)
		{
			LightVolume lightVolume = LightVolume.Instance();
			Vector3Int gridRes = lightVolume.gridRes;
			float raySpacing = lightVolume.raySpacing;
			int num = gridRes.x * gridRes.y * gridRes.z;
			AreaVoxeliser.CreateArrays(num);
			float3 offset = lightVolume.gridOffset;
			float3 halfExtents = new float3(raySpacing * 0.5f);
			AreaVoxeliser.BuildOverlapCommandsJob buildOverlapCommandsJob = default(AreaVoxeliser.BuildOverlapCommandsJob);
			buildOverlapCommandsJob.commands = AreaVoxeliser.commands;
			buildOverlapCommandsJob.gridRes = new int3(gridRes.x, gridRes.y, gridRes.z);
			buildOverlapCommandsJob.spacing = raySpacing;
			buildOverlapCommandsJob.offset = offset;
			buildOverlapCommandsJob.halfExtents = halfExtents;
			QueryParameters query = default(QueryParameters);
			query.layerMask = occluderMask;
			query.hitTriggers = QueryTriggerInteraction.Ignore;
			buildOverlapCommandsJob.query = query;
			AreaVoxeliser.BuildOverlapCommandsJob buildOverlapCommandsJob2 = buildOverlapCommandsJob;
			JobHandle jobHandle = IJobParallelForExtensions.Schedule<AreaVoxeliser.BuildOverlapCommandsJob>(buildOverlapCommandsJob2, num, 128, default(JobHandle));
			JobHandle jobHandle2 = OverlapBoxCommand.ScheduleBatch(AreaVoxeliser.commands, AreaVoxeliser.results, 128, 1, jobHandle);
			AreaVoxeliser.ProcessOverlapResultsJob processOverlapResultsJob = new AreaVoxeliser.ProcessOverlapResultsJob
			{
				results = AreaVoxeliser.results,
				occupancy = AreaVoxeliser.occupancyNative
			};
			IJobParallelForExtensions.Schedule<AreaVoxeliser.ProcessOverlapResultsJob>(processOverlapResultsJob, num, 128, jobHandle2).Complete();
			return AreaVoxeliser.occupancyNative;
		}

		private static void CreateArrays(int voxelCount)
		{
			AreaVoxeliser.DisposeArrays();
			AreaVoxeliser.commands = new NativeArray<OverlapBoxCommand>(voxelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
			AreaVoxeliser.results = new NativeArray<ColliderHit>(voxelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
			AreaVoxeliser.occupancyNative = new NativeArray<uint>(voxelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
		}

		public static void DisposeArrays()
		{
			bool isCreated = AreaVoxeliser.commands.IsCreated;
			if (isCreated)
			{
				AreaVoxeliser.commands.Dispose();
			}
			bool isCreated2 = AreaVoxeliser.results.IsCreated;
			if (isCreated2)
			{
				AreaVoxeliser.results.Dispose();
			}
			bool isCreated3 = AreaVoxeliser.occupancyNative.IsCreated;
			if (isCreated3)
			{
				AreaVoxeliser.occupancyNative.Dispose();
			}
		}

		private static NativeArray<OverlapBoxCommand> commands;

		private static NativeArray<ColliderHit> results;

		public static NativeArray<uint> occupancyNative;

		[BurstCompile]
		private struct BuildOverlapCommandsJob : IJobParallelFor
		{
			public void Execute(int index)
			{
				int num = index % this.gridRes.x;
				int num2 = index / this.gridRes.x % this.gridRes.y;
				int num3 = index / (this.gridRes.x * this.gridRes.y);
				float3 @float = new float3(((float)num + 0.5f) * this.spacing, ((float)num2 + 0.5f) * this.spacing, ((float)num3 + 0.5f) * this.spacing);
				@float -= new float3(this.gridRes) * this.spacing * 0.5f;
				@float += this.offset;
				this.commands[index] = new OverlapBoxCommand(@float, this.halfExtents, quaternion.identity, this.query);
			}

			[WriteOnly]
			public NativeArray<OverlapBoxCommand> commands;

			public int3 gridRes;

			public float spacing;

			public float3 offset;

			public float3 halfExtents;

			public QueryParameters query;
		}

		[BurstCompile]
		private struct ProcessOverlapResultsJob : IJobParallelFor
		{
			public void Execute(int index)
			{
				this.occupancy[index] = ((this.results[index].instanceID != 0) ? 1U : 0U);
			}

			[ReadOnly]
			public NativeArray<ColliderHit> results;

			[WriteOnly]
			public NativeArray<uint> occupancy;
		}
	}
}
