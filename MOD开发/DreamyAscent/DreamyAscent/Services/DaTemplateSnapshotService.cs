using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using Newtonsoft.Json;

namespace DreamyAscent.Services
{
    internal static class DaTemplateSnapshotService
    {
        private const string SnapshotFileName = "template-snapshots.json";
        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        };

        private static bool s_loaded;
        private static DaTemplateSnapshotBundle s_bundle;

        public static string SnapshotFilePath { get; private set; }

        public static void Initialize(string pluginDirectory)
        {
            SnapshotFilePath = DaPathResolver.ResolveDataFile(pluginDirectory, SnapshotFileName);
            DaLog.Info("Template snapshot data path: " + SnapshotFilePath);
        }

        public static DaTemplateSnapshotBundle GetBundle()
        {
            EnsureLoaded();
            return s_bundle;
        }

        public static DaTemplateSnapshotMatch MatchSegment(DaSegmentData segment)
        {
            DaTemplateSnapshotMatch result = new DaTemplateSnapshotMatch
            {
                SegmentName = segment != null ? segment.SegmentName : null,
                NormalizedVariantName = segment != null ? segment.NormalizedVariantName : null,
                Status = "segment-missing"
            };

            if (segment == null)
            {
                return result;
            }

            result.RuntimeGrouperCount = segment.Groupers != null ? segment.Groupers.Count : 0;
            result.RuntimeStepCount = CountRuntimeSteps(segment);

            EnsureLoaded();
            if (s_bundle == null || s_bundle.Snapshots == null || s_bundle.Snapshots.Count == 0)
            {
                result.Status = "snapshot-file-missing";
                return result;
            }

            List<DaTemplateSnapshot> candidates = GetCandidateSnapshots(segment);
            result.CandidateCount = candidates.Count;
            if (candidates.Count == 0)
            {
                result.Status = "no-segment-variant-snapshot";
                return result;
            }

            DaTemplateSnapshot bestSnapshot = null;
            MatchStats bestStats = default(MatchStats);
            float bestScore = -1f;
            for (int index = 0; index < candidates.Count; index++)
            {
                DaTemplateSnapshot candidate = candidates[index];
                MatchStats stats = ScoreSnapshot(segment, candidate);
                if (stats.Score > bestScore)
                {
                    bestScore = stats.Score;
                    bestSnapshot = candidate;
                    bestStats = stats;
                }
            }

            if (bestSnapshot == null)
            {
                result.Status = "no-match";
                return result;
            }

            result.HasSnapshot = true;
            result.Status = bestScore >= 0.5f ? "matched" : "weak-match";
            result.SnapshotId = bestSnapshot.SnapshotId;
            result.Source = bestSnapshot.Source;
            result.DiagnosticDirectory = bestSnapshot.DiagnosticDirectory;
            result.SnapshotSegmentName = bestSnapshot.SegmentName;
            result.SnapshotVariantName = bestSnapshot.NormalizedVariantName;
            result.SnapshotGrouperCount = bestSnapshot.GrouperCount;
            result.SnapshotStepCount = bestSnapshot.StepCount;
            result.MatchedGrouperCount = bestStats.MatchedGroupers;
            result.MatchedStepCount = bestStats.MatchedSteps;
            result.Score = bestStats.Score;
            return result;
        }

        public static List<DaTemplateSnapshotMatch> MatchTerrain(DaTerrainData data)
        {
            List<DaTemplateSnapshotMatch> result = new List<DaTemplateSnapshotMatch>();
            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                return result;
            }

            for (int index = 0; index < data.Map.Segments.Count; index++)
            {
                result.Add(MatchSegment(data.Map.Segments[index]));
            }

            return result;
        }

        public static DaTemplateSnapshot FindSnapshot(string snapshotId)
        {
            EnsureLoaded();
            if (s_bundle == null || s_bundle.Snapshots == null || string.IsNullOrWhiteSpace(snapshotId))
            {
                return null;
            }

            for (int index = 0; index < s_bundle.Snapshots.Count; index++)
            {
                DaTemplateSnapshot snapshot = s_bundle.Snapshots[index];
                if (snapshot != null && string.Equals(snapshot.SnapshotId, snapshotId, StringComparison.Ordinal))
                {
                    return snapshot;
                }
            }

            return null;
        }

        public static void Invalidate()
        {
            s_loaded = false;
            s_bundle = null;
        }

        private static void EnsureLoaded()
        {
            if (s_loaded)
            {
                return;
            }

            s_loaded = true;
            if (string.IsNullOrWhiteSpace(SnapshotFilePath) || !File.Exists(SnapshotFilePath))
            {
                DaLog.OnceWarn("template-snapshots-missing", "Template snapshot file not found: " + (SnapshotFilePath ?? string.Empty));
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(SnapshotFilePath))
                using (JsonTextReader jsonReader = new JsonTextReader(reader))
                {
                    s_bundle = JsonSerializer.Create(s_jsonSettings).Deserialize<DaTemplateSnapshotBundle>(jsonReader);
                }

                int count = s_bundle != null && s_bundle.Snapshots != null ? s_bundle.Snapshots.Count : 0;
                DaLog.Info("Template snapshots loaded. snapshots=" + count);
            }
            catch (Exception ex)
            {
                s_bundle = null;
                DaLog.Warn("Failed to load template snapshots: " + ex.Message);
            }
        }

        private static List<DaTemplateSnapshot> GetCandidateSnapshots(DaSegmentData segment)
        {
            List<DaTemplateSnapshot> candidates = new List<DaTemplateSnapshot>();
            if (segment == null || s_bundle == null || s_bundle.Snapshots == null)
            {
                return candidates;
            }

            string segmentName = segment.SegmentName ?? string.Empty;
            string variantName = segment.NormalizedVariantName ?? string.Empty;
            bool hasVariant = !string.IsNullOrWhiteSpace(variantName);

            for (int index = 0; index < s_bundle.Snapshots.Count; index++)
            {
                DaTemplateSnapshot snapshot = s_bundle.Snapshots[index];
                if (snapshot == null || !string.Equals(snapshot.SegmentName, segmentName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!hasVariant || string.IsNullOrWhiteSpace(snapshot.NormalizedVariantName))
                {
                    candidates.Add(snapshot);
                    continue;
                }

                if (string.Equals(snapshot.NormalizedVariantName, variantName, StringComparison.Ordinal))
                {
                    candidates.Add(snapshot);
                }
            }

            if (candidates.Count > 0 || !hasVariant)
            {
                return candidates;
            }

            for (int index = 0; index < s_bundle.Snapshots.Count; index++)
            {
                DaTemplateSnapshot snapshot = s_bundle.Snapshots[index];
                if (snapshot != null && string.Equals(snapshot.SegmentName, segmentName, StringComparison.Ordinal))
                {
                    candidates.Add(snapshot);
                }
            }

            return candidates;
        }

        private static MatchStats ScoreSnapshot(DaSegmentData segment, DaTemplateSnapshot snapshot)
        {
            MatchStats stats = new MatchStats();
            if (segment == null || snapshot == null)
            {
                return stats;
            }

            HashSet<string> runtimeGrouperPaths = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> runtimeGrouperNames = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> runtimeStepPaths = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> runtimeStepKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int grouperIndex = 0; segment.Groupers != null && grouperIndex < segment.Groupers.Count; grouperIndex++)
            {
                DaPropGrouperData grouper = segment.Groupers[grouperIndex];
                if (grouper == null)
                {
                    continue;
                }

                AddNormalized(runtimeGrouperPaths, grouper.HierarchyPath);
                AddNormalized(runtimeGrouperNames, grouper.GrouperName);

                for (int stepIndex = 0; grouper.Steps != null && stepIndex < grouper.Steps.Count; stepIndex++)
                {
                    DaLevelGenStepData step = grouper.Steps[stepIndex];
                    if (step == null)
                    {
                        continue;
                    }

                    AddNormalized(runtimeStepPaths, step.HierarchyPath);
                    AddNormalized(runtimeStepKeys, BuildStepKey(grouper.GrouperName, step.StepName, step.StepType));
                }
            }

            int snapshotGroupers = 0;
            int snapshotSteps = 0;
            for (int grouperIndex = 0; snapshot.Groupers != null && grouperIndex < snapshot.Groupers.Count; grouperIndex++)
            {
                DaTemplateSnapshotGrouper grouper = snapshot.Groupers[grouperIndex];
                if (grouper == null)
                {
                    continue;
                }

                snapshotGroupers++;
                string grouperPath = NormalizePath(grouper.Path);
                string grouperName = NormalizeText(grouper.Name);
                if (!string.IsNullOrWhiteSpace(grouperPath) && runtimeGrouperPaths.Contains(grouperPath) ||
                    !string.IsNullOrWhiteSpace(grouperName) && runtimeGrouperNames.Contains(grouperName))
                {
                    stats.MatchedGroupers++;
                }

                for (int stepIndex = 0; grouper.Steps != null && stepIndex < grouper.Steps.Count; stepIndex++)
                {
                    DaTemplateSnapshotStep step = grouper.Steps[stepIndex];
                    if (step == null)
                    {
                        continue;
                    }

                    snapshotSteps++;
                    string stepPath = NormalizePath(step.Path);
                    string stepKey = NormalizeText(BuildStepKey(grouper.Name, step.Name, step.Type));
                    if (!string.IsNullOrWhiteSpace(stepPath) && runtimeStepPaths.Contains(stepPath) ||
                        !string.IsNullOrWhiteSpace(stepKey) && runtimeStepKeys.Contains(stepKey))
                    {
                        stats.MatchedSteps++;
                    }
                }
            }

            float grouperScore = Ratio(stats.MatchedGroupers, Math.Max(snapshotGroupers, runtimeGrouperPaths.Count));
            float stepScore = Ratio(stats.MatchedSteps, Math.Max(snapshotSteps, runtimeStepPaths.Count));
            float variantBonus = string.Equals(snapshot.NormalizedVariantName ?? string.Empty, segment.NormalizedVariantName ?? string.Empty, StringComparison.Ordinal)
                ? 0.1f
                : 0f;
            stats.Score = Math.Min(1f, grouperScore * 0.35f + stepScore * 0.55f + variantBonus);
            return stats;
        }

        private static int CountRuntimeSteps(DaSegmentData segment)
        {
            int count = 0;
            for (int grouperIndex = 0; segment != null && segment.Groupers != null && grouperIndex < segment.Groupers.Count; grouperIndex++)
            {
                DaPropGrouperData grouper = segment.Groupers[grouperIndex];
                count += grouper != null && grouper.Steps != null ? grouper.Steps.Count : 0;
            }

            return count;
        }

        private static void AddNormalized(HashSet<string> target, string value)
        {
            string normalized = NormalizePath(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                target.Add(normalized);
            }
        }

        private static string BuildStepKey(string grouperName, string stepName, string stepType)
        {
            return (grouperName ?? string.Empty) + "/" + (stepName ?? string.Empty) + "/" + (stepType ?? string.Empty);
        }

        private static float Ratio(int numerator, int denominator)
        {
            return denominator <= 0 ? 0f : (float)numerator / denominator;
        }

        private static string NormalizePath(string value)
        {
            return NormalizeText(value).Replace('\\', '/').Trim('/');
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private struct MatchStats
        {
            public int MatchedGroupers;

            public int MatchedSteps;

            public float Score;
        }
    }
}
