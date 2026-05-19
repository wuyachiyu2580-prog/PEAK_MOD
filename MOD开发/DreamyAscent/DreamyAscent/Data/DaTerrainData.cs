using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace DreamyAscent.Data
{
    internal sealed class DaTerrainData
    {
        [JsonProperty("map")]
        public DaMapData Map { get; set; } = new DaMapData();

        [JsonProperty("settings")]
        public DaSettingsData Settings { get; set; } = new DaSettingsData();
    }

    internal sealed class DaMapData
    {
        [JsonProperty("mapKey")]
        public string MapKey { get; set; }

        [JsonProperty("segments")]
        public List<DaSegmentData> Segments { get; set; } = new List<DaSegmentData>();
    }

    internal sealed class DaSegmentData
    {
        private List<DaSubAreaData> _subAreas = new List<DaSubAreaData>();
        private List<DaPlacementRuleData> _placementRules = new List<DaPlacementRuleData>();

        [JsonProperty("segmentName")]
        public string SegmentName { get; set; }

        [JsonProperty("levelSlot")]
        public int LevelSlot { get; set; } = -1;

        [JsonProperty("segmentPath")]
        public string SegmentPath { get; set; }

        [JsonProperty("variantSelectionType")]
        public string VariantSelectionType { get; set; }

        [JsonProperty("activeVariantNames")]
        public List<string> ActiveVariantNames { get; set; } = new List<string>();

        [JsonProperty("activeVariantPaths")]
        public List<string> ActiveVariantPaths { get; set; } = new List<string>();

        [JsonProperty("normalizedVariantName")]
        public string NormalizedVariantName { get; set; }

        [JsonProperty("rootPaths")]
        public List<string> RootPaths { get; set; } = new List<string>();

        [JsonProperty("editMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public DaSegmentEditMode EditMode { get; set; } = DaSegmentEditMode.OfficialTemplate;

        [JsonProperty("subAreas", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<DaSubAreaData> SubAreas
        {
            get
            {
                return _subAreas;
            }
            set
            {
                _subAreas = value ?? new List<DaSubAreaData>();
                HasSubAreasSpecified = true;
            }
        }

        [JsonProperty("placementRules", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<DaPlacementRuleData> PlacementRules
        {
            get
            {
                return _placementRules;
            }
            set
            {
                _placementRules = value ?? new List<DaPlacementRuleData>();
                HasPlacementRulesSpecified = true;
            }
        }

        [JsonProperty("groupers")]
        public List<DaPropGrouperData> Groupers { get; set; } = new List<DaPropGrouperData>();

        [JsonIgnore]
        public List<Transform> SourceRoots { get; set; } = new List<Transform>();

        [JsonIgnore]
        public MapHandler.MapSegment SourceSegment { get; set; }

        [JsonIgnore]
        public bool HasSubAreasSpecified { get; private set; }

        [JsonIgnore]
        public bool HasPlacementRulesSpecified { get; private set; }

        [JsonIgnore]
        public bool HasPlacementConfigSpecified
        {
            get
            {
                return HasSubAreasSpecified || HasPlacementRulesSpecified;
            }
        }

        public void MarkSubAreasSpecified()
        {
            HasSubAreasSpecified = true;
        }

        public void MarkPlacementRulesSpecified()
        {
            HasPlacementRulesSpecified = true;
        }

        public bool ShouldSerializeSubAreas()
        {
            return HasSubAreasSpecified || _subAreas != null && _subAreas.Count > 0;
        }

        public bool ShouldSerializePlacementRules()
        {
            return HasPlacementRulesSpecified || _placementRules != null && _placementRules.Count > 0;
        }
    }

    internal enum DaSegmentEditMode
    {
        OfficialTemplate,
        CustomBlank,
        Hybrid
    }

    internal enum DaSubAreaShape
    {
        SegmentBounds,
        Box,
        Circle
    }

    internal enum DaPlacementMode
    {
        RandomInSubArea,
        SurfaceRaycast
    }

    internal enum DaPlacementRotationMode
    {
        KeepPrefab,
        RandomYaw,
        AlignToSurfaceNormal
    }

    internal enum DaPlacementOwnershipMode
    {
        DreamyAscentRuntime,
        SegmentChild
    }

    internal sealed class DaVector3Data
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("z")]
        public float Z { get; set; }

        public static DaVector3Data Zero()
        {
            return new DaVector3Data();
        }

        public static DaVector3Data One()
        {
            return new DaVector3Data
            {
                X = 1f,
                Y = 1f,
                Z = 1f
            };
        }

        public static DaVector3Data AreaSize()
        {
            return new DaVector3Data
            {
                X = 40f,
                Y = 30f,
                Z = 40f
            };
        }
    }

    internal sealed class DaSubAreaData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("shape")]
        [JsonConverter(typeof(StringEnumConverter))]
        public DaSubAreaShape Shape { get; set; } = DaSubAreaShape.SegmentBounds;

        [JsonProperty("centerOffset")]
        public DaVector3Data CenterOffset { get; set; } = DaVector3Data.Zero();

        [JsonProperty("size")]
        public DaVector3Data Size { get; set; } = DaVector3Data.AreaSize();

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;
    }

    internal sealed class DaPlacementRuleData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("registryId")]
        public string RegistryId { get; set; }

        [JsonProperty("registryDisplayName")]
        public string RegistryDisplayName { get; set; }

        [JsonProperty("targetSubAreaId")]
        public string TargetSubAreaId { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; } = 1;

        [JsonProperty("minScale")]
        public float MinScale { get; set; } = 1f;

        [JsonProperty("maxScale")]
        public float MaxScale { get; set; } = 1f;

        [JsonProperty("placementMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public DaPlacementMode PlacementMode { get; set; } = DaPlacementMode.SurfaceRaycast;

        [JsonProperty("rotationMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public DaPlacementRotationMode RotationMode { get; set; } = DaPlacementRotationMode.RandomYaw;

        [JsonProperty("ownershipMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public DaPlacementOwnershipMode OwnershipMode { get; set; } = DaPlacementOwnershipMode.DreamyAscentRuntime;

        [JsonProperty("localOffset")]
        public DaVector3Data LocalOffset { get; set; } = DaVector3Data.Zero();

        [JsonIgnore]
        public string CountEditText { get; set; }

        [JsonIgnore]
        public string MinScaleEditText { get; set; }

        [JsonIgnore]
        public string MaxScaleEditText { get; set; }
    }

    internal sealed class DaPropGrouperData
    {
        [JsonProperty("grouperName")]
        public string GrouperName { get; set; }

        [JsonProperty("hierarchyPath")]
        public string HierarchyPath { get; set; }

        [JsonProperty("steps")]
        public List<DaLevelGenStepData> Steps { get; set; } = new List<DaLevelGenStepData>();

        [JsonIgnore]
        public PropGrouper SourceObject { get; set; }
    }

    internal sealed class DaLevelGenStepData
    {
        [JsonProperty("stepName")]
        public string StepName { get; set; }

        [JsonProperty("stepType")]
        public string StepType { get; set; }

        [JsonProperty("hierarchyPath")]
        public string HierarchyPath { get; set; }

        [JsonProperty("properties")]
        public List<DaPropertyData> Properties { get; set; } = new List<DaPropertyData>();

        [JsonProperty("modifiers")]
        public List<DaConstraintData> Modifiers { get; set; } = new List<DaConstraintData>();

        [JsonProperty("constraints")]
        public List<DaConstraintData> Constraints { get; set; } = new List<DaConstraintData>();

        [JsonProperty("postConstraints")]
        public List<DaConstraintData> PostConstraints { get; set; } = new List<DaConstraintData>();

        [JsonIgnore]
        public LevelGenStep SourceObject { get; set; }
    }

    internal sealed class DaConstraintData
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("properties")]
        public List<DaPropertyData> Properties { get; set; } = new List<DaPropertyData>();

        [JsonIgnore]
        public object SourceObject { get; set; }
    }

    internal sealed class DaPropertyData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("initialValue")]
        public object InitialValue { get; set; }

        [JsonIgnore]
        public string EditText { get; set; }

        [JsonIgnore]
        public string[] EditParts { get; set; }
    }

    internal sealed class DaSettingsData
    {
        [JsonProperty("useRandomSeed")]
        public bool UseRandomSeed { get; set; } = true;

        [JsonProperty("seed")]
        public int Seed { get; set; }

        [JsonProperty("enableSnowStorm")]
        public bool EnableSnowStorm { get; set; } = true;

        [JsonProperty("enableRain")]
        public bool EnableRain { get; set; } = true;
    }
}


