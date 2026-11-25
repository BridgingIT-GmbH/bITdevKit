---
goal: 'Improve the usability and adoption of five Common APIs'
version: '1.0'
date_created: '2026-11-25'
last_updated: '2026-11-25'
owner: 'bITdevKit maintainers'
status: 'Completed'
tags: ['feature', 'common', 'compatibility', 'performance']
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

Make keyed locks easier to acquire, adopt friendly durations in three features, accept scalar metadata from a JSON DOM, bound UTF-8 truncation work, and let HTTP callers decline date-based range validation. The plan builds on the Common extractions present in `d535627ae` and records a separate implementation phase for each improvement.

## 1. Requirements & Constraints

- **REQ-001** — C06 adds a single-call timed acquisition that returns an owned lease on success, null on timeout, and propagates cancellation.
- **REQ-002** — C02/A01 adds opt-in invariant duration parsing to Queueing and Orchestrations startup delays and to annotated Console Commands options and arguments. The existing Common duration grammar supplies the value, and each feature retains its validation.
- **REQ-003** — C04 adds a scalar parser for `JsonElement`, with the same conversion rules as the string parser. Blob and Document maintenance endpoints use it for body metadata and preserve their public request models.
- **REQ-004** — C01 bounds input scanning by the UTF-8 byte budget and retains the current byte, Unicode, and return-value semantics.
- **REQ-005** — C05 adds an explicit way to decline date-based `If-Range` validation while still accepting a matching strong ETag and preserving both existing comparison modes.
- **SEC-001** — Endpoints continue to own authorization and atomically enforce mutation conditions. A declined or mismatched date-based `If-Range` selects the full representation.
- **CON-001** — Preserve existing public signatures, named arguments, overload resolution, default behavior, exceptions, and wire or persisted formats. Adopt different behavior only at the new entry points defined below.
- **CON-002** — Use existing project references and .NET libraries. Preserve the existing Common package boundaries and avoid new dependencies.
- **CON-003** — Keep C07 counting streams and C08 CSV, duration grammar extensions, and metadata type inference outside these phases.
- **GUD-001** — Put working examples and usage rules in the owning guides. Add XML documentation and a fictional example for each new public type or method.
- **GUD-002** — Keep each improvement reviewable on its own. Run builds and tests sequentially in a given worktree. Apply changes to shared Markdown files sequentially and retain each phase's section edits.
- **PAT-001** — Reuse the current implementation behind each added entry point. Keep flags, validator interpretation, and input policies at the boundary that owns them.

## 2. Implementation Steps

Phases 1 through 5 have no runtime dependencies on one another. Within a phase, start tasks after their listed dependencies. Each phase includes its own documentation and tests.

### Implementation Phase 1

- GOAL-001 — Provide the common single-key lock acquisition path. Complete when the pool returns an acquired lease or null, existing rentals retain their lifecycle, and TEST-001 and TEST-002 pass.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-001 | In `src/Common.Utilities/Threading/AsyncKeyedLock.cs`, add `public Task<ILease> TryAcquireAsync(TKey key, TimeSpan timeout, CancellationToken cancellationToken = default)` as an async method. Rent once via `Rent(key)`, await that lease's `TryAcquireAsync(timeout, cancellationToken)` with `ConfigureAwait(false)`, and return the same acquired lease on true or null on false. Reuse the existing lease cleanup for timeout and failed waits. Keep `Rent`, `ILease`, and File Storage's two-path reservation and lock order. Document that the caller disposes a non-null result. | Yes | 2026-11-25 |
| TASK-002 | Depends on TASK-001. Add TEST-001 to `tests/Common.UnitTests/Utilities/AsyncKeyedLockTests.cs` using the pool-level overload. Use the existing comparer and entry-retirement fixtures to check that failed waits return their reservations. Run the File Storage regression filter in TEST-002. | Yes | 2026-11-25 |
| TASK-003 | Depends on TASK-001. In `docs/common-utilities.md` under **Keyed asynchronous locks**, make one-call acquisition the first example. Use `using var lease = await locks.TryAcquireAsync("report-42", TimeSpan.FromSeconds(2), cancellationToken);` and check for null before accessing the resource. Retain `Rent` and its explicit acquisition as the documented path for reserving a key in advance. | Yes | 2026-11-25 |

### Implementation Phase 2

- GOAL-002 — Give Queueing, Orchestrations, and Console Commands a shared duration grammar through opt-in APIs. Complete when TEST-003 through TEST-005 pass for both opted-in and existing callers.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-004 | In `src/Application.Queueing/QueueingOptionsBuilder.cs`, add `public QueueingOptionsBuilder StartupDelayInvariant(string value)`. In `src/Application.Orchestrations/Authoring/OrchestrationBuilderContext.cs`, add `public OrchestrationBuilderContext StartupDelayInvariant(string value)`. Each calls `value.TryParseDuration(out var duration)`, throws `ArgumentException("Supply a valid invariant duration.", nameof(value))` before changing settings when parsing fails, and returns `this.StartupDelay(duration)` on success. Retain all existing overloads and the typed setters' range policy. | Yes | 2026-11-25 |
| TASK-005 | Add `src/Presentation/ConsoleCommands/ConsoleCommandDurationAttribute.cs` with `public sealed class ConsoleCommandDurationAttribute : Attribute` in `BridgingIT.DevKit.Presentation`, marked `[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]`. In `ConsoleCommandBinder.cs`, add init-only `UseFriendlyDuration` to the private `OptionMeta` and `ArgumentMeta` records. Set it in `GetMeta` via `p.IsDefined(typeof(ConsoleCommandDurationAttribute), inherit: false)`. Pass it from both `TryBind` conversion sites into private `TryConvert`. For `TimeSpan` and `TimeSpan?`, choose `raw.TryParseDuration` when true and the existing invariant `TimeSpan.TryParse` when false. Reuse the existing option and argument errors on failure. Other types retain their conversion path even when annotated. | Yes | 2026-11-25 |
| TASK-006 | Depends on TASK-004. Add TEST-003 in `tests/Application.UnitTests/Queueing/QueueingOptionsBuilderDurationTests.cs` and `tests/Application.UnitTests/Orchestrations/OrchestrationBuilderContextTests.cs`. Verify the builder and execution settings after fluent calls, including the state after a failed parse. | Yes | 2026-11-25 |
| TASK-007 | Depends on TASK-005. Add TEST-004 in `tests/Presentation.UnitTests/ConsoleCommands/ConsoleCommandDurationBindingTests.cs` using test commands derived from `ConsoleCommandBase` and binding through `ConsoleCommandBinder.TryBind`. Run TEST-005, including the existing Profiling command suite. | Yes | 2026-11-25 |
| TASK-008 | Depends on TASK-004 and TASK-005. Document `StartupDelayInvariant("500ms")` in `docs/features-queueing.md` and `docs/features-orchestrations.md`. Under **Options and arguments** in `docs/features-presentation-console-commands.md`, show a `TimeSpan` option and nullable positional duration annotated with `[ConsoleCommandDuration]`. Link from all three guides to **Parse durations** in `docs/common-extensions.md`. Update A01 in the extraction spec with the shipped opt-in entry points and test results. | Yes | 2026-11-25 |

### Implementation Phase 3

- GOAL-003 — Parse scalar metadata directly from a JSON DOM and adopt the path in Blob and Document maintenance. Complete when TEST-006 and TEST-007 pass and preserve update semantics for omitted, null, and empty metadata.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-009 | In `src/Common.Serialization/ScalarPropertyBagJson.cs`, add `public static Result<PropertyBag> ParseElement(JsonElement json, bool preserveMissing = false)`. Move the existing root-kind check and property traversal from `Parse(string, bool)` into private `ParseRoot(JsonElement, bool)`. Share it across both entry points. Treat `Undefined` and `Null` elements as missing. Keep the string method's null, whitespace, and parse handling. Preserve the current conversion, first-spelling/last-value duplicate rules, validation error mapping, and the documented `InvalidOperationException` on malformed surrogate decoding. Finish reading the element during the call and return a bag containing CLR scalars. A disposed source document retains its normal `ObjectDisposedException`. | Yes | 2026-11-25 |
| TASK-010 | Depends on TASK-009. In `src/Presentation.Web.Storage/Documents/DocumentStorageMaintenanceEndpoints.cs`, replace `HttpMetadata.ParseProperties(JsonSerializer.Serialize(request.Properties), preserveMissing: true)` in `WriteAsync` and `UpdatePropertiesAsync` with `ScalarPropertyBagJson.ParseElement(JsonSerializer.SerializeToElement(request.Properties), preserveMissing: true)`. Apply the same replacement to the JSON properties update in `src/Presentation.Web.Storage/Blobs/BlobStorageMaintenanceEndpoints.cs`. Retain each endpoint's failure mapping, request model, and mutation options. Keep text-header callers on `HttpMetadata.ParseProperties`. | Yes | 2026-11-25 |
| TASK-011 | Depends on TASK-009. Add TEST-006 to `tests/Common.UnitTests/Serialization/ScalarPropertyBagJsonTests.cs`. Compare result status, stored types, values, lookup and key spelling against the string entry point. Store the successful result beyond the lifetime of the source `JsonDocument` in the ownership case. | Yes | 2026-11-25 |
| TASK-012 | Depends on TASK-010. Add or extend TEST-007 in `tests/Presentation.UnitTests/Web/Storage/DocumentStorageMaintenanceEndpointsTests.cs` and `tests/Presentation.UnitTests/Web/Storage/BlobStorageMutationEndpointsTests.cs`. Submit representative request bodies through the existing HTTP fixtures and inspect the provider's stored state. | Yes | 2026-11-25 |
| TASK-013 | Depends on TASK-009. In `docs/common-serialization.md` under **Plain scalar JSON**, add a `ParseElement` example showing `JsonDocument.RootElement`, the input lifetime, and the missing-versus-empty rules. Keep the existing string example. | Yes | 2026-11-25 |

### Implementation Phase 4

- GOAL-004 — Avoid scanning the unneeded suffix of a long string while keeping the UTF-8 output contract. Complete when TEST-008 confirms output parity and BEN-001 measures both fitting and truncated inputs.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-014 | In `src/Common.Abstractions/Extensions/StringExtensions.cs`, keep `TruncateUtf8`'s negative-limit guard and null handling first. Gate the existing `Encoding.UTF8.GetByteCount(source) <= maximumBytes` check with `source.Length <= maximumBytes`. When the UTF-16 length exceeds the byte budget, skip the full count and enter the existing rune loop. Stop at the first rune that exceeds the remaining bytes. Keep the two-character stack buffer, fitting-string identity, and replacement behavior for retained malformed UTF-16 on truncation. | Yes | 2026-11-25 |
| TASK-015 | Depends on TASK-014. Add TEST-008 to `tests/Common.UnitTests/Abstractions/Extensions/StringExtensionsTests.cs` and run the existing `tests/Application.UnitTests/Auditing/AuditCollectorTextTests.cs` facade tests. Include a cut through a supplementary scalar's byte width and a fitting malformed input that must be returned by identity. | Yes | 2026-11-25 |
| TASK-016 | Depends on TASK-014. Add `benchmarks/Common.Benchmarks/Utf8TruncationBenchmarks.cs` using BenchmarkDotNet and `[MemoryDiagnoser]`. Add a public `Before()` marked `[Benchmark(Baseline = true)]` that calls a private copy of `TruncateUtf8` from `d535627ae`. Add a public `After()` marked `[Benchmark]` that calls the updated API. Run BEN-001 and document its command and cases in `benchmarks/Common.Benchmarks/README.md`. | Yes | 2026-11-25 |
| TASK-017 | Depends on TASK-014. In `docs/common-extensions.md` under **Truncate text to a UTF-8 byte limit**, document the bounded scan for inputs whose UTF-16 length exceeds the budget. Keep the byte limit and unpaired-surrogate examples. | Yes | 2026-11-25 |

### Implementation Phase 5

- GOAL-005 — Let HTTP consumers use strong ETag range validation when their timestamps cannot identify a representation reliably. Complete when TEST-009 and TEST-010 exercise the new opt-out alongside both existing comparison modes.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-018 | In `src/Common.Extensions.Web/HttpRepresentationHeaders.cs`, add `SelectRange(HttpRequest request, long length, string entityTag, DateTimeOffset lastModified, HttpRepresentationHeaderMode mode, bool allowIfRangeDate)` returning `HttpRepresentationRangeSelection`. Forward the existing mode overload to it with `allowIfRangeDate: true` and retain the existing strict default on the four-argument overload. Pass the flag to private `MatchesIfRange`. In strict mode gate the `condition.LastModified` comparison on the flag and leave strong ETag comparison in place. In legacy mode gate the existing date branch after the ETag comparison. With the flag false a date-valued `If-Range` selects `Full` before range parsing. Requests without `If-Range` follow the same byte selection as before. | Yes | 2026-11-25 |
| TASK-019 | Depends on TASK-018. Add TEST-009 to `tests/Common.UnitTests/Extensions/HttpRepresentationHeadersTests.cs`, exercising the new overload with explicit mode and flag. Run TEST-010 against the existing Presentation facade and Application Caching HTTP conditions. | Yes | 2026-11-25 |
| TASK-020 | Depends on TASK-018. In `docs/common-extensions.md` under **Evaluate representation conditions and byte ranges**, show the required named arguments `mode: HttpRepresentationHeaderMode.Strict` and `allowIfRangeDate: false`. Explain that decline selects the full response for a date header and leaves matching strong ETags eligible for partial content. | Yes | 2026-11-25 |

### Implementation Phase 6

- GOAL-006 — Close verification for all five phases. Depends on Phases 1–5 and completes when TEST-011 passes and the spec records verified results for each phase.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-021 | Run TEST-011, verify the examples against their compiled public signatures, and review the public API diff against `d535627ae`. Record test counts, benchmark results, and any environment-specific skips in the **Implementation progress** section of `docs/specs/spec-common-feature-extractions.md`. Keep the extraction count as tracked by that spec because these items improve existing extractions. | Yes | 2026-11-25 |

## 3. Alternatives

- **ALT-001** — Use `Rent` and lease-level acquisition everywhere. The plan retains that workflow for pre-reservation and adds a shorter path for a single key.
- **ALT-002** — Switch existing duration settings and command inputs to the friendly parser by default. That would change the accepted grammar and invalid-input behavior of existing entry points.
- **ALT-003** — Add a second `Parse` overload that takes `JsonElement`. The distinct name `ParseElement` preserves source binding for existing calls such as `Parse(default)`.
- **ALT-004** — Change public metadata request properties to `JsonElement`. Keeping the dictionary DTOs preserves client signatures and binding. `SerializeToElement` provides a DOM without the intermediate JSON string.
- **ALT-005** — Replace the UTF-8 byte-count preflight with rune iteration for every input. The length guard retains the fast preflight for values that may fit.
- **ALT-006** — Add a five-argument `SelectRange` overload whose last argument is a Boolean. The six-argument overload preserves existing calls that pass `default` for the mode.

## 4. Dependencies

- **DEP-001** — bITdevKit's .NET SDK from `global.json` and current version pins in `Directory.Packages.props`.
- **DEP-002** — Existing implementations of `AsyncKeyedLock<TKey>`, `TryParseDuration`, `ScalarPropertyBagJson`, `TruncateUtf8`, and `HttpRepresentationHeaders` at `d535627ae`.
- **DEP-003** — Existing Common references from Application Queueing, Application Orchestrations, Presentation, and Presentation Web Storage.
- **DEP-004** — Existing xUnit, Shouldly, ASP.NET Core test hosts, BenchmarkDotNet, and the repository's MkDocs workflow.

## 5. Files

- **FILE-001** — C06 implementation and tests in `src/Common.Utilities/Threading/AsyncKeyedLock.cs` and `tests/Common.UnitTests/Utilities/AsyncKeyedLockTests.cs`. Guide in `docs/common-utilities.md`.
- **FILE-002** — A01 builder APIs in `src/Application.Queueing/QueueingOptionsBuilder.cs` and `src/Application.Orchestrations/Authoring/OrchestrationBuilderContext.cs`. Tests in `tests/Application.UnitTests/Queueing/QueueingOptionsBuilderDurationTests.cs` and `tests/Application.UnitTests/Orchestrations/OrchestrationBuilderContextTests.cs`.
- **FILE-003** — A01 binder and marker in `src/Presentation/ConsoleCommands/ConsoleCommandBinder.cs` and `src/Presentation/ConsoleCommands/ConsoleCommandDurationAttribute.cs`. Tests in `tests/Presentation.UnitTests/ConsoleCommands/ConsoleCommandDurationBindingTests.cs`. Guides in `docs/features-queueing.md`, `docs/features-orchestrations.md`, and `docs/features-presentation-console-commands.md`.
- **FILE-004** — C04 parser and tests in `src/Common.Serialization/ScalarPropertyBagJson.cs` and `tests/Common.UnitTests/Serialization/ScalarPropertyBagJsonTests.cs`. Guide in `docs/common-serialization.md`.
- **FILE-005** — C04 endpoint adoption in `src/Presentation.Web.Storage/Blobs/BlobStorageMaintenanceEndpoints.cs` and `src/Presentation.Web.Storage/Documents/DocumentStorageMaintenanceEndpoints.cs`. Tests in `tests/Presentation.UnitTests/Web/Storage/BlobStorageMutationEndpointsTests.cs` and `tests/Presentation.UnitTests/Web/Storage/DocumentStorageMaintenanceEndpointsTests.cs`.
- **FILE-006** — C01 implementation and tests in `src/Common.Abstractions/Extensions/StringExtensions.cs` and `tests/Common.UnitTests/Abstractions/Extensions/StringExtensionsTests.cs`. Benchmark and run instructions in `benchmarks/Common.Benchmarks/Utf8TruncationBenchmarks.cs` and `benchmarks/Common.Benchmarks/README.md`.
- **FILE-007** — C05 overload and tests in `src/Common.Extensions.Web/HttpRepresentationHeaders.cs` and `tests/Common.UnitTests/Extensions/HttpRepresentationHeadersTests.cs`.
- **FILE-008** — Shared guide edits in `docs/common-extensions.md` and verification record in `docs/specs/spec-common-feature-extractions.md`.

## 6. Testing

- **TEST-001** — Pool-level acquisition returns a non-null lease for a free key. A comparer-equivalent held key times out and returns null, and a distinct key proceeds. Cover zero and positive timeouts, pre-cancellation, cancellation during a wait, a disposed pool, and an invalid timeout. Verify that disposal restores acquisition and that a canceled or timed-out attempt leaves active holders and waiters intact. Verify unused entry retirement with the existing fixture.
- **TEST-002** — Run `dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter 'FullyQualifiedName~LocalFileStorage|FullyQualifiedName~FileSystemReviewRegressionTests'` and confirm existing single- and two-path locking and timeout behavior.
- **TEST-003** — Set each builder's startup delay to one second. Under `en-US` and `nl-NL`, assert that the new method turns `500ms` into 500 milliseconds and `1.5m` into 90 seconds. Assert that `invalid`, null, and overflow throw with parameter name `value` and leave the setting at its prior value. Compare successful calls with the typed overload. Assert that existing string overloads ignore `500ms` and still accept `00:00:30`. Restore the original culture after each case.
- **TEST-004** — Bind an annotated `TimeSpan` option and annotated nullable positional argument through `TryBind` using `500ms` and `1.5m`. Assert the same ticks in `en-US` and `nl-NL`. Confirm that invalid duration input uses the existing binding errors and leaves the target property unchanged. Verify unannotated TimeSpan options and arguments still accept standard text and reject friendly suffixes. Verify an annotated non-TimeSpan property retains its normal binding.
- **TEST-005** — Run `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --filter 'FullyQualifiedName~QueueingOptionsBuilderDurationTests|FullyQualifiedName~OrchestrationBuilderContextTests'` and `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --filter 'FullyQualifiedName~ConsoleCommandDurationBindingTests|FullyQualifiedName~ProfilingConsoleCommandTests'`.
- **TEST-006** — Compare string and element entry points for `null`, `{}`, scalar values, case-equivalent duplicates, blank keys, arrays, nested objects, a non-object root, overflow, underflow, and well-formed Unicode. Cover malformed escaped names and strings with the existing documented exception. Explicitly test `default(JsonElement)` with both missing policies, an element from a disposed document, and successful use of the bag after its document is disposed. Compile `ScalarPropertyBagJson.Parse(default)` to verify source compatibility.
- **TEST-007** — For Document write and Document and Blob property updates, verify omitted and explicit null preserve existing metadata, `{}` clears it, and valid scalars keep the existing CLR types, including explicit null values. Verify nested values produce the existing validation response and leave the stored bag intact. Run `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --filter 'FullyQualifiedName~DocumentStorageMaintenanceEndpointsTests|FullyQualifiedName~BlobStorageMutationEndpointsTests'`.
- **TEST-008** — Exercise `TruncateUtf8` with null, empty, zero, negative, fitting ASCII and non-ASCII, a long ASCII input with budget 32, a supplementary scalar that cannot fit, and malformed UTF-16 that fits or truncates. Assert exact output and reference identity for fitting inputs, plus existing Auditing facade behavior. Run `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --filter 'FullyQualifiedName~AuditCollectorTextTests'`.
- **BEN-001** — Compare the baseline and public `TruncateUtf8` in Release mode on the same host. Include `new string('a', 1_048_576)` with budget 32, 32 ASCII characters with budget 32, 32 `é` characters with budget 64, and 32 `é` characters with budget 32. Assert matching results during benchmark setup. Run `dotnet run -c Release --project benchmarks/Common.Benchmarks -- --filter '*Utf8TruncationBenchmarks*'`. Capture median duration, allocated bytes, runtime, and host for all cases using at least five measured iterations. Report the long-input improvement and any fitting-input regressions. Keep timing assertions out of xUnit.
- **TEST-009** — With `allowIfRangeDate: false`, assert that an otherwise matching date yields `Full` in strict and legacy modes, a matching strong ETag still yields `Partial`, and a mismatch or weak ETag yields `Full`. Check that absent `If-Range` keeps normal range selection. With the flag true, assert strict exact-date behavior and legacy later-date behavior. Compile `SelectRange(request, 10, entityTag, lastModified, default)` to verify source compatibility.
- **TEST-010** — Run `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --filter 'FullyQualifiedName~HttpRepresentationConditionsTests|FullyQualifiedName~CacheHttpConditionalTests'`. Verify that date comparisons and range unit casing retain their legacy behavior through the existing Presentation entry points.
- **TEST-011** — Run `dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --filter 'FullyQualifiedName~AsyncKeyedLockTests|FullyQualifiedName~TimeSpanExtensionsTests|FullyQualifiedName~ScalarPropertyBagJsonTests|FullyQualifiedName~StringExtensionsTests|FullyQualifiedName~HttpRepresentationHeadersTests'` plus the affected feature test filters above. Run `git diff --check`. Sync docs with `pwsh -File docs/site/scripts/sync-docs.ps1`, then run `docker run --rm -v "$PWD:/docs" squidfunk/mkdocs-material:9 build --strict --site-dir /tmp/bitdevkit-common-api-docs`. Record any environment-specific test skips with their stated reason.

## 7. Risks & Assumptions

- **RISK-001** — A lease returned before successful acquisition could break exclusion. The pool overload transfers lease ownership only after the existing acquisition reports success.
- **RISK-002** — Changing duration parsing implicitly could change existing startup timing. The builder method names and per-property marker make adoption explicit.
- **RISK-003** — Changing the metadata input type at the public DTO or altering null and empty handling could change a storage update. Only the DOM handoff inside the endpoint changes.
- **RISK-004** — Optimizing truncation can change malformed UTF-16 behavior or lose the fitting-string fast path. Preserve the existing rune loop and test byte boundaries and fitting-object identity.
- **RISK-005** — An overload can break source clients even when existing signatures remain. Use `ParseElement` and the required six-argument range overload to preserve existing calls with default literals.
- **ASSUMPTION-001** — Startup delay is the A01 builder scope. Each consumer can adopt friendly duration syntax explicitly at its current parsing boundary.
- **ASSUMPTION-002** — Existing pool and stream ownership contracts and the C05 compatibility profiles from the extraction branch remain the baseline.

## 8. Related Specifications / Further Reading

- [Common feature extractions](../docs/specs/spec-common-feature-extractions.md)
- [A01 invariant duration alignment](../docs/specs/spec-common-feature-extractions.md#a01-share-one-invariant-duration-grammar)
- [Keyed asynchronous locks](../docs/common-utilities.md#keyed-asynchronous-locks)
- [Plain scalar JSON](../docs/common-serialization.md#plain-scalar-json)
- [Common Extensions](../docs/common-extensions.md)
- [Common benchmark workflow](../benchmarks/Common.Benchmarks/README.md)
