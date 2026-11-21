using System; using System.Collections.Generic; namespace Probe { internal static class DiagnosticContext {internal static int MainId=Environment.CurrentManagedThreadId;internal static bool IsMainThread=>Environment.CurrentManagedThreadId==MainId;}internal static class SelfMetrics
    {
        private static readonly object gate=new object();
        private static readonly Dictionary<string,Tuple<double,double,long>> values=new Dictionary<string,Tuple<double,double,long>>();
        private static double frameMs;
        internal static void Add(string name,double ms,bool countFrame=true)
        {
            lock(gate){values.TryGetValue(name,out var v);values[name]=Tuple.Create((v?.Item1??0)+ms,Math.Max(v?.Item2??0,ms),(v?.Item3??0)+1);}
            if(countFrame && DiagnosticContext.IsMainThread && (name=="MainUpdate"||name=="FrameBoundary"||name=="Hud"))frameMs+=ms;
        }
        internal static double TakeFrame(){double v=frameMs;frameMs=0;return v;}
        internal static Dictionary<string,object> Snapshot(bool reset=false)
        {
            lock(gate){var result=new Dictionary<string,object>();foreach(var p in values){result[p.Key+"TotalMs"]=p.Value.Item1;result[p.Key+"MaxMs"]=p.Value.Item2;result[p.Key+"Count"]=p.Value.Item3;}if(reset)values.Clear();return result;}
        }
    }internal static class CandidateSelfMetrics
    {
        private struct Metric {internal double Total,Max;internal long Count;}
        private static readonly object gate=new object();
        private static readonly Dictionary<string,Metric> values=new Dictionary<string,Metric>();
        private static double frameMs;
        internal static void Add(string name,double ms,bool countFrame=true)
        {
            lock(gate){values.TryGetValue(name,out var v);v.Total+=ms;v.Max=Math.Max(v.Max,ms);v.Count++;values[name]=v;}
            if(countFrame && DiagnosticContext.IsMainThread && (name=="MainUpdate"||name=="FrameBoundary"||name=="Hud"))frameMs+=ms;
        }
        internal static double TakeFrame(){double v=frameMs;frameMs=0;return v;}
        internal static Dictionary<string,object> Snapshot(bool reset=false)
        {
            lock(gate){var result=new Dictionary<string,object>();foreach(var p in values){result[p.Key+"TotalMs"]=p.Value.Total;result[p.Key+"MaxMs"]=p.Value.Max;result[p.Key+"Count"]=p.Value.Count;}if(reset)values.Clear();return result;}
        }
    }internal static class ReusableSelfMetrics
    {
        private sealed class Metric {internal double Total,Max;internal long Count;}
        private static readonly object gate=new object();
        private static readonly Dictionary<string,Metric> values=new Dictionary<string,Metric>();
        private static double frameMs;
        internal static void Add(string name,double ms,bool countFrame=true)
        {
            lock(gate){if(!values.TryGetValue(name,out var v))values[name]=v=new Metric();v.Total+=ms;v.Max=Math.Max(v.Max,ms);v.Count++;}
            if(countFrame && DiagnosticContext.IsMainThread && (name=="MainUpdate"||name=="FrameBoundary"||name=="Hud"))frameMs+=ms;
        }
        internal static double TakeFrame(){double v=frameMs;frameMs=0;return v;}
        internal static Dictionary<string,object> Snapshot(bool reset=false)
        {
            lock(gate){var result=new Dictionary<string,object>();foreach(var p in values){result[p.Key+"TotalMs"]=p.Value.Total;result[p.Key+"MaxMs"]=p.Value.Max;result[p.Key+"Count"]=p.Value.Count;}if(reset)values.Clear();return result;}
        }
    }}