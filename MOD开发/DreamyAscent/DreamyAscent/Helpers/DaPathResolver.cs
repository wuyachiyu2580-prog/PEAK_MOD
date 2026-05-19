using System.IO;

namespace DreamyAscent.Helpers
{
    internal static class DaPathResolver
    {
        private const string DataDirectoryName = "DreamyAscent Data";

        public static string ResolveDataDirectory(string pluginDirectory)
        {
            string primary = Path.Combine(pluginDirectory ?? string.Empty, DataDirectoryName);
            if (Directory.Exists(primary))
            {
                return primary;
            }

            string fallback = Path.Combine(GetParentDirectory(pluginDirectory), DataDirectoryName);
            return Directory.Exists(fallback) ? fallback : primary;
        }

        public static string ResolveDataFile(string pluginDirectory, string fileName)
        {
            string primary = Path.Combine(pluginDirectory ?? string.Empty, DataDirectoryName, fileName);
            if (File.Exists(primary))
            {
                return primary;
            }

            string fallback = Path.Combine(GetParentDirectory(pluginDirectory), DataDirectoryName, fileName);
            return File.Exists(fallback) ? fallback : primary;
        }

        private static string GetParentDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string fullPath = Path.GetFullPath(path);
            string parent = Directory.GetParent(fullPath)?.FullName;
            return parent ?? fullPath;
        }
    }
}
