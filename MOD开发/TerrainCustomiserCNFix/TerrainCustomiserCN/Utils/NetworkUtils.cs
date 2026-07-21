using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using TerrainCustomiserCN.Managers;
using TerrainCustomiserCN.Map.Serialization;
using UnityEngine;

namespace TerrainCustomiserCN.Utils
{
	public static class NetworkUtils
	{
		public static byte[] CompressBrotli(byte[] data)
		{
			byte[] result;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BrotliStream brotliStream = new BrotliStream(memoryStream, System.IO.Compression.CompressionLevel.Optimal))
				{
					brotliStream.Write(data, 0, data.Length);
				}
				result = memoryStream.ToArray();
			}
			return result;
		}

		public static byte[] DecompressBrotli(byte[] compressed)
		{
			byte[] result;
			using (MemoryStream memoryStream = new MemoryStream(compressed))
			{
				using (BrotliStream brotliStream = new BrotliStream(memoryStream, CompressionMode.Decompress))
				{
					using (MemoryStream memoryStream2 = new MemoryStream())
					{
						brotliStream.CopyTo(memoryStream2);
						result = memoryStream2.ToArray();
					}
				}
			}
			return result;
		}

		public static byte[] SerializeMapSyncData(MapSerializer.MapSyncData data)
		{
			string s = JsonConvert.SerializeObject(data);
			return Encoding.UTF8.GetBytes(s);
		}

		public static MapSerializer.MapSyncData DeserializeMapSyncData(byte[] bytes)
		{
			string @string = Encoding.UTF8.GetString(bytes);
			return JsonConvert.DeserializeObject<MapSerializer.MapSyncData>(@string);
		}

		public static byte[] CompressMapSyncData(MapSerializer.MapSyncData data)
		{
			byte[] data2 = NetworkUtils.SerializeMapSyncData(data);
			return NetworkUtils.CompressBrotli(data2);
		}

		public static MapSerializer.MapSyncData DecompressMapSyncData(byte[] compressed)
		{
			byte[] bytes = NetworkUtils.DecompressBrotli(compressed);
			return NetworkUtils.DeserializeMapSyncData(bytes);
		}

		public static void SetMapDataRoomProperty(MapSerializer.MapSyncData mapSyncData)
		{
			byte[] array = NetworkUtils.CompressMapSyncData(mapSyncData);
			double num = (double)array.Length / 1048576.0;
			Debug.Log(string.Format("Compressed MapSyncData Size: {0:F2} MB", num));
			NetworkManager.SetRoomProperty("mapData", array);
		}
	}
}
