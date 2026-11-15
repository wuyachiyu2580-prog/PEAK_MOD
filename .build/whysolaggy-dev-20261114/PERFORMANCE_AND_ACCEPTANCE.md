# Synthetic measurements and pending gameplay acceptance

2026-11-15. Build1.0.5-dev.20261115, .NET Framework4.7.2 MSTest process.
These are synthetic component measurements, not Unity/Mono, Harmony invocation cost or FPS improvements.
100000 calls/round; explicit warmup; three rounds; no forcedGC.
ManagedNetBytes is signed occupied-memory change, not an allocation counter.
Adaptive sampling did not establish an overhead advantage.

    TestContext 消息:
    Synthetic mode=exact round=1 calls=100000 samples=100000 elapsedMs=16.441 nsPerCall=164.4 gen0Delta=0 managedNetBytes=0; not in-game A/B
    Synthetic mode=exact round=2 calls=100000 samples=100000 elapsedMs=18.012 nsPerCall=180.1 gen0Delta=0 managedNetBytes=0; not in-game A/B
    Synthetic mode=exact round=3 calls=100000 samples=100000 elapsedMs=19.143 nsPerCall=191.4 gen0Delta=0 managedNetBytes=8192; not in-game A/B
    Synthetic mode=adaptive round=1 calls=100000 samples=10029 elapsedMs=17.933 nsPerCall=179.3 gen0Delta=0 managedNetBytes=0; not in-game A/B
    Synthetic mode=adaptive round=2 calls=100000 samples=10029 elapsedMs=16.418 nsPerCall=164.2 gen0Delta=0 managedNetBytes=0; not in-game A/B
    Synthetic mode=adaptive round=3 calls=100000 samples=10029 elapsedMs=17.945 nsPerCall=179.4 gen0Delta=0 managedNetBytes=0; not in-game A/B
    Synthetic RPC round=1 count=100000 elapsedMs=58.426; not network throughput
    Synthetic RPC round=2 count=100000 elapsedMs=53.037; not network throughput
    Synthetic RPC round=3 count=100000 elapsedMs=46.343; not network throughput

## Pending real performance matrix

Same machine/profile/scenario; warmup; three independent3-minute samples per mode.

| Mode | Round1 | Round2 | Round3 |
|---|---|---|---|
| Previous fixedDLL | NOT RUN | NOT RUN | NOT RUN |
| Automatic standby | NOT RUN | NOT RUN | NOT RUN |
| Plugin recording | NOT RUN | NOT RUN | NOT RUN |
| Targeted recording | NOT RUN | NOT RUN | NOT RUN |
| Manual full profiling | NOT RUN | NOT RUN | NOT RUN |

Collect mean/p95/p99 frame times,1%low, validGC counters, log growth, diagnostics drops, self-main distributions and memory/handle/file counts.
Normal multiplayer and burst pressure must be separate. At least30 minutes/four players, scene transition, leave/rejoin, repeated captures.
No game was launched; all real gameplay results remain pending.
