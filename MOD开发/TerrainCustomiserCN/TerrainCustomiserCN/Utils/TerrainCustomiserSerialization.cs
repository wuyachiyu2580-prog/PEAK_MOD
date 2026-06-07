using System;
using Sirenix.Serialization;
using TerrainCustomiserCN.Managers;
using TerrainCustomiserCN.Map.Serialization;

namespace TerrainCustomiserCN.Utils
{
	internal static class TerrainCustomiserSerialization
	{
		public static SerializationContext CreateSerializationContext()
		{
			return new SerializationContext
			{
				Binder = TerrainCustomiserCompatibilityBinder.Instance
			};
		}

		public static DeserializationContext CreateDeserializationContext()
		{
			return new DeserializationContext
			{
				Binder = TerrainCustomiserCompatibilityBinder.Instance
			};
		}
	}

	internal sealed class TerrainCustomiserCompatibilityBinder : TwoWaySerializationBinder
	{
		public static readonly TerrainCustomiserCompatibilityBinder Instance = new TerrainCustomiserCompatibilityBinder();

		// 保存时写回原版 TerrainCustomiser 类型名，读取时再映射回 TerrainCustomiserCN 类型。
		// 这样中英文 MOD 可以共用同一份地图和房间同步数据。
		private static readonly string[,] TypeNameMappings = new string[,]
		{
			{ typeof(PlayerInfo).FullName + ", TerrainCustomiserCN", "TerrainCustomiser.Managers.PlayerInfo, TerrainCustomiser" },
			{ typeof(MapSerializer.ObjectData).FullName + ", TerrainCustomiserCN", "TerrainCustomiser.Map.Serialization.MapSerializer+ObjectData, TerrainCustomiser" },
			{ typeof(MapSerializer.TransformData).FullName + ", TerrainCustomiserCN", "TerrainCustomiser.Map.Serialization.MapSerializer+TransformData, TerrainCustomiser" },
			{ typeof(MapSerializer.ComponentData).FullName + ", TerrainCustomiserCN", "TerrainCustomiser.Map.Serialization.MapSerializer+ComponentData, TerrainCustomiser" },
			{ typeof(MapSerializer.FieldData).FullName + ", TerrainCustomiserCN", "TerrainCustomiser.Map.Serialization.MapSerializer+FieldData, TerrainCustomiser" },
			{ typeof(MapSerializer.MapSaveData).FullName + ", TerrainCustomiserCN", "TerrainCustomiser.Map.Serialization.MapSerializer+MapSaveData, TerrainCustomiser" },
			{ typeof(MapSerializer.MapSyncData).FullName + ", TerrainCustomiserCN", "TerrainCustomiser.Map.Serialization.MapSerializer+MapSyncData, TerrainCustomiser" },
			{ typeof(MapSerializer.SegmentSaveData).FullName + ", TerrainCustomiserCN", "TerrainCustomiser.Map.Serialization.MapSerializer+SegmentSaveData, TerrainCustomiser" }
		};

		private TerrainCustomiserCompatibilityBinder()
		{
		}

		public override string BindToName(Type type, DebugContext debugContext = null)
		{
			string typeName = TwoWaySerializationBinder.Default.BindToName(type, debugContext);
			return TerrainCustomiserCompatibilityBinder.ReplaceTypeNames(typeName, useOriginalNames: true);
		}

		public override Type BindToType(string typeName, DebugContext debugContext = null)
		{
			// 先尝试按 CN 类型解析原版存档；失败时退回默认绑定，兼容未改名的 Unity/游戏类型。
			string cnTypeName = TerrainCustomiserCompatibilityBinder.ReplaceTypeNames(typeName, useOriginalNames: false);
			if (!string.Equals(cnTypeName, typeName, StringComparison.Ordinal))
			{
				Type type = TwoWaySerializationBinder.Default.BindToType(cnTypeName, debugContext);
				if (type != null)
				{
					return type;
				}
			}
			return TwoWaySerializationBinder.Default.BindToType(typeName, debugContext);
		}

		public override bool ContainsType(string typeName)
		{
			string cnTypeName = TerrainCustomiserCompatibilityBinder.ReplaceTypeNames(typeName, useOriginalNames: false);
			return TwoWaySerializationBinder.Default.ContainsType(typeName) || TwoWaySerializationBinder.Default.ContainsType(cnTypeName);
		}

		private static string ReplaceTypeNames(string typeName, bool useOriginalNames)
		{
			string result = typeName;
			for (int i = 0; i < TerrainCustomiserCompatibilityBinder.TypeNameMappings.GetLength(0); i++)
			{
				string cnName = TerrainCustomiserCompatibilityBinder.TypeNameMappings[i, 0];
				string originalName = TerrainCustomiserCompatibilityBinder.TypeNameMappings[i, 1];
				result = result.Replace(useOriginalNames ? cnName : originalName, useOriginalNames ? originalName : cnName);
			}
			return result;
		}
	}
}
