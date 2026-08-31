# WhereIsMyAmulet Recent

更新时间：2026-08-31

## 1.0.3 发布

- 当前发布版本为 `1.0.3`，程序集版本为 `1.0.3.0`。
- 修正 Scout Statue 的碎片名称映射：读取 `hasAmulets[slot]` 得到 fragment type，再访问 `amuletObjects[type]`；标签键按 statue + slot 保存，并在有效性检查时同时校验 slot/type/object。
- 距离文字和阴影锚点从 `-16` 下移到 `-40`，避免双行标题的来源文本与距离重叠。
- 删除启动时的普通 Info 日志，只保留本地化和字体兜底等可行动警告。
- `发行/1.0.3` 当前包含 `WhereIsMyAmulet.dll`、`README.md`、`CHANGELOG.md`、`manifest.json`、`icon.png` 和 ZIP；发行 DLL 为 `40960` 字节，SHA-256 为 `77AA45DA6A90664910172596649F8E567F3369961D4EC19D7A5634E5FBDC7984`。
- DLL 已同步到 `C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WUYACHIYU-WhereIsMyAmulet\WhereIsMyAmulet.dll`，profile DLL 与发行 DLL hash 一致。

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

- 仍需在游戏内确认定时到期时普通掉落、背包、普通雕像和 Scout Statue 标签都能同时清除。
- 仍需确认多人场景下扫描和定时隐藏不会影响其他客户端的显示。
- 惊喜模式尚未进入当前 1.0.3 源码或发行包，不能当作已实现功能。
