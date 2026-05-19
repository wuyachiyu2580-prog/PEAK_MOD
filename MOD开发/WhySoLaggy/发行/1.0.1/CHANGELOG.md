# Changelog

## [1.0.1] - 2026-04-20

- Major improvement to room-bombing detection: now accurately identifies which remote player is causing trouble
- Added real-time on-screen alert notifications (top-left corner, red text, auto-fades after 12s)
- Notifications automatically display in Chinese or English based on system language
- Fixed incorrect attribution that blamed all activity on the local player
- Fixed potential crash caused by thread-safety issues

## [1.0.0] - 2026-04-07

- Initial release
- Performance profiling: FPS tracker, plugin timing, Harmony patch method timing (tested)
- Room-bombing detection: Instantiate/Destroy/RPC flood monitoring, object spike detection (experimental)
- Dual log files: `WhySoLaggy.log` (performance) and `WhySoLaggy_Abuse.log` (abuse detection)

