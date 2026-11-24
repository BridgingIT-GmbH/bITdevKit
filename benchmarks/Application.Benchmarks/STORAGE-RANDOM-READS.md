# Random Content Read Scaling

This exploratory benchmark measures full-content EF provider reads by key. It runs SQL Server and PostgreSQL at 10k, 100k and 1m records, independently for blobs and documents. Dimensions and Properties are absent from the fixture. It makes no new latency guarantee and does not repeat the complete Dimensions qualification.

Each collection contains 70% 1-KiB, 20% 16-KiB, 9% 256-KiB and 1% 1-MiB payloads. Readers rotate across size classes and select uniformly random keys within each class, using recorded independent worker seeds. Results are per class, including its actual population and sampled unique keys. A million-record collection is not one million records of each size.

The operation includes the EF provider call, complete blob stream consumption or document byte materialization, and SHA256 verification against the seeded bytes. It excludes HTTP, typed document deserialization, optional caches and transforms. Each record has a unique prefix and reproducible random body. No compression is configured. Every default payload fits the normal 4-MiB blob chunk; these results do not cover multi-chunk streaming.

Run one and 16 readers separately, with no writers. Three 30-second intervals follow five seconds of warm-up each. Report p50/p95/p99, all repetitions, verified bytes, operations/second, MiB/second, errors and unique keys. A percentile with fewer than 200 successful samples is marked as having insufficient samples. Histogram overflow above two seconds is retained. Smoke intervals use one three-second repetition and establish correctness only.

These are warmed random reads with engine and physical-host caches retained. They are not cold-storage measurements, and a fixed resource budget does not remove the physical host's OS cache. Read locality changes as collections and size-class populations grow. Raw content is about 37 GiB per million-record feature; preflight checks the owned database filesystem for three times raw payload bytes plus 10 GiB before seeding. Setup time is separate from read timings.

The runner first repeats the original million-record combined Dimension workload for SQL Server and PostgreSQL using verified seed archives. It retains the original mixed read/count jobs, 16 readers/four writers, three 120-second intervals and 15-second warm-ups. SQL Server retains MAXDOP 1 and cost threshold 50. This targeted recheck skips ordinary comparison, allocation attribution and stress loops. Original observations and failures remain unchanged. Random reads then run at each growing collection size, sequentially by engine and feature.

Build before starting any measured workload:

```powershell
dotnet build benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -c Release --no-restore --nologo /p:UseSharedCompilation=false
$env:DOTNET_gcServer = '1'
./benchmarks/Application.Benchmarks/Run-StorageReadScaling.ps1 -Smoke -Output artifacts/storage-dimensions/read-smoke-01
./benchmarks/Application.Benchmarks/Run-StorageReadScaling.ps1 -Output artifacts/storage-dimensions/read-scaling-01
```

Use `-RandomOnly` or `-UpdateOnly` to select one track. `-Resume` requires matching binaries, runtime configuration, GC setting and options; prior failed/interrupted evidence is retained. Each case owns disposable databases and preserves small reports and logs, without creating new large fixture archives. No builds, tests or other workloads may overlap the measurements.

Compare median run-p95 and its spread at the same payload size and concurrency across collection sizes. Throughput by class is measured while all four classes share readers, not as an isolated per-size capacity test. Show key coverage and cache limitations alongside latency. A faster new update observation alone does not establish a fix or explain the old gap.

See the [implementation plan](../../plan/pln-feature-storage-random-reads-1.md) and the separate [Dimensions benchmark](STORAGE-DIMENSIONS.md).

## Measured outcome, 2026-11-19

All 12 full random-read cases and both targeted update repetitions completed without operation errors. The read cases contain 72 measured intervals, 1,258,826 successful full-content reads and 389 GiB verified by SHA256. Individual and combined interval reports agree; unique-key counts, sample counts, resource budgets and empty Dimensions were checked. Every per-size interval has at least 200 successful samples and no latency-histogram overflow.

At 16 readers, one-million-record 1-KiB reads ranged from 8.1 to 9.4 ms median run-p95 across the two features and engines. The 1-MiB range was 63.5 to 73.2 ms. Growing from 10k to one million records increased those 1-MiB timings by about 6–21%. Single-reader large-content reads showed a stronger working-set effect: SQL Server 1-MiB p95 rose from about 6.7 to 20–21 ms; PostgreSQL rose from about 7 to 9–10 ms. SQL Server's million-blob intervals recorded data-file reads absent at 100k, supporting cache pressure as a contributor. These are warmed mixed-size workloads, not isolated per-size capacity, cold-storage proof or a general engine ranking.

The update repetition did not improve SQL Server's gap: blob/document median run-p95 was 140.08/138.62 ms, versus original selected values 132.08/131.45 ms. PostgreSQL was 21.99/21.57 ms, versus 23.39/22.14 ms. Three repetitions per session and separate historical sessions do not establish a regression, improvement, or a causal explanation. The original results remain retained.

Evidence stays in the ignored `artifacts/storage-dimensions/read-scaling-01/` directory: `progress.json`, audited `summary.json`, each case's original interval reports and `results.html`. Fixture creation was outside timed work. Both owned engine containers were disposed, and no benchmark processes remained after completion.
