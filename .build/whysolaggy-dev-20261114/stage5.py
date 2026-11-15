from pathlib import Path
root=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy/WhySoLaggy'
(root/'Helpers/HarmonyScanner.cs').write_text(r'''using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
namespace WhySoLaggy
{
    internal static class HarmonyScanner
    {
        internal static string LatestCsv="",Status="";
        private static HashSet<string> previous=new HashSet<string>();
        internal static bool IsDiagnostic(string owner)=>owner==WhySoLaggyPlugin.PluginGuid+".PatchProfiler"||owner==WhySoLaggyPlugin.PluginGuid+".PluginProfiler";
        internal static bool HasCycle(Dictionary<string,HashSet<string>> graph)
        {
            var states=new Dictionary<string,int>();Func<string,bool> visit=null;
            visit=node=>{if(states.TryGetValue(node,out int state))return state==1;states[node]=1;if(graph.TryGetValue(node,out var edges))foreach(string next in edges)if(visit(next))return true;states[node]=2;return false;};
            return graph.Keys.Any(visit);
        }
        private static string Csv(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";
        public static void Scan(string dir)
        {
            var sb=new StringBuilder("TargetId,TargetMethod,PatchType,OwnerHarmonyId,DiagnosticOnly,PatchMethod,Assembly,Priority,Before,After,ReflectedType,DeclaredType\n");
            var ids=new HashSet<string>();int shared=0,cycles=0,failures=0;
            foreach(var method in Harmony.GetAllPatchedMethods().ToArray())try
            {
                var info=Harmony.GetPatchInfo(method);if(info==null)continue;string id=ProfilerTargetResolver.Identity(method);var external=new HashSet<string>();
                foreach(var group in new[]{Tuple.Create("Prefix",info.Prefixes),Tuple.Create("Postfix",info.Postfixes),Tuple.Create("Transpiler",info.Transpilers),Tuple.Create("Finalizer",info.Finalizers),Tuple.Create("ILManipulator",info.ILManipulators)})
                {
                    var graph=new Dictionary<string,HashSet<string>>();var phaseOwners=new HashSet<string>(group.Item2.Select(x=>x.owner??"?"));
                    foreach(var p in group.Item2)
                    {
                        string owner=p.owner??"?";if(!IsDiagnostic(owner))external.Add(owner);
                        if(!graph.ContainsKey(owner))graph[owner]=new HashSet<string>();
                        foreach(string after in p.after??new string[0])if(phaseOwners.Contains(after)){if(!graph.ContainsKey(after))graph[after]=new HashSet<string>();graph[after].Add(owner);}
                        foreach(string before in p.before??new string[0])if(phaseOwners.Contains(before))graph[owner].Add(before);
                        string patch=p.PatchMethod==null?"?":MethodKey.Canonical(p.PatchMethod);
                        string row=string.Join(",",new[]{id,MethodKey.Canonical(method),group.Item1,owner,IsDiagnostic(owner).ToString(),patch,p.PatchMethod?.Module.Assembly.FullName??"?",p.priority.ToString(),string.Join(";",p.before??new string[0]),string.Join(";",p.after??new string[0]),method.ReflectedType?.FullName,method.DeclaringType?.FullName}.Select(Csv));
                        ids.Add(row);sb.AppendLine(row);
                    }
                    if(HasCycle(graph))cycles++;
                }
                if(external.Count>1)shared++;
            }catch{failures++;}
            string csv=sb.ToString();LatestCsv=csv;
            var added=ids.Except(previous).ToArray();var removed=previous.Except(ids).ToArray();previous=ids;
            Status=$"Shared targets={shared}; ordering cycles={cycles}; unavailable={failures}; added={added.Length}; removed={removed.Length}";
            string summary=Status;
            DiagnosticIO.Enqueue(()=>{File.WriteAllText(Path.Combine(dir,"harmony_patches.csv"),csv,new UTF8Encoding(false));File.WriteAllText(Path.Combine(dir,"harmony_patches_summary.txt"),summary+"\nADDED\n"+string.Join("\n",added)+"\nREMOVED\n"+string.Join("\n",removed),new UTF8Encoding(false));},csv.Length*2+256,true);
            StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=DiagnosticContext.Frame,Type=EventType.HarmonyPatchMap,
                Fields=new Dictionary<string,object>{{"SharedTargets",shared},{"OrderingCycles",cycles},{"Unavailable",failures},{"Added",added.Length},{"Removed",removed.Length}}});
        }
    }
}''',encoding='utf-8')
(root/'Helpers/PerformanceDashboard.cs').write_text(r'''using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
namespace WhySoLaggy
{
    internal static class PerformanceDashboard
    {
        public static bool ShowDashboard;
        private static Rect rect=new Rect(16,16,540,400);
        private static bool initialized,collapsed,complete,raw;
        private static int tab;
        private static double next;
        private static string overview="",network="",errors="",alert="";
        private static GUIStyle label,button;
        internal static string T(string en,string cn)=>UiText.Get(en,cn);
        public static void ReportAlert(string value)=>alert=EventCodec.Limit(value,250);
        public static void DrawGUI()
        {
            if(!ShowDashboard)return;
            try
            {
                if(!initialized){initialized=true;var p=(WhySoLaggyPlugin.DashboardPosition?.Value??"16,16").Split(',');if(p.Length==2&&float.TryParse(p[0],NumberStyles.Float,CultureInfo.InvariantCulture,out float x)&&float.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out float y)){rect.x=x;rect.y=y;}}
                int size=(int)(WhySoLaggyPlugin.DashboardFont?.Value??14);
                label=new GUIStyle(GUI.skin.label){fontSize=size,wordWrap=true};button=new GUIStyle(GUI.skin.button){fontSize=size};
                if(DiagnosticClock.Now>=next){next=DiagnosticClock.Now+.25;Refresh();}
                rect.width=Math.Min(600,Math.Max(300,Screen.width-20));rect.height=collapsed?65:Math.Min(620,Math.Max(300,Screen.height-30));
                rect.x=Mathf.Clamp(rect.x,0,Math.Max(0,Screen.width-rect.width));rect.y=Mathf.Clamp(rect.y,0,Math.Max(0,Screen.height-rect.height));
                rect=GUI.Window(0x1A9ED,rect,Draw,"WhySoLaggy "+DiagnosticContext.BuildId);
                if(Event.current.type==EventType.MouseUp&&WhySoLaggyPlugin.DashboardPosition!=null)WhySoLaggyPlugin.DashboardPosition.Value=rect.x.ToString("F0",CultureInfo.InvariantCulture)+","+rect.y.ToString("F0",CultureInfo.InvariantCulture);
            }
            catch(Exception ex){if(DiagnosticClock.Now>next+5){next=DiagnosticClock.Now;WhySoLaggyPlugin.Log?.LogWarning("[WHY_LAG] HUD: "+ex.GetType().Name);}}
        }
        private static string N(double? value)=>value.HasValue?value.Value.ToString("F2",CultureInfo.InvariantCulture):T("Unavailable","不可用");
        private static void Refresh()
        {
            var s=FpsTracker.Statistics;
            overview=$"FPS: {(s.Mean>0?1000/s.Mean:0):F1}   p95: {s.P95:F2}ms   p99: {s.P99:F2}ms\n1% low: {N(s.Low1)}   "+T("Samples","样本")+": "+s.Count+
                "\n"+T("Unity allocated / managed used","Unity 已分配 / 托管已用")+": "+N(MemoryMetrics.UnityBytes/1048576d)+" / "+N(MemoryMetrics.ManagedUsedBytes/1048576d)+" MiB\n"+
                T("Memory net change","内存净变化")+": "+N(MemoryMetrics.NetKBps)+" KiB/s\n"+T("GC allocated this frame","本帧 GC 分配")+": "+N(MemoryMetrics.GcAllocatedBytes)+" B\n"+
                T("Scene state","场景状态")+": "+UiText.State(DiagnosticContext.State)+"\n"+T("Observed chains are inclusive; associated owners are not proof of causation.","调用链耗时包含嵌套调用；关联插件不代表责任归因。");
            network=$"RTT: {NetworkObserver.Ping}ms   "+T("SDK variance","SDK 波动值")+$": {NetworkObserver.Variance}\n"+T("Incoming / outgoing backlog","收包 / 发包积压")+$": {NetworkObserver.Incoming} / {NetworkObserver.Outgoing}\n"+
                T("Reliable retransmissions / second","每秒可靠消息重传")+": "+NetworkObserver.Resent+"\n"+T("Diagnostic queue / delay","诊断队列 / 等待")+$": {RpcMonitor.LastDepth} / {RpcMonitor.LastQueueDelayMs:F1}ms\n"+
                T("Dropped / sampled details","丢弃 / 采样省略明细")+$": {RpcMonitor.TotalDropped} / {RpcIngress.SampledOut}\n"+RpcMonitor.GetRecentWatchedSummary(8)+"\nCPU: "+N(NetworkObserver.CpuMs)+"ms GPU: "+N(NetworkObserver.GpuMs)+"ms";
            errors=string.Join("\n\n",ExceptionObserver.Recent.Take(5).Select(e=>e.Kind+" x"+e.Count+"\n"+e.Stack))+"\n"+T("Dropped error samples","丢弃异常样本")+": "+ExceptionObserver.Dropped;
        }
        private static void Draw(int id)
        {
            GUILayout.BeginVertical();
            if(GUILayout.Button(collapsed?T("Expand","展开"):T("Collapse","折叠"),button))collapsed=!collapsed;
            if(!collapsed)
            {
                tab=GUILayout.Toolbar(tab,new[]{T("Overview","概览"),T("Capture","采集"),T("Network","网络"),T("Errors","异常")},button);
                if(tab==0){GUILayout.Label(overview,label);GUILayout.Label(T("Last alert","最近告警")+": "+alert,label);}
                if(tab==2)GUILayout.Label(network,label);
                if(tab==3)GUILayout.Label(errors,label);
                if(tab==1)
                {
                    GUILayout.Label(UiText.State(CaptureController.Phase.ToString())+$"  {CaptureController.SecondsLeft:F0}s",label);
                    GUILayout.Label(UiText.Runtime(CaptureController.Detail),label);
                    GUILayout.BeginHorizontal();
                    if(GUILayout.Button(CaptureController.Busy?T("Stop recording","停止录制"):T("Record 30 seconds","录制 30 秒"),button)){if(CaptureController.Busy)CaptureController.Finish("User stopped");else CaptureController.Begin(true,"User capture");}
                    if(GUILayout.Button(T("Mark","标记"),button))CaptureController.Mark();
                    if(GUILayout.Button(CaptureController.Paused?T("Resume auto","恢复自动"):T("Pause auto","暂停自动"),button)){CaptureController.Paused=!CaptureController.Paused;if(CaptureController.Paused&&CaptureController.Busy)CaptureController.Finish("Paused by user");}
                    GUILayout.EndHorizontal();
                    complete=GUILayout.Toggle(complete,T("Include player / room identities for this export","本次导出包含玩家和房间身份"));
                    if(complete)raw=GUILayout.Toggle(raw,T("Include original logs (may contain private text)","包含原始日志（可能包含隐私文本）"));else raw=false;
                    if(GUILayout.Button(T("Export recent recording","导出最近记录"),button)){CaptureController.ExportLatest(complete,raw);complete=raw=false;}
                    GUILayout.BeginHorizontal();
                    if(GUILayout.Button(T("Refresh patch list","刷新补丁清单"),button))WhySoLaggyPlugin.Instance?.RefreshInventory();
                    if(GUILayout.Button(T("Reload rules","重新加载规则"),button))WhySoLaggyPlugin.Instance?.ReloadRules();
                    if(GUILayout.Button(T("Open folder","打开目录"),button)){Directory.CreateDirectory(CaptureController.DirectoryPath);Process.Start(new ProcessStartInfo(CaptureController.DirectoryPath){UseShellExecute=true});}
                    GUILayout.EndHorizontal();
                    GUILayout.Label(UiText.Runtime(WhySoLaggyPlugin.RuntimeStatus),label);
                    GUILayout.Label(UiText.Runtime(CaptureController.ExportStatus),label);
                    GUILayout.Label(T("Writer queue bytes / dropped","写入队列字节 / 丢弃")+$": {DiagnosticIO.PendingBytes} / {DiagnosticIO.Dropped}\n"+DiagnosticIO.Error,label);
                }
            }
            GUILayout.EndVertical();GUI.DragWindow(new Rect(0,0,rect.width,22));
        }
        public static void Reset(){initialized=false;alert="";}
    }
}''',encoding='utf-8')
for name in ['LagLogger','AbuseLogger']:
 p=root/'Helpers'/f'{name}.cs';s=p.read_text(encoding='utf-8');s=s.replace('FILE'+name,'WhySoLaggy.log' if name=='LagLogger' else 'WhySoLaggy_Abuse.log');p.write_text(s,encoding='utf-8')
p=root/'Helpers/CaptureController.cs';s=p.read_text(encoding='utf-8');s=s.replace('manual,targeted,desiredPlugin,desiredPatch','manual,targeted');s=s.replace('ApplyHooks?.Invoke(true,null,ExceptionTargets);Phase=CapturePhase.Plugins;','Phase=CapturePhase.Plugins;ApplyHooks?.Invoke(true,null,ExceptionTargets);');p.write_text(s,encoding='utf-8')
print('Scanner, HUD, text filenames integrated')
