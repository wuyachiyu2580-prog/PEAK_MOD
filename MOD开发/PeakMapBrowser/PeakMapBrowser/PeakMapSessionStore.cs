using System;
using System.IO;
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

        public static PeakMapSession Load(ManualLogSource log)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return new PeakMapSession();
                }

                PeakMapSession session = JsonConvert.DeserializeObject<PeakMapSession>(File.ReadAllText(FilePath));
                return session ?? new PeakMapSession();
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
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(session ?? new PeakMapSession(), Formatting.Indented));
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
    }
}
