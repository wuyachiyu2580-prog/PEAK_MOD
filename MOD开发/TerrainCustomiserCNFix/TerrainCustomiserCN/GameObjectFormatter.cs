using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Sirenix.Serialization;
using TerrainCustomiserCN.Map;
using TerrainCustomiserCN.Map.Serialization;
using UnityEngine;

public sealed class GameObjectFormatter : MinimalBaseFormatter<GameObject>
{
		protected override void Read(ref GameObject value, IDataReader reader)
		{
			for (;;)
			{
				string a;
				bool flag = reader.PeekEntry(out a) != EntryType.EndOfNode;
				if (!flag)
				{
					break;
				}
			string kv;
			reader.ReadString(out kv);
			bool flag2 = a == "name";
			if (flag2)
			{
				GameObject gameObject = ResourceManager.GameObjectResources.Find((GameObject x) => x.name == kv);
				value = gameObject;
			}
			else
			{
				bool flag3 = a == "targetId";
				if (flag3)
				{
					GameObject gameObject2 = new GameObject(kv);
					gameObject2.SetActive(false);
					value = gameObject2;
					MapSerializer.testgos.Add(gameObject2);
				}
				else
				{
					reader.SkipEntry();
				}
			}
		}
	}

	protected override void Write(ref GameObject value, IDataWriter writer)
	{
			GameObject go = value.gameObject;
			KeyValuePair<string, GameObject> keyValuePair = MapSerializer.GameObjectReferences.FirstOrDefault((KeyValuePair<string, GameObject> x) => x.Value == go);
		bool flag = keyValuePair.Value == null;
		if (flag)
		{
			writer.WriteString("name", value.name);
		}
		else
		{
			writer.WriteString("targetId", keyValuePair.Key);
		}
	}
}
