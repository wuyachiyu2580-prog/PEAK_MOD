using System;
using System.IO;
namespace WhySoLaggy
{
    internal static class LagLogger
    {
        private static string path;
        public static void Initialize(string dir){path=Path.Combine(dir,"WhySoLaggy.log");DiagnosticIO.Start();Info("[WHY_LAG] Performance monitor session "+DiagnosticContext.SessionId);}
        public static void Info(string message){if(path!=null)TextLogSink.Write(path,"["+StructuredLogger.NowStamp()+"] "+message,true);}
        public static void Write(string message){if(LogFilter.AllowLag())Raw(message,false);}
        public static void WriteRaw(string message){if(LogFilter.AllowLag())Raw(message,false);}
        private static void Raw(string message,bool important){if(path!=null)TextLogSink.Write(path,"["+StructuredLogger.NowStamp()+"] "+message,important);}
        public static void Flush()=>StructuredLogger.Flush();
        public static void Shutdown(){if(path!=null)Info("Session ended");path=null;}

    }
}
