using System;
using System.IO;
namespace WhySoLaggy
{
    internal static class AbuseLogger
    {
        private static string path;
        public static void Initialize(string dir){path=Path.Combine(dir,"WhySoLaggy_Abuse.log");DiagnosticIO.Start();Info("[WHY_LAG] Abuse monitor session "+DiagnosticContext.SessionId);}
        public static void Info(string message){if(path!=null)TextLogSink.Write(path,"["+StructuredLogger.NowStamp()+"] "+message,true);}
        public static void Write(string message){if(LogFilter.AllowAbuse())Raw(message,false);}
        public static void WriteRaw(string message){if(LogFilter.AllowAbuse())Raw(message,false);}
        private static void Raw(string message,bool important){if(path!=null)TextLogSink.Write(path,"["+StructuredLogger.NowStamp()+"] "+message,important);}
        public static void Flush()=>StructuredLogger.Flush();
        public static void Shutdown(){if(path!=null)Info("Session ended");path=null;}
        public static void Alert(string message){Raw("ABUSE ALERT: "+message,true);WhySoLaggyPlugin.Log?.LogWarning("[WHY_LAG] "+message);if(DiagnosticContext.IsMainThread)PerformanceDashboard.ReportAlert(message);}
        public static void AlertDetail(string message)=>Raw(message,true);
        public static void AlertDetailRaw(string message)=>Raw(message,true);
    }
}