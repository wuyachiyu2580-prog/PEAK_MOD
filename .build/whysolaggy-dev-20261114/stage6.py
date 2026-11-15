# -*- coding: utf-8 -*-
from pathlib import Path
import re
root=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy'
p=root/'WhySoLaggy/Helpers/ModConfigLocalization.cs';s=p.read_text(encoding='utf-8')
entries=[('Capture','AutoCapture','Automatic Capture','自动卡顿录制','Automatically collect plugin timing and then verified patch targets after severe or repeated frame spikes.','严重或连续卡顿后自动采集插件耗时，再深入有证据的补丁目标。'),('General','AdaptiveTiming','Adaptive Sampling','自适应采样','Manual patch timing may sample at entry; automatic detailed recording remains exact.','手动补丁计时可在入口采样；自动详细录制保持精确。'),('General','CpuGpuMetrics','CPU/GPU Timings','CPU/GPU 帧耗时','Read supported CPU/GPU counters; unavailable counters stay unavailable.','读取受支持的 CPU/GPU 指标；不可用时明确显示不可用。'),('RpcMonitor','PumpBudgetMs','RPC Frame Budget','RPC 每帧预算','Soft RPC detail processing budget in milliseconds; one item cannot be interrupted.','RPC 明细处理的每帧软预算，单位毫秒；单条处理无法中断。'),('UI','FontSize','Font Size','字体大小','Dashboard font size.','诊断面板字体大小。'),('UI','Position','Window Position','窗口位置','Saved dashboard coordinates, clamped to the screen.','保存的诊断面板坐标，限制在屏幕内。'),('UI','DashboardKey','Dashboard Shortcut','面板快捷键','Toggle the dashboard outside text input and rebinding.','在文字输入和按键绑定之外切换诊断面板。'),('Capture','CaptureKey','Record Shortcut','录制快捷键','Start or stop a 30-second manual capture.','开始或停止一次 30 秒手动录制。'),('Capture','MarkerKey','Marker Shortcut','标记快捷键','Add a timestamped marker to the recording.','在记录中加入带时间的标记。')]
new='\n'.join('            Entry('+', '.join('"'+v+'"' for v in e)+'),' for e in entries)
s=s.replace('        private static readonly LocalizedConfigEntry[] ConfigEntries =\n        {','        private static readonly LocalizedConfigEntry[] ConfigEntries =\n        {\n'+new)
s=s.replace('                Token("General",','                Token("Capture", "Capture", "采集"),\n                Token("General",')
s=s.replace('entry => GetLocalizedConfigText(entry.Definition.Section, entry.Definition.Key, IsChinese)','entry => UiText.LocalizeChinese(GetLocalizedConfigText(entry.Definition.Section, entry.Definition.Key, IsChinese))')
s=s.replace('text => GetLocalizedToken(FindCanonicalToken(text), IsChinese)','text => UiText.LocalizeChinese(GetLocalizedToken(FindCanonicalToken(text), IsChinese))')
s=s.replace('SetDescription(entry, description);','SetDescription(entry, UiText.LocalizeChinese(description));')
s=s.replace('Normalize(token.Chinese), StringComparison.OrdinalIgnoreCase)', 'Normalize(token.Chinese), StringComparison.OrdinalIgnoreCase) ||\n                    string.Equals(normalized, Normalize(UiText.Traditional(token.Chinese)), StringComparison.OrdinalIgnoreCase)')
s=s.replace('Sample GC allocation rate and include AllocRateKBps in FpsReport events.','Monitor Unity/managed memory, signed net changes and supported GC counters; unavailable counters are not replaced with zeros.').replace('采样 GC 分配速率，并在 FpsReport 事件中记录 AllocRateKBps。','监测 Unity/托管内存、有符号净变化和受支持的 GC 指标；不可用指标不补零。')
s=s.replace('Low performance overhead (<0.5ms/s for 1000 RPCs/s).','Aggregate counts are independent of sampled detail records.').replace('性能开销较低（1000 RPC/秒时低于 0.5 毫秒/秒）。','总量统计独立于采样的详细记录。')
s=s.replace('Sample low-cost methods less often below this average time (ms); calls and frame timings remain counted. Applied when patch profiling is next enabled.','Threshold used only in Adaptive Sampling mode; sampled frame coverage is reported explicitly.').replace('平均耗时低于此毫秒阈值的方法减少累计耗时采样，调用次数和帧内耗时仍统计。下次开启补丁性能分析时生效。','仅用于自适应采样模式的平均耗时阈值；明确报告采样覆盖率。')
p.write_text(s,encoding='utf-8')
p=root/'WhySoLaggy/WhySoLaggyPlugin.cs';s=p.read_text(encoding='utf-8');s=s.replace('            return new ConfigEntryBase[]','            return new ConfigEntryBase[]')
# Include new entries in the existing local description-refresh list.
s=s.replace('                EnablePluginProfiling,','                AutoCapture, AdaptiveTiming, CpuGpuMetrics, RpcPumpBudget, DashboardFont, DashboardPosition, DashboardKey, CaptureKey, MarkerKey,\n                EnablePluginProfiling,')
p.write_text(s,encoding='utf-8')
print('Final English / Simplified / Traditional localization pass applied')
