# 2026-09-12 StateKeeper 使用归因优化

- 上轮 analysisVersion6 并未完成整份计划：只增加逐行评分和粗略汇总；不能以构建成功代替测试。此次用组级 ItemUseRules 替换逐行打分，analysisVersion7。
- 完成动作事实与效果归因分开；结束时效果窗口不足不撤销已观察动作。资源量按字段保留原单位，持续燃料/比例变化不推导动作次数；推断持有人不作为确认使用者。
- 加入同组一致分类、epoch/中断隔离、移槽/转移、死亡后库存消失和重复事件身份键修复；新使用/流转视图保留旧贡献和流水。
- 测试发现原因为缺 Microsoft.NET.Test.Sdk，已补；最终79项测试全部通过，无跳过，报告`.build/test-results/use-final.trx`。当前12局Runs只读回放（1306可读块/220085帧/12406库存/366236事件），efec666c缺6块显式中断归因，逐字节核对原文件不改。
- 测试DLL已同步profile两个现有路径，SHA256为48AFCC1BDF1C01C86CE06572270B235D6E5F9C6314D2A09ADB3D55A8B4A14B34，备份`.build/profile-backup/20260912-230408-283/`。重启生效，历史发行ZIP未重打包。
- 仍未做：整份计划的10/30/60秒死亡前窗口专门摘要、完整山段资源报表、胜负资源差异和救援响应派生统计；Unity运行中UI与真实性能仍待实测。此次不接WhySoLaggy。

## 2026-09-09 StateKeeper 英文按钮与重命名修复（历史）

- 用户截图显示UNFAVORITE/RENAME截断，随后补充“点重命名没窗口”。两项一起修复，已构建并部署，未做Unity内截图验收。
- 只读解析PEAK 2.4.b level3原版资源：PauseMenu Canvas排序204，旧重命名子Canvas强制100，确实低于父菜单；现去掉子Canvas/GraphicRaycaster，与历史页同Canvas，SetAsLastSibling置顶。
- 原版UI_MainMenuButton_Resume的Button.m_OnClick.m_PersistentCalls包含GUIManager.Resume。RemoveAllListeners只清运行时监听，无法清持久监听；StateKeeperButton.Create现在赋值新的ButtonClickedEvent，避免操作同时触发恢复游戏。这不是推测，已读取实际资源确认。
- Daruma Drop One字体20号字测量：UNFAVORITE119.531、RENAME74.945、DELETE61.213；旧文字区108/72/72。新按钮176/128/112、左右16内边距，文字区144/96/80，均通过字形advance检查；紧凑文字18-20自适应。
- 历史行右侧保留488操作区，移除比较复选框对行文字宽度的二次覆盖。重命名打开强制输入焦点；离开历史页时关闭弹窗并恢复控件。
- tools/Audit-HistoryUi.py是只读资源与字形检查工具，依赖UnityPy及TypeTreeGeneratorAPI。后者仅装在.build/ui-audit-deps，不进入发行包。实际字体/资源核查不是运行中Unity排版测试。
- 64项测试通过，Release0警告0错误，未使用成员/参数静态检查通过；测试报告.build/test-results/statekeeper-ui-fix.trx。
- 0.1.0包原地更新（没有线上发布确认，不擅自升版）；独立Release、发行和profile DLL已同步。
- DLL SHA256：7FE428E3FCF3C08AC2065FAC044A584A142C6F40824A5FBD1C8AC2E38C660B71。
- ZIP SHA256：411945A8FB73A36CBA3823FFA2E01B2C6BBAEBA07D08726D9DD795E421D77AE0。
- 尚待重启游戏确认英文完整文案、重命名显示/输入/确认/取消和语言切换；不得写实机已通过。原始局和分析合同未改。
