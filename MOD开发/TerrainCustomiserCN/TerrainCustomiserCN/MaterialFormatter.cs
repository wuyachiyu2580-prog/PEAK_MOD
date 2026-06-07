using System;
using System.Runtime.CompilerServices;
using Sirenix.Serialization;
using TerrainCustomiserCN.Map;
using UnityEngine;

public sealed class MaterialFormatter : MinimalBaseFormatter<Material>
{
		protected override void Read(ref Material value, IDataReader reader)
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
				Material material = ResourceManager.MaterialResources.Find((Material x) => x != null && x.name == kv);
				value = material;
			}
			else
			{
				reader.SkipEntry();
			}
		}
	}

	protected override void Write(ref Material value, IDataWriter writer)
	{
		writer.WriteString("name", value.name);
	}
}
