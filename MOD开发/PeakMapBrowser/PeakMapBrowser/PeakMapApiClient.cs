using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        public PeakMapSession Session { get; private set; }

        public PeakMapApiClient(MonoBehaviour runner, ManualLogSource log, string baseUrl, string language)
        {
            _runner = runner;
            _log = log;
            _baseUrl = (baseUrl ?? "https://peakmap.top").TrimEnd('/');
            _language = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
            Session = PeakMapSessionStore.Load(log);
        }

        public bool IsSignedIn
        {
            get { return Session != null && Session.HasUser; }
        }

        public bool ShouldRefreshSession
        {
            get
            {
                if (Session == null || !Session.HasRefreshToken) return false;
                if (string.IsNullOrEmpty(Session.access_token)) return true;
                if (Session.expires_at <= 0) return false;
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return Session.expires_at - now < 300;
            }
        }

        public void SetLanguage(string language)
        {
            _language = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
        }

        public void SignOut()
        {
            PeakMapSessionStore.ClearUser(Session, _log);
        }

        public void SignOut(Action<bool, string> done)
        {
            _runner.StartCoroutine(SignOutRoutine(done));
        }

        public void SignIn(string email, string password, Action<AuthResponse, string> done)
        {
            _runner.StartCoroutine(SignInRoutine(email, password, done));
        }

        public void RefreshSession(Action<bool, string> done)
        {
            _runner.StartCoroutine(RefreshSessionRoutine(done));
        }

        public void FetchMaps(int page, int pageSize, string query, string sort, string modVersion, Action<MapsResponse, string> done)
        {
            _runner.StartCoroutine(FetchMapsRoutine(page, pageSize, query, sort, modVersion, done));
        }

        public void FetchAccountMaps(Action<MapsResponse, string> done)
        {
            _runner.StartCoroutine(FetchAccountMapsRoutine(done));
        }

        public void FetchModVersions(Action<ModVersionsResponse, string> done)
        {
            _runner.StartCoroutine(FetchModVersionsRoutine(done));
        }

        public void ToggleLike(MapEntry map, Action<LikeResponse, string> done)
        {
            _runner.StartCoroutine(ToggleLikeRoutine(map, done));
        }

        public void DownloadMap(MapEntry map, Action<string, string> done)
        {
            DownloadMap(map, false, done);
        }

        public void DownloadMap(MapEntry map, bool allowOverwriteLocalChanges, Action<string, string> done)
        {
            _runner.StartCoroutine(DownloadMapRoutine(map, allowOverwriteLocalChanges, done));
        }

        public MapDownloadInfo GetDownloadInfo(MapEntry map)
        {
            return MapSaveService.GetDownloadInfo(map);
        }

        public void UploadMap(string mapName, string author, string version, string description, string jsonPath, string imagePath, Action<string> done)
        {
            _runner.StartCoroutine(UploadMapRoutine(mapName, author, version, description, jsonPath, imagePath, done));
        }

        public void UpdateMap(string mapId, string mapName, string author, string version, string description, string jsonPath, string imagePath, bool removeImage, Action<string> done)
        {
            _runner.StartCoroutine(UpdateMapRoutine(mapId, mapName, author, version, description, jsonPath, imagePath, removeImage, done));
        }

        public void DeleteMap(string mapId, Action<string> done)
        {
            _runner.StartCoroutine(DeleteMapRoutine(mapId, done));
        }

        public void DownloadTexture(string url, Action<Texture2D> done)
        {
            string cachedPath = MapImageCache.GetExistingPath(url);
            if (!string.IsNullOrEmpty(cachedPath))
            {
                _runner.StartCoroutine(LoadCachedTextureRoutine(url, cachedPath, done));
                return;
            }

            _runner.StartCoroutine(DownloadTextureRoutine(url, done));
        }

        private IEnumerator SignInRoutine(string email, string password, Action<AuthResponse, string> done)
        {
            string json = JsonConvert.SerializeObject(new { email = email == null ? string.Empty : email.Trim(), password = password ?? string.Empty });
            using (UnityWebRequest request = JsonRequest(_baseUrl + "/api/auth/sign-in", "POST", json))
            {
                yield return request.SendWebRequest();
                AuthResponse response = Parse<AuthResponse>(Body(request));
                if (HasError(request) || response == null || !response.success)
                {
                    done(response, ResponseError(response, Text("登录失败", "Sign in failed")));
                    yield break;
                }

                ApplyAuthResponse(response);
                done(response, null);
            }
        }

        private IEnumerator RefreshSessionRoutine(Action<bool, string> done)
        {
            if (Session == null || string.IsNullOrEmpty(Session.refresh_token))
            {
                done(false, Text("没有可刷新的登录状态", "No refreshable session"));
                yield break;
            }

            string json = JsonConvert.SerializeObject(new { refresh_token = Session.refresh_token });
            using (UnityWebRequest request = JsonRequest(_baseUrl + "/api/auth/refresh", "POST", json))
            {
                yield return request.SendWebRequest();
                AuthResponse response = Parse<AuthResponse>(Body(request));
                if (HasError(request) || response == null || !response.success)
                {
                    PeakMapSessionStore.ClearUser(Session, _log);
                    done(false, ResponseError(response, Text("登录状态已过期", "Session expired")));
                    yield break;
                }

                ApplyAuthResponse(response);
                done(true, null);
            }
        }

        private IEnumerator SignOutRoutine(Action<bool, string> done)
        {
            bool serverRevoked = false;
            string error = null;
            if (Session != null && !string.IsNullOrEmpty(Session.access_token))
            {
                using (UnityWebRequest request = JsonRequest(_baseUrl + "/api/auth/sign-out", "POST", "{}"))
                {
                    ApplySessionHeaders(request);
                    yield return request.SendWebRequest();
                    BasicResponse response = Parse<BasicResponse>(Body(request));
                    serverRevoked = !HasError(request) && response != null && response.success;
                    if (!serverRevoked)
                    {
                        error = ResponseError(response, Text("服务端退出登录失败", "Server sign-out failed"));
                    }
                }
            }
            else
            {
                serverRevoked = true;
            }

            PeakMapSessionStore.ClearUser(Session, _log);
            done(serverRevoked, error);
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
                ApplySessionHeaders(request);
                yield return request.SendWebRequest();
                CaptureGuestCookie(request);
                if (HasError(request))
                {
                    done(null, Text("获取地图列表失败: ", "Failed to fetch maps: ") + ErrorText(request));
                    yield break;
                }

                MapsResponse response = Parse<MapsResponse>(Body(request));
                if (response == null || !response.success)
                {
                    done(response, ResponseError(response, Text("获取地图列表失败", "Failed to fetch maps")));
                    yield break;
                }

                done(response, null);
            }
        }

        private IEnumerator FetchAccountMapsRoutine(Action<MapsResponse, string> done)
        {
            if (!IsSignedIn)
            {
                done(null, Text("请先登录", "Please sign in first"));
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequest.Get(_baseUrl + "/api/account/maps"))
            {
                ApplySessionHeaders(request);
                yield return request.SendWebRequest();
                if (HasError(request))
                {
                    MapsResponse failed = Parse<MapsResponse>(Body(request));
                    done(failed, ResponseError(failed, Text("获取我的地图失败", "Failed to fetch my maps")));
                    yield break;
                }

                MapsResponse response = Parse<MapsResponse>(Body(request));
                if (response == null || !response.success)
                {
                    done(response, ResponseError(response, Text("获取我的地图失败", "Failed to fetch my maps")));
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
                    done(null, Text("获取 MOD 版本失败: ", "Failed to fetch MOD versions: ") + ErrorText(request));
                    yield break;
                }

                ModVersionsResponse response = Parse<ModVersionsResponse>(Body(request));
                if (response == null || !response.success)
                {
                    done(response, ResponseError(response, Text("获取 MOD 版本失败", "Failed to fetch MOD versions")));
                    yield break;
                }

                done(response, null);
            }
        }

        private IEnumerator ToggleLikeRoutine(MapEntry map, Action<LikeResponse, string> done)
        {
            if (map == null || string.IsNullOrEmpty(map.id))
            {
                done(null, Text("地图缺少 ID", "Map is missing an ID"));
                yield break;
            }

            using (UnityWebRequest request = JsonRequest(_baseUrl + "/api/maps/" + Escape(map.id) + "/like", "POST", "{}"))
            {
                ApplySessionHeaders(request);
                yield return request.SendWebRequest();
                CaptureGuestCookie(request);
                LikeResponse response = Parse<LikeResponse>(Body(request));
                if (HasError(request) || response == null || !response.success)
                {
                    done(response, ResponseError(response, Text("点赞失败", "Like failed")));
                    yield break;
                }

                map.liked_by_me = response.liked;
                map.likes = response.likes;
                done(response, null);
            }
        }

        private IEnumerator DownloadMapRoutine(MapEntry map, bool allowOverwriteLocalChanges, Action<string, string> done)
        {
            if (map == null || string.IsNullOrEmpty(map.download_url))
            {
                done(null, Text("地图缺少下载地址", "Map is missing a download URL"));
                yield break;
            }

            MapDownloadInfo localInfo = MapSaveService.GetDownloadInfo(map);
            if (localInfo.Status == MapDownloadStatus.UpToDate)
            {
                done(null, Text("地图已经是最新版本", "This map is already up to date"));
                yield break;
            }
            if ((localInfo.Status == MapDownloadStatus.LocalModified || localInfo.Status == MapDownloadStatus.UpdateAvailable)
                && !allowOverwriteLocalChanges)
            {
                done(null, localInfo.Status == MapDownloadStatus.LocalModified
                    ? Text("本地 JSON 已被修改，请确认覆盖", "The local JSON was modified; confirm overwrite")
                    : Text("发现地图新版本，请确认更新", "A newer map version is available; confirm update"));
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequest.Get(map.download_url))
            {
                yield return request.SendWebRequest();
                if (HasError(request))
                {
                    done(null, Text("下载失败: ", "Download failed: ") + ErrorText(request));
                    yield break;
                }

                try
                {
                    string saved = MapSaveService.SaveDownloadedMap(map, request.downloadHandler.data, allowOverwriteLocalChanges);
                    done(saved, null);
                }
                catch (MapSaveException ex)
                {
                    done(null, ex.Status == MapDownloadStatus.LocalModified
                        ? Text("本地 JSON 已被修改，请确认覆盖", "The local JSON was modified; confirm overwrite")
                        : Text("保存失败: ", "Save failed: ") + ex.Message);
                }
                catch (Exception ex)
                {
                    done(null, Text("保存失败: ", "Save failed: ") + ex.Message);
                }
            }
        }

        private IEnumerator UploadMapRoutine(string mapName, string author, string version, string description, string jsonPath, string imagePath, Action<string> done)
        {
            List<IMultipartFormSection> form;
            string error = BuildMapForm(mapName, author, version, description, jsonPath, imagePath, false, out form);
            if (!string.IsNullOrEmpty(error))
            {
                done(error);
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequest.Post(_baseUrl + "/api/upload", form))
            {
                ApplySessionHeaders(request);
                _log.LogInfo("Sending upload request to " + _baseUrl + "/api/upload");
                yield return request.SendWebRequest();
                if (HasError(request))
                {
                    string body = Body(request);
                    _log.LogWarning("Upload request failed. Code=" + request.responseCode + ", error=" + request.error + ", body=" + body);
                    done(Text("上传失败: ", "Upload failed: ") + (!string.IsNullOrEmpty(body) ? ExtractError(body) : ErrorText(request)));
                    yield break;
                }

                _log.LogInfo("Upload request succeeded. Code=" + request.responseCode);
                done(null);
            }
        }

        private IEnumerator UpdateMapRoutine(string mapId, string mapName, string author, string version, string description, string jsonPath, string imagePath, bool removeImage, Action<string> done)
        {
            if (!IsSignedIn)
            {
                done(Text("请先登录", "Please sign in first"));
                yield break;
            }
            if (string.IsNullOrEmpty(mapId))
            {
                done(Text("地图缺少 ID", "Map is missing an ID"));
                yield break;
            }

            List<IMultipartFormSection> form;
            string error = BuildMapForm(mapName, author, version, description, jsonPath, imagePath, true, out form);
            if (!string.IsNullOrEmpty(error))
            {
                done(error);
                yield break;
            }
            form.Add(new MultipartFormDataSection("remove_image", removeImage ? "true" : "false"));

            using (UnityWebRequest request = UnityWebRequest.Post(_baseUrl + "/api/maps/" + Escape(mapId), form))
            {
                request.method = "PUT";
                ApplySessionHeaders(request);
                yield return request.SendWebRequest();
                if (HasError(request))
                {
                    done(Text("保存失败: ", "Save failed: ") + ExtractError(Body(request), ErrorText(request)));
                    yield break;
                }

                done(null);
            }
        }

        private IEnumerator DeleteMapRoutine(string mapId, Action<string> done)
        {
            if (!IsSignedIn)
            {
                done(Text("请先登录", "Please sign in first"));
                yield break;
            }
            if (string.IsNullOrEmpty(mapId))
            {
                done(Text("地图缺少 ID", "Map is missing an ID"));
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequest.Delete(_baseUrl + "/api/maps/" + Escape(mapId)))
            {
                ApplySessionHeaders(request);
                yield return request.SendWebRequest();
                if (HasError(request))
                {
                    done(Text("删除失败: ", "Delete failed: ") + ExtractError(Body(request), ErrorText(request)));
                    yield break;
                }

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

                MapImageCache.Save(url, request.downloadHandler.data);
                done(DownloadHandlerTexture.GetContent(request));
            }
        }

        private IEnumerator LoadCachedTextureRoutine(string url, string path, Action<Texture2D> done)
        {
            string fileUrl;
            try
            {
                fileUrl = new Uri(path).AbsoluteUri;
            }
            catch
            {
                MapImageCache.Remove(url);
                fileUrl = null;
            }

            if (string.IsNullOrEmpty(fileUrl))
            {
                yield return _runner.StartCoroutine(DownloadTextureRoutine(url, done));
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fileUrl))
            {
                yield return request.SendWebRequest();
                if (HasError(request))
                {
                    _log.LogWarning("Cached thumbnail failed, downloading again: " + request.error);
                    MapImageCache.Remove(url);
                    yield return _runner.StartCoroutine(DownloadTextureRoutine(url, done));
                    yield break;
                }

                done(DownloadHandlerTexture.GetContent(request));
            }
        }

        private string BuildMapForm(string mapName, string author, string version, string description, string jsonPath, string imagePath, bool jsonOptional, out List<IMultipartFormSection> form)
        {
            form = new List<IMultipartFormSection>();
            if (string.IsNullOrWhiteSpace(mapName) || string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(version))
            {
                return Text("地图名称、作者和版本不能为空", "Map name, author, and version are required");
            }
            if (!jsonOptional && (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath)))
            {
                return Text("请选择本地 JSON 地图文件", "Please select a local JSON map file");
            }

            form.Add(new MultipartFormDataSection("name", mapName.Trim()));
            form.Add(new MultipartFormDataSection("author", author.Trim()));
            form.Add(new MultipartFormDataSection("mod_version", version.Trim()));
            form.Add(new MultipartFormDataSection("description", description == null ? string.Empty : description.Trim()));

            if (!string.IsNullOrEmpty(jsonPath) && File.Exists(jsonPath))
            {
                form.Add(new MultipartFormFileSection("json_file", File.ReadAllBytes(jsonPath), Path.GetFileName(jsonPath), "application/json"));
            }
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                string ext = Path.GetExtension(imagePath).ToLowerInvariant();
                string contentType = ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : ext == ".webp" ? "image/webp" : ext == ".gif" ? "image/gif" : "image/png";
                form.Add(new MultipartFormFileSection("image_file", File.ReadAllBytes(imagePath), Path.GetFileName(imagePath), contentType));
            }

            return null;
        }

        private UnityWebRequest JsonRequest(string url, string method, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json ?? "{}");
            UnityWebRequest request = new UnityWebRequest(url, method);
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            request.SetRequestHeader("Accept", "application/json");
            return request;
        }

        private void ApplySessionHeaders(UnityWebRequest request)
        {
            if (request == null || Session == null) return;
            if (!string.IsNullOrEmpty(Session.access_token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + Session.access_token);
            }
            if (!string.IsNullOrEmpty(Session.guest_id))
            {
                request.SetRequestHeader("Cookie", "peak_guest_id=" + Session.guest_id);
            }
        }

        private void CaptureGuestCookie(UnityWebRequest request)
        {
            if (request == null || Session == null) return;
            string setCookie = request.GetResponseHeader("Set-Cookie");
            if (string.IsNullOrEmpty(setCookie)) return;

            const string key = "peak_guest_id=";
            int start = setCookie.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return;
            start += key.Length;
            int end = setCookie.IndexOf(';', start);
            string guestId = end >= 0 ? setCookie.Substring(start, end - start) : setCookie.Substring(start);
            if (!string.IsNullOrEmpty(guestId) && !string.Equals(Session.guest_id, guestId, StringComparison.Ordinal))
            {
                Session.guest_id = guestId;
                PeakMapSessionStore.Save(Session, _log);
            }
        }

        private void ApplyAuthResponse(AuthResponse response)
        {
            if (Session == null)
            {
                Session = new PeakMapSession();
            }

            Session.access_token = response.access_token;
            Session.refresh_token = response.refresh_token;
            Session.expires_at = response.expires_at;
            if (response.user != null)
            {
                Session.user_id = response.user.id;
                Session.email = response.user.email;
                Session.nickname = response.user.nickname;
            }
            PeakMapSessionStore.Save(Session, _log);
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

        private static string Body(UnityWebRequest request)
        {
            return request != null && request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        }

        private static string ErrorText(UnityWebRequest request)
        {
            return request != null && !string.IsNullOrEmpty(request.error) ? request.error : "HTTP " + (request != null ? request.responseCode.ToString() : "0");
        }

        private static string ResponseError(MapsResponse response, string fallback)
        {
            return response != null && !string.IsNullOrEmpty(response.error_message) ? response.error_message
                : response != null && !string.IsNullOrEmpty(response.error) ? response.error
                : fallback;
        }

        private static string ResponseError(ModVersionsResponse response, string fallback)
        {
            return response != null && !string.IsNullOrEmpty(response.error_message) ? response.error_message
                : response != null && !string.IsNullOrEmpty(response.error) ? response.error
                : fallback;
        }

        private static string ResponseError(AuthResponse response, string fallback)
        {
            return response != null && !string.IsNullOrEmpty(response.error_message) ? response.error_message
                : response != null && !string.IsNullOrEmpty(response.error) ? response.error
                : fallback;
        }

        private static string ResponseError(BasicResponse response, string fallback)
        {
            return response != null && !string.IsNullOrEmpty(response.error_message) ? response.error_message
                : response != null && !string.IsNullOrEmpty(response.error) ? response.error
                : fallback;
        }

        private static string ResponseError(LikeResponse response, string fallback)
        {
            return response != null && !string.IsNullOrEmpty(response.error_message) ? response.error_message
                : response != null && !string.IsNullOrEmpty(response.error) ? response.error
                : fallback;
        }

        private string ExtractError(string json)
        {
            return ExtractError(json, Text("服务器错误", "Server error"));
        }

        private string ExtractError(string json, string fallback)
        {
            BasicResponse response = Parse<BasicResponse>(json);
            if (response != null)
            {
                if (!string.IsNullOrEmpty(response.error_message)) return response.error_message;
                if (!string.IsNullOrEmpty(response.error)) return response.error;
            }
            return string.IsNullOrEmpty(json) ? fallback : json;
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
