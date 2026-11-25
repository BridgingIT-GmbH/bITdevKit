---
created: 2026-11-17
last_updated: 2026-11-25
status: draft
---

# Design Specification: Common Feature Extractions

> Extract small, general-purpose helpers from DevKit features and align avoidable behavioral differences through existing Common packages.

[TOC]

## Purpose and status

This specification records extraction decisions and tracks related alignment work.

Common is already the shared foundation used by features. The target is reusable code still owned by Application, Infrastructure, or Presentation features. Existing Common implementations were checked only to avoid proposing duplicate utilities and to identify contracts the extractions must respect.

The scan indexed 3,708 C# source files across 72 projects under `src/`, excluding build output. Candidate implementations, consumers, package references, and relevant tests were inspected selectively. This is a broad first pass, not a claim that every source file received a line-by-line review.

Initial scan baseline: `2493219fee1576b98be8e672209d4d2e8d73a5fa`. Latest reviewed `main`: `f7bf71de1d7710fe583d01249409f3aaea6b6860`.

## Implementation progress

Nine of eleven extraction candidates are implemented. PR #145 delivered C11. PR #146 delivered C03, C04, and C10. This branch implements C01, C02, C05, C06, and C09. C07 and C08 are marked **Won't do**, along with their related alignments A03 and A02. [PR #145](https://bridgingitgruppe.ghe.com/bit-intern/bitdevkit/pull/145) and [PR #146](https://bridgingitgruppe.ghe.com/bit-intern/bitdevkit/pull/146)

PR #146 reports passing checks for 152 Common tests, 144 Presentation HTTP and Storage tests, 436 Application Caching tests, and 24 focused Infrastructure tests. It also reports 104 passing facade cases from a pre-extraction test assembly run against the updated binaries. The six refreshed example lock files passed locked-mode restore. Documentation sync and the native strict MkDocs build passed. The full Pages and DocFX workflow requires Docker, which was unavailable for that verification. [PR #146 verification](https://bridgingitgruppe.ghe.com/bit-intern/bitdevkit/pull/146)

C01, C02, and C05 were verified with the Common, Application, and Presentation unit test projects. A standalone client compiled against the pre-extraction assemblies passed 16 checks against the updated assemblies without recompilation. The same source compiled and passed against the updated assemblies. The Common guide examples ran with Common-only DevKit references, and the strict MkDocs build passed.

C06 and C09 passed 3,643 Common, 2,242 Presentation, and 1,287 Infrastructure unit tests. Infrastructure skipped 26 cases that require other platforms or share fixtures. A client compiled against the original File Storage and Presentation assemblies passed all 15 checks with the updated assemblies, including the exact Profiling key hash. The same source compiled and passed against the updated assemblies. The Common guide examples ran with Common-only DevKit references, and the strict MkDocs build passed.

The C01 and C05 review fixes passed 108 cases from the affected xUnit fixtures in an isolated runner and 89 existing Presentation regression cases against the updated assemblies. The original C01, C02, and C05 client binary also passed all 16 compatibility assertions. The strict MkDocs build passed.

The usability work bounds C01's UTF-8 scan for Auditing and Common clients, adds C02 duration opt-ins for Queueing, Orchestrations, and Console Commands, and uses C04's JSON DOM parser for Blob and Document Storage maintenance bodies. It also adds C05's date-validation opt-out for HTTP clients and C06's single-call acquisition for Common clients. Presentation HTTP compatibility and File Storage's two-path reservation are covered by regression tests. [Common API usability plan](../../plan/pln-feature-common-api-usability-1.md)

The focused usability suites passed 231 Common, 12 Application, 149 Presentation, and 77 Infrastructure tests with zero skips. The affected production projects built successfully. Two pre-extraction client binaries passed all 31 compatibility checks against the updated assemblies without recompilation. The new guide examples compiled and ran, and the strict MkDocs build passed.

Pre-merge review fixed signed values in opted-in Console Commands and added support for obsolete HTTP-date formats in Common's strict preconditions. The follow-up run passed 3,690 Common, 2,258 Presentation, 12 focused Application, and 77 focused Infrastructure tests with zero skips. Both precompiled clients and the strict MkDocs build passed again.

BenchmarkDotNet 0.15.8 ran in Release on .NET 10.0.15 on an Intel Core i5-7600K under Linux Omarchy. Each case ran three warmups and five measured iterations. Median time for 1,048,576 ASCII characters with a 32-byte limit fell from 51.169 µs to 255.402 ns, about 200× faster, with 296 B allocated per call in both runs. For 32 multibyte characters truncated to 32 bytes, the median went from 133.166 ns to 132.932 ns with 160 B allocated. Fitting ASCII and multibyte inputs took 0.764 ns and 0.689 ns longer respectively (8.345 to 9.109 ns and 12.478 to 13.167 ns). Both fitting cases allocated zero bytes. [Benchmark cases and command](../../benchmarks/Common.Benchmarks/README.md)

## Review against merged main

The initial November 23 review compared the scan baseline with `main` at `17edde6ea`. That review added C11 and expanded the scope of several open candidates.

- **C04 and A08 have more storage consumers.** Blob maintenance now uses the same omitted/null-preserve and empty-object-clear conventions as Document maintenance. The Blob dashboard also calls the scalar JSON parser. The Document dashboard still infers GUIDs and timestamps, so both dashboard profiles need coverage. [Properties alignment plan](../../plan/pln-refactor-storage-properties-alignment-1.md)
- **C10 covers the new Dimensions update endpoints.** Both endpoint families reuse the existing ETag transport and whole-item version. C05 still covers Application Caching's conditional requests and ranges. [Storage Dimensions specification](spec-storage-dimensions.md)
- **C11 identified scalar property bag persistence.** Blob and Document Storage had matching clone and persistence helpers around `PropertyBagScalarCodec`. PR #145 extracted those helpers. C04 covers parsing ordinary scalar JSON input.
- **A06 must preserve new storage retry behavior.** Blob uploads with Dimensions skip retry. Document Dimensions updates and FilterModel list queries pass through the Document retry behavior. The existing difference in attempt-count conventions still applies.

The Filtering review commit `c3a89b782` adds a specification. Storage's implemented FilterModel queries share `StorageFilterQuery` and reuse `OpaqueContinuationTokenCodec`. Their field restrictions, limits, tie-breakers, query hashes, and token binding stay with Storage. Keep A04 focused on scalar conversion. [Filtering review](spec-filtering-review.md) and [storage query helper](../../src/Application.Storage/StorageFilterQuery.cs)

AppState's leases own state registration and disposal. LiteDB's leases and file streams manage provider lifetimes, chunking, and progress. Those contracts do not add consumers to C03, C06, or C07. [AppState lease](../../src/Presentation.Web.Client/AppState/AppStateLease.cs) and [LiteDB file streams](../../src/Infrastructure.LiteDB/Storage/Files/LiteDbFileStreams.cs)

## Scope and selection rules

An extraction qualifies when it has a useful contract independent of its current feature, fits an existing Common package, and removes a concrete dependency or duplicated implementation. A single implementation can qualify when clients have a clear use for it. Potential future consumers are identified as proposals, not existing adoption.

Alignment also qualifies when features implement the same concept differently or bypass an appropriate Common API. Record the current difference, proposed shared behavior, and migration impact. A deliberate feature requirement can remain an explicit policy while the underlying implementation is shared. All extraction and alignment work must satisfy the compatibility requirements below.

Keep the work small. Each candidate must be independently implementable without replacing a feature framework or redesigning persistence.

The following rules apply to every implementation:

- Use the existing `BridgingIT.DevKit.Common` namespace and the appropriate Common package. Physical folders can group related files.
- Preserve inward dependencies. Common must not reference Application, Domain, Infrastructure, or Presentation projects.
- Keep ASP.NET Core types in `Common.Extensions.Web`. Keep serialization in `Common.Serialization`, BCL extension methods in `Common.Abstractions`, and stateful helpers in `Common.Utilities`.
- Prefer existing BCL APIs and installed dependencies when they already satisfy the contract. Do not introduce a package for an extraction.
- Separate generic mechanics from feature error codes, limits, authorization, identity rules, and wire formats.
- Preserve current feature behavior during extraction and alignment. Share mechanics through adapters that retain existing policies. Offer different behavior through additive APIs or explicit opt-in profiles, preserving existing defaults.
- Preserve public types and methods at their existing fully qualified names through forwarding facades when introducing Common replacements. Moving a namespace breaks callers; a type forwarder alone cannot preserve a renamed type.
- New public APIs and protected extension points require XML documentation, a fictional usage example, and focused behavior tests. Update the assigned Common guide in the same implementation change, as required by the documentation section below.

## API visibility and XML documentation

Extracted reusable types, constructors, and operations must be `public` so other features and client applications can use them through normal package references. Use `protected` for intended extension points on public base classes. Do not introduce inheritance or virtual members solely to use `protected`.

Avoid `internal`, `protected internal`, and `private protected` for these reusable contracts. They restrict access for clients outside the assembly and are not substitutes for a documented public API. If an assembly-only declaration is technically necessary, record the specific reason with the affected candidate. Implementation details that are not reusable contracts can remain `private`. Existing API accessibility and inheritance contracts remain subject to the compatibility requirements below.

Every new or changed public or protected declaration must have meaningful XML documentation:

- Describe its purpose and behavior in `<summary>`.
- Document parameters and type parameters with `<param>` and `<typeparam>`, plus return values or property values where applicable.
- Explain relevant exceptions, cancellation, ownership, disposal, concurrency, and limits through appropriate XML elements and remarks.
- Include a fictional usage example for each extracted public type or extension-method family. Describe how a client implements a supported protected extension point when one is provided.
- Preserve existing meaningful documentation. Use `<inheritdoc/>` for overrides and interface implementations when it fully describes the contract, adding remarks for behavior specific to the implementation.

Verify that reusable APIs are accessible from a separate consumer assembly without `InternalsVisibleTo` or reflection. Check intended protected hooks through a client subclass where applicable. Visibility and XML documentation are part of each candidate's completion criteria.

## Compatibility requirements

Breaking changes are prohibited in this work. Existing clients must continue to compile without source changes, and previously compiled clients must continue to run with the updated packages. Consumers must not need to change imports, call sites, configuration, or persisted data to adopt the update.

### Preserve public contracts

- Retain public and protected types in their existing namespaces and defining assemblies. Keep old packages and entry points available. A Common implementation is additive; existing feature types delegate to it.
- Preserve constructors, methods, properties, events, return types, parameter types, parameter names, parameter order, optional defaults, and `ref`, `in`, and `out` modifiers. Parameter names matter for named arguments. Adding an optional parameter to an existing method still changes its binary signature and is prohibited.
- Preserve generic arity and constraints, accessibility, inheritance, virtual and abstract contracts, interface requirements, record constructors and deconstruction, and enum numeric values. Do not require existing client implementations or subclasses to implement new members.
- Retain existing overloads and extension methods. New APIs must not introduce ambiguous calls, change existing overload resolution, or create type-name ambiguity when Common and feature namespaces are imported together. Use distinct names when needed.
- Do not add obsolescence warnings to retained APIs in this work. Existing clients may treat warnings as errors. Do not tighten nullable annotations or add required-member constraints that make unchanged client source fail its build.

### Preserve behavior and formats

- Keep existing defaults, accepted inputs, failure results, exception behavior, culture handling, retry counts, byte-count meanings, stream ownership, and cancellation behavior at existing entry points.
- Preserve configuration keys and meanings, serialized shapes, export bytes, hashes, continuation tokens, ETags, and stored representations. Sharing code must not require rewriting existing state.
- Opt-in behavior must be selected explicitly through a new API, overload, or profile that passes the source-compatibility checks. Keep its default disabled for existing callers. Do not modify an existing signature to add an opt-in switch.
- Keep a difference behind a compatibility adapter when removing it would change an existing contract. Record the proposed common behavior for new consumers without silently applying it to old ones.

### Required compatibility evidence

For each implementation slice, compare the affected public API with the baseline and verify that no existing contract was removed or changed. Compile representative unchanged clients against the updated packages, including named arguments, optional arguments, extension methods, and simultaneous Common and feature imports. When public implementation ownership changes, run a representative client binary compiled against the baseline with the new packages without rebuilding that client.

Retain behavior fixtures for the existing entry points and add separate fixtures for any opt-in behavior. Check exact bytes where exports, hashes, or tokens depend on them. Passing new utility tests alone does not establish compatibility. Release notes and a migration guide do not make a breaking change acceptable under this specification.

## Recommended implementation order

Each batch lists the participating features and their roles. Source owners supply the implementation being extracted; existing consumers need compatible adapters or call-site updates. Opt-in alignment targets are planned participants, not evidence that they already use the extracted helper. Package names identify the code in scope without requiring changes to every provider of a feature.

### Batch 1. Text, duration, and hash helpers

C01 UTF-8 truncation, C02 friendly duration parsing, and C03 incremental hash output are implemented.

Participating features:

- Application Auditing uses C01 through `AuditCollector.Text`. Its Entity Framework search projection uses the C03 hash stream in `Infrastructure.EntityFramework`.
- Profiling's Console Commands use C02 through `ProfilingDurationParser` in `Presentation.Web`.
- Application Caching uses the C03 hash stream in its in-memory, Entity Framework, and filesystem providers.
- Queueing, Orchestrations, and the general Console Commands binder use C02 through the A01 opt-in APIs in `Application.Queueing`, `Application.Orchestrations`, and `Presentation`.

Common destinations: `Common.Abstractions` and `Common.Utilities`. Each candidate remains an independent small change. Blob Storage's hashing read stream is an optional later investigation, not part of C03's write-only extraction.

### Batch 2. Scalar metadata and HTTP conditions

C04 scalar JSON properties, C05 HTTP representation conditions, and C11 scalar property bag persistence are implemented.

Participating features:

- Shared HTTP endpoint helpers in `Presentation.Web` expose the retained C04 and C05 facades.
- Blob Storage and Document Storage are existing C04 consumers through their maintenance endpoints and dashboards in `Presentation.Web.Storage`. Both dashboards participate in A08 because they handle date-like and GUID-like strings differently.
- Application Caching's HTTP endpoints in `Presentation.Web.Caching` consume C05 for conditional requests, entity-tag comparison, and range selection.
- Blob Storage and Document Storage in `Application.Storage` use C11 through their existing public facades. Their in-memory and cache paths and existing callers in Entity Framework, File System, Azure Storage, and Azure Cosmos can use the same facades.

Common destinations are `Common.Serialization` and `Common.Extensions.Web`. A08 metadata-inference alignment can build on C04 when its opt-in behavior is ready.

### Batch 3. Locks, stream counters, CSV, path globs, and ETags

C06 keyed asynchronous locks, C09 anchored path globs, and C10 opaque HTTP version tokens are implemented. C07 counting streams and C08 CSV cells are marked **Won't do**.

Participating features:

- File Storage uses C06 through its local provider in `Infrastructure.FileSystem`.
- Request Profiling uses C09 through its matcher facade in `Presentation.Web`.
- Blob Storage and Document Storage endpoints in `Presentation.Web.Storage`, including both Dimensions update endpoints, use C10 through the `Presentation.Web` facade.

Common destinations include `Common.Abstractions`, `Common.Utilities`, `Common.Serialization`, and `Common.Extensions.Web`. A09 tracks Request Profiling path matching. Application Caching owns its representation-bound ETag format.

### Participation in additional alignment work

A04 through A07 can proceed as independent alignment slices rather than expanding the extraction batches:

- A04 scalar conversion involves Console Commands in `Presentation`, the Entities dashboard in `Presentation.Web`, DataPorter in `Application.DataPorter`, Domain Filtering in `Domain`, and existing conversion helpers in `Common.Abstractions`.
- A05 command grammar involves Console Commands and history in `Presentation`, CLI forwarding in `Presentation.Cli`, and web console IPC in `Presentation.Web`, with a proposed shared grammar in Common.
- A06 retry conventions involve Application Commands, Application Queries, Messaging, Blob Storage, and Document Storage in `Application.Commands`, `Application.Queries`, `Application.Messaging`, and `Application.Storage`. Common Requester retries, Result retries, and job retry policy contracts are comparison points for reuse, not a requirement to change all their consumers.
- A07 daylight-saving-time resolution involves Jobs in `Application.Jobs` and the existing date/time helpers in `Common.Abstractions`.

Implement the related A-series alignment with its extraction when the target behavior and compatible adoption path are settled. Prioritize A06 retry-count semantics and A05 token round trips for characterization, since their differences can affect execution rather than just API consistency. Existing entry points retain their behavior even when a new shared contract differs.

Priority reflects reuse and coupling reduction. It does not imply a defect in the current feature. Effort labels describe relative extraction scope, not delivery estimates.

## C01. UTF-8 byte-limited text truncation

Status: implemented on this branch.

As a client developer, I want to truncate text to a UTF-8 byte limit so that labels and metadata fit a byte budget without cutting a Unicode scalar.

`Common.Abstractions` exposes `string.TruncateUtf8(int maximumBytes)`. It preserves a fitting string as supplied and replaces retained unpaired surrogates with U+FFFD when truncating. [Implementation](../../src/Common.Abstractions/Extensions/StringExtensions.cs), [Common Extensions guide](../common-extensions.md#truncate-text-to-a-utf-8-byte-limit), and [Common tests](../../tests/Common.UnitTests/Abstractions/Extensions/StringExtensionsTests.cs)

`AuditCollector.Text` treats negative limits as zero when calling Common, preserving its original results. The CEF formatter continues to budget its escaped output as it writes. [Auditing facade](../../src/Application.Auditing/Recording/AuditCollector.cs), [facade tests](../../tests/Application.UnitTests/Auditing/AuditCollectorTextTests.cs), and [CEF formatter](../../src/Application.Auditing/Formatters/CefAuditMappingV1.cs)

Acceptance criteria:

1. Given well-formed text within the limit, return the original text. Preserve null and empty values.
2. Given longer text, return the longest prefix of complete Unicode scalars whose UTF-8 encoding fits the limit. Do not append a marker.
3. A zero limit returns an empty string for non-null text. A negative limit throws `ArgumentOutOfRangeException`.
4. ASCII, multibyte letters, supplementary characters, and limits smaller than one scalar have focused tests. Character-count shortening remains unchanged.

## C02. Friendly duration parsing

Status: implemented on this branch.

As a client developer, I want to parse durations such as `500ms` and `1.5m` so that configuration and commands can share one duration grammar.

`Common.Abstractions` exposes `string.TryParseDuration(out TimeSpan duration)`. `ProfilingDurationParser.TryParse` forwards to it with its original parameters and behavior. [Implementation](../../src/Common.Abstractions/Extensions/TimeSpanExtensions.cs), [Common Extensions guide](../common-extensions.md#parse-durations), [Common tests](../../tests/Common.UnitTests/Abstractions/Extensions/TimeSpanExtensionsTests.cs), [Profiling facade](../../src/Presentation.Web/Profiling/ConsoleCommands/ProfilingDurationParser.cs), and [Profiling tests](../../tests/Presentation.UnitTests/ConsoleCommands/ProfilingConsoleCommandTests.cs)

Acceptance criteria:

1. Standard invariant `TimeSpan` text and case-insensitive `ms`, `s`, `m`, and `h` suffixes preserve the existing parser's results, including fractional and signed values.
2. Null, blank text, unsupported suffixes, non-finite numbers, and values outside `TimeSpan` range return false without throwing. The output is zero on failure.
3. Parsing is independent of the current culture. Feature callers retain responsibility for requiring a positive interval or duration.
4. The existing public profiling parser forwards to the Common implementation, and its command tests continue to pass.

A01 tracks opt-in adoption for Queueing, Orchestrations, and the general command binder. `TryParseTime` continues to handle clock text.

## C03. Incremental hash output stream

Status: implemented. Merged in `f7bf71de1` through PR #146.

As a feature or client developer, I want to serialize directly into an incremental hash so that computing a digest does not require materializing the serialized payload.

The public `IncrementalHashWriteStream` in `Common.Utilities` appends writes to a caller-owned `IncrementalHash`. The caller finalizes the digest. [Implementation](../../src/Common.Utilities/Streams/IncrementalHashWriteStream.cs), [Common Utilities guide](../common-utilities.md#hash-output-as-it-is-written), and [stream tests](../../tests/Common.UnitTests/Utilities/IncrementalHashWriteStreamTests.cs)

Four consumers use the shared stream:

- [Entity Framework audit search projection](../../src/Infrastructure.EntityFramework/Auditing/Queries/AuditSearchProjection.cs)
- [In-memory cache provider](../../src/Application.Caching/Providers/InMemory/InMemoryCacheStoreProvider.cs)
- [Entity Framework cache provider](../../src/Infrastructure.EntityFramework/Caching/EntityFrameworkCacheStoreProvider.cs)
- [Filesystem cache provider](../../src/Infrastructure.FileSystem/Caching/FileSystemCacheStoreProvider.cs)

Acceptance criteria:

1. Array, span, and asynchronous writes append exactly the supplied bytes to the caller's `IncrementalHash`. No complete payload buffer is allocated.
2. A canceled constructor token or write token prevents the affected write from appending bytes.
3. The stream is write-only and non-seekable. Flush does not finalize the digest.
4. Disposing the stream does not dispose or reset the caller's hash. The caller controls digest finalization and algorithm selection.
5. Replacing the four adapters preserves their exact existing serialized bytes and digests. Stored fingerprints and query/token compatibility remain unchanged.

Blob verified downloads still own EOF finalization and read integrity checks through their read stream. [Blob verified downloads](../../src/Application.Storage/Blobs/Integrity/BlobVerifiedDownloadExtensions.cs)

## C04. Scalar JSON property parsing

Status: implemented. Merged in `f7bf71de1` through PR #146.

As a client developer, I want to parse a JSON object into portable scalar properties so that non-HTTP consumers can use the same numeric and validation rules as storage endpoints.

`Common.Serialization` exposes `ScalarPropertyBagJson.Parse(string json, bool preserveMissing = false)` returning `Result<PropertyBag>`. The parser preserves scalar mapping, case-insensitive keys, and missing-value behavior. It keeps the inherited `InvalidOperationException` when decoding an unpaired surrogate escape in a property name or string value. [Implementation](../../src/Common.Serialization/ScalarPropertyBagJson.cs), [Common Serialization guide](../common-serialization.md#plain-scalar-json), and [parser tests](../../tests/Common.UnitTests/Serialization/ScalarPropertyBagJsonTests.cs)

- `HttpMetadata.ParseProperties` delegates to Common. `HttpMetadata.ResolveExpiration` stays in Presentation. [HTTP facade](../../src/Presentation.Web/Http/HttpMetadata.cs) and [facade tests](../../tests/Presentation.UnitTests/Web/Http/HttpMetadataTests.cs)
- Blob and Document maintenance now call the helper with `preserveMissing: true` on Properties mutations. The Blob dashboard uses the helper when a Properties form value is supplied. [Blob maintenance endpoints](../../src/Presentation.Web.Storage/Blobs/BlobStorageMaintenanceEndpoints.cs), [Document maintenance endpoints](../../src/Presentation.Web.Storage/Documents/DocumentStorageMaintenanceEndpoints.cs), and [Blob dashboard endpoints](../../src/Presentation.Web.Storage/Blobs/Dashboard/DashboardEndpoints.cs)
- [`Document Storage dashboard property parsing`](../../src/Presentation.Web.Storage/Documents/Dashboard/DashboardEndpoints.cs) reuses the numeric rules, then deliberately infers dates and GUIDs from strings.

Acceptance criteria:

1. JSON object scalars preserve the current CLR mapping for null, strings, booleans, integers, decimals, and doubles. Date-like and GUID-like strings remain strings.
2. Omitted input and JSON null produce a fresh empty bag or null according to `preserveMissing`. An explicit empty object always means empty replacement.
3. Nested objects, arrays, blank property names, unsupported numeric overflow, and nonzero values that underflow to zero fail with a validation result.
4. Parsing remains culture-independent and does not share mutable bags across calls.
5. `HttpMetadata` delegates parsing, while the dashboard's date/GUID inference remains an explicit feature adapter.
6. Blob and Document maintenance continue to preserve properties for omitted or null input and clear properties for an explicit empty object. Preserve each dashboard's current form behavior and string inference. [Blob endpoint regression tests](../../tests/Presentation.UnitTests/Web/Storage/BlobStorageEndpointsTests.cs)

Boundary: do not change [`PropertyBagJsonConverter`](../../src/Common.Serialization/Converters/PropertyBagJsonConverter.cs), which accepts nested data, or [`PropertyBagScalarCodec`](../../src/Common.Serialization/PropertyBagScalarCodec.cs), which encodes typed provider strings. Their existing contracts serve different purposes.

## C05. HTTP representation conditions and byte ranges

Status: implemented on this branch.

As a client application developer, I want to evaluate conditional HTTP requests and a single byte range without depending on the full Presentation.Web package.

`Common.Extensions.Web` exposes `HttpRepresentationHeaders`, `HttpRepresentationPreconditionOutcome`, `HttpRepresentationRangeKind`, and `HttpRepresentationRangeSelection`. `Presentation.Web` retains `HttpRepresentationConditions` and its original enum and record types. The facade maps Common outcomes to those types. The new names let callers import both namespaces without ambiguity. [Implementation](../../src/Common.Extensions.Web/HttpRepresentationHeaders.cs), [Common Extensions guide](../common-extensions.md#evaluate-representation-conditions-and-byte-ranges), [Common tests](../../tests/Common.UnitTests/Extensions/HttpRepresentationHeadersTests.cs), and [Presentation facade](../../src/Presentation.Web/Http/HttpRepresentationConditions.cs)

Application Caching uses the Presentation facade for conditions and entity-tag comparison. The facade selects `HttpRepresentationHeaderMode.LegacyPresentation` to preserve its existing results. Common defaults to `Strict`. It accepts RFC 1123, RFC 850, and asctime dates, keeps commas inside quoted tags, requires a single exact `If-Range` validator, and accepts any casing of the `bytes` unit. [Cache conditions](../../src/Presentation.Web.Caching/Http/CacheHttpPreconditions.cs), [cache entity tags](../../src/Presentation.Web.Caching/Http/CacheEntityTag.cs), and [facade tests](../../tests/Presentation.UnitTests/Web/Http/HttpRepresentationConditionsTests.cs)

Acceptance criteria:

1. Existing precedence among `If-Match`, `If-Unmodified-Since`, `If-None-Match`, and `If-Modified-Since` remains unchanged.
2. Reads distinguish proceed, not modified, and failed preconditions. Existing strong and weak comparison behavior is preserved.
3. Range selection preserves full, partial, unsupported, and unsatisfiable outcomes, including suffix ranges, empty content, and `If-Range` mismatch behavior.
4. Common adds no dependency on cache, storage, or Presentation projects. Existing public Presentation APIs remain facades that map their existing outcome types.
5. Existing tests pass against the adapters, and equivalent Common tests exercise the extracted implementation.

The caller authorizes access and applies mutation preconditions atomically. Common supports one byte range and the documented validator comparisons.

## C06. Keyed asynchronous lock leases

Status: implemented on this branch.

As a feature or client developer, I want to serialize asynchronous work per key so that unrelated keys can run concurrently without an unbounded permanent semaphore dictionary.

`Common.Utilities` exposes `AsyncKeyedLock<TKey>` with an optional `IEqualityComparer<TKey>`. `Rent(TKey key)` reserves a single-use lease and `TryAcquireAsync` waits for the lock. Rent-before-acquire lets File Storage reserve both paths before waiting and retain its existing lock order, timeout results, and platform path comparer. [Implementation](../../src/Common.Utilities/Threading/AsyncKeyedLock.cs), [Common Utilities guide](../common-utilities.md#keyed-asynchronous-locks), and [local provider](../../src/Infrastructure.FileSystem/Files/LocalFileStorageProvider.cs)

Acceptance criteria:

1. Equal keys share exclusion according to the supplied comparer; different keys can enter concurrently.
2. A timeout returns false. Cancellation and failed waits propagate their exceptions. Each returns the lease reservation automatically.
3. The final lease retires the entry. All holders and waiters for a key use the same semaphore.
4. Lease disposal is idempotent. Disposal during a pending wait defers cleanup until the wait settles. If that wait acquires the lock, it releases the lock and throws `ObjectDisposedException`.
5. Disposing the pool rejects new rentals. Already rented leases can acquire and retire normally.

Equal keys share exclusion even when rented by the holder. A lease rejects concurrent or repeated acquisition attempts. This pool coordinates operations within the current process. [Common tests](../../tests/Common.UnitTests/Utilities/AsyncKeyedLockTests.cs), [File Storage compatibility tests](../../tests/Infrastructure.UnitTests/FileSystem/LocalFileStorageLockCompatibilityTests.cs), and [provider lock contention tests](../../tests/Infrastructure.UnitTests/FileSystem/FileSystemReviewRegressionTests.cs)

## C07. Counting stream decorators

**Status: Won't do.** See A03 for the related alignment decision.

As a client developer, I want to observe bytes passing through a stream so that progress and diagnostics work without seek support.

Evidence:

- [`DataPorter.WriteStreamWrapper`](../../src/Application.DataPorter/WriteStreamWrapper.cs) is generic stream forwarding with a byte counter and `leaveOpen`. It is used by DataPorter services and multiple format providers.
- [`ProfilingResponseBodyFeature.CountingStream`](../../src/Presentation.Web/Profiling/Requests/ProfilingResponseBodyFeature.cs) counts response writes and reports faults through a detachable callback.
- [`ProfilingRequestBodyObserver.ReadStream`](../../src/Presentation.Web/Profiling/Requests/ProfilingRequestBodyObserver.cs) observes request reads.
- [`OpenWriteFileStream`](../../src/Application.Storage/Files/OpenWriteFileStream.cs) combines counting with file progress and finalization callbacks.

Proposed API and destination: small counting read/write decorators in `Common.Utilities/Streams`. Keep request lifecycle callbacks, `FileProgress`, and storage completion in their feature adapters.

Acceptance criteria:

1. The shared counter means bytes successfully transferred through this wrapper, independently of underlying length and position.
2. Array, span, memory, asynchronous, and single-byte operations count once and preserve cancellation and exceptions.
3. Ownership is explicit through `leaveOpen`; disposing follows the documented ownership contract.
4. Feature adapters preserve current observable counts, callback behavior, and fault handling. In particular, DataPorter's current `BytesWritten` returns underlying length when seekable, which differs from transferred bytes.
5. Non-seekable outputs retain coverage through [`DataPorterServiceExportTests`](../../tests/Application.UnitTests/DataPorter/DataPorterServiceExportTests.cs), including asynchronous export.

The proposal left public decorator types and callback ownership undecided. [`LimitedWriteStream`](../../src/Common.Utilities/Streams/LimitedWriteStream.cs) already counts capped writes with sticky failure state.

## C08. CSV cell escaping and formula neutralization

**Status: Won't do.** See A02 for the related alignment decision.

As a client developer, I want reusable CSV cell formatting with explicit spreadsheet formula handling so that exports share quoting rules without changing their published formats.

Evidence:

- [`EntitiesDashboardExportService.EscapeCsv`](../../src/Presentation.Web/Entities/Dashboard/EntitiesDashboardExportService.cs) quotes delimiters and line breaks and neutralizes configured leading characters.
- [`CsvAuditExportFormatter.RecordAsync`](../../src/Application.Auditing/Export/CsvAuditExportFormatter.cs) implements quoting and formula handling in a local `Cell` function. It scans past whitespace, control characters, and BOM before inspecting formula prefixes and records transformation counts.
- [`EntitiesDashboardExportServiceTests`](../../tests/Presentation.UnitTests/Web/Entities/EntitiesDashboardExportServiceTests.cs) and [`AuditExportTests`](../../tests/Application.UnitTests/Auditing/AuditExportTests.cs) cover consumer behavior.

Proposed destination: `Common.Serialization`, with shared cell quoting and separately selected formula policies. Evaluate the existing CsvHelper dependency before adding public formatting types.

Acceptance criteria:

1. Delimiters, quotes, CR, and LF retain the selected format's quoting and quote-doubling behavior.
2. Numeric cells can retain a leading sign. Untrusted text uses an explicit neutralization policy, and the formatter reports whether it changed the cell.
3. Null and empty-string distinctions remain compatible with each consumer's current output.
4. Auditing retains its whitespace/BOM scan, output byte budgets, fixed columns, transformation counts, and incremental JSON-cell writing.
5. Existing dashboard and audit exports retain their characterized bytes under their compatibility profiles.

The proposal left the cell API and profile names undecided. Auditing's [`AuditExportBuffer`](../../src/Application.Auditing/Export/AuditExportBuffer.cs) supports bounded streaming for JSON cells.

## C09. Anchored path glob matching

Status: implemented on this branch.

As a client developer, I want bounded path glob matching so that path-based exclusions can be reused without pulling in Profiling.

`Common.Abstractions` exposes `PathGlobMatcher` with the 128-pattern and 256-code-unit limits. `RequestProfilingPathMatcher` forwards matching to Common and preserves its existing handling of unrooted inputs and validation errors. Profiling still owns prefix removal and canonical key hashing. [Implementation](../../src/Common.Abstractions/Matching/PathGlobMatcher.cs), [Common Extensions guide](../common-extensions.md#match-anchored-path-globs), and [Presentation facade](../../src/Presentation.Web/Profiling/Requests/RequestProfilingPathMatcher.cs)

Acceptance criteria:

1. Matching is anchored to the complete slash-delimited path. A single star cannot consume a slash; a complete double-star segment can consume zero or more segments.
2. Trailing-slash behavior, case handling, supported syntax, and setup-time validation retain the existing contract.
3. The implementation remains bounded and avoids regex backtracking. The existing Common glob matcher remains the segment implementation where compatible.
4. [`RequestProfilingSamplingTests`](../../tests/Presentation.UnitTests/Web/Profiling/RequestProfilingSamplingTests.cs) and [`RequestProfilingPatternCompatibilityTests`](../../tests/Presentation.UnitTests/Web/Profiling/RequestProfilingPatternCompatibilityTests.cs) retain their results, including supplementary letters and exact unpaired-surrogate literals.

The Common API requires a leading slash on nonempty inputs. It uses invariant casing per UTF-16 code unit and delegates ASCII segments to `GlobPatternMatcher`. It accepts exact unpaired-surrogate literals. [Common tests](../../tests/Common.UnitTests/Abstractions/PathGlobMatcherTests.cs)

## C10. Opaque version tokens in HTTP ETags

Status: implemented. Merged in `f7bf71de1` through PR #146.

As a client application developer, I want to transport an opaque version token as one HTTP ETag so that custom endpoints can use DevKit's existing storage token conventions.

`Common.Extensions.Web` exposes `OpaqueVersionETag.ReadCondition` and `Write`. The Presentation `HttpETag` facade delegates both methods. The guide describes supported provider versions and the quoted `bdk.` transport. [Implementation](../../src/Common.Extensions.Web/OpaqueVersionETag.cs), [HTTP facade](../../src/Presentation.Web/Http/HttpETag.cs), [Common Extensions guide](../common-extensions.md#http-helpers), and [ETag tests](../../tests/Common.UnitTests/Extensions/OpaqueVersionETagTests.cs)

The new Dimensions mutations call the same helper to read conditions and write the committed whole-item version. [Blob Dimensions endpoints](../../src/Presentation.Web.Storage/Blobs/BlobStorageMaintenanceEndpoints.Dimensions.cs), [Document Dimensions endpoints](../../src/Presentation.Web.Storage/Documents/DocumentStorageMaintenanceEndpoints.Dimensions.cs), and [endpoint tests](../../tests/Presentation.UnitTests/Web/Storage/StorageDimensionsEndpointsTests.cs)

Acceptance criteria:

1. Encoding and decoding round-trip supported provider versions exactly, including their original quoting. The Common guide documents the input limits.
2. Conflicting header/body tokens, wildcard conditions, lists, control characters, and malformed encoded input retain the current rejection behavior.
3. Existing accepted raw tokens and omitted conditions remain compatible. The migration introduces no token-format change.
4. The shared adapter has no Storage or Caching dependency, and existing endpoint regression tests pass.
5. Dimensions updates retain the current header/body conflict handling and return the committed whole-item ETag. Storage and its providers continue to enforce that condition atomically.

`Common.Extensions.Web` references `Common.Serialization` for `Base64UrlHelper` and gets Common Results through that project. [Web package references](../../src/Common.Extensions.Web/Common.Extensions.Web.csproj)

Application Caching owns the encoding that binds a cache version to a representation. Its entity-tag comparison uses C05 through the Presentation facade. [Cache entity tags](../../src/Presentation.Web.Caching/Http/CacheEntityTag.cs)

## C11. Scalar property bag persistence

Status: implemented. Merged into `main` on 2026-11-23 in `978f5861d`. [PR #145](https://bridgingitgruppe.ghe.com/bit-intern/bitdevkit/pull/145)

As a client developer, I want to clone and persist a scalar `PropertyBag` without depending on Storage so that values keep their CLR types and mutable byte arrays remain independent.

`Common.Serialization` exposes `PropertyBagSerialization.Clone`, `Serialize`, and `Deserialize` in `BridgingIT.DevKit.Common`. It uses `PropertyBagScalarCodec` for the existing encoded string format. The Common guide documents the persistence format, scalar types, array ownership, and null and empty behavior. [Implementation](../../src/Common.Serialization/PropertyBagSerialization.cs) and [Common Serialization guide](../common-serialization.md#propertybagserialization)

`BlobPropertyValues` and `DocumentPropertyValues` delegate those three methods through their existing signatures. Each facade still validates values with `PropertyBagScalarCodec.Encode` and maps failures to its own feature error. [Blob property helpers](../../src/Application.Storage/Blobs/Properties/BlobPropertyValues.cs) and [Document property helpers](../../src/Application.Storage/Documents/DocumentPropertyValues.cs)

Acceptance criteria:

1. Cloning preserves null versus empty bags, case-insensitive keys, and scalar runtime types. Cloned byte arrays do not share storage with the source.
2. Serialization preserves the existing JSON dictionary of codec-encoded strings byte for byte. Both null and empty bags serialize to null. Reading null or empty text returns a new empty bag. Existing persisted JSON and legacy unprefixed scalar strings are still readable.
3. Existing facade signatures, validation errors, and malformed-input behavior remain compatible. Blob and Document still distinguish omitted mutation properties from an explicit empty bag, and private transform metadata stays separate.
4. Common tests exercise the public API independently from Storage. Storage contract tests cover facade round trips and snapshots. [Common tests](../../tests/Common.UnitTests/Serialization/PropertyBagSerializationTests.cs), [Blob contract tests](../../tests/Application.UnitTests/Storage/Blobs/BlobStoreProviderContractTests.cs), and [Document contract tests](../../tests/Application.UnitTests/Storage/Documents/DocumentPropertiesContractTests.cs)

PR #145 reports 63 Common serialization tests, 626 Application Storage tests, and 47 EF Storage contract tests passed. A caller compiled against the previous `main` produced identical facade metadata, persisted bytes, and error results using the new assemblies. The strict MkDocs build passed. [Verification in PR #145](https://bridgingitgruppe.ghe.com/bit-intern/bitdevkit/pull/145)

The delivered scope covers the two matching facades. Any later adoption in native provider mappings requires separate compatibility checks against provider fixtures. [Dimensions ADR](../adr/0044-independent-storage-dimensions.md)

## Behavioral alignment backlog

These items track differences that can be aligned by sharing Common APIs. Introduce different semantics through explicit opt-in APIs and preserve existing contracts.

### A01. Share one invariant duration grammar

Status: implemented. Related extraction: C02.

Queueing and Orchestrations expose `StartupDelayInvariant(string)` for invariant `TimeSpan` text and friendly values such as `500ms`. Each method passes a parsed value to its existing typed setter and throws `ArgumentException` before changing the setting if parsing fails. Existing `StartupDelay(string)` calls keep their culture-dependent parser and retain the previous value on invalid input. [Queueing startup delay](../features-queueing.md#basic-usage), [Orchestrations execution settings](../features-orchestrations.md#background-execution-settings)

Console Commands use the same grammar for `TimeSpan` and `TimeSpan?` options and arguments marked with `[ConsoleCommandDuration]`, including signed values. Registered option names and aliases take precedence when a separate token could be either an option or a signed value. Unmarked properties continue to use standard invariant `TimeSpan` parsing. Both grammars use the binder's existing validation errors. Profiling continues to use the C02 facade. [Console command options and arguments](../features-presentation-console-commands.md#options-and-arguments), [Common duration parsing](../common-extensions.md#parse-durations)

Builder and binder tests verify matching values under `en-US` and `nl-NL`, existing defaults, and invalid-input behavior. The existing Profiling command tests cover the facade.

### A02. Share CSV quoting and formula policies

**Status: Won't do.** Related extraction C08.

Current difference: the Entities dashboard inspects the first character and includes tab and CR among its formula prefixes. Auditing skips whitespace, controls, and BOM, then checks `=`, `+`, `-`, and `@`. Empty text is quoted by Auditing and unquoted by the dashboard. Both treat numeric cells separately.

Proposed alignment: use one quoting implementation with explicit policies. Offer a shared conservative formula policy that covers formula-leading text after whitespace or BOM, plus the dashboard's existing tab/CR handling. Preserve a distinction between untrusted text and typed numeric cells. Existing dashboard and audit exports retain their current default policies and bytes. Stricter handling is an explicit opt-in profile.

Verification and migration: test direct and whitespace-prefixed formulas, BOM, tab/CR, signed numbers, delimiters, quotes, and multiline values against both consumers. Preserve both existing export formats, including the audit `csv-v1` byte contract and each consumer's empty-string quoting. Test the shared opt-in profile separately.

### A03. Separate transferred bytes from stream size

**Status: Won't do.** Related extraction C07.

Current difference: DataPorter reports underlying stream length when seekable and counts writes otherwise. File Storage progress uses stream position when seekable. HTTP profiling counts bytes passing through the wrapper. Pre-populated streams, nonzero starting positions, and overwritten bytes produce different meanings for the same progress concept.

Proposed alignment: new shared counters always measure completed transfers. If a feature also needs artifact length or output position, expose and name that separately through additive APIs. Existing public progress fields retain their current meanings. Keep final file count and completion state outside the byte counter.

Verification and migration: overwrite an existing seekable stream, append at a nonzero position, and write to a non-seekable stream. Assert transferred count separately from final length and position. Verify that existing progress properties, constructors, and callbacks remain unchanged.

### A04. Share scalar parsing mechanics and explicit policies

Priority: medium. Related evidence appears under scalar conversion below.

Current difference: the command binder accepts boolean `1` and `0`; the Entities dashboard accepts boolean words only. The command binder accepts `Enum.TryParse` results without the dashboard's defined-value check. DataPorter's enum converter can accept numeric values and returns the default enum on invalid text. Date parsing also differs: the command binder assumes local time, the dashboard uses round-trip DateTime handling, and Common conversion defaults assume/adjust to UTC.

Proposed alignment: begin with one narrow type at a time. New shared parsing APIs use invariant conversion mechanics, explicit boolean aliases, explicit enum policies for numeric values and defined/flags combinations, and an observable failure result. Keep required-field validation and Domain enumeration lookup at the boundary. Choose date assumptions explicitly for the new API. Existing consumers retain their current aliases, default-on-failure behavior, enum validation, and time-zone assumptions through adapters unless an application selects a new opt-in policy.

Verification and migration: exercise the same boolean, enum, null/empty, overflow, and timestamp inputs through each selected consumer. Characterize [`Common To/TryTo`](../../src/Common.Abstractions/Extensions/To.cs) and [`ToType`](../../src/Common.Abstractions/Extensions/ToType.cs) before reusing them; preserve their existing behavior. Any added capability needs a distinct compatible entry point or explicit opt-in policy. Verify legacy and opt-in expectations separately.

### A05. Make command token parsing and formatting round-trip

Priority: high. Related evidence appears under console tokenization below.

Current difference: quoting and escape handling disagree between the command executor and CLI. Empty quoted arguments are lost by the splitter, and literal embedded quotes cannot reliably round-trip through the existing formatters.

Proposed alignment: define a new explicit DevKit command grammar and share its tokenizer and formatter in Common. Specify preservation of empty arguments, whitespace, quotes, and backslashes, plus a deterministic error for unmatched quotes. Keep already tokenized argument arrays intact across forwarding boundaries. Existing `SplitArgs`, history replay, CLI forwarding, and web IPC retain their current grammar by default; adopt the new grammar through a separate API or explicit opt-in setting.

Verification and migration: `Parse(Format(tokens))` reproduces supported token arrays exactly for the new grammar. Test existing and opt-in terminal commands, history replay, CLI forwarding, and web IPC separately. Existing inputs retain their current interpretation and unmatched-quote handling. This requires a grammar decision and compatibility fixtures before extraction; shell-specific quoting remains outside the contract.

### A06. Align retry counts, delay calculation, and clocks

Priority: high. No new retry framework is proposed.

Current difference:

- [`RetryCommandBehavior`](../../src/Application.Commands/Behaviors/RetryCommandBehavior.cs), [`RetryQueryBehavior`](../../src/Application.Queries/Behaviors/RetryQueryBehavior.cs), and [`RetryMessageHandlerBehavior`](../../src/Application.Messaging/Behaviors/RetryMessageHandlerBehavior.cs) pass `Attempts` directly to `MaxRetryAttempts`.
- [`RetryDocumentStoreClientBehavior`](../../src/Application.Storage/Documents/Behaviors/RetryDocumentStoreClientBehavior.cs) and [`RetryBlobStoreClientBehavior`](../../src/Application.Storage/Blobs/Behaviors/RetryBlobStoreClientBehavior.cs) count the initial execution within `Attempts`. The source therefore expresses different count conventions. Confirm the resulting execution counts with focused tests before migration.
- Blob uploads carrying Dimensions bypass retry. Document Dimensions updates and FilterModel list queries use the Document retry loop with its existing transient-error and indeterminate-write rules.
- Blob retries delay through an injected `TimeProvider`; Document retries use `Task.Delay` without a supplied clock. Their default delays and exponential settings also differ.
- Common already has [`RetryPolicy`](../../src/Common.Results/Extensions/ResultRetryPolicy.cs), [`Requester retry behavior`](../../src/Common.Utilities/Requester/Behaviors/RetryPipelineBehavior.cs), and [`JobRetryPolicy`](../../src/Common.Abstractions/Jobs/Models/JobRetryPolicy.cs). Their names must not be treated as proof of identical semantics.

Proposed alignment: in new shared contracts, define `MaxAttempts` as total executions including the first and `MaxRetries` as additional executions. Preserve each existing `Attempts` property's name, default, and execution-count meaning. Translate between those meanings inside adapters without changing observable counts. Share compatible delay computation and cancellation/clock support through existing Common or Microsoft.Extensions.Resilience contracts. Keep retry eligibility, idempotency, stream rewind, and indeterminate-write handling in features. Preserve existing default delays and exponential settings.

Verification and migration: assert exact existing invocation counts for zero, one, and three configured attempts, cancellation during backoff, and constant/exponential delays with a controlled clock. Test seekable upload replay and indeterminate writes in their own features. Do not reinterpret existing `Attempts` properties. Alternative count semantics require an explicit new API; release notes alone do not permit changing old behavior.

Keep the merged Dimensions retry cases in the regression set. Cover the Document FilterModel overload when refactoring its retry path. [Blob Dimensions retry tests](../../tests/Application.UnitTests/Storage/Blobs/BlobDimensionsRetryTests.cs) and [Document Dimensions retry tests](../../tests/Application.UnitTests/Storage/Documents/DocumentDimensionsRetryTests.cs)

### A07. Reuse Common daylight-saving-time resolution

Priority: medium. Extraction is unnecessary if compatibility tests confirm equivalence.

Current difference: [`DefaultJobCalendarEngine.ConvertLocalToUtc`](../../src/Application.Jobs/Triggers/DefaultJobCalendarEngine.cs) implements minute-by-minute advancement through invalid local times and selects the earliest UTC instant for an ambiguous local time. [`DateTimeExtensions.ToDateTimeOffset`](../../src/Common.Abstractions/Extensions/DateTimeExtensions.cs) already provides explicit invalid-time and ambiguous-offset policies.

Proposed alignment: construct the unspecified local DateTime and delegate to the Common helper with `InvalidTimePolicy.MoveForward` and the offset policy that selects the greater offset, then convert to UTC. In the current Common implementation that is `AmbiguousTimePolicy.LaterOffset`. Document the selected UTC behavior so the offset-policy name cannot be mistaken for a later instant.

Verification and migration: compare ordinary times, DST gaps, overlaps, and a transition crossing a date boundary. Assert identical UTC instants and no change to schedule occurrence selection. Keep selector composition and excluded dates in Jobs.

### A08. Make property type inference an explicit option

Related extraction: C04. Priority: medium.

Current difference: maintenance endpoints and the Blob dashboard keep date-like and GUID-like JSON strings as strings. The Document dashboard reparses the same object and infers DateTimeOffset and Guid values. The general Common PropertyBag JSON converter accepts nested objects and arrays, which scalar storage metadata rejects. Dimensions require explicit typed operands and follow their own contract.

Proposed alignment: use C04's validation and number handling where equivalent. The new shared parser defaults to preserving JSON strings. Keep the dashboard's existing typed inference as its default compatibility profile, implemented through one shared inference function rather than a second parser. Offer string-preserving dashboard input only through explicit opt-in behavior. Keep general PropertyBag deserialization's nested-data support as a separate contract.

Verification and migration: use identical input containing date-like strings, GUID-like strings, large numbers, arrays, and objects through each scalar metadata boundary. Assert equal results under the same profile and unchanged results for each existing default. Do not change an existing inferred value to a string without explicit caller opt-in.

### A09. Share path matching without silently changing Unicode rules

Related extraction: C09. Priority: medium.

The C09 extraction centralizes path matching in Common and documents invariant UTF-16 code-unit comparisons. Compatibility tests preserve behavior for supplementary-letter case pairs and exact unpaired-surrogate literals.

A Unicode-scalar path comparison mode remains a possible opt-in addition with its own tests and documentation.

## Additional observations requiring separate analysis

### Console command tokenization

[`ConsoleCommandExecutor.SplitArgs` and `EscapeToken`](../../src/Presentation/ConsoleCommands/ConsoleCommandExecutor.cs) and [`CliApplication.QuoteToken`](../../src/Presentation.Cli/CliHost/CliApplication.cs) suggest a reusable tokenizer and formatter. They currently disagree: the splitter toggles on quotes without escape processing, one formatter inserts escaped quotes, and the CLI formatter removes quotes from whitespace-containing tokens.

Do not promote these methods as a general command-line API yet. Specify empty tokens, embedded quotes, tabs, backslashes, unmatched quotes, and parse/format round trips first. A DevKit command grammar would be distinct from shell quoting. Actual process execution should continue using `ProcessStartInfo.ArgumentList`.

### Scalar value conversion

[`ConsoleCommandBinder.TryConvert`](../../src/Presentation/ConsoleCommands/ConsoleCommandBinder.cs), [`EntitiesDashboardValueConverter`](../../src/Presentation.Web/Entities/Dashboard/EntitiesDashboardValueConverter.cs), [`EnumValueConverter`](../../src/Application.DataPorter/Converters/EnumValueConverter.cs), and [`SpecificationBuilder.ConvertValueType`](../../src/Domain/Filtering/SpecificationBuilder.cs) overlap in parsing CLR values.

The contracts differ in culture, null/empty handling, enum validation, fallback behavior, and Domain-specific enumerations. A04 records concrete alignment work. A generic conversion framework would exceed a small extraction.

The public `TypeConverter` declared alongside `SpecificationBuilder` is a particularly narrow extraction candidate: it checks assignability, invokes an exact-source implicit conversion declared on the target type, then calls `Convert.ChangeType`. It has no Domain-specific types. Assess exposing that exact implicit-operator capability through a new compatible Common entry point, retaining a Domain facade and existing Common conversion behavior. Do not present its limited operator search as the full C# conversion rules.

## Exclusions from this extraction backlog

- Cache identity uppercasing, byte limits, UTC component rules, and contract identifiers remain cache policy in [`CacheIdentityNormalizer`](../../src/Application.Caching/Identity/CacheIdentityNormalizer.cs).
- Audit canonicalization, integrity formats, export schemas, and query field bindings remain feature contracts. Generic byte transport can move without generalizing the signed data format.
- Filesystem root handling, link inspection, native publication, durability, and cross-process authority remain Infrastructure concerns. Extracting C06 does not generalize these mechanisms.
- Provider SQL syntax, migrations, EF mappings, and provider-specific stream lifetimes remain with their providers.
- Blob and Document Dimensions keep their independent models, validators, and query translation. Storage FilterModel field rules and query-bound cursor identity remain in Storage. Reuse existing Common codecs without adding another shared Dimensions or filtering engine. [Dimensions ADR](../adr/0044-independent-storage-dimensions.md)
- Domain typed IDs, value objects, specifications, and business validation remain in Domain. Common must not acquire a Domain dependency to support a convenience helper.
- Async sequence `ToListAsync` in DataPorter is not an automatic new Common API. First check the target framework's async LINQ operators and extension-method ambiguity. Preserve its existing null behavior through an adapter if replacement is warranted.

## Common documentation requirements

Every implemented extraction must be documented in the appropriate Common guide in the same change as its code and tests. XML comments, this specification, and the originating feature guide do not replace the Common guide. Client developers must be able to discover and use the extracted element without reading its original feature documentation.

The following destinations are required. Section names describe where to add the implemented API; they do not imply that the proposed API is already available.

- C01 UTF-8 truncation belongs in [Common Extensions](../common-extensions.md), under text, enum, and argument helpers. Explain byte limits versus character limits and show multibyte text.
- C02 friendly duration parsing belongs in [Common Extensions](../common-extensions.md), under date and time helpers. Document accepted units, invariant parsing, invalid-input results, and the distinction from clock parsing.
- C03 is documented under [hash output in Common Utilities](../common-utilities.md#hash-output-as-it-is-written), with a cross-reference from hashing.
- C04 is documented under [plain scalar JSON in Common Serialization](../common-serialization.md#plain-scalar-json), alongside the persisted `PropertyBag` format and the nested JSON converter.
- C05 HTTP representation conditions belong in the HTTP helpers section of [Common Extensions](../common-extensions.md#evaluate-representation-conditions-and-byte-ranges). Identify the `Common.Extensions.Web` package and its ASP.NET Core dependency. Document condition precedence, outcomes, byte-range limits, and the caller's responsibilities.
- C06 keyed asynchronous locks are documented under [Keyed asynchronous locks](../common-utilities.md#keyed-asynchronous-locks) in Common Utilities.
- C09 anchored path globs are documented under [Match anchored path globs](../common-extensions.md#match-anchored-path-globs) in Common Extensions.
- C10 is documented under [HTTP helpers in Common Extensions](../common-extensions.md#http-helpers). The guide covers the Base64Url transport, supported provider versions, and condition handling.
- C11 scalar bag persistence is documented in the [`PropertyBagSerialization` section](../common-serialization.md#propertybagserialization), next to `PropertyBagScalarCodec`. The Blob and Document guides link to that section.

Alignment work must update the same Common references when it adds or clarifies shared contracts. A04 scalar conversion and A05 command grammar belong in Common Extensions. A06 retry conventions belong in Common Utilities' resiliency guidance, with a link to the [Results guide](../features-results.md) when documenting `RetryPolicy`. A07 must explain the selected daylight-saving-time policies in Common Extensions' date and time section. The remaining alignments use their related extraction's destination above.

### Required guide content

For each new public element or supported protected extension point, document:

- Its purpose, owning package, namespace, actual signatures, defaults, and required registration or configuration.
- A minimal working example with fictional data, using only the Common packages required by the element. Show disposal or asynchronous cleanup where applicable.
- Input and output semantics, invalid-input and failure behavior, cancellation, ownership, and relevant limits. Include edge cases that distinguish it from neighboring Common helpers.
- Existing feature compatibility behavior and how callers explicitly select new behavior. State which legacy APIs remain supported without suggesting that their signatures or defaults change.
- Links to related Common helpers and to feature adapters where their feature-specific policies matter. Feature guides should link back to the Common reference rather than maintain a second copy of the generic API documentation.

Keep existing guide structure and unrelated content intact. Extend the established pages before creating a separate page. If a new Common page is necessary, add it to [the documentation index](../INDEX.md) and [MkDocs navigation](../../mkdocs.yml), following the repository's `common-*.md` publishing convention.

Documentation is part of completion for each implementation slice. Verify links and examples against the delivered public API, and run the repository's documentation build when public guide content changes. Do not publish proposed helpers as implemented capabilities before their code is delivered.

## Implementation and verification requirements

Implement one candidate per logical change. A batch describes priority, not a requirement to combine candidates into one PR.

For each accepted candidate:

1. Characterize the existing feature behavior that will move, including the edge cases named above.
2. Capture the affected existing public contracts and confirm the new name, public or protected accessibility, destination package, dependencies, and compatibility adapter before editing consumers. Check new names and overloads for ambiguity in unchanged client code.
3. Implement the smallest reusable contract and migrate compatible internals. For alignment work, preserve each existing default and specify how callers explicitly select different behavior. Do not add speculative integrations to claim adoption.
4. Add focused tests under the existing `tests/Common.UnitTests` project and retain feature tests for adapter behavior. Complete the source, binary, behavior, and format checks required by the compatibility section. Check new and retained APIs separately.
5. Build sequentially in the same worktree and run Common plus the affected Application, Infrastructure, or Presentation test projects. Run integration tests only when a provider or endpoint behavior actually changes, after inspecting their service requirements.
6. Complete the assigned Common guide update and feature cross-references in the same change, including fictional examples and XML documentation for public and protected declarations. Apply the visibility and documentation requirements above before marking the slice complete.

Concurrency extractions additionally require contention, timeout, cancellation, disposal, and entry-retirement tests. Stream extractions require seekable and non-seekable streams, supported sync and async entry points, ownership, failures, and cancellation. Serialization extractions require byte-level compatibility checks where hashes or versioned exports depend on exact output.

Check source references and Markdown diffs when editing this specification. Run the compatibility and runtime checks above for each implementation.
