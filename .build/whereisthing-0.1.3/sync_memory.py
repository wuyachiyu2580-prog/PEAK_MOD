from pathlib import Path
root=Path('memory')
heading='## 2026-10-29 22:07 WhereIsThing 0.1.3 已实施'
common='用户已授权实施，且要求国际化最后统一修。0.1.3/0.1.3.0 已完成植物真实身份与迁移、箱子折叠筛选/单实体标签、当前段/全图范围、国际化及窗口刷新；71 项逻辑回归通过，PEAK 3.0.1 隔离 Release 0 警告 0 错误，已部署原 profile 的 WUYACHIYU-WhereIsThing 子目录。未启动游戏，主客机/自定义地图/中文字体与布局待实机。历史发行与 ZIP 未改。'
entries={
 'mods/WhereIsThing/README.md':common+' 详见 [实施记录](IMPLEMENTATION_0.1.3_2026-10-29.md)。下方旧“只调研”与 0.1.2 当前状态为历史。',
 'mods/WhereIsThing/STATUS.md':common+' DLL SHA256 4D99F461…8B522，180224 bytes；安装仅一份，cfg 未直接编辑。验收清单见 [实施记录](IMPLEMENTATION_0.1.3_2026-10-29.md)。',
 'mods/WhereIsThing/RECENT.md':common+' 新增 Tests 回归项目以及 PickupIdentity/Plants/Luggage/SegmentFilter 文件；最后统一语言分支、新配置翻译和打开窗口刷新。',
 'mods/WhereIsThing/DECISIONS.md':'最新用户“开始修改”覆盖本页旧调研限制；国际化最后做并包含新增功能。默认 CurrentSegment，WholeMap 保留全图能力；动态目标优先现场支撑地形再按运行时进度 Z，不能永久按出生段过滤。未知地图回退附近 40m 已写配置说明。箱子旧 token/分享协议保持，普通箱分组只是 UI 聚合；复活雕像列地标。Item 与 FakeItem 都遵守原版一次拾取引用，禁止修改游戏对象或 RPC。新默认部署目录采用已有 WUYACHIYU-WhereIsThing 子目录防重复。',
 'mods/WhereIsThing/FILES.md':'当前源码与程序集 0.1.3 / 0.1.3.0。新增 `ThingPickupIdentity.cs`、`WhereIsThingPlugin.Plants.cs`、`WhereIsThingPlugin.Luggage.cs`、`ThingSegmentFilter.cs`、`Tests/WhereIsThing.Regression.csproj`（71 检查）。隔离构建命令：`dotnet build MOD开发/WhereIsThing/WhereIsThing/WhereIsThing.csproj -c Release -p:OutputPath=C:/Users/Administrator/Desktop/MOD/PEAK/.build/whereisthing-0.1.3/`。默认 OutputPath/安装为 profiles/2.0.a/BepInEx/plugins/WUYACHIYU-WhereIsThing/，DLL 180224 bytes，SHA256 4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522。备份 `.build/whereisthing-0.1.3/backup-20261029-220734/`。详见 [实施记录](IMPLEMENTATION_0.1.3_2026-10-29.md)。',
 'mods/WhereIsThing/TODO.md':'代码、版本、71 项逻辑回归、Release 构建和 profile 部署已完成；不再执行下方“尚未实施”的旧候选状态。下一步仅实机验收：植物采摘/旧预设迁移、箱子高级筛选/陷阱状态、无限距离跨段/携带/Nadir/自定义地图、语言/字体/窗口布局与性能。清单见 [实施记录](IMPLEMENTATION_0.1.3_2026-10-29.md)。0.1.3 发行包未准备，未发布。',
 'mods/WhereIsThing/PLAN.md':'本轮实施顺序已完成：版本与植物 → 箱子及关卡范围 → 最后统一国际化 → 71 项逻辑回归/Release 0/0 → profile 单份部署与 memory。待用户实机反馈；发行打包另行处理。范围/回退和证据见 [实施记录](IMPLEMENTATION_0.1.3_2026-10-29.md)。',
 'mods/WhereIsThing/temp/current.md':common+' 当前最终 DLL hash 4D99F461…8B522；备份 `.build/whereisthing-0.1.3/backup-20261029-220734/`；源码和测试均已保存。下面“实施中/仅调研”段为历史。接续先读 [实施记录](../IMPLEMENTATION_0.1.3_2026-10-29.md)，不要重做已完成工作或声称实机通过。',
 'README.md':common+' 当前入口：[实施与验收](mods/WhereIsThing/IMPLEMENTATION_0.1.3_2026-10-29.md)。下方同日调研状态已被此项覆盖。',
 'TODO.md':'WhereIsThing 0.1.3 已实施、71 项逻辑回归通过、Release 0/0 并部署单份到原 WUYACHIYU-WhereIsThing 子目录。下一步为主客机、地形植物、跨段/自定义地图、箱子与语言 UI 实机；不再停留只调研。详见 [实施记录](mods/WhereIsThing/IMPLEMENTATION_0.1.3_2026-10-29.md)。未准备发行包/ZIP。',
 'MEMORY_INDEX.md':'- [新增] `mods/WhereIsThing/IMPLEMENTATION_0.1.3_2026-10-29.md`：五项更新、国际化最后统一、71 项回归、3.0.1 构建、0.1.3 单份部署与实机清单。\n- [修改] WhereIsThing 六件套、PLAN/current、根 README/TODO/CHANGELOG 同步当前已实施状态；旧同日调研保留为历史。',
 'CHANGELOG.md':'- [新增/修改/索引] WhereIsThing 0.1.3 实施记录及六件套/PLAN/current/根入口：植物身份和迁移、箱子聚合去重、关卡范围、最后统一国际化；71 项回归通过，Release 0/0，部署 WUYACHIYU-WhereIsThing 单份 DLL，备份旧 DLL/cfg；未实机/未改发行。只保存本地 Markdown。'
}
for rel,body in entries.items():
    path=root/rel
    text=path.read_text(encoding='utf-8-sig')
    assert heading not in text, rel
    first,rest=text.split('\n',1)
    path.write_text(first+'\n\n'+heading+'\n\n'+body.replace('\\n','\n')+'\n'+rest,encoding='utf-8')
    print(rel)
