using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using TerrainCustomiserCN.UI;
using UnityEngine;

namespace TerrainCustomiserCNCollector
{
	[BepInDependency("com.wuyachiyu.terraincustomisercn")]
	[BepInPlugin("com.wuyachiyu.terraincustomisercn.collector", "TerrainCustomiserCN Translation Collector", "0.1.1")]
	public class Plugin : BaseUnityPlugin
	{
		private static readonly SortedDictionary<string, SortedSet<string>> Missing = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

		private static readonly HashSet<string> PersistedRows = new HashSet<string>(StringComparer.Ordinal);

		private static readonly object SyncRoot = new object();

		private static string outputPath;

		private static bool dirty;

		private static float nextWriteTime;

		private void Awake()
		{
			string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Paths.PluginPath;
			outputPath = Path.Combine(pluginDirectory, "TerrainCustomiserCN_missing_translations.tsv");
			LoadExistingOutput();
			DisplayNameTranslator.MissingTranslationFound += OnMissingTranslationFound;
			CollectStaticReflectionNames();
			AppendOutput();
			Debug.Log("[TCCN Collector] Translation collector enabled. Missing output: " + outputPath);
		}

		private void Update()
		{
			if (dirty && Time.unscaledTime >= nextWriteTime)
			{
				AppendOutput();
				dirty = false;
			}
		}

		private void OnDestroy()
		{
			DisplayNameTranslator.MissingTranslationFound -= OnMissingTranslationFound;
			AppendOutput();
		}

		private static void OnMissingTranslationFound(MissingTranslation missing)
		{
			if (Add(missing.Kind, missing.Key, missing.Source, "Runtime"))
			{
				dirty = true;
				nextWriteTime = Time.unscaledTime + 1f;
			}
		}

		// 启动时扫描 TerrainCustomiser 会反射编辑的类型，提前发现字段、类型和枚举漏译。
		public static void CollectStaticReflectionNames()
		{
			Type[] inspectedTypes = GetInspectableTypes();
			foreach (Type type in inspectedTypes)
			{
				AddIfMissing(TranslationKind.Type, type.Name, type.FullName, "StaticType");
				foreach (FieldInfo fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				{
					AddIfMissing(TranslationKind.Field, fieldInfo.Name, type.FullName, "StaticField");
					Type fieldType = fieldInfo.FieldType;
					if (fieldType.IsEnum)
					{
						CollectEnum(fieldType, type.FullName + "." + fieldInfo.Name);
					}
					else if (fieldType.IsArray && fieldType.GetElementType() != null && fieldType.GetElementType().IsEnum)
					{
						CollectEnum(fieldType.GetElementType(), type.FullName + "." + fieldInfo.Name);
					}
					else if (fieldType.IsGenericType)
					{
						foreach (Type genericArgument in fieldType.GetGenericArguments())
						{
							if (genericArgument.IsEnum)
							{
								CollectEnum(genericArgument, type.FullName + "." + fieldInfo.Name);
							}
						}
					}
				}
			}
		}

		private static Type[] GetInspectableTypes()
		{
			Assembly gameAssembly = typeof(PropSpawner).Assembly;
			Type[] allTypes = gameAssembly.GetTypes();
			Type[] bases = new Type[]
			{
				typeof(LevelGenStep),
				typeof(PropSpawnerMod),
				typeof(PropSpawnerConstraint),
				typeof(PropSpawnerConstraintPost),
				typeof(PostSpawnBehavior)
			};
			HashSet<Type> result = new HashSet<Type>();
			foreach (Type type in allTypes)
			{
				if (type == null || type.IsAbstract || type.IsInterface)
				{
					continue;
				}
				if (bases.Any((Type baseType) => baseType.IsAssignableFrom(type)))
				{
					result.Add(type);
				}
			}
			result.Add(typeof(PropGrouper));
			result.Add(typeof(RockMaterialSwapper));
			result.Add(typeof(SpecialDayZone));
			return result.OrderBy((Type type) => type.FullName, StringComparer.Ordinal).ToArray();
		}

		private static void CollectEnum(Type enumType, string source)
		{
			foreach (string name in Enum.GetNames(enumType))
			{
				if (!DisplayNameTranslator.HasTranslation(TranslationKind.Enum, name, enumType))
				{
					Add(TranslationKind.Enum, enumType.FullName + "." + name, source, "StaticEnum");
				}
			}
		}

		private static void AddIfMissing(TranslationKind kind, string key, string source, string origin)
		{
			if (!DisplayNameTranslator.HasTranslation(kind, key))
			{
				Add(kind, key, source, origin);
			}
		}

		private static bool Add(TranslationKind kind, string key, string source, string origin)
		{
			if (string.IsNullOrWhiteSpace(key) || !key.Any(IsAsciiLetter))
			{
				return false;
			}
			string cleanSource = string.IsNullOrEmpty(source) ? "-" : source;
			string line = Escape(key) + "\t" + Escape(cleanSource) + "\t" + Escape(origin);
			lock (SyncRoot)
			{
				string kindKey = kind.ToString();
				if (!Missing.TryGetValue(kindKey, out SortedSet<string> values))
				{
					values = new SortedSet<string>(StringComparer.Ordinal);
					Missing.Add(kindKey, values);
				}
				return values.Add(line);
			}
		}

		// 缺失项只追加到 TSV，主 MOD 不会运行时查询这份文件，避免影响编辑器性能。
		private static void WriteOutput()
		{
			AppendOutput();
		}

		private static void LoadExistingOutput()
		{
			if (string.IsNullOrEmpty(outputPath) || !File.Exists(outputPath))
			{
				return;
			}
			lock (SyncRoot)
			{
				foreach (string rawLine in File.ReadLines(outputPath, Encoding.UTF8))
				{
					string line = rawLine.TrimStart('\ufeff');
					if (string.IsNullOrWhiteSpace(line) || line.StartsWith("kind\t", StringComparison.Ordinal))
					{
						continue;
					}
					string[] columns = line.Split('\t');
					if (columns.Length < 4)
					{
						continue;
					}
					AddLoadedRow(Unescape(columns[0]), Unescape(columns[1]), Unescape(columns[2]), Unescape(columns[3]));
				}
			}
		}

		private static void AddLoadedRow(string kind, string key, string source, string origin)
		{
			if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(key))
			{
				return;
			}
			string line = Escape(key) + "\t" + Escape(source) + "\t" + Escape(origin);
			if (!Missing.TryGetValue(kind, out SortedSet<string> values))
			{
				values = new SortedSet<string>(StringComparer.Ordinal);
				Missing.Add(kind, values);
			}
			values.Add(line);
			PersistedRows.Add(kind + "\t" + line);
		}

		private static void AppendOutput()
		{
			if (string.IsNullOrEmpty(outputPath))
			{
				return;
			}
			lock (SyncRoot)
			{
				string outputDirectory = Path.GetDirectoryName(outputPath);
				if (!string.IsNullOrEmpty(outputDirectory))
				{
					Directory.CreateDirectory(outputDirectory);
				}
				bool writeHeader = !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0L;
				List<string> rowsToAppend = new List<string>();
				foreach (KeyValuePair<string, SortedSet<string>> pair in Missing)
				{
					foreach (string line in pair.Value)
					{
						string fullLine = pair.Key + "\t" + line;
						if (PersistedRows.Add(fullLine))
						{
							rowsToAppend.Add(fullLine);
						}
					}
				}
				if (!writeHeader && rowsToAppend.Count == 0)
				{
					return;
				}
				using (StreamWriter writer = new StreamWriter(outputPath, true, new UTF8Encoding(true)))
				{
					if (writeHeader)
					{
						writer.WriteLine("kind\tkey\tsource\torigin");
					}
					foreach (string line in rowsToAppend)
					{
						writer.WriteLine(line);
					}
				}
			}
		}

		private static bool IsAsciiLetter(char c)
		{
			return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
		}

		private static string Escape(string value)
		{
			return (value ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
		}

		private static string Unescape(string value)
		{
			string result = value ?? string.Empty;
			if (result.Length >= 2 && result[0] == '"' && result[result.Length - 1] == '"')
			{
				result = result.Substring(1, result.Length - 2).Replace("\"\"", "\"");
			}
			return result;
		}
	}
}
