using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Sirenix.Serialization;
using TerrainCustomiserCN.Map.Serialization;
using UnityEngine;

public sealed class TransformFormatter : MinimalBaseFormatter<Transform>
{
		protected override void Read(ref Transform value, IDataReader reader)
		{
			for (;;)
			{
				string a;
				bool flag = reader.PeekEntry(out a) != EntryType.EndOfNode;
				if (!flag)
				{
					break;
				}
			string text;
			reader.ReadString(out text);
			bool flag2 = a == "refid";
			if (flag2)
			{
				GameObject gameObject = new GameObject(text);
				gameObject.SetActive(false);
				value = gameObject.transform;
				MapSerializer.testgos.Add(gameObject);
			}
			else
			{
				bool flag3 = a == "refpath";
				if (flag3)
				{
					GameObject gameObject2 = GameObject.Find(text);
					bool flag4 = gameObject2 != null;
					if (flag4)
					{
						value = gameObject2.transform;
					}
					else
					{
						value = null;
					}
				}
				else
				{
					reader.SkipEntry();
				}
			}
		}
	}

	protected override void Write(ref Transform value, IDataWriter writer)
	{
		GameObject go = value.gameObject;
		KeyValuePair<string, GameObject> keyValuePair = MapSerializer.GameObjectReferences.FirstOrDefault((KeyValuePair<string, GameObject> x) => x.Value == go);
		bool flag = keyValuePair.Value == null;
		if (flag)
		{
			string pathFromGameObject = MapSerializer.GetPathFromGameObject(go);
			writer.WriteString("refpath", pathFromGameObject);
		}
		else
		{
			writer.WriteString("refid", keyValuePair.Key);
		}
	}
}
