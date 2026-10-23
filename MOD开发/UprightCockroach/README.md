# Upright Cockroach

PEAK client-side visual mod that replaces player bodies with an upright cockroach shell. The stock Character, Animator, ragdoll, IK, colliders, items and Photon object stay in place, so the mod does not add network state or change gameplay.

The current development build embeds a converted, textured four-arm upright cockroach asset. It removes the microphone, grass and scene floor, keeps the stock Scout Animator/IK/ragdoll/Photon objects, and skins the mesh to the Scout pose. The two additional arms follow the player arms locally and do not add gameplay or network limbs.

## Current status

- Version: `0.1.0` development MVP
- Target: PEAK `2.6.b`
- Client side: every installed client sees its own local shell and shells for characters it renders; players without the mod still see normal human characters.
- Not included yet: lobby preview replacement and full in-game single-player/multiplayer validation. The FBX has no original skeleton; the package uses a documented landmark-based binding that still needs playtest review.

## Antenna settings

The development build adds independent left/right antenna position, rotation, attachment bone, length, thickness and first-person offsets. Edit `BepInEx/config/wuyachiyu.peak.uprightcockroach.cfg`; saved changes are checked once per second while the game runs. Coordinates are local to the selected bone, not world axes. Restart the game once after installing the new DLL.

These settings control the imported antenna bone chain. The supplied FBX had no skeleton or skin weights, so the offline converter added Scout-compatible bones and used all 8 diffuse textures. Existing screenshots came from the old placeholder build; a successful build does not establish game visual quality.

The local first-person view now has separate compact head, wing-case and antenna groups. The head is scaled down and moved toward the lower screen edge, while antennae use independent `FirstPersonLengthScale` and `FirstPersonThicknessScale` values. The `[FirstPerson]` section can disable or reposition these groups without affecting third-person players.

## Build

```powershell
dotnet build .\MOD开发\UprightCockroach\UprightCockroach\UprightCockroach.csproj --configuration Release --nologo
```

The DLL is emitted to the configured r2modman PEAK `2.0.a` profile plugins directory.
