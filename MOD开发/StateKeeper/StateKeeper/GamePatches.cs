using HarmonyLib;
using UnityEngine;

namespace StateKeeper
{
    [HarmonyPatch(typeof(GlobalEvents), "TriggerSomeoneWonRun")]
    internal static class GlobalEventsVictoryPatch
    {
        private static void Postfix()
        {
            if (RunCollector.Instance != null) RunCollector.Instance.MarkVictory();
        }
    }

    [HarmonyPatch(typeof(GlobalEvents), "TriggerRunEnded")]
    internal static class GlobalEventsRunEndedPatch
    {
        private static void Prefix()
        {
            if (RunCollector.Instance != null) RunCollector.Instance.EndRun();
        }
    }

    [HarmonyPatch(typeof(GlobalEvents), "TriggerCharacterDied")]
    internal static class CharacterDiedPatch
    {
        private static void Postfix(Character character)
        {
            if (RunCollector.Instance != null)
                RunCollector.Instance.RecordSimpleEvent("PlayerDied", character,
                    character != null && character.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved, "GlobalEvents");
        }
    }

    [HarmonyPatch(typeof(GlobalEvents), "TriggerCharacterPassedOut")]
    internal static class CharacterPassedOutPatch
    {
        private static void Postfix(Character character)
        {
            if (RunCollector.Instance != null)
                RunCollector.Instance.RecordSimpleEvent("PlayerPassedOut", character,
                    character != null && character.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved, "GlobalEvents");
        }
    }

    [HarmonyPatch(typeof(Item), "Interact")]
    internal static class ItemInteractPatch
    {
        private static void Postfix(Item __instance, Character interactor)
        {
            if (RunCollector.Instance != null)
                RunCollector.Instance.RecordItemEvent("ItemPickupRequested", __instance, interactor,
                    null, EventSource.LocalAuthoritative, "Item.Interact");
        }
    }

    [HarmonyPatch(typeof(CharacterItems), "OnPickupAccepted")]
    internal static class PickupAcceptedPatch
    {
        private static void Postfix(CharacterItems __instance, byte slotID)
        {
            if (RunCollector.Instance == null) return;
            Character character = __instance == null ? null : __instance.GetComponent<Character>();
            Item item = null;
            if (character != null && character.player != null)
            {
                ItemSlot slot = character.player.GetItemSlot(slotID);
                if (slot != null) item = slot.prefab;
            }
            RunCollector.Instance.RecordItemEvent("ItemPickedUp", item, character, null,
                EventSource.LocalAuthoritative, "OnPickupAccepted:" + slotID);
        }
    }

    [HarmonyPatch(typeof(Item), "StartUsePrimary")]
    internal static class ItemPrimaryStartedPatch
    {
        private static void Postfix(Item __instance)
        {
            RecordItem("ItemUseStarted", __instance, "StartUsePrimary");
        }

        private static void RecordItem(string type, Item item, string detail)
        {
            if (RunCollector.Instance == null || item == null) return;
            Character holder = item.holderCharacter;
            RunCollector.Instance.RecordItemEvent(type, item, holder, null,
                holder != null && holder.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved, detail);
        }
    }

    [HarmonyPatch(typeof(Item), "FinishCastPrimary")]
    internal static class ItemPrimaryFinishedPatch
    {
        private static void Postfix(Item __instance)
        {
            if (RunCollector.Instance == null || __instance == null) return;
            Character holder = __instance.holderCharacter;
            RunCollector.Instance.RecordItemEvent("ItemPrimaryCastFinished", __instance, holder, null,
                holder != null && holder.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved,
                "FinishCastPrimary");
        }
    }

    [HarmonyPatch(typeof(Item), "FinishCastSecondary")]
    internal static class ItemSecondaryFinishedPatch
    {
        private static void Postfix(Item __instance)
        {
            if (RunCollector.Instance == null || __instance == null) return;
            Character holder = __instance.holderCharacter;
            RunCollector.Instance.RecordItemEvent("ItemSecondaryCastFinished", __instance, holder, null,
                holder != null && holder.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved,
                "FinishCastSecondary");
        }
    }

    [HarmonyPatch(typeof(Character), "GetFedItemRPC")]
    internal static class FedItemObservedPatch
    {
        private static void Postfix(Character __instance, int itemPhotonID)
        {
            if (RunCollector.Instance == null) return;
            Photon.Pun.PhotonView view = Photon.Pun.PhotonView.Find(itemPhotonID);
            Item item = view == null ? null : view.GetComponent<Item>();
            RunCollector.Instance.RecordItemEvent("ItemFedToPlayer", item, __instance, __instance,
                EventSource.RemoteObserved, "GetFedItemRPC");
        }
    }

    [HarmonyPatch(typeof(Item), "Consume")]
    internal static class ItemConsumedPatch
    {
        private static void Postfix(Item __instance, int consumerID)
        {
            if (RunCollector.Instance == null || __instance == null) return;
            Character consumer = null;
            Photon.Pun.PhotonView view = Photon.Pun.PhotonView.Find(consumerID);
            if (view != null) consumer = view.GetComponent<Character>();
            Character holder = __instance.holderCharacter;
            EventSource source = consumer != null && consumer.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved;
            RunCollector.Instance.RecordItemEvent("ItemConsumed", __instance, consumer ?? holder, null, source, "Consume");
        }
    }

    [HarmonyPatch(typeof(Action_ReduceUses), "ReduceUsesRPC")]
    internal static class ItemUsesReducedPatch
    {
        private static void Postfix(Action_ReduceUses __instance)
        {
            if (RunCollector.Instance == null || __instance == null) return;
            Item item = __instance.GetComponent<Item>();
            Character holder = item == null ? null : item.holderCharacter;
            RunCollector.Instance.RecordItemEvent("ItemUsesReduced", item, holder, null,
                holder != null && holder.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved,
                "ReduceUsesRPC");
        }
    }

    [HarmonyPatch(typeof(Character), "UseStamina")]
    internal static class StaminaUsePatch
    {
        private static void Prefix(Character __instance, out float __state)
        {
            __state = __instance == null || __instance.data == null ? 0f : __instance.GetTotalStamina();
        }

        private static void Postfix(Character __instance, float __state)
        {
            if (RunCollector.Instance == null || __instance == null || __instance.data == null) return;
            float current = __instance.GetTotalStamina();
            float change = Mathf.Abs(current - __state);
            const float thresholdComparisonEpsilon = 0.000001f;
            if (change > 0f && change + thresholdComparisonEpsilon >= StateKeeperPlugin.StaminaEventThreshold)
                RunCollector.Instance.RecordSimpleEvent("StaminaChanged", __instance,
                    __instance.IsLocal ? EventSource.LocalAuthoritative : EventSource.RemoteObserved,
                    "UseStamina:" + (__state - current).ToString("R"));
        }
    }
}
