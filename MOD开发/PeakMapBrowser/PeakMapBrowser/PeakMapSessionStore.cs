using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Logging;
using Newtonsoft.Json;
using UnityEngine;

namespace PeakMapBrowser
{
    internal static class PeakMapSessionStore
    {
        private static string DirectoryPath
        {
            get { return Path.Combine(Application.persistentDataPath, "PeakMapBrowser"); }
        }

        private static string FilePath
        {
            get { return Path.Combine(DirectoryPath, "session.json"); }
        }

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("com.wuyachiyu.peakmapbrowser.session.v2");

        public static PeakMapSession Load(ManualLogSource log)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return new PeakMapSession();
                }

                string json = File.ReadAllText(FilePath);
                PersistedSession persisted = JsonConvert.DeserializeObject<PersistedSession>(json);
                if (persisted != null && persisted.version >= 2)
                {
                    PeakMapSession session = new PeakMapSession
                    {
                        user_id = persisted.user_id,
                        email = persisted.email,
                        nickname = persisted.nickname,
                        guest_id = persisted.guest_id
                    };

                    if (!string.IsNullOrEmpty(persisted.protected_refresh_token))
                    {
                        session.refresh_token = Unprotect(persisted.protected_refresh_token);
                    }

                    return session;
                }

                // Migrate the previous plaintext format immediately after a successful read.
                PeakMapSession legacy = JsonConvert.DeserializeObject<PeakMapSession>(json);
                if (legacy != null)
                {
                    Save(legacy, log);
                    return legacy;
                }

                return new PeakMapSession();
            }
            catch (Exception ex)
            {
                log.LogWarning("Failed to load PeakMapBrowser session: " + ex.Message);
                return new PeakMapSession();
            }
        }

        public static void Save(PeakMapSession session, ManualLogSource log)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                PeakMapSession current = session ?? new PeakMapSession();
                PersistedSession persisted = new PersistedSession
                {
                    version = 2,
                    protected_refresh_token = string.IsNullOrEmpty(current.refresh_token) ? string.Empty : Protect(current.refresh_token),
                    user_id = current.user_id ?? string.Empty,
                    email = current.email ?? string.Empty,
                    nickname = current.nickname ?? string.Empty,
                    guest_id = current.guest_id ?? string.Empty
                };

                string tempPath = FilePath + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.WriteAllText(tempPath, JsonConvert.SerializeObject(persisted, Formatting.Indented), new UTF8Encoding(false));
                    if (File.Exists(FilePath))
                    {
                        File.Replace(tempPath, FilePath, null);
                    }
                    else
                    {
                        File.Move(tempPath, FilePath);
                    }
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogWarning("Failed to save PeakMapBrowser session: " + ex.Message);
            }
        }

        public static void ClearUser(PeakMapSession session, ManualLogSource log)
        {
            if (session == null)
            {
                session = new PeakMapSession();
            }

            session.access_token = string.Empty;
            session.refresh_token = string.Empty;
            session.expires_at = 0;
            session.user_id = string.Empty;
            session.email = string.Empty;
            session.nickname = string.Empty;
            Save(session, log);
        }

        private static string Protect(string value)
        {
            byte[] plain = Encoding.UTF8.GetBytes(value ?? string.Empty);
            byte[] protectedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static string Unprotect(string value)
        {
            byte[] protectedBytes = Convert.FromBase64String(value);
            byte[] plain = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }

        private sealed class PersistedSession
        {
            public int version;
            public string protected_refresh_token;
            public string user_id;
            public string email;
            public string nickname;
            public string guest_id;
        }
    }
}
