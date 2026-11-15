using System;
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
        private static double next,lastError;
        internal static bool HasTextFocus;
        private static Vector2 scroll;
        private static int fontSize;
        private static string overview="",network="",errors="",alert="";
        private static GUIStyle label,button;
        internal static string T(string en,string cn)=>UiText.Get(en,cn);
        public static void ReportAlert(string value)=>alert=EventCodec.Limit(value,250);
        public static void DrawGUI()
        {
            if(!ShowDashboard){HasTextFocus=false;return;}
            try
            {
                if(!initialized){initialized=true;var p=(WhySoLaggyPlugin.DashboardPosition?.Value??"16,16").Split(',');if(p.Length==2&&float.TryParse(p[0],NumberStyles.Float,CultureInfo.InvariantCulture,out float x)&&float.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out float y)){rect.x=x;rect.y=y;}}
                int size=(int)(WhySoLaggyPlugin.DashboardFont?.Value??14);
                if(label==null||fontSize!=size){fontSize=size;label=new GUIStyle(GUI.skin.label){fontSize=size,wordWrap=true};button=new GUIStyle(GUI.skin.button){fontSize=size};}
                if(DiagnosticClock.Now>=next){next=DiagnosticClock.Now+.25;Refresh();}
                rect.width=Math.Min(600,Math.Max(300,Screen.width-20));rect.height=collapsed?65:Math.Min(620,Math.Max(300,Screen.height-30));
                rect.x=Mathf.Clamp(rect.x,0,Math.Max(0,Screen.width-rect.width));rect.y=Mathf.Clamp(rect.y,0,Math.Max(0,Screen.height-rect.height));
                HasTextFocus=!string.IsNullOrEmpty(GUI.GetNameOfFocusedControl());
                rect=GUI.Window(0x1A9ED,rect,Draw,"WhySoLaggy "+DiagnosticContext.BuildId);
                if(Event.current.type==UnityEngine.EventType.MouseUp&&WhySoLaggyPlugin.DashboardPosition!=null)WhySoLaggyPlugin.DashboardPosition.Value=rect.x.ToString("F0",CultureInfo.InvariantCulture)+","+rect.y.ToString("F0",CultureInfo.InvariantCulture);
            }
            catch(Exception ex){if(DiagnosticClock.Now>lastError+5){lastError=DiagnosticClock.Now;WhySoLaggyPlugin.Log?.LogWarning("[WHY_LAG] HUD: "+ex.GetType().Name);}}
        }
        private static string N(double? value)=>value.HasValue?value.Value.ToString("F2",CultureInfo.InvariantCulture):T("Unavailable","不可用");
        private static void Refresh()
        {
            var s=FpsTracker.NormalStatistics;
            overview=T("Gameplay / airport (30 seconds)","游玩 / 机场（最近 30 秒）")+"\n"+$"FPS: {(s.Mean>0?1000/s.Mean:0):F1}   p95: {s.P95:F2}ms   p99: {s.P99:F2}ms\n1% low: {N(s.Low1)}   "+T("Samples","样本")+": "+s.Count+
                "\n"+T("Unity allocated / managed used","Unity 已分配 / 托管已用")+": "+N(MemoryMetrics.UnityBytes/1048576d)+" / "+N(MemoryMetrics.ManagedUsedBytes/1048576d)+" MiB\n"+
                T("Memory net change","内存净变化")+": "+N(MemoryMetrics.NetKBps)+" KiB/s\n"+T("GC allocated this frame","本帧 GC 分配")+": "+N(MemoryMetrics.GcAllocatedBytes)+" B\n"+
                T("Self p95 / p99","自身耗时 p95 / p99")+$": {FpsTracker.SelfStatistics.P95:F3} / {FpsTracker.SelfStatistics.P99:F3}ms\n"+T("Scene state","场景状态")+": "+UiText.State(DiagnosticContext.State)+"\n"+T("Observed chains are inclusive; associated owners are not proof of causation.","调用链耗时包含嵌套调用；关联插件不代表责任归因。");
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
                scroll=GUILayout.BeginScrollView(scroll);
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
                    GUILayout.Label(T("Writer queue bytes / dropped","写入队列字节 / 丢弃")+$": {DiagnosticIO.PendingBytes} / {DiagnosticIO.Dropped}\n"+UiText.Runtime(DiagnosticIO.Error),label);
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();GUI.DragWindow(new Rect(0,0,rect.width,22));
        }
        public static void Reset(){initialized=false;alert="";}
    }
}
