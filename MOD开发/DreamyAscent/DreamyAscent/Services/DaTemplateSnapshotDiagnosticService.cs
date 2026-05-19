using System;
using System.Globalization;
using System.IO;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DreamyAscent.Services
{
    internal static class DaTemplateSnapshotDiagnosticService
    {
        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static void WriteTemplateSnapshotMatchReport(DaTerrainData data, string mapDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mapDirectory))
                {
                    return;
                }

                DaTemplateSnapshotMatchReport report = BuildReport(data);
                string path = Path.Combine(mapDirectory, "TemplateSnapshotMatchReport.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(report, s_jsonSettings));
                DaLog.Info(string.Format(
                    CultureInfo.InvariantCulture,
                    "Template snapshot match report written: {0}, matches={1}, weakMatches={2}",
                    path,
                    report.MatchedCount,
                    report.WeakMatchCount));
            }
            catch (Exception ex)
            {
                DaLog.Warn("Failed to write template snapshot match report: " + ex.Message);
            }
        }

        private static DaTemplateSnapshotMatchReport BuildReport(DaTerrainData data)
        {
            DaTemplateSnapshotMatchReport report = new DaTemplateSnapshotMatchReport
            {
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                MapKey = data != null && data.Map != null ? data.Map.MapKey : null
            };

            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                return report;
            }

            report.SegmentCount = data.Map.Segments.Count;
            DaTemplateSnapshotBundle bundle = DaTemplateSnapshotService.GetBundle();
            report.SnapshotCount = bundle != null && bundle.Snapshots != null ? bundle.Snapshots.Count : 0;

            for (int index = 0; index < data.Map.Segments.Count; index++)
            {
                DaSegmentData segment = data.Map.Segments[index];
                DaTemplateSnapshotMatch match = DaTemplateSnapshotService.MatchSegment(segment);
                report.Matches.Add(match);
                if (match != null && match.HasSnapshot)
                {
                    if (match.Score >= 0.5f)
                    {
                        report.MatchedCount++;
                    }
                    else
                    {
                        report.WeakMatchCount++;
                    }
                }
            }

            return report;
        }
    }
}
