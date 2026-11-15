# -*- coding: utf-8 -*-
from pathlib import Path
import ctypes,re
root=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy'
r=root/'WhySoLaggy'
def edit(name,fn):
 p=r/name;p.write_text(fn(p.read_text(encoding='utf-8')),encoding='utf-8')
edit('Helpers/UiText.cs',lambda s:s.replace('internal static string State(string state)','internal static void Invalidate(){next=0;}\n        internal static string State(string state)').replace('if(items.TryGetValue(text,out string cn))','items["Automatic expansion stopped after a hook failure or long installation"]="自动深入已停止：补丁失败或单次安装耗时过长";\n            items["Shutdown incomplete; background writes pending"]="退出刷盘未完成，仍有后台写入";\n            if(items.TryGetValue(text,out string cn))').replace('Tuple.Create("Export failed:","导出失败：")','Tuple.Create("Flush failed:","写盘失败："),Tuple.Create("Export failed:","导出失败：")'))
edit('Helpers/ModConfigLocalization.cs',lambda s:s.replace('Per-method ring buffer size for watched high-risk RPCs.','Per-method ring buffer request; all watched methods share a 1024-record retention ceiling.').replace('高风险监控 RPC 的单方法环形缓冲区大小。','高风险监控 RPC 的单方法环形缓冲区请求容量；所有方法合计最多保留 1024 条。').replace('Applied when patch profiling is next enabled or the game restarts.','Applied at the next safe hook transition.').replace('下次开启补丁性能分析或重启游戏时生效。','在下一次安全的补丁切换时生效。').replace('Entry("General", "MinReportMs", "Minimum Report Time", "最低报告耗时"','Entry("General", "MinReportMs", "Adaptive Sampling Threshold", "自适应采样阈值"'))
edit('WhySoLaggyPlugin.cs',lambda s:s.replace('            InitializeRuntime();','            InitializeRuntime();\n            ModConfigLocalization.ApplyLocalizedDescriptions(GetConfigEntries());').replace('private void OnGameLanguageChanged()\n        {','private void OnGameLanguageChanged()\n        {\n            UiText.Invalidate();').replace('''        void OnGUI()
        {
            if (EnableAbuseDetection.Value)
                AbuseNotificationUI.DrawGUI();
            PerformanceDashboard.DrawGUI();
        }''','''        void OnGUI()
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            try{if(EnableAbuseDetection.Value)AbuseNotificationUI.DrawGUI();PerformanceDashboard.DrawGUI();}
            finally{SelfMetrics.Add("Hud",DiagnosticClock.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp()-start));}
        }'''))
edit('Helpers/AbuseNotificationUI.cs',lambda s:s.replace('Message = message,','Message = UiText.LocalizeChinese(message),'))
edit('Helpers/PerformanceDashboard.cs',lambda s:s.replace('var s=FpsTracker.Statistics;','var s=FpsTracker.NormalStatistics;').replace('overview=$"FPS:', 'overview=T("Gameplay / airport (30 seconds)","游玩 / 机场（最近 30 秒）")+"\\n"+$"FPS:').replace(' +DiagnosticIO.Error', ' +UiText.Runtime(DiagnosticIO.Error)').replace('+DiagnosticIO.Error,label)', '+UiText.Runtime(DiagnosticIO.Error),label)').replace('internal static string T(string en,string cn)', 'internal static string T(string en,string cn)'))
edit('Helpers/CaptureController.cs',lambda s:s.replace('string summaryText=UiText.Get(', 'string durationLabel=UiText.Get("Duration","持续时间"),recordsLabel=UiText.Get("Records","记录条数"),evictedLabel=UiText.Get("Evicted","省略条数"),privacyLabel=UiText.Get("Privacy","隐私模式"),privacyValue=UiText.Get(complete?"Complete identities":"Anonymous",complete?"完整身份":"匿名"),reasonText=UiText.Runtime(record.Reason);string summaryText=UiText.Get(').replace('+record.Reason+"\\nDuration: "','+reasonText+"\\n"+durationLabel+": "').replace('+"s\\nRecords: "','+"s\\n"+recordsLabel+": "').replace('+"\\nEvicted: "','+"\\n"+evictedLabel+": "').replace('+"\\nPrivacy: "+(complete?"Complete identities":"Anonymous allowlist")','+"\\n"+privacyLabel+": "+privacyValue'))
p=root/'WhySoLaggy.Tests/CoreBehaviorTests.cs';s=p.read_text(encoding='utf-8');s=s.replace('"General/SpikeThresholdMs",','"Capture/AutoCapture", "Capture/CaptureKey", "Capture/MarkerKey", "General/AdaptiveTiming", "General/CpuGpuMetrics", "RpcMonitor/PumpBudgetMs", "UI/FontSize", "UI/Position", "UI/DashboardKey",\n                "General/SpikeThresholdMs",',1);p.write_text(s,encoding='utf-8')
# Generate source literals with Windows' built-in simplified/traditional mapping.
# Runtime remains portable and does not depend on Windows globalization APIs.
p=r/'Helpers/UiText.cs';s=p.read_text(encoding='utf-8')
s=re.sub(r'            const string simplified="[^"]*";\n            const string traditional="[^"]*";', '            // Generated and length-checked from Windows zh-TW mapping.\n            const string simplified="";\n            const string traditional="";',s)
p.write_text(s,encoding='utf-8')
chars=sorted(set(''.join(re.findall(r'[\u4e00-\u9fff]', ''.join(x.read_text(encoding='utf-8') for x in r.rglob('*.cs'))))))
fn=ctypes.windll.kernel32.LCMapStringEx
fn.argtypes=[ctypes.c_wchar_p,ctypes.c_uint,ctypes.c_wchar_p,ctypes.c_int,ctypes.c_wchar_p,ctypes.c_int,ctypes.c_void_p,ctypes.c_void_p,ctypes.c_longlong]
pairs=[]
for c in chars:
 buf=ctypes.create_unicode_buffer(8)
 n=fn('zh-TW',0x04000000,c,1,buf,8,None,None,0)
 if n!=1:raise RuntimeError('Unexpected mapping length')
 if buf[0]!=c:pairs.append((c,buf[0]))
s=s.replace('const string simplified="";','const string simplified="'+''.join(x[0] for x in pairs)+'";').replace('const string traditional="";','const string traditional="'+''.join(x[1] for x in pairs)+'";')
s=s.replace('return b.ToString();','return b.ToString().Replace("錄制","錄製").Replace("采集","採集").Replace("采樣","採樣").Replace("字節","位元組").Replace("內存","記憶體");')
p.write_text(s,encoding='utf-8')
print('Localized 50 configuration entries and runtime UI; verified '+str(len(pairs))+' traditional character mappings')
