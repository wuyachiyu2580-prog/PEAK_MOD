from pathlib import Path
OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[1];p=OUT/'metrics_probe';p.mkdir(exist_ok=True)
s=(ROOT/'MOD开发/WhySoLaggy/WhySoLaggy/Helpers/HookSession.cs').read_text(encoding='utf-8-sig')
start=s.index('internal static class SelfMetrics');left=s.index('{',start);i=left+1;depth=1
while depth:
 if s[i]=='{':depth+=1
 elif s[i]=='}':depth-=1
 i+=1
original=s[start:i];candidate=original.replace('class SelfMetrics','class CandidateSelfMetrics')
candidate=candidate.replace('private static readonly object gate=new object();','private struct Metric {internal double Total,Max;internal long Count;}'+chr(10)+'        private static readonly object gate=new object();')
candidate=candidate.replace('Dictionary<string,Tuple<double,double,long>>','Dictionary<string,Metric>')
candidate=candidate.replace('values[name]=Tuple.Create((v?.Item1??0)+ms,Math.Max(v?.Item2??0,ms),(v?.Item3??0)+1);','v.Total+=ms;v.Max=Math.Max(v.Max,ms);v.Count++;values[name]=v;')
candidate=candidate.replace('p.Value.Item1','p.Value.Total').replace('p.Value.Item2','p.Value.Max').replace('p.Value.Item3','p.Value.Count')
reference=original.replace('class SelfMetrics','class ReusableSelfMetrics')
reference=reference.replace('private static readonly object gate=new object();','private sealed class Metric {internal double Total,Max;internal long Count;}'+chr(10)+'        private static readonly object gate=new object();')
reference=reference.replace('Dictionary<string,Tuple<double,double,long>>','Dictionary<string,Metric>')
reference=reference.replace('values.TryGetValue(name,out var v);values[name]=Tuple.Create((v?.Item1??0)+ms,Math.Max(v?.Item2??0,ms),(v?.Item3??0)+1);','if(!values.TryGetValue(name,out var v))values[name]=v=new Metric();v.Total+=ms;v.Max=Math.Max(v.Max,ms);v.Count++;')
reference=reference.replace('p.Value.Item1','p.Value.Total').replace('p.Value.Item2','p.Value.Max').replace('p.Value.Item3','p.Value.Count')
support='using System; using System.Collections.Generic; namespace Probe { internal static class DiagnosticContext {internal static int MainId=Environment.CurrentManagedThreadId;internal static bool IsMainThread=>Environment.CurrentManagedThreadId==MainId;}'+original+candidate+reference+'}'
(p/'Metrics.cs').write_text(support,encoding='utf-8')
(p/'MetricsProbe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>',encoding='utf-8')
print('Prepared exact SelfMetrics extraction and isolated mutable value candidate')
