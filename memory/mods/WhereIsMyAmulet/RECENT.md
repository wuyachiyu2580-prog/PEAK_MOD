# WhereIsMyAmulet Recent

## 2026-09-21 发行文案纠正

发行文件已按用户纠正恢复英文，沿用上一版章节、表格、图片位置和 manifest 排版；本版更新说明明确写入 README 的 What's new 和 CHANGELOG 对应版本章节。DLL/图标/旧版目录及 ZIP 未改，未生成新 ZIP。 当前发行版本 1.0.4。

## 2026-09-21 发行文件准备完成

当前版本 `1.0.4` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhereIsMyAmulet/发行/1.0.4`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：19→3 TMP、共享材质和刷新缓存、20000 排序、投影 Z/固定字号修正、ModConfig 适配。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

## 2026-09-21 ModConfig 集成更新

开发/测试版本 `1.0.4`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。恢复新版菜单跟踪并修正 UI 归属、枚举显示和刷新清理。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/1.0.4` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。

新菜单适配、声明方法去重、按配置文件/section/key 隔离标题和选项、仅修改枚举显示、合并可见 UI 刷新、保护自身配置行 LocalizedText。

WhySoLaggy.Tests 实际 23 项通过/0 跳过；四项目构建通过。完整 UI 验收未完成，详见 `../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

更新时间：2026-08-31

## 2026-09-20 1.0.4 测试中

最新字号补丁（优先于以下产物hash）：用户报告越远文字越大，已清除屏幕坐标中相机深度Z，创建/样式刷新统一关闭TMP自动字号。Release构建0警告0错误，IL验证Z=0、AutoSizing=false；profile DLL SHA256 `DA005FAF81403620415FDA735F623C3F78F506BFEB46DF2026D4E57F4CAAE74E`，版本1.0.4.0，排序仍20000，无诊断探针。实机效果待用户确认。

最终覆盖以下早先排查状态：用户对照确认30000不行、20000可以，已固定Canvas排序20000并在代码旁注释TMP/TFA排序关系。普通Release构建0警告0错误、IL验证20000且无探针，部署原profile，程序集1.0.4.0，SHA256 `281F477A31413E54A259294D510501A869A33922220B2F962CD67F7751137ED7`。未打包。

以下为先前诊断过程（已结束）：

- 标签19→3 TMP、共享描边阴影材质、相机/文本缓存、移除每帧ToList已实现。TFA兼容实测仍失败，不能宣布修复。
- 限量只读诊断构建：`dotnet build MOD开发/WhereIsMyAmulet/WhereIsMyAmulet.slnx --configuration Release -p:DefineConstants=TFA_UI_DIAGNOSTICS`；0警告0错误，程序集1.0.4.0。SHA256 `B39F7C0DEE5B580FFBE9D78D4A4B8E873F37422A92476C3A2625ED7A98F95080`。
- 已输出原profile根plugins，未打包、未修改发行1.0.3；下一步读取实机诊断报告。

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
