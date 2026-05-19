using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    internal static class LanternFuelSync
    {
        public const byte EventCode = 188;
        private const float FreshSeconds = 5f;
        private static readonly Dictionary<Guid, Snapshot> _byGuid = new Dictionary<Guid, Snapshot>();

        internal struct Snapshot
        {
            public float Fuel;
            public float MaxFuel;
            public bool Lit;
            public int ViewId;
            public int SenderActor;
            public float Time;
        }

        public static void Reset()
        {
            _byGuid.Clear();
        }

        public static bool TryGetFresh(Guid guid, out Snapshot snapshot)
        {
            if (LanternHelper.IsLocalPlayerLanternGuid(guid))
            {
                snapshot = default;
                return false;
            }

            if (guid != Guid.Empty && _byGuid.TryGetValue(guid, out snapshot)
                && Time.unscaledTime - snapshot.Time <= FreshSeconds)
            {
                return true;
            }

            snapshot = default;
            return false;
        }

        public static void HandleEvent(EventData photonEvent)
        {
            if (photonEvent == null || photonEvent.Code != EventCode) return;
            if (!(photonEvent.CustomData is object[] payload) || payload.Length < 5) return;

            try
            {
                Guid guid = ParseGuid(payload[0]);
                if (guid == Guid.Empty) return;
                if (LanternHelper.IsLocalPlayerLanternGuid(guid)) return;

                var snapshot = new Snapshot
                {
                    Fuel = Convert.ToSingle(payload[1]),
                    MaxFuel = Mathf.Max(1f, Convert.ToSingle(payload[2])),
                    Lit = Convert.ToBoolean(payload[3]),
                    ViewId = Convert.ToInt32(payload[4]),
                    SenderActor = photonEvent.Sender,
                    Time = Time.unscaledTime
                };

                _byGuid[guid] = snapshot;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[DEBUG] [FuelSync] event parse failed: {ex.Message}");
            }
        }

        private static Guid ParseGuid(object value)
        {
            if (value is Guid guid) return guid;
            if (value is string text && Guid.TryParse(text, out Guid parsed)) return parsed;
            return Guid.Empty;
        }
    }
}
