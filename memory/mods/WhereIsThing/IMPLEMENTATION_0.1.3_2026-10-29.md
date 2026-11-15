# WhereIsThing 0.1.3 实施与验证

## 2026-10-29 23:03 部署路径纠正

用户明确要求 DLL 放在 2.0.a/BepInEx/plugins 根目录。此前采用子目录不符合要求；本次检查时 profile 下已没有 WhereIsThing.dll/PDB，不推断移除原因。现已将验证过的 0.1.3.0 DLL 和 PDB 复制到 C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/，csproj 默认 OutputPath 同步恢复根目录。DLL 为 180224 bytes，SHA256 4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522，与隔离产物一致；递归复查仅一份 DLL。未改功能或配置、未重新构建/启动游戏。本条覆盖下方当前安装在子目录的历史描述。既有 71 项逻辑回归与实机待验状态不变；仅同步本地 memory。

状态时间：2026-10-29 22:07（PowerShell Get-Date）。用户已授权“开始修改”，随后要求国际化最后修；本轮按植物→箱子/范围→国际化顺序完成。下方旧调研中的“未获实施授权”已不再代表当前状态。

## 已实现

- 版本：PluginVersion/csproj 为 0.1.3，AssemblyVersion/FileVersion 为 0.1.3.0。游戏引用仍为本机 PEAK 3.0.1。
- 拾取身份：Item 使用原版拾取逻辑的一次 isSecretlyOtherItemPrefab 跳转；FakeItem 使用 realItemPrefab。目录按真实身份合并，标签位置保持现场载体。曼德拉草和药用根茎分别可选。
- 地形植物：Item.ALL_ITEMS 补充无 PhotonView 的植物，FakeItem 场景扫描仅针对两种根类、仅 Ground 范围，每 5 秒验证周期分步执行。采摘/失活/取消选择后旧标签失效；与普通 Item 使用相同实体键去重。不添加无法证明的“在地里”状态。
- 迁移：新增隐藏 PickupIdentityVersion=1，旧载体 ID 换成真实拾取 ID；自定义选择保留原来覆盖的两种拾取物；仅识别为完整旧默认组合的 Survival Medical 自动排除伪装曼德拉草，修改过的预设保留。共享协议仍 2、原枚举 token 不变；旧房主载体 ID 在新客户端扫描/摘要仍能解析。
- 箱子：默认普通/远古/诅咒/小丑/陷阱；普通包含 10 个旧地区/掉落池项，可展开高级筛选并显示部分选中数。复活雕像在地标。旧地区池选择仍有效，包括旧普通池选中的陷阱箱；不强行移除其原选择含义。
- 箱子实体：两个发现入口统一 luggage:instanceID，陷阱名优先；已开普通箱不再显示，已开且 TrickLuggage/statusField 仍有效的陷阱保留危险提示，失效后移除。
- 地图范围：新增本地 Display.MapScope（CurrentSegment 默认 / WholeMap），预设窗口与 ModConfig 都可切换；不写入房主共享协议。无限距离只取消米数限制。
- 段过滤：静态目标优先实际段根/营地；动态物品按实时位置，先检查支撑地形（非分配 RaycastNonAlloc），再用游戏运行时 progressPoints 的 Z 边界。当前段入口营地按游戏既有 40m 范围保留。标签资格统一过滤，所以屏外箭头/数字不会绕过。段变化、移动和设置变化触发重检。
- 地图资源核验：本机 level4 含 5 段；Caldera 与 The Kiln 同 biome=Volcano，必须按递增的两个进度点分别解析。对应 Z 为 -364.2、203.2、627.7、1297.9、2011.1，代码从运行时读取，不硬编码这些值。Nadir 是另附分支，不让其缺少顺序进度点破坏前五段判断。
- 未知布局：无可用顺序边界时继续用真实段层级/支撑地形；无法归属的动态目标仅显示本地玩家附近 40m，可选择 WholeMap。该回退会限制自定义地图未知目标的远距显示，不能称完整 TFA/PLL 兼容。
- 国际化最后统一：Game 尊重游戏语言；物品、背包/雕像后缀、变体计数、类别、箱子风险和摘要采用一致的名称语言。菜单外壳遵循游戏语言，保留原独立 NameLanguage 意义；非中英文的自定义文案仍以英文回退，游戏物品名使用官方表。
- Game 返回空值/LOC 占位时再查对应官方语言表；新增范围/高级筛选/内部迁移配置有中英文显示和说明。切游戏语言刷新已打开窗口而不丢工作副本，已有标签刷新文字与字体；中文可使用游戏提供的中文字体，不修改全局字体表。

## 验证与部署

- `dotnet run --project MOD开发/WhereIsThing/Tests/WhereIsThing.Regression.csproj -c Release`：**71 项全部通过**。测试链接实际生产源码，使用轻量游戏 API 替身；涵盖植物生命周期、选择迁移、预设序列化、箱子单实体/危险存续、Game/强制中英、段切换/携带/重叠地形/Nadir/未知地图回退。
- 隔离 Release 构建：**0 warnings / 0 errors**。输出 `.build/whereisthing-0.1.3/`，记录在 build.txt/regression.txt。
- 2026-10-29 22:07:34 已复制到 `C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/WUYACHIYU-WhereIsThing/WhereIsThing.dll`，同时复制 PDB。部署前检查无 PEAK 进程。
- DLL：180224 bytes，0.1.3.0，SHA256 **4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522**；安装与构建哈希一致，profile 下只有一份 WhereIsThing.dll。
- csproj 默认 OutputPath 改到上述已有子目录，避免后续构建在 plugins 根目录再生成第二份。
- 旧 DLL 与 cfg 备份：`.build/whereisthing-0.1.3/backup-20261029-220734/`。本轮不直接编辑用户 cfg；部署前后 SHA256 均为 811ECAD5C4C2FC2EF90127FB7A1FDA748BF9134C4945C9C29E609CFA92398CB5。新配置和迁移将在游戏加载插件时执行。
- 开发 CHANGELOG 已增加英文 0.1.3；历史发行目录与 ZIP 未改，未创建 0.1.3 发行包、未上传、未提交 Git。

## 实机待验（不等同自动回归）

1. 房主/客机分别找地形曼德拉草和药用根茎，拔起、拾取、丢下，开关 Ground/Held。
2. 旧配置首次加载与生存/自定义/旧房主共享预设；普通箱高级勾选和陷阱开箱前、持续危险期、失效后。
3. 无限距离下跨段、营地、上一段残留、下一段、携带后丢下；Caldera/Kiln、Gloom/Citadel、Nadir，TFA/PLL 等自定义地图分别验。
4. Game 中/英/繁、强制英文/中文；开着窗口改语言，保留未应用选择/搜索，检查截图里的蘑菇及雕像后缀、中文字体、窗口范围按钮布局。
5. 大量目标下 RaycastNonAlloc 与标签刷新性能。没有启动游戏或得到新日志，不能称“实机修复已验证”。

本地 Markdown 为交接依据；当前无独立 update_memory 客户端工具，不宣称同步到外部记忆服务。
