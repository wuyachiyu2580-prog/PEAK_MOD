using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WhySoLaggy
{
    public partial class WhySoLaggyPlugin
    {
        internal static WhySoLaggyPlugin Instance;
        internal static ConfigEntry<bool> AutoCapture,AdaptiveTiming,CpuGpuMetrics;
        internal static ConfigEntry<bool> DebugPhaseTiming;
        internal static ConfigEntry<int> DebugPhaseTimingSeconds;
        internal static ConfigEntry<StructuredDiskFormat> DiskFormatConfig;
        internal static ConfigEntry<float> RpcPumpBudget,DashboardFont;
        internal static ConfigEntry<string> DashboardPosition;
        internal static ConfigEntry<KeyboardShortcut> DashboardKey,CaptureKey,MarkerKey;
        private int runtimeDirty;
        private bool runtimeStarted;
        private string hooksSignature="";
        private long inventoryRevision;private double nextInventory;
        private Harmony rpcHarmony,abuseHarmony,traceHarmony,probeHarmony;
        internal static string RuntimeStatus="";
        private void InitializeRuntime()
        {
            Instance=this;
            DebugPhaseTiming=Config.Bind("Diagnostics","PhaseTiming",false,"Run one bounded phase timing window; automatically turns off. Nested phases are not additive.");
            DebugPhaseTimingSeconds=Config.Bind("Diagnostics","PhaseTimingSeconds",45,new ConfigDescription("Duration of a diagnostic timing window, 30 to 60 seconds.",new AcceptableValueRange<int>(30,60)));
            AutoCapture=Config.Bind("Capture","AutoCapture",true,"Automatically capture repeated or severe frame spikes; plugin timing first, then verified targets.");
            AdaptiveTiming=Config.Bind("General","AdaptiveTiming",false,"Sample at method entry in manual patch profiling. Detailed automatic captures are exact.");
            CpuGpuMetrics=Config.Bind("General","CpuGpuMetrics",false,"Read available CPU/GPU frame timings without changing graphics settings.");
            RpcPumpBudget=Config.Bind("RpcMonitor","PumpBudgetMs",.5f,new ConfigDescription("Soft main-thread RPC detail budget per frame, in milliseconds.",new AcceptableValueRange<float>(.1f,5)));
            DashboardFont=Config.Bind("UI","FontSize",14f,new ConfigDescription("Dashboard text size.",new AcceptableValueRange<float>(11,24)));
            DashboardPosition=Config.Bind("UI","Position","16,16","Saved dashboard position; clamped to screen.");
            DashboardKey=Config.Bind("UI","DashboardKey",new KeyboardShortcut(KeyCode.F8,KeyCode.LeftControl),"Toggle dashboard. Disabled while text input or key rebinding is active.");
            CaptureKey=Config.Bind("Capture","CaptureKey",new KeyboardShortcut(KeyCode.F9,KeyCode.LeftControl),"Start or stop a manual 30-second capture.");
            MarkerKey=Config.Bind("Capture","MarkerKey",new KeyboardShortcut(KeyCode.F10,KeyCode.LeftControl),"Add a timestamped marker to the rolling capture.");
            FrameCoordinator.Initialize();ExceptionObserver.Start();
            CaptureController.Initialize(_bepInExDir,new Dictionary<string,object>{{"SpikeThresholdMs",SpikeThresholdMs.Value},{"AutoCapture",AutoCapture.Value},{"PumpBudgetMs",RpcPumpBudget.Value}});
            FrameCoordinator.Completed+=CaptureController.OnFrame;
            CaptureController.ApplyHooks=(capture,owners,errors)=>{hooksSignature="";ApplyRuntimeHooks();};
            Config.SettingChanged+=OnRuntimeSettingChanged;
            rpcHarmony=new Harmony(PluginGuid+".RpcMonitor");abuseHarmony=new Harmony(PluginGuid+".AbuseDetection");
            traceHarmony=new Harmony(PluginGuid+".MethodTracer");probeHarmony=new Harmony(PluginGuid+".FieldProbe");
            Interlocked.Exchange(ref runtimeDirty,1);
        }
        private void OnRuntimeSettingChanged(object sender,SettingChangedEventArgs args)=>Interlocked.Exchange(ref runtimeDirty,1);
        private void ApplyRuntimeSettings()
        {
            FpsTracker.SpikeThresholdMs=SpikeThresholdMs.Value;FpsTracker.ReportIntervalSeconds=ReportIntervalSeconds.Value;FpsTracker.EnableMemoryMonitor=EnableMemoryMonitor.Value;
            LogFilter.Level=VerbosityCfg.Value;PerformanceDashboard.ShowDashboard=ShowDashboard.Value;
            StructuredLogger.MaxLogFileSizeMB=MaxLogFileSizeMB.Value;StructuredLogger.MaxRotatedFiles=MaxRotatedFiles.Value;StructuredLogger.MaxRotatedStorageMB=MaxRotatedStorageMB.Value;
            StructuredLogger.ConfigureDiskFormat(DiskFormatConfig.Value);
            CaptureController.UpdateConfiguration(new Dictionary<string,object>{{"SpikeThresholdMs",SpikeThresholdMs.Value},{"AutoCapture",AutoCapture.Value},{"PumpBudgetMs",RpcPumpBudget.Value},{"ManualPluginProfiling",EnablePluginProfiling.Value},{"ManualPatchProfiling",EnablePatchProfiling.Value},{"AdaptiveTiming",AdaptiveTiming.Value},{"QueueCapacity",QueueCapacity.Value},{"StructuredDiskFormat",DiskFormatConfig.Value.ToString()},{"PhaseTiming",DebugPhaseTiming.Value},{"PhaseTimingSeconds",DebugPhaseTimingSeconds.Value}});
            CaptureController.AutoEnabled=AutoCapture.Value;NetworkObserver.CpuGpuEnabled=CpuGpuMetrics.Value;
            PatchProfiler.TopMethodCount=TopMethodCount.Value;PatchProfiler.MinReportMs=MinReportMs.Value;PatchProfiler.Adaptive=AdaptiveTiming.Value;
            PatchProfiler.IgnoreMethods.Clear();FillIgnoreSet(IgnorePatchMethods.Value,PatchProfiler.IgnoreMethods);
            PluginProfiler.IgnoreGuids.Clear();FillIgnoreSet(IgnorePluginGuids.Value,PluginProfiler.IgnoreGuids);
            RpcMonitor.PumpBudgetMs=RpcPumpBudget.Value;RpcMonitor.PumpBatchSize=PumpBatchSize.Value;RpcMonitor.QueueCapacity=QueueCapacity.Value;RpcMonitor.ApplyCapacity();
            RpcMonitor.TopMethodCount=RpcMonitorTopCount.Value;RpcMonitor.WatchedShowPerMethod=RpcMonitorWatchShowPerMethod.Value;
            NetworkAbuseDetector.InstantiateRateThreshold=InstantiateRateThreshold.Value;NetworkAbuseDetector.DestroyRateThreshold=DestroyRateThreshold.Value;
            NetworkAbuseDetector.RpcRateThreshold=RpcRateThreshold.Value;NetworkAbuseDetector.ObjectSpikeThreshold=ObjectSpikeThreshold.Value;
            NetworkAbuseDetector.CheckIntervalSeconds=AbuseCheckInterval.Value;NetworkAbuseDetector.ReportIntervalSeconds=AbuseReportInterval.Value;
            NetworkAbuseDetector.ActorMethodRateThreshold=ActorMethodRateThreshold.Value;NetworkAbuseDetector.OwnershipGrabRateThreshold=OwnershipGrabRateThreshold.Value;
            NetworkAbuseDetector.OwnershipRequestRateThreshold=OwnershipRequestRateThreshold.Value;NetworkAbuseDetector.AlertCooldownSeconds=AlertCooldownSeconds.Value;
            if(runtimeStarted)
            {
                if(EnableRpcMonitor.Value&&!RpcMonitor.Enabled){RpcMonitor.Enabled=true;RpcMonitor.Initialize(rpcHarmony);}
                else if(!EnableRpcMonitor.Value&&RpcMonitor.Enabled){RuleTargets.RemoveOwner(rpcHarmony);RpcMonitor.Shutdown();}
                if(EnableAbuseDetection.Value)NetworkAbuseDetector.Initialize(abuseHarmony);
                else{RuleTargets.RemoveOwner(abuseHarmony);NetworkAbuseDetector.Shutdown();}
                RpcMonitor.ReconfigureWatches(ExtraWatchMethods.Value,RpcMonitorWatchPerMethodCapacity.Value);
            }
            ApplyRuntimeHooks();RuntimeStatus="Applied; trace/probe rule changes require Reload rules";
        }
        private void ApplyRuntimeHooks()
        {
            if(!_patchProfilerReady)return;
            bool plugin=EnablePluginProfiling.Value||CaptureController.WantsPlugin;
            bool patch=EnablePatchProfiling.Value||CaptureController.Phase==CapturePhase.Targeted;
            var owners=EnablePatchProfiling.Value?null:CaptureController.TargetOwners;
            var errors=EnablePatchProfiling.Value?new MethodBase[0]:CaptureController.ExceptionTargets;
            string signature=plugin+"|"+patch+"|"+EnablePatchProfiling.Value+"|"+string.Join(",",owners?.OrderBy(x=>x)??Enumerable.Empty<string>())+"|"+string.Join(",",errors.Select(ProfilerTargetResolver.Identity))+"|"+IgnorePluginGuids.Value+"|"+IgnorePatchMethods.Value;
            if(signature==hooksSignature)return;hooksSignature=signature;
            PluginProfiler.Request(plugin,!EnablePluginProfiling.Value);
            PatchProfiler.Request(patch,owners,!EnablePatchProfiling.Value,errors);
        }
        private void RuntimeTick()
        {
            if(DiagnosticPhases.Profiler.Tick(DebugPhaseTiming.Value,DebugPhaseTimingSeconds.Value,DiagnosticClock.Now,DiagnosticPhases.Report))DebugPhaseTiming.Value=false;
            long started=Stopwatch.GetTimestamp();
            if(Interlocked.Exchange(ref runtimeDirty,0)!=0)try{ApplyRuntimeSettings();}catch(Exception ex){RuntimeStatus="Apply failed: "+ex.GetType().Name;}
            FrameCoordinator.Transition=PluginProfiler.Session.Busy||PatchProfiler.Session.Busy;
            if(FrameCoordinator.Transition)FrameCoordinator.StableSince=DiagnosticClock.Now;
            FrameCoordinator.Check();long hookStart=Stopwatch.GetTimestamp(),before=PluginProfiler.Session.Revision;
            PluginProfiler.Pump();
            PatchProfiler.Pump(Math.Max(0,2-(int)(PluginProfiler.Session.Revision-before)),Math.Max(0,2-DiagnosticClock.Milliseconds(Stopwatch.GetTimestamp()-hookStart)));
            long revision=PluginProfiler.Session.Revision+PatchProfiler.Session.Revision;
            if(runtimeStarted&&!FrameCoordinator.Transition&&revision!=inventoryRevision&&DiagnosticClock.Now>=nextInventory){inventoryRevision=revision;nextInventory=DiagnosticClock.Now+5;RefreshInventory();}
            if(runtimeStarted){ExceptionObserver.Tick();NetworkObserver.Tick();}
            if(!InputBusy())
            {
                if(DashboardKey.Value.IsDown())ShowDashboard.Value=!ShowDashboard.Value;
                if(CaptureKey.Value.IsDown()){if(CaptureController.Busy)CaptureController.Finish("User stopped");else CaptureController.Begin(true,"User capture");}
                if(MarkerKey.Value.IsDown())CaptureController.Mark();
            }
            SelfMetrics.Add("Runtime",DiagnosticClock.Milliseconds(Stopwatch.GetTimestamp()-started),false);
        }
        private static Type eventSystemType;
        private static PropertyInfo eventSystemCurrent,eventSystemSelected;
        internal static bool InputBusy()
        {
            if(PerformanceDashboard.HasTextFocus)return true;
            try
            {
                if(eventSystemType==null){eventSystemType=AccessTools.TypeByName("UnityEngine.EventSystems.EventSystem");eventSystemCurrent=eventSystemType?.GetProperty("current");eventSystemSelected=eventSystemType?.GetProperty("currentSelectedGameObject");}
                object current=eventSystemCurrent?.GetValue(null,null);
                var selected=current==null?null:eventSystemSelected?.GetValue(current,null) as GameObject;
                if(selected!=null)foreach(var component in selected.GetComponents<Component>())
                {if(component==null)continue;string name=component.GetType().Name;if(name.Contains("InputField")||name.Contains("Key")||name.Contains("Rebind"))return true;}
                return ModConfigUiAdapter.IsMenuOpen;
            }
            catch{return true;}
        }
        internal void RefreshInventory()=>HarmonyScanner.Scan(_bepInExDir);
        internal void ReloadRules()
        {
            try
            {
                string path=ResolveFieldProbeRulesPath(FieldProbeRulesFile.Value);
                bool oldEnabled=FieldProbe.Enabled;string oldPath=FieldProbe.RulesFilePath;
                FieldProbe.Enabled=EnableFieldProbe.Value;
                try{MethodTracer.Validate(TraceMethodNames.Value);FieldProbe.Validate(path);}
                catch{FieldProbe.Enabled=oldEnabled;throw;}
                FieldProbe.RulesFilePath=path;
                if(!FieldProbe.TryReload(probeHarmony,out string probeError)){FieldProbe.Enabled=oldEnabled;FieldProbe.RulesFilePath=oldPath;RuntimeStatus="Rule reload failed: "+probeError;return;}
                if(!MethodTracer.TryReload(traceHarmony,TraceMethodNames.Value,out string traceError)){RuntimeStatus="Rule reload failed: "+traceError;return;}
                MethodTracer.TraceMaxDepth=TraceMaxDepth.Value;MethodTracer.TraceRateLimit=TraceRateLimit.Value;
                FieldProbe.DefaultRateLimit=FieldProbeDefaultRate.Value;FieldProbe.DefaultMaxValueLen=FieldProbeDefaultMaxLen.Value;FieldProbe.DefaultIncludeStack=FieldProbeDefaultStack.Value;FieldProbe.DefaultStackMaxDepth=FieldProbeDefaultStackDepth.Value;
                RuntimeStatus="Rules reloaded";FrameCoordinator.Transition=true;RefreshInventory();
            }catch(Exception ex){RuntimeStatus="Rule reload failed: "+ex.GetType().Name;}
        }
        private void ShutdownRuntime()
        {
            DiagnosticPhases.Profiler.Stop(DiagnosticClock.Now,DiagnosticPhases.Report,"shutdown");
            Config.SettingChanged-=OnRuntimeSettingChanged;CaptureController.Shutdown();CaptureController.ApplyHooks=null;
            ExceptionObserver.Stop();FrameCoordinator.Shutdown();
            foreach(var h in new[]{rpcHarmony,abuseHarmony,traceHarmony,probeHarmony})try{RuleTargets.RemoveOwner(h);}catch{}
            Instance=null;
        }
    }
}
