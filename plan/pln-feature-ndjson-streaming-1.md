---
goal: Implement reusable NDJSON streaming with .NET clients and example integrations
version: '1.3'
date_created: 2026-10-29
last_updated: 2026-10-30
owner: bITdevKit maintainers
status: 'Completed'
tags: [feature, ndjson, streaming, serialization, rest, dotnet, blazor]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-green)

Implement finite and long-lived NDJSON streams through Common serialization primitives, Minimal API results, .NET HTTP clients, DoFiesta demonstrations, and a WeatherFiesta endpoint. This plan creates no Auditing dependency and delivers no JavaScript client SDK. This plan tracks implementation and verification of the delivered feature.

All paths are relative to `/home/vip32/Projects/bITdevKit`. The source specification is `docs/specs/spec-presentation-ndjson-streaming.md`, including sections 4.2.1, 6.4.1, 6.11, and 9.1. Its SHA-256 for the reconciled implementation is `f9db76733ca6e65e911cfdce31f85e378700d2863b2876ba3e6e4c71471328ea`; the repository baseline is commit `a1482898d793394f2bf37e80c0190cc37a5a1c84`. Reconcile a changed specification before executing tasks, and preserve unrelated worktree changes. Line references identify this snapshot; symbols and paths remain the execution references if unrelated line numbers move.

## 1. Requirements & Constraints

- **REQ-001**: Support finite `IEnumerable<T>` and finite or long-lived `IAsyncEnumerable<T>` sources. Return an outer `Result<IAsyncEnumerable<T>>` for requester preparation; do not extend existing requester behaviors over deferred enumeration.
- **REQ-002**: Write UTF-8 without BOM, one valid compact JSON value followed by LF per record. Preserve null items. Nested arrays and dictionaries remain single records. Leave caller-owned streams open.
- **REQ-003**: Default `MaxRecordBytes` to `1_048_576` JSON bytes, excluding the delimiter and including envelope overhead. Enforce the logical cap during encoding before destination writes; bound writer reservations to `3 * MaxRecordBytes + 4096` plus pool bucket rounding as specified in section 6.2. Do not truncate, split, drop, reserialize for sizing, or buffer a response to completion.
- **REQ-004**: Deliver owned `NdjsonEncodedRecord` and borrowed `NdjsonReadRecord` views exposing `JsonBytes`, `DelimiterBytes`, and `FramedBytes` over one bounded buffer. Owned encoding uses LF. Received records preserve their original whitespace, escaping, and LF or CRLF delimiter; compatibility EOF has an empty delimiter. Borrowed views expire on the next move or disposal. Typed reading uses the same framing implementation.
- **REQ-005**: Reader defaults reject BOM, invalid UTF-8, malformed JSON, multiple values on a line, empty lines, and an unterminated final record. Provide explicit `SkipEmptyLines` and `AllowUnterminatedFinalRecord` compatibility flags, both false by default. Keep limits active and never skip malformed records.
- **REQ-006**: Raw DTO lines are the default. Optional envelope version 1 uses fixed, case-sensitive structural names: `version`, `kind`, `data`, `recordCount`, and `error`. Kinds are `data`, `heartbeat`, `completed`, and `error`. Only `data` uses application JSON naming/converters. A null data value still requires its property.
- **REQ-007**: Every envelope declares integer version 1. `completed.recordCount` is a non-negative `long` equal to observed data records, excluding controls. Error code/message are required; trace ID is optional. Reject duplicate reserved fields, unknown kinds/versions, missing fields, and inconsistent counts. Ignore additional non-reserved fields within version 1.
- **REQ-008**: Flush each record by default. Batching is disabled unless both positive `BatchByteLimit` and positive finite `BatchMaxDelay` are supplied. Its timer starts with the first pending frame and is not reset by later frames. Flush pending data and control frames immediately for heartbeat/error/completion. A record larger than batch capacity bypasses batching after pending output is flushed.
- **REQ-009**: Envelope execution emits an initial heartbeat after admission, then a heartbeat after 15 seconds without output by default. Raw execution injects no control values or empty keepalive lines. Maintain at most one pending `MoveNextAsync` and one destination writer.
- **REQ-010**: Overall duration defaults to no cap. `WriteTimeout` defaults to 30 seconds per awaited write or flush. Duration expiry stops normal production and uses a separately bounded terminal-error attempt when safe. A stalled/partial write aborts without another write attempt. Caller cancellation remains active.
- **REQ-011**: Raw failure after output starts aborts. Envelope failure attempts one safe terminal error only at a complete-record boundary on a writable response. No completion follows an error. Cleanup must succeed before emitting success completion. Missing terminal records mean incomplete envelope output; deliberate cancellation with successful cleanup remains canceled.
- **REQ-012**: Expose the exact terminal observer contract below, including `CleanupCompleted`. Notify once after output stops and cleanup succeeds, fails, or exhausts REQ-024's grace period. `ObserverTimeout` defaults to five seconds, uses a fresh budget token, and bounds the wait without changing the transport outcome. A callback failure is a reporting failure, not an output failure. Late cleanup never changes the outcome or triggers another callback.
- **REQ-013**: Written counters include only complete destination writes. Flushed counters describe the contiguous prefix covered by the last successful flush. Staged batches are not written output. Preserve confirmed counters after ambiguity and set `PartialWritePossible` conservatively. Counters include shared envelope controls, not just business data.
- **REQ-014**: Provide raw-only `NdjsonHttpResult.FromEncodedRecords<TRecord>` for already encoded, budgeted frames. `TRecord` supplies metadata only. Transfer each yielded frame to the executor, dispose after its awaited write or bounded batch copy, and never serialize the buffer-owner object. Producer owns frames not yielded.
- **REQ-015**: Configure global server defaults and per-endpoint overrides. Capture immutable effective settings for each response. Distinguish omitted duration overrides from explicitly clearing an inherited duration cap. Default to raw mode and no attachment; filenames are explicit and safely encoded.
- **REQ-016**: Emit `application/x-ndjson`, `X-Ndjson-Mode`, and envelope version header when selected. OpenAPI describes the actual stream mode, per-record schema, examples, and pre-stream failures. Do not describe the wire body as a JSON array or assume generated clients stream it.
- **REQ-017**: Deliver .NET `HttpClient` helpers only. Open with `ResponseHeadersRead`, preserve caller authentication and GET/POST requests, use Results for recoverable opening failures, and own/dispose the response. Typed raw, original-byte raw, and envelope paths are alternative single-use enumerations. Never dispose caller-owned clients or requests.
- **REQ-018**: .NET envelope inactivity defaults to 45 seconds while awaiting a complete valid input frame. Partial bytes do not reset it; application processing time between pulls does not count. Raw inactivity is disabled by default. Cancellation, malformed input, timeout, and incomplete output are distinct. Reconnection is off; application code explicitly starts any new request and owns resume/replay semantics.
- **REQ-019**: Default to unbuffered and uncompressed delivery. Compression is explicit and tested with streaming-safe middleware ordering. Verify incremental delivery through a real forwarding proxy and document deployment limits. Do not set a guessed response length or force HTTP/1.1 transfer encoding.
- **REQ-020**: DoFiesta adds finite raw `/api/core/todoitems/stream`, finite enveloped `/api/core/todoitems/stream/enveloped`, and live enveloped `/api/core/streaming/ticks`, plus a Blazor consumer with bounded history. Preserve Todo list and per-item read permissions.
- **REQ-021**: WeatherFiesta adds finite raw `/api/core/cities/weather/stream`, emitting existing `UserCityModel` values for the current user's subscriptions and stored current weather. Preserve staleness semantics. Retrieve bounded, stably ordered batches; do not wrap the complete `UserCitiesQuery` result or fetch upstream weather per row.
- **REQ-022**: Prove consumer-defined exact-byte budgeting/digests and terminal reporting using fictional raw metadata/data/completion records, without importing Auditing. A business Partial export can have transport success; a failed final flush cannot publish successful business completion.
- **REQ-023**: Commit raw success headers after authentication, authorization, requester admission, and option validation, before the first producer move. Apply `WriteTimeout` to header start. Do not prefetch or vary timing by finite/live source. Admission failures use normal HTTP errors; any first-record failure after header commitment aborts the admitted response. Envelope mode commits with its initial heartbeat.
- **REQ-024**: Add positive finite `CleanupTimeout`, default five seconds, to Common writer/server options and inheritable endpoint overrides. One non-resetting budget covers a pending producer move and one disposal attempt after production stops. Never dispose concurrently with a move. On expiry abort HTTP, stop awaiting, mark Failed and `CleanupCompleted=false`, and use Cleanup/`stream_cleanup_timeout` absent an earlier failure. Cancellation alone does not hide cleanup failure. Observe late settlement and release late leases with one continuation; no output, disposal retry, outcome mutation, or second callback. Caller-owned Common streams stay open. Producers must use independent scopes, cooperate with cancellation, and avoid synchronous blocking; arbitrary code cannot be forcibly stopped.
- **REQ-025**: Common and HTTP envelope/data readers throw `NdjsonStreamException` on the same pull that parses a terminal `error`, with `ReasonCode=stream_terminal_error`, `RemoteErrorCode=error.code`, and optional `TraceId=error.traceId`. Never yield the error. Its exception message is `NDJSON stream terminated with an error.` and omits the remote message/record. Envelope reading yields heartbeat/data/completed; data-only reading yields data. Valid completion releases HTTP resources without waiting for EOF. Opening Results and other typed failures remain distinct.
- **SEC-001**: Runtime wire errors, observer outcomes, diagnostics, and test artifacts must not expose payload contents, principals, credentials, exception objects, or stacks. Use stable reason codes and optional trace IDs. Error bodies read by the client are capped at 4,096 bytes.
- **SEC-002**: Admission/authentication/authorization precede success output, including the initial heartbeat. Expose NDJSON headers through the host's existing CORS policy only when cross-origin access is already authorized. Do not widen CORS or add an authentication bypass to production code.
- **SEC-003**: Release safely releasable record leases and response resources on every exit. An unfinished operation retains its owned buffer until it settles; late cleanup releases it without accessing HTTP or request-scoped services. Never return pooled storage still in use. Reject disposed owners before writing and clear used ranges on return. Captured `ReadOnlyMemory<byte>` cannot be physically revoked. Treat remote error metadata as untrusted diagnostic input and keep exception text fixed.
- **CON-001**: Target the repository's .NET 10 SDK and existing packages. Add no runtime package for NDJSON. Existing centrally managed xUnit, Shouldly, NSubstitute, EF test providers, MVC testing, and TimeProvider testing packages may be referenced by test projects through `dotnet` package commands.
- **CON-002**: Preserve `ISerializer`, `ITextSerializer`, existing JSON response behavior, existing requester semantics, and existing WeatherFiesta CSV routes. Do not implement Auditing, persistence/replay, database migrations, SSE, a generic format registry, or non-.NET client libraries.
- **CON-003**: Application producers use repositories/Active Entity and detached DTO batches; no Application reference to Infrastructure or direct DbContext access. No transaction spans an entire live response. Request-scoped observers are attached per execution, never retained in shared options.
- **CON-004**: Run top-level builds/tests sequentially in this worktree. Tasks touching the same file are serialized through the explicit dependencies below. Independent code edits may run in parallel; permission to spawn agents is not implied by this plan.
- **CON-005**: This plan fixes implementation choices left open by the specification: reader I/O chunks are 4,096 bytes; sample database batches contain at most 100 source rows; the Blazor history retains 200 items; tick interval defaults to 1,000 ms and accepts integers from 1,000 through 60,000 ms. Tests use a 20,000 ms tick interval to exercise the 15-second heartbeat with controlled time.
- **GUD-001**: Follow `.editorconfig`, file-scoped namespaces, disabled reference nullability, `this.` field access, conventional errors, and XML examples for every new public C# symbol. Common types use namespace `BridgingIT.DevKit.Common`; server types use `BridgingIT.DevKit.Presentation.Web`.
- **PAT-001**: Use `TimeProvider` for timers and elapsed deadlines, gated producers for determinism, and `TaskCreationOptions.RunContinuationsAsynchronously` for fixture gates. Do not poll with sleeps or create unbounded channels.
- **PAT-002**: Reuse existing HTTP Result mapping and `ConfigureJson` options. Clone JSON settings once per execution, force compact output, validate framing independently of permissive custom JSON options, and never mutate shared options.

The observer contracts must retain these signatures and enum values:

```csharp
public enum NdjsonStreamStatus { Succeeded, Failed, Canceled }
public enum NdjsonStreamFailureStage { Source, Encoding, Write, Flush, Cleanup }
public sealed record NdjsonStreamOutcome(
	NdjsonStreamStatus Status,
	bool SourceCompleted,
	bool CleanupCompleted,
	long WrittenRecordCount,
	long WrittenFramedByteCount,
	long FlushedRecordCount,
	long FlushedFramedByteCount,
	bool PartialWritePossible,
	NdjsonStreamFailureStage? FailureStage = null,
	string ReasonCode = null);
public interface INdjsonStreamObserver
{
	Task OnFinishedAsync(NdjsonStreamOutcome outcome,
		CancellationToken cancellationToken = default);
}
```

Public API choices for execution are fixed as follows. Add `NdjsonWriter.WriteAsync<T>` for both sequence types, observer-taking overloads of both, `WriteRecordAsync<T>`, `EncodeRecordAsync<T>`, and `WriteEncodedRecordAsync`. Add `NdjsonReader.ReadAsync<T>`, `ReadRecordsAsync`, and `Deserialize<T>(NdjsonReadRecord)`. Constructors capture JSON/options/TimeProvider values; no ambient HTTP state enters these classes. HTTP factories create `NdjsonHttpResult<T>` from asynchronous, synchronous, or pre-encoded sources; the pre-encoded factory remains raw-only. `WithObserver` returns the configured result instance before execution and rejects changes after execution begins.

Add `NdjsonHttpClientExtensions.OpenNdjsonAsync<T>(HttpClient, HttpRequestMessage, NdjsonClientOptions, CancellationToken)` and `GetNdjsonAsync<T>(HttpClient, string, NdjsonClientOptions, CancellationToken)`, returning `Task<Result<NdjsonHttpResponse<T>>>`. Nullable optional options parameters default to null under the repository's disabled nullability conventions. The disposable response handle exposes `ReadAsync`, `ReadRecordsAsync`, `ReadEnvelopesAsync`, and `ReadDataAsync`, each with cancellation. Raw typed/framed methods reject envelope mode. The envelope methods validate version/count/terminal rules and follow REQ-025 same-pull exceptions without yielding errors. `NdjsonStreamException.RemoteErrorCode` and `.TraceId` are optional immutable strings separate from the local `ReasonCode`; fixed exception text omits the remote message. Only one path can be enumerated.

Stable reason codes are `stream_http_error`, `stream_invalid_content_type`, `stream_source_failed`, `stream_encoding_failed`, `stream_record_too_large`, `stream_write_failed`, `stream_flush_failed`, `stream_cleanup_failed`, `stream_cleanup_timeout`, `stream_duration_exceeded`, `stream_write_timeout`, `stream_canceled`, `stream_incomplete`, `stream_invalid_record`, `stream_unsupported_envelope`, `stream_input_timeout`, `stream_terminal_error`, `stream_observer_failed`, and `stream_observer_timeout`. Keep the first execution failure; cancellation alone is not an earlier failure that hides failed cleanup. Report observer/secondary cleanup failures separately without payload-bearing exception messages.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Establish validated contracts and deterministic test fixtures.
- Dependencies: none.
- Completion criteria: option, envelope, and observer contract tests compile and pass; existing serialization interfaces have no diff; all observer enums, outcome fields, and interface signatures match REQ-012.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | In `src/Common.Serialization/Ndjson/NdjsonDefaults.cs`, `NdjsonMode.cs`, `NdjsonWriterOptions.cs`, and `NdjsonReaderOptions.cs`, define constants, raw/envelope modes, captured JSON settings, strict reader flags, batch settings, five-second `CleanupTimeout`, and `ObserverTimeout`. Implement `Validate()` with REQ-003/005/008/012 defaults and finite-positive checks. Validation: TEST-001. | Yes | 2026-10-29 |
| TASK-002 | Depends on TASK-001. Add `src/Common.Serialization/Ndjson/NdjsonStreamOutcome.cs`, `INdjsonStreamObserver.cs`, and `NdjsonStreamException.cs`; declare the exact observer contracts above and a safe exception carrying `ReasonCode`, optional record number, optional `RemoteErrorCode`, and optional `TraceId`, with fixed library message text and no remote-message or payload property. Validation: TEST-009 verifies safe outcomes and first-failure retention. | Yes | 2026-10-29 |
| TASK-003 | Depends on TASK-001. Add `src/Common.Serialization/Ndjson/NdjsonEnvelope.cs` and `NdjsonEnvelopeJsonConverter.cs`. Implement the four version-1 shapes, required null `data`, strict structural casing, duplicate/version/kind validation, safe errors, and `long` data counts independent of host naming settings. Validation: TEST-005. | Yes | 2026-10-29 |
| TASK-004 | Add `tests/Common.UnitTests/Serialization/Ndjson/NdjsonTestStreams.cs` and `NdjsonTestTimeProvider.cs` in namespace `BridgingIT.DevKit.Testing.Ndjson`: asynchronous-only non-seekable streams, controllable partial-write/flush failures, bounded producer gates, cancellation-aware iterators, disposal spies, independently gated non-cooperative async moves/disposal, late encoded-lease ownership checks, and a monotonic manual `TimeProvider`. Keep these fixture files independent of test-assembly types so other test projects can link their source. Validation: scripted reads/writes and timer advancement work without sleeps. | Yes | 2026-10-29 |
| TASK-005 | Depends on TASK-001, TASK-002, TASK-003, TASK-004. Add `tests/Common.UnitTests/Serialization/Ndjson/NdjsonContractTests.cs` for option validation, constant values, envelope structural fields, and observer outcome data safety. Run VLD-001. | Yes | 2026-10-29 |

### Implementation Phase 2

- GOAL-002: Implement one bounded encoding/framing path with exact original-byte access.
- Dependencies: GOAL-001.
- Completion criteria: no failed encoding writes destination bytes; LF/CRLF/compatibility delimiter tests preserve original bytes; disposed-owner and borrowed-lifetime checks pass.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-006 | Depends on TASK-005. Add `src/Common.Serialization/Ndjson/NdjsonRecordBuffer.cs`, a capped `IBufferWriter<byte>` backed by `ArrayPool<byte>`. Enforce the exact logical JSON cap on Advance and the specified bounded writer reservation allowance before allocation; allow two delimiter bytes, account for pool bucket overhead, and clear returned storage. Validation: TEST-002 exercises exact limit, next-byte rejection, and outstanding rents. | Yes | 2026-10-29 |
| TASK-007 | Depends on TASK-006. Add `src/Common.Serialization/Ndjson/NdjsonEncodedRecord.cs` and `NdjsonReadRecord.cs`. Expose three read-only views into one buffer, idempotent owned disposal, no unchecked public encoded constructor, and reader-managed borrowed lifetime. Guard owner properties after disposal; document previously captured memory lifetime. Validation: TEST-003. | Yes | 2026-10-29 |
| TASK-008 | Depends on TASK-007. Add `src/Common.Serialization/Ndjson/NdjsonWriter.cs` record primitives: `EncodeRecordAsync<T>` encodes/validates once with compact cloned settings; `WriteEncodedRecordAsync` validates owner/size and writes the identical frame without flush or disposal; `WriteRecordAsync<T>` owns encode/write/dispose. Preserve null records and leave streams open. Validation: TEST-002/003/004. | Yes | 2026-10-29 |
| TASK-009 | Depends on TASK-007. Add `src/Common.Serialization/Ndjson/NdjsonReader.cs`: `ReadRecordsAsync` uses 4,096-byte async reads, strict UTF-8/JSON validation, LF/CRLF framing, bounded lookahead, compatibility flags, and borrowed views; `Deserialize<T>` never advances; typed `ReadAsync<T>` releases frame views before yielding independent values. Validation: TEST-003/004/006. | Yes | 2026-10-29 |
| TASK-010 | Depends on TASK-008, TASK-009. Add `tests/Common.UnitTests/Serialization/Ndjson/NdjsonRecordTests.cs` and `NdjsonReaderTests.cs`; cover one-byte fragmented UTF-8, multi-record chunks, whitespace/escaping, null/nested values, CRLF, invalid input, count limits, early disposal, and all strict/compatibility combinations. Run VLD-001. | Yes | 2026-10-29 |

### Implementation Phase 3

- GOAL-003: Deliver bounded sequence writing, envelope validation, and terminal observer behavior outside HTTP.
- Dependencies: GOAL-002.
- Completion criteria: observer sees one finalized outcome after cleanup succeeds, fails, or exhausts its grace period; confirmed output/flush counters survive ambiguous failures; idle batch flushing does not await another source item.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-011 | Depends on TASK-010. Implement both `NdjsonWriter.WriteAsync<T>` sequence overloads and private `NdjsonOutputSession.FlushBatchAsync` in `src/Common.Serialization/Ndjson/NdjsonWriter.cs`. Use the record primitives; retain at most one pending move; default flush-per-record; implement byte/delay-limited batching, large-record bypass, success final flush after cleanup, and owned frame disposal. Validation: TEST-007/008. | Yes | 2026-10-29 |
| TASK-012 | Depends on TASK-011. Add `src/Common.Serialization/Ndjson/NdjsonCleanupCoordinator.cs` and integrate it into both writer sequence overloads. Implement REQ-024 with one non-resetting five-second budget, pending-move settlement before one disposal attempt, safe late settlement, and no buffer reuse while an operation owns it. Add observer overloads and internal `NotifyAsync` in shared `src/Common.Serialization/Ndjson/NdjsonOutputSession.cs`. Track exact confirmed written/flushed prefixes; classify cancellation only from execution cancellation; preserve first failure; finalize `CleanupCompleted` after successful cleanup, cleanup failure, or grace expiry before publishing the immutable outcome; bound notification with a fresh five-second token and `TimeProvider`; report safe observer failures separately without retrying or changing output propagation. Validation: TEST-009/024. | Yes | 2026-10-29 |
| TASK-013 | Depends on TASK-003, TASK-009. Add `src/Common.Serialization/Ndjson/NdjsonEnvelopeReader.cs` with `ReadAsync<T>` over the shared framed reader. Validate initial/terminal control semantics, supported version, data count, incomplete EOF, and terminal errors. Implement REQ-025: throw `NdjsonStreamException` on the error-parsing pull, set local/remote code and trace fields, and never yield the error. Yield heartbeat/data/completed only on the envelope path; data-only yields data. Stop at validated completion without waiting for EOF. Do not introduce HTTP headers into the Common reader. Validation: TEST-005/006/025. | Yes | 2026-10-29 |
| TASK-014 | Depends on TASK-012, TASK-013. Add `tests/Common.UnitTests/Serialization/Ndjson/NdjsonSequenceWriterTests.cs`, `NdjsonEnvelopeTests.cs`, and `NdjsonObserverTests.cs`. Include source exhaustion followed by flush/cleanup failure, observer timeout/non-cooperation, five-second cleanup grace with never-completing moves/disposal, serialized late cleanup and lease release, host cancellation, same-pull terminal-error exceptions, completion without EOF, failed batches, and bounded output. Run VLD-001. | Yes | 2026-10-29 |

### Implementation Phase 4

- GOAL-004: Implement typed HTTP execution, endpoint descriptors, runtime metadata, and optional compression.
- Dependencies: GOAL-003.
- Completion criteria: raw/envelope/encoded paths share one HTTP executor; pre-stream errors preserve existing mapping; heartbeat/deadline/observer tests pass; metadata matches actual bytes.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-015 | Depends on TASK-014. Add `src/Presentation.Web/Ndjson/NdjsonStreamingOptions.cs`, `NdjsonEndpointOptions.cs`, `NdjsonEndpointDescriptor.cs`, and `NdjsonServiceCollectionExtensions.cs`. Implement `AddNdjsonStreaming`, `TimeProvider` fallback registration, validation, copy-then-configure endpoint inheritance, positive finite cleanup-grace overrides, explicit duration-cap clearing, JSON option capture, and per-response observer exclusion from shared options. Validation: TEST-010. | Yes | 2026-10-29 |
| TASK-016 | Depends on TASK-015. Add `src/Presentation.Web/Ndjson/NdjsonEndpointConventionBuilderExtensions.cs` with `WithNdjson<T>(RouteHandlerBuilder, Action<NdjsonEndpointOptions>)` and `ProducesNdjson<T>`. Store one normalized descriptor for execution and metadata; `WithNdjson` configures both, while `ProducesNdjson` describes the matching declared mode. Add `NdjsonOpenApiOperationTransformer.cs` for NDJSON text body, record schema metadata/extensions, control examples, version headers, and ordinary error statuses. Reject contradictory modes before output. Validation: TEST-010/011. | Yes | 2026-10-29 |
| TASK-017 | Depends on TASK-016. Add `src/Presentation.Web/Ndjson/NdjsonHttpResult.cs`, `NdjsonHttpResultOfT.cs`. Implement typed factories for both sequence types, raw-only `FromEncodedRecords<TRecord>`, `WithObserver`, and `ExecuteAsync`. Resolve descriptor/global defaults once; set content type/mode/version/disposition; delegate every record to Common encoding and every encoded write to its exact-byte path. Reject reused execution and envelope pre-encoded output. Validation: TEST-011/012. | Yes | 2026-10-29 |
| TASK-018 | Depends on TASK-017. Add `src/Presentation.Web/Ndjson/NdjsonHttpStreamExecutor.cs`. Implement one ordered state machine for producer move, idle heartbeat, batch deadline, duration, and write deadlines. Apply REQ-023: after admission, start raw headers under `WriteTimeout` before the first move, for both finite and live sources; envelope mode starts with its initial heartbeat. A failed first move/encoding after admission aborts raw output. Reuse `NdjsonCleanupCoordinator` and signal producer cancellation on host shutdown. Finish cleanup within REQ-024 before success completion; send safe terminal error only at an intact boundary; abort on ambiguous/stalled writes; dispose yielded encoded leases before the next frame. Validation: TEST-012/013/014/024/026. | Yes | 2026-10-29 |
| TASK-019 | Depends on TASK-018. Complete outcome/observer handling in `src/Presentation.Web/Ndjson/NdjsonHttpStreamExecutor.cs`: count all raw/envelope frames, not staged bytes; mark partial-write uncertainty conservatively; preserve first failure and separately report cleanup/reporting failures; expose `CleanupCompleted=false` after grace expiry or unsafe pending-resource release; notify once after final output and cleanup settlement or grace expiry with fresh observer timeout and host-shutdown linkage. Never also attach the observer to the inner record writer. Validation: TEST-009/014/024. | Yes | 2026-10-29 |
| TASK-020 | Depends on TASK-017. Add `src/Presentation.Web/Result/Map/ResultMapNdjsonExtensions.cs` with typed `MapHttpNdjson<T>` overloads for `Result<IAsyncEnumerable<T>>`, `Result<IEnumerable<T>>`, `Result<List<T>>`, and `Result<T[]>`. Delegate failure mapping to existing `ResultMapHttpExtensions`; do not enumerate failed values or start observer executions for mapping rejection. Return typed HTTP result unions. Validation: TEST-011. | Yes | 2026-10-29 |
| TASK-021 | Depends on TASK-016. Add `src/Presentation.Web/Ndjson/NdjsonCompressionApplicationBuilderExtensions.cs` with `UseNdjsonResponseCompression`. Place it after routing and make it the host's single compression middleware registration: bypass compression for NDJSON descriptors unless `EnableCompression` is true; retain framework behavior for non-NDJSON requests. Use the response-body feature to disable buffering at execution. Document that unconditional compression middleware and external proxies are outside this gate. Validation: TEST-015. | Yes | 2026-10-29 |
| TASK-022 | Depends on TASK-019, TASK-020, TASK-021. Add `tests/Presentation.UnitTests/Web/Ndjson/NdjsonOptionsTests.cs`, `NdjsonMetadataTests.cs`, `NdjsonHttpResultTests.cs`, and `NdjsonHttpObserverTests.cs`; use real `DefaultHttpContext` features and failure-injecting streams. Cover first-record failure after headers, header-start deadline, rejected admission without output, cleanup grace/late settlement, and no second observer call. In `tests/Presentation.UnitTests/Presentation.UnitTests.csproj`, link the two TASK-004 fixture source files through `Compile Include` entries; do not reference the Common test assembly. Run VLD-002. | Yes | 2026-10-29 |

### Implementation Phase 5

- GOAL-005: Deliver single-response .NET client helpers reusable by Blazor.
- Dependencies: GOAL-003. HTTP wire interoperability verification also depends on GOAL-004.
- Completion criteria: every client ownership path is single-use and disposable; original-byte views remain exact; inactivity excludes consumer processing; no helper automatically reissues a request.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-023 | Depends on TASK-014. Add `src/Common.Utilities/Http/NdjsonClientOptions.cs` and `NdjsonClientError.cs`. Capture expected mode, JSON/reader options, raw optional inactivity, envelope 45-second inactivity, `TimeProvider`, and safe initial HTTP/protocol errors. Do not read server headers as permission to raise client limits. Validation: TEST-016. | Yes | 2026-10-29 |
| TASK-024 | Depends on TASK-023. Add `src/Common.Utilities/Http/NdjsonHttpClientExtensions.cs`. Implement the two opening methods specified above using `ResponseHeadersRead`; validate 2xx status, NDJSON media type, declared modes, and envelope version; read at most 4,096 error-body bytes. Preserve caller headers and support POST. GET owns its created request; request-based overload preserves caller request ownership. Validation: TEST-016. | Yes | 2026-10-29 |
| TASK-025 | Depends on TASK-024. Add `src/Common.Utilities/Http/NdjsonHttpResponse.cs`. Implement the four alternative read paths using the Common reader; atomically reject a second enumeration, propagate cancellation, and dispose the response/owned request on terminal outcome or early exit. Hold borrowed views until the next pull. Start inactivity budgets only while awaiting a complete frame, reset on valid data/heartbeat, and exclude time after yielding. Propagate REQ-025 same-pull typed terminal failures, release the response before throwing, and close on validated completion without draining EOF. Validation: TEST-006/016/017/025. | Yes | 2026-10-29 |
| TASK-026 | Depends on TASK-025, TASK-022. Add `tests/Common.UnitTests/Utilities/Http/NdjsonHttpClientTests.cs` with scripted `HttpMessageHandler` and asynchronous-only streams. Cover raw and envelope servers, header absence for external raw servers, unsupported mode/version, safe opening failures, partial-byte trickles, slow application processing, same-pull terminal errors without a yielded error frame, remote-message non-disclosure, completion while transport stays open, early exit, and one-request-only behavior. Run VLD-001 and VLD-002 sequentially. | Yes | 2026-10-29 |

### Implementation Phase 6

- GOAL-006: Add DoFiesta producers, routes, and isolated application integration tests.
- Dependencies: GOAL-004. Producer work can run independently of GOAL-005.
- Completion criteria: all three required routes work under existing authentication and entity permissions, database batches are bounded, and the new integration project runs independently of the developer database.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-027 | Depends on TASK-022. Add `examples/DoFiesta/DoFiesta.Application/Modules/Core/Queries/TodoItemStreamQuery.cs`. In `HandleAsync`, capture user identity and immutable selection; return a lazy iterator. Per iterator batch, create a scope, resolve repository/mapper/permission evaluator, call `FindAllAsync` with `FindOptions<TodoItem>` using `NoTracking=true`, `Skip=offset`, `Take=100`, and stable ascending Id order; apply the existing per-item Read check and map DTOs before scope disposal. Advance offset by source rows, not authorized rows; yield only detached DTOs outside the scope. No full-result materialization or snapshot claim. Validation: TEST-018. | Yes | 2026-10-29 |
| TASK-028 | Depends on TASK-022. Add `examples/DoFiesta/DoFiesta.Application/Modules/Core/Models/StreamTickModel.cs` and `Queries/StreamTicksQuery.cs`. Implement validated interval 1,000..60,000 ms, default 1,000 ms, ascending checked `long` sequence numbers, UTC timestamps from `TimeProvider`, and a cancellation-aware infinite async iterator with bounded state. No broker or persistence. Validation: TEST-018. | Yes | 2026-10-29 |
| TASK-029 | Depends on TASK-027, TASK-028. Modify `examples/DoFiesta/DoFiesta.Presentation.Web.Server/Modules/Core/Endpoints/CoreTodoItemEndpoints.cs` and add `CoreStreamingEndpoints.cs` beside it. Map the three exact routes in REQ-020 with raw/envelope descriptors, Todo List authorization and existing per-item Read semantics, typed metadata, ordinary errors, and no default attachment. Register NDJSON defaults and the compression gate in server `Program.cs` without changing unrelated policies. Validation: TEST-018/019. | Yes | 2026-10-29 |
| TASK-030 | Depends on TASK-029. Create `examples/DoFiesta/DoFiesta.IntegrationTests/DoFiesta.IntegrationTests.csproj`, `DoFiestaApplicationFactory.cs`, `TestAuthenticationHandler.cs`, and `StreamingEndpointsTests.cs`. Target net10.0; use repository xUnit/Shouldly/NSubstitute/MVC Testing conventions and isolated SQLite per factory. Reference the DoFiesta server and `Common.Utilities.Xunit`; link both TASK-004 fixture sources. Add packages/project references through `dotnet` commands using central versions, then `dotnet sln bITdevKit.slnx add` the project. Override test authentication/configuration only and reuse the WeatherFiesta factory isolation pattern. Validation: VLD-003 and TEST-018/019. | Yes | 2026-10-29 |

### Implementation Phase 7

- GOAL-007: Demonstrate .NET streaming in the DoFiesta Blazor client.
- Dependencies: GOAL-005 and GOAL-006.
- Completion criteria: start, switch, cancellation, disposal, heartbeat, and terminal states work with at most 200 retained items and no JavaScript parsing implementation.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-031 | Depends on TASK-026, TASK-030. Add `examples/DoFiesta/DoFiesta.Presentation.Web.Client/Services/StreamingDemoClient.cs` using `IHttpClientFactory.CreateClient("backend-api")`, preserving its existing OIDC handler and using the new `ResponseHeadersRead` helpers rather than Kiota JSON-body decoding. Keep one active response/token source; cancel and await disposal before switching; expose item/control/terminal state without retaining all DTOs. Register it in client `Program.cs`; do not change shared client timeout settings. Validation: TEST-020. | Yes | 2026-10-29 |
| TASK-032 | Depends on TASK-031. Add `examples/DoFiesta/DoFiesta.Presentation.Web.Client/Pages/Streaming.razor` at `/streaming`; modify `Layout/NavMenu.razor`. Provide source values `finite-raw`, `finite-envelope`, and `live`, interval selection, Start/Cancel, last-200 FIFO history, total count, heartbeat, and success/error/incomplete/canceled states. Add `data-testid` values `ndjson-source`, `ndjson-interval`, `ndjson-start`, `ndjson-cancel`, `ndjson-state`, `ndjson-total`, and `ndjson-history`. Implement `IAsyncDisposable` and .NET HTTP streaming. Validation: TEST-020. | Yes | 2026-10-29 |
| TASK-033 | Depends on TASK-032. Verify the existing authenticated WASM consumer through real browser interaction: mode switching, bounded history, live cancellation and cleanup. Launch the app using VLD-005. Browser verification was completed; its tracked harness was subsequently retired at user request. Future checks use disposable automation and ignored artifacts. Validation: VLD-006. | Yes | 2026-10-29 |

### Implementation Phase 8

- GOAL-008: Add the WeatherFiesta subscribed-city weather stream.
- Dependencies: GOAL-004. This phase is independent of the DoFiesta UI phase.
- Completion criteria: the new route emits existing user-scoped weather DTOs from multiple bounded batches, preserves zero-record behavior, and makes no upstream weather calls.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-034 | Depends on TASK-022. Add `examples/WeatherFiesta/WeatherFiesta.Application/Modules/Core/Queries/UserCitiesStreamQuery.cs`. Capture current user and stale threshold during admission. Use `UserCity.FindAllAsync` with `UserCitiesByUserSpecification` and `FindOptions<UserCity>`: `NoTracking=true`, `Take=100`, offset paging, ascending DisplayOrder then Id. Retain the existing `ActiveEntityContextScope.UseAsync` operation lifetimes; do not replace its global provider. Load stored weather for that bounded city set and reuse mapping/staleness semantics. Materialize detached page DTOs and release operation scopes before yielding. Validation: TEST-021. | Yes | 2026-10-29 |
| TASK-035 | Depends on TASK-034. Modify `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Modules/Core/Endpoints/WeatherEndpoints.cs` to add exact raw route `/api/core/cities/weather/stream`, with existing authorization and typed NDJSON metadata. Register defaults/compression gate in server `Program.cs`. Leave CSV `/api/core/cities/export` and existing JSON routes unchanged. Validation: TEST-021/022. | Yes | 2026-10-29 |
| TASK-036 | Depends on TASK-035. Add `examples/WeatherFiesta/WeatherFiesta.IntegrationTests/Modules/Core/Presentation/WeatherStreamingEndpointsTests.cs`; reuse `WeatherFiestaApplicationFactory.cs` and extend its fixtures only for fresh subscriptions/weather rows. Test two users, zero/205 records, stable multi-batch order, cancellation, oversized output, and zero calls to mocked upstream `WeatherAgent`. Validation: VLD-004. | Yes | 2026-10-29 |

### Implementation Phase 9

- GOAL-009: Prove real network delivery, compression/proxy behavior, and generic consumer extension contracts.
- Dependencies: GOAL-004 and GOAL-005; the full acceptance run also depends on GOAL-006, GOAL-007, and GOAL-008.
- Completion criteria: a real client receives data before gated source completion; forwarding and compression preserve control timing; byte budgets/digests and terminal outcomes match destination-confirmed output.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-037 | Depends on TASK-022, TASK-026. Add `tests/Presentation.UnitTests/Web/Ndjson/LoopbackStreamingFixture.cs` and `NdjsonNetworkTests.cs`. Start real Kestrel listeners on loopback port zero, obtain assigned addresses from server features, and consume with the .NET helper. Gate source completion until the first frame is parsed; prove finite/raw/live/envelope delivery and cancellation. Dispose listeners and clients in async cleanup. Validation: TEST-019 and VLD-002. | Yes | 2026-10-29 |
| TASK-038 | Depends on TASK-037. Add `tests/Presentation.UnitTests/Web/Ndjson/StreamingProxyFixture.cs` and `NdjsonCompressionTests.cs`. Use a second loopback Kestrel app as the reference streaming proxy, forwarding with `ResponseHeadersRead`, a fixed 4,096-byte buffer, and awaited flushes. Exercise uncompressed defaults and explicit framework Gzip/Brotli through the proxy; preserve status, content type, mode/version headers, cancellation, first-record delivery, and heartbeat timing. No external proxy package or deployment is required. Validation: TEST-015/019. | Yes | 2026-10-29 |
| TASK-039 | Depends on TASK-037. Add `tests/Presentation.UnitTests/Web/Ndjson/NdjsonConsumerContractTests.cs`. Construct fictional metadata/data/completion records, encode once, enforce an exact framed-byte budget, hash selected original bytes with `IncrementalHash`, emit via `FromEncodedRecords`, and verify using client `ReadRecordsAsync`. Cover CRLF input and equivalent JSON spellings producing different original-byte digests. No Auditing reference is permitted. Validation: TEST-023. | Yes | 2026-10-29 |
| TASK-040 | Depends on TASK-039. Extend `NdjsonConsumerContractTests.cs` and `NdjsonHttpObserverTests.cs`: assert a byte-limited business Partial response can have transport Succeeded; source-complete/final-flush or cleanup failure cannot publish business success; observer is attempted once with confirmed prefixes and safe reason/stage values, including cancellation and pre-first-frame failures. Validation: TEST-009/014/023. | Yes | 2026-10-29 |
| TASK-041 | Depends on TASK-033, TASK-036, TASK-038, TASK-040. Execute VLD-001 through VLD-006 sequentially where specified and record logs, TRX files, screenshots, proxy configuration, and failed cases in `docs/validation/ndjson-streaming/README.md`. Fix failures in their owning layer and rerun affected proofs before the final gate. Do not substitute unit snapshots for the network/browser requirements. | Yes | 2026-10-29 |

### Implementation Phase 10

- GOAL-010: Publish usage guidance and complete repository validation.
- Dependencies: GOAL-009.
- Completion criteria: documentation covers every delivered API/default/example; required checks pass; no unimplemented task is marked complete; only task-aligned changes remain.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-042 | Depends on TASK-041. Create `docs/features-presentation-streaming.md`; update `docs/common-serialization.md`, `docs/features-presentation-endpoints.md`, `src/Common.Serialization/README.md`, `src/Presentation.Web.Client/README.md`, and `mkdocs.yml`. Document raw/envelope APIs, immediate admission headers, exact bytes/lease lifetimes, `CleanupCompleted`, five-second cleanup grace, typed terminal-error exceptions, caller ownership, deadlines, batching, compression gate placement, explicit reconnect examples, CORS headers, and fictional inventory and sensor route samples. State that flush/output counts are not delivery acknowledgments or export-completion proof. | Yes | 2026-10-29 |
| TASK-043 | Depends on TASK-042. Create `docs/validation/ndjson-streaming/hosting.md` with the tested loopback forwarding profile and deployment instructions for disabling buffering and allowing long-lived responses, immediate raw admission headers, five-second cleanup grace and independent producer scopes, typed terminal exceptions, and successful completion without an EOF wait. Distinguish the tested reference proxy from other production proxies requiring host-specific verification. Update `docs/specs/spec-presentation-ndjson-streaming.md` with implemented symbol links and evidence, retaining every agreed requirement. | Yes | 2026-10-29 |
| TASK-044 | Depends on TASK-043. Review new public symbols in all FILE entries for XML documentation and copyable examples, then run VLD-007. Record exact commands/results and limitations in `docs/validation/ndjson-streaming/README.md`. Mark task rows complete only after their criteria pass. Do not commit, push, publish, or deploy unless separately requested. | Yes | 2026-10-29 |

## 3. Alternatives

- **ALT-001**: Extend synchronous `ISerializer` to guess record boundaries from `object`. Rejected because arbitrary arrays/dictionaries are valid single records and the contract does not describe async enumeration or lifetime.
- **ALT-002**: Register an MVC output formatter or global negotiation middleware. Rejected because the target is explicit Minimal API endpoints and existing endpoints must keep their behavior.
- **ALT-003**: Use permissive whitespace-separated JSON deserialization as the only reader. Rejected because it cannot enforce line boundaries or preserve original delimiters for byte-exact verification.
- **ALT-004**: Buffer the full response, serialize twice to measure, or hash reserialized DTOs. Rejected because it breaks bounded streaming and changes digest bytes.
- **ALT-005**: Automatically reconnect using generic sequence numbers. Rejected because sequence numbers are not durable cursors or processing acknowledgments; applications explicitly select replay behavior.
- **ALT-006**: Deliver only a serializer or make Auditing own the transport. Rejected because endpoint execution, .NET clients, example integrations, and observer/byte contracts are required reusable components.
- **ALT-007**: Add a JavaScript SDK or new format/plugin registry. Rejected by the agreed .NET-only scope and explicit endpoint model.
- **ALT-008**: Prefetch a first raw record before committing headers. Rejected because an admitted live source can remain idle indefinitely; requester admission owns normal HTTP failures.
- **ALT-009**: Wait indefinitely for producer cleanup or dispose concurrently with a pending move. Rejected because the request can hang or violate iterator ownership; use bounded waiting, serialized late cleanup, and honest incomplete-cleanup outcomes.
- **ALT-010**: Yield terminal errors as ordinary values or throw only on a later pull. Rejected because consumers can mistake failure for successful enumeration; throw on the pull that parses the error.

## 4. Dependencies

- **DEP-001**: Repository .NET 10 SDK selection in `global.json`, `bITdevKit.slnx`, central packages, `AGENTS.md`, and `.github/copilot-instructions.md`.
- **DEP-002**: BCL `System.Text.Json`, `ArrayPool<byte>`, `TimeProvider`, `IAsyncEnumerable<T>`, `HttpClient`, and `IncrementalHash`; ASP.NET Core shared-framework routing, Result, OpenAPI, compression, and Kestrel APIs.
- **DEP-003**: Existing Common Results, requester, JSON settings, `ResultMapHttpExtensions`, repository `FindOptions<TEntity>`, and Active Entity scoped data access. Do not add reverse layer dependencies.
- **DEP-004**: Existing test projects use xUnit/Shouldly/NSubstitute. New DoFiesta tests use existing centrally versioned `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.EntityFrameworkCore.Sqlite`. Any project reference/package change uses `dotnet` commands and central versions.
- **DEP-005**: WeatherFiesta integration factory uses a disposable SQL Server Testcontainer and requires a functioning Docker engine with sufficient resources. DoFiesta integration storage is isolated per test factory and never points at the developer's persistent database.
- **DEP-006**: DoFiesta browser verification uses the repo-local Playwright helper, Chromium, the built-in fictional development identity provider, and a development app launched by `dotnet run` or the workspace watch task. No user secret is required.
- **DEP-007**: Source drift gate: compare the source spec SHA-256 with the introduction snapshot before starting. If it differs, reconcile this plan's requirements/task coverage to the changed source and update the snapshot before execution; do not silently implement the older snapshot.
- **DEP-008**: [GitHub issue #52](https://github.com/BridgingIT-GmbH/bITdevKit/issues/52) publishes versioned specification and plan snapshots in linked issue comments. Restore both files onto the implementation branch before coding when they are absent; compare the published specification hash with DEP-007. Issue publication does not commit or push the local worktree.

## 5. Files

- **FILE-001**: New `src/Common.Serialization/Ndjson/NdjsonDefaults.cs`, `NdjsonMode.cs`, `NdjsonWriterOptions.cs`, and `NdjsonReaderOptions.cs`: constants and validated common options.
- **FILE-002**: New `src/Common.Serialization/Ndjson/NdjsonStreamOutcome.cs`, `INdjsonStreamObserver.cs`, and `NdjsonStreamException.cs`: exact observer/outcome contracts and safe failures.
- **FILE-003**: New `src/Common.Serialization/Ndjson/NdjsonEnvelope.cs`, `NdjsonEnvelopeJsonConverter.cs`, and `NdjsonEnvelopeReader.cs`: fixed version-1 shapes and lifecycle validation.
- **FILE-004**: New `src/Common.Serialization/Ndjson/NdjsonRecordBuffer.cs`, `NdjsonEncodedRecord.cs`, and `NdjsonReadRecord.cs`: bounded storage and owned/borrowed views.
- **FILE-005**: New `src/Common.Serialization/Ndjson/NdjsonWriter.cs` and `NdjsonReader.cs`: record/sequence writing and incremental framing.
- **FILE-006**: New `src/Presentation.Web/Ndjson/NdjsonStreamingOptions.cs`, `NdjsonEndpointOptions.cs`, `NdjsonEndpointDescriptor.cs`, and `NdjsonServiceCollectionExtensions.cs`: server configuration and registration.
- **FILE-007**: New `src/Presentation.Web/Ndjson/NdjsonEndpointConventionBuilderExtensions.cs` and `NdjsonOpenApiOperationTransformer.cs`: runtime descriptor and response metadata.
- **FILE-008**: New `src/Presentation.Web/Ndjson/NdjsonHttpResult.cs`, `NdjsonHttpResultOfT.cs` and `NdjsonHttpStreamExecutor.cs`: HTTP execution and encoding factories.
- **FILE-009**: New `src/Presentation.Web/Ndjson/NdjsonCompressionApplicationBuilderExtensions.cs` and `src/Presentation.Web/Result/Map/ResultMapNdjsonExtensions.cs`: compression gate and existing Result mapping integration.
- **FILE-010**: New `src/Common.Utilities/Http/NdjsonClientOptions.cs`, `NdjsonClientError.cs`, `NdjsonHttpClientExtensions.cs`, and `NdjsonHttpResponse.cs`: .NET client opening, ownership, read paths, and inactivity.
- **FILE-011**: New `tests/Common.UnitTests/Serialization/Ndjson/NdjsonTestStreams.cs`, `NdjsonTestTimeProvider.cs`, `NdjsonContractTests.cs`, `NdjsonRecordTests.cs`, `NdjsonReaderTests.cs`, `NdjsonSequenceWriterTests.cs`, `NdjsonEnvelopeTests.cs`, and `NdjsonObserverTests.cs`.
- **FILE-012**: New `tests/Common.UnitTests/Utilities/Http/NdjsonHttpClientTests.cs`.
- **FILE-013**: New `tests/Presentation.UnitTests/Web/Ndjson/NdjsonOptionsTests.cs`, `NdjsonMetadataTests.cs`, `NdjsonHttpResultTests.cs`, `NdjsonHttpObserverTests.cs`, `LoopbackStreamingFixture.cs`, `NdjsonNetworkTests.cs`, `StreamingProxyFixture.cs`, `NdjsonCompressionTests.cs`, and `NdjsonConsumerContractTests.cs`.
- **FILE-014**: New `examples/DoFiesta/DoFiesta.Application/Modules/Core/Queries/TodoItemStreamQuery.cs`, `Queries/StreamTicksQuery.cs`, and `Models/StreamTickModel.cs`.
- **FILE-015**: Modify `examples/DoFiesta/DoFiesta.Presentation.Web.Server/Modules/Core/Endpoints/CoreTodoItemEndpoints.cs`; add `CoreStreamingEndpoints.cs` in that directory; modify server `Program.cs`, `CoreModule.cs`, and `Components/App.razor`.
- **FILE-016**: New `examples/DoFiesta/DoFiesta.IntegrationTests/DoFiesta.IntegrationTests.csproj`, `DoFiestaApplicationFactory.cs`, `TestAuthenticationHandler.cs`, and `StreamingEndpointsTests.cs`; update `bITdevKit.slnx` with the project through the CLI.
- **FILE-017**: New `examples/DoFiesta/DoFiesta.Presentation.Web.Client/Services/StreamingDemoClient.cs` and `Pages/Streaming.razor`; modify client `Program.cs` and `Layout/NavMenu.razor`.
- **FILE-018**: Browser-only automation was retired at user request. No tracked browser scripts, screenshots or execution reports are required; use disposable scripts and ignored artifacts for future verification.
- **FILE-019**: New `examples/WeatherFiesta/WeatherFiesta.Application/Modules/Core/Queries/UserCitiesStreamQuery.cs`; modify `WeatherFiesta.Presentation.Web.Server/Modules/Core/Endpoints/WeatherEndpoints.cs` and server `Program.cs` under the same example root.
- **FILE-020**: New `examples/WeatherFiesta/WeatherFiesta.IntegrationTests/Modules/Core/Presentation/WeatherStreamingEndpointsTests.cs`; reuse existing `Modules/Core/WeatherFiestaApplicationFactory.cs` unchanged; seed fresh subscriptions/weather inside the new tests without changing isolation guarantees.
- **FILE-021**: New `docs/features-presentation-streaming.md`, `docs/validation/ndjson-streaming/README.md`, and `hosting.md`; modify `docs/common-serialization.md`, `docs/features-presentation-endpoints.md`, `docs/specs/spec-presentation-ndjson-streaming.md`, `src/Common.Serialization/README.md`, `src/Presentation.Web.Client/README.md`, and `mkdocs.yml`.
- **FILE-022**: Modify `tests/Presentation.UnitTests/Presentation.UnitTests.csproj` to link the two Common NDJSON fixture source files. The new DoFiesta test project links those sources too; neither test project references another test assembly.
- **FILE-023**: New `src/Common.Serialization/Ndjson/NdjsonCleanupCoordinator.cs`: shared one-budget producer settlement/disposal, bounded waiting, safe late lease release, and immutable cleanup status for Common and HTTP executions.
- **FILE-024**: New `src/Common.Serialization/Ndjson/NdjsonOutputSession.cs` `NdjsonDiagnostics.cs`, and `NdjsonAssemblyInfo.cs`: shared output accounting/deadlines and internal access for HTTP/client composition. Modify `src/Infrastructure.EntityFramework/ActiveEntity/EntityFrameworkActiveEntityProvider.cs` to order before paging, with the multi-page Weather regression test.

Existing `ISerializer.cs`, `ITextSerializer.cs`, requester behavior files, Auditing specification/implementation, database schemas, and unrelated endpoint contracts are preserved. Package references are added to the new test project only as DEP-004 requires; no runtime project/package addition is planned.

## 6. Testing

- **TEST-001**: Defaults and validation: exact sizes/intervals, immutable snapshots, finite-positive bounds, unset versus explicit duration-cap clearing, and reader compatibility flags.
- **TEST-002**: Bounded encoding: exact cap accepted, next JSON byte rejected before destination output, envelope overhead counted, null retained, custom converter line violations rejected, and pooled allocation/rent counts bounded.
- **TEST-003**: Exact-byte ownership: no sizing reserialization; owned records dispose once; reject disposed writes; borrowed views survive until advance; preserve original whitespace/escaping/LF/CRLF/empty compatibility delimiter; caller streams remain open.
- **TEST-004**: Framing: asynchronous-only/non-seekable streams, one-byte UTF-8 fragments, several records per read, strict malformed/BOM/empty/trailing-input rejection, and every allowed compatibility combination under the same byte cap.
- **TEST-005**: Envelope: structural casing independent of host settings, required null data, version/kind/duplicate-field rejection, empty completion, data counts, sanitized terminal error, and incomplete EOF detection.
- **TEST-006**: Consumption: typed/framed/envelope paths share framing, only one response enumeration is allowed, data-only readers preserve terminal failures, and early disposal releases buffers and responses.
- **TEST-007**: Backpressure: no unbounded source moves, record/destination writes awaited, source and buffers released on failure, and a real first record arrives before producer completion.
- **TEST-008**: Batching: byte/delay ceilings, delay not reset by new records, idle flush, large-record bypass, immediate control/final flush, and no optimistic counters for staged or failed batch output.
- **TEST-009**: Observer: exact single notification after cleanup/final flush, empty/pre-first-frame outcomes, source completion versus output failure, written/flushed prefix counters, conservative partial-write flag, first failure retained, separate observer failure/timeout, five-second fresh budget, and safe immutable fields.
- **TEST-010**: Configuration: inherited limits, explicitly cleared duration cap, descriptor snapshot stability, observer excluded from shared options, invalid settings rejected before requests, and consistent execution/metadata modes.
- **TEST-011**: Result/metadata: standard mapping for failed requester results, no failed-value enumeration or observer execution, typed success unions, NDJSON mode/version headers, safe optional attachment, and OpenAPI stream/per-record schemas rather than JSON array bodies.
- **TEST-012**: HTTP output: typed and encoded sources, exact bytes forwarded once, yielded owners released after write/batch copy, unyielded ownership retained by producer, raw-only encoded factory, and source cleanup before success completion.
- **TEST-013**: Liveness: admitted raw headers arrive before a gated first item without injecting data, immediate admitted envelope heartbeat, no success headers/heartbeat on rejected admission, 15-second idle heartbeat during pending source move, no competing moves/writes, and timers stop at termination.
- **TEST-014**: HTTP failures: safe error only at a complete boundary; partial write/write timeout aborts; cleanup failure prevents success footer; optional duration timeout uses a separate bounded error budget; cancellation remains distinct from unrelated cancellation exceptions and timeouts.
- **TEST-015**: Hosting: default response uncompressed, explicit compression preserves incremental records and heartbeats through the forwarding proxy, middleware gate does not change non-NDJSON semantics, and no guessed length/forced transfer headers.
- **TEST-016**: Client opening: GET/POST/authentication preserved, 2xx/media/mode/version checked, external raw servers allowed without DevKit headers, bounded safe error body, Results for initial failures, and caller-owned request/client lifetime preserved.
- **TEST-017**: Client inactivity: 45-second complete-record deadline, partial bytes do not reset, heartbeat resets activity, yielded-item processing does not consume the budget, raw timeout opt-in, response cleanup, and zero automatic reconnect requests.
- **TEST-018**: DoFiesta source/authorization: more than two 100-row batches, filtering/permissions maintained, stable order, denied rows do not corrupt paging offset, scopes released before yield, fictional ticks remain bounded and cancelable.
- **TEST-019**: Network: real loopback Kestrel and forwarding proxy expose first data before gated completion; finite/live and raw/envelope cases; socket cancellation releases resources; in-memory TestServer snapshots are supplementary evidence only.
- **TEST-020**: Blazor: real WASM .NET stream, fake-IDP sign-in, Start/Cancel/switch/navigation disposal, finite success/live heartbeat/error/incomplete UI states, and last-200 history with monotonically increasing total count.
- **TEST-021**: WeatherFiesta: per-user fixtures, zero/205 subscriptions, stable DisplayOrder/Id batches, stored weather/staleness mapping, no upstream calls, cancellation, and oversized-record behavior.
- **TEST-022**: Regression: existing WeatherFiesta CSV/JSON endpoints and normal DoFiesta Todo routes retain media types, authorization, and behavior.
- **TEST-023**: Generic consumer extension: budget the exact framed bytes before admission, hash selected original records without reserialization, distinguish representation changes, permit business Partial plus transport success, and prohibit publishing success after final flush/cleanup failure without any Auditing package reference.
- **TEST-024**: Cleanup: validate five-second default and finite-positive overrides; use manual time and gated non-cooperative async moves/disposal to prove a single budget, no concurrent disposal, timeout failure with `CleanupCompleted=false`, cancellation precedence, prior-failure preservation, late encoded-lease disposal, no early pool reuse, no late output, and one observer call. Release every fixture gate during test teardown.
- **TEST-025**: Client terminal API: both read paths throw on the same error-parsing pull with local/remote code and trace fields, yield no error value, omit the remote message, and dispose the response. Envelope reading still yields heartbeat/completed; completion succeeds while the transport remains open.
- **TEST-026**: Admission boundary: finite and live raw responses commit headers before their first move; denied/invalid admission commits none; source/size failure on the first item after admission aborts rather than returning a new HTTP error; header start is bounded by the response write deadline.

Validation commands are run from the repository root. Runtime tests below are future execution gates, not checks performed while writing this plan.

- **VLD-001**: `dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --nologo --filter FullyQualifiedName~Ndjson --logger "trx;LogFileName=ndjson-common.trx" --results-directory artifacts/ndjson-streaming/test-results`
- **VLD-002**: `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --nologo --filter FullyQualifiedName~Ndjson --logger "trx;LogFileName=ndjson-presentation.trx" --results-directory artifacts/ndjson-streaming/test-results`
- **VLD-003**: `dotnet test examples/DoFiesta/DoFiesta.IntegrationTests/DoFiesta.IntegrationTests.csproj --nologo --logger "trx;LogFileName=ndjson-dofiesta.trx" --results-directory artifacts/ndjson-streaming/test-results`
- **VLD-004**: `dotnet test examples/WeatherFiesta/WeatherFiesta.IntegrationTests/WeatherFiesta.IntegrationTests.csproj --nologo --filter FullyQualifiedName~WeatherStreamingEndpointsTests --logger "trx;LogFileName=ndjson-weatherfiesta.trx" --results-directory artifacts/ndjson-streaming/test-results`
- **VLD-005**: Use workspace task `DoFiesta - watch`, or `dotnet run --project examples/DoFiesta/DoFiesta.Presentation.Web.Server/DoFiesta.Presentation.Web.Server.csproj -- --urls 'https://localhost:5001;http://localhost:5000'`. Probe readiness with certificate validation disabled only for local test HTTPS. Track the launched process and stop it before any build that would lock its outputs.
- **VLD-006**: When browser verification is needed, exercise the authenticated streaming page against an isolated host with disposable automation or manual review. Check mode switching, bounded history, cancellation and cleanup. Keep screenshots and reports under ignored `artifacts/ndjson-streaming/`; no checked-in browser harness is required.
- **VLD-007**: Stop owned example watch/run processes; run workspace task `Solution - build` or `dotnet build` from root. Then run `Solution - tests (unit)` and `Solution - tests (integration)` from `.vscode/tasks.json` sequentially, followed by the complete DoFiesta and WeatherFiesta example integration projects because the workspace task scans only `tests/`. Finish with `git diff --check`; record every failed gate rather than claiming completion.

When the task runner is unavailable, use `dotnet build --nologo`, then this CLI equivalent of the test tasks and the required example checks. Run it after the focused and browser checks, not concurrently with them:

```python
from pathlib import Path
import subprocess

for category in ("UnitTests", "IntegrationTests"):
	for project in sorted(Path("tests").rglob("*.csproj")):
		if project.stem.endswith("." + category) and not any(
			name in project.stem for name in ("EventSourcing", "WeatherForecast")
		):
			subprocess.run(["dotnet", "test", str(project), "--nologo"], check=True)

for project in (
	"examples/DoFiesta/DoFiesta.IntegrationTests/DoFiesta.IntegrationTests.csproj",
	"examples/WeatherFiesta/WeatherFiesta.IntegrationTests/WeatherFiesta.IntegrationTests.csproj",
):
	subprocess.run(["dotnet", "test", project, "--nologo"], check=True)

subprocess.run(["git", "diff", "--check"], check=True)
```

## 7. Risks & Assumptions

- **RISK-001**: A borrowed `ReadOnlyMemory<byte>` cannot be revoked after capture. Public lifetime documentation, disposed-owner guards, pooled-range clearing, and tests prevent false safety guarantees; callers retaining a borrowed view remain responsible for an explicit bounded copy.
- **RISK-002**: JSON converters and producer code can allocate their own large graphs, block synchronously, or ignore cancellation. The five-second cleanup grace bounds asynchronous waiting, not arbitrary code execution. Pending operations can retain independently owned scopes/buffers until settlement. Record caps do not imply globally constant process memory or forced release; require cooperative producers and report cleanup timeouts.
- **RISK-003**: A destination may accept part of a failed write. Retain confirmed counters, set uncertainty, abort, and never replay the ambiguous frame automatically. Observer success does not prove recipient acknowledgment.
- **RISK-004**: A source may finish but flush or cleanup may fail. Emitting success before these boundaries would publish a false completion; the executor must finalize source cleanup and pending output before its success record and observer.
- **RISK-005**: Global middleware or an external proxy can buffer/compress independently of the feature. The supported recipe and gate order must be tested. The loopback forwarding profile is not a certification of every production ingress.
- **RISK-006**: Offset paging can observe concurrent inserts/deletes. The example endpoints promise bounded retrieval and stable ordering within the selected reads, not a transaction-wide snapshot or lossless resume. Do not add an unrequested replay store to hide this limitation.
- **RISK-007**: Non-cooperative observers can outlive their wait budget. Never retry or let them hold request-scoped services; report notification failure separately and do not change finalized transport status.
- **ASSUMPTION-001**: The reviewed source spec remains authoritative, including its exact-byte and terminal-observer refinements. Changes after this snapshot trigger DEP-007 reconciliation.
- **ASSUMPTION-002**: Existing .NET 10 browser streaming and authenticated DoFiesta clients are reused. Browser test JavaScript is automation only and does not expand the shipped-client scope.
- **ASSUMPTION-003**: DoFiesta integration-test scaffolding is new at this baseline. WeatherFiesta's existing factory, DTOs, current-user access, and stored-weather model are reused without adding a live weather subscription.

## 8. Related Specifications / Further Reading

- [NDJSON Streaming specification](../docs/specs/spec-presentation-ndjson-streaming.md), especially sections 4.2.1, 6.11, 7, and 8.
- [Common serialization guidance](../docs/common-serialization.md).
- [Presentation endpoints guidance](../docs/features-presentation-endpoints.md).
- [Minimal API endpoint architecture](../docs/adr/0014-minimal-api-endpoints-dto-exposure.md).
- [Repository agent instructions](../AGENTS.md) and [authoritative architecture/coding instructions](../.github/copilot-instructions.md).
- [NDJSON community specification](https://github.com/ndjson/ndjson-spec).
- [ASP.NET Core Minimal API response extension points](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0).
- [.NET 10 browser HTTP streaming](https://learn.microsoft.com/en-us/dotnet/core/compatibility/networking/10.0/default-http-streaming).
- [ASP.NET Core response compression](https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-10.0).

## 9. Implementation reconciliation

Version 1.2 records the bounded `Utf8JsonWriter` reservation allowance described in specification section 6.2, without changing the logical record limit. Shared output mechanics live in `NdjsonOutputSession`; the generic HTTP result lives in `NdjsonHttpResultOfT.cs`. DoFiesta explicitly registers `CoreStreamingEndpoints` in `CoreModule.cs`. Its SQLite fixture translates provider-specific sequence/schema annotations in test-only model customization. Generated OpenAPI and Kiota files follow the app's existing build process; streaming UI consumption uses the .NET helper directly.

Task completion and validation evidence are recorded after execution below and in `docs/validation/ndjson-streaming/README.md`.

The 205-row SQL integration proof found that `EntityFrameworkActiveEntityProvider` composed paging before ordering. The prerequisite correction applies all order keys before Skip/Take, preserving a globally ordered stream across bounded pages. The Weather regression test exercises this through the real provider.

The browser launch forwards application URL arguments after `--`. Passing `--nologo` to `dotnet run` in this SDK forwarded it to the application and consumed the following `--urls` argument; the corrected invocation binds the planned HTTPS endpoint.

## 10. Completion evidence

All 44 tasks are complete. The final source build has zero warnings/errors. Required suites have 8,152 passing tests and 142 existing skips; the initial outbox timing failure and successful unchanged full-suite retry are recorded. Focused NDJSON checks pass 103 cases, and the final real-WASM browser report passes 11 checkpoints. The disposable host is stopped, local ports are closed, and its SQL fixture was verified removed. No commit, push, publish, or deployment was performed at that validation checkpoint.

The browser harness and checked-in execution reports were subsequently removed at user request. Historical evidence is retained locally under ignored `artifacts/ndjson-streaming/retired-browser/evidence/`; future test runs write to `artifacts/ndjson-streaming/`.
