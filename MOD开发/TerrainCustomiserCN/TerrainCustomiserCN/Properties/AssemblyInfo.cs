using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Sirenix.Serialization;

[assembly: AssemblyVersion("0.1.2.0")]
[assembly: RegisterFormatter(typeof(GameObjectFormatter), 10)]
[assembly: RegisterFormatter(typeof(MaterialFormatter), 10)]
[assembly: RegisterFormatter(typeof(TransformFormatter), 10)]
[assembly: AssemblyCompany("TerrainCustomiserCN")]
[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyFileVersion("0.1.2.0")]
[assembly: AssemblyInformationalVersion("0.1.2")]
[assembly: AssemblyProduct("TerrainCustomiserCN")]
[assembly: AssemblyTitle("TerrainCustomiserCN")]
