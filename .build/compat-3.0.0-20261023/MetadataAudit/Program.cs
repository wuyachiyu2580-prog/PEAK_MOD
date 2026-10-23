using Mono.Cecil;
using System.Text.Json;
using System.Security.Cryptography;
var root=Path.GetFullPath(args[0]);
var output=Path.Combine(root,".build/compat-3.0.0-20261023");
var game="C:/SteamLibrary/steamapps/common/PEAK/PEAK_Data/Managed";
var resolver=new DefaultAssemblyResolver();
resolver.AddSearchDirectory(game);
resolver.AddSearchDirectory(Path.Combine(root,"测试环境/BepInEx/core"));
var before=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(output,"before-hashes.json")));
var files=before.Keys.Where(p=>p.EndsWith(".dll")).ToList();
files.AddRange(Directory.GetFiles(Path.Combine(root,"MOD开发/ItemInfoCN/发行/1.0.0"),"*.dll"));
var lantern=Directory.GetFiles(Path.Combine(root,"MOD开发/Lantern&ShootZombies&Night"),"*.dll",SearchOption.AllDirectories).Where(p=>!p.Contains("obj")&&!p.Contains("bin")).ToArray();
files.AddRange(lantern);
files.AddRange(Directory.GetFiles(Path.Combine(root,"MOD开发/TerrainCustomiserCN/发布/0.1.2"),"*.dll"));
files.AddRange(Directory.GetFiles(Path.Combine(root,"MOD开发/Lantern&ShootZombies&Night/发行/0.2.1"),"*.dll"));
foreach(var p in Directory.GetFiles(Path.Combine(output,"build"),"*.dll",SearchOption.AllDirectories))
  if(Path.GetFileNameWithoutExtension(p)==new DirectoryInfo(Path.GetDirectoryName(p)).Name || Path.GetFileNameWithoutExtension(p)=="Lantern_ShootZombies_Night")files.Add(p);
foreach(var dir in files.Select(Path.GetDirectoryName).Distinct())resolver.AddSearchDirectory(dir);
var parameters=new ReaderParameters{AssemblyResolver=resolver};
var current=AssemblyDefinition.ReadAssembly(Path.Combine(game,"Assembly-CSharp.dll"),parameters);
IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> ts){foreach(var t in ts){yield return t;foreach(var n in Types(t.NestedTypes))yield return n;}}
var all=Types(current.MainModule.Types).ToDictionary(t=>t.FullName);
TypeDefinition Target(string name){ if(all.TryGetValue(name,out var t))return t; return all.Values.FirstOrDefault(x=>x.Name==name); }
IEnumerable<MethodDefinition> Methods(TypeDefinition t,string name){
  for(int depth=0;t!=null&&depth<12;depth++){ foreach(var m in t.Methods.Where(m=>m.Name==name))yield return m; try{t=t.BaseType?.Resolve();}catch{t=null;} }
}
bool HasField(TypeDefinition t,string name){for(int depth=0;t!=null&&depth<12;depth++){if(t.Fields.Any(f=>f.Name==name))return true;try{t=t.BaseType?.Resolve();}catch{break;}}return false;}
var results=new List<object>();
foreach(var file in files.Distinct()){
 try{
  var asm=AssemblyDefinition.ReadAssembly(file,parameters);
  var missing=new List<string>();var unavailable=new List<string>();int checkedRefs=0;
  foreach(var member in asm.MainModule.GetMemberReferences()){
   var scope=member.DeclaringType.Scope as AssemblyNameReference;
   if(scope==null || !File.Exists(Path.Combine(game,scope.Name+".dll")) || scope.Name.StartsWith("System")||scope.Name=="mscorlib"||scope.Name=="netstandard")continue;
   checkedRefs++;
   try{
    if(member is MethodReference mr){var found=mr.Resolve();if(found==null)missing.Add(member.FullName);else if(found.HasThis!=mr.HasThis)missing.Add("static/instance changed: "+member.FullName);}
    if(member is FieldReference fr && fr.Resolve()==null)missing.Add(member.FullName);
   }catch(Exception ex){unavailable.Add(member.FullName+" => "+ex.GetType().Name);}
  }
  var patches=new List<object>();
  foreach(var type in Types(asm.MainModule.Types)){
   string targetType=null;string methodName=null;
   var attrs=type.CustomAttributes.Where(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch").ToList();
   foreach(var attr in attrs)foreach(var arg in attr.ConstructorArguments){if(arg.Value is TypeReference tr)targetType=tr.FullName;else if(arg.Value is string s && methodName==null)methodName=s;}
   var specs=new List<(string type,string method,string patch)>();
   if(targetType!=null&&methodName!=null)specs.Add((targetType,methodName,type.FullName));
   foreach(var method in type.Methods){
    foreach(var attr in method.CustomAttributes.Where(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch")){
     string mt=targetType,mn=null;
     foreach(var arg in attr.ConstructorArguments){if(arg.Value is TypeReference tr)mt=tr.FullName;else if(arg.Value is string s && mn==null)mn=s;}
     if(mt!=null&&mn!=null)specs.Add((mt,mn,method.FullName));
    }
   }
   foreach(var spec in specs){
    var t=Target(spec.type);
    if(t==null){patches.Add(new{target=spec.type+"."+spec.method,status="type absent from Assembly-CSharp (check external dependency)",patch=spec.patch});continue;}
    var targets=Methods(t,spec.method).ToArray();
    var badParams=new List<string>();
    foreach(var m in type.Methods.Where(m=>m.Name=="Prefix"||m.Name=="Postfix"||m.Name=="Finalizer")){
     foreach(var p in m.Parameters){
      if(p.Name.StartsWith("___")){if(!HasField(t,p.Name.Substring(3)))badParams.Add("injected field missing: "+p.Name);}
      else if(!p.Name.StartsWith("__") && !targets.Any(tm=>tm.Parameters.Any(q=>q.Name==p.Name)))badParams.Add("injected parameter missing: "+p.Name);
     }
    }
    patches.Add(new{target=spec.type+"."+spec.method,status=targets.Length==0?"MISSING":badParams.Count>0?"PARAMETER MISMATCH":"found",overloads=targets.Length,issues=badParams,patch=spec.patch});
   }
  }
  var result=new{file=Path.GetRelativePath(root,file),name=asm.Name.Name,version=asm.Name.Version.ToString(),checkedRefs,missing=missing.Distinct().ToArray(),unavailable=unavailable.Distinct().ToArray(),patches};
  results.Add(result);
  Console.WriteLine(JsonSerializer.Serialize(new{result.file,result.name,result.version,checkedRefs,missing=result.missing,unavailable=result.unavailable.Length,patches=patches.Count}));
 }catch(Exception ex){results.Add(new{file,error=ex.ToString()});}
}
File.WriteAllText(Path.Combine(output,"metadata-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
var identity=new{file=Path.Combine(game,"Assembly-CSharp.dll"),bytes=new FileInfo(Path.Combine(game,"Assembly-CSharp.dll")).Length,mvid=current.MainModule.Mvid,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(game,"Assembly-CSharp.dll")))),typeCount=all.Count};
File.WriteAllText(Path.Combine(output,"game-identity.json"),JsonSerializer.Serialize(identity,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(identity));
var api=all.Values.Select(t=>new{name=t.FullName,baseType=t.BaseType?.FullName,methods=t.Methods.Select(m=>new{name=m.Name,returns=m.ReturnType.FullName,isStatic=m.IsStatic,parameters=m.Parameters.Select(p=>new{name=p.Name,type=p.ParameterType.FullName})}),fields=t.Fields.Select(f=>new{name=f.Name,type=f.FieldType.FullName})});
File.WriteAllText(Path.Combine(output,"game-api.json"),JsonSerializer.Serialize(api));
