using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace PeakWorkshopCN
{
    internal static class ConfigurationLocalization
    {
        private static readonly Dictionary<ConfigDescription, string> Descriptions = new Dictionary<ConfigDescription, string>();
        private static ConfigFile _config;
        private static bool _titleWarning;
        internal static int Count => Descriptions.Count;

        internal static void Install(Harmony harmony, ConfigFile config)
        {
            _config = config;
            foreach (var pair in config)
            {
                var entry = pair.Value;
                if (entry.Description == null || entry.Description == ConfigDescription.Empty) continue;
                if (ConfigTexts.TryGetValue(pair.Key.Section + "/" + pair.Key.Key, out var text)) Descriptions[entry.Description] = text;
            }
            harmony.Patch(AccessTools.PropertyGetter(typeof(ConfigDescription), "Description"),
                postfix: new HarmonyMethod(typeof(ConfigurationLocalization), nameof(DescriptionPostfix)));

            try
            {
                var modConfig = AccessTools.TypeByName("PEAKLib.ModConfig.Components.ModSettingsMenu");
                if (modConfig == null) return;
                var patched = new HashSet<MethodInfo>();
                foreach (var type in modConfig.Assembly.GetTypes())
                {
                    if (type.FullName == null || !type.FullName.StartsWith("PEAKLib.ModConfig.SettingOptions.BepInEx", StringComparison.Ordinal) ||
                        type.ContainsGenericParameters || type.IsAbstract) continue;
                    var method = AccessTools.Method(type, "GetDisplayName", Type.EmptyTypes);
                    if (method == null || !patched.Add(method)) continue;
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(ConfigurationLocalization), nameof(DisplayNamePostfix)));
                }
            }
            catch (Exception ex) { Plugin.Warn("Optional ModConfig name hooks: " + ex.Message); }
        }

        private static void DescriptionPostfix(ConfigDescription __instance, ref string __result)
        {
            if (Plugin.Chinese && Descriptions.TryGetValue(__instance, out var text)) __result = text;
        }

        private static void DisplayNamePostfix(object __instance, ref string __result)
        {
            if (!Plugin.Chinese) return;
            try
            {
                ConfigEntryBase entry = null;
                foreach (var contract in __instance.GetType().GetInterfaces())
                {
                    var property = contract.GetProperty("ConfigBase", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property != null && typeof(ConfigEntryBase).IsAssignableFrom(property.PropertyType))
                    { entry = property.GetValue(__instance, null) as ConfigEntryBase; break; }
                }
                if (entry == null) entry = AccessTools.Property(__instance.GetType(), "ConfigBase")?.GetValue(__instance, null) as ConfigEntryBase;
                if (entry?.ConfigFile != _config) return;
                if (ConfigNames.TryGetValue(entry.Definition.Section + "/" + entry.Definition.Key, out var text)) __result = text;
            }
            catch (Exception ex)
            {
                if (!_titleWarning) { _titleWarning = true; Plugin.Warn("Configuration title: " + ex.Message); }
            }
        }

        private static readonly Dictionary<string, string> ConfigTexts = new Dictionary<string, string>
        {
            ["General/Enabled"] = "总开关。关闭后游戏按原版方式运行。",
            ["General/Verbose"] = "输出详细构建、散布、采样和网络诊断日志。仅用于开发排错，会增加加载耗时。",
            ["Editor/Language"] = "原工坊界面语言：auto 跟随游戏，de 德文，en 英文。中文补丁独立生效；关闭补丁可查看原界面。",
            ["Editor/UiScale"] = "编辑器界面缩放。0 为按屏幕高度自动调整，1080p 对应 1。",
            ["Editor/ShowHints"] = "在设置下方显示说明文字。",
            ["Editor/PaletteGrid"] = "以网格卡片显示构件，关闭则使用列表。",
            ["Editor/ViewKey3D"] = "自由三维视图快捷键。使用 Unity Input System 按键名；None 表示不绑定。",
            ["Editor/ViewKeyTop"] = "俯视图快捷键。使用 Unity Input System 按键名；None 表示不绑定。",
            ["Editor/ViewKeyFront"] = "前视图快捷键。使用 Unity Input System 按键名；None 表示不绑定。",
            ["Editor/ViewKeyRight"] = "右视图快捷键。使用 Unity Input System 按键名；None 表示不绑定。",
            ["Generation/Seed"] = "地图、变体和物件随机选择前使用的随机种子。",
            ["Generation/RandomSeed"] = "每次开始时随机生成一个新种子。",
            ["Generation/Slot1"] = "第 1 个地形槽位，原版固定为 Shore（海岸）。",
            ["Generation/Slot2"] = "第 2 个地形槽位：Tropics（热带）或 Roots（根林）。",
            ["Generation/Slot3"] = "第 3 个地形槽位：Alpine（高山）或 Mesa（荒漠）。",
            ["Generation/Slot4"] = "第 4 个地形槽位：Volcano（火山）或 Swamp（沼泽）。",
            ["Probe/BuildProbeGeometry"] = "未加载地图时在起点旁生成测试山丘，仅用于开发验证。",
            ["Probe/RockCount"] = "在测试山丘上随机散布的游戏岩石数量。",
            ["Probe/OffsetX"] = "测试山丘中心相对出生点的 X 偏移，单位米。",
            ["Probe/OffsetZ"] = "测试山丘中心相对出生点的 Z 偏移，单位米；正 Z 为上山方向。",
            ["Map/File"] = "地图根文件夹（由 active.txt 选择地图），或含 map.json 的具体文件夹。留空或路径不存在时使用 BepInEx\\PeakWorkshop\\maps。",
            ["Map/LaunchMode"] = "起飞模式：vanilla 原版每日地图；play 按玩家模式游玩地图；edit 加载地图并打开编辑器。可在机场终端切换。",
            ["Export/AutoExport"] = "编辑飞行时自动生成摘要和玩法导出，供浏览器编辑器使用。默认关闭；F9 导出玩法，F10 完整导出。",
            ["Export/Directory"] = "JSON 导出目录，留空使用 BepInEx\\PeakWorkshop\\exports。",
            ["Heightfield/MinX"] = "浏览器编辑器参考高度场的最小 X 采样坐标。",
            ["Heightfield/MaxX"] = "高度场最大 X 采样坐标；原测量的可行走范围约为 X -325 至 320、Z -470 至 2370。",
            ["Heightfield/Step"] = "高度场采样间距，单位米。默认边界与 5 米间距约生成 160×600 个采样点。",
            ["Heightfield/SpikeAspect"] = "高度与宽度比达到此值的物件视作植被并跳过高度采样。0 关闭过滤。",
            ["Heightfield/AutoExport"] = "编辑飞行时自动导出基础地图及高度场等开发数据，会短暂开启所有区段并增加加载时间；也可按 F11 手动导出。",
            ["Bridge/Enabled"] = "启用仅限本机开发工具使用的 HTTP 桥接接口。默认关闭，重启游戏生效；浏览器来源请求会被拒绝。",
            ["Bridge/Port"] = "本地开发工具 HTTP 桥接端口；重启游戏后生效。",
            ["Load/SliceMs"] = "地图构建每帧计算时间（毫秒）。默认 33；绘制负担较高时可自动增至 50。较小更流畅，较大可能加载更快。",
            ["Load/WaitTimeoutSec"] = "房主构建完成后等待较慢队友的最长秒数。",
            ["Load/MaxHoldSeconds"] = "出生与加载画面的最长保护等待秒数（包含构建与等待）。超时后继续游戏。",
            ["Net/OnMissingMap"] = "客机地图缺失或与房主不一致时：leave 提示并退出至主菜单；vanilla 不构建自定义地图，游玩原始岛屿。不会改用其他地图。",
            ["Chat/FadeSeconds"] = "聊天消息淡出前保留的秒数。",
            ["Editor/FlyKey"] = "编辑器起飞模式中试玩时的穿墙飞行按键。WASD 移动，空格上升，Ctrl 下降，Shift 加速；None 关闭。",
            ["Menu/SignX"] = "主菜单工坊标牌中心的水平位置：0 为左侧，1 为右侧。游戏内按住 Ctrl 可拖动。",
            ["Menu/SignY"] = "主菜单工坊标牌中心的垂直位置：0 为底部，1 为顶部，链条长度随之调整。",
            ["Menu/SignMoveKey"] = "按住此键拖动标牌，同时右键可重置。LeftCtrl 也接受 RightCtrl；None 固定标牌。",
            ["Lobby/HotkeyHint"] = "处于 MOD 大厅时，在右上角显示 MOD 按键提示（F6 大厅、F7 队伍）。",
            ["Lobby/PanelKey"] = "打开大厅窗口的按键，在地图游玩中也可使用。房主可实时调整大厅设置；None 关闭。",
            ["Teams/LastConfig"] = "房主上次的队伍设置，JSON 格式与房间属性 pmf.tcfg 相同，每次修改后保存。建立在线房间时，若保存的设置包含队伍、自由混战或关闭友伤，则自动恢复；留空时每局按原版开始（合作、开启友伤）。",
            ["Teams/PvpHealthBars"] = "在 PvP（队伍或自由混战）中显示其他玩家的生命条，用于确认攻击是否命中。",
            ["Teams/ShowEnemyNames"] = "队伍模式中显示敌方玩家姓名。关闭后隐藏敌人姓名，队友姓名仍使用队伍颜色。",
            ["Teams/PanelKey"] = "机场和岛屿中的队伍概览按键，仅显示队伍、存活/总人数及占点进度。None 关闭；队伍设置位于终端“工坊 › 多人”。",
            ["Chat/Enabled"] = "在 MOD 房间中启用文字聊天，使用 Photon，无独立聊天服务器。",
            ["Chat/OpenKey"] = "打开聊天的按键。Enter 发送，Esc 关闭。",
            ["Net/Diagnose"] = "将加入、房主切换、踢出、断线、Photon 错误及停滞诊断写入 BepInEx\\PeakMapForge_net.log；重要内容也写入 BepInEx 日志，前缀为 [Forge/Net]。排查被踢出问题时需同时提供此文件和 Player.log。",
            ["Net/NetDebug"] = "提高 Photon 日志级别（PUN Informational、Peer WARNING）并记录每个 Photon 事件。仅用于排错，会产生大量日志。",
            ["Net/LobbySchutz"] = "大厅保护：原房主仍在房间时，忽略其他人的踢人请求；若其他人夺取 Master，原房主会将其收回。仅对安装此 MOD 的电脑生效。",
            ["Net/FremdeKicken"] = "房主在 5 秒后自动踢出没有 SteamID64 且没有主机平台标识的玩家。关闭时仅警告。",
            ["Load/Sliced"] = "分帧构建地图，保持加载画面更新和 Photon 连接，Alt+Tab 不会暂停构建。关闭后一次性构建，作为应急开关；两种方式构建的世界相同。",
            ["Load/HideWorld"] = "加载画面完全遮住世界时，暂停摄像机绘制以节省每帧耗时，尤其适用于较弱的电脑。构建后恢复。",
            ["Load/FreezeSearch"] = "对没有匹配放置缓存的散布区域分帧搜索。在区域之间更新画面，同时暂停游戏脚本、动画及物理；世界结果不变。关闭时连续搜索，画面会暂时停住。",
            ["Load/StartTogether"] = "多人同步开始：房主构建完成后暂缓出生，等待安装 PeakMapForge 的队友完成，最长等待 WaitTimeoutSec。地图可通过 startTogether=false 关闭；单人不等待。",
            ["Load/ShowStatus"] = "加载画面显示构建阶段、进度、剩余时间及队友状态，飞机也反映实际进度。关闭时仅显示原版加载画面，不影响地图。",
            ["Load/SliceInsideAreas"] = "分帧构建时，在散布区域内每放置一个物件后让出一帧，区域的装饰、材质及校验步骤保持连续。关闭时每个区域连续构建，仅在区域之间让出一帧。两种方式的地图和散布校验值应相同；仅在 Load/Sliced 开启时生效。",
            ["Cache/ScatterCache"] = "放置缓存：地图、环境及顺序相同时，回放已有散布记录并比较区域校验值；关闭后始终重新搜索。开发构建只回放相同 MOD 版本的记录，更新后的首次起飞会重新搜索比较。玩家构建和多人起飞（房主及客机）始终回放附带记录，以保持世界一致；对照测试时可关闭。",
            ["Cache/VerifyCache"] = "校验模式：重新搜索每个区域，与记录及回放结果比较，速度较慢，仅用于检查。不一致的记录会被丢弃。",
            ["Online/Enabled"] = "启用在线工坊、大厅浏览和地图下载，服务器为 peakmapforge-api。",
            ["Online/BaseUrl"] = "API 基础地址；留空使用 https://peakmapforge.aicoded.de/forge/v1。",
            ["Online/ClientKey"] = "公开客户端密钥（X-Forge-Key）；留空使用内置默认值。",
            ["Online/CacheMB"] = "下载地图和预览图的缓存大小上限，单位 MB。",
            ["Online/NetMapsKeep"] = "在本地保留的最近使用在线地图数量。",
            ["Online/OfferDownscale"] = "上传时提供缩小超过 2048 像素纹理的选项，仅修改上传副本。",
            ["Online/TimeoutSeconds"] = "API 请求超时秒数；文件传输只有连续停滞达到此时长才中断，传输时最低为 30 秒。"
        };

        private static readonly Dictionary<string, string> ConfigNames = new Dictionary<string, string>
        {
            ["General/Enabled"] = "启用工坊", ["General/Verbose"] = "详细日志",
            ["Editor/Language"] = "原界面语言", ["Editor/UiScale"] = "界面缩放",
            ["Editor/ShowHints"] = "显示说明", ["Editor/PaletteGrid"] = "构件网格",
            ["Editor/ViewKey3D"] = "三维视图按键", ["Editor/ViewKeyTop"] = "俯视图按键",
            ["Editor/ViewKeyFront"] = "前视图按键", ["Editor/ViewKeyRight"] = "右视图按键", ["Editor/FlyKey"] = "试玩飞行按键",
            ["Generation/Seed"] = "随机种子", ["Generation/RandomSeed"] = "每次随机种子",
            ["Generation/Slot1"] = "地形槽位 1", ["Generation/Slot2"] = "地形槽位 2", ["Generation/Slot3"] = "地形槽位 3", ["Generation/Slot4"] = "地形槽位 4",
            ["Probe/BuildProbeGeometry"] = "生成测试山丘", ["Probe/RockCount"] = "测试岩石数量", ["Probe/OffsetX"] = "测试 X 偏移", ["Probe/OffsetZ"] = "测试 Z 偏移",
            ["Map/File"] = "地图路径", ["Map/LaunchMode"] = "起飞模式",
            ["Export/AutoExport"] = "自动导出", ["Export/Directory"] = "导出目录",
            ["Heightfield/MinX"] = "最小 X", ["Heightfield/MaxX"] = "最大 X", ["Heightfield/MinZ"] = "最小 Z", ["Heightfield/MaxZ"] = "最大 Z",
            ["Heightfield/Step"] = "采样间距", ["Heightfield/SpikeAspect"] = "植被过滤比例", ["Heightfield/AutoExport"] = "自动导出高度场",
            ["Bridge/Enabled"] = "本地开发桥接", ["Bridge/Port"] = "桥接端口",
            ["Load/SliceMs"] = "每帧构建预算", ["Load/WaitTimeoutSec"] = "等待队友时限", ["Load/MaxHoldSeconds"] = "加载保护时限",
            ["Load/Sliced"] = "分帧构建", ["Load/HideWorld"] = "加载时暂停绘制世界", ["Load/FreezeSearch"] = "分帧散布搜索",
            ["Load/StartTogether"] = "同步开始", ["Load/ShowStatus"] = "加载状态", ["Load/SliceInsideAreas"] = "区域内分帧",
            ["Cache/ScatterCache"] = "散布缓存", ["Cache/VerifyCache"] = "校验缓存",
            ["Online/Enabled"] = "启用在线工坊", ["Online/BaseUrl"] = "API 地址", ["Online/ClientKey"] = "客户端密钥",
            ["Online/CacheMB"] = "缓存上限 MB", ["Online/NetMapsKeep"] = "保留在线地图数", ["Online/OfferDownscale"] = "上传纹理缩小选项", ["Online/TimeoutSeconds"] = "请求超时秒数",
            ["Net/OnMissingMap"] = "缺失地图处理", ["Net/Diagnose"] = "网络诊断", ["Net/NetDebug"] = "Photon 详细日志",
            ["Net/LobbySchutz"] = "大厅保护", ["Net/FremdeKicken"] = "踢出无平台标识玩家",
            ["Chat/Enabled"] = "启用聊天", ["Chat/OpenKey"] = "聊天按键", ["Chat/FadeSeconds"] = "消息淡出秒数",
            ["Lobby/HotkeyHint"] = "大厅按键提示", ["Lobby/PanelKey"] = "大厅窗口按键",
            ["Teams/LastConfig"] = "上次队伍设置", ["Teams/PvpHealthBars"] = "PvP 生命条", ["Teams/ShowEnemyNames"] = "敌人姓名", ["Teams/PanelKey"] = "队伍窗口按键",
            ["Menu/SignX"] = "标牌水平位置", ["Menu/SignY"] = "标牌垂直位置", ["Menu/SignMoveKey"] = "标牌拖动按键"
        };
    }
}
