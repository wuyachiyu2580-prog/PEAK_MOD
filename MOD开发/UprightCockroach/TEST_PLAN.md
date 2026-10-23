# Upright Cockroach test plan

Earlier placeholder builds have been run inside PEAK; user screenshots show incorrect placement and white materials. The latest antenna-configuration build passes compilation and 13 offline BepInEx configuration checks, but has not passed in-game validation. The supplied FBX remains unrigged and is not integrated. Start PEAK with the 2.0.a profile, then check the cases below.

1. Load the airport and a fresh run. In first person, confirm the local player can see the roach arms/hands, antennae, wings and lower abdomen without the head blocking the view. In third person, confirm the full upright bipedal cockroach has body, wings, arms, legs, eyes and antennae.
2. Move, jump, climb, grab an item and carry/drop a player. The shell should follow the stock Animator and no new collider should affect movement.
3. Join with a second client that has the mod. Both clients should see both players as cockroaches. A client without the mod should still see the normal human model.
4. Check first person, third person/spectating, remote player, carried player, passed out, death, revive and scene transition.
5. Confirm backpack, held item, nameplate and interaction visuals are still present. Confirm no PhotonView, RPC or room property is added.
6. Edit one left antenna axis and save the config. Confirm only the left side moves after about one second; independently test the right side, first-person offset, rotation and segment length/thickness. Return to third person and confirm first-person offsets no longer apply.
7. Try an invalid attachment bone name and verify Head fallback without per-frame warning spam. Verify zero/default offsets reproduce the previous positions; local axes are not world axes.
8. In a diagnostic setup force replacement creation to fail; verify original character renderers remain available and partial replacement objects are cleaned up. Unload/destroy the visual and confirm the mod releases forceRenderingOff without restoring stale enabled flags.

If PEAK reports an exception, disable `UprightCockroach.dll`, preserve the new `BepInEx/LogOutput.log`, and record the first stack trace before changing the code.
