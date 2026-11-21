# WhereIsThing 审查问题修复（2026-11-16）

用户“开始修改”已授权实施。**正确性修复、生命周期/内部残留与低风险优化已完成，未部署、未实机、未发行或提交。** 开发标识 0.1.3-dev.20261116.fixes，插件仍 0.1.3、程序集 0.1.3.0。此前晚安莓/预设改动保留，WhySoLaggy 既有修改未触碰。

## 已完成

### 共享、目录和计时

- 新增 WhereIsThingPlugin.State.cs，将房间、共享、有效选择与计时协调从主文件拆出，供正式流程测试直接编译。
- 在线 Room 对象引用界定会话，换房/同名新房/离房清旧发布与接收状态；房主切换使发布缓存失效。去重包含会话、actor、模式、payload/hash，协议常量仍为 2。
- 发送需要有效本机 actor。SetCustomProperties 返回 false 保持 dirty，并维持 0.25 秒发送间隔；true 记录已提交，等待回显期间不重复发相同内容。未增加 RPC、CAS 或心跳；“已提交”不是服务器确认。
- 房主 PublishedPresets 模式下，未就绪目录按 0.5 秒间隔自动重试，成功即停止。空物品库只产生场景/箱子条目时不提交目录、不迁移；目录解析/迁移及预设保存都会标记发布与有效选择需要更新。
- 无关 WIT 属性直接忽略；完整快照不变不重新解码。自由客机缓存共享信息但不重开本地编辑窗口。受限客机更新标注保留截止时间。
- 保留同房换房主三秒宽限，主循环负责无新回调时的超时清理；新房立即清旧共享视图。
- 扫描配置由主线程统一观察，ModConfig 与自带按钮走同一入口。常驻→定时产生有限截止，定时→常驻清截止；主动修改时长沿用原重新计时语义。同值更新无副作用。
- 自动同步、目录刷新、换房/房主和宽限到期不续时；显式扫描/预设与选择操作保留原有计时行为，停止的扫描不被自动启动。

### 生命周期与清理

- 主 Update 的菜单分支不再跳过 Enabled、到期、有效性/投影更新；菜单仍拦截扫描热键并保留光标维护。
- Picker 独占 Escape 处理：先关重命名层，再关预设窗。
- 场景 additionalFilter 加入已有标签有效性，每 0.25 秒复查；正常危险关闭原有 active 检查继续生效。
- 相机缓存检查 isActiveAndEnabled，并每 0.5 秒重新解析 Camera.main，覆盖同场景替换的缓存路径；真实观战仍需实机。
- 清 _scanSeenBuffer/seen 参数链、空 Normalize 与空循环、未使用 ActionFieldSubscription、无消费的字体来源 out 值；字体 fallback 一次收集候选后复用。
- 保留旧单选键、SelectedLuggage、旧 token/枚举与迁移，保留五个 WIT 共享字段。网络 LabelKeys/DesiredLabelKeys 继续承担标签清理。

### 低风险优化

- 有效选择并集/身份/晚安莓展开按 dirty 及共享来源变化重算；预设保存、启停、多选、迁移、限制与 session 变化均有失效入口。
- 网络标签先记录 desired key，再比较 target/source/definition/owner 显示绑定；不变时复用，背包同格换物/换格子实例、绳锚目标变化时移除旧绑定并重建。移除标签记录时清绑定缓存。
- 晚分类继续原覆盖、次数与预算；分类结果未变时保留原 record 和现有标签，避免旧委托捕获已替换记录。结果变化时正常清理并继承时间/扫描代数。
- 标签仅距离改变时只更新距离 TMP 文本；名称、显示方向和样式变化仍重排。
- 本地模式/时长/范围/距离/玩家名设置改为只刷新设置区域，避免销毁预设行、滑块和重命名输入。

## 未纳入本批的优化与限制

- 未裁剪绳索/投掷物的延迟分类覆盖，未增加段范围共享缓存、场景事件注册、对象池或虚拟列表；按照调研方案留待性能测量，避免先优化出漏标。
- 未对搜索列表做复用/防抖，也未改 BepInEx SaveOnConfigSet。当前修改配置本身会自动保存，所以只删显式 Save 或做局部防抖不能证明减少磁盘写；需要单独设计所有配置路径再实施。
- 全部标签内容仍有周期刷新，动态归属/语言正确性优先；本轮缓存的是有效选择及绑定，而非所有文字来源。
- 空库 guard 只要求真实可用 prefab，不证明其他 MOD 的分批物品注入已经结束。热替换目录仍属后续兼容议题。
- 未测实机 FPS、GC 或 Unity 主线程耗时，不宣称具体性能提升。

## 验证

1. 现有 helper 回归：**184/184 通过**，覆盖此前晚安莓、真实身份、蘑菇、预设、范围等。
2. 新增正式 Tests/StateFlow 项目：**97/97 通过**。直接编译生产 State.cs，构建前由 ExtractProduction.ps1 从当前生产文件逐字提取 Update、选择/保存、重试/绑定、Picker.Tick 和 ThingLabel.SetText 等方法及记录类型。
3. 新流程覆盖换房/同名房、房主/actor/共享模式、拒绝提交/节流恢复/延迟回显、空库迁移与自动补发、重复/无关属性、限制开关、三秒过渡、配置计时、菜单中更新/到期/禁用、Escape、选择缓存、重试原记录保留、背包绑定替换、设置局部刷新、附加过滤器与相机缓存。
4. 使用安装的 PEAK 3.1.0 游戏依赖，net4.7.2 Release 构建：**0 警告、0 错误**。首轮设置 BaseIntermediateOutputPath 导致旧 obj 被额外纳入产生重复 TargetFrameworkAttribute，恢复默认中间目录并只覆盖 OutputPath 后已解决；没有为绕过错误删除生产属性或用户文件。
5. Git diff whitespace 检查及本轮基线对比：未覆盖 WhySoLaggy 或历史发行。11 个只有换行/编码变化的源码文件已恢复本轮开始时的原始字节；实质编辑文件保留原编码/换行。

测试为逻辑级：Photon/Unity/配置持久化使用替身，TMP 使用计数布局写入替身，不能视为真实网络、原生布局、磁盘保存或性能验证。提取脚本仅适用于当前选定的方法体；未来在字符串/注释内引入花括号时需增强解析或改为直接编译单元。详见 Tests/README.md。

## 产物与复现

- 隔离目录：C:/Users/Administrator/Desktop/MOD/PEAK/.build/whereisthing-fixes-20261116/
- DLL：bin/WhereIsThing.dll，189952 bytes；SHA256 5FC795194989B4F0A47FF73B50914642FA86390526CD90EF2268B7D482AFBBC5。
- 同目录包含 PDB、build.log、regression.log、stateflow.log、baseline.json、backup/ 与 final-summary.json。
- baseline/backup 是本轮开始时的工作区快照，包含此前用户已授权改动；不是 Git HEAD，不应恢复成更早版本。
- implement_state.py、implement_lifecycle.py、prepare_tests.py 是一次性实施脚本，**不可直接重复执行**。后续修复在当前源码定点编辑。

构建命令（工作区根目录执行）：

    dotnet build MOD开发/WhereIsThing/WhereIsThing/WhereIsThing.csproj -c Release -p:OutputPath=C:/Users/Administrator/Desktop/MOD/PEAK/.build/whereisthing-fixes-20261116/bin/ --nologo

测试命令：

    dotnet run --project MOD开发/WhereIsThing/Tests/WhereIsThing.Regression.csproj -c Release
    dotnet run --project MOD开发/WhereIsThing/Tests/StateFlow/WhereIsThing.StateFlow.csproj -c Release

工程默认 OutputPath 仍指向 2.0.a/plugins 根目录，本轮命令均显式覆盖到隔离目录，未安装或改用户 cfg。开发 CHANGELOG 为英文 Unreleased，历史发行目录未更新。

## 后续实机验收

- 两端同配置换房、晚加入、房主退出/恢复、断线重入、三种共享模式；房主不打开菜单也能在物品库就绪后共享完整目标。
- 自由客机正在重命名时收到无关/共享属性；受限客机切换与宽限到期；ModConfig 与自带界面调整模式和时长。
- 打开菜单时目标移动、采摘、销毁、扫描到期；Escape 两层退出；距离滑动与中文布局/字体。
- 背包同格换物、绳索晚锚定/锚替换、投掷蘑菇归属变化、晚安莓自己/他人采摘与晚加入、当前段/全图。
- 性能测量后再决定复杂优化。未完成的实机项目不能写为通过；未部署、未发行或提交。
