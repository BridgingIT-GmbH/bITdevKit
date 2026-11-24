# Storage Dimensions qualification

This explicit workload exercises the EF blob and document providers independently or together. It is dispatched before BenchmarkDotNet. Build in Release and run engines sequentially without other builds or tests during measurement.

```powershell
dotnet build benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -c Release --nologo /p:UseSharedCompilation=false
dotnet benchmarks/Application.Benchmarks/bin/Release/net10.0/Application.Benchmarks.dll --storage-dimensions-qualification --engine sqlite --feature combined --smoke --output artifacts/storage-dimensions/smoke
dotnet benchmarks/Application.Benchmarks/bin/Release/net10.0/Application.Benchmarks.dll --storage-dimensions-qualification --engine sqlserver --feature blob --owners 1000000 --seed 17043 --output artifacts/storage-dimensions/sqlserver-blob-million
```

Use a new output directory. The public volume counts are 10,000, 100,000, and 1,000,000 per selected feature. `--smoke` substitutes 1,000 owners and short intervals; its results cannot qualify volume targets.

The Windows launcher places itself and the workload child in an 8-GiB job with four logical CPUs. Its owned server container receives 24 GiB, a four-CPU time quota, and CPU set `4-7`. Both quota and CPU visibility are verified. SQL Server must expose four visible online schedulers before seeding; a quota alone can leave it planning for the larger host. SQLite runs in the application process, so its job instead receives 32 GiB and eight logical CPUs. The child GC heap limit is below the job limit. No global Docker/VM settings change. The child verifies its effective job limit and affinity and records peak job commitment. Windows filesystem cache belongs to the larger physical host; the job is not equivalent to VM memory isolation. Report host cache residency with the physical-host details. Linux launch budgeting is not implemented.

The fixture contains eight values per owner, ten scopes and 32 automatically registered names per scope. A deterministic 50-cell schedule produces 18% of each non-null kind and 10% explicit null. Strings have 32 UTF-8 bytes; payloads have exactly 1,024 bytes. In the reference profile the first 40 owners use provider writes to obtain database-assigned dictionary IDs. Remaining fixture rows are inserted in 500-owner EF batches using those IDs and the production scalar encoders. Another 32 owners per feature exercise dimensionless paths in both databases. Native optimizer statistics are refreshed after seeding. Row counts and complete expected memberships of queries with up to 1,000 matches are checked before timing. Broad queries verify the first page, and document counts also verify their complete match count. Selective late pages contain 100 owners even when the final remainder page is shorter.

Server workloads use 16 readers and four writers; SQLite uses 16 readers and one writer with WAL and a 60-second busy timeout. Workers enter through a start barrier on separate tasks because SQLite async methods can finish synchronously. Each operation must receive measured work. Query cases cover scalar equality, supported numeric/date ranges, null/non-null/existence states, AND/OR, anchored negatives, first/late pages, document counts, and both document projections. Broad states/counts have no selective-latency promise. Writers change one non-query dimension on separate keys. A bounded 0.01-ms latency histogram retains overflow counts and exports nonempty buckets; failures count regardless of latency. Percentiles above two seconds remain Infinity.

Parent-only comparisons use the same provider and payloads against a benchmark-only context that omits the added snapshot/navigations/entities. They alternate model order over three pairs. Before comparison, three separate saturation intervals per model measure capacity. Freeze read/write rates at 70% of the median parent-only throughput. Both models then receive the same deterministic per-worker arrival schedule. Latency includes scheduler/queue delay from scheduled arrival; requests waiting over 250 ms count as missed and fail comparison. Reports include offered/completed/missed counts and queue p95. Saturated selective/delta workloads remain unchanged; ordinary saturation reports are separate capacity evidence. Per-operation allocation attribution remains a separate diagnostic and supplies no latency claim. Full intervals have 15 seconds of warm-up and two minutes of measurement. Global allocation results cover the complete interval. Three separate ten-second attribution pairs count managed allocations along logical async flows, under the same reader/writer concurrency. A concurrent-flow self-check and coverage comparison against total managed allocations guard the attribution. These diagnostic intervals do not supply latency measurements. A command capture checks that ordinary Properties and dimensionless operations access no dimension tables.

SQLite execution continues to use the project's native EF provider. If that build omits `dbstat`, install the footprint inspector locally under the ignored artifacts directory:

```powershell
python -m pip install --target artifacts/storage-dimensions/sqlite-tools apsw==3.54.1.0
```

The [APSW package](https://pypi.org/project/apsw/3.54.1.0/) opens only the run-owned files in read-only mode to inspect each table/index. Its SQLite version is recorded separately from the execution engine. No application dependency or global Python package changes. PostgreSQL totals include TOAST, relation forks, indexes and dictionary sequences; SQL Server reports used pages. Baseline subtraction includes the snapshot delta and every added dictionary/value structure.

Use `--profile sparse` for ten-percent populated owner groups and `--profile skew` for a 91-percent hot scope. Their actual owner/value counts are recorded; reference SLA assessment does not apply to these stress profiles. Skew seeding registers the initial 400 owners because scope/name combinations differ from the round-robin fixture.

Add `--stress` to run separate maximum-size and registration intervals after the representative measurements. These seed eight 64-dimension owners with 128-character names and 256-byte strings, execute a 16-predicate/four-level filter, register 10,240 distinct retained names through one provider, and measure identical collections, type changes, clears, inserts/deletes, one-value changes and bursts of eight new names. Stress cleanup verifies bulk deletion, retention cascades and retained dictionary names. Stress results have no 250-ms promise.

Server runs also record one sequential read pass after resetting the owned database buffers: SQL Server uses `CHECKPOINT` and `DBCC DROPCLEANBUFFERS`; PostgreSQL restarts its disposable container and clears client pools. Later requests in the pass can reuse earlier reads. The larger host OS cache remains populated. This pass is informational and does not establish cold-storage latency. Normal profiling and warm-up follow it.

Plans and profiling run outside latency intervals. SQL Server XML includes actual rows, reads, sorts and spills; PostgreSQL uses `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)`. SQLite also executes the captured read-only query through the exact native provider handle to record VM steps, full-scan steps, sorts, automatic indexes, cache counters and spills where available. Counter definitions follow the [SQLite statement](https://www.sqlite.org/c3ref/c_stmtstatus_counter.html) and [database](https://www.sqlite.org/c3ref/c_dbstatus_options.html) APIs. Unsupported counters remain null. Command counters report reader/nonquery batches and parameter counts; nonquery affected-row counts do not include DML returned through readers. SQL Server file I/O and PostgreSQL catalog/WAL counters are captured before/after intervals. SQLite file/WAL lengths are snapshots, not cumulative WAL-write counts.

SQL Server reference cases provision both models' transaction logs to at least 8 GiB before measurement, without shrinking existing larger files. Log allocation is excluded from timing and recorded separately. This avoids comparing a smaller parent-only log that must grow with a larger Dimensions log that already has room. Recovery mode and commit durability are retained. Diagnostic replays retain the archived file sizes so growth remains observable.

The Windows benchmark process requests a one-millisecond timer period for the fixed offered-load scheduler and releases it on disposal. Each preflight records an actual `Task.Delay(1)` probe inside the capped process. This prevents coarse Windows timer ticks from batching scheduled arrivals. Both models use the same timer setting. The scheduler rechecks its monotonic arrival after timer wakes, preventing early dispatch. Rates, queue-delay accounting and the 250-ms rejection deadline remain unchanged. Comparison reports retain up to 128 missed-request details, including requested/actual delay and previous-operation timing, when delivery fails.

For the server reference profile, set `DOTNET_gcServer=1` for the runner process. Both parent-only and Dimensions contexts use SQL Server `MinBatchSize(1)`, so paired metadata/value updates can share a batch. The effective GC mode, SQL setting, and process environment are recorded; both models use the same settings. Comparison and mixed intervals capture GC pause totals and collection indices outside the timed work. A prior pause is attributable to an interval only when the corresponding collection index changes. These observations do not excuse missed offered requests or change acceptance limits.

The sequential matrix script stops on the first failed case and preserves all evidence. Resume requires the same application/storage/provider binaries, runtime configuration file, declared GC environment and workload parameters. A crashed attempt retains its files; resumption uses a new run-owned directory.

```powershell
./benchmarks/Application.Benchmarks/Run-StorageDimensionsQualification.ps1
./benchmarks/Application.Benchmarks/Run-StorageDimensionsQualification.ps1 -Engines sqlite -Features combined -Owners 10000 -Profile sparse -Stress -Smoke
./benchmarks/Application.Benchmarks/Run-StorageDimensionsQualification.ps1 -Output artifacts/storage-dimensions/my-matrix -Resume
```

The default long matrix covers SQL Server and PostgreSQL, each feature independently and together, at all three declared owner counts. Sparse/skew and stress matrices require their own invocations. Allow roughly 35 minutes plus seeding/profiling per full reference case; large datasets can take longer. No builds/tests or other qualification cases may overlap a measured interval.

The report deliberately retains `QualificationComplete: false` until all specification gates are evaluated. This workload alone does not complete the specification. Completion requires the complete engine/feature/size matrix, separate sparse/skew/maximum-size/registration evidence, storage and transaction/log-work assessment, and review of the actual plans. The presence of a runner does not qualify these gates. The existing real-engine integration proofs provide additional plans and mutation evidence but do not replace volume measurements. `assessment.json` evaluates the measured subset; a failing measured gate returns a nonzero exit code. A passing smoke run cannot qualify performance.

Outputs stay under `artifacts/storage-dimensions/`. Owned server containers are disposed in `finally`; SQLite files remain with the evidence. The runner does not target existing application catalogs or delete external resources. No benchmark targets, smoke runs, or partial reports are published as measured guarantees.

For isolated investigations, `--storage-dimensions-diagnostic` accepts the same declared owner counts but uses short diagnostic intervals. It seeds fresh owned data with the current implementation. Its `Smoke` report flag stays true, and it never evaluates volume acceptance. Use the full qualification dispatch for acceptance results.

`--storage-dimensions-metadata-profile --engine sqlite --fixture <completed-combined-fixture> --model parent|dimensions|snapshot|hidden|discard --seconds 60 --output <new-owned-directory>` runs metadata-only diagnostics on a private SQLite backup. Snapshot/hidden/discard models are benchmark-only controls; they do not alter production SQL mapping. The discard control consumes snapshot strings before dropping them, preventing expression/JIT elimination. Optional `--workload all|populated|dimensionless` and `--readers 0..16` isolate competing reads; zero readers selects writer jobs only. The effective GC mode is recorded. These diagnostic controls never change the qualification workload, never qualify latency, and may be sampled separately with EventPipe.

For fast feedback on real dataset sizes, use `-Diagnostic` instead of `-Smoke`. It keeps the requested owner count but shortens intervals and never evaluates performance acceptance. The default engine order is SQL Server, PostgreSQL, then SQLite. For example:

```powershell
./benchmarks/Application.Benchmarks/Run-StorageDimensionsQualification.ps1 -Features combined -Owners 10000 -Diagnostic -Output artifacts/storage-dimensions/quick-server-first
```

Follow with full-length qualification using the same engine order. Three paired ordinary-operation comparisons meet the specification minimum; shortened diagnostic windows do not replace those measurements. Do not run engines concurrently against the shared reference budget.

Use `-Scaling -Features combined` for the prioritized server growth sweep. Default long-run engines are SQL Server then PostgreSQL. Scaling visits both engines at each size before increasing dataset size. It retains three full two-minute mixed query/update intervals, correctness checks, plans, storage footprints and delta counters, while skipping ordinary-operation saturation/comparison and allocation-attribution loops. Those skipped gates remain unqualified. Allow roughly fourteen minutes of measured/warm-up intervals per case plus seeding, archiving and profiling; million-owner seeding can dominate elapsed time.

```powershell
./benchmarks/Application.Benchmarks/Run-StorageDimensionsQualification.ps1 -Scaling -Features combined -Output artifacts/storage-dimensions/server-scaling
```

Compare actual record/value counts, predicate selectivity/matches, fixed page sizes, median p95, throughput, native rows/reads/sorts/spills and index bytes across sizes. Investigate disproportionate latency growth with plans and row counts rather than inferring complexity from timings alone. SQLite performance degradation is accepted; its default coverage is correctness and short diagnostics, not long server-equivalent performance gates. Short modes include SQLite after the two servers; long modes exclude it.

Focused scaling mode now separates selective pages plus dimension writers from broad/count requests plus dimension writers, using three full intervals for each group. `run-*.json` and `broad-run-*.json` record these different workloads. This measures lookup growth separately from broad-query interference. Original mixed-workload failures remain valid; isolated scaling checks do not certify that combined-load target. Normal full qualification retains its original mixed read/count workload.

Full SQL Server fixtures are saved as compressed native backups before timed profiling/writes, with schema hashes and run metadata under the owned artifact directory. Backup/copy time is excluded from latency intervals. These fictional-data backups preserve diagnostic material after container disposal; restoration must verify schema, codec/version, population and seed, then rerun semantic checks and warm-up. A backup is not qualification evidence by itself.

For an owned SQL seed archive, direct dispatch accepts `--restore-seed <archive-directory>`. It checks schema digests, feature/profile/owner metadata and deterministic snapshot kinds/values across scopes and boundaries; native query membership preflight still runs. Restores target only the two fixed catalogs in the newly owned container. Binary assembly hashes are retained as evidence but are not codec identities because this repository stamps each build with the current time.

PostgreSQL qualification explicitly enables and verifies `fsync`, `synchronous_commit` and `full_page_writes`. Its container has a 1-GiB `/dev/shm` limit within the existing 24-GiB total memory cap. Docker's small default shared-memory mount can fail concurrent parallel queries despite available container memory. The effective shared-memory size, parallelism settings and durability settings are recorded. Earlier runs with `fsync=off` remain diagnostic evidence and do not qualify durable-write performance. Compare growing datasets using the same recorded settings.

Full PostgreSQL fixtures also save both fixed catalogs using custom-format `pg_dump` archives before timed work. `-RestoreSeed` supports a single PostgreSQL case, verifies archive and schema SHA256 digests, restores only into that invocation's owned container, and reruns snapshot and query-membership checks. Both server engines preserve their database logs before disposing the owned container; document-provider failures include the underlying exception in the local report.

`--probe-union-all` records a read-only, alternating native SQL experiment and ends before qualification. If the current query already has no UNION, both timing lists sample that same current statement and are labeled accordingly. `--sql-cost-threshold <5..32767>` changes only the run-owned SQL Server instance, records the actual setting, and defaults to 5. An experimental nondefault value is not a deployment recommendation or proof of the latency gate.

For SQL Server update investigations, direct dispatch accepts `--storage-dimensions-diagnostic --profile-mixed` with a matching `--restore-seed`. It alternates five pairs of 30-second mixed intervals using the engine default and serial query execution, with 15-second warm-ups, then records a separate writer-only interval. Writer command and transaction timings, request waits, scheduler queues and statement counters are diagnostic evidence. Detailed writer samples are capped at 20,000 per interval. Instrumentation and server sampling perturb the workload, so this mode never qualifies performance or replaces full qualification.

`--sql-max-dop <0..4>` or runner `-SqlMaxDop` sets and verifies query parallelism only in the run-owned SQL Server instance. Zero retains the engine default. Report the setting with the measured result and use it for both models. A diagnostic setting comparison does not qualify the full workload or establish a deployment recommendation.

`--storage-dimensions-diagnostic --profile-offered-load <previous-ordinary-offered-load.json>` replays that case's frozen ordinary request rates on the same engine/feature/volume. It first reproduces the full saturation-calibration history while retaining the recorded replay rates, then alternates up to five full two-minute pairs after warm-up. It records database file/log/statistics state and SQL growth/statistics/long-wait events retained in a 256-MiB file target, and stops after a delivery failure. It never grants qualification; external runtime tracing can attach to the preflight process ID during this diagnostic.

Offered requests enter bounded shared queues with 16 reader and four server-writer consumers. The next available consumer starts the oldest request, preserving the fixed arrival rates and including all queue delay. This avoids private per-worker streams that can strand requests despite idle consumers. Queue overflow and deadline rejection still invalidate the comparison. `--storage-dimensions-scheduler-proof --engine sqlserver` verifies overload/accounting and heterogeneous-job shared capacity without a database.

On Windows, qualification requests a one-millisecond timer period for its own process. On Windows 11 it also explicitly disables ignoring that request, preserving the previous process policy on disposal. This prevents visibility-driven timer changes from biasing alternating offered-load intervals. Preflight and probes around each ordinary calibration/comparison record the actual delay precision and process policy outside timed work. CPU and memory limits, request rates, queue deadlines and acceptance thresholds remain unchanged. This setting is part of the benchmark environment, not a requirement of the storage providers. See the [Windows process timer policy](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessinformation).

For a short frozen-rate investigation, add `--skip-profile-calibration --offered-profile-pairs 1` to the offered-load diagnostic. This skips the preceding saturation history and records one alternating full-duration pair with SQL query CPU, waits and scheduler samples. It remains diagnostic only and cannot qualify a performance gate or establish a fix for a failure whose preceding workload history matters.

Use runner `-OrdinaryComparisonPairs 5` or direct `--ordinary-comparison-pairs 5` to predeclare five ordinary-operation comparison pairs when interval variation needs more replication. The default remains three, and both satisfy the three-pair minimum. Each measured interval remains two minutes after warm-up; saturation calibration, per-operation allocation attribution, mixed selective intervals, primary median-p95 estimator, delivery deadline and all thresholds are unchanged. Retain failed earlier runs; additional pairs cannot retroactively turn them into passes.


## Matched Dimension contention diagnostic

`Run-StorageDimensionContention.ps1` isolates the update gap at 10k and 100k records per feature. It runs both engines sequentially, with SQL Server cost threshold 50/MAXDOP 1 and the existing enforced resource budget. It creates fresh owned fixtures, uses the original query and writer jobs, and skips unrelated qualification phases and new seed archives. Smoke retains 10k records because the reference selective-query classification requires at least 200 matches.

Three cohorts run at predeclared identical offered rates on both engines: writers alone at 100 writes/second; selective reads plus writers at 200 reads/100 writes per second; and broad queries/counts plus writers at the same rates. Each cohort has five 15-second intervals after three-second warm-ups. Cohort order rotates each repetition; engine order reverses at 100k. The queue deadline remains 250 ms. Missed deliveries remain visible and invalidate a matched latency comparison, even if no provider operation fails.

Reports capture offered/delivered/missed counts, queue delays, writer latency, command counts, catalog work, owned-container CPU deltas and SQL Server statement/wait deltas. CPU collection supports cgroup v1 and v2. Afterwards, separate 15-second saturated diagnostics capture EF command/transaction stages on both engines and SQL request/scheduler samples. Instrumentation and engine-specific saturated throughput make those intervals diagnostic only. No interval qualifies new performance guarantees or establishes an engine-wide ranking.

```powershell
$env:DOTNET_gcServer = '1'
./benchmarks/Application.Benchmarks/Run-StorageDimensionContention.ps1 -Smoke -Output artifacts/storage-dimensions/contention-smoke
./benchmarks/Application.Benchmarks/Run-StorageDimensionContention.ps1 -Output artifacts/storage-dimensions/contention-study
```

Resume requires identical binaries and options, preserves prior attempts and uses a new attempt directory. Do not overlap with builds or other cases. Compare writer-only against selective/broad cohorts within each engine before attributing the cross-engine difference to the update implementation. Existing original mixed-load numbers and earlier failures remain unchanged.


### Completed current-model finalization runs

Both engines completed the full combined million-record/eight-Dimension reference workload, including three ordinary comparison pairs, three mixed-load intervals, allocation attribution, maximum-size/name-growth stress and cleanup. They restored independently prepared index-only copies of the premeasurement archives after verifying generated schemas, deterministic snapshots and query membership. The original archives are unchanged.

| Current-model result | SQL Server | PostgreSQL |
|---|---:|---:|
| Worst targeted median p95 | 125.44 ms | 82.77 ms |
| Maximum ordinary p95 overhead | 14.94% | 2.50% |
| Maximum allocation overhead | 2.86% | 4.35% |
| Representative bytes/value | 528.73 | 408.64 |
| Fixed-load missed requests | 65, accepted recorded window | 0 |
| Operation failures | 0 | 0 |
| Native plans reviewed | 86 | 86 |
| Maximum selective parent rows observed | 960 | 960 |

PostgreSQL passed every strict gate. SQL Server's three ordinary-write overhead failures are blob Properties write 16.53→19.00 ms (+14.94%), blob dimensionless content write 17.21→19.11 ms (+11.04%), and document Properties write 10.47→11.76 ms (+12.32%). The user explicitly accepted these exact outcomes for this release. Their raw comparisons remain failed against the unchanged future 10-percent criterion. No causal index regression or root cause is claimed from a separate-model comparison. All raw results, including failures, are retained.

The exact 65-miss SQL Server comparison window is separately accepted for this release. All other checks passed. Compact measurements, native plans, original failures and explicit acceptances are retained locally under `artifacts/storage-dimensions/retained-evidence/`; reusable diagnostic commands above remain available.
