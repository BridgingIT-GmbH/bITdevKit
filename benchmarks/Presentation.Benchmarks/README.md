# Presentation Benchmarks

These experiments use fictional inventory records. Run them from the repository root in Release mode, with other builds and load tests stopped.

```sh
dotnet run -c Release --project benchmarks/Presentation.Benchmarks
dotnet run -c Release --project benchmarks/Common.Benchmarks -- --filter '*NdjsonRecordBenchmarks*' --artifacts docs/validation/streaming-experiments/records
```

The HTTP command writes `docs/validation/streaming-experiments/http.json`. Pass a different JSON path as its single application argument. Raw results stay in the ignored validation directory.

The HTTP experiment compares buffered JSON, streamed JSON arrays, raw NDJSON, envelope NDJSON, and two explicit batch configurations. Every format uses the same producer, records, JSON naming policy, and validation. Full-format warm-up rounds run for at least five seconds. Five rotated measured rounds each transfer 20,000 records of about 1 KiB. One paced run per format produces 50 records with a 2 ms delay per record. First-record latency excludes headers and heartbeat controls. Record lag runs from producer timestamp to client observation.

Two additional runs pause one or eight consumers of long-lived streams with 64 KiB records. After a one-second settling period, they measure another second of producer progress and managed heap change, then wait for producer cancellation cleanup. The source does not prebuild its output. A plateau is evidence for these runs, not a general memory bound or a leak test.

Server and client share a process and use HTTP/1.1 loopback without TLS, compression, or a proxy. Managed heap samples include both sides, transport, and pooled buffers. Forced collections occur outside transfer timings. The JSON baselines have no NDJSON envelope, frame-size enforcement, or protocol deadlines. Treat their throughput as context, not equivalent functionality.

The BenchmarkDotNet cases isolate warm encoding, envelope encoding, borrowed framing, and typed reading for small, escaped Unicode, and 64 KiB payloads. Encoding includes validation and disposal. Reading reports per-record values from 128-frame inputs. Warm pool allocations do not describe retained memory or cold-start cost. `ShortRun` supports a small local experiment; use longer runs before drawing conclusions from small timing differences.

See the [feature guide](../../docs/features-presentation-streaming.md#performance-experiments) for measured outcomes and configuration guidance. The experiments have no timing thresholds and are not CI performance gates. Protocol regression checks remain ordinary tests:

```sh
dotnet test tests/Common.UnitTests --filter 'FullyQualifiedName~Ndjson'
dotnet test tests/Presentation.UnitTests --filter 'FullyQualifiedName~Ndjson'
```
