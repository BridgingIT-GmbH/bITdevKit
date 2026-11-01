# Presentation Streaming

[TOC]

## Overview

Presentation Streaming provides finite exports and live HTTP responses that clients process incrementally. The first supported wire format is NDJSON, which sends one UTF-8 JSON value per line. Its implementation uses `Common.Serialization`, Minimal API results in `Presentation.Web`, and `HttpClient` helpers in `Common.Utilities`.

This guide groups shared streaming concepts and format-specific APIs under one feature. The APIs and configuration below apply to NDJSON. Additional formats can be documented here when supported. All sample domain models, producer services, and routes are fictional. The DevKit APIs are real. Streaming has no Auditing dependency.

NDJSON is a community format, with `application/x-ndjson` as its media type. It is distinct from RFC 7464 JSON Text Sequences. A response consumed by an SPA is ordinary streamed HTTP content. It becomes an attachment only when the endpoint explicitly configures a download filename.

## Challenges

A JSON array client often buffers the whole response. Long-lived streams also need cancellation, bounded memory, idle activity, and a way to distinguish successful completion from a broken connection. A flush alone proves neither client delivery nor business completion.

## Solution

The NDJSON implementation uses `NdjsonWriter` to encode a bounded record once; `NdjsonReader` preserves received framing. `NdjsonHttpResult<T>` coordinates production, writes, flushing, deadlines, and cleanup. Raw streams contain application values. Optional version 1 envelopes add heartbeat, completion, and terminal-error records. `NdjsonHttpResponse<T>` owns one response and validates the selected protocol as the application pulls records.

## Key Features

- Finite `IEnumerable<T>` and finite or live `IAsyncEnumerable<T>` sources.
- Strict framing, original-byte leases, and a configurable 1 MiB JSON record limit.
- Flush per record by default; bounded batching is an explicit option.
- Endpoint metadata and OpenAPI record schemas separate from the streamed text body.
- One terminal observer with confirmed write/flush prefixes and cleanup status.
- .NET clients, including Blazor, with explicit ownership and no automatic reconnect.

## Architecture

The Common types have no HTTP dependency. The Presentation coordinator maintains one pending source move and one ordered writer. Each producer owns independent scopes for bounded database batches, maps detached DTOs, releases those scopes, and then yields. `IRequester` validates and prepares the source before HTTP admission. Its transaction, retry, and handler timeout behaviors do not wrap deferred enumeration.

Client framing and server encoding share the Common contracts. The HTTP client chooses one exclusive reading path: typed raw values, original raw frames, envelopes, or envelope data. A handle cannot be enumerated twice.

## Use Cases

Use raw mode for exports and streams whose application protocol already defines completion. Use envelopes when callers need idle heartbeats and explicit completion/error semantics. A live tick source demonstrates transport only; it supplies no persistence, replay, durable cursor, or exactly-once guarantee. Offset-paged exports have stable ordering but do not promise a snapshot while records change.

## Basic Usage

The fictional inventory API exports two stock records. The same source can supply raw lines or version 1 envelopes:

```csharp
using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNdjsonStreaming();
var app = builder.Build();

app.MapGet("/api/inventory/stream", () => NdjsonHttpResult.From(ReadStockAsync()))
	.WithNdjson<StockRecord>();

app.MapGet("/api/inventory/stream/enveloped", () => NdjsonHttpResult.From(ReadStockAsync()))
	.WithNdjson<StockRecord>(options => options.Mode = NdjsonMode.Envelope);

app.Run();

static async IAsyncEnumerable<StockRecord> ReadStockAsync(
	[EnumeratorCancellation] CancellationToken cancellationToken = default)
{
	foreach (var record in new[] { new StockRecord("ITEM-100", 12), new StockRecord("ITEM-200", 7) })
	{
		cancellationToken.ThrowIfCancellationRequested();
		yield return record;
		await Task.Yield();
	}
}

public sealed record StockRecord(string Sku, int Quantity);
```

The raw endpoint produces these lines:

```json
{"sku":"ITEM-100","quantity":12}
{"sku":"ITEM-200","quantity":7}
```

For a prepared application query, map a `Result<IAsyncEnumerable<StockRecord>>` with `result.MapHttpNdjson()`. The mapping overloads also accept `IEnumerable<T>`, `List<T>`, and arrays. Failed Results use existing HTTP error mapping without enumerating the source. Authenticate, authorize, and validate requests before execution when exposing protected data.

Consume the enveloped endpoint with a caller-owned `HttpClient` whose base address points to the inventory API:

```csharp
var opened = await http.GetNdjsonAsync<StockRecord>("/api/inventory/stream/enveloped",
	new NdjsonClientOptions { ExpectedMode = NdjsonMode.Envelope }, ct);
if (opened.IsFailure)
{
	// Inspect NdjsonClientError.ReasonCode and StatusCode.
	return;
}

await using var response = opened.Value;
try
{
	await foreach (var record in response.ReadDataAsync(ct))
	{
		Console.WriteLine($"{record.Sku}: {record.Quantity}");
	}
}
catch (NdjsonStreamException error)
{
	Console.WriteLine(error.ReasonCode);
}
```

`OpenNdjsonAsync<T>(request, options, ct)` also supports POST and preserves caller headers and request ownership. `GetNdjsonAsync` owns its internally created GET request. Neither overload disposes the caller's client or changes its timeout. Use a suitable timeout on a dedicated client for long-lived operations. `ResponseHeadersRead` makes the HTTP client timeout govern opening; the complete-frame timeout governs subsequent reads.

## NDJSON wire modes and admission

Raw responses contain compact values followed by LF, including null values. Nested arrays/dictionaries remain a single record. The server emits `Content-Type: application/x-ndjson`, `Cache-Control: no-store`, and `X-Ndjson-Mode: raw`. Raw headers start immediately after admission and option validation, before the first producer move. HTTP 200 confirms admission. A first-record encoding/source failure therefore aborts that response.

Envelope responses declare `X-Ndjson-Mode: envelope` and `X-Ndjson-Envelope-Version: 1`:

```json
{"version":1,"kind":"heartbeat"}
{"version":1,"kind":"data","data":{"sku":"ITEM-100","quantity":12}}
{"version":1,"kind":"completed","recordCount":1}
```

The initial heartbeat follows admission. Further heartbeats occur after 15 seconds without output. Raw mode injects no controls or empty lines. Envelope structural names stay fixed and case-sensitive, independently of application naming policies and converters. The application JSON configuration applies to `data`. Completion counts only data, using a nonnegative `long`.

A safe error has `kind: "error"` with required `error.code` and `error.message`, and optional `error.traceId`. It terminates the stream. Common and HTTP envelope readers throw on the same pull that parses it; they never yield the error. `ReasonCode` is `stream_terminal_error`, `RemoteErrorCode` holds the remote code, and `TraceId` holds the optional correlation value. The exception message is always `NDJSON stream terminated with an error.` It never includes the remote message or JSON payload.

`ReadEnvelopesAsync` yields heartbeat, data, and completion. `ReadDataAsync` yields only data. Missing completion produces `stream_incomplete`; a valid completion releases HTTP resources without waiting for EOF. Duplicate reserved fields, unsupported versions/kinds, invalid shapes, and inconsistent completion counts fail validation. Unknown non-reserved version 1 fields are ignored.

## NDJSON options and deadlines

Configure global `NdjsonStreamingOptions` through `AddNdjsonStreaming`; override through `.WithNdjson<T>(...)` or factory `NdjsonEndpointOptions`. Effective settings are captured for execution. Contradictory declared modes, models, or compression policies fail before success output. Declare compression with `WithNdjson`; a result cannot change a compression decision already made by middleware. Prefer declaring endpoint settings with `WithNdjson` so compression and documentation see the same contract.

| Setting | Default | Meaning |
| --- | --- | --- |
| `MaxRecordBytes` | 1,048,576 | JSON bytes, including envelope overhead, excluding delimiter |
| `Mode` | Raw | Application values or shared envelopes |
| `HeartbeatInterval` | 15 seconds | Envelope idle output interval |
| `WriteTimeout` | 30 seconds | Each awaited response start, write, or flush |
| `CleanupTimeout` | 5 seconds | One budget for pending source settlement and disposal |
| `ObserverTimeout` | 5 seconds | Fresh terminal reporting budget |
| `MaxDuration` | null | No overall duration cap |
| `BatchByteLimit`, `BatchMaxDelay` | null | Both required to enable batching |
| `EnableCompression` | false | Explicit host compression opt-in |
| `DownloadFileName` | null | No attachment by default |
| `JsonOptions` | host HTTP JSON options | Application payload serialization |

Endpoint nullable values inherit defaults. `ClearMaxDuration=true` removes an inherited duration cap. `DisableBatching=true` removes inherited batching. A duration override cannot also clear it; batching cannot also be disabled in the same override. Timer values must be positive and at most `uint.MaxValue - 1` milliseconds. Record caps must be positive and at most `int.MaxValue - 4096`.

The batch timer starts with the first staged frame and never resets on later arrivals. It flushes even while the producer is idle or continuously ready. Byte capacity also triggers flushing. Controls flush pending output immediately. A record larger than the batch capacity bypasses batching after pending output flushes.

Client `NdjsonClientOptions` captures its own independent limit, JSON settings, expected mode, and clock. Envelope inactivity defaults to 45 seconds; raw inactivity defaults to disabled and can be set with `RawInactivityTimeout`. The budget covers a complete valid frame while awaiting a pull. Partial trickles do not reset it. Time spent processing a yielded record is excluded. Server headers never raise client limits.

## Exact bytes and bounded memory

```csharp
var writer = new NdjsonWriter(options: new() { MaxRecordBytes = 65536 });
using var frame = await writer.EncodeRecordAsync(value, ct);
if (frame.FramedBytes.Length <= remainingBudget)
{
    digest.AppendData(frame.FramedBytes.Span);
    await writer.WriteEncodedRecordAsync(frame, destination, ct);
}
```

`NdjsonEncodedRecord` owns one pooled buffer. Dispose it after the awaited write. `WriteEncodedRecordAsync` leaves the lease and destination open and does not flush. `WriteRecordAsync` encodes and releases its lease; sequence `WriteAsync` handles flushing and cleanup. `NdjsonHttpResult.FromEncodedRecords<TRecord>` transfers each yielded owner to the executor, which writes those bytes and releases it. The producer owns any frame it does not yield. This path is raw-only; `TRecord` supplies metadata.

`JsonBytes`, `DelimiterBytes`, and `FramedBytes` are views over the same frame. Received `NdjsonReadRecord` views preserve whitespace, escaping, and LF or CRLF. Borrowed views expire at the next pull or disposal; do not retain a memory slice beyond that lifetime. Typed reading uses the same strict framer and returns independent values. Caller-owned Common streams stay open.

The logical JSON cap is enforced on every buffer commit before destination writes. `Utf8JsonWriter` can reserve more space than it commits. Its bounded reservation allowance is `3 * MaxRecordBytes + 4096`, plus ArrayPool bucket rounding, to accept exact-limit ASCII values without measuring by reserialization. Growth may temporarily retain the previous pooled allocation while copying. Delimiters consume at most two extra logical bytes. Producer graphs, custom converter allocations, explicit batch buffers, and HTTP buffers have separate responsibilities. No whole-response buffering is used.

Strict reading rejects BOM, invalid UTF-8, malformed JSON, multiple values, empty lines, and an unterminated final record. `SkipEmptyLines` and `AllowUnterminatedFinalRecord` are explicit compatibility flags, both false by default. Neither skips malformed records or disables the size limit.

## Cleanup and terminal reporting

Attach a per-execution observer with `.WithObserver(observer)` before execution. `INdjsonStreamObserver.OnFinishedAsync` receives one `NdjsonStreamOutcome` after final output and cleanup settlement or grace expiry. It includes status, `SourceCompleted`, `CleanupCompleted`, confirmed written/flushed record and framed-byte counts, `PartialWritePossible`, failure stage, and safe reason code. Controls count as transport records. Staged bytes do not count as writes; a successful flush confirms only the contiguous written prefix.

The first execution failure is preserved. A cancellation is Canceled only if cleanup succeeds. Cleanup failure/timeout makes it Failed. Pending `MoveNextAsync` must settle before the single disposal attempt; disposal never overlaps a move. On grace expiry HTTP aborts and reports `CleanupCompleted=false`. Late settlement only releases leases and observes failures. It never starts more output, retries disposal, changes the outcome, or notifies again. An already pending opaque write may still settle after abort; storage remains leased until it does.

Arbitrary synchronous blocking code cannot be preempted. Producers and observers must cooperate with cancellation, use independent scopes, and avoid capturing request-scoped resources for deferred cleanup. Observer failures/timeouts are separate reporting failures and never rewrite the transport outcome. HTTP execution uses the host's `ILoggerFactory` for structured safe counts and reason codes. Standalone writers can receive an optional `ILogger` through the `logger` constructor parameter. Exception objects, payloads, and remote messages are excluded; logging failures cannot change output status.

A transport Succeeded result can describe a deliberately byte-limited business Partial export. It is not a delivery acknowledgment. A consumer must verify its own completion record/digest and counts; an observer must not publish business success after a failed final flush or cleanup.

## Failure reference

Opening failures return `NdjsonClientError` with `stream_http_error`, `stream_invalid_content_type`, `stream_unsupported_envelope`, `stream_input_timeout`, or `stream_canceled`, plus an optional HTTP status. Error-body reads are capped at 4,096 bytes and discarded.

Reading uses `stream_invalid_record`, `stream_record_too_large`, `stream_unsupported_envelope`, `stream_incomplete`, `stream_input_timeout`, and `stream_terminal_error`. Caller cancellation remains `OperationCanceledException`; native transport disconnection can also propagate an I/O exception. No helper automatically reopens the request.

Execution outcomes use `stream_source_failed`, `stream_encoding_failed`, `stream_record_too_large`, `stream_write_failed`, `stream_flush_failed`, `stream_write_timeout`, `stream_duration_exceeded`, `stream_cleanup_failed`, `stream_cleanup_timeout`, or `stream_canceled`. Stages identify Source, Encoding, Write, Flush, or Cleanup. The first execution failure is retained.

Observer diagnostics use `stream_observer_failed` or `stream_observer_timeout`, separately from the immutable transport outcome. Configure the optional standalone writer logger or the host logger providers to capture reporting failures.

## Hosting, compression, CORS, and OpenAPI

Place `UseNdjsonResponseCompression()` after routing as the host's single compression middleware. Register normal framework compression providers and include `application/x-ndjson` in `MimeTypes` if opting in. The gate bypasses compression for declared NDJSON endpoints by default; `EnableCompression=true` opts a declared endpoint in. Other responses retain framework behavior. Do not add unconditional compression middleware around this gate. If a host currently has no compression, limit the compression middleware to the routes that explicitly require it.

Execution disables response buffering. Proxy buffering, gateway idle timeouts, and external compression still require deployment checks. Do not force transfer encoding or guess Content-Length.

For an already authorized cross-origin client, expose `X-Ndjson-Mode` and `X-Ndjson-Envelope-Version` through the existing CORS policy. Do not widen allowed origins. Authentication precedes all successful output, including heartbeat.

OpenAPI describes a text response plus `x-ndjson-record-schema`, mode/version headers, and examples. Generated JSON-array clients are unsuitable; use the streaming helper. Application schema naming follows host OpenAPI JSON configuration. If an endpoint uses a different custom payload naming/conversion policy, provide the corresponding application OpenAPI schema transformer.

## Live streams and SPA consumption

A fictional sensor API can expose a long-lived `IAsyncEnumerable<SensorReading>` at `/api/sensors/readings/stream`. Declare envelope mode to provide idle heartbeats and explicit terminal errors. The producer waits asynchronously for each reading, observes cancellation, and retains bounded state.

A Blazor dashboard uses the same .NET HTTP helpers as any other client. Reuse its authenticated `HttpClient`, consume `ReadEnvelopesAsync` to display heartbeat state, and retain a bounded history such as the last 200 data values. Cancel and dispose the current response before switching sources or leaving the page. Reading the response does not create a file download.

For a stored-record export, retrieve pages of at most 100 inventory rows, ordered by SKU with an identity tie-breaker. Capture the caller's access scope during admission, apply item-level access checks, map detached `StockRecord` values, and release each data-access scope before yielding. Advance the offset by source rows, including rows excluded by access checks. Opening the stream need not refresh an upstream service.

## Reconnection

Reconnection is off. Start another request only under application control, with a new response handle and cancellation policy:

```csharp
await ConsumeOneResponseAsync(ct); // handle incomplete/error/canceled explicitly
// After a user action or an application-approved retry decision:
await ConsumeOneResponseAsync(ct);
```

A new request has no implicit cursor, replay, deduplication, or gap recovery. No JavaScript SDK is shipped. .NET 10 enables browser HTTP response streaming by default; keep reads asynchronous and do not disable that browser option. See [Microsoft's compatibility note](https://learn.microsoft.com/en-us/dotnet/core/compatibility/networking/10.0/default-http-streaming).

## Validation

See the [specification](specs/spec-presentation-ndjson-streaming.md). The .NET suites cover exact bytes, limits, strict parsing, timers, ownership, ambiguous writes, cleanup, network delivery, compression, proxy forwarding, authorization, and consumer contracts. Verify browser interaction in the consuming host when needed; keep captures outside the documentation and test source trees.

## Performance experiments

The experiments in `benchmarks/Presentation.Benchmarks/` use fictional inventory records and validate every transferred value. Run them in Release mode from the repository root:

```sh
dotnet run -c Release --project benchmarks/Presentation.Benchmarks
dotnet run -c Release --project benchmarks/Common.Benchmarks -- --filter '*NdjsonRecordBenchmarks*' --artifacts docs/validation/streaming-experiments/records
```

A local run on an Intel Core i7-1165G7 with .NET 10.0.14 used HTTP/1.1 loopback without TLS, compression, or a proxy. After warm-up, five rounds each transferred 20,000 records of about 1 KiB. Median throughput was approximately 182,000 records/s for raw NDJSON, 267,000 with a 16 KiB/2 ms batch, and 348,000 with a 64 KiB/10 ms batch. These measurements include the .NET client consuming and validating records.

With a producer delay of 2 ms per record, the observed p95 time from production to client consumption was 0.41 ms without batching, 2.67 ms for the smaller batch, and 11.14 ms for the larger batch. This paced case has one observation per configuration. Flush-per-record remains the default. Consider explicit batching for bulk exports when the extra latency is acceptable.

The same bulk workload reached approximately 225,000 records/s with buffered JSON and 231,000 with streamed JSON arrays. Median first-record latency was 83 ms for buffered JSON, 0.83 ms for streamed JSON, and 4.85 ms for raw NDJSON. Both streamed formats delivered incrementally. The JSON baselines omit NDJSON frame-size enforcement, protocol deadlines, and envelope lifecycle validation, so their throughput represents different functionality.

With one and eight paused consumers of long-lived 64 KiB-record streams, producer counts stopped advancing during the sampled second. Managed heap samples also stayed steady. Retained heap above the warmed baseline was approximately 0.66 MiB and 5.27 MiB, respectively. Canceling the responses released every producer within 18 ms. These short observations include server, client, pools, and managed transport state in one process. They exclude native socket buffers and do not establish production capacity or prove the absence of leaks.

The record experiments led to one small implementation change: received frames can rent smaller pool buckets instead of the previous 4 KiB minimum under default limits. Small-record typed reading fell from 861 ns to 782–798 ns across two follow-up runs. Borrowed framing fell from 280 ns to 248–270 ns. Encoding timings for the small control payload stayed effectively unchanged. Escaped and large-payload timings varied, so these short runs support no general speedup claim.

Record benchmarks also distinguish pooled storage from managed allocations. Typed reading of a 64 KiB ASCII payload allocates about 128 KiB per record, mainly for the decoded string. Borrowed framing allocates about 98 B per record in the warm benchmark. This total excludes buffers reused from the pool. Use borrowed frames for hashing or forwarding when you do not need a model, and observe their lifetime rules.

Raw JSON reports and BenchmarkDotNet output are written under the ignored `docs/validation/streaming-experiments/` directory. Results depend on hardware, scheduling, payloads, and hosting. Re-run the HTTP experiment through the deployment's actual transport before selecting endpoint settings.
