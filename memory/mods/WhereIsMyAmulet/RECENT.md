# WhereIsMyAmulet Recent

更新时间：2026-08-20

## 1.0.2 发布

- 恢复扫描标签的 `Persistent` / `Timed` 两种显示模式，默认值为 `Persistent`。
- 新增 `General.DisplayDurationSeconds`，默认 `8` 秒；定时模式下每次按扫描键重新计时，到期清除全部标签。
- 逻辑只借鉴 WhereIsThing 的标签生命周期，不引入其选择窗口或窗口配置。
- 雕像碎片仍然通过现有的 `FakeItem` / `ScoutStatue` 对象识别，不生成 fake item，不拿起或修改碎片。
- ModConfig 的中英文名称、说明和 `Persistent` / `Timed` 枚举显示已补齐；原始 section、key 和枚举值保持不变。
- `dotnet build MOD开发/WhereIsMyAmulet/WhereIsMyAmulet.slnx -v:minimal` 通过，0 警告、0 错误。
- DLL 已同步到 `C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsMyAmulet.dll`。
- 发行包为 `MOD开发/WhereIsMyAmulet/发行/1.0.2/wuyachiyu-WhereIsMyAmulet-1.0.2.zip`，包含 DLL、README、CHANGELOG、manifest 和 icon。

## 当前待验证

- 仍需在游戏内确认定时到期时普通掉落、背包和两类雕像碎片标签都能同时清除。
- 仍需确认多人场景下扫描和定时隐藏不会影响其他客户端的显示。
