using DreamyAscent.Data;
using DreamyAscent.Helpers;

namespace DreamyAscent.Services
{
    internal static class DaObjectCatalogService
    {
        private static string s_cachedMapKey;
        private static DaObjectCatalog s_cachedCatalog;

        public static DaObjectCatalog GetCurrentCatalog(DaTerrainData data)
        {
            if (data == null || data.Map == null)
            {
                return null;
            }

            string mapKey = data.Map.MapKey ?? string.Empty;
            if (s_cachedCatalog != null && string.Equals(s_cachedMapKey, mapKey, System.StringComparison.Ordinal))
            {
                return s_cachedCatalog;
            }

            s_cachedCatalog = DaObjectCatalogDiagnosticService.BuildCatalog(data);
            s_cachedMapKey = mapKey;
            DaLog.Info("Object catalog cache rebuilt. mapKey=" + mapKey + ", items=" + s_cachedCatalog.ItemCount + ", materials=" + s_cachedCatalog.MaterialCount);
            return s_cachedCatalog;
        }

        public static void Invalidate()
        {
            s_cachedMapKey = null;
            s_cachedCatalog = null;
        }
    }
}


