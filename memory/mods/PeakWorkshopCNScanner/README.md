# PeakWorkshopCNScanner

**2026-10-07 用户最终确认**：profile中的scanner和CN DLL是用户为暂时游玩其他内容而自行移走，明确要求不要重新复制。保留当前停用/移走状态；扫描器源码、使用说明和候选DLL保存在开发区，50项离线检查通过，实机尚未验证。此前部署记录仅为历史。

最终目录变化（14:56）：scanner与原CN DLL在部署后均从profile消失，原因未知；候选保留，当前未安装。已询问用户是否重复制。下文的部署成功为14:50历史记录；先读STATUS最新段。

2026-10-07。用户要求改用小扫描 MOD 标注漏译，已实现独立 `0.1.0` 诊断插件并部署指定 2.0.a profile。它观察最终 IMGUI 文本，不提供译文。

当前恢复入口：`temp/current.md`；产物与边界：`STATUS.md`；试用任务：`TODO.md`；文件：`FILES.md`。开发区说明：`MOD开发/PeakWorkshopCNScanner/使用说明.md`。

50 项离线测试通过，Unity 实机尚未验证。原 PeakWorkshop 当前仅 `.dll.old`，必须在管理器启用后才会加载扫描器；本轮没有擅自恢复其启用状态。
