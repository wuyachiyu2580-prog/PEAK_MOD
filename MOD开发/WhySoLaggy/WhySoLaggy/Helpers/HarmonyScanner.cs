using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HarmonyLib;

namespace WhySoLaggy
{
    /// <summary>
    /// 启动时一次性扫描所有 Harmony 补丁，输出 harmony_patches.csv +
    /// StructuredLogger(EventType.HarmonyPatchMap)。检测同方法多 Owner 冲突并告警。
    /// 1.0.3 新增。运行时无开销。
    /// </summary>
    internal static class HarmonyScanner
    {
        public static void Scan(string bepInExDir)
        {
            try
            {
                string csvPath = Path.Combine(bepInExDir, "harmony_patches.csv");
                using (var sw = new StreamWriter(csvPath, append: false, encoding: Encoding.UTF8))
                {
                    sw.WriteLine("TargetMethod,PatchType,OwnerHarmonyId,Priority");

                    int total = 0;
                    int conflicts = 0;

                    foreach (var method in Harmony.GetAllPatchedMethods())
                    {
                        if (method == null) continue;

                        Patches info = null;
                        try { info = Harmony.GetPatchInfo(method); }
                        catch (Exception ex)
                        {
                            WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] HarmonyScanner GetPatchInfo failed on {method.Name}: {ex.Message}");
                            continue;
                        }
                        if (info == null) continue;

                        string target = (method.DeclaringType?.FullName ?? "?") + "." + method.Name;
                        var owners = new HashSet<string>(StringComparer.Ordinal);

                        WriteGroup(sw, info.Prefixes, "Prefix", target, owners);
                        WriteGroup(sw, info.Postfixes, "Postfix", target, owners);
                        WriteGroup(sw, info.Transpilers, "Transpiler", target, owners);
                        WriteGroup(sw, info.Finalizers, "Finalizer", target, owners);

                        total++;
                        if (owners.Count >= 2)
                        {
                            conflicts++;
                            string msg = $"[HARMONY_SCAN] Potential conflict: {target} patched by {owners.Count} mods: {string.Join(", ", owners)}";
                            AbuseLogger.Write(msg);
                        }
                    }

                    sw.Flush();
                    AbuseLogger.Write($"[HARMONY_SCAN] Scanned {total} patched methods, {conflicts} potential conflicts. CSV -> {csvPath}");
                }
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogError($"[WHY_LAG] HarmonyScanner failed: {ex.Message}");
            }
        }

        private static void WriteGroup(StreamWriter sw, IList<Patch> list, string kind, string target, HashSet<string> owners)
        {
            if (list == null) return;
            foreach (var p in list)
            {
                if (p == null) continue;
                string owner = p.owner ?? "?";
                owners.Add(owner);

                string safeTarget = CsvEscape(target);
                string safeOwner = CsvEscape(owner);
                sw.Write(safeTarget);
                sw.Write(',');
                sw.Write(kind);
                sw.Write(',');
                sw.Write(safeOwner);
                sw.Write(',');
                sw.Write(p.priority.ToString(CultureInfo.InvariantCulture));
                sw.WriteLine();

                try
                {
                    StructuredLogger.WriteEvent(new StructuredEvent
                    {
                        Timestamp = StructuredLogger.NowStamp(),
                        FrameNumber = UnityEngine.Time.frameCount,
                        Type = EventType.HarmonyPatchMap,
                        Fields = new Dictionary<string, object>
                        {
                            { "TargetMethod", target },
                            { "PatchType", kind },
                            { "OwnerHarmonyId", owner },
                            { "Priority", p.priority },
                        },
                    });
                }
                catch { }
            }
        }

        private static string CsvEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            bool needQuote = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ',' || c == '"' || c == '\n' || c == '\r') { needQuote = true; break; }
            }
            if (!needQuote) return s;
            var sb = new StringBuilder(s.Length + 8);
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') sb.Append("\"\"");
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
