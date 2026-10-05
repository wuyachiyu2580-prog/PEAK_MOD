# PeakWorkshopCN 决策与边界

日期：2026-10-05。

## 用户已确认

- 用户重新审核后说“开始”，已授权补齐、构建与指定 profile 部署。版本保持 0.1.0，按完整审核清单继续。

- 名称 PeakWorkshopCN，版本0.1.0，通过补丁中文化。初始调研不写代码，后续“先执行，我看看效果”已授权实现、构建与试用部署。
- 范围包括菜单按钮、提示、编辑器标签和配置说明。
- 游戏参考2.6.a，原MOD反编译指定PeakWorkshop目录，DLL输出到用户指定profile plugins根目录。

## 当前技术选择

- 独立DLL，保留原PeakWorkshop；HardDependency按 `com.peakcode.peakworkshop`，AccessTools按真实PeakMapForge类型/签名解析。
- 建议默认中文并提供独立开关；保留原auto/en/de、En/Code/Setting与数字格式，不扩展原Toggle，不向原cfg写zh。
- 固定词条只修改 IL 中明确的相邻德英字面量 Tr 调用，以审核后的固定英文词条精确查表；保留原调用的语言行为。变量内容不进入固定词表。动态按来源方法及德英参数一致规则处理；不全局替换 GUI/TMP 显示层。
- IMGUI用UnityEngine.Font，TMP用TMP_FontAsset；验证中文字形，限定Workshop所属UI，缓存与测量同步。
- 作者内容/ID/协议不翻译不写回。语言代码Tr("de","en")保留。显式语言GetText重载不随便覆写。
- 配置只处理原ConfigFile实例+section/key，保留Value/Tags/AcceptableValues；禁止ModConfig全局重注册。
- 配置说明通过限定 ConfigDescription 实例的 getter 后缀显示，不改 backing field 或主动保存原 cfg。ModConfig 显示名兼容可选，异常去重。
- 长度不是不翻译的依据；动态句子保留姓名/数量/修订等参数。8,192 字符上限及正则超时仅用于控制匹配成本，超限原文回退。
- 既有启动文本/缓存需单独恢复与刷新，不能重跑原Awake/Boot/静态构造。

## 禁止回退

- Boss 击败规则已修正为 DrawBosses；OnGUI没有Tr。审核必须验证真实调用来源，不能只靠模板测试通过。
- 4,979旧搜索匹配不是调用数量；本次源码节点/DLL指令均4,968。运行累加 hook 数与 1,084 静态字面量命中数均不能代表实机完整率。

- 禁止改原DLL或复制功能做分叉版。
- 禁止依据旧common/07的2.1.a记录覆盖用户指定2.6.a。
- 禁止称4979搜索匹配等于4979独立词条或全部UI已覆盖。
- 禁止把反编译/字体文件存在/计划设计称为实机验收。
- 已授权的试用实施已完成；不能把部分覆盖、构建或离线测试描述为完整实机验收。未请求发行打包上传。
