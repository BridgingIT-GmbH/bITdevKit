"""Rebuild the capacity report from preserved measured trials, without running workloads."""

import collections
import json
import pathlib
import statistics

directory = pathlib.Path(__file__).resolve().parents[1] / "evidence/profiling-runtime-and-operations-1/phase-12"
original = json.loads((directory / "performance/results.json").read_text())
updated = json.loads((directory / "performance-memory-retention/results.json").read_text())
results = [row for row in original if row["provider"] != "memory"] + updated
groups = collections.defaultdict(list)
for row in results:
    groups[(row["provider"], row["scenario"])].append(row)
    for field in ["failedRequests", "schedulerShed", "captureFaults"]:
        assert row["metrics"][field] == 0, (row["provider"], row["scenario"], field)
    assert row["metrics"]["exemplarPayloadBytes"] <= 4096
    assert row["metrics"]["unknownWithinWindow"] == 0
    if row["scenario"] == "nested-p010":
        assert row["metrics"]["endQueueRecords"] <= 512
        assert row["metrics"]["dashboardPollFailures"] == 0
for provider in ["memory", "sqlite", "sqlserver", "postgres"]:
    for case in ["omitted", "disabled", "empty", "nested", "nested-trace", "saturated", "nested-p010"]:
        assert len(groups[(provider, case)]) == 3, (provider, case)
assert len(results) == 84


def median(rows, metric):
    return statistics.median(row["metrics"][metric] for row in rows)


text = """# Measured profiling capacity and limits

The accepted matrix contains 84 trials: three repetitions of six baseline cases and the tested 10% sampling adjustment for each of memory, SQLite, SQL Server and PostgreSQL. The original four-provider matrix passed; the 21 memory trials were rerun after correcting append-time retention eviction reporting. The accepted memory rows come from `performance-memory-retention/`; the EF rows come from `performance/`. Both directories retain their raw measurements, environment, gzip latency arrays and incremental summaries. The original memory rows are historical evidence, not the accepted retention-count results.

Each trial offered 1,000 operations/s for 60 measured seconds after 10 seconds warm-up. The workload runs the real optional pipeline behavior, generic operation recorder, four nested/parallel segment invocations (except root-only/control cases), a 4 KiB charged-record bound, periodic writer/retention and dashboard polling. It measures the general operation core through loopback HTTP/1.1, **not** the larger HTTP metadata projection. Trace uses a formatting/counting sink without disk I/O. Endpoint percentiles and allocations include the collocated client, Kestrel and test process; allocation is not recorder-only. GC was workstation, with eight reported processors and .NET 10.0.12. Exact engine versions: SQLite 3.53.3, SQL Server 16.0.4265.3, PostgreSQL 16.15.

The full matrix spans the explicit pause/resume with validated environment/workload and hashes of reused raw artifacts. The memory rerun is a separate recording session after the counter correction. These measurements establish this fixture's behavior; machine load, data volume, queries, real logging sinks, network transport and workload complexity can change results. No universal overhead percentage or production throughput guarantee follows from them.

## Endpoint and persistence results

Values are medians of the three per-trial measurements; p95 columns are medians of trial p95 values, not population percentiles. Drops, unknowns and removals are sums across the three measured windows. Persisted/s is observed before unmeasured draining and can include earlier warm-up backlog. Retention removals are node-local observations, distinct from incoming diagnostic loss.

| Provider | Case | p50 ms | p95 ms | Completed/s | Persisted/s | Diagnostic drops | Unknown commits | Retention removals | p95 delta vs omitted % |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
"""
for provider in ["memory", "sqlite", "sqlserver", "postgres"]:
    baseline = groups[(provider, "omitted")]
    for case in ["omitted", "disabled", "empty", "nested", "nested-trace", "saturated", "nested-p010"]:
        rows = groups[(provider, case)]
        delta = (median(rows, "latencyP95Ms") / median(baseline, "latencyP95Ms") - 1) * 100
        text += f"| {provider} | {case} | {median(rows, 'latencyP50Ms'):.3f} | {median(rows, 'latencyP95Ms'):.3f} | {median(rows, 'achievedPerSecond'):.1f} | {median(rows, 'persistedPerSecond'):.1f} | {sum(r['metrics']['droppedWithinWindow'] for r in rows):.0f} | {sum(r['metrics']['unknownWithinWindow'] for r in rows):.0f} | {sum(r['metrics']['retentionRemovedWithinWindow'] for r in rows):.0f} | {delta:.1f} |\n"

text += """
## Allocation, heap and queue observations

Allocation per successful completion is gross process allocation during the window. Retained heap change is measured after full collection at the window boundaries; it can be negative and does not identify an individual operation's allocation. Peak queue includes queued and in-flight records. The age metric is the maximum sampled oldest age, not a percentile. Raw samples retain poll durations/statuses and full before/after/post-drain health.

| Provider | Case | Alloc KiB/completion | Alloc delta % | Heap change MiB | Peak queue records | Peak queue MiB | Peak oldest age ms | End queue (median) | Poll failures (sum) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
"""
for provider in ["memory", "sqlite", "sqlserver", "postgres"]:
    baseline = groups[(provider, "omitted")]
    for case in ["omitted", "disabled", "empty", "nested", "nested-trace", "saturated", "nested-p010"]:
        rows = groups[(provider, case)]
        delta = (median(rows, "allocatedBytesPerCompletion") / median(baseline, "allocatedBytesPerCompletion") - 1) * 100
        text += f"| {provider} | {case} | {median(rows, 'allocatedBytesPerCompletion') / 1024:.2f} | {delta:.1f} | {median(rows, 'retainedHeapChangeBytes') / 1048576:.2f} | {max(r['metrics']['peakQueueRecords'] for r in rows):.0f} | {max(r['metrics']['peakQueueBytes'] for r in rows) / 1048576:.2f} | {max(r['metrics']['peakQueueAgeMs'] for r in rows):.1f} | {median(rows, 'endQueueRecords'):.0f} | {sum(r['metrics']['dashboardPollFailures'] for r in rows):.0f} |\n"

text += """
## Interpretation and explicit adjustment

- All accepted trials preserved application responses: zero failed/shed offers and zero capture faults. Unknown persistence outcomes were zero in these runs; correctness tests separately exercise unknown commits and cancellation-ignoring providers.
- Default nested memory capture kept up with the offered rate without incoming drops. Its 10,000-root retention cap evicted older history; the corrected counter reports those removals on subsequent successful maintenance observations. An end-of-window queue larger than one batch also triggers the conservative sampling experiment, even when the writer is keeping up.
- SQLite nested capture produced variable backlog; SQL Server and PostgreSQL nested capture could not sustain 1,000 stored profiles/s with these defaults. Their bounded queues eventually dropped incoming diagnostics while application responses continued. This is an observed persistence limit, not evidence that every workload or deployment has that limit.
- A **tested explicit** configuration is `WithRequestProfiling(requests => requests.WithSampling(sampling => sampling.Probability(0.1)))`. The fixture exercises that prepared head-sampling policy at generic-owner admission. In all providers' three adjustment trials it recorded no drops or unknown commits and ended below one batch of queued work. Request sampling applies to eligible HTTP requests in applications; manual operations are not automatically HTTP sampled. The feature default remains AllRequests.
- Saturation cases deliberately used eight queued records, eight-record batches and a ten-second flush interval. They demonstrate bounded overflow and visible loss rather than sustainable throughput.
- Control-case dashboard polls returned 503 (profiling unavailable). Enabled-case failures, including bounded/too-large analyses, remain visible in raw statuses and the table. At 10% sampling, all adjustment trials had zero polling failures. Dashboard reads do not force a flush.
- Lifecycle Trace formatting was exercised and counted. Real file/network logging sinks were not measured. SQL request profiling payloads, TLS, remote network latency and application-specific heavy work require their own capacity tests.

Scheduler ceilings are proven independently of database speed by `OperationProfilingWriterTests`, including `DefaultTick_AtMostEight512RecordBatches_LeavesLaterWorkQueued`, fixed arrival watermarks, next-batch time budget, ignored timeout ownership, retries and bounded shutdown. The passing 341-test focused Common log includes these checks and the new retention reporting regressions. Eight starts of 512 records are a scheduling ceiling; this report never treats 4,096 records/s as observed throughput.

Reproduce the tables with `python3 plan/profiling/report-capacity.py`. Canonical workloads are opt-in; smoke mode is not acceptance evidence. The benchmark supports explicit validated provider subsets and resume of matching completed artifacts, with interrupted trials run again.
"""
(directory / "capacity-report.md").write_text(text)
print("Verified accepted matrix: 84 trials; capacity-report.md regenerated.")
