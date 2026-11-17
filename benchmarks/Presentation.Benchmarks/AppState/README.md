# AppState performance and allocation validation

Run from the repository root with other builds, hosts, and load tests stopped. The fixture uses fictional catalog preferences, copied memory providers, and native DI. It does not measure browser paint, JS transport, backend queries, or AOT execution.

```sh
dotnet run -c Release --project benchmarks/Presentation.Benchmarks -- appstate --filter '*AppState*Benchmarks*' --artifacts .tmp/appstate-benchmarks/correctness --exporters fulljson github
dotnet run -c Release --project benchmarks/Presentation.Benchmarks -- appstate-allocations --output .tmp/appstate-benchmarks/async-allocations.json
```

Each BenchmarkDotNet class uses `MemoryDiagnoser`, three launches, five warmup iterations, fifteen measured iterations, and a 100 ms iteration target. This retains repeated measurements while keeping the complete workload matrix practical. Dry/Short runs do not establish a performance baseline. Use a working .NET SDK in the child process's PATH and DOTNET_ROOT; passing an extra CLI job can duplicate attribute-defined jobs.

The snapshot cases use 16 and 256 selected GUIDs. JSON fallback, generated metadata, and manual copying preserve the same independently mutable model. Reads, version checks, and stale conditional rejection measure their allocation-free paths separately from copying. Transition and selector cases vary observer counts. History uses bounded capacities. Ownership varies sibling inventories. Persistence separates key construction, missing reads, explicit acknowledged writes, and bursts coalesced by an explicit flush. It does not sleep through a real debounce interval. Diagnostics cases measure disabled, metadata-only, and explicitly projected steady-state buffers.

`AppStateHandoffBenchmarks` uses the same two payload sizes. `Capture` includes independent model copying and logical/context key construction. `InitializeFreshOwner` includes state construction, guarded initialization and disposal. `NativeRoundTrip` includes native persistence services, a minimal renderer, callback ownership, model JSON, the native string envelope, copied store payloads and fresh-owner `LastSnapshot` restoration. These costs are separate from ordinary in-memory updates. The workload excludes HTTP, browser activation, protected Server transport, circuit retention and rendering latency.

The original-code baseline is retained under `.tmp/appstate-benchmarks/before`. Its attribute-defined job used the original 500 ms iteration target. An additional CLI-generated job is identified separately in those raw reports. Compare equivalent snapshot/ordinary-update behavior with that methodological difference visible. Old disabled snapshots, broken undo/redo, and no-op explicit saves are not valid equivalence baselines.

Store regression baselines and measured JSON locally. Generated benchmark measurements, collections and evidence are ignored by Git. One-off comparison scripts and wrappers are also local helpers, so a fresh checkout does not include them.

Compare equivalent case/environment pairs. Flag time above the greater of 115% of baseline mean or baseline mean plus three baseline standard deviations. Allocation flags use the greater of 105% of baseline bytes or baseline bytes plus 32. Repeat a flagged case twice before deciding whether it is a reproducible regression. Preserve the previous baseline and explain an intentional replacement in the local verification record. Include source, workload, environment and job fingerprints in that baseline; it is not a universal latency promise.

The separate finite allocation experiment warms 1,000 operations and measures five 10,000-operation batches. It waits for owned work and samples process-wide allocated bytes, GC collection deltas, and post-cleanup heap size. An idle harness sample stays separate. Current-thread counters alone cannot describe worker allocations; retained heap is not allocated bytes per operation. Cleanup inventory counts must return to baseline. Heap samples support investigation and do not prove absence of every leak.

Functional assertions remain ordinary tests. Faster results cannot justify removing independent copies, revision guards, partition checks, or disposal.
