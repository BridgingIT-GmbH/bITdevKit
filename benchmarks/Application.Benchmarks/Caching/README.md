# Application cache benchmarks

These BenchmarkDotNet workloads exercise the ordinary `IApplicationCache` engine and complete native providers. Setup creates a real isolated store, applies consumer-owned database migrations, seeds a bounded data set, and verifies retained entry counts. Each workload checks its declared outcomes. An unexpected rejection, failure or incomplete operation throws and never contributes successful throughput.

## Run a workload

Run from the repository root with the worktree's SDK. Build and test commands in the same worktree must run sequentially.

```powershell
dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheProviderBenchmarks.GetHitAsync*'
dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheHotEntryBenchmarks*'
dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheSharedLoadBenchmarks*'
dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheStreamingBenchmarks*'
dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheMaintenanceBenchmarks*'
dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheExpiryBenchmarks*'
dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheMultiInstanceBenchmarks*'
dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CachePolicyBenchmarks*'
dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheSourceOutcomeBenchmarks*'
dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheNativeReadBenchmarks*'
```

Keep BenchmarkDotNet's raw log, measurement CSV, runtime and hardware metadata locally with the generated report. Test and benchmark artifacts are ignored from Git; source and documentation remain tracked. Use at least five measured iterations after warm-up. `--job dry` checks workload execution; it provides no performance qualification.

Persistent preparation can exceed the normal runner budget. Set `BDK_CACHE_BENCH_TIMEOUT_SECONDS` to an explicit finite value from 30 through 86,400 seconds when that preparation is part of the selected profile. This selects the bounded in-process toolchain and removes a redundant `--inProcess` argument; omit that argument when setting the variable. The whole-case budget includes setup, warm-up, measured work and cleanup. BenchmarkDotNet's workload timings exclude seeding. A longer runner budget does not turn setup time into lookup time or hide a failed profile. For example, the local native 1,000-entry profile can use a deliberately recorded 1,200-second budget with one invocation per iteration.

## Workload reference

| Class / operation | Successful work per invocation | Included work |
| --- | --- | --- |
| `CacheProviderBenchmarks.GetHitAsync` | One typed entry | Native validity, payload acquisition, complete decode and integrity |
| `GetMissAsync` | One confirmed miss, zero entries | Absent-key read; no source |
| `SetAsync` | One stored entry | Replacement staging, encoding, native commit and acknowledgement |
| `MetadataPageAsync` | min(100, Entries) metadata records, zero payload entries | Indexed fixed-cutoff metadata page |
| `GetNegativeAsync` | One negative hit, zero positive entries | Confirmed-absence metadata without payload materialization |
| `GetBatchAsync` | Four typed entries | Ordered batch hit acquisition and per-item results |
| `AcquiredSnapshotToken` | No provider operation | Getter on a snapshot acquired during setup |
| `SourceMaterialization` | No cache operation | Source-side string allocation baseline |
| `CacheHotEntryBenchmarks.GetAsync` | One typed entry | Identical key/value with Off, Binary, Decoded or BinaryAndDecoded retention; retained reuses assert one authority validation |
| `CacheSharedLoadBenchmarks.SameKeyColdBurstAsync` | `Concurrency` caller results, one source invocation | Exact invalidation, source gate, local joins and confirmed publication |
| `OverlappingColdBatchesAsync` | Four ordered caller entries, three distinct source keys | Two cold overlapping batches held at a source barrier; exact once-per-key source checks and confirmed retained results |
| `CacheStreamingBenchmarks.CopyValidatedAsync` | One complete binary entry | Acquire, full copy, final integrity completion and reader disposal |
| `CacheMaintenanceBenchmarks.MaintainLiveDataAsync` | One bounded maintenance attempt | Native maintenance of the selected live data set |
| `ForegroundWithMaintenanceAsync` | One typed entry plus one bounded maintenance attempt | Both operations complete; duration is combined scenario time |
| `ForegroundWithRefreshAsync` | One held source refresh plus two foreground reads | Original publication, old-version read during actual refresh, replacement publication and new-version read; combined scenario time |
| `CacheExpiryBenchmarks.JitteredExpiryBurstAsync` | 32 complete entries; exact source count follows persisted deadlines | Per-invocation seeding, 20% downward jitter validation, controlled UTC advance and simultaneous hits/expired-key reloads |
| `CacheHotEntryBenchmarks.DistinctScanAsync` | min(32, Entries) complete entries | Successive distinct keys, admission and scan turnover under the selected finite hot limits |
| `SkewedReadsAsync` | 32 complete entries | 24 reads target the hot key and eight scan the working set under the same retention/validity policy |
| `VersionTurnoverAsync` | One replacement and three new-version reads | Old snapshot read, stored replacement, obsolete-hot rejection and new-version admission |
| `CacheMultiInstanceBenchmarks.ColdBurstAsync` | 32 caller observations | Two/four independent DI containers over one local SQLite/native root; explicit source scope or uncached positive handoff |
| `CachePolicyBenchmarks.RunAsync` | Scenario-specific; see below | Actual traffic, partial results, selection, pressure, tracked work, breaker cycle or diagnostics |
| `CacheSourceOutcomeBenchmarks.RunAsync` | 16 completed observations, or 15 delivered plus one cancelled caller | Equal admission, actual follower registration, source success/failure/timeout, suppression or invalidation |
| `CacheNativeReadBenchmarks.EngineAsync` / `DirectNativeAsync` | One completely verified plain binary entry | Same authoritative snapshot, persisted framing, terminal integrity, length and byte checks; collection dispatch versus native-envelope path |

An operation rate is not an entry rate. Multiply a successful batch operation rate by four for entries/second. A same-key burst has multiple delivered results but only one source execution and one stored entry. Do not interpret miss, negative, metadata, maintenance or token-getter throughput as positive entry throughput.

The original shared-load microbenchmark uses one process and one service container. `CacheMultiInstanceBenchmarks` composes two/four independent application containers over one local SQLite file or native root. It keeps the total at 32 callers: SharedStore executes one source, NodeLocal one per instance, Independent one per caller, and SourceOnly executes 32 computations without measured cache access. UncachedHandoff suppresses the first owner group, then requires one new peer source and confirmed publication for remote delivery. Source and follower barriers are included in scenario time. These are in-process application-instance measurements, separate from independent OS-process tests or cross-host qualification.

Run each workload in a separately recorded cohort with its exact revision and local raw output. [RESULTS.md](RESULTS.md) identifies the methods and profiles actually measured. The overlapping-batch method has a fixed two-batch / three-key shape; the class's `Concurrency` parameter applies to `SameKeyColdBurstAsync` only.

The expiry workload uses a one-hour TTL shortened once by up to 20%. It selects the midpoint of the largest gap between actual persisted deadlines, advances validity only between completed operations, and derives the expected hit/source count from those deadlines. If all deadlines coincide, it advances past the final deadline instead. Real elapsed-time measurement and cancellation timers stay on system time. Each advance is bounded to one hour and a host may advance at most ten years; cleanup reports the actual UTC offset and clock mode. This tests logical expiration, not automatic refresh scheduling or clock-skew recovery. Foreground refresh uses `RefreshAsync` with a real held source callback; it does not substitute maintenance for source work.

For a bounded execution check, select reduced profiles explicitly and disable automatic invocation calibration:

```powershell
$env:BDK_CACHE_BENCH_PROVIDERS = 'InMemory,Sqlite'
$env:BDK_CACHE_BENCH_ENTRIES = '4'
dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*OverlappingColdBatchesAsync*' '*JitteredExpiryBurstAsync*' '*ForegroundWithRefreshAsync*' --job dry --invocationCount 1 --unrollFactor 1
```

The dry job proves execution and work assertions only. It does not provide latency, throughput, or repeatability qualification.

## Provider profiles

`InMemory`, `Sqlite`, and `FileSystem` are the checked-in default parameter values. FileSystem uses a private local temporary root with `Durable` acknowledgement. SQLite uses a private local file, WAL, explicit host migrations, fresh owned contexts, and a five-second busy timeout. Seed and file cleanup remain outside timed operations.

Set `BDK_CACHE_BENCH_PROVIDERS` to a comma-separated subset of `InMemory,Sqlite,SqlServer,PostgreSql,FileSystem` to select explicit result parameters without editing the workloads. SQL Server and PostgreSQL start disposable local Testcontainers when `BDK_CACHE_BENCH_CONNECTION` is absent. The selected workload images follow the repository's SQL Server `2022-latest` and PostgreSQL `16-alpine` fixture profiles; record the actual image digest in evidence. Set that variable to use an existing dedicated benchmark database instead. The benchmark host applies its own linked consumer migration; it does not deploy runtime-package migrations. The host disposes owned containers and does not delete an externally supplied database.

The default active data set has 1,000 entries and 256-byte or 4-KiB logical ASCII strings. Set `BDK_CACHE_BENCH_ENTRIES` to explicit comma-separated counts; every provider/maintenance result includes its actual `Entries` parameter. A reduced representative such as 100 entries does not qualify the specified 1,000-entry baseline. Larger persisted workloads must use explicit 100,000 / 1,000,000 / extended 10,000,000 entry parameters and declared storage/resident budgets. InMemory setup rejects a data set it cannot actually retain. A failed capacity profile must be reported separately rather than compared with a complete retained-hit workload.

`BDK_CACHE_BENCH_MAX_ENTRIES` sets the same finite provider and collection entry ceiling, between the largest selected seeded count plus 64 and 1,000,000,000. When omitted, that ceiling is the greater of the seeded count plus 64 and the normal collection default (100,000 for InMemory; 1,000,000 for persistent providers). The 64-entry allowance admits temporary workload keys without silently evicting the declared seeded data set.

`BDK_CACHE_BENCH_MAX_STORAGE_BYTES` explicitly sets the same provider and collection stored/staged/retired byte ceiling, up to four TiB. When omitted, existing storage defaults remain: provider ten GiB; collection 128 MiB for InMemory or ten GiB for persistent providers. The selected single-payload limit must fit the storage limit. Leave room for seeded data, candidate staging, framing, metadata, and retained reader snapshots; a one-GiB payload requires more than one GiB of available capacity. For example, a dedicated persistent one-GiB transfer profile can use `BDK_CACHE_BENCH_MAX_STORAGE_BYTES=4294967296` with `BDK_CACHE_BENCH_STREAM_BYTES=1073741824`. Resident and metadata ceilings have separate explicit controls below. Large retained InMemory profiles reject capacity they cannot retain.

Binary workloads use 64-KiB and 1-MiB uncompressed payloads by default. Set `BDK_CACHE_BENCH_STREAM_BYTES=67108864` for 64 MiB or `1073741824` for one GiB, and choose a provider whose explicit retained-storage capacity supports that profile. The source emits zeros or deterministic pseudorandom bytes through a forward-only stream without allocating a payload-sized array. `BDK_CACHE_BENCH_STREAM_REPRESENTATIONS` selects `PlainZeros`, `PlainRandom`, `GZipZeros`, or `GZipRandom`; the default is `PlainZeros`. The source is hashed during setup, and each measured transfer checks its complete logical SHA-256, length, and engine integrity. The client buffer is a fixed 64 KiB. GZip selects Optimal compression with ordinary minimum-saving fallback, so incompressible input may retain its intact uncompressed representation. The default InMemory resident budget does not support the one-GiB profile. Complete-transfer results include payload verification; timing only `OpenReadAsync` would omit the work. The optional diagnostic cohort below records actual collection acquisition, one-byte delivery and validated full-transfer durations. Full HTTP transfer latency has a separate [HTTP runner](../../Presentation.Benchmarks/Caching/README.md).

## Interpret results

Compare identical runtime, provider engine, durability, payload, data set, concurrency, policy and admission settings. Set `BDK_CACHE_BENCH_POLICIES` to `Plain`, `Compression`, `Jitter`, `Breaker` or a comma-separated subset for visible `CacheProviderBenchmarks` policy parameters. Compression selects GZip/Optimal, jitter shortens the absolute TTL by at most 20%, and the breaker profile measures a healthy closed provider circuit. Plain leaves all three disabled. Negative hits remain explicitly enabled and measured separately from misses and positives. `CacheExpiryBenchmarks` explicitly uses Jitter; streaming selects its labelled compression representation, and the policy runner enables only its selected scenario policy.

The application workload host sets process resident/transient limits to 512/256 MiB, provider and collection resident limits to 256 MiB, and a conservative typed decoded-graph bound of 4 MiB. The concurrent source workload raises native I/O/stream admission to its selected concurrency and retains finite queues, so the 128-caller profile can admit its stated work. Hot retention uses the standard finite hot sublimits and is enabled only by its dedicated workload. Raw transfer limits are raised only to the selected binary profile. Matching these settings is necessary for comparisons; changing a bound changes admitted work.

`MemoryDiagnoser` reports the microbenchmark's allocation profile. It is not a hard managed-heap ceiling and is not sufficient by itself for concurrent cache-owned memory attribution. Native I/O, provider query counts, resident/transient accounts and successful source/join counts need their corresponding diagnostics or a separately labeled profiler run. Profiling changes execution and must not be mixed into timing evidence.

Report the median and range across repeated runs. If variation exceeds 5%, an apparent 10% difference is inconclusive until the variance or limiting resource is explained. Record machine load before timing; builds, antivirus, container startup and unrelated workloads can dominate short measurements.

See [RESULTS.md](RESULTS.md) for executed profiles and explicit gaps. A Windows local-root run does not qualify Linux, SMB, NFS, network reconnect, cross-host ownership, kernel crashes or power loss. Unexecuted profiles remain unqualified.

## Scenario controls and work accounting

`BDK_CACHE_BENCH_LOAD_MODES` selects the multi-instance `SharedStore`, `NodeLocal`, `Independent`, `SourceOnly` or `UncachedHandoff` modes. Competing node-local or independent publications can return Conflict while delivering their complete computed values; at least one caller must confirm a stored winner.

`BDK_CACHE_BENCH_TYPED_BYTES` selects explicit typed value sizes from `256,4096,65536,1048576`; normal defaults are 256 and 4096. Dataset cardinality and payload size are independent result parameters. `BDK_CACHE_BENCH_DURABILITY=Durable` or `Buffered` labels the selected filesystem acknowledgement policy; the default is Durable. It does not change the local filesystem's native authority rules.

`CachePolicyBenchmarks` normally selects SQLite. `BDK_CACHE_BENCH_POLICY_CASES` selects these coherent profiles instead of multiplying unrelated policy flags:

| Scenario | Actual completed work per invocation |
| --- | --- |
| DistinctCold | Remove 32 keys, admit 32 held sources, then confirm 32 stored caller results; no same-key coalescing |
| MixedTraffic | Seed 32 keys, then complete 24 reads and 8 stored replacements on distinct keys |
| PartialBatch | Four cold ordered items; exactly one declared source failure and three retained positive entries |
| SelectionInvalidate | Seed 32 entries, preview exactly 32 metadata records, then run fixed-cutoff native continuations to invalidate 32 versions |
| TrackedInvalidation | Seed 260 entries, accept a provider-owned status before mutation, and run bounded continuation units until the cumulative completed effect is 260 |
| Clear | Clear preceding scenario data, seed 32 entries, then clear the logical collection through fixed-cutoff native continuations |
| EvictionPressure | Clear, seed a 32-entry child cap, acquire a low-priority snapshot, publish a 33rd key and a replacement, then verify the entry cap and the unchanged fully validated acquired content; returns two publications |
| ExpiredCleanup | Clear, seed 32 one-minute entries, advance logical UTC by 6 minutes, confirm a logical miss and retire all 32 current entries; payload retention remains provider-owned |
| BreakerCycle | Persistent providers only: five controlled infrastructure failures, one open rejection with no native call, then an 11-second logical-clock advance and one nonmutating recovery probe plus requested quota read |
| DiagnosticsMinimal | One fully decoded hit with optional intrinsic observations disabled |
| DiagnosticsStandard | One fully decoded hit with cache metrics and active Common operation profiling |
| DiagnosticsDetailed | One fully decoded hit with metrics, active Common profiling, bounded key history and a real enabled cache-log record |

These mutation and failure scenarios include their stated preparation and verification. Their operation rates do not describe a single warm lookup. Tracked invalidation and native maintenance can consume multiple bounded units; use explicit invocation counts when running them. A continuation failure or unexpected partial effect fails the workload instead of contributing successful throughput.

Common operation profiling uses its bounded in-memory provider and periodic writer in Standard/Detailed. Only that writer is started; cache background refresh/maintenance workers are not implicitly started. Detailed logging uses a counter sink at Debug for cache categories, without message formatting, console output or external exporter I/O. The writer is stopped and drained during cleanup. Exporters and sinks with other costs need their own host profile.

`BDK_CACHE_BENCH_SOURCE_CASES` selects `SharedSuccess`, `IndependentSuccess`, `SourceOnly`, `SharedFailure`, `SharedInvalidation`, `SharedSuppressed`, `CallerDeparture`, or `SourceTimeout`. Each submits 16 callers with the same admission limits. Shared cases wait for 15 actual local joins before source release. Shared/independent/source-only success executes 1/16/16 sources respectively. Independent callers complete with their computed values; competing publication fences allow Stored or Conflict outcomes, with at least one retained winner. Failure and timeout deliver explicit failures without negative markers. Suppression and invalidation deliver local typed results without stored tokens; CallerDeparture cancels only the initiator and still delivers 15 followers. SourceTimeout uses a real five-second owned deadline and checks its classification. These are node-local controls; multi-instance source scope and uncached handoff have their separate runner.

The provider observation Behavior counts authoritative validation, payload-opening lookup, source acquisition and quota calls. These are provider-boundary operations, not native file opens/flushes or SQL statements. An EF command interceptor separately counts actual executed reader/nonquery/scalar commands after setup; it covers both synchronous and asynchronous command entry points. Cleanup labels the native profile, clock, limits, SQL commands, log records, bounded actual latency samples and resource observations. Latency windows explicitly include setup, warm-up and measured observations; the SQL/log counter baselines exclude setup. The sampled p50/p95/p99 values come from retained real durations, not iteration means. Native byte-I/O and OS flush/open counts require a separately labelled profiler run; they are not inferred from provider call counts.

`CacheNativeReadBenchmarks` compares the collection binary path with a direct native snapshot plus checked envelope on the same plain bytes. Both consume through EOF and verify every logical byte and terminal integrity under finite lifetime/idle cancellation. The direct path excludes collection dispatch, optional observers and transfer-admission overhead; it does not skip the provider's current-version or framing checks. Compressed, typed and raw-stream workloads remain separately labelled.

## Bounded scale and diagnostic cohorts

The resident, transient and index byte settings are finite and at most four TiB; child resident and metadata budgets must fit their parent. Resident settings start at 16 MiB, transient at four MiB, and metadata at one MiB. Setup cancellation accepts 1–86,400 seconds; progress accepts 1–1,000,000 writes per record. Omitted values use the defaults shown.

| Environment setting | Default | Scope |
| --- | ---: | --- |
| `BDK_CACHE_BENCH_MAX_RESIDENT_BYTES` | 536870912 | Process cache accounts |
| `BDK_CACHE_BENCH_PROVIDER_RESIDENT_BYTES` | 268435456 | Complete provider resident accounts |
| `BDK_CACHE_BENCH_COLLECTION_RESIDENT_BYTES` | 268435456 | Each collection's resident accounts |
| `BDK_CACHE_BENCH_MAX_TRANSIENT_BYTES` | 268435456 | Process transient accounts |
| `BDK_CACHE_BENCH_METADATA_BYTES` | 16777216 | Provider metadata/index capacity |
| `BDK_CACHE_BENCH_SETUP_TIMEOUT_SECONDS` | 1200 | Finite setup cancellation, from native-host creation through exact prefill verification |
| `BDK_CACHE_BENCH_PREFILL_PROGRESS_EVERY` | 10000 | Successfully stored public writes between progress records |

Setup invokes `Collection.SetAsync` for every declared entry. It reports progress and elapsed seconds, then requires the provider's exact retained count to equal `Entries`. Capacity eviction, incomplete prefill or data left in a reused store invalidates the case. No bulk import or synthetic cardinality bypasses the cache API.

This 100,000-entry, 256-byte InMemory profile declares two GiB for process cache accounts, 1.5 GiB for provider/collection accounts, 768 MiB for metadata and one GiB for storage. It leaves 64 temporary entry slots. The whole-case toolchain budget is 1,800 seconds, while setup cancellation is 900 seconds. The finite invocation count avoids automatic pilot multiplication.

```powershell
$env:BDK_CACHE_BENCH_PROVIDERS = 'InMemory'
$env:BDK_CACHE_BENCH_ENTRIES = '100000'
$env:BDK_CACHE_BENCH_TYPED_BYTES = '256'
$env:BDK_CACHE_BENCH_MAX_ENTRIES = '100064'
$env:BDK_CACHE_BENCH_MAX_RESIDENT_BYTES = '2147483648'
$env:BDK_CACHE_BENCH_PROVIDER_RESIDENT_BYTES = '1610612736'
$env:BDK_CACHE_BENCH_COLLECTION_RESIDENT_BYTES = '1610612736'
$env:BDK_CACHE_BENCH_METADATA_BYTES = '805306368'
$env:BDK_CACHE_BENCH_MAX_STORAGE_BYTES = '1073741824'
$env:BDK_CACHE_BENCH_SETUP_TIMEOUT_SECONDS = '900'
$env:BDK_CACHE_BENCH_TIMEOUT_SECONDS = '1800'
$env:BDK_CACHE_BENCH_PREFILL_PROGRESS_EVERY = '10000'
dotnet benchmarks/Application.Benchmarks/bin/Release/net10.0/Application.Benchmarks.dll --filter '*CacheProviderBenchmarks.GetHitAsync*' --warmupCount 1 --iterationCount 5 --invocationCount 128 --unrollFactor 1 --exporters json csv --allStats --artifacts .tmp/cache-100k
```

One-million and ten-million profiles select their real entry counts through the same controls; supply matching entry, storage, resident and metadata limits and an explicit whole-case budget. Those counts are selected capabilities until a completed exact-prefill case is recorded. No budget selection establishes a performance qualification by itself.

Set `BDK_CACHE_BENCH_CAPTURE_METRICS=1` for a separate diagnostic run. `ExecutionMeasurements` in cleanup JSON then contains real bounded p50/p95/p99 windows for typed hits, each submitted source caller's wait, actual source executions, stream acquisition, first-byte delivery and complete validated transfer. The stream first-byte sample ends after a one-byte read; full-transfer samples include EOF, integrity verification and handle disposal. Multi-instance caller waits belong to their actual node; its first node aggregates source durations across the scenario.

These windows begin after setup and include BenchmarkDotNet pilot, warm-up and measured calls. They are not measured-only percentiles. Each phase retains at most 4,096 actual durations from the last hour, with total observed and retained counts. Failure/cancellation waits and source durations remain caller outcomes rather than positive-entry throughput.

The diagnostic sampler uses Common runtime profiling every 250 ms and at cohort boundaries. Process allocation and GC deltas cover all threads in the same process, including BenchmarkDotNet, diagnostic sampling and other work in that process. Process peaks are observed working-set/private/managed-memory samples; they can miss shorter peaks. Cache account peaks are observed at sampling and operation/stream boundaries. They are accounting charges, not exact managed-heap ceilings. Shared-process multi-instance samples must not be summed as separate process memory.

Keep diagnostics off for ordinary timing comparisons and record enabled diagnostic runs separately. For a same-correctness cache-overhead comparison, run both `CacheNativeReadBenchmarks` methods with identical provider, payload, job and diagnostic settings. `DirectNativeAsync` is the baseline; both methods validate the native snapshot, framing, every logical byte and terminal integrity. Subtract paired per-operation means and allocation columns to report incremental collection-engine cost, with repeat ranges and uncertainty. Whole-process diagnostic allocation totals include harness/sampler costs and are not cache-only allocation subtraction.