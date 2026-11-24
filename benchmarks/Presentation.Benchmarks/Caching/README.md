# Cache HTTP transfer experiment

The explicit `caching` selector starts a real Kestrel listener on a dynamically assigned loopback port. Requests pass through the optional cache endpoints, ordinary requester handlers and the InMemory complete provider. Every successful transfer checks all bytes against a deterministic payload.

## Run the experiment

```powershell
dotnet run -c Release --project benchmarks/Presentation.Benchmarks -- caching --output benchmarks/Presentation.Benchmarks/Caching/artifacts/http-4k-c8.json --rounds 5 --iterations 100 --concurrency 8 --payload 4096
```

Arguments are option/value pairs. The runner accepts 5–100 rounds, 1–100,000 iterations per worker, 1–128 workers, and 1–1,048,576 payload bytes. The default is five rounds, 100 transfers per worker, eight workers, and 4,096 bytes per resource. The existing streaming runner still handles no arguments or its existing output-path argument.

Before timing, the runner proves anonymous access is denied, collection opt-out returns the API's 400 validation response, a positive resource supplies an ETag, a conditional GET returns 304, and a stale conditional write returns 412. It then performs 32 complete warm-up transfers. Its local authentication handler is a loopback test fixture, not a production authentication recommendation.

Each measured round records operation and successful-entry rates, bytes, complete and first-byte p50/p95/p99 latencies, process-wide allocations, and generation 0/1/2 collection counts. The output also records .NET/OS, processor count, GC mode, effective finite limits, resident/transient accounts and process working set. Requests fail fast on HTTP errors, missing bytes or content mismatch; a failed run must not be published as successful throughput.

Allocations cover the complete shared client/server process, including the test client's receive buffer, HttpClient, Kestrel and cache. They are not cache-only allocations. First-byte latency starts before sending the request and ends when the first body bytes arrive. Complete latency ends only after EOF and exact byte verification.

Keep the raw JSON with the command, source revision, hardware and storage profile. Report median and range across rounds. High variance makes comparisons inconclusive. This InMemory/loopback workload does not establish database, shared filesystem, external network, TLS, production authentication or large persistent-data-set performance.
