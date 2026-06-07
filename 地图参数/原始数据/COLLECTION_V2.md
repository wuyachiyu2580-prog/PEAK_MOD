# Snapshot V2 Collection Rules

这轮重采只接受带 `GeneratedChildrenSnapshot.json` 的诊断目录。
2026-05-17 后要求 `GeneratedChildrenSnapshot.json` 的 `schemaVersion` 至少为 `3`。

## 目录

- 官方自然地图样本放到 `data/map-data/1.62.a-snapshot-v2/`。
- `TerrainRandomiser` 强制变体样本放到 `data/map-data/TerrainRandomiser-snapshot-v2/`。
- 旧目录 `1.62.a/` 和 `TerrainRandomiser/` 不再参与新产物构建。
- 当前 `generated/template-snapshots.json` 和 `generated/object-registry-input.json` 暂时保留给 DLL 运行时使用；新样本回归通过前不要覆盖。

## 官方自然样本

- 新开地图，进入后不要操作 DA 生成、模板切换、导入、参数编辑。
- 直接点击“写出诊断”，或使用初始自动诊断。
- `GeneratedChildrenSnapshot.json` 里 `potentiallyDirty` 必须是 `false`。
- `externalMapModifiers` 里不能有 enabled 的 `TerrainRandomiser`。
- `GeneratedChildrenSnapshot.json` 里每个 segment 应带 `relationshipCandidates`，用于检查椰子/椰子树、子生成器、`SingleItemSpawner`、桥、营火附属物、RisingLava、独立机关等父子/业务关系候选。
- 关键生成器或关系候选应带 `interestingComponentFields`，用于一次样本里看清 `BeachSpawner.treeParent/palmTrees/spawned`、`PSM_ChildSpawners`、`PSM_SingleItemSpawner.objToSpawn`、`SingleItemSpawner.prefab` 等引用字段。

## TerrainRandomiser 样本

- 只用于补官方自然跑图难遇到的变体。
- 需要重新用新版 DLL 采集，旧 TR JSON 不再复用。
- `GeneratedChildrenSnapshot.json` 里应能看到 enabled 的 `TerrainRandomiser` 标记。
- 同样要求 `schemaVersion >= 3` 和 `relationshipCandidates`，否则不算新版样本。
- 这些样本可以参与变体覆盖和生成结果观察，但不能改名混进官方自然样本目录。

## 先跑示范

先只跑一份示范样本。确认 `schemaVersion=3`、`relationshipCandidates`、`interestingComponentFields`、脏样本判定、TR 标记和文件体积都正常后，再全量重跑。
