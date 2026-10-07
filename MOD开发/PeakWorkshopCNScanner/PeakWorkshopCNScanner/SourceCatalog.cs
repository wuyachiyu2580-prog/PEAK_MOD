using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace PeakWorkshopCNScanner
{
    [DataContract]
    internal sealed class SourceCatalog
    {
        [DataMember] public string Sha256 = null;
        [DataMember] public List<ScopeTarget> Scopes = null;
        [DataMember] public Dictionary<string, string> Literals = null;

        internal static SourceCatalog Load()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SourceCatalog.json.gz"))
            using (var zip = new GZipStream(stream, CompressionMode.Decompress))
                return (SourceCatalog)new DataContractJsonSerializer(typeof(SourceCatalog),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true }).ReadObject(zip);
        }
    }

    [DataContract]
    internal sealed class ScopeTarget
    {
        [DataMember] public int Token = 0;
        [DataMember] public string Type = null;
    }
}
