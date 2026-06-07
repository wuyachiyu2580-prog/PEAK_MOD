using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace PeakMapBrowser
{
    internal sealed class PeakMapApiClient
    {
        private readonly MonoBehaviour _runner;
        private readonly ManualLogSource _log;
        private readonly string _baseUrl;
        private string _language;

        public PeakMapApiClient(MonoBehaviour runner, ManualLogSource log, string baseUrl, string language)
        {
            _runner = runner;
            _log = log;
            _baseUrl = (baseUrl ?? "https://peakmap.top").TrimEnd('/');
            _language = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
        }

        public void SetLanguage(string language)
        {
            _language = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
        }

        public void FetchMaps(int page, int pageSize, string query, string sort, string modVersion, Action<MapsResponse, string> done)
        {
            _runner.StartCoroutine(FetchMapsRoutine(page, pageSize, query, sort, modVersion, done));
        }

        public void FetchModVersions(Action<ModVersionsResponse, string> done)
        {
            _runner.StartCoroutine(FetchModVersionsRoutine(done));
        }

        public void DownloadMap(MapEntry map, Action<string, string> done)
        {
            _runner.StartCoroutine(DownloadMapRoutine(map, done));
        }

        public void UploadMap(string mapName, string author, string version, string description, string jsonPath, string imagePath, Action<string> done)
        {
            _runner.StartCoroutine(UploadMapRoutine(mapName, author, version, description, jsonPath, imagePath, done));
        }

        public void DownloadTexture(string url, Action<Texture2D> done)
        {
            _runner.StartCoroutine(DownloadTextureRoutine(url, done));
        }

        private IEnumerator FetchMapsRoutine(int page, int pageSize, string query, string sort, string modVersion, Action<MapsResponse, string> done)
        {
            string url = _baseUrl + "/api/maps?page=" + page + "&page_size=" + pageSize + "&sort=" + Escape(sort) + "&lang=" + _language;
            if (!string.IsNullOrWhiteSpace(query))
            {
                url += "&q=" + Escape(query.Trim());
            }
            if (!string.IsNullOrWhiteSpace(modVersion))
            {
                url += "&mod_version=" + Escape(modVersion.Trim());
            }

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();
                if (HasError(request))
                {
                    done(null, Text("获取地图列表失败: ", "Failed to fetch maps: ") + request.error);
                    yield break;
                }

                MapsResponse response = Parse<MapsResponse>(request.downloadHandler.text);
                if (response == null || !response.success)
                {
                    done(response, ResponseError(response, Text("获取地图列表失败", "Failed to fetch maps")));
                    yield break;
                }

                done(response, null);
            }
        }

        private IEnumerator FetchModVersionsRoutine(Action<ModVersionsResponse, string> done)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(_baseUrl + "/api/mod-versions?lang=" + _language))
            {
                yield return request.SendWebRequest();
                if (HasError(request))
                {
                    done(null, Text("获取 MOD 版本失败: ", "Failed to fetch MOD versions: ") + request.error);
                    yield break;
                }

                ModVersionsResponse response = Parse<ModVersionsResponse>(request.downloadHandler.text);
                if (response == null || !response.success)
                {
                    done(response, ResponseError(response, Text("获取 MOD 版本失败", "Failed to fetch MOD versions")));
                    yield break;
                }

                done(response, null);
            }
        }

        private IEnumerator DownloadMapRoutine(MapEntry map, Action<string, string> done)
        {
            if (map == null || string.IsNullOrEmpty(map.download_url))
            {
                done(null, Text("地图缺少下载地址", "Map is missing a download URL"));
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequest.Get(map.download_url))
            {
                yield return request.SendWebRequest();
                if (HasError(request))
                {
                    done(null, Text("下载失败: ", "Download failed: ") + request.error);
                    yield break;
                }

                try
                {
                    string saved = MapSaveService.SaveDownloadedMap(map, request.downloadHandler.data);
                    done(saved, null);
                }
                catch (Exception ex)
                {
                    done(null, Text("保存失败: ", "Save failed: ") + ex.Message);
                }
            }
        }

        private IEnumerator UploadMapRoutine(string mapName, string author, string version, string description, string jsonPath, string imagePath, Action<string> done)
        {
            if (string.IsNullOrWhiteSpace(mapName) || string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(version))
            {
                done(Text("地图名称、作者和版本不能为空", "Map name, author, and version are required"));
                yield break;
            }
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            {
                done(Text("请选择本地 JSON 地图文件", "Please select a local JSON map file"));
                yield break;
            }

            List<IMultipartFormSection> form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("name", mapName.Trim()),
                new MultipartFormDataSection("author", author.Trim()),
                new MultipartFormDataSection("mod_version", version.Trim()),
                new MultipartFormDataSection("description", description == null ? string.Empty : description.Trim()),
                new MultipartFormFileSection("json_file", File.ReadAllBytes(jsonPath), Path.GetFileName(jsonPath), "application/json")
            };

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                string ext = Path.GetExtension(imagePath).ToLowerInvariant();
                string contentType = ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : ext == ".webp" ? "image/webp" : ext == ".gif" ? "image/gif" : "image/png";
                _log.LogInfo("Upload request includes image file: " + imagePath + " (" + contentType + ")");
                form.Add(new MultipartFormFileSection("image_file", File.ReadAllBytes(imagePath), Path.GetFileName(imagePath), contentType));
            }
            else
            {
                _log.LogInfo("Upload request has no image file. imagePath=" + (string.IsNullOrEmpty(imagePath) ? "<none>" : imagePath));
            }

            using (UnityWebRequest request = UnityWebRequest.Post(_baseUrl + "/api/upload", form))
            {
                _log.LogInfo("Sending upload request to " + _baseUrl + "/api/upload");
                yield return request.SendWebRequest();
                if (HasError(request))
                {
                    string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    _log.LogWarning("Upload request failed. Code=" + request.responseCode + ", error=" + request.error + ", body=" + body);
                    done(Text("上传失败: ", "Upload failed: ") + (!string.IsNullOrEmpty(body) ? body : request.error));
                    yield break;
                }

                _log.LogInfo("Upload request succeeded. Code=" + request.responseCode);
                done(null);
            }
        }

        private IEnumerator DownloadTextureRoutine(string url, Action<Texture2D> done)
        {
            if (string.IsNullOrEmpty(url))
            {
                done(null);
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();
                if (HasError(request))
                {
                    _log.LogWarning("Thumbnail failed: " + request.error);
                    done(null);
                    yield break;
                }

                done(DownloadHandlerTexture.GetContent(request));
            }
        }

        private T Parse<T>(string json) where T : class
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                _log.LogWarning("API JSON parse failed: " + ex.Message);
                return null;
            }
        }

        private static bool HasError(UnityWebRequest request)
        {
            return request.result != UnityWebRequest.Result.Success;
        }

        private static string ResponseError(MapsResponse response, string fallback)
        {
            return response != null && !string.IsNullOrEmpty(response.error_message) ? response.error_message : fallback;
        }

        private static string ResponseError(ModVersionsResponse response, string fallback)
        {
            return response != null && !string.IsNullOrEmpty(response.error_message) ? response.error_message : fallback;
        }

        private static string Escape(string value)
        {
            return UnityWebRequest.EscapeURL(value ?? string.Empty);
        }

        private string Text(string zh, string en)
        {
            return string.Equals(_language, "en", StringComparison.OrdinalIgnoreCase) ? en : zh;
        }
    }
}
