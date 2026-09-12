using System;
using HarmonyLib;
using UnityEngine;

namespace StateKeeper
{
    [HarmonyPatch(typeof(CharacterSyncer), "OnDataReceived")]
    internal static class CharacterObservationSyncPatch
    {
        private static void Postfix(CharacterSyncer __instance)
        {
            if (RunCollector.Instance != null && __instance != null) RunCollector.Instance.ObserveSync(__instance.GetComponent<Character>());
        }
    }
    [HarmonyPatch(typeof(Character), "OnJump")]
    internal static class CharacterJumpPatch
    {
        private static void Postfix(Character __instance)
        {
            if (RunCollector.Instance != null)
                RunCollector.Instance.RecordJump(__instance);
        }
    }

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
            ItemSlot slot = null;
            if (character != null && character.player != null)
                slot = character.player.GetItemSlot(slotID);
            RunCollector.Instance.RecordItemSlotEvent("ItemPickedUp", slot, character,
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
        private static void Prefix(Character __instance, int itemPhotonID, out RunCollector.ItemEventCapture __state)
        {
            __state = null;
            if (RunCollector.Instance == null || __instance == null || !__instance.photonView.IsMine) return;
            Photon.Pun.PhotonView view = Photon.Pun.PhotonView.Find(itemPhotonID);
            Item item = view == null ? null : view.GetComponent<Item>();
            if (item != null && item.OnPrimaryFinishedCast != null) __state = RunCollector.Instance.CaptureItemEvent(item, __instance, __instance);
        }
        private static void Postfix(RunCollector.ItemEventCapture __state)
        {
            if (RunCollector.Instance != null) RunCollector.Instance.RecordCapturedItemEvent("ItemFedToPlayer", __state);
        }
    }

    internal sealed class ModifyStatusObservation
    {
        internal Item item;
        internal Character target;
        internal Character actor;
        internal float before;
        internal float statusSumBefore;
        internal bool wasPassedOut;
        internal CharacterAfflictions.STATUSTYPE status;
    }

    [HarmonyPatch(typeof(Action_ModifyStatus), "RunAction")]
    internal static class ModifyStatusOutcomePatch
    {
        private static void Prefix(Action_ModifyStatus __instance, out ModifyStatusObservation __state)
        {
            __state = null;
            if (RunCollector.Instance == null || !RunCollector.Instance.CollectionEnabled || __instance == null || __instance.changeAmount >= 0) return;
            Item item = __instance.GetComponent<Item>();
            Character target = item == null ? null : item.holderCharacter;
            if (item == null || target == null || !target.IsLocal || target.refs.afflictions == null) return;
            Character actor;
            if (!item.TryGetFeeder(out actor) || actor == null || actor == target) return;
            __state = new ModifyStatusObservation { item = item, target = target, before = ReadStatus(target, __instance.statusType),
                statusSumBefore = target.refs.afflictions.statusSum,
                wasPassedOut = target.data.passedOut || target.data.fullyPassedOut, status = __instance.statusType,
                actor = actor };
        }

        private static void Postfix(ModifyStatusObservation __state)
        {
            if (__state == null || RunCollector.Instance == null || __state.target.refs.afflictions == null) return;
            float after = ReadStatus(__state.target, __state.status);
            float amount = AssistanceEvidence.HealedAmount(__state.before, after);
            if (amount <= 0f) return;
            RunCollector.Instance.RecordItemOutcome("PlayerFriendHealed", __state.item, __state.actor,
                __state.target, __state.actor, amount, "Observed status reduction", __state.status.ToString(), __state.before);
            if (AssistanceEvidence.RestoredRecoveryEligibility(__state.wasPassedOut, __state.target.data.dead,
                __state.statusSumBefore, __state.target.refs.afflictions.statusSum, __state.target.data.petrifyAmount))
                RunCollector.Instance.RecordItemOutcome("PlayerLifeSaved", __state.item, __state.actor,
                    __state.target, __state.actor, amount, "Treatment crossed unconsciousness threshold; not a resurrection");
        }

        private static float ReadStatus(Character target, CharacterAfflictions.STATUSTYPE status)
        {
            return status == CharacterAfflictions.STATUSTYPE.Petrify ? target.data.petrifyAmount / 100f : target.refs.afflictions.GetCurrentStatus(status);
        }
    }

    [HarmonyPatch(typeof(RescueHook), "RPCA_RescueCharacter")]
    internal static class RescueHookOutcomePatch
    {
        private static void Postfix(RescueHook __instance, Photon.Pun.PhotonView characterView)
        {
            if (RunCollector.Instance == null || __instance == null || characterView == null) return;
            Character target = characterView.GetComponent<Character>();
            Item item = __instance.GetComponent<Item>();
            if (item == null) return;
            Character actor = item.trueHolderCharacter;
            if (target != null && actor != null && actor != target)
                RunCollector.Instance.RecordItemOutcome("PlayerRescuePulled", item, actor, target, actor, 1f, "RescueHook target acquired");
        }
    }

    [HarmonyPatch(typeof(Character), "RPCA_ReviveAtPosition")]
    internal static class ReviveObservedPatch
    {
        private static void Prefix(Character __instance, out bool __state)
        {
            __state = __instance != null && (__instance.data.dead || __instance.data.passedOut || __instance.data.fullyPassedOut);
        }
        private static void Postfix(Character __instance, bool __state)
        {
            if (__state && __instance != null && !__instance.data.dead && RunCollector.Instance != null)
                RunCollector.Instance.RecordSimpleEvent("PlayerRevivedObserved", __instance, EventSource.RemoteObserved, "RPCA_ReviveAtPosition; rescuer unknown");
        }
    }

    [HarmonyPatch(typeof(Item), "Consume")]
    internal static class ItemConsumedPatch
    {
        private static void Prefix(Item __instance, int consumerID, out RunCollector.ItemEventCapture __state)
        {
            __state = null;
            if (RunCollector.Instance == null || __instance == null) return;
            Character consumer = null;
            Photon.Pun.PhotonView view = Photon.Pun.PhotonView.Find(consumerID);
            if (view != null) consumer = view.GetComponent<Character>();
            __state = RunCollector.Instance.CaptureItemEvent(__instance, consumer ?? __instance.holderCharacter, consumer);
        }
        private static void Postfix(RunCollector.ItemEventCapture __state)
        {
            if (RunCollector.Instance != null) RunCollector.Instance.RecordCapturedItemEvent("ItemConsumed", __state);
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
