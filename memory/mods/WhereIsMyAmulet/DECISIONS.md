# WhereIsMyAmulet Decisions

## 2026-09-21 发行语言与格式

- 用户要求发行文件使用英文，保留上一版格式；仅追加对应版本更新说明及必要修改。
- README 和 CHANGELOG 都必须有当前版本的明确更新条目，不能只改版本号。

## 2026-09-21 发行文件范围

- 用户明确要求编辑四 MOD 发行文件，包含此前非 ModConfig 改动；该指示覆盖旧记录中的“验收前不更新发行目录”限制。
- 当前 `1.0.4` 目录仅准备 DLL、图标和三份说明/清单，不生成 ZIP、不上传；不因此宣称完成实机验收。
- 复用已验证最终 DLL，不改源码版本；既有历史发行目录及 ZIP 原样保留。

## 2026-09-21 ModConfig 接入约束

- ModConfig 保持 SoftDependency；按新菜单签名适配，旧类型只回退；按声明方法去重。
- 配置值、section/key、枚举原值与网络协议不变；只改归属明确的显示控件，不清空/重建第三方全局设置。
- 自身实例 autoSet=false 防止标题覆盖，不能修改共享模板；同根 UI 刷新合并，销毁清理自身监听/补丁。
- 本轮源码版本 1.0.4，历史发行包保留；最终实机边界见 `../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

## 1.0.3 映射和显示决策

> 2026-09-20最终结论：用户对照确认30000失败、20000成功，标签Canvas固定20000且无Raycaster；不要再回到30000/32700。TMP弹出列表固定30000，TFA会把窗口抬到活动Canvas最大排序+10，须留出层级空间。Manual mode本身会禁用难度/永久天气控件，与排序故障分开判断。临时探针已移除。当前开发1.0.4使用共享材质替代影子文本，以下1.0.3阴影描述为历史。

- Scout Statue 的权威映射固定为 `hasAmulets[slot] -> amuletObjects[type]`；不能按 `amuletObjects` 数组位置直接推断护符名称。
- Scout Statue 标签以 statue instance ID + slot 区分，运行时有效性必须重新确认当前 slot/type 和对应 GameObject，避免场景状态变化后保留错误标签。
- 双行标题下的距离文字和阴影使用 anchored Y `-40`；标题/目标位置不随之移动。
- 启动日志保持安静，普通加载提示不再输出；本地化、字体回退和运行时异常等可行动信息仍可记录。
- 惊喜模式不是 1.0.3 的现有功能；如后续实现，必须单独确认显示范围、默认值和多人/本地显示边界，不能把历史需求写成当前能力。

## 显示生命周期

- Overlay标签只能使用WorldToScreenPoint的XY，Z固定为0；不得将目标相机深度写入UI位置。标题、距离和箭头显式关闭TMP自动字号，保持配置/固定字号。2026-09-20代码与产物验证完成，用户远近视觉复测待完成。

- 默认 `General.ScanMode=Persistent`，保持常驻显示行为。
- `General.ScanMode=Timed` 时，扫描完成后以 `Time.unscaledTime` 记录结束时间，使用不受游戏暂停影响的时间，到期调用现有 `ClearLabels()`。
- `General.DisplayDurationSeconds` 最低按 `0.5` 秒处理，避免无效或负数配置造成异常；配置值仍以 BepInEx 原始数值保存。
- 定时逻辑只放在 `WhereIsMyAmuletPlugin`，不复制 WhereIsThing 的选择窗口、预设、范围编辑或 ModConfig 全局注册流程。

## 目标识别边界

- 普通雕像上的碎片使用 `PropSpawner_AmuletStatues` 生成的 `FakeItem`。
- Scout Statue 上的碎片使用 `ScoutStatue.amuletObjects`。
- 标签只读真实场景对象，不生成 fake item、不修改网络状态、不拾取碎片。

## 配置本地化

- `General` / `Display`、配置 key 和 `Persistent` / `Timed` 序列化值保持英文原值。
- 中文只通过 `ModConfigLocalization` 替换界面显示名和说明。
- PEAKLib.ModConfig 未安装或未初始化时必须静默降级。
