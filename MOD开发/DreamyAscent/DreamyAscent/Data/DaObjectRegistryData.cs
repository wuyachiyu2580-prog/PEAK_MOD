using System.Collections.Generic;
using Newtonsoft.Json;

namespace DreamyAscent.Data
{
    internal sealed class DaObjectRegistry
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("generatedAt")]
        public string GeneratedAt { get; set; }

        [JsonProperty("summary")]
        public DaObjectRegistrySummary Summary { get; set; }

        [JsonProperty("templates")]
        public List<DaObjectRegistryTemplate> Templates { get; set; } = new List<DaObjectRegistryTemplate>();

        [JsonProperty("materials")]
        public List<DaObjectRegistryMaterial> Materials { get; set; } = new List<DaObjectRegistryMaterial>();
    }

    internal sealed class DaObjectRegistrySummary
    {
        [JsonProperty("templateCount")]
        public int TemplateCount { get; set; }

        [JsonProperty("materialCount")]
        public int MaterialCount { get; set; }

        [JsonProperty("technicalLowRiskPlacementCandidateCount")]
        public int TechnicalLowRiskPlacementCandidateCount { get; set; }

        [JsonProperty("recommendedFirstPassCandidateCount")]
        public int RecommendedFirstPassCandidateCount { get; set; }

        [JsonProperty("itemRoleCounts")]
        public Dictionary<string, int> ItemRoleCounts { get; set; } = new Dictionary<string, int>();

        [JsonProperty("materialRoleCounts")]
        public Dictionary<string, int> MaterialRoleCounts { get; set; } = new Dictionary<string, int>();

        [JsonProperty("riskTagCounts")]
        public Dictionary<string, int> RiskTagCounts { get; set; } = new Dictionary<string, int>();
    }

    internal sealed class DaObjectRegistryTemplate
    {
        [JsonProperty("registryId")]
        public string RegistryId { get; set; }

        [JsonProperty("stableKey")]
        public string StableKey { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("objectType")]
        public string ObjectType { get; set; }

        [JsonProperty("gameObjectPath")]
        public string GameObjectPath { get; set; }

        [JsonProperty("scene")]
        public string Scene { get; set; }

        [JsonProperty("roles")]
        public List<string> Roles { get; set; } = new List<string>();

        [JsonProperty("segments")]
        public List<string> Segments { get; set; } = new List<string>();

        [JsonProperty("variants")]
        public Dictionary<string, List<string>> Variants { get; set; } = new Dictionary<string, List<string>>();

        [JsonProperty("components")]
        public List<string> Components { get; set; } = new List<string>();

        [JsonProperty("rendererMaterials")]
        public List<string> RendererMaterials { get; set; } = new List<string>();

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

        [JsonProperty("riskTags")]
        public List<string> RiskTags { get; set; } = new List<string>();

        [JsonProperty("technicalLowRiskPlacementCandidate")]
        public bool TechnicalLowRiskPlacementCandidate { get; set; }

        [JsonProperty("recommendedFirstPassCandidate")]
        public bool RecommendedFirstPassCandidate { get; set; }

        [JsonProperty("sampleSources")]
        public Dictionary<string, int> SampleSources { get; set; } = new Dictionary<string, int>();

        [JsonProperty("sourceCount")]
        public int SourceCount { get; set; }

        [JsonProperty("sourceExamples")]
        public List<DaObjectRegistrySourceExample> SourceExamples { get; set; } = new List<DaObjectRegistrySourceExample>();
    }

    internal sealed class DaObjectRegistryMaterial
    {
        [JsonProperty("registryId")]
        public string RegistryId { get; set; }

        [JsonProperty("stableKey")]
        public string StableKey { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("objectType")]
        public string ObjectType { get; set; }

        [JsonProperty("shader")]
        public string Shader { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("mainTexture")]
        public string MainTexture { get; set; }

        [JsonProperty("roles")]
        public List<string> Roles { get; set; } = new List<string>();

        [JsonProperty("segments")]
        public List<string> Segments { get; set; } = new List<string>();

        [JsonProperty("variants")]
        public Dictionary<string, List<string>> Variants { get; set; } = new Dictionary<string, List<string>>();

        [JsonProperty("sampleSources")]
        public Dictionary<string, int> SampleSources { get; set; } = new Dictionary<string, int>();

        [JsonProperty("sourceCount")]
        public int SourceCount { get; set; }

        [JsonProperty("sourceExamples")]
        public List<DaObjectRegistrySourceExample> SourceExamples { get; set; } = new List<DaObjectRegistrySourceExample>();
    }

    internal sealed class DaObjectRegistrySourceExample
    {
        [JsonProperty("itemId")]
        public string ItemId { get; set; }

        [JsonProperty("materialId")]
        public string MaterialId { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("diagnosticDirectory")]
        public string DiagnosticDirectory { get; set; }

        [JsonProperty("segment")]
        public string Segment { get; set; }

        [JsonProperty("normalizedVariantName")]
        public string NormalizedVariantName { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("grouperPath")]
        public string GrouperPath { get; set; }

        [JsonProperty("stepPath")]
        public string StepPath { get; set; }

        [JsonProperty("field")]
        public string Field { get; set; }

        [JsonProperty("defaults")]
        public DaCatalogDefaults Defaults { get; set; }
    }
}
