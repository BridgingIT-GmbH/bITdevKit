# Application caching measurements

These exploratory measurements were executed on 2026-11-10. They establish completed work on the recorded local profiles. They do not establish universal performance or qualify the unexecuted matrix.

## Acceptance scope

On 2026-11-11, the user deferred the remaining scale and non-scale benchmark executions, plus external shared-filesystem, cross-host, reconnect and power-loss qualification. They are not prerequisites for current implementation acceptance. The measured cohorts and unqualified profiles below retain their original evidence and limitations. Deferred work supplies no passing result or additional deployment guarantee.

## Environment and method

- AMD Ryzen 9 9900X, 12 cores / 24 logical processors, approximately 64 GiB RAM; Windows 11 Pro build 26300, local NTFS temporary storage.
- SDK 10.0.403, .NET 10.0.14, x64 RyuJIT, concurrent workstation GC. The captured power plan was High performance; initial whole-machine CPU load was 8%.
- SQL Server `2022-latest` and PostgreSQL `16-alpine` used disposable local Linux containers. The environment manifest `artifacts/environment.json` records their image IDs, Docker storage driver, hardware, initial revision, dirty documentation/test paths and binary SHA-256 hashes. Execution snapshots exclude connection/root settings and credentials.
- Common and the eight initial provider-hit cases used revision `e9b7c30a6`. The visible data-set selector was committed as `4671a7e3b`; the HTTP harness expectation correction was committed as `26f41e2aa`. Runtime source stayed frozen. These are mixed recorded cohorts, not one clean rebuilt release at a single revision.
- BenchmarkDotNet 0.15.8 used its in-process emitter, two warm-ups, five measured iterations, a 100-ms target and unroll factor one. The bounded native run used one warm-up and **one invocation per iteration** because a maintenance unit consumes approximately its configured five-second budget.

BDN minimum/median/maximum values below describe normalized iteration samples, **not individual-request p50/p95/p99**. HTTP percentiles come from actual complete request-duration samples. BDN allocations are inclusive managed benchmark allocations, not a cache-only overhead subtraction. On this runtime, [BenchmarkDotNet 0.15.8 allocation accounting](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/GcStats.cs) uses process-wide `GC.GetTotalAllocatedBytes(precise: true)`, including async worker threads. HTTP allocation uses `GC.GetTotalAllocatedBytes` across the complete client/server process.

BDN's default outlier policy applies to its summary statistics; full JSON retains the actual sample count and measurements. Five requested measured iterations do not imply that every summary retained five samples.

Every workload checked completed work. Source bursts asserted one source per invocation and complete results for every caller. Binary workloads copied through EOF, awaited validated completion and disposed the reader. HTTP checked every byte and ran authorization, opt-out, ETag/304 and stale-write/412 gates before timing.

## Later execution checks

The later overlapping-batch, jittered-expiry and foreground-refresh runners passed 12 InMemory/SQLite dry cases after a clean Release build. They asserted complete work, zero remaining transient/native-tail charges, and the explicit 512-entry/512-MiB storage bounds. These dry cases are separate from the 60 timed BDN cases below and supply no performance result.

The full filesystem caching suite passed 133 cases on Windows NTFS under the operating-system temporary directory, and 133 on Linux using `/tmp/bdk-cache-local`. Linux ran in the local `mcr.microsoft.com/dotnet/sdk:10.0.403` Ubuntu 24.04 container, kernel `5.15.167.4-microsoft-standard-WSL2`, with the container's `overlay` filesystem. Source, build output, and test data were copied into isolated Linux `/tmp` directories. Both suites include independent local process ownership and production-process crash/recovery checks. This qualifies those local test profiles; it does not establish native ext4/XFS, independent-host, network-share or power-loss behavior. The first Linux snapshot omitted the solution marker required by CLI tests; including the actual solution file fixed the fixture and the complete rerun passed without runtime changes. Test logs/TRX remain local and ignored.

## 2026-11-11 execution and I/O checks

The expanded workload sources are committed as `4f0773a3c`. The successful-unretained typed handoff correction is `cc9304f01`; native multiprocess coverage is `214fd69b8`. The later dry cohort executed 64 workloads, with 63 clean lifecycle completions and one diagnostics-disabled cleanup failure: 16 source-outcome controls, 12 SQLite policy scenarios, eight stored/source-only multi-instance cases, two successful-unretained multi-instance handoffs, 16 hot-retention cases, four binary representations, four native/engine read baselines, and two larger typed payloads. These are workload execution checks, not additional timed results. The original tally missed the cleanup exception because every workload produced a numeric row. The optional hot-footprint access is corrected in `ade038e2b`; subsequent runs record the repaired lifecycle separately.

The final filesystem caching suite passed 142 cases on Windows NTFS temporary storage and 142 on Linux `/tmp` on the local container's `overlay` filesystem. Each suite includes 13 independent-process cases covering cold source scopes and the additional authority-outage, killed-owner, strict-hot, tracked-operation, simultaneous-clear, pinned-reader/quota, caller-departure and typed/binary handoff scenarios. These results qualify the stated local profiles only.

After consolidation into `Infrastructure.UnitTests/FileSystem/Caching`, the full existing Windows infrastructure unit suite passes 1,022 tests with 13 explicit skips. All 214 caching cases pass on both Windows and Linux, including the same 142 native cases. The first consolidated Linux archive omitted an existing transitive test-project dependency; the complete snapshot resolves all 50 project references and its isolated build/test phases pass. These are source-snapshot and test-project changes, with no cache runtime change.

An isolated Linux syscall diagnostic used SDK 10.0.403, Ubuntu 24.04, WSL2 kernel `5.15.167.4-microsoft-standard-WSL2`, container `overlay`, and Durable acknowledgement. It initialized one native store, stored one 256-character typed value, then completed 20 nonobserving metadata-only lookups and 20 full typed reads. All cache results and returned values were checked. The filesystem provider source matches `27bc8185c`; the checkout also contained subsequent handoff and benchmark edits, which the diagnostic did not exercise. This is a separate diagnostic cohort, not an untraced latency comparison.

| Profile phase | Successful opens | Successful fsync calls | Returned read bytes | Returned written bytes |
| --- | ---: | ---: | ---: | ---: |
| Initialization and one stored value | 577 | 263 | 3,260,576 | 1,633,797 |
| 20 metadata-only lookups | 300 | 0 | 678,616 | 0 |
| 20 complete typed reads | 2,501 | 562 | 13,764,560 | 3,515,676 |
| Total | 3,378 | 825 | 17,703,752 | 5,149,473 |

Counts attribute syscall paths and annotated file descriptors to the owned cache root. Open totals include 462 directory opens; fsync totals include 440 directory flushes. Read/write bytes are actual successful return values, not requested lengths. No attributed open, read, write or flush failed. Existence probes returned expected `ENOENT` and atomic-publication checks returned one expected `EEXIST`; those are not failed cache operations. The parser reconstructed split syscalls across threads and found no unresolved provider-root call.

The root ledger accounts for 93.95 percent of returned read bytes, 94.51 percent of returned write bytes and 82.42 percent of fsync calls. Full reads register and release durable reader pins and quota credits, so their native work includes writes. The trace establishes syscall activity, not physical-device traffic, CPU saturation, a latency bottleneck, or power-loss survival. Windows WPR collection was denied before recording started; no Windows trace is claimed.

Raw trace, parser, checked summary and launch logs are local under `.tmp/linux-native-fileio*`, `.tmp/parse-native-strace.py` and `.tmp/run-linux-native-profile.sh`. The trace SHA256 is `bf84ca850bc7816c0cb0e08ef364727abe5412fe34d402677d5c7f96b3d497b2`. Native collection used `strace -f -yy -e trace=%file,read,write,pread64,pwrite64,fsync,fdatasync`; profiling overhead is excluded from performance conclusions.

## 2026-11-11 timed local cohorts

The first seven fresh cohorts used source `40ec996ba971e083b33d6f509457b25c24c274ef` and a clean prebuilt Release executable. Windows 11 Pro build 26300 ran on an AMD Ryzen 9 9900X with 12 cores, 24 logical processors and 61.6 GiB visible RAM. Native data used NTFS under the OS temporary directory; SQLite used a local WAL/FULL database. SDK 10.0.403 hosted .NET 10.0.14. The benchmark assembly SHA256 is `52A9E994D9C1B69BD4078823F0B8DF8AD0E9958A8F5EA738574CC8BA9CE7F051`; `cohort.json` also records the cache assembly identity.

Each case requested two warm-ups and five measured iterations with invocation/unroll count one and an explicit 1,200-second in-process whole-case budget. All 30 workloads produced measurements and passed their workload assertions. 29 completed cleanup normally. DiagnosticsMinimal failed cleanup when optional diagnostics exposed no hot footprint; its original row is retained as a failed lifecycle. The correction and follow-up cohorts are reported separately. A launcher environment-variable error occurred during discovery before any cache workload and is recorded separately from cache failures.

The tables use BenchmarkDotNet's retained normalized iteration statistics. Its default outlier policy retained 142 of 150 measured samples. Min-max ranges and N describe that retained distribution; these five-iteration statistics are not individual-request p50/p95/p99. The SQLite instance profiles use two/four independent service providers in one process over one local database. Independent OS-process semantics also have separate functional tests; these timings establish the stated local application-host profile.

### Completion ledger

| Cohort | Rows with measurements | Clean lifecycle | Elapsed wall time |
| --- | --- | --- | --- |
| native-1000 | 1 | 1 | 322.1 s |
| native-stream-64m | 4 | 4 | 21.9 s |
| native-stream-1g | 1 | 1 | 55.1 s |
| sqlite-stream-64m | 1 | 1 | 7.3 s |
| sqlite-stream-1g | 1 | 1 | 77.6 s |
| sqlite-policies | 12 | 11 | 111.8 s |
| sqlite-multi | 10 | 10 | 55.4 s |

### Complete logical transfers

| Provider | Logical size | Requested source/transform | Median ms | Min-max ms | Allocated B/op | N | GC0/1/2 per diagnostic op | Quota CurrentBytes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| FileSystem | 64 MiB | GZipRandom | 279.119 | 273.482-282.330 | 5,992,000 | 4 | 0/0/0 | 67,146,991 |
| FileSystem | 64 MiB | GZipZeros | 114.530 | 105.429-115.345 | 4,482,416 | 5 | 0/0/0 | 66,546 |
| FileSystem | 64 MiB | PlainRandom | 269.132 | 263.110-270.149 | 3,660,584 | 4 | 0/0/0 | 67,146,991 |
| FileSystem | 64 MiB | PlainZeros | 268.149 | 262.225-270.628 | 3,670,032 | 5 | 0/0/0 | 67,146,991 |
| FileSystem | 1024 MiB | PlainRandom | 3,512.995 | 3,457.759-3,563.187 | 38,996,312 | 5 | 2/1/0 | 1,074,332,915 |
| Sqlite | 64 MiB | PlainRandom | 400.056 | 377.220-410.629 | 73,123,608 | 5 | 10/10/10 | 67,146,991 |
| Sqlite | 1024 MiB | PlainRandom | 5,616.695 | 5,499.611-5,909.026 | 1,162,164,368 | 5 | 175/170/170 | 1,074,332,915 |

### SQLite traffic and policies

| Scenario | Median ms | Min-max ms | Allocated B/op | N | CV | SQL window | Checked work per invocation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| BreakerCycle | 3.916 | 3.455-4.697 | 137,784 | 5 | 12.0% | 38 | 7 control outcomes; 5 injected quota failures, open rejection, recovery probe/read |
| Clear | 785.306 | 773.282-786.726 | 37,043,328 | 4 | 0.8% | 14,195 | Seed32, then fixed-cutoff clear32 |
| DiagnosticsDetailed | 2.676 | 2.061-3.124 | 144,088 | 5 | 15.1% | 20 | 1 hit; metrics/profiling/history/log sink |
| DiagnosticsMinimal | 2.066 | 1.858-2.618 | 134,888 | 5 | 14.2% | 18 before failed cleanup; final unavailable | 1 hit; optional observations disabled; cleanup failed |
| DiagnosticsStandard | 2.274 | 2.180-2.975 | 143,456 | 5 | 14.3% | 20 | 1 hit; metrics and active profiling |
| DistinctCold | 2,456.725 | 2,294.634-2,631.791 | 47,631,576 | 5 | 5.6% | 18,338 | 32 distinct sources and stored caller values |
| EvictionPressure | 729.927 | 712.160-789.890 | 41,414,560 | 5 | 4.1% | 16,751 | Seed32, add key/replacement, entry cap and acquired-value integrity |
| ExpiredCleanup | 647.552 | 611.922-754.716 | 37,987,248 | 5 | 9.6% | 16,465 | 32 expirations, logical miss, bounded current-entry retirement |
| MixedTraffic | 776.742 | 769.126-807.922 | 47,861,488 | 5 | 2.2% | 18,730 | Seed32, then24 reads +8 stored replacements |
| PartialBatch | 82.393 | 72.359-105.445 | 5,129,808 | 5 | 15.5% | 1,991 | 4 ordered items:3 positive,1 declared source failure |
| SelectionInvalidate | 628.280 | 606.428-629.545 | 37,484,800 | 4 | 1.8% | 14,137 | 32 metadata preview records, invalidate32 versions |
| TrackedInvalidation | 5,078.780 | 4,995.881-5,158.487 | 297,780,624 | 4 | 1.8% | 113,752 | Provider-owned tracked work,260 affected versions |

### Central SQLite application instances

| Hosts | Mode | Median ms | Min-max ms | N | Verified sources/invocation | Observed cache source attempts | Observed joins | Observed cache read successes | SQL window |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 2 | Independent | 1,949.2130 | 1,800.583-2,049.377 | 5 | 32 | 288 | 0 | 288 | 7,339 |
| 2 | NodeLocal | 110.5949 | 94.580-125.284 | 5 | 2 | 18 | 270 | 288 | 1,407 |
| 2 | SharedStore | 185.0372 | 127.942-189.661 | 4 | 1 | 9 | 279 | 288 | 1,527 |
| 2 | SourceOnly | 0.0467 | 0.039-0.075 | 5 | 32 | 0 | 0 | 0 | 4 |
| 2 | UncachedHandoff | 121.1852 | 112.859-126.206 | 4 | 2 | 18 | 279 | 288 | 1,880 |
| 4 | Independent | 2,466.3069 | 1,477.400-2,808.329 | 5 | 32 | 288 | 0 | 288 | 7,399 |
| 4 | NodeLocal | 137.0078 | 123.267-153.461 | 5 | 4 | 36 | 252 | 288 | 1,815 |
| 4 | SharedStore | 200.1788 | 109.973-214.826 | 5 | 1 | 9 | 279 | 288 | 1,917 |
| 4 | SourceOnly | 0.0401 | 0.039-0.043 | 4 | 32 | 0 | 0 | 0 | 8 |
| 4 | UncachedHandoff | 145.4462 | 114.302-197.844 | 5 | 2 | 18 | 282 | 288 | 2,516 |

Native 1,000 hit: median 181.477 ms, min 124.075 ms/max 191.621 ms, CV 19.90%, 4,807,320 B allocated/op; repeat required. 142 of 150 measured samples were retained overall. All seven stream cases have 9 validated transfers each, 63 in the wider counter window and 35 in measured regions, with zero failed/abandoned transfers. Stored quotas include the typed seed and framing. GZipZeros shows actual retained-size reduction; GZipRandom's plain-equivalent quota is consistent with raw fallback, but actual per-entry compression metadata was not emitted.

SQL windows include jitting, warm-up, memory diagnostics and cleanup inspection, including PRAGMAs; they are not measured-only or per-call counts. Multi-instance windows cover nine scenarios. SourceOnly's callback count is asserted, while its engine counters are zero. Competing Stored/Conflict outcome counts were not emitted. Handoff asserts 16 local unretained results and 16 remote confirmed values for two hosts, or eight local unretained results and 24 remote confirmed values for four hosts. A remote value can come from its node's Stored completion or a confirmed storage recheck with Hit/None and an acquired version; those are distinct outcomes.

All available stream snapshots have zero transient charge/native tails. Across 49 available snapshots in the initial fresh cohort, seven SQLite snapshots observe one native tail, with four retaining 2 MiB transient charge before host disposal. No post-disposal zero snapshot exists. The native quota's roughly 512 MiB or higher staging figure includes conservative control-workspace reservation, not transient memory. Actual stored capacity is 10 GiB; resident/transient caps are 512/256 MiB per application host. GC figures are one MemoryDiagnoser operation, not all timing samples. Peak memory was not sampled, and diagnostic latency windows include setup.

The native 1,000-entry case, most multi-instance timings, PartialBatch, diagnostics, breaker, expiration and distinct cold traffic show substantial variation. Follow-up runs are recorded separately below. Full raw sample ranges and CVs are in `summary.json`; no comparison or limiting-resource claim is justified. Preserve the pre-workload launcher failure and the DiagnosticsMinimal cleanup failure separately from cache-result failures. Raw JSON, console logs, per-case observations and cohort identities are local under `.tmp/caching-final-timed`.

### Repeated and repaired cohorts

The follow-up build uses source `a7adc062ef3a8433483cc7615ae045a9575f7a79`. Between these cohorts, test files moved into the existing unit-test project and the benchmark cleanup learned to handle an absent optional hot footprint. Production cache source is unchanged. The follow-up benchmark assembly SHA256 is `6F1E869AA4AF46B90560750621603A3B597E6C7DCFD8CE5CF22C5698A66A0D3F`. The same finite limits and two-warm-up/five-iteration invocation-one controls apply.

All 18 follow-up workloads completed with clean lifecycle logs and complete observation records. The seven policy cases include the repaired DiagnosticsMinimal profile. The ten multi-instance cases preserve the asserted source counts and all 32 caller results per invocation. Across the two fresh builds there are 48 case executions for 30 distinct profiles: 47 clean lifecycle completions and the retained original diagnostics cleanup failure. All 30 profiles now have a clean completed cohort. The 240 requested measured samples yield 232 retained samples under the default outlier policy.

| Repeated workload | Parameters | Median ms | Retained min-max ms | N | CV | Allocated B/op |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Native typed hit | Provider=FileSystem; Policy=Plain; Entries=1000; PayloadBytes=256 | 181.150 | 115.339-219.916 | 5 | 24.24% | 4,799,744 |
| SQLite policy | Provider=Sqlite; Scenario=BreakerCycle | 4.252 | 4.157-4.444 | 5 | 2.73% | 137,784 |
| SQLite policy | Provider=Sqlite; Scenario=DiagnosticsDetailed | 3.013 | 2.337-3.487 | 5 | 15.36% | 145,288 |
| SQLite policy | Provider=Sqlite; Scenario=DiagnosticsMinimal | 2.833 | 2.438-3.239 | 5 | 10.46% | 136,136 |
| SQLite policy | Provider=Sqlite; Scenario=DiagnosticsStandard | 3.315 | 2.870-3.668 | 5 | 10.52% | 144,712 |
| SQLite policy | Provider=Sqlite; Scenario=DistinctCold | 2,276.592 | 1,985.167-2,568.681 | 5 | 10.90% | 47,762,176 |
| SQLite policy | Provider=Sqlite; Scenario=ExpiredCleanup | 622.366 | 606.441-646.041 | 5 | 2.79% | 38,032,456 |
| SQLite policy | Provider=Sqlite; Scenario=PartialBatch | 91.508 | 82.664-99.667 | 5 | 9.30% | 5,146,784 |
| SQLite instances | Instances=2; Provider=Sqlite; Mode=Independent | 1,959.133 | 1,816.846-2,136.451 | 5 | 6.94% | 18,169,176 |
| SQLite instances | Instances=2; Provider=Sqlite; Mode=NodeLocal | 115.592 | 89.638-135.993 | 5 | 17.45% | 5,447,192 |
| SQLite instances | Instances=2; Provider=Sqlite; Mode=SharedStore | 190.819 | 187.171-206.163 | 5 | 4.77% | 5,943,944 |
| SQLite instances | Instances=2; Provider=Sqlite; Mode=SourceOnly | 0.063 | 0.039-0.085 | 5 | 30.47% | 43,456 |
| SQLite instances | Instances=2; Provider=Sqlite; Mode=UncachedHandoff | 135.032 | 116.164-219.274 | 5 | 30.59% | 6,615,344 |
| SQLite instances | Instances=4; Provider=Sqlite; Mode=Independent | 2,097.394 | 1,041.454-2,785.194 | 5 | 31.66% | 17,963,216 |
| SQLite instances | Instances=4; Provider=Sqlite; Mode=NodeLocal | 139.737 | 122.446-154.949 | 5 | 8.55% | 7,232,624 |
| SQLite instances | Instances=4; Provider=Sqlite; Mode=SharedStore | 216.209 | 185.465-219.608 | 5 | 7.12% | 7,123,928 |
| SQLite instances | Instances=4; Provider=Sqlite; Mode=SourceOnly | 0.052 | 0.051-0.070 | 5 | 14.91% | 43,456 |
| SQLite instances | Instances=4; Provider=Sqlite; Mode=UncachedHandoff | 154.461 | 139.228-278.174 | 5 | 31.20% | 9,039,856 |

The native 1,000-entry medians are 181.477 ms and 181.150 ms, but each run has broad variation: CV 19.90 percent and 24.24 percent. Similar medians do not qualify a stable latency bound or a provider performance comparison. Several policy and multi-instance repeats also vary substantially; the tables characterize these local runs without an improvement/regression claim. The original diagnostics cleanup failure remains visible even though its corrected profile now completes.

Follow-up output is local in `native-1000-repeat`, `sqlite-policies-repeat` and `sqlite-multi-repeat` under `.tmp/caching-final-timed`. `repeat-cohort.json` records identities; each folder contains the exact profile selectors, runner log and full BDN reports. Resource snapshots occur before host disposal and do not assert that all asynchronous cleanup has already ended.

### Fresh cohort commands

The shared options were `--warmupCount 2 --iterationCount 5 --iterationTime 100 --invocationCount 1 --unrollFactor 1 --exporters json csv --allStats`, with `BDK_CACHE_BENCH_TIMEOUT_SECONDS=1200`. The explicit bounded in-process toolchain replaces the ordinary `--inProcess` flag. Use the prebuilt `benchmarks/Application.Benchmarks/bin/Release/net10.0/Application.Benchmarks.dll`, an explicit `--filter` below and a fresh ignored `--artifacts` directory. Remove unused selectors before switching profiles.

| Cohort | Method filter | Additional selectors |
| --- | --- | --- |
| Native baseline and repeat | `*CacheProviderBenchmarks.GetHitAsync*` | `PROVIDERS=FileSystem`, `ENTRIES=1000`, `TYPED_BYTES=256`, `POLICIES=Plain` |
| Native 64 MiB streams | `*CacheStreamingBenchmarks.CopyValidatedAsync*` | `PROVIDERS=FileSystem`, `STREAM_BYTES=67108864`, `STREAM_REPRESENTATIONS=PlainZeros,PlainRandom,GZipZeros,GZipRandom` |
| Native one GiB stream | Same stream filter | `PROVIDERS=FileSystem`, `STREAM_BYTES=1073741824`, `STREAM_REPRESENTATIONS=PlainRandom` |
| SQLite 64 MiB / one GiB streams | Same stream filter | `PROVIDERS=Sqlite`, `STREAM_BYTES=67108864` or `1073741824`, `STREAM_REPRESENTATIONS=PlainRandom` |
| All SQLite policies | `*CachePolicyBenchmarks.RunAsync*` | `PROVIDERS=Sqlite`, default complete policy-case list |
| Repeated SQLite policies | Same policy filter | `PROVIDERS=Sqlite`, `POLICY_CASES=BreakerCycle,DiagnosticsMinimal,DiagnosticsStandard,DiagnosticsDetailed,DistinctCold,PartialBatch,ExpiredCleanup` |
| SQLite scopes and repeat | `*CacheMultiInstanceBenchmarks.ColdBurstAsync*` | `PROVIDERS=Sqlite`, `LOAD_MODES=SharedStore,NodeLocal,Independent,SourceOnly,UncachedHandoff` |

Every abbreviated selector name in this table has the `BDK_CACHE_BENCH_` prefix. The instance runner selects both two and four hosts. Actual capacity was 10 GiB per persistent store/collection, with 512/256 MiB resident/transient limits per application host; a four-GiB capacity was proposed but was not used. Stream cases explicitly raised encoded/logical entry limits to their selected size. Effective policies and clocks are emitted in the raw observation records.

## Completed historical cohorts and local output

Generated benchmark and test output is ignored by Git. The paths below identify local CSV, JSON, log and manifest files relative to this report; they are not repository links. The checked-in report records the observed summaries and commands.

| Cohort | Completed cases | Work and data set | Local raw output |
| --- | ---: | --- | --- |
| Common codecs | 15, then the same 15 repeated | 256 B / 4 KiB / 64 KiB; materialization, forward encode/decode, serializer adapter, pre-cancelled rejection | First CSV `../../Common.Benchmarks/codec-artifacts/results/BridgingIT.DevKit.Common.Benchmarks.ValueCodecBenchmarks-report.csv`, repeat CSV `../../Common.Benchmarks/codec-repeat-artifacts/results/BridgingIT.DevKit.Common.Benchmarks.ValueCodecBenchmarks-report.csv` |
| Typed positive hits | 8 | InMemory / SQLite / PostgreSQL / SQL Server; 1,000 entries; 256 B / 4 KiB | CSV `artifacts/provider-local/results/BridgingIT.DevKit.Application.Benchmarks.Caching.CacheProviderBenchmarks-report.csv`, observations `artifacts/provider-local/observations.json` |
| Reduced native provider | 4 | Local FileSystem/Durable; **100 entries**; two hit sizes and two maintenance scenarios | Hit CSV `artifacts/native-bounded/results/BridgingIT.DevKit.Application.Benchmarks.Caching.CacheProviderBenchmarks-report.csv`, maintenance CSV `artifacts/native-bounded/results/BridgingIT.DevKit.Application.Benchmarks.Caching.CacheMaintenanceBenchmarks-report.csv`, observations `artifacts/native-bounded/observations.json` |
| Strict decoded hot reuse | 4 | InMemory / SQLite; 1,000 entries; 4 KiB; retention off/on | CSV `artifacts/hot-shared/results/BridgingIT.DevKit.Application.Benchmarks.Caching.CacheHotEntryBenchmarks-report.csv` |
| Same-key source bursts | 8 | InMemory / SQLite; 1 / 8 / 32 / 128 callers; one source per burst | CSV `artifacts/hot-shared/results/BridgingIT.DevKit.Application.Benchmarks.Caching.CacheSharedLoadBenchmarks-report.csv`, observations `artifacts/hot-shared/observations.json` |
| Full binary transfers | 6 | InMemory / SQLite / FileSystem; 64 KiB / 1 MiB; one retained raw resource | CSV `artifacts/streaming/results/BridgingIT.DevKit.Application.Benchmarks.Caching.CacheStreamingBenchmarks-report.csv`, observations `artifacts/streaming/observations.json` |
| Real Kestrel HTTP | 3 profiles × 5 rounds | 8,500 successful complete transfers; zero failures/misses | 4 KiB/c1 `../../Presentation.Benchmarks/Caching/artifacts/http-4k-c1.json`, 4 KiB/c8 `../../Presentation.Benchmarks/Caching/artifacts/http-4k-c8.json`, 64 KiB/c8 `../../Presentation.Benchmarks/Caching/artifacts/http-64k-c8.json` |

There are 60 completed BDN cases including the 15-case repeat. These operations have different semantics. A burst delivers `Concurrency` results from one source; miss, negative, metadata and maintenance operations must not be counted as positive-entry throughput.

Observation snapshots exclude workload-specific seed counters and include warm-up plus measured execution. At 128 callers, InMemory recorded 3,443 source attempts and 437,261 joins: exactly 127 joins per source. SQLite recorded 37 attempts and 4,699 joins, also 127 per source. Both recorded zero source failures. These are node-local execution facts, not a cross-host performance result.

## Representative results

### Typed positive hits

These results include authoritative validity, decoding and full integrity verification. The native rows use a smaller data set and one invocation per iteration; they are not matched comparisons with the 1,000-entry rows.

| Provider | Entries | Logical bytes | Median normalized time | Min–max normalized time | Managed allocation/op |
| --- | ---: | ---: | ---: | ---: | ---: |
| InMemory | 1,000 | 256 | 18.59 µs | 18.54–18.73 µs | 10.04 KiB |
| InMemory | 1,000 | 4,096 | 14.96 µs | 13.76–17.24 µs | 24.92 KiB |
| SQLite | 1,000 | 256 | 1.445 ms | 1.418–1.564 ms | 87.04 KiB |
| SQLite | 1,000 | 4,096 | 1.468 ms | 1.446–1.474 ms | 111.91 KiB |
| PostgreSQL, local container | 1,000 | 256 | 0.970 ms | 0.952–0.990 ms | 82.52 KiB |
| PostgreSQL, local container | 1,000 | 4,096 | 1.010 ms | 0.991–1.031 ms | 106.32 KiB |
| SQL Server, local container | 1,000 | 256 | 0.884 ms | 0.874–0.926 ms | 119.95 KiB |
| SQL Server, local container | 1,000 | 4,096 | 0.938 ms | 0.909–0.993 ms | 149.00 KiB |
| FileSystem, local Durable | **100** | 256 | 156.3 ms | 121.8–175.2 ms | 2.73 MiB |
| FileSystem, local Durable | **100** | 4,096 | 156.0 ms | 116.1–174.0 ms | 2.75 MiB |

Native results are slow and variable on this recorded profile. They motivate separate investigation; they do not identify a protocol bottleneck. No CPU/I/O profiler run established the limiting resource.

### Strict decoded reuse and source sharing

| Provider / 4-KiB value | Decoded retention | Median normalized time | Managed allocation/op |
| --- | --- | ---: | ---: |
| InMemory | Off | 27.343 µs | 25,560 B |
| InMemory | On, strict authority | 1.135 µs | 832 B |
| SQLite | Off | 1.785 ms | 106,603 B |
| SQLite | On, strict authority | 1.327 ms | 79,128 B |

Each enabled profile retained 9,048 hot bytes for the one decoded string version. Authority validation remained part of each reuse. Several timing ranges exceed 5%, so these samples do not establish a percentage speedup.

The 128-caller InMemory burst median was 406.66 µs (398.98–408.05 µs); SQLite was 212.301 ms (206.486–217.243 ms). Each operation included removal, caller observations and one confirmed publication. One operation delivered 128 results; it did not execute 128 sources.

### Binary transfers and maintenance

For one-MiB resources, normalized median complete-transfer time was 1.648 ms for InMemory, 16.633 ms for SQLite, and 61.760 ms for local Durable FileSystem. Ranges were 1.643–1.655 ms, 16.058–18.368 ms and 45.662–74.456 ms. These include integrity completion and reader disposal, not just stream acquisition.

Native maintenance over 100 live entries had median 5.018 seconds (5.001–5.023 seconds). The foreground-hit-plus-maintenance scenario had median 5.013 seconds (5.000–5.030 seconds), measuring **both operations**, not foreground latency. Inclusive allocations were 119.62 / 83.45 MiB per scenario; these measure managed allocation churn, not retained resident memory.

### Actual HTTP requests

Each cell reports the median of five independently calculated round statistics, not a pooled percentile across rounds. One successful request delivered one entry.

| Resource / callers | Successful entries | Entry/s median (round min–max) | Complete-request p50 / p95 / p99, ms | Process allocated bytes/entry, median |
| --- | ---: | ---: | ---: | ---: |
| 4 KiB / 1 | 500 | 5,001 (3,805–5,338) | 0.1918 / 0.2559 / 0.2733 | 100,182 |
| 4 KiB / 8 | 4,000 | 29,070 (19,380–30,976) | 0.2264 / 0.4879 / 1.0000 | 100,179 |
| 64 KiB / 8 | 4,000 | 16,384 (14,384–16,684) | 0.3691 / 1.1728 / 1.3133 | 284,936 |

JSON retains every round's first-byte/complete quantiles, GC counts, rates and finite limits. All final HTTP snapshots had zero transient charges. Resident accounts were 240,170 bytes for 4 KiB and 301,616 bytes for 64 KiB. Process allocation includes HttpClient, Kestrel and receive buffers; it is not cache-only overhead. Short rounds show substantial variance and are not capacity/saturation qualification.

## Repetition, failures and remaining qualification

Common's identical 15-case cohort was repeated. Forward-decode medians for 256 B / 4 KiB / 64 KiB were initially 371.2 ns / 649.1 ns / 19.611 µs, then 307.2 ns / 769.0 ns / 19.550 µs. Within-run ranges and between-run differences exceed 5% in several cases. Comparisons remain **inconclusive**; no 10% regression or improvement claim is made.

The FileSystem 1,000-entry in-process case exceeded the 300-second **whole-case** timeout. Its raw log has zero workload-jitting, pilot, actual or cleanup-observation markers. The exact worker stage was not independently captured. Do not attribute it specifically to seeding, setup, lookup latency or protocol replay. The failure log `artifacts/provider/native1k-timeout.txt` and process check `artifacts/native-timeout-process-check.json` retain this initial failed attempt. The later explicit-budget cohorts above completed the 1,000-entry workload twice; the original failure stage remains unknown.

The first reduced-native automatic-pilot run was deliberately interrupted after five-second maintenance units caused excessive pilot multiplication. Its partial log `artifacts/native-reduced/interrupted-native.txt` is not completed qualification. The invocation-one rerun produced the four native rows above.

All historical completed profiles had zero transient charges in their cleanup snapshots. Three SQLite burst snapshots still observed one admitted native cleanup tail at that instant; host disposal followed. The report does not invent a zero-active-tail snapshot. Those cohorts did not independently collect peak memory, SQL statement counts, native byte-I/O counts or CPU/I/O bottleneck profiles. The later syscall diagnostic above supplies separately scoped native I/O counts.

Still unqualified: persistent 100,000/1,000,000/extended 10,000,000-entry profiles beyond resident budgets; larger typed-value timings; remaining miss/negative/jitter/overlapping-batch/refresh performance combinations; phase-isolated request percentiles and large-stream acquisition/first-byte distributions; an equivalent-native baseline for cache-only overhead subtraction; peak-memory and CPU/device bottleneck measurements; matched Durable/Buffered comparison; separate-host 2/4-node performance; Linux performance and native ext4/XFS device profiles; SMB/NFS, reconnect and power loss. The native 1,000-entry baseline and seven 64-MiB/one-GiB full-stream cases now have executed numeric results above. Repeated noisy samples remain inconclusive for stable latency or relative performance claims.

## Exact run controls

See [README.md](README.md) for provider/policy/admission settings. All timing used prebuilt Release executables. Shared BDN options were `--inProcess --warmupCount 2 --iterationCount 5 --iterationTime 100 --unrollFactor 1 --exporters json csv --allStats`.

| Executable | Filter | Environment override | Artifact directory |
| --- | --- | --- | --- |
| `Common.Benchmarks.dll` | `*ValueCodecBenchmarks*` | None | `codec-artifacts`, then `codec-repeat-artifacts` under Common.Benchmarks |
| `Application.Benchmarks.dll` | `*CacheProviderBenchmarks.GetHitAsync*` | `BDK_CACHE_BENCH_PROVIDERS=InMemory,Sqlite,SqlServer,PostgreSql` | `Caching/artifacts/provider-local` |
| `Application.Benchmarks.dll` | `*CacheProviderBenchmarks.GetHitAsync*` and `*CacheMaintenanceBenchmarks*` | `BDK_CACHE_BENCH_PROVIDERS=FileSystem`, `BDK_CACHE_BENCH_ENTRIES=100`; **replace warmup with 1 and add `--invocationCount 1`** | `Caching/artifacts/native-bounded` |
| `Application.Benchmarks.dll` | `*CacheHotEntryBenchmarks*` and `*CacheSharedLoadBenchmarks*` | `BDK_CACHE_BENCH_PROVIDERS=InMemory,Sqlite` | `Caching/artifacts/hot-shared` |
| `Application.Benchmarks.dll` | `*CacheStreamingBenchmarks*` | `BDK_CACHE_BENCH_PROVIDERS=InMemory,Sqlite,FileSystem` | `Caching/artifacts/streaming` |

Executable locations are `benchmarks/<project>/bin/Release/net10.0/`. Each row also supplied `--filter` and `--artifacts` with the indicated values. The exact HTTP command was:

```powershell
dotnet benchmarks/Presentation.Benchmarks/bin/Release/net10.0/Presentation.Benchmarks.dll caching --output benchmarks/Presentation.Benchmarks/Caching/artifacts/http-4k-c8.json --rounds 5 --iterations 100 --concurrency 8 --payload 4096
```

The other HTTP runs changed payload/concurrency to 4096/1 and 65536/8 and used their corresponding filenames. All generated CSV, full JSON, observations, manifests, failure logs and test results remain local and Git-ignored. Run the commands above to execute the workloads and produce new output. These runs complement the [provider contracts](../../../tests/Shared/Caching/CacheProviderContractTestsBase.cs) and real multi-node tests; they do not replace them.

## 2026-11-11 bounded scale and diagnostic observations

These additional executions used runtime revision `58abecd37` with uncommitted benchmark instrumentation and controls. That revision includes core fixes `8fb217513`, provider fixes `e2f641280`, and HTTP fixes `3062c8487`. Historical cohorts above retain their original source and measurement scopes.

The host was Windows 11 build 10.0.26300.9711 on an AMD Ryzen 9 9900X, 12 physical / 24 logical cores, with 66,157,084,672 bytes visible physical memory. BenchmarkDotNet was 0.15.8, the SDK 10.0.403, and the runtime .NET 10.0.14 X64 RyuJIT. Runs used the ordinary OS temporary directory. No independent host or share was involved.

### Actual 100,000-entry InMemory data set

The `InMemory / Plain / 256-byte` case confirmed all 100,000 individual public `Collection.SetAsync` writes in 3.742 seconds and required the final seed quota to equal 100,000. No bulk prefill bypassed the cache. Setup then added the workload's one negative marker, so cleanup correctly reported **100,001 current entries**, 77,300,000 stored representation bytes, and 697,143,412 resident account bytes. Account and metadata byte estimates are not physical heap sizes.

| Explicit bound | Bytes or count |
| --- | ---: |
| Process resident | 2,147,483,648 B |
| Provider and collection resident, each | 1,610,612,736 B |
| Provider metadata/index | 805,306,368 B |
| Process transient | 268,435,456 B |
| Provider and collection storage, each | 1,073,741,824 B |
| Provider and collection entry ceiling, each | 100,064 |
| Setup / whole-case budgets | 900 / 1,800 s |

The run used one warm-up iteration, five measured iterations, 128 invocations per iteration and unroll factor one. BenchmarkDotNet reported a median of **18.740 µs**, mean 18.547 µs, retained iteration range 15.477–21.233 µs and 10,731 allocated bytes/op. One 26.785-µs iteration was removed as an outlier. Iterations were only about two milliseconds, and the interval/range is wide; this is a completed scale representative, not stable latency or comparative performance qualification.

The separate diagnostic window was explicitly enabled with `BDK_CACHE_BENCH_CAPTURE_METRICS=1`. It retained **897 actual hit durations**, including workload jitting, warm-up, measured operations and the allocation pass after setup. Its actual-duration p50/p95/p99 were **18.3 / 26.7 / 110.1 µs**. Cache counters agreed: 897 positive hits, 897 completed transfers, zero source attempts and zero joins. These are not percentiles of iteration means or measured-only samples.

| Diagnostic observation after setup | Value |
| --- | ---: |
| All-thread process allocated bytes, including harness/sampler | 11,779,528 B |
| Process Gen0 / Gen1 / Gen2 collections | 138 / 138 / 138 |
| Sampled peak working set | 510,861,312 B |
| Sampled peak managed memory | 407,224,968 B |
| Observed peak resident / transient accounts | 701,412,475 / 4,269,063 B |
| Runtime samples / failed samples | 16 / 0 |
| Final transient accounts / actual native tails | 0 / 0 |

BenchmarkDotNet's default job forced garbage collections between stages. The whole-process GC totals include that harness work; the measured allocation pass reported zero collections. Memory sampling started after prefill, used a 250-ms interval plus cohort boundaries, and can miss shorter peaks. Resource observations also sampled actual operation boundaries. They do not establish a hard heap limit or a peak for setup.

The exact numeric environment and executable command are the 100,000-entry example in [README.md](README.md), with `BDK_CACHE_BENCH_CAPTURE_METRICS=1`. Raw artifacts are `.tmp/cache-bench-inmemory-100k/`; the complete console/progress log is `.tmp/cache-bench-inmemory-100k.log`. The completed case took 17.09 seconds including setup and harness work.

### Actual 1,000,000-entry InMemory data set

The second scale case used one million 256-byte ASCII values through the same public write path. It confirmed every write in **23.475 seconds**, required exactly 1,000,000 seed entries, and completed its full benchmark in **148.72 seconds**. Its later negative-marker setup produced 1,000,001 current entries at cleanup. The store retained 773,000,000 representation bytes and 6,963,945,636 resident account bytes, with zero final transient accounts or native tails.

This case explicitly selected process resident 17,179,869,184 B; provider and collection resident 12,884,901,888 B each; provider metadata 8,589,934,592 B; storage 8,589,934,592 B; and entry ceilings 1,000,064. Process transient stayed 268,435,456 B. Setup/whole-case budgets were 900/1,800 seconds, progress was every 100,000 stored entries, and diagnostics were enabled. The command used the same provider/hit filter with `BDK_CACHE_BENCH_ENTRIES=1000000`, one warm-up, five measured iterations, **8192 invocations per iteration**, and `.tmp/cache-bench-inmemory-1m/` artifacts.

BenchmarkDotNet reported a median **16.31 µs**, mean 15.61 µs, iteration range 10.91–18.50 µs, and 9.96 KiB allocated/op. There were **57,345 verified positive hits and completed transfers**, zero source attempts and zero joins. The diagnostic duration window observed all 57,345 operations and retained its last **4,096 actual samples**, whose p50/p95/p99 were **10.3 / 14.5 / 21.0 µs**. The window includes the later allocation pass and is not a measured-only distribution. This single diagnostic run does not establish a timing comparison with the 100,000-entry case.

| Diagnostic observation after setup | Value |
| --- | ---: |
| All-thread process allocated bytes, including harness/sampler | 587,234,840 B |
| Process Gen0 / Gen1 / Gen2 collections | 165 / 134 / 134 |
| Cumulative GC pause in this window | 122.490396 s |
| Sampled peak working set | 4,206,477,312 B |
| Sampled peak private / managed memory | 4,165,070,848 / 4,023,934,072 B |
| Observed peak resident / transient accounts | 6,968,214,699 / 4,269,063 B |
| Runtime samples / failed samples | 22 / 0 |

BenchmarkDotNet's forced collections dominated whole-case time and are included in the process GC/pause totals. They are not cache-request GC counts. The sampler can miss peaks while the runtime is paused; 250 ms is its requested interval, not continuous coverage. Free physical RAM was 33.51 GiB before, 29.60 GiB during, and 33.51 GiB after the process exited. The full console/progress log is `.tmp/cache-bench-inmemory-1m.log`. The ten-million-entry profile was not run.

### Interrupted SQLite 100,000-entry prefill

The `Sqlite / Plain / 4096-byte` attempt selected 100,000 entries, intending a logical data set larger than the configured 256-MiB provider resident budget. It used public `SetAsync` throughout. Confirmed progress was 10,000 entries in 234.329 seconds, 20,000 in 441.332 seconds, and **30,000 in 683.259 seconds**. No benchmark workload-jitting, warm-up, measured-iteration or cleanup-observation marker was emitted.

The observed rate implied roughly 2,200 seconds for the full prefill, exceeding its already fixed 1,800-second setup deadline. The benchmark process was **manually interrupted before that predictable timeout**. No automatic rerun followed. This is an incomplete prefill attempt, with no 100,000-entry retained-count verification, no timed SQLite result and no claimed hardware or capacity failure. The last checkpoint proves 30,000 confirmed public writes, not the exact row count when the process was stopped. The partial database remains in its dedicated OS temporary directory; the complete interrupted log/artifact directory are `.tmp/cache-bench-sqlite-100k.log` and `.tmp/cache-bench-sqlite-100k/`.

Selected finite limits were process resident/transient 536,870,912 / 268,435,456 B; provider/collection resident 268,435,456 B each; provider metadata 16,777,216 B; storage 4,294,967,296 B; entry ceiling 100,064; setup/whole-case budgets 1,800/3,600 seconds; and progress every 10,000 writes. The intended workload used one warm-up, five measured iterations, 128 invocations and unroll factor one with diagnostics enabled.

An isolated Linux validation job ran from 02:22:04.349549569 through 02:25:31.138568659 UTC, overlapping this interrupted setup. It did not overlap either completed InMemory cohort. The SQLite setup duration therefore includes that competing host work and must not be treated as isolated provider throughput. A completed beyond-resident persistent profile remains unqualified; a future run needs its own explicitly larger setup/case budgets and recorded host activity.

### Duration, source and equivalent-native instrumentation checks

Seven additional finite cases exercised the new observations with one warm-up, three measured iterations and one invocation per iteration. All completed their work assertions with zero final transient accounts or native tails. Their short and noisy timing samples are execution evidence for the instrumentation.

The shared-success and source-only scenarios each observed 96 real caller completions across six actual cohort executions. Shared success executed six sources; source-only executed 96. Each source and each caller has its own real elapsed sample. Caller/source windows include jitting, warm-up, measurements and allocation work after setup.

The `InMemory / PlainZeros / 64-MiB` stream case completed six transfers with SHA-256, length and engine-integrity verification. The phase windows contained six actual durations each:

| Phase | p50 | p95 / p99 |
| --- | ---: | ---: |
| Collection acquisition | 0.1181 ms | 0.1692 / 0.1692 ms |
| First byte, including acquisition | 0.2149 ms | 0.2906 / 0.2906 ms |
| Full validated transfer and handle disposal | 131.6114 ms | 134.7171 / 134.7171 ms |

These windows include all post-setup executions and have only six samples. The sampler observed a 168,878,080-byte working set, 71,481,624-byte managed memory, and 68,128,874 / 139,264-byte resident/transient account peaks. The complete-transfer measured-iteration median was 129.7 ms, range 129.1–134.7 ms. No throughput comparison is inferred from this diagnostic smoke.

The direct-native and collection-engine binary paths both completed at 256 bytes and 4 KiB, checking authoritative native snapshots, framing, EOF, every logical byte and terminal integrity. `DirectNativeAsync` is now the BenchmarkDotNet baseline. The small, metrics-enabled samples do not support cache-only overhead subtraction: allocation differences changed sign by payload and timing ranges were wide. Paired, repeated timing runs with diagnostics off are still required for that comparison.

The diagnostic command selected `*CacheStreamingBenchmarks.CopyValidatedAsync*`, `*CacheSourceOutcomeBenchmarks.RunAsync*` and `*CacheNativeReadBenchmarks*`, with `BDK_CACHE_BENCH_PROVIDERS=InMemory`, `BDK_CACHE_BENCH_STREAM_BYTES=67108864`, `BDK_CACHE_BENCH_STREAM_REPRESENTATIONS=PlainZeros`, `BDK_CACHE_BENCH_SOURCE_CASES=SharedSuccess,SourceOnly`, `BDK_CACHE_BENCH_CAPTURE_METRICS=1`, setup/whole-case budgets 30/120 seconds, and `--warmupCount 1 --iterationCount 3 --invocationCount 1 --unrollFactor 1 --exporters json csv --allStats`. Its artifacts/log are `.tmp/cache-bench-diagnostics-smoke/` and `.tmp/cache-bench-diagnostics-smoke.log`.

### Benchmark source identity

These SHA-256 values identify the compiled dirty benchmark sources for these executions. Runtime source identity is the committed revision above. Documentation edits are outside the executable hash set.

| Source under `Caching/` | SHA-256 |
| --- | --- |
| `CacheBenchmarkEnvironment.cs` | `7D30614091E21F783E69DD6235B2C46C9025928B50F7251A7D295C84D3C45146` |
| `CacheBenchmarkMeasurements.cs` | `BF81B52DCB428116D391B3002562D8D7BE64B338BFF120C6D5F6C7AE4ADE1A2F` |
| `CacheNativeReadBenchmarks.cs` | `2C177751B7B883A3BFFF680E6DDBAA2A3B1C422185FA04F80CEA3B385766B412` |
| `CacheProviderBenchmarks.cs` | `FF4ED17DE96B80716A89BDBEE87B07BC26DE7068CC8F49D32635F4C3FE485579` |
| `CacheSharedLoadBenchmarks.cs` | `6AF33DE35046D12FBEC800B6F7211E43B93BD7F23BE9F209D34ABF907601D1C1` |
| `CacheSourceOutcomeBenchmarks.cs` | `5F12319FFEA1934594F859477F9506B919A5E063D87ED417039E84DEF681E32F` |
| `CacheMultiInstanceBenchmarks.cs` | `8EDF59AD26D8056F9A716C4D851EF017EF8A5F55B2FE6D0F44C6244F11300993` |
| `CacheStreamingBenchmarks.cs` | `3EE10D4EEE2D33F66F5465C3200E31E1630806374E548598071835B73A4C3BD9` |