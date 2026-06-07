# Map Data

本目录用于沉淀 DreamyAscent 地图导出样本和验证数据。

## 目录约定

- `1.62.a/`
  - 当前以 PEAK `1.62.a` 为基线保存的原始导出数据。
- `1.62.a/DreamyAscent Files/`
  - 原始导出 JSON。
  - 这些文件最适合做批量统计、变体字段检查、回归对比。
- `1.62.a/DreamyAscent Diagnostics/`
  - 每次导出对应的完整诊断目录。
  - 里面包含 `RuntimeExport.json`、`NameMap.json`、`ObjectCatalog.json`、`ObjectReferenceMap.json`。
- `TerrainRandomiser/`
  - 预留给 `TerrainRandomiser` 强制切图/切变体后的验证样本。
  - 这些样本可以用于验证 DreamyAscent 的识别和纯净导出逻辑，但不要和“官方自然跑图样本”混为一类。
- `sample-index.json`
  - 当前样本集的机器可读索引。
  - 后续模板快照、对象注册表和回归检查工具优先读取它，不必解析 Markdown。
- `generated/`
  - 由 `data/tools/build_map_data_artifacts.py` 从诊断样本生成。
  - 包含 `template-snapshots.json`、`object-registry-input.json`、`sample-regression-report.json`。
- `../tools/`
  - 放置样本分析和产物生成脚本。

## 存放规则

- 只要对 DreamyAscent 的开发、验证、排查或后续研究有帮助的数据，都可以放进 `data/`。
- 如果现有目录不合适，可以按用途新建子目录，不必强行塞进已有结构。
- 官方自然跑图样本放进 `1.62.a/`。
- 用 `TerrainRandomiser` 强制切出的样本单独放进 `TerrainRandomiser/`。
- 后续如果游戏版本变化，按版本号新开目录，不要把不同版本样本混放。

## 当前状态

- `1.62.a/` 下已包含一批 DreamyAscent 导出和诊断样本。
- 当前 DreamyAscent 导出主逻辑已验证通过：
  - 非当前已加载关卡不再错误导出为 `0 grouper`
  - `ObjectReferenceMap.json` 已可稳定落盘
  - `BiomeVariant` 与 `VariantObject` 的当前激活分支可被识别并导出
- `SAMPLE_AUDIT_2026-05-13.md` 是当前样本集的集中审计记录，包含覆盖、完整性、关键样本和纯净性判断。
- `sample-index.json` 已记录本轮批量校验结果：26 个诊断目录完整、0 个 `0 grouper` segment、0 个未知 variant、五类已知变体全部覆盖。
- `generated/sample-regression-report.json` 当前为 `pass`，读取了 26 个诊断目录、130 个 segment、520 个 grouper、4001 个 step、3683 个 catalog item、402 个 catalog material、26350 个 object reference。
- `generated/object-registry-input.json` 当前合并出 193 个模板候选、25 个材质候选；其中 79 个是技术低风险候选，20 个是第一批推荐测试候选。
