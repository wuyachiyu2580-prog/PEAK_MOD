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

    internal sealed class AuthResponse
    {
        public bool success;
        public string access_token;
        public string refresh_token;
        public long expires_at;
        public int expires_in;
        public AccountUser user;
        public string error;
        public string error_message;
    }

    internal sealed class AccountUser
    {
        public string id;
        public string email;
        public string nickname;
    }

    internal sealed class LikeResponse
    {
        public bool success;
        public bool liked;
        public int likes;
        public string error;
        public string error_message;
    }

    internal sealed class BasicResponse
    {
        public bool success;
        public string message;
        public string error;
        public string error_message;
    }

    internal sealed class PeakMapSession
    {
        public string access_token;
        public string refresh_token;
        public long expires_at;
        public string user_id;
        public string email;
        public string nickname;
        public string guest_id;

        public bool HasUser
        {
            get { return !string.IsNullOrEmpty(access_token) && !string.IsNullOrEmpty(refresh_token); }
        }

        public bool HasRefreshToken
        {
            get { return !string.IsNullOrEmpty(refresh_token); }
        }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(nickname)) return nickname;
                if (!string.IsNullOrWhiteSpace(email)) return email;
                return string.Empty;
            }
        }
    }

    internal sealed class MapEntry
    {
        public string id;
        public string name;
        public string author;
        public string mod_version;
        public string description;
        public int downloads;
        public int likes;
        public string created_at;
        public string updated_at;
        public int revision;
        public bool liked_by_me;
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
