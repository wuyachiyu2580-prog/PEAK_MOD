using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PeakWorkshopCNScanner
{
    [DataContract]
    internal sealed class ScanReport
    {
        [DataMember] public string ScannerVersion = "0.1.0";
        [DataMember] public string ExportedUtc;
        [DataMember] public string WorkshopSha256;
        [DataMember] public string ChinesePatchSha256;
        [DataMember] public string ChinesePatchState;
        [DataMember] public string[] InstalledScopes;
        [DataMember] public string[] VisitedScopes;
        [DataMember] public string Coverage;
        [DataMember] public string Failure;
        [DataMember] public bool ClipFilterAvailable;
        [DataMember] public bool LimitReached;
        [DataMember] public bool OversizeTextSeen;
        [DataMember] public long DrawSubmissions, Classifications, StackCaptures;
        [DataMember] public double ObserverMilliseconds, StartupMilliseconds;
        [DataMember] public int CacheEntries;
        [DataMember] public ScanRecord[] Records;
    }

    internal static class ReportWriter
    {
        private static readonly object Gate = new object();

        internal static void Write(string directory, ScanReport report)
        {
            lock (Gate)
            {
                Directory.CreateDirectory(directory);
                byte[] json;
                using (var buffer = new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(ScanReport)).WriteObject(buffer, report);
                    json = buffer.ToArray();
                }
                AtomicWrite(Path.Combine(directory, "report.json"), json);
                AtomicWrite(Path.Combine(directory, "report.html"), Encoding.UTF8.GetBytes(Html(report)));
                AtomicWrite(Path.Combine(Path.GetDirectoryName(directory), "latest-session.txt"),
                    Encoding.UTF8.GetBytes(directory + Environment.NewLine));
            }
        }

        private static void AtomicWrite(string path, byte[] data)
        {
            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, data);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        internal static string Html(ScanReport report)
        {
            var html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>PeakWorkshop 漏译扫描</title><style>body{font:15px system-ui;margin:24px;background:#141820;color:#eceff4}table{border-collapse:collapse;width:100%}td,th{border:1px solid #48505d;padding:8px;text-align:left;vertical-align:top}pre{white-space:pre-wrap;overflow-wrap:anywhere;margin:0}th{background:#263140}small{color:#b9c0cd}.builtin-candidate{border-left:5px solid #ff893d}.resource-candidate{border-left:5px solid #55d8ff}.review{border-left:5px solid #f1d967}</style><h1>PeakWorkshop 漏译扫描</h1>");
            html.Append("<p>这里是疑似漏译，尚未逐项确认。橙色：匹配原 MOD 内置文案；蓝色：疑似资源键；黄色：需人工判断（可能是作者内容）。编号与本次游戏内标注对应。</p><p>扫描只观察已绘制的界面，不修改显示原文、地图或资源键。未打开页面、输入框和原生 TMP/世界文字未覆盖；加载页转为 IMGUI 的文字已覆盖。英文作者内容与内置文案重名仍可能误报。</p>");
            html.Append("<p>导出：").Append(E(report.ExportedUtc)).Append("；条目：").Append(report.Records.Length)
                .Append("；中文补丁：").Append(E(report.ChinesePatchState)).Append("；达到容量上限：").Append(report.LimitReached)
                .Append("；超长文本未记录：").Append(report.OversizeTextSeen).Append("</p><p>").Append(E(report.Coverage)).Append("</p>");
            if (!string.IsNullOrEmpty(report.Failure)) html.Append("<p>扫描已停用：").Append(E(report.Failure)).Append("</p>");
            html.Append("<details><summary>已绘制的入口与运行信息（不是完整页面验收）</summary><pre>")
                .Append(E(string.Join("\n", report.VisitedScopes ?? new string[0])))
                .Append("\n原 DLL SHA256: ").Append(E(report.WorkshopSha256))
                .Append("\n中文 DLL SHA256: ").Append(E(report.ChinesePatchSha256))
                .Append("\n分类次数: ").Append(report.Classifications).Append("；抓栈次数: ").Append(report.StackCaptures)
                .Append("；观察累计耗时(ms): ").Append(report.ObserverMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
                .Append("</pre></details><p>可用浏览器 Ctrl+F 搜索原文或编号。相同入口中的相同文字合并，坐标保留最后一次采样值；次数为采样帧数。</p><table><tr><th>编号 / 分类</th><th>原显示文字</th><th>来源与证据</th><th>最近观测</th></tr>");
            foreach (var record in report.Records)
            {
                html.Append("<tr class=\"").Append(E(record.Kind)).Append("\"><td>#").Append(record.Id).Append("<br>").Append(E(record.Kind))
                    .Append("</td><td><pre>").Append(E(record.Text)).Append("</pre></td><td><pre>").Append(E(record.Scope))
                    .Append("\n").Append(E(record.DrawStack)).Append("\n").Append(E(record.Evidence))
                    .Append("</pre></td><td><pre>").Append(E(record.Scene)).Append("\n").Append(E(record.LastSeenUtc))
                    .Append("\n").Append(record.SampledFrames).Append(" 个采样帧\n").Append(E(record.ChinesePatchState)).Append("</pre></td></tr>");
            }
            return html.Append("</table></html>").ToString();
        }

        private static string E(string text) => WebUtility.HtmlEncode(text ?? "");
    }
}
