using System;
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
        internal static bool IsDiagnostic(string owner)=>PatchProfiler.IsOwnOwner(owner);
        internal static bool HasCycle(Dictionary<string,HashSet<string>> graph)
        {
            var states=new Dictionary<string,int>();Func<string,bool> visit=null;
            visit=node=>{if(states.TryGetValue(node,out int state))return state==1;states[node]=1;if(graph.TryGetValue(node,out var edges))foreach(string next in edges)if(visit(next))return true;states[node]=2;return false;};
            return graph.Keys.Any(visit);
        }
        private static string Csv(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";
        public static void Scan(string dir)
        {
            var snapshot=HarmonyPatchSnapshot.Refresh();
            var sb=new StringBuilder("TargetId,TargetMethod,PatchType,OwnerHarmonyId,DiagnosticOnly,PatchMethod,Assembly,Priority,Before,After,ReflectedType,DeclaredType\n");
            var ids=new HashSet<string>();int shared=0,cycles=0,failures=0;
            foreach(var method in snapshot.Methods)try
            {
                if(sb.Length>512*1024){failures++;break;}
                var info=snapshot.GetPatchInfo(method);if(info==null)continue;string id=ProfilerTargetResolver.Identity(method);var external=new HashSet<string>();
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
            DiagnosticIO.Enqueue(()=>{File.WriteAllText(Path.Combine(dir,"harmony_patches.csv"),csv,new UTF8Encoding(false));File.WriteAllText(Path.Combine(dir,"harmony_patches_summary.txt"),summary+"\nADDED\n"+string.Join("\n",added)+"\nREMOVED\n"+string.Join("\n",removed),new UTF8Encoding(false));},csv.Length*2+added.Sum(x=>x.Length*2)+removed.Sum(x=>x.Length*2)+256,true);
            StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=DiagnosticContext.Frame,Type=EventType.HarmonyPatchMap,
                Fields=new Dictionary<string,object>{{"SharedTargets",shared},{"OrderingCycles",cycles},{"Unavailable",failures},{"Added",added.Length},{"Removed",removed.Length}}});
        }
    }
}
