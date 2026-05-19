using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DreamyAscent.Data
{
    internal sealed class DaTemplateSnapshotBundle
    {
        [JsonProperty("generatedAt")]
        public string GeneratedAt { get; set; }

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("summary")]
        public DaTemplateSnapshotSummary Summary { get; set; }

        [JsonProperty("snapshots")]
        public List<DaTemplateSnapshot> Snapshots { get; set; } = new List<DaTemplateSnapshot>();
    }

    internal sealed class DaTemplateSnapshotSummary
    {
        [JsonProperty("snapshotCount")]
        public int SnapshotCount { get; set; }

        [JsonProperty("segmentCount")]
        public int SegmentCount { get; set; }

        [JsonProperty("grouperCount")]
        public int GrouperCount { get; set; }

        [JsonProperty("stepCount")]
        public int StepCount { get; set; }
    }

    internal sealed class DaTemplateSnapshot
    {
        [JsonProperty("snapshotId")]
        public string SnapshotId { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("diagnosticDirectory")]
        public string DiagnosticDirectory { get; set; }

        [JsonProperty("mapKey")]
        public string MapKey { get; set; }

        [JsonProperty("levelSlot")]
        public int LevelSlot { get; set; } = -1;

        [JsonProperty("segmentName")]
        public string SegmentName { get; set; }

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

        [JsonProperty("grouperCount")]
        public int GrouperCount { get; set; }

        [JsonProperty("stepCount")]
        public int StepCount { get; set; }

        [JsonProperty("groupers")]
        public List<DaTemplateSnapshotGrouper> Groupers { get; set; } = new List<DaTemplateSnapshotGrouper>();
    }

    internal sealed class DaTemplateSnapshotGrouper
    {
        [JsonProperty("grouperId")]
        public string GrouperId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("stepCount")]
        public int StepCount { get; set; }

        [JsonProperty("steps")]
        public List<DaTemplateSnapshotStep> Steps { get; set; } = new List<DaTemplateSnapshotStep>();
    }

    internal sealed class DaTemplateSnapshotStep
    {
        [JsonProperty("stepId")]
        public string StepId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("properties")]
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);

        [JsonProperty("modifierTypes")]
        public List<string> ModifierTypes { get; set; } = new List<string>();

        [JsonProperty("constraintTypes")]
        public List<string> ConstraintTypes { get; set; } = new List<string>();

        [JsonProperty("postConstraintTypes")]
        public List<string> PostConstraintTypes { get; set; } = new List<string>();
    }

    internal sealed class DaTemplateSnapshotMatch
    {
        public string SegmentName { get; set; }

        public string NormalizedVariantName { get; set; }

        public bool HasSnapshot { get; set; }

        public string Status { get; set; }

        public string SnapshotId { get; set; }

        public string Source { get; set; }

        public string DiagnosticDirectory { get; set; }

        public string SnapshotSegmentName { get; set; }

        public string SnapshotVariantName { get; set; }

        public int SnapshotGrouperCount { get; set; }

        public int SnapshotStepCount { get; set; }

        public int RuntimeGrouperCount { get; set; }

        public int RuntimeStepCount { get; set; }

        public int MatchedGrouperCount { get; set; }

        public int MatchedStepCount { get; set; }

        public int CandidateCount { get; set; }

        public float Score { get; set; }
    }

    internal sealed class DaTemplateSnapshotMatchReport
    {
        [JsonProperty("generatedAt")]
        public string GeneratedAt { get; set; }

        [JsonProperty("mapKey")]
        public string MapKey { get; set; }

        [JsonProperty("snapshotCount")]
        public int SnapshotCount { get; set; }

        [JsonProperty("segmentCount")]
        public int SegmentCount { get; set; }

        [JsonProperty("matchedCount")]
        public int MatchedCount { get; set; }

        [JsonProperty("weakMatchCount")]
        public int WeakMatchCount { get; set; }

        [JsonProperty("matches")]
        public List<DaTemplateSnapshotMatch> Matches { get; set; } = new List<DaTemplateSnapshotMatch>();
    }

    internal sealed class DaTemplateBaselineReport
    {
        [JsonProperty("generatedAt")]
        public string GeneratedAt { get; set; }

        [JsonProperty("mapKey")]
        public string MapKey { get; set; }

        [JsonProperty("segmentCount")]
        public int SegmentCount { get; set; }

        [JsonProperty("readyCount")]
        public int ReadyCount { get; set; }

        [JsonProperty("warningCount")]
        public int WarningCount { get; set; }

        [JsonProperty("baselines")]
        public List<DaTemplateBaselineData> Baselines { get; set; } = new List<DaTemplateBaselineData>();
    }

    internal sealed class DaTemplateBaselineData
    {
        public string SegmentName { get; set; }

        public string NormalizedVariantName { get; set; }

        public string Status { get; set; }

        public bool IsReady { get; set; }

        public string SnapshotId { get; set; }

        public string SnapshotSource { get; set; }

        public string DiagnosticDirectory { get; set; }

        [JsonIgnore]
        public DaTemplateSnapshot Snapshot { get; set; }

        public float MatchScore { get; set; }

        public int RuntimeGrouperCount { get; set; }

        public int RuntimeStepCount { get; set; }

        public int SnapshotGrouperCount { get; set; }

        public int SnapshotStepCount { get; set; }

        public int MatchedGrouperCount { get; set; }

        public int MatchedStepCount { get; set; }

        public int MissingGrouperCount { get; set; }

        public int MissingStepCount { get; set; }

        public int ExtraRuntimeGrouperCount { get; set; }

        public int ExtraRuntimeStepCount { get; set; }

        public List<DaTemplateBaselineGrouperData> Groupers { get; set; } = new List<DaTemplateBaselineGrouperData>();

        public List<DaTemplateBaselineRuntimeItem> ExtraRuntimeGroupers { get; set; } = new List<DaTemplateBaselineRuntimeItem>();
    }

    internal sealed class DaTemplateBaselineGrouperData
    {
        public string Name { get; set; }

        public string Status { get; set; }

        public string SnapshotPath { get; set; }

        public string RuntimePath { get; set; }

        public int SnapshotStepCount { get; set; }

        public int RuntimeStepCount { get; set; }

        public int MatchedStepCount { get; set; }

        public int MissingStepCount { get; set; }

        public int ExtraRuntimeStepCount { get; set; }

        public List<DaTemplateBaselineStepData> Steps { get; set; } = new List<DaTemplateBaselineStepData>();

        public List<DaTemplateBaselineRuntimeItem> ExtraRuntimeSteps { get; set; } = new List<DaTemplateBaselineRuntimeItem>();
    }

    internal sealed class DaTemplateBaselineStepData
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public string Status { get; set; }

        public string SnapshotPath { get; set; }

        public string RuntimePath { get; set; }
    }

    internal sealed class DaTemplateBaselineRuntimeItem
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public string RuntimePath { get; set; }
    }
}
