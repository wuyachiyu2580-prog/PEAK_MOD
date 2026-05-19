using System.Collections.Generic;
using Newtonsoft.Json;

namespace DreamyAscent.Data
{
    internal sealed class DaParentChildRegistry
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("generatedAt")]
        public string GeneratedAt { get; set; }

        [JsonProperty("summary")]
        public DaParentChildRegistrySummary Summary { get; set; }

        [JsonProperty("segments")]
        public List<DaParentChildRegistrySegment> Segments { get; set; } = new List<DaParentChildRegistrySegment>();

        [JsonProperty("templates")]
        public List<DaParentChildRegistryTemplate> Templates { get; set; } = new List<DaParentChildRegistryTemplate>();
    }

    internal sealed class DaParentChildRegistrySummary
    {
        [JsonProperty("segmentCount")]
        public int SegmentCount { get; set; }

        [JsonProperty("templateCount")]
        public int TemplateCount { get; set; }

        [JsonProperty("parentChildTemplateCount")]
        public int ParentChildTemplateCount { get; set; }

        [JsonProperty("singleItemSpawnerTemplateCount")]
        public int SingleItemSpawnerTemplateCount { get; set; }

        [JsonProperty("technicalLowRiskPlacementCandidateCount")]
        public int TechnicalLowRiskPlacementCandidateCount { get; set; }

        [JsonProperty("recommendedFirstPassCandidateCount")]
        public int RecommendedFirstPassCandidateCount { get; set; }

        [JsonProperty("sourceCounts")]
        public Dictionary<string, int> SourceCounts { get; set; } = new Dictionary<string, int>();

        [JsonProperty("riskTagCounts")]
        public Dictionary<string, int> RiskTagCounts { get; set; } = new Dictionary<string, int>();
    }

    internal sealed class DaParentChildRegistrySegment
    {
        [JsonProperty("segment")]
        public string Segment { get; set; }

        [JsonProperty("levelSlot")]
        public int LevelSlot { get; set; } = -1;

        [JsonProperty("segmentPath")]
        public string SegmentPath { get; set; }

        [JsonProperty("variantSelectionType")]
        public string VariantSelectionType { get; set; }

        [JsonProperty("normalizedVariantName")]
        public string NormalizedVariantName { get; set; }

        [JsonProperty("activeVariantNames")]
        public List<string> ActiveVariantNames { get; set; } = new List<string>();

        [JsonProperty("activeVariantPaths")]
        public List<string> ActiveVariantPaths { get; set; } = new List<string>();

        [JsonProperty("rootPaths")]
        public List<string> RootPaths { get; set; } = new List<string>();

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("templateIds")]
        public List<string> TemplateIds { get; set; } = new List<string>();
    }

    internal sealed class DaParentChildRegistryTemplate
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
        public List<DaParentChildRegistrySourceExample> SourceExamples { get; set; } = new List<DaParentChildRegistrySourceExample>();

        [JsonProperty("children")]
        public List<DaParentChildRegistryChild> Children { get; set; } = new List<DaParentChildRegistryChild>();

        [JsonProperty("interestingComponentFields")]
        public List<DaParentChildRegistryComponentField> InterestingComponentFields { get; set; } = new List<DaParentChildRegistryComponentField>();
    }

    internal sealed class DaParentChildRegistrySourceExample
    {
        [JsonProperty("templateId")]
        public string TemplateId { get; set; }

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

        [JsonProperty("sourceKind")]
        public string SourceKind { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("grouperPath")]
        public string GrouperPath { get; set; }

        [JsonProperty("stepPath")]
        public string StepPath { get; set; }

        [JsonProperty("relationshipCount")]
        public int RelationshipCount { get; set; }

        [JsonProperty("childCount")]
        public int ChildCount { get; set; }

        [JsonProperty("defaults")]
        public DaCatalogDefaults Defaults { get; set; }
    }

    internal sealed class DaParentChildRegistryChild
    {
        [JsonProperty("depth")]
        public int Depth { get; set; }

        [JsonProperty("childIndex")]
        public int ChildIndex { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("pathHash")]
        public string PathHash { get; set; }

        [JsonProperty("stableSignature")]
        public string StableSignature { get; set; }

        [JsonProperty("sourceKind")]
        public string SourceKind { get; set; }

        [JsonProperty("relationshipType")]
        public string RelationshipType { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("confidence")]
        public float Confidence { get; set; }

        [JsonProperty("activeSelf")]
        public bool ActiveSelf { get; set; }

        [JsonProperty("activeInHierarchy")]
        public bool ActiveInHierarchy { get; set; }

        [JsonProperty("rendererCount")]
        public int RendererCount { get; set; }

        [JsonProperty("colliderCount")]
        public int ColliderCount { get; set; }

        [JsonProperty("levelGenStepCount")]
        public int LevelGenStepCount { get; set; }

        [JsonProperty("propGrouperCount")]
        public int PropGrouperCount { get; set; }

        [JsonProperty("singleItemSpawnerCount")]
        public int SingleItemSpawnerCount { get; set; }

        [JsonProperty("photonViewCount")]
        public int PhotonViewCount { get; set; }
    }

    internal sealed class DaParentChildRegistryComponentField
    {
        [JsonProperty("componentType")]
        public string ComponentType { get; set; }

        [JsonProperty("componentName")]
        public string ComponentName { get; set; }

        [JsonProperty("objectPath")]
        public string ObjectPath { get; set; }

        [JsonProperty("fields")]
        public List<DaParentChildRegistryField> Fields { get; set; } = new List<DaParentChildRegistryField>();
    }

    internal sealed class DaParentChildRegistryField
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("valueKind")]
        public string ValueKind { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("objectPath")]
        public string ObjectPath { get; set; }

        [JsonProperty("objectInstanceId")]
        public int ObjectInstanceId { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("truncated")]
        public bool Truncated { get; set; }

        [JsonProperty("items")]
        public List<DaParentChildRegistryFieldItem> Items { get; set; } = new List<DaParentChildRegistryFieldItem>();
    }

    internal sealed class DaParentChildRegistryFieldItem
    {
        [JsonProperty("valueKind")]
        public string ValueKind { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("objectPath")]
        public string ObjectPath { get; set; }

        [JsonProperty("objectInstanceId")]
        public int ObjectInstanceId { get; set; }
    }
}
