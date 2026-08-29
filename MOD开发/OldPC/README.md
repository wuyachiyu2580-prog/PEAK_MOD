# OldPC 0.0.1

OldPC is a client-only visual mode for PEAK. It gives the local screen a low-spec, old-computer look and provides a temporary telescope clarity mode for inspecting distant terrain.

## Installation

Build `OldPC/OldPC.csproj`, then place `OldPC.dll` in the PEAK `BepInEx/plugins` folder.

## Controls

- Press `F8` to toggle telescope clarity mode.
- The key can be changed with `General.TelescopeToggleKey`.

OldPC mode is enabled by default. Telescope mode temporarily restores the captured local render settings, increases terrain visibility, and narrows the camera field of view. Press `F8` again to return to the OldPC look.

## Configuration

The configuration file is `BepInEx/config/com.wuyachiyu.OldPC.cfg`.

- `General.Enabled`: enable or disable the local visual mode.
- `General.TelescopeToggleKey`: telescope clarity hotkey. Default: `F8`.
- `General.TelescopeFov`: telescope field of view. Default: `22`.
- `Retro.RenderScale`: local render scale. Default: `0.45`.
- `Retro.ShadowDistance`: local shadow distance. Default: `80`.
- `Retro.LodBias`: local LOD bias. Default: `0.7`.
- `Retro.TextureLimit`: local global texture mip limit. Default: `1`.
- `Retro.PixelLightCount`: local pixel light count. Default: `1`.
- `Retro.FogDistance`: local shader fog distance. Default: `110`.
- `Retro.CloseFog`: local close-fog modifier. Default: `4`.
- `Retro.ScreenEffects`: enable the local CRT-style overlay. Default: enabled.
- `Retro.NoiseStrength`: CRT noise opacity. Default: `0.08`.
- `Retro.ScanlineStrength`: CRT scanline opacity. Default: `0.06`.

## Multiplayer

OldPC only changes local camera, render-pipeline, quality, shader, fog-color, and screen-overlay state. It does not send RPCs, change Photon room properties, spawn objects, or require the host to install the mod.
