using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DreamyAscent.Data
{
    internal sealed class DaObjectCatalog
    {
        [JsonProperty("mapKey")]
        public string MapKey { get; set; }

        [JsonProperty("segmentCount")]
        public int SegmentCount { get; set; }

        [JsonProperty("itemCount")]
        public int ItemCount { get; set; }

        [JsonProperty("materialCount")]
        public int MaterialCount { get; set; }

        [JsonProperty("segments")]
        public List<DaCatalogSegment> Segments { get; set; } = new List<DaCatalogSegment>();

        [JsonProperty("items")]
        public List<DaCatalogItem> Items { get; set; } = new List<DaCatalogItem>();

        [JsonProperty("materials")]
        public List<DaCatalogMaterial> Materials { get; set; } = new List<DaCatalogMaterial>();

        [JsonProperty("itemRoleCounts")]
        public Dictionary<string, int> ItemRoleCounts { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

        [JsonProperty("materialRoleCounts")]
        public Dictionary<string, int> MaterialRoleCounts { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    internal sealed class DaCatalogSegment
    {
        [JsonProperty("name")]
        public string Name { get; set; }

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

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("itemIds")]
        public List<string> ItemIds { get; set; } = new List<string>();

        [JsonProperty("materialIds")]
        public List<string> MaterialIds { get; set; } = new List<string>();
    }

    internal sealed class DaCatalogItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("segment")]
        public string Segment { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("stableKey")]
        public string StableKey { get; set; }

        [JsonProperty("objectType")]
        public string ObjectType { get; set; }

        [JsonProperty("gameObjectPath")]
        public string GameObjectPath { get; set; }

        [JsonProperty("scene")]
        public string Scene { get; set; }

        [JsonProperty("hasChildGeneration")]
        public bool HasChildGeneration { get; set; }

        [JsonProperty("hasSingleItemSpawner")]
        public bool HasSingleItemSpawner { get; set; }

        [JsonProperty("hasPhotonView")]
        public bool HasPhotonView { get; set; }

        [JsonProperty("childLevelGenStepCount")]
        public int ChildLevelGenStepCount { get; set; }

        [JsonProperty("childSingleItemSpawnerCount")]
        public int ChildSingleItemSpawnerCount { get; set; }

        [JsonProperty("rendererCount")]
        public int RendererCount { get; set; }

        [JsonProperty("components")]
        public List<string> Components { get; set; } = new List<string>();

        [JsonProperty("rendererMaterials")]
        public List<string> RendererMaterials { get; set; } = new List<string>();

        [JsonProperty("source")]
        public DaCatalogSource Source { get; set; }

        [JsonProperty("defaults")]
        public DaCatalogDefaults Defaults { get; set; }
    }

    internal sealed class DaCatalogMaterial
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("segment")]
        public string Segment { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("stableKey")]
        public string StableKey { get; set; }

        [JsonProperty("objectType")]
        public string ObjectType { get; set; }

        [JsonProperty("shader")]
        public string Shader { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("mainTexture")]
        public string MainTexture { get; set; }

        [JsonProperty("source")]
        public DaCatalogSource Source { get; set; }
    }

    internal sealed class DaCatalogSource
    {
        [JsonProperty("levelSlot")]
        public int LevelSlot { get; set; } = -1;

        [JsonProperty("segment")]
        public string Segment { get; set; }

        [JsonProperty("segmentPath")]
        public string SegmentPath { get; set; }

        [JsonProperty("grouper")]
        public string Grouper { get; set; }

        [JsonProperty("grouperPath")]
        public string GrouperPath { get; set; }

        [JsonProperty("step")]
        public string Step { get; set; }

        [JsonProperty("stepType")]
        public string StepType { get; set; }

        [JsonProperty("stepPath")]
        public string StepPath { get; set; }

        [JsonProperty("variantSelectionType")]
        public string VariantSelectionType { get; set; }

        [JsonProperty("activeVariantNames")]
        public List<string> ActiveVariantNames { get; set; } = new List<string>();

        [JsonProperty("normalizedVariantName")]
        public string NormalizedVariantName { get; set; }

        [JsonProperty("ownerKind")]
        public string OwnerKind { get; set; }

        [JsonProperty("ownerType")]
        public string OwnerType { get; set; }

        [JsonProperty("ownerRuntimeType")]
        public string OwnerRuntimeType { get; set; }

        [JsonProperty("field")]
        public string Field { get; set; }
    }

    internal sealed class DaCatalogDefaults
    {
        [JsonProperty("properties")]
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}


