# -*- coding: utf-8 -*-
from pathlib import Path
r=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy/WhySoLaggy.Tests'
p=r/'PatchProfilerIntegrationTests.cs';s=p.read_text(encoding='utf-8')
a=s.index('            var data=(IDictionary)',s.index('private static int Calls'))
b=s.index('\n        }',a)
s=s[:a]+'''            return PatchProfiler.TakeSnapshot(false).Where(x=>x.Key==MethodKey.Canonical(method)).Sum(x=>x.Calls);'''+s[b:]
s=s.replace('Assert.AreEqual("original failure",ex.InnerException.Message);\n                Assert.AreEqual(0,Calls(method));\n                method.Invoke(null,new object[]{false});\n                Assert.AreEqual(1,Calls(method));','Assert.AreEqual("original failure",ex.InnerException.Message);\n                Assert.AreEqual(1,Calls(method));\n                method.Invoke(null,new object[]{false});\n                Assert.AreEqual(2,Calls(method));\n                Assert.AreEqual(1L,PatchProfiler.TakeSnapshot(false).Sum(x=>x.Exceptions));')
p.write_text(s,encoding='utf-8')
p=r/'CoreBehaviorTests.cs';s=p.read_text(encoding='utf-8');a=s.index('            PatchProfiler.Shutdown();',s.index('public void PatchProfiler_Estimates'));b=s.index('\n        }',a)
s=s[:a]+'''            TimingLedger.Remove("test");
            var entry=TimingLedger.Register("test",typeof(OverloadedTarget).GetMethod(nameof(OverloadedTarget.Work),new[]{typeof(int),typeof(string)}),"test");
            entry.Adaptive=true;entry.Threshold=double.MaxValue;
            for(int i=0;i<100;i++){var call=TimingLedger.Enter(entry);TimingLedger.Exit(ref call,null);}
            var sample=TimingLedger.Snapshot("test",true).Single();
            Assert.AreEqual(100L,sample.Calls);
            Assert.IsTrue(sample.Samples>=32 && sample.Samples<100);
            Assert.AreEqual(0,TimingLedger.Snapshot("test",true).Count);
            TimingLedger.Remove("test");'''+s[b:]
p.write_text(s,encoding='utf-8')
print('Updated regression tests for the new timing contract')
