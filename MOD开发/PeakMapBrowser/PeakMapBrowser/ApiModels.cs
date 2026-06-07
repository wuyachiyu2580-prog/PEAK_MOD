using System.Collections.Generic;

namespace PeakMapBrowser
{
#pragma warning disable 0649
    internal sealed class MapsResponse
    {
        public bool success;
        public List<MapEntry> data;
        public PaginationInfo pagination;
        public string error_code;
        public string error;
        public string error_message;
    }

    internal sealed class ModVersionsResponse
    {
        public bool success;
        public List<ModVersionEntry> data;
        public string error_code;
        public string error;
        public string error_message;
    }

    internal sealed class MapEntry
    {
        public string id;
        public string name;
        public string author;
        public string mod_version;
        public string description;
        public int downloads;
        public string created_at;
        public string image_url;
        public string thumbnail_url;
        public string json_file_url;
        public string download_url;
    }

    internal sealed class PaginationInfo
    {
        public int page;
        public int page_size;
        public int total;
        public int total_pages;
        public bool has_next;
        public bool has_prev;
    }

    internal sealed class ModVersionEntry
    {
        public string id;
        public string version_name;
        public string created_at;
    }
#pragma warning restore 0649
}
