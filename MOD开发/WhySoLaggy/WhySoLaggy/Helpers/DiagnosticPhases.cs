using System;
namespace WhySoLaggy
{
    internal static class DiagnosticPhases
    {
        internal enum Stage { Update, FrameBoundary, Seal, Statistics, Freeze, RollingAdd, RecordingAdd, PreRollCopy, TimingWindowFlush, RpcPump, Hud, Pack }
        internal static readonly BoundedPhaseProfiler Profiler=new BoundedPhaseProfiler(Enum.GetNames(typeof(Stage)));
        internal static BoundedPhaseProfiler.Scope Measure(Stage stage)=>Profiler.Measure((int)stage);
        internal static void Report(string report)
        {
            WhySoLaggyPlugin.Log?.LogInfo(report);
            if(!string.IsNullOrEmpty(CaptureController.DirectoryPath))
            {
                string directory=System.IO.Path.GetDirectoryName(CaptureController.DirectoryPath);
                // The first timing window may finish before any diagnostic ZIP exists.
                // Keep directory creation on the ordered IO worker, ahead of the write.
                DiagnosticIO.Enqueue(()=>System.IO.Directory.CreateDirectory(directory),256,true);
                TextLogSink.Write(System.IO.Path.Combine(directory,"phase-timing.log"),report,true);
            }
        }
    }
}
