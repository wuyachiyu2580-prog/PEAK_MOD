# PEAKLib.ModConfig 本地化与安全集成规范

更新时间：2026-09-09

适用范围：所有需要在 PEAK 游戏中让 BepInEx MOD 配置跟随游戏语言显示的项目。

## 一、核心边界

PEAKLib.ModConfig 的职责是：启动时收集各 MOD 的 BepInEx `ConfigEntry`，建立配置列表，并据此创建界面。

语言切换时，MOD 只应修改已经存在的显示内容：

```text
配置注册：启动时一次
配置值：始终使用原始 section/key 和原始枚举值
显示名称/说明/选项文字：可以随语言变化
```

不要把“语言变化”当成“重新注册配置”。ModConfig 的全局缓存和内部注册方法属于第三方实现细节，不是给外部 MOD 使用的刷新 API。

## 二、绝对禁止的做法

任何 MOD 都不要反射操作以下全局状态：

```csharp
EntriesProcessed.Clear();
ModdedKeys.Clear();
GetValidKeyPaths.Clear();
```

也不要在语言变化或打开界面时反射调用：

```csharp
GenerateValidKeyPaths();
ProcessModEntries();
LoadModSettings();
```

这些方法可能会重新遍历所有 MOD 并把配置追加进列表。多个 MOD 同时执行时，结果就是：切换一次语言后所有 MOD 配置项复制一份，继续切换会继续追加。

尤其不要在 `OnGameLanguageChanged`、`Update`、ModConfig 菜单打开回调、`SceneManager.sceneLoaded` 或每个 MOD 自己的延迟初始化协程中调用全局重建。

如果旧代码有 `RefreshCache()`，不能只保留方法名再换一套反射写法；应移除它的实际调用和全局缓存操作。

## 三、section + key 是什么

配置文件中的分区和配置项名称是稳定身份：

```ini
[Display]
FontSize = 20
LabelFont = GameDefault
```

其中 `section = Display`，`key = FontSize`，配置身份就是 `Display + FontSize`。

本地化表应该使用这个身份，而不是使用当前屏幕上的文字：

| Section | Key | 中文显示 | English display |
|---|---|---|---|
| Display | FontSize | 标签字号 | Label Font Size |
| Display | LabelFont | 标签字体 | Label Font |
| Display | BoldLabels | 标签粗体 | Bold Labels |
| Presets | ShareMode | 房主共享模式 | Share Mode |

配置的 section/key 必须继续保存为稳定的英文内部名称。中文只用于显示，不能把中文名称写进新的配置 section/key，否则会破坏升级、迁移和跨语言兼容。

## 四、本地化数据层

为每个 MOD 建立一张以规范英文 section/key 为主键的本地化表。可以用 `switch`、字典或强类型记录，重点是键稳定：

```csharp
private static string GetLocalizedConfigName(string section, string key)
{
    bool chinese = IsChineseLanguage();

    switch (section + "\\0" + key)
    {
        case "Display\\0FontSize":
            return chinese ? "标签字号" : "Label Font Size";
        case "Display\\0LabelFont":
            return chinese ? "标签字体" : "Label Font";
        case "Presets\\0ShareMode":
            return chinese ? "房主共享模式" : "Share Mode";
        default:
            return null;
    }
}
```

分区也必须参与查表。不要只按 `key` 查找，因为未来不同 section 可能出现同名 key。

旧版本如果曾把中文写入 section/key，应增加“旧别名 -> 规范英文键”的迁移层；迁移只发生在读取旧配置时，不能让新配置继续使用中文键。

## 五、配置项名称的安全接入

如果 ModConfig 提供 `GetDisplayName` 入口，可以通过 Harmony 做后缀，但必须先验证配置归属：

```csharp
private static void DisplayNamePostfix(object __instance, ref string __result)
{
    ConfigEntryBase entry = TryGetConfigEntry(__instance);
    if (!IsOwnConfigEntry(entry))
        return;

    string localized = GetLocalizedConfigName(
        entry.Definition.Section,
        entry.Definition.Key);

    if (!string.IsNullOrEmpty(localized))
        __result = localized;
}
```

`IsOwnConfigEntry` 至少要用本 MOD 的配置文件名或 `ConfigFile` 实例判断：

```csharp
private static bool IsOwnConfigEntry(ConfigEntryBase entry)
{
    string path = entry == null || entry.ConfigFile == null
        ? null
        : entry.ConfigFile.ConfigFilePath;

    return !string.IsNullOrEmpty(path) &&
        path.EndsWith("com.example.MyMod.cfg", StringComparison.OrdinalIgnoreCase);
}
```

不能只因为当前正在渲染 ModConfig，就把所有 `GetDisplayName` 结果翻译成自己的文字。

## 六、描述文本的安全更新

语言变化时，只更新本 MOD 自己保存的 `ConfigEntry` 描述：

```csharp
public static void ApplyLocalizedDescriptions(IEnumerable<ConfigEntryBase> entries)
{
    foreach (ConfigEntryBase entry in entries)
    {
        if (!IsOwnConfigEntry(entry))
            continue;

        string text = GetLocalizedDescription(
            entry.Definition.Section,
            entry.Definition.Key);

        if (!string.IsNullOrEmpty(text))
            SetOwnDescription(entry, text);
    }
}
```

如果 ModConfig 没有公开修改 `ConfigDescription` 的接口，可以反射修改 `ConfigDescription` 的私有 backing field，但只能修改本 MOD 自己的 entry。这个兼容层必须 `try/catch`，失败时保留英文，不影响 MOD 主功能。

## 七、枚举和选项值

枚举的序列化值不能翻译。下面的值必须保持不变：

```text
Persistent
Timed
GameDefault
AscentDisplay
```

只替换界面显示：

```text
Persistent     -> 常驻
Timed          -> 定时
GameDefault    -> 游戏默认
AscentDisplay  -> 天阶显示字体
```

不要把中文写回 `ConfigEntry.Value`，也不要把中文写回 `.cfg` 文件。若 ModConfig 没有公开的枚举显示名入口，应只在当前 MOD 的可见设置控件中替换文字，并保留原始枚举名作为识别依据。

## 八、生命周期与语言变化

推荐生命周期如下：

```text
Awake
  1. Bind 配置
  2. Apply 自己的描述
  3. 安装只针对自己的 Harmony 补丁
  4. 订阅游戏语言事件（如果可靠）

Start
  只启动一个延迟本地化协程，等待语言和 ModConfig UI 初始化

语言变化
  1. 更新自己的 ConfigDescription
  2. 请求刷新当前可见的自己所属 UI
  3. 不清空 ModConfig 全局列表
  4. 不重新处理所有 MOD
```

语言事件可能在启动阶段连续触发，因此需要合并请求：

```csharp
private bool _refreshScheduled;

private void ScheduleLocalizationRefresh()
{
    if (_refreshScheduled)
        return;

    _refreshScheduled = true;
    StartCoroutine(DeferredLocalizationRefresh());
}
```

协程结束时必须恢复 `_refreshScheduled = false`。如果游戏没有可靠语言事件，可以每 `0.5` 秒检查一次语言状态并缓存结果，但不要每帧反射查询。

## 九、ModConfig UI 刷新策略

优先级从高到低：

1. 使用 ModConfig 公开的当前界面刷新接口；
2. 监听 ModConfig 菜单的打开/切换 section 生命周期，只刷新当前可见根节点；
3. 对当前 MOD 所属面板做有限帧数的稳定器协程，反复应用自己的文字；
4. 如果无法可靠判断面板归属，宁可只更新 `ConfigDescription`，不要扫描整个 ModConfig UI 并替换所有文字。

稳定器必须满足：只接收当前 MOD 的面板根节点，有最大帧数或超时时间，同一根节点只启动一个协程，只按规范 section/key 或已知原始文本替换，不调用全局注册、扫描或缓存重建方法。

## 十、迁移已有危险实现

对每个已有 MOD 按以下顺序处理：

1. 搜索危险调用：

```powershell
rg -n "RefreshCache|EntriesProcessed|ModdedKeys|GetValidKeyPaths|ProcessModEntries|LoadModSettings" <MOD目录>
```

2. 删除 `RefreshCache()` 中清空全局列表和调用 ModConfig 私有注册方法的代码。
3. 删除启动、语言事件、菜单打开和场景加载中的 `RefreshCache()` 调用。
4. 保留并收窄 `GetDisplayName` 补丁，只处理自己的配置文件。
5. 将按当前显示文字反查的逻辑改成 `section + key` 查表。
6. 合并重复的启动协程和语言切换协程。
7. 编译并检查源码中不再出现危险调用。

不能只修改 WhereIsThing。如果 PlayersInfo、LanternShootZombiesNight 或其他 MOD 仍执行旧的全局刷新，问题仍会存在。

## 十一、验证标准

必须在至少安装 `PEAKLib.ModConfig` 和目标 MOD 的环境中验证：

- 启动后每个 MOD 的配置项数量只出现一次；
- 连续切换中文/英文至少 5 次，配置项总数不增长；
- 打开、关闭、切换 ModConfig section 后，配置项总数不增长；
- 目标 MOD 的语言变化不会改变其他 MOD 的 section、key 或描述；
- 枚举下拉只改变显示文字，实际配置值不变；
- 删除 ModConfig 后，目标 MOD 仍能正常启动和运行；
- 日志中没有重复注册、清空全局缓存或异常反射堆栈；
- 语言切换期间没有创建重复 Harmony patch 或重复协程。

推荐加入日志：

```text
[ModConfig] Localization initialized: ownedEntries=<n>
[ModConfig] Language changed: Chinese -> English
[ModConfig] Updated owned descriptions: count=<n>
[ModConfig] UI localization applied: root=<name>, replacements=<n>
```

不要把每帧 UI 文本或所有 MOD 的配置列表刷进日志。

## 十二、当前项目状态

- WhereIsThing 已按这套安全原则移除全局 ModConfig 缓存重建，并改为稳定键本地化。
- PlayersInfo、LanternShootZombiesNight 以及其他没有迁移的 MOD 仍需按本规范审查。
- 本规范是后续新增中英文 ModConfig 功能的默认实现标准；除非确认 ModConfig 提供正式公开 API，不得恢复全局缓存反射刷新。

## 十三、PEAK 2.4.b / ModConfig 1.8.0 的 `LOC: 0`

2026-09-09 实测环境：

```text
PEAK 2.4.b
PEAKLib.ModConfig 1.8.0
PEAKLib.UI 1.7.0
PEAKLib.Core 1.7.2
```

诊断报告没有发现重复 DLL、配置身份或包装设置。唯一 `LOC: 0` 位于未激活的 `SettingsCell/Text (TMP)`。反编译链路如下：

1. PEAK 2.4.b 的 `LocalizedText.OnEnable()` 在 `index` 为空时使用 `row.ToString()`；默认 `row=0`。
2. 本地化表缺少 ID `0` 时，`LocalizedText.GetText()` 返回 `LOC: 0`。
3. PEAKLib.UI 1.7.0 使用全局资源搜索取得名为 `SettingsCell` 的原版对象作为模板。
4. ModConfig 1.8.0 克隆该模板后直接写 `component.m_text.text = item.GetDisplayName()`，但未禁用克隆上的 `LocalizedText`。
5. 初次直接写入可能暂时覆盖 `LOC: 0`，但后续 `LocalizedText.RefreshAllText()` 仍可能重新覆盖活动配置名称。

公共依赖的首选修复是在每个克隆的 `SettingsUICell` 上保留 `localizedText` 引用但设置 `autoSet=false`，随后写入最终显示名。不要向原版本地化表伪造 ID `0`，也不要让每个业务 MOD分别修改共享模板。

模板查找也应从明确的 `SharedSettingsMenu.m_settingsCellPrefab` 或已知菜单层级取得，避免使用无上下文的 `First(name == "SettingsCell")`。需要跨场景持有时，应克隆并规范化模板后再持有，而不是保存场景对象引用。

## 十四、新旧菜单类型迁移

ModConfig 1.8.0 的实际类型为：

```text
PEAKLib.ModConfig.Components.ModSettingsMenu
```

旧类型 `PEAKLib.ModConfig.Components.ModdedSettingsMenu` 已不存在。兼容代码应先解析新类型，再把旧类型作为旧版回退，并按真实方法签名补丁：

```text
ShowSettings()
SetSection(string)
UpdateSectionTabs(string)
```

当前需要迁移的开发区项目：PlayersInfo、Lantern&ShootZombies&Night、WhereIsThing、WhereIsMyAmulet、WhySoLaggy。

扫描各 `BepInEx*` 类型的 `GetDisplayName()` 时，只补丁真正声明该方法的实现，或者按 `MethodBase` 去重。不要重复补丁继承自同一泛型基类的方法；PlayersInfo、WhereIsThing 和 WhySoLaggy 已在 2.4.b 日志中产生 HarmonyX 警告。

详细实施顺序和诊断增强见 `mods/ModConfigDiagnostics/PLAN.md`。
