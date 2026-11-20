---
goal: 'Measure random full-content read scaling and repeat Dimension update measurements'
version: '1.0'
date_created: '2026-11-19'
last_updated: '2026-11-19'
owner: 'bITdevKit maintainers'
status: 'Completed'
tags: ['storage', 'benchmarks', 'performance']
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-green)

Measure complete random blob and document content reads as EF collections grow. Run a separate repetition of the original Dimension update workload. The read matrix contains no Dimensions. This exploratory study adds no production latency guarantee and does not repeat the complete Dimensions qualification.

Completed on 2026-11-19. Release build: zero warnings/errors. Six final smoke cases and all 14 full cases passed. The 12 random-read cases include 72 measured intervals and 1,258,826 verified content reads. Separate historical update repetitions did not improve SQL Server latency. Full observations and cache/coverage limits are retained in the benchmark methodology and ignored case evidence. No production code changed.

## 1. Requirements & Constraints

- **REQ-001**: Run SQL Server and PostgreSQL sequentially, with independent blob/document cases at 10k, 100k and 1m records. Exclude SQLite.
- **REQ-002**: Populate each collection with 70% 1-KiB, 20% 16-KiB, 9% 256-KiB and 1% 1-MiB payloads. Assign sizes by record number modulo 100. Report each class's actual population. One million total records is not one million of each size.
- **REQ-003**: Rotate reads across four size classes, selecting keys uniformly within each class with reproducible per-worker seeds. Report sampled distinct keys; do not use a fixed hot-key subset.
- **REQ-004**: Use `IBlobStoreProvider.DownloadAsync` with full stream consumption and `IDocumentStoreProvider.GetAsync` with full byte materialization. Verify length and SHA256 inside the timed operation. Exclude HTTP, typed deserialization, optional caches and transforms.
- **REQ-005**: Measure one and 16 readers separately, without writers. Use three 30-second intervals, each after five seconds of warm-up. Smoke uses one three-second interval after one second of warm-up and proves correctness only.
- **REQ-006**: Report p50/p95/p99, errors, successful verified bytes, operations/second, MiB/second, unique keys, and all repetitions by size class. Mark percentiles with fewer than 200 successful samples. Freeze each interval's coverage before the next interval starts.
- **REQ-007**: Label results as warmed random reads. Preserve ordinary database and host caches; do not claim cold disk or complete working-set coverage.
- **REQ-008**: Record the configured blob chunk size. All default payloads fit one chunk, so multi-chunk streaming performance remains unmeasured.
- **REQ-009**: Enforce the existing four-CPU/8-GiB application and four-CPU/24-GiB database budgets. Record binaries, runtime, engine settings, seed and physical-host cache. Do not record firmware.
- **REQ-010**: Use owned disposable databases. Before seeding, verify free database-filesystem space for three times raw payload bytes plus 10 GiB. A million-record mixed collection has about 37 GiB of raw content. Preserve small reports rather than creating new large backups.
- **REQ-011**: Repeat the million-record combined Dimension workload with its unchanged jobs, 16 readers/four writers, eight Dimensions per owner, 1-KiB payloads, three 120-second intervals and 15-second warm-ups. Retain SQL Server MAXDOP 1/cost threshold 50 and durable PostgreSQL settings. Restore verified original archives. Skip ordinary comparison, allocation and stress loops, and label this as a targeted recheck.
- **REQ-012**: Preserve original observations and failures. Compare repetitions without treating one lower percentile as proof of a fix. Collect any detailed diagnostics outside the reported intervals.
- **CON-001**: Change benchmark code only. Do not alter production storage behavior, indexes, schema or Dimensions acceptance criteria.
- **CON-002**: Do not overlap measurements with other cases, builds or tests. Stop on failures and preserve evidence.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Implement the independent read workload and targeted update repetition.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-001 | Add random-read and update-recheck dispatch in `benchmarks/Application.Benchmarks/Program.cs` and `StorageDimensionsQualification.cs`, retaining the original qualification path. | Yes | 2026-11-19 |
| TASK-002 | Implement deterministic content, random selection, stream consumption/checksums, empty-Dimensions checks and interval reports in `StorageDimensionsQualification.RandomReads.cs`. | Yes | 2026-11-19 |
| TASK-003 | Implement `RunDimensionUpdateRecheckAsync` using the unchanged original mixed jobs. | Yes | 2026-11-19 |
| TASK-004 | Add `Run-StorageReadScaling.ps1` with sequential execution, fingerprinted resume, failure preservation and logs; document it in `STORAGE-RANDOM-READS.md`. | Yes | 2026-11-19 |

### Implementation Phase 2

- GOAL-002: Verify correctness before volume measurements. Depends on Phase 1.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-005 | Build the Release benchmark. Run server smoke cases and verify every size class, complete bytes, checksums, empty Dimensions, key spread and stream disposal. | Yes | 2026-11-19 |
| TASK-006 | Check frozen per-interval coverage, reader-only scheduling, owned paths and original update job/settings reuse. | Yes | 2026-11-19 |

### Implementation Phase 3

- GOAL-003: Collect and explain the growing-collection evidence. Depends on Phase 2.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-007 | Run the targeted SQL Server million-record update repetition and PostgreSQL control; retain the original measurements beside them. | Yes | 2026-11-19 |
| TASK-008 | Run random reads at 10k, then 100k, then 1m for each engine and feature. Preserve all repetitions and failures. | Yes | 2026-11-19 |
| TASK-009 | Visualize median run-p95 and spread against collection size, with payload/concurrency views, throughput and key coverage. Explain cache/storage effects without claiming asymptotic complexity from three points. | Yes | 2026-11-19 |

## 3. Alternatives

- **ALT-001**: Fixed repeated keys favor caching and reproduce the old coverage gap.
- **ALT-002**: Separate million-record fixtures for every size require about 1 TiB for the largest payload before overhead. Use mixed-size collections and label class populations.
- **ALT-003**: Full Dimensions qualification repeats unrelated hours of work. Reuse only the original mixed intervals as a labelled targeted recheck.

## 4. Dependencies

- **DEP-001**: Existing EF providers, storage entities, Testcontainers and SQL Server/PostgreSQL images.
- **DEP-002**: Existing resource budget, command counters and latency histograms.
- **DEP-003**: Verified seed archives and `artifacts/storage-dimensions/qualification-final-01/assessment.json` for update comparisons.

## 5. Files

- **FILE-001**: `benchmarks/Application.Benchmarks/Program.cs` and `StorageDimensionsQualification.cs`, dispatch and setup reuse.
- **FILE-002**: `benchmarks/Application.Benchmarks/StorageDimensionsQualification.RandomReads.cs`, workloads and reports.
- **FILE-003**: `benchmarks/Application.Benchmarks/Run-StorageReadScaling.ps1`, sequential runner.
- **FILE-004**: `benchmarks/Application.Benchmarks/STORAGE-RANDOM-READS.md`, reproducible methodology.
- **FILE-005**: `artifacts/storage-dimensions/`, ignored case evidence and progress.

## 6. Testing

- **TEST-001**: Server smoke cases validate every payload size and complete bytes. Wrong content, keys, incomplete streams and unexpected Dimensions fail.
- **TEST-002**: Assert exact row count, empty dimension tables and no dimension-table access in captured read commands.
- **TEST-003**: Verify saved unique-key counts never exceed successful interval operations, and individual reports match their combined summary. Retain errors and histogram overflow.
- **TEST-004**: Update repetition retains the original mixed jobs and settings; instrumented diagnostics cannot replace measurement intervals.

## 7. Risks & Assumptions

- **RISK-001**: Working sets can outgrow the database buffer budget, causing expected latency changes that do not imply inefficient key lookup.
- **RISK-002**: The larger physical host retains OS caches; local caps do not establish an isolated 32-GiB VM or cold-disk result.
- **RISK-003**: Seed/log storage and setup can dominate runtime. Report setup separately and check available space.
- **RISK-004**: The 1-MiB class has 100 owners at 10k and 10,000 at 1m, so its cache behavior differs with collection size.
- **ASSUMPTION-001**: The goal is a practical scaling picture, not an added SLA or general engine ranking.

## 8. Related Specifications / Further Reading

- [Random-read methodology](../benchmarks/Application.Benchmarks/STORAGE-RANDOM-READS.md)
- [Dimensions benchmark](../benchmarks/Application.Benchmarks/STORAGE-DIMENSIONS.md)
- [Blob Storage](../docs/features-storage-blobs.md)
- [Document Storage](../docs/features-storage-documents.md)
