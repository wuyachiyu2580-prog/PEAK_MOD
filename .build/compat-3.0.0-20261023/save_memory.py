from audit import *
import shutil
REPORT='COMPATIBILITY_3.0.0_2026-10-23.md'
heading='## 2026-10-23 PEAK 3.0.0 兼容性审核'
changed=[]
def insert(rel,body):
    p=ROOT/rel
    if not p.exists(): return
    old=read(p)
    if heading in old: return
    backup=OUT/'memory-before'/Path(rel).relative_to('memory')
    backup.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(p,backup)
    first,sep,rest=old.partition('\n')
    p.write_text(first+'\n\n'+heading+'\n\n'+body.strip()+'\n\n'+rest.lstrip('\n'),encoding='utf-8')
    changed.append(str(p.relative_to(ROOT)))
summary='当前游戏检查基线为 3.0.0，对照归档 2.6.b；12 个活动插件工程中 8 个隔离 Release 构建 0 警告/0 错误。ItemInfoCN、Lantern_ShootZombies_Night、TerrainCustomiserCN（含 Collector）需要适配；WhySoLaggy 另需更新写死 2.4.b 的测试路径。详见 ['+REPORT+']('+REPORT+')。本轮只审核并记录，插件源码/配置/版本/已安装 DLL/发行文件未改，未部署或启动游戏。'
insert('memory/README.md',summary)
insert('memory/MEMORY_INDEX.md',summary+'\n\n新审核报告包含构建矩阵、失效成员、反射/RPC 检查、测试结果和各项目实机边界；优先于下方旧版本兼容结论。')
insert('memory/TODO.md','- 待用户后续安排实施：修 ItemInfoCN 的 Mandrake/StatusField；修灯笼整合 MOD 的背包和点灯 API/同步；TerrainCustomiserCN 建立 3.0.0 引用/依赖并迁移生成/光照。\n- WhySoLaggy 测试目录需要从写死 2.4.b 更新；63 个 watched RPC 已独立核验仍在。\n- terrain profile 的 PlayersInfo 0.1.1 和 ItemInfoCN 1.0.0 为失效旧 DLL，使用前需核对更新；本轮未自动覆盖。\n- 其余通过构建项目需补 3.0.0 实机；StateKeeper 跨平台身份比较是条件性增强项，不判定 Steam 采集全面失效。\n- 完整结果：['+REPORT+']('+REPORT+')。')
insert('memory/CHANGELOG.md','- [审核/基线/状态/待办/索引] 新增 '+REPORT+'：12 工程隔离构建（8 通过），确认 ItemInfoCN、灯笼整合及 TerrainCustomiserCN 兼容欠账；WhySoLaggy 22过/1旧目录失败，63 RPC另核验全部在；StateKeeper 78过/1缺样本跳过，蟑螂14+49过。更新通用3.0.0基线及项目入口，源码/版本/profile DLL未改、未部署/实机。原始证据在 .build/compat-3.0.0-20261023/；240文件哈希保留。本地Markdown已同步，无外部update_memory工具。')
insert('memory/common/07_PEAK版本与反编译基线.md','当前基线改为 **PEAK 3.0.0**，参考目录为 引用参考代码/反编译/3.0.0；上一个对照为 作废/2.6.b。已安装 Assembly-CSharp.dll 为 1,648,128 bytes，SHA256 E9A8585ABFA566AFA7A3EF3E3699BC27369039EEEFCDB3CAA38D624D56F59E5C。\n\n主要变化涉及跨平台认证/房间加入、复活后延迟状态推送、篝火与传送状态行为。版本号变化不等于所有 MOD 都要改；8 个工程通过当前 DLL 构建，另有遗留不兼容和缺依赖项目。检查必须覆盖字符串反射、Harmony 目标与旧 publicized 引用，不能仅看编译。\n\n完整项目矩阵、证据和验证边界见 [3.0.0审核](../'+REPORT+')。以下原有 2.1.a 章节全部为 **2026-08-14 历史记录**，不再代表当前基线。')
affected={
 'ItemInfoCN':'必须适配：当前源码访问已删除 Mandrake.aoe，Release 3个CS1061；旧发行/terrain 1.0.0 DLL还含失效 StatusField 签名。Mandrake问题2.6.b已存在。本轮未修代码或更新DLL。',
 'Lantern_ShootZombies_Night':'必须适配：BackpackSlot.hasBackpack 导致8个字段错误及4个连带变量错误；Lantern.lit已是属性，LightLanternRPC已不存在，但源码仍反射字段、打补丁及发送旧RPC。必须按当前authority/OnPhotonSerializeView保留燃料同步语义；2.6.b已存在这些断点。0.2.1发行目录目前无DLL，本轮未修代码或部署。',
 'TerrainCustomiserCN':'必须适配：UBImGui/PhotonCustomPropsUtils引用缺失，主/Collector构建失败；游戏引用仍为2026-05-22旧publicized DLL。0.1.2发布DLL有23个失效成员，涉及生成和光照；LevelGenStep.Execute现返回IEnumerator，PropGrouper.RunAll返回Awaitable。需独立兼容迁移，本轮未改地图/网络/源码或部署。',
 'WhySoLaggy':'1.0.5插件隔离Release 0警告/0错误，当前DLL成员检查无断点。现成测试22通过、1失败（写死反编译2.4.b目录）；独立复核63/63 watched RPC仍在3.0.0。需要更新测试路径，不因此判定插件运行时不兼容；测试源码与profile/发行DLL本轮未改。'
}
for mod,body in affected.items():
    for leaf in ('README.md','STATUS.md','TODO.md','RECENT.md','DECISIONS.md','FILES.md','temp/current.md'):
        link=('../../../' if leaf.startswith('temp/') else '../../')+REPORT
        intro=body+'\n\n详细结论与原始证据：['+REPORT+']('+link+')。这是审核结果，不是实施完成或实机验收。下方旧版本结论仅适用于原记录时间。'
        insert('memory/mods/'+mod+'/'+leaf,intro)
passed={
 'PlayersInfo':'0.2.5隔离Release 0/0，当前引用和2个静态Harmony目标通过；terrain profile另有0.1.1旧DLL，含失效背包/物品槽成员，未覆盖。',
 'WhereIsThing':'0.1.2隔离Release 0/0，直接成员以及绳索/投掷者/生成池关键反射入口仍在。',
 'WhereIsMyAmulet':'1.0.4隔离Release 0/0，直接成员与ScoutStatue.hasAmulets入口仍在。',
 'StateKeeper':'0.1.0隔离Release 0/0，20个静态Harmony目标仍在；现成测试78通过、1因缺可选录像跳过、0失败。跨局比较仅接受17位Steam ID，非Steam/哈希身份需额外支持；不能据此判定Steam采集全面失效。',
 'PeakMapBrowser':'csproj 0.1.1隔离Release 0/0，直接游戏/Unity成员可解析；账户/网络API在线联调未进行。',
 'ModConfigDiagnostics':'0.1.0隔离Release 0/0，Unity直接成员可解析；第三方ModConfig实际菜单与诊断需实机。',
 'UprightCockroach':'0.1.0隔离Release 0/0，Character.Start/渲染API仍在，14配置+49模型/配置检查通过。保留现有实机画面、骨骼、多人和生命周期待验项。'
}
for mod,body in passed.items():
    insert('memory/mods/'+mod+'/STATUS.md',body+'\n\n完整结果：[3.0.0审核](../../'+REPORT+')。本轮未改源码/版本或部署DLL，构建通过不代表实机兼容已验收。')
dump('memory-updated-files.json',changed)
print(json.dumps({'updated_count':len(changed),'files':changed},ensure_ascii=False))
