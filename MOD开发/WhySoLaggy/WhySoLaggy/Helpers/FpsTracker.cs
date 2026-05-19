using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace WhySoLaggy
{
    /// <summary>
    /// FPS 追踪器：持续监测帧率，检测卡顿尖峰，定期输出汇总。
    /// 1.0.3：增加 MemoryMonitor（每秒分配 KB 估算）+ 结构化事件输出。
    /// </summary>
    internal static class FpsTracker
    {
        // ── 配置（由 Plugin 设置）──
        public static int SpikeThresholdMs = 50;
        public static int ReportIntervalSeconds = 10;

        /// <summary>1.0.3：是否启用内存分配速率监测。</summary>
        public static bool EnableMemoryMonitor = true;

        // ── 当前帧状态 ──
        public static bool IsSpikeFrame { get; private set; }
        public static float CurrentFrameMs { get; private set; }

        // ── 滚动窗口 ──
        private const int WindowSize = 600; // ~10s at 60fps
        private static readonly float[] _frameTimes = new float[WindowSize];
        private static int _writeIndex;
        private static int _sampleCount;

        // ── 报告周期统计 ──
        private static float _periodTimer;
        private static int _periodFrames;
        private static float _periodSumMs;
        private static float _periodMinFps = float.MaxValue;
        private static float _periodMaxFps;
        private static int _periodSpikeCount;

        // ── 1.0.3：MemoryMonitor ──
        private static long _memLastSampleBytes;   // 上次采样的 GetTotalAllocatedMemoryLong
        private static float _memSampleTimer;      // 采样计时器
        private static float _memLastRateKBps;     // 最近一次计算出的分配速率（KB/s）
        private const float MemSampleInterval = 1f;// 每 1s 采样一次（差值 / 时间）

        // 1.0.3：复用
        private static readonly StringBuilder _reportSb = new StringBuilder(256);

        /// <summary>
        /// 每帧调用一次。
        /// </summary>
        public static void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            float frameMs = dt * 1000f;
            CurrentFrameMs = frameMs;

            // 写入滚动窗口
            _frameTimes[_writeIndex] = frameMs;
            _writeIndex = (_writeIndex + 1) % WindowSize;
            if (_sampleCount < WindowSize) _sampleCount++;

            // 尖峰检测
            IsSpikeFrame = frameMs > SpikeThresholdMs;
            if (IsSpikeFrame) _periodSpikeCount++;

            // 周期统计
            _periodFrames++;
            _periodSumMs += frameMs;
            float fps = dt > 0f ? 1f / dt : 0f;
            if (fps < _periodMinFps) _periodMinFps = fps;
            if (fps > _periodMaxFps) _periodMaxFps = fps;

            // 1.0.3：MemoryMonitor 采样
            if (EnableMemoryMonitor)
            {
                _memSampleTimer += dt;
                if (_memSampleTimer >= MemSampleInterval)
                {
                    try
                    {
                        long now = Profiler.GetTotalAllocatedMemoryLong();
                        if (_memLastSampleBytes > 0 && now >= _memLastSampleBytes)
                        {
                            long delta = now - _memLastSampleBytes;
                            _memLastRateKBps = (float)(delta / 1024.0 / _memSampleTimer);
                        }
                        _memLastSampleBytes = now;
                    }
                    catch (Exception ex)
                    {
                        WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] MemoryMonitor sample failed: {ex.Message}");
                        EnableMemoryMonitor = false; // 一次失败即关闭，避免刷屏
                    }
                    _memSampleTimer = 0f;
                }
            }

            // 定期报告
            _periodTimer += dt;
            if (_periodTimer >= ReportIntervalSeconds)
            {
                WriteReport();
                ResetPeriod();
            }
        }

        /// <summary>
        /// 获取启动以来所有帧时间的平均值（ms）。
        /// 注意：当前实现遍历 _frameTimes[0.._sampleCount-1]，在缓冲未满时计算的是"启动以来累计平均"，
        /// 缓冲写满后进入滚动覆盖，计算值为"最近 WindowSize 帧平均"。
        /// 若需要严格的"最近 N 帧"语义，请从 _writeIndex 向前环形遍历 N 帧重新实现。
        /// </summary>
        public static float GetWindowAvgMs()
        {
            if (_sampleCount == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < _sampleCount; i++)
                sum += _frameTimes[i];
            return sum / _sampleCount;
        }

        /// <summary>1.0.3：供 Dashboard 读取的最新分配速率 KB/s。</summary>
        public static float GetAllocRateKBps() => _memLastRateKBps;

        private static void WriteReport()
        {
            if (_periodFrames == 0) return;

            float avgFps = _periodSumMs > 0 ? _periodFrames / (_periodSumMs / 1000f) : 0f;
            float avgMs = _periodSumMs / _periodFrames;

            _reportSb.Clear();
            _reportSb.AppendLine($"[WHY_LAG] === FPS Report ({_periodTimer:F1}s, {_periodFrames} frames) ===");
            _reportSb.AppendLine($"[WHY_LAG] FPS: avg={avgFps:F1} | min={_periodMinFps:F1} | max={_periodMaxFps:F1} | avgFrame={avgMs:F1}ms");
            _reportSb.Append($"[WHY_LAG] Spikes(>{SpikeThresholdMs}ms): {_periodSpikeCount}");
            if (EnableMemoryMonitor)
                _reportSb.Append($" | AllocRate: {_memLastRateKBps:F1} KB/s");

            LagLogger.Write(_reportSb.ToString());

            // 1.0.3：结构化事件
            try
            {
                var fields = new Dictionary<string, object>
                {
                    { "AvgFps", Math.Round((double)avgFps, 1) },
                    { "MinFps", Math.Round((double)_periodMinFps, 1) },
                    { "MaxFps", Math.Round((double)_periodMaxFps, 1) },
                    { "AvgFrameMs", Math.Round((double)avgMs, 2) },
                    { "SpikeThresholdMs", SpikeThresholdMs },
                    { "SpikeCount", _periodSpikeCount },
                    { "ReportDuration", Math.Round((double)_periodTimer, 2) },
                };
                if (EnableMemoryMonitor)
                    fields["AllocRateKBps"] = Math.Round((double)_memLastRateKBps, 2);

                StructuredLogger.WriteEvent(new StructuredEvent
                {
                    Timestamp = StructuredLogger.NowStamp(),
                    FrameNumber = Time.frameCount,
                    Type = EventType.FpsReport,
                    Fields = fields,
                });
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] FpsTracker WriteEvent failed: {ex.Message}");
            }
        }

        private static void ResetPeriod()
        {
            _periodTimer = 0f;
            _periodFrames = 0;
            _periodSumMs = 0f;
            _periodMinFps = float.MaxValue;
            _periodMaxFps = 0f;
            _periodSpikeCount = 0;
        }
    }
}
