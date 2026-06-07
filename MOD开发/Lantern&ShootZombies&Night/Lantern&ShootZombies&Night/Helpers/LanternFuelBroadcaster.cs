using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    internal class LanternFuelBroadcaster : MonoBehaviour
    {
        private float _nextSendTime;
        private float _nextLogTime;

        private static readonly RaiseEventOptions Options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.Others
        };

        private static readonly SendOptions SendOptions = new SendOptions
        {
            Reliability = false
        };

        private void Update()
        {
            if (Plugin.EnableFuelBroadcast == null || !Plugin.EnableFuelBroadcast.Value) return;
            if (!PhotonNetwork.InRoom) return;
            if (Time.unscaledTime < _nextSendTime) return;

            float interval = Plugin.FuelBroadcastInterval != null ? Plugin.FuelBroadcastInterval.Value : 2f;
            _nextSendTime = Time.unscaledTime + Mathf.Clamp(interval, 1f, 10f);
            BroadcastOwnedLanterns();
        }

        private void BroadcastOwnedLanterns()
        {
            int sent = 0;
            foreach (var lantern in Object.FindObjectsByType<Lantern>(FindObjectsSortMode.None))
            {
                if (lantern == null || LanternHelper.IsSpecialLantern(lantern)) continue;
                if (lantern.photonView == null || !lantern.photonView.IsMine) continue;

                Item item = lantern.GetComponent<Item>();
                if (item == null || item.data == null || item.data.guid == System.Guid.Empty) continue;
                if (!LanternHelper.IsPrimaryLocalLantern(item)) continue;
                if (!item.data.TryGetDataEntry(DataEntryKey.Fuel, out FloatItemData fuelData) || fuelData == null) continue;

                bool lit = ReflectionCache.GetLit(lantern);
                if (!lit && item.data.TryGetDataEntry(DataEntryKey.FlareActive, out BoolItemData flareData) && flareData != null)
                    lit = flareData.Value;

                object[] payload =
                {
                    item.data.guid.ToString("D"),
                    fuelData.Value,
                    Mathf.Max(1f, lantern.startingFuel),
                    lit,
                    lantern.photonView.ViewID
                };
                PhotonNetwork.RaiseEvent(LanternFuelSync.EventCode, payload, Options, SendOptions);
                sent++;
            }

            if (sent > 0 && Time.unscaledTime >= _nextLogTime)
            {
                _nextLogTime = Time.unscaledTime + 15f;
                Plugin.Log?.LogInfo($"[DEBUG] [FuelSync] broadcast {sent} owned lantern(s)");
            }
        }
    }
}
