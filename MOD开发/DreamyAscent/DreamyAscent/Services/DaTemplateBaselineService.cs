using System;
using System.Collections.Generic;
using System.Globalization;
using DreamyAscent.Data;
using DreamyAscent.Helpers;

namespace DreamyAscent.Services
{
    internal static class DaTemplateBaselineService
    {
        private static readonly Dictionary<string, DaTemplateBaselineData> s_cachedBySegmentKey = new Dictionary<string, DaTemplateBaselineData>(StringComparer.Ordinal);
        private static string s_cachedMapKey = string.Empty;

        public static DaTemplateBaselineReport BuildReport(DaTerrainData data)
        {
            DaTemplateBaselineReport report = new DaTemplateBaselineReport
            {
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                MapKey = data != null && data.Map != null ? data.Map.MapKey : null
            };

            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                return report;
            }

            EnsureCache(data);
            report.SegmentCount = data.Map.Segments.Count;
            for (int index = 0; index < data.Map.Segments.Count; index++)
            {
                DaSegmentData segment = data.Map.Segments[index];
                DaTemplateBaselineData baseline = BuildBaseline(segment);
                report.Baselines.Add(baseline);
                if (baseline.IsReady)
                {
                    report.ReadyCount++;
                }

                if (baseline.WarningCount() > 0)
                {
                    report.WarningCount += baseline.WarningCount();
                }
            }

            return report;
        }

        public static DaTemplateBaselineData GetBaseline(DaSegmentData segment)
        {
            if (segment == null)
            {
                return null;
            }

            EnsureCache(DaTerrainExportService.LastExportedTerrain);
            string key = BuildKey(segment);
            if (s_cachedBySegmentKey.TryGetValue(key, out DaTemplateBaselineData baseline))
            {
                return baseline;
            }

            baseline = BuildBaseline(segment);
            s_cachedBySegmentKey[key] = baseline;
            return baseline;
        }

        public static bool IsBaselineReady(DaSegmentData segment)
        {
            DaTemplateBaselineData baseline = GetBaseline(segment);
            return baseline != null &&
                   baseline.IsReady &&
                   baseline.WarningCount() == 0;
        }

        public static bool HasCurrentVariantDefaultTemplate(DaSegmentData segment)
        {
            return IsCurrentVariantDefaultTemplateReady(GetBaseline(segment));
        }

        public static DaTemplateBaselineData GetCurrentVariantDefaultTemplate(DaSegmentData segment)
        {
            DaTemplateBaselineData baseline = GetBaseline(segment);
            return IsCurrentVariantDefaultTemplateReady(baseline) ? baseline : null;
        }

        public static bool ContainsRuntimeGrouper(DaSegmentData segment, DaPropGrouperData grouper)
        {
            DaTemplateBaselineData baseline = GetBaseline(segment);
            if (!IsCurrentVariantDefaultTemplateReady(baseline) || baseline.Groupers == null || grouper == null)
            {
                return false;
            }

            string runtimePath = Normalize(grouper.HierarchyPath);
            string runtimeName = Normalize(grouper.GrouperName);
            for (int index = 0; index < baseline.Groupers.Count; index++)
            {
                DaTemplateBaselineGrouperData baselineGrouper = baseline.Groupers[index];
                if (baselineGrouper == null ||
                    !string.Equals(baselineGrouper.Status, "matched", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(runtimePath) &&
                    string.Equals(Normalize(baselineGrouper.RuntimePath), runtimePath, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(runtimeName) &&
                    string.Equals(Normalize(baselineGrouper.Name), runtimeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static void Invalidate()
        {
            s_cachedMapKey = string.Empty;
            s_cachedBySegmentKey.Clear();
        }

        private static void EnsureCache(DaTerrainData data)
        {
            string mapKey = data != null && data.Map != null ? data.Map.MapKey ?? string.Empty : string.Empty;
            if (string.Equals(s_cachedMapKey, mapKey, StringComparison.Ordinal))
            {
                return;
            }

            s_cachedMapKey = mapKey;
            s_cachedBySegmentKey.Clear();
        }

        private static DaTemplateBaselineData BuildBaseline(DaSegmentData segment)
        {
            DaTemplateBaselineData data = new DaTemplateBaselineData
            {
                SegmentName = segment != null ? segment.SegmentName : null,
                NormalizedVariantName = segment != null ? segment.NormalizedVariantName : null
            };

            DaTemplateSnapshotMatch match = DaTemplateSnapshotService.MatchSegment(segment);
            if (match == null || !match.HasSnapshot)
            {
                data.Status = match != null ? match.Status : "no-match";
                return data;
            }

            data.Status = match.Status;
            data.IsReady = match.Score >= 0.5f;
            data.SnapshotId = match.SnapshotId;
            data.SnapshotSource = match.Source;
            data.DiagnosticDirectory = match.DiagnosticDirectory;
            data.MatchScore = match.Score;
            data.RuntimeGrouperCount = match.RuntimeGrouperCount;
            data.RuntimeStepCount = match.RuntimeStepCount;
            data.SnapshotGrouperCount = match.SnapshotGrouperCount;
            data.SnapshotStepCount = match.SnapshotStepCount;

            DaTemplateSnapshot snapshot = DaTemplateSnapshotService.FindSnapshot(match.SnapshotId);
            if (snapshot == null)
            {
                return data;
            }

            data.Snapshot = snapshot;
            Dictionary<string, DaPropGrouperData> runtimeGroupers = BuildRuntimeGrouperIndex(segment);
            HashSet<string> matchedRuntimeGroupers = new HashSet<string>(StringComparer.Ordinal);

            for (int grouperIndex = 0; grouperIndex < snapshot.Groupers.Count; grouperIndex++)
            {
                DaTemplateSnapshotGrouper snapshotGrouper = snapshot.Groupers[grouperIndex];
                if (snapshotGrouper == null)
                {
                    continue;
                }

                DaTemplateBaselineGrouperData grouperData = new DaTemplateBaselineGrouperData
                {
                    Name = snapshotGrouper.Name,
                    SnapshotPath = snapshotGrouper.Path
                };

                DaPropGrouperData runtimeGrouper = FindRuntimeGrouper(runtimeGroupers, snapshotGrouper);
                HashSet<string> matchedStepsInGrouper = new HashSet<string>(StringComparer.Ordinal);
                if (runtimeGrouper != null)
                {
                    matchedRuntimeGroupers.Add(runtimeGrouper.HierarchyPath ?? string.Empty);
                    grouperData.RuntimePath = runtimeGrouper.HierarchyPath;
                    grouperData.RuntimeStepCount = runtimeGrouper.Steps != null ? runtimeGrouper.Steps.Count : 0;
                    grouperData.Status = "matched";
                }
                else
                {
                    grouperData.Status = "missing";
                }

                for (int stepIndex = 0; snapshotGrouper.Steps != null && stepIndex < snapshotGrouper.Steps.Count; stepIndex++)
                {
                    DaTemplateSnapshotStep snapshotStep = snapshotGrouper.Steps[stepIndex];
                    if (snapshotStep == null)
                    {
                        continue;
                    }

                    DaTemplateBaselineStepData stepData = new DaTemplateBaselineStepData
                    {
                        Name = snapshotStep.Name,
                        Type = snapshotStep.Type,
                        SnapshotPath = snapshotStep.Path
                    };

                    DaLevelGenStepData runtimeStep = FindRuntimeStep(runtimeGrouper, snapshotStep);
                    if (runtimeStep != null)
                    {
                        matchedStepsInGrouper.Add(BuildRuntimeStepIdentity(runtimeStep));
                        stepData.RuntimePath = runtimeStep.HierarchyPath;
                        stepData.Status = "matched";
                        grouperData.MatchedStepCount++;
                        data.MatchedStepCount++;
                    }
                    else
                    {
                        stepData.Status = "missing";
                        grouperData.MissingStepCount++;
                        data.MissingStepCount++;
                    }

                    grouperData.Steps.Add(stepData);
                }

                grouperData.SnapshotStepCount = snapshotGrouper.StepCount;
                grouperData.MatchedStepCount = grouperData.Steps.Count - grouperData.MissingStepCount;
                PopulateExtraRuntimeSteps(runtimeGrouper, matchedStepsInGrouper, grouperData);
                data.ExtraRuntimeStepCount += grouperData.ExtraRuntimeStepCount;
                data.MatchedGrouperCount += runtimeGrouper != null ? 1 : 0;
                data.MissingGrouperCount += runtimeGrouper == null ? 1 : 0;
                data.Groupers.Add(grouperData);
            }

            for (int grouperIndex = 0; segment.Groupers != null && grouperIndex < segment.Groupers.Count; grouperIndex++)
            {
                DaPropGrouperData runtimeGrouper = segment.Groupers[grouperIndex];
                if (runtimeGrouper == null)
                {
                    continue;
                }

                string runtimePath = runtimeGrouper.HierarchyPath ?? string.Empty;
                if (matchedRuntimeGroupers.Contains(runtimePath))
                {
                    continue;
                }

                data.ExtraRuntimeGrouperCount++;
                data.ExtraRuntimeGroupers.Add(new DaTemplateBaselineRuntimeItem
                {
                    Name = runtimeGrouper.GrouperName,
                    Type = "grouper",
                    RuntimePath = runtimePath
                });
                if (runtimeGrouper.Steps != null)
                {
                    data.ExtraRuntimeStepCount += runtimeGrouper.Steps.Count;
                }
            }

            return data;
        }

        private static void PopulateExtraRuntimeSteps(
            DaPropGrouperData runtimeGrouper,
            HashSet<string> matchedStepsInGrouper,
            DaTemplateBaselineGrouperData grouperData)
        {
            if (runtimeGrouper == null || runtimeGrouper.Steps == null || grouperData == null)
            {
                return;
            }

            for (int index = 0; index < runtimeGrouper.Steps.Count; index++)
            {
                DaLevelGenStepData step = runtimeGrouper.Steps[index];
                if (step == null || matchedStepsInGrouper.Contains(BuildRuntimeStepIdentity(step)))
                {
                    continue;
                }

                grouperData.ExtraRuntimeStepCount++;
                grouperData.ExtraRuntimeSteps.Add(new DaTemplateBaselineRuntimeItem
                {
                    Name = step.StepName,
                    Type = step.StepType,
                    RuntimePath = step.HierarchyPath
                });
            }
        }

        private static Dictionary<string, DaPropGrouperData> BuildRuntimeGrouperIndex(DaSegmentData segment)
        {
            Dictionary<string, DaPropGrouperData> result = new Dictionary<string, DaPropGrouperData>(StringComparer.Ordinal);
            for (int index = 0; segment != null && segment.Groupers != null && index < segment.Groupers.Count; index++)
            {
                DaPropGrouperData grouper = segment.Groupers[index];
                if (grouper == null)
                {
                    continue;
                }

                string key = Normalize(grouper.HierarchyPath);
                if (!string.IsNullOrWhiteSpace(key) && !result.ContainsKey(key))
                {
                    result[key] = grouper;
                }

                string nameKey = Normalize(grouper.GrouperName);
                if (!string.IsNullOrWhiteSpace(nameKey) && !result.ContainsKey(nameKey))
                {
                    result[nameKey] = grouper;
                }
            }

            return result;
        }

        private static DaPropGrouperData FindRuntimeGrouper(Dictionary<string, DaPropGrouperData> index, DaTemplateSnapshotGrouper snapshotGrouper)
        {
            if (index == null || snapshotGrouper == null)
            {
                return null;
            }

            string pathKey = Normalize(snapshotGrouper.Path);
            if (!string.IsNullOrWhiteSpace(pathKey) && index.TryGetValue(pathKey, out DaPropGrouperData byPath))
            {
                return byPath;
            }

            string nameKey = Normalize(snapshotGrouper.Name);
            if (!string.IsNullOrWhiteSpace(nameKey) && index.TryGetValue(nameKey, out DaPropGrouperData byName))
            {
                return byName;
            }

            return null;
        }

        private static DaLevelGenStepData FindRuntimeStep(DaPropGrouperData runtimeGrouper, DaTemplateSnapshotStep snapshotStep)
        {
            if (runtimeGrouper == null || snapshotStep == null || runtimeGrouper.Steps == null)
            {
                return null;
            }

            string pathKey = Normalize(snapshotStep.Path);
            string key = BuildStepKey(snapshotStep.Name, snapshotStep.Type);
            for (int index = 0; index < runtimeGrouper.Steps.Count; index++)
            {
                DaLevelGenStepData step = runtimeGrouper.Steps[index];
                if (step == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(pathKey) && string.Equals(Normalize(step.HierarchyPath), pathKey, StringComparison.Ordinal))
                {
                    return step;
                }

                if (string.Equals(BuildStepKey(step.StepName, step.StepType), key, StringComparison.Ordinal))
                {
                    return step;
                }
            }

            return null;
        }

        private static string BuildKey(DaSegmentData segment)
        {
            return (segment != null ? segment.SegmentName : string.Empty) + "|" + (segment != null ? segment.NormalizedVariantName : string.Empty);
        }

        private static string BuildStepKey(string name, string type)
        {
            return Normalize(name) + "|" + Normalize(type);
        }

        private static string BuildRuntimeStepIdentity(DaLevelGenStepData step)
        {
            if (step == null)
            {
                return string.Empty;
            }

            string path = Normalize(step.HierarchyPath);
            return !string.IsNullOrWhiteSpace(path) ? path : BuildStepKey(step.StepName, step.StepType);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace('\\', '/');
        }

        private static int WarningCount(this DaTemplateBaselineData data)
        {
            return data == null ? 0 : data.MissingGrouperCount + data.MissingStepCount + data.ExtraRuntimeGrouperCount + data.ExtraRuntimeStepCount;
        }

        private static bool IsCurrentVariantDefaultTemplateReady(DaTemplateBaselineData baseline)
        {
            return baseline != null &&
                   baseline.Snapshot != null &&
                   baseline.IsReady &&
                   baseline.MissingGrouperCount == 0 &&
                   baseline.MissingStepCount == 0;
        }
    }
}
