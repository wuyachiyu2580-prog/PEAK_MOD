using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WhereIsThing
{
    internal enum ThingPresetShareMode
    {
        Off,
        BuiltInOnly,
        PublishedPresets
    }

    internal sealed class ThingPresetDefinition
    {
        public ThingPresetDefinition(string id, string name)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Name = string.IsNullOrWhiteSpace(name) ? ThingUi.Text("Preset", "预设") : name;
            SelectedItemIds = new HashSet<ushort>();
            SelectedLuggageTypes = new HashSet<ThingLuggageType>();
            SelectedSceneTargetTypes = new HashSet<ThingSceneTargetType>();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public bool Published { get; set; }
        public HashSet<ushort> SelectedItemIds { get; private set; }
        public HashSet<ThingLuggageType> SelectedLuggageTypes { get; private set; }
        public HashSet<ThingSceneTargetType> SelectedSceneTargetTypes { get; private set; }

        public ThingPresetDefinition Clone(string newId = null, string newName = null)
        {
            ThingPresetDefinition copy = new ThingPresetDefinition(newId ?? Id, newName ?? Name)
            {
                Published = Published
            };
            copy.SelectedItemIds.UnionWith(SelectedItemIds);
            copy.SelectedLuggageTypes.UnionWith(SelectedLuggageTypes);
            copy.SelectedSceneTargetTypes.UnionWith(SelectedSceneTargetTypes);
            return copy;
        }

        public void Normalize()
        {
        }
    }

    internal static class ThingPresetCodec
    {
        private const char RecordSeparator = '\n';
        private const char FieldSeparator = '|';

        public static string SerializePresets(IEnumerable<ThingPresetDefinition> presets)
        {
            if (presets == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            foreach (ThingPresetDefinition preset in presets)
            {
                if (preset == null)
                {
                    continue;
                }

                preset.Normalize();
                if (builder.Length > 0)
                {
                    builder.Append(RecordSeparator);
                }

                builder.Append(preset.Id ?? string.Empty);
                builder.Append(FieldSeparator);
                builder.Append(EncodeText(preset.Name ?? string.Empty));
                builder.Append(FieldSeparator);
                builder.Append(preset.Published ? "1" : "0");
                builder.Append(FieldSeparator);
                builder.Append(string.Join(",", preset.SelectedItemIds.OrderBy(value => value).Select(value => value.ToString()).ToArray()));
                builder.Append(FieldSeparator);
                builder.Append(string.Join(",", preset.SelectedLuggageTypes.OrderBy(value => (int)value).Select(value => value.ToString()).ToArray()));
                builder.Append(FieldSeparator);
                builder.Append(string.Join(",", preset.SelectedSceneTargetTypes.OrderBy(value => (int)value).Select(value => value.ToString()).ToArray()));
            }

            return builder.ToString();
        }

        public static List<ThingPresetDefinition> DeserializePresets(string payload)
        {
            List<ThingPresetDefinition> result = new List<ThingPresetDefinition>();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return result;
            }

            foreach (string line in payload.Split(new[] { RecordSeparator }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split(FieldSeparator);
                if (parts.Length < 6)
                {
                    continue;
                }

                ThingPresetDefinition preset = new ThingPresetDefinition(parts[0], DecodeText(parts[1]))
                {
                    Published = parts[2] == "1"
                };

                foreach (string token in parts[3].Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    ushort itemId;
                    if (ushort.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId))
                    {
                        preset.SelectedItemIds.Add(itemId);
                    }
                }

                foreach (string token in parts[4].Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    ThingLuggageType luggageType;
                    if (Enum.TryParse(token, true, out luggageType) && Enum.IsDefined(typeof(ThingLuggageType), luggageType))
                    {
                        preset.SelectedLuggageTypes.Add(luggageType);
                    }
                }

                foreach (string token in parts[5].Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    ThingSceneTargetType sceneTargetType;
                    if (Enum.TryParse(token, true, out sceneTargetType) && Enum.IsDefined(typeof(ThingSceneTargetType), sceneTargetType))
                    {
                        preset.SelectedSceneTargetTypes.Add(sceneTargetType);
                    }
                }

                preset.Normalize();
                result.Add(preset);
            }

            return result;
        }

        private static string EncodeText(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string DecodeText(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
            }
            catch
            {
                return value ?? string.Empty;
            }
        }
    }

    internal static class ThingPresetFactory
    {
        public const string AchievementPresetId = "preset-achievement";
        public const string SurvivalPresetId = "preset-survival";
        public const string AscentEightPresetId = "preset-ascent-8";
        public const string PlayerPlacedPresetId = "preset-player-placed";

        public static ThingPresetDefinition CreateAchievementPreset()
        {
            ThingPresetDefinition preset = new ThingPresetDefinition(AchievementPresetId, ThingUi.AchievementPresetName())
            {
                Published = true
            };
            preset.SelectedLuggageTypes.Add(ThingLuggageType.Clown);
            preset.SelectedSceneTargetTypes.Add(ThingSceneTargetType.GloomBellTower);
            preset.Normalize();
            return preset;
        }

        public static ThingPresetDefinition CreateSurvivalPreset()
        {
            ThingPresetDefinition preset = new ThingPresetDefinition(SurvivalPresetId, ThingUi.SurvivalPresetName())
            {
                Published = true
            };
            preset.Normalize();
            return preset;
        }

        public static ThingPresetDefinition CreateAscentEightPreset()
        {
            ThingPresetDefinition preset = new ThingPresetDefinition(AscentEightPresetId, ThingUi.AscentEightPresetName())
            {
                Published = true
            };
            preset.Normalize();
            return preset;
        }

        public static ThingPresetDefinition CreatePlayerPlacedPreset()
        {
            ThingPresetDefinition preset = new ThingPresetDefinition(PlayerPlacedPresetId, ThingUi.PlayerPlacedPresetName())
            {
                Published = true
            };
            preset.Normalize();
            return preset;
        }

        public static ThingPresetDefinition CreateCustomPreset(int index)
        {
            return new ThingPresetDefinition(Guid.NewGuid().ToString("N"), ThingUi.CustomPresetName(index))
            {
                Published = false
            };
        }

        public static bool IsBuiltInPresetId(string presetId)
        {
            return string.Equals(presetId, AchievementPresetId, StringComparison.Ordinal) ||
                string.Equals(presetId, SurvivalPresetId, StringComparison.Ordinal) ||
                string.Equals(presetId, AscentEightPresetId, StringComparison.Ordinal) ||
                string.Equals(presetId, PlayerPlacedPresetId, StringComparison.Ordinal);
        }

        public static string GetDisplayName(ThingPresetDefinition preset)
        {
            if (preset == null)
            {
                return string.Empty;
            }

            if (string.Equals(preset.Id, AchievementPresetId, StringComparison.Ordinal))
            {
                return ThingUi.AchievementPresetName();
            }

            if (string.Equals(preset.Id, SurvivalPresetId, StringComparison.Ordinal))
            {
                return ThingUi.SurvivalPresetName();
            }

            if (string.Equals(preset.Id, AscentEightPresetId, StringComparison.Ordinal))
            {
                return ThingUi.AscentEightPresetName();
            }

            if (string.Equals(preset.Id, PlayerPlacedPresetId, StringComparison.Ordinal))
            {
                return ThingUi.PlayerPlacedPresetName();
            }

            return preset.Name;
        }

        public static int ResolveBuiltInItemTargets(ThingPresetDefinition preset, IEnumerable<ThingTargetDefinition> catalog)
        {
            if (preset == null || catalog == null || preset.SelectedItemIds.Count > 0)
            {
                return 0;
            }

            string[] localizationKeys;
            string[] englishNames;
            if (string.Equals(preset.Id, SurvivalPresetId, StringComparison.Ordinal))
            {
                localizationKeys = new string[0];
                englishNames = new[]
                {
                    "First Aid Kit",
                    "Bandages",
                    "Medicinal Root",
                    "Remedy Fungus"
                };
            }
            else if (string.Equals(preset.Id, AscentEightPresetId, StringComparison.Ordinal))
            {
                localizationKeys = new[]
                {
                    "NAME_AMULET_HEALING",
                    "NAME_AMULET_CLONE",
                    "NAME_AMULET_DOUBLEJUMP",
                    "NAME_AMULET_INFINITESTAM"
                };
                englishNames = new[]
                {
                    "Scout's Tenacity",
                    "Scout's Generosity",
                    "Scout's Initiative",
                    "Scout's Ambition"
                };
            }
            else if (string.Equals(preset.Id, PlayerPlacedPresetId, StringComparison.Ordinal))
            {
                localizationKeys = new string[0];
                englishNames = new string[0];
            }
            else
            {
                return 0;
            }

            int before = preset.SelectedItemIds.Count;
            foreach (ThingTargetDefinition definition in catalog)
            {
                if (definition == null || definition.IsLuggage || definition.IsSceneTarget)
                {
                    continue;
                }

                string displayName = definition.GetDisplayName(ThingNameLanguage.English);
                bool keyMatch = definition.Prefabs.Any(item => item != null && item.UIData != null &&
                    localizationKeys.Any(key => string.Equals(item.UIData.itemName, key, StringComparison.OrdinalIgnoreCase)));
                bool nameMatch = englishNames.Any(name => string.Equals(NormalizeTargetName(displayName),
                    NormalizeTargetName(name), StringComparison.OrdinalIgnoreCase));
                bool prefabMatch = string.Equals(preset.Id, PlayerPlacedPresetId, StringComparison.Ordinal) &&
                    definition.Prefabs.Any(item => item != null && item.gameObject != null &&
                        PlayerPlacedPrefabNames.Any(name => string.Equals(item.gameObject.name, name,
                            StringComparison.OrdinalIgnoreCase)));
                if (keyMatch || nameMatch || prefabMatch)
                {
                    preset.SelectedItemIds.UnionWith(definition.ItemIds);
                }
            }
            return preset.SelectedItemIds.Count - before;
        }

        private static string NormalizeTargetName(string value)
        {
            return (value ?? string.Empty).Trim().Replace('\u2018', '\'').Replace('\u2019', '\'');
        }

        private static readonly string[] PlayerPlacedPrefabNames =
        {
            "Flag_Plantable_Checkpoint",
            "BounceShroom",
            "ShelfShroom",
            "CloudFungus",
            "ScoutCannonItem",
            "ChainShooter",
            "ClimbingSpike",
            "RopeSpool",
            "Anti-Rope Spool",
            "RopeShooter",
            "RopeShooterAnti"
        };

        public static string BuildSummary(ThingPresetDefinition preset, IEnumerable<ThingTargetDefinition> catalog)
        {
            if (preset == null)
            {
                return string.Empty;
            }

            List<string> pieces = new List<string>();
            if (catalog != null)
            {
                List<string> names = catalog
                    .Where(definition => definition != null && definition.IsSelected(preset.SelectedItemIds, preset.SelectedLuggageTypes, preset.SelectedSceneTargetTypes))
                    .Select(definition => definition.GetDisplayName(ThingNameLanguage.Game))
                    .Distinct()
                    .OrderBy(value => value)
                    .Take(6)
                    .ToList();

                if (names.Count > 0)
                {
                    pieces.Add(string.Join(ThingUi.IsChinese ? "、" : ", ", names.ToArray()));
                }
            }

            if (pieces.Count == 0)
            {
                pieces.Add(ThingUi.NoTargets());
            }

            if (IsBuiltInPresetId(preset.Id))
            {
                    pieces.Add(ThingUi.BuiltInTag());
            }
            else if (preset.Published)
            {
                pieces.Add(ThingUi.PublishedTag());
            }

            return string.Join("  |  ", pieces.ToArray());
        }
    }
}
