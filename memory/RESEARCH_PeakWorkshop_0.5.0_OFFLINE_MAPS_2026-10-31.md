# PeakWorkshop 0.5.0 地图压缩包分享与离线安装调研

## 2026-10-31 已下载地图转发包已准备

用户澄清目的：工作坊下载慢，由一人下载后打包分享给其他玩家，保留在线下载身份以复用同版地图。本次采用手动解压 ZIP，保留 BepInEx/PeakWorkshop/maps/_net/<原目录>/ 全部内容，包括 .forge-online.json 和 cache，不按普通独立 .peakmap 导出规则剔除元数据。仅发现一张已下载地图“自群山来”，在线版本 13，map.json 作者修订 1001。已生成 地图素材/PeakWorkshop离线分享/PeakWorkshop-FromTheMountainRanges-online-r13-20261031.zip，73,261,005 bytes；13 个地图文件逐一 SHA256 与源文件一致，另含 INSTALL.txt 和 offline-map-manifest.json（放包根层，不进入地图目录）。包 SHA256 717FE11DE2C7BE11FD8C879C308BA836D2E4B4C5196A6B409CD46C478CF1BB6E，旁附 .zip.sha256；脚本和验证在 .build/peakworkshop-offline-share/。未改源码/配置/已安装地图，未上传或发送给他人、未实机。接收方关闭游戏后将包内 BepInEx 合并至实际 profile 根目录；同版 CID/pkg 匹配才可跳过地图重下载，新版本仍可能更新。_net 保留原缓存清理策略。下方最初“未打包”的描述仅代表前一轮调研。

日期：2026-10-31（Asia/Shanghai）。范围：用户提供的 0.5.0 反编译代码与本机现有地图目录，只做分析；未修改插件、配置、地图或已安装 DLL，未构建、打包或启动游戏。未恢复已作废的 PeakWorkshopCN/Scanner。

## 结论

**可以。普通完整地图已有原生离线包读取能力，无需先修改 PeakWorkshop DLL。** `.peakmap` 实际是 ZIP 容器。将地图目录的内容放在 ZIP 根层，改扩展名为 `.peakmap`，放入接收者实际 profile 的 `BepInEx/PeakWorkshop/maps/`，启动时插件自动解包为同名地图目录。普通 `.zip` 不会被此自动扫描入口识别。

地图安装不依赖工作坊 API、登录或上传，但接收者需要已安装可兼容的 PEAK、BepInEx 和 PeakWorkshop。多人联机仍使用游戏的 Steam/Photon 通路；离线安装不代表断网多人联机。普通地图方案与远征方案必须区分：远征规划会引用多个地貌及其包版本，不能只复制规划文件。

以下结论是源码级分析，不代表已经完成断网启动、干净 profile 或双机验收。

## 源码证据

反编译根目录：`引用参考代码/反编译/BepInEx/plugins/PeakWorkshop0.5.0/`。

| 证据 | 文件与行号（相对反编译根目录） | 判断 |
|---|---|---|
| 插件声明 0.5.0 | `PeakMapForge/Plugin.cs:26`、`:37` | 确认分析版本 |
| 独立数据目录 | `PeakMapForge/DataPaths.cs:20`、`:22` | 默认 `BepInEx/PeakWorkshop/maps`，不是 `plugins/maps` |
| 启动时自动解包 | `PeakMapForge/Plugin.cs:146`、`:149` | 启动迁移之后调用 `MapPack.UnpackAll` |
| 包文件发现 | `PeakMapForge.Format/MapPack.cs:63`、`:73` | 扫 maps 根、插件 DLL 所在目录及其 maps 子目录；只扫描直接子文件 `*.peakmap`，不递归 |
| ZIP 解压实现 | `PeakMapForge.Format/MapPack.cs:301`、`:305` | `ZipArchive` 读取包，根层需存在 `map.json`；不会自动剥离外层目录 |
| 自动更新与保护 | `PeakMapForge.Format/MapPack.cs:108`、`:114`、`:123`、`:131` | 无 stamp 的同名人工目录不覆盖；有 stamp 且包内容变化时删除旧解包目录再解包 |
| 本地地图列表 | `PeakMapForge.Editor/EditorSession.cs:4060` | 扫 maps 直接子目录中的 `map.json` |
| 本地 CID 索引 | `PeakMapForge.Online/MapStore.cs:77`、`:1089`、`:1153` | maps、_net、_exp 等目录的地图可本地识别；普通目录避免 `_`、`.`、`~` 开头 |
| 客户端本地优先 | `PeakMapForge.Patches/JoinGatePatches.cs:78`；`PeakMapForge.Net/JoinFlow.cs:311` | 相同 CID 已在本地时可直接通过地图检查，不必先从服务器下载 |
| 下载前本地判断 | `PeakMapForge.Online/MapDownloader.cs:303`、`:330` | 已有匹配地图且无需补包则直接完成 |
| _net 包版本例外 | `PeakMapForge.Online/MapStore.cs:360` | _net 内 CID 相同但 installed 包版本不同可要求刷新；普通本地目录不走该补包判断 |
| 地图依赖诊断 | `PeakMapForge.Net/MapIdentity.cs:257`；`PeakMapForge.Build/MapNeeds.cs:52` | 检查模型、物品、bundle；仅 JSON 或仅本机能运行不足以证明可移植 |
| 模型库回退 | `PeakMapForge.Build.Models/ModelLibrary.cs:131` | 本图 models 之外还有本机模型库和 Workshop 回退，需要防止漏打依赖 |
| 地图 CID 范围 | `PeakMapForge.Net/MapIdentity.cs:177`、`:462`、`:562` | 对参与内容计算 SHA-256；排除 cache、online 元数据、部分备份和未使用资源 |
| 远征地貌解析 | `PeakMapForge.Expedition/ExpeditionComposer.cs:224`；`PeakMapForge.Net/ExpeditionLobby.cs:288` | local 用目录 ID，workshop 用在线 ID 与 pkg 元数据；缺失/版本不符会尝试下载 |
| 完整远征本地命中 | `PeakMapForge.Generation/RuntimeGenerator.cs:266`；`PeakMapForge.Net/JoinFlow.cs:312` | 已有与房主 CID 相同的完整合成结果，可跳过重新拉地貌的路径；仍需实机验证阶段切换 |

## 推荐：单地图原生 .peakmap

### 分享方

1. 找到实际地图目录。自建地图通常在 `maps/<目录>/`；下载地图通常在 `maps/_net/<id>@<cid前12位>/`。
2. 在独立导出副本中检查并补齐地图依赖：models 及其贴图/附属文件、items 定义及资源、assets bundle、sounds、地图实际引用的地形/其他文件。原版 prefab 由游戏提供，不需要把游戏资产打进去。
3. 保留地图自带 `cache/`，尤其散布记录、光照及阶段记录。它们不参与地图 CID，不意味着可以当普通临时缓存删除；同 CID 不保证这些记录相同。全局 `BepInEx/PeakWorkshop/cache` 与地图内部 cache 不是一回事。
4. 压缩地图目录的“内容”，确保压缩包根层就是 `map.json`，不是 `<地图文件夹>/map.json`。使用普通 ZIP 格式，不能只把 RAR/7z 后缀改成 peakmap。
5. 改名为例如 `my-map-r12.peakmap`。不要改 map.json 内的 id、revision 或内容来做本轮分享；保留文件相对路径和文件名大小写。文件名避免 `_`、`.`、`~` 开头。

包内容示意（除 map.json 外按地图实际需要）：

```text
my-map-r12.peakmap  [ZIP 容器]
  map.json
  thumb.png
  models/
  items/
  assets/
  sounds/
  cache/
  credits.json
```

普通独立地图导出遵循 `MapPack.ShouldPack`：排除 `.peakmap.stamp`、`.forge-online.json`、`votes.json`、自动保存、备份、运行统计及系统垃圾；安装时原生代码会生成 stamp。不要把说明/额外 manifest 随意塞进地图内容里：未排除的新文件可能改变 CID，外层说明应放在地图包之外。

### 接收方

1. 提前装好对应游戏和 PeakWorkshop 0.5.0 及其依赖，退出游戏。
2. 放入所用 profile 的 `BepInEx/PeakWorkshop/maps/my-map-r12.peakmap`。当前机器已确认 `[Map] File` 为空，采用默认目录。
3. 启动游戏，插件自动解包到 `maps/my-map-r12/`，在 PeakWorkshop 自己的入口/本地地图列表选择。原版主菜单的 Play Offline 仍是原版流程，不能据此判断自定义地图未安装。
4. 包是在插件启动时扫描；游戏运行中复制后应重启。已有 active.txt 时不会保证自动选中新包，应自行选择。

本机当前默认目标：

```text
C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/PeakWorkshop/maps/
```

若别人设置了 `[Map] File` 为自定义地图根目录，应使用其实际配置目录，不照抄本机绝对路径。

同名更新需注意：人工创建、无 `.peakmap.stamp` 的目录不会被新包覆盖；原生解包目录在同名包变更时会整目录替换。建议使用可区分修订的包名，不在自动解包目录中保留未备份编辑。

## 普通 ZIP 与远征分享

| 形式 | 是否可行 | 边界 |
|---|---|---|
| 完整地图 .peakmap | 可以，优先推荐 | 原生自动解包，一包一地图 |
| 普通 ZIP 手动解压 | 可以作为文件分发 | 需解成 `maps/<地图>/map.json`；直接扔 .zip 不会自动安装，散布记录的来源判断与原生 stamp 路径需实测 |
| 一个 ZIP 内多份 .peakmap | 可以作为外层分发包 | 先解出各 .peakmap 到 maps 根，插件不会递归读取外层 ZIP |
| 只发 map.json | 仅纯内置资源且无外部依赖时可能够用 | 声音、模型、贴图、物品、缓存记录等可能漏失 |
| 只发远征规划 | 通常不够 | 还需要所有指定地貌及其准确版本 |
| 分享完整合成远征结果 | 源码有可行入口 | 各方 CID 一致可跳过地貌下载，但尚未验证后续阶段及继续游戏 |

远征若要保留“规划、重组、续跑”的完整能力，应打一个保留相对路径的外层 ZIP，包含规划、所依赖的全部地貌、必要的平台和资源。Workshop 地貌要保留真实 `.forge-online.json` 中的 ID/pkg/installed 等信息，并保持其 _net 布局；不能一律按普通地图导出规则剔除此文件，也不能伪造版本数据。复制整个账号/在线会话目录没有必要。

尤其 `ExpeditionComposer.ResolveDir` 的 workshop 分支首先要求 _net 存在，再检查在线 ID/pkg，不能把所有地貌随意搬到顶层且删元数据后宣称一定离线可用。

## 已有实际样本

本机发现普通下载地图 `自群山来 | From the Mountain Ranges .<Test>`（实际标题含空格差异）：

- 目录：`maps/_net/asset_20261002_1516-3@db5f05439ea2/`。
- map.json：format=peakmap/1，id=asset_20261002_1516，revision=1001，kind 未指定。
- 13 个文件，共 75,032,812 字节（约 71.6 MiB）。包括 map.json、thumb.png、在线元数据、7 个 OGG、3 个 cache 文件。
- map.json SHA256：`12579EF6B2933033FE1BE728CDDADD5D576AB61A52727860ECF84109D5BFBE12`。
- 已说明该图不能只发 JSON；本轮只核对目录和 JSON 摘要，没有做完整依赖闭包、压缩往返或游戏运行测试。

## 如果后续要实现一键导出/导入

建议复用原生 .peakmap 格式，先做普通地图导出与校验，不另造游戏加载格式。导出工具负责依赖检查/补齐、保留地图 cache、过滤临时文件、检查根层、记录游戏与 MOD 版本，并按原生算法验证 CID；包级 SHA256 用来保证包括 cache 在内的全部导出内容一致。

导入若使用外部工具，应先在暂存目录验证路径、完整性和同名冲突，成功后再放入目标 profile；避免原生“先删除旧解包目录再解压”的更新过程在损坏包上造成损失。大型包原生实现会先把整个压缩文件读入内存，导出时也应检查体积。远征支持作为第二阶段处理，单独解析地貌版本依赖。

建议验收：用无该图和无共享资源库的干净 profile，先验证 `.peakmap` 自动解压及本地列表，再关闭工作坊 API 或断开其访问验证无下载仍能加载；最后双机同一游戏/MOD/地图版本验证 CID、散布状态、声音、所有阶段和迟加入。网络多人验收仍保留 Steam/Photon 连接。

本轮未制作分享包、未做插件改动。
