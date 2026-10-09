---
goal: Implement unified Runtime and Operation Profiling with an HTTP Request adapter
version: 1.0
date_created: 2026-10-07
last_updated: 2026-10-09
owner: bITdevKit maintainers
status: Completed
tags: [feature, profiling, architecture, persistence, dashboard]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

Implement [Runtime and Operation Profiling](../docs/specs/spec-profiling-runtime-and-requests.md), including generic operation recording, aggregated segments, periodic provider persistence, the HTTP adapter, feature behaviors, and Runtime/Operations/Requests dashboard views. Preserve Runtime Broadcast behavior. Replace the unused profiling API and storage names without final compatibility aliases.

This is an execution plan, not implementation evidence. All tasks start incomplete. Repository paths and extension points were inspected at commit `224904465`. Resolve paths from the repository root. The renamed specification is authoritative; the former `pln-feature-profiling-runtime-and-requests-1.md` is not an input. Here, implementation phases are work packages; the product's timed work is always called a **Segment**.

## Execution checkpoint — 2026-10-09 UTC

All 64 tasks are implemented and verified, including the original feature, final application integration and Phase 14 repository/Active Entity profiling behaviors. Phase 14 passed 593 Domain unit, 72 provider integration, 55 application unit and 109 application integration tests, plus the solution and MkDocs/API builds. The accepted 84-trial capacity matrix uses the corrected 21-trial memory rerun and preserved 63 EF trials; original measurements remain historical evidence. The solution build, workspace unit/integration tasks, feature acceptance and documentation/site build passed. Final WeatherFiesta checks passed 55 unit and 109 integration tests, followed by browser verification and another complete MkDocs/API build. The profiling guide and specification remain application-neutral; example setup is documented in the application's README. The historical [pause checkpoint](evidence/profiling-runtime-and-operations-1/phase-12/performance/PAUSED.md) remains as evidence. See the [implementation evidence](pln-feature-profiling-runtime-and-operations-1-evidence.md) for results, existing skips/warnings and measurement limitations. No planned task remains outstanding.

## 1. Requirements & Constraints

Implementation refinements: parent JSON retains non-queryable operation measurements and outcome/reducer details, `IProfilingDbContext` uses inherited generic EF sets, and recoverable observation/worker failures never change application execution. Track these refinements in TASK-056 and TASK-057 before the remaining analysis work. Avoid introducing `internal` modifiers where practical and document every public declaration with XML comments and usage examples. Keep private implementation details private.

The requirement identifiers below match the specification. Task and test identifiers provide implementation traceability.

| Requirement | Required result | Tasks | Tests |
| --- | --- | --- | --- |
| REQ-001 | One feature; Runtime, Operations, and HTTP-filtered Requests views. | TASK-008, TASK-029, TASK-044, TASK-045 | TEST-001, TEST-014 |
| REQ-002 | Operation scopes work without HTTP, Runtime, Broadcast, or a request DI scope. | TASK-009, TASK-010, TASK-014 | TEST-002, TEST-003 |
| REQ-003 | Explicit fluent subfeatures; Requests requires explicitly enabled Operations. | TASK-008, TASK-029, TASK-030 | TEST-001 |
| REQ-004 | Every eligible HTTP request is selected by default without endpoint opt-in. | TASK-031, TASK-032 | TEST-011 |
| REQ-005 | Unique operation IDs, stable keys, dimensions, outcomes, and structured segment paths. | TASK-003, TASK-004, TASK-009, TASK-011 | TEST-002, TEST-004 |
| REQ-006 | Aggregate repeated, nested, and parallel segments without occurrence traces. | TASK-011, TASK-012, TASK-013 | TEST-003, TEST-004 |
| REQ-007 | Capture request lifecycle, method, route, status, bytes, and response ID. | TASK-032, TASK-033, TASK-034, TASK-035 | TEST-011, TEST-012 |
| REQ-008 | One replaceable provider; bounded periodic writes for memory and EF. | TASK-016, TASK-021, TASK-026, TASK-027, TASK-029, TASK-056 | TEST-005, TEST-006, TEST-008 |
| REQ-009 | Slow, Recent, and By count modes with grouping and bounded refresh. | TASK-040, TASK-044, TASK-045 | TEST-013, TEST-014 |
| REQ-010 | Wall-time bars count overlap once and expose partial coverage. | TASK-013, TASK-046 | TEST-004, TEST-014 |
| REQ-011 | Correlate Runtime by process identity and observed interval; retain independent ownership. | TASK-006, TASK-042, TASK-046 | TEST-010, TEST-015 |
| REQ-012 | Exact lookup, distributions, comparisons, sample counts, and capture quality. | TASK-019, TASK-024, TASK-040, TASK-041 | TEST-013 |
| REQ-013 | UTC datetimes with literal `Z`; elapsed time uses a monotonic clock. | TASK-003, TASK-009, TASK-011, TASK-041 | TEST-002, TEST-004, TEST-013 |
| REQ-014 | Generic typed dimensions/measurements and bounded adapter metadata. | TASK-004, TASK-009, TASK-012 | TEST-004 |
| REQ-015 | Report capture/persistence loss, unknown commits, scope, retention, and freshness. | TASK-027, TASK-028, TASK-043, TASK-047 | TEST-005, TEST-008, TEST-014 |
| REQ-016 | Default HTTP key is incoming path; configured prefix stripping and path blacklist. | TASK-031 | TEST-011 |
| REQ-017 | Structured operation/segment start and stop logging at Trace/Verbose. | TASK-015 | TEST-007 |
| REQ-018 | Head sampling: AllRequests, probability, and node-local token bucket. | TASK-031, TASK-032 | TEST-011 |
| REQ-019 | Jobs, pipeline steps, orchestration actions, repositories and Active Entities participate through optional behaviors. | TASK-036, TASK-037, TASK-038, TASK-039, TASK-060, TASK-061, TASK-062, TASK-063, TASK-064 | TEST-016, TEST-022 |
| REQ-020 | Optional feature injection works when AddProfiling is omitted; no hidden registrations. | TASK-030, TASK-035, TASK-036, TASK-037, TASK-039, TASK-060, TASK-061, TASK-062, TASK-063 | TEST-001, TEST-016, TEST-022 |
| REQ-021 | Clear Runtime, Operations, or both, fully or by completion-UTC range; fence late writes. | TASK-017, TASK-018, TASK-023, TASK-025, TASK-028 | TEST-006, TEST-008, TEST-009 |
| REQ-022 | Cached executing-node identity on every operation; Runtime keeps Broadcast control. | TASK-006, TASK-009, TASK-044 | TEST-002, TEST-010 |
| REQ-023 | Case-insensitive keys/path components, identical across providers, original display casing. | TASK-004, TASK-012, TASK-019, TASK-024 | TEST-004, TEST-008, TEST-013 |
| REQ-024 | Expire abandoned capture, release memory, preserve running business work. | TASK-014, TASK-034 | TEST-003, TEST-012 |

- **CON-001**: Preserve Onion dependencies. Public contracts and value DTOs belong in `src/Common.Abstractions/Profiling/`; use existing `IResult` and `IResult<T>`. Concrete `Result` creation and recorder/provider orchestration belong in `Common.Utilities`; EF context/entities stay in `Infrastructure.EntityFramework`; ASP.NET middleware and request sampling stay in `Presentation.Web`.
- **CON-002**: `Common.Results` already references `Common.Abstractions`. Do not add the reverse reference or move Runtime's Broadcast-dependent services into the abstractions assembly. Keep concrete error factories outside that assembly. This dependency correction is reflected in the canonical specification's `ClearAsync` signature.
- **CON-003**: Application work never waits for profiling storage, queue capacity, maintenance, or a sampling token. Helpers invoke their business delegate exactly once and preserve its result, exception, and cancellation behavior. Recoverable capture, adapter, timer and worker faults are isolated, including initialization and cleanup (TASK-057); invalid DI/configuration remains a setup error. Process-fatal failures cannot be guaranteed away.
- **CON-004**: Runtime measurement retains its explicit asynchronous store/session lifecycle. Never implement operation recording by calling the existing `ProfilingMeasurementService` or its renamed Runtime equivalent.
- **CON-005**: No OpenTelemetry, Aspire, Azure Monitor, per-operation CPU/memory attribution, CSV, raw invocation traces, Operations CLI, or custom-client HTTP API. Preserve Runtime JSON/archive/Perfetto exports; archives become version 2 and reject version 1 before mutation.
- **CON-006**: Change only profiling-specific uses of names such as `WithEntityFrameworkStore`. File monitoring and other feature overloads keep their names and behavior. Preserve user changes and run full builds/test commands sequentially.
- **CON-007**: Use existing .NET 10 projects and centrally managed dependencies. No new production package is required. Reuse `TimeProvider`, BCL synchronization, current logging, RazorSlices, EF, and existing SQL Server/PostgreSQL test fixtures.
- **CON-008**: Final public APIs have XML documentation and compile-checked examples. Nullable annotations remain disabled. Code examples use tabs; source code follows the repository `.editorconfig`.
- **CON-009**: Keep `docs/features-profiling.md` and the generic profiling specification application-neutral. Document WeatherFiesta-specific registration, workloads and verification in `examples/WeatherFiesta/WeatherFiesta-README.md` and implementation evidence.
- **SEC-001**: Never retain bodies, SQL text, arguments/results, raw query strings, exception objects, stack dumps, application scopes, or service providers in completed records. Error messages require a bounded host sanitization policy.
- **SEC-002**: All profiling views, HTML content, detail, and JSON routes remain under dashboard enablement and authorization. JSON is dashboard-internal. IDs and paging cursors confer no authorization; metadata renders as text.
- **SEC-003**: Suppress profiling's own writer/query/provider instrumentation. HTTP blacklist and sampling suppression also prevent nested explicit helpers and behaviors from creating replacement roots.
- **PAT-001**: An injected singleton `IOperationProfiler` resolves execution-local ownership. HTTP middleware owns the request operation; endpoint/service injection enriches it. Independent worker entries clear inherited context. Optional behaviors accept an absent profiler without installing a fallback service.
- **PAT-002**: Providers share conformance tests. Each root and owned graph is atomic; stable-ID retries, writer leases, settlement, clear fences, retention, and query publication boundaries use provider-authoritative coordination.

The following values are the implementation baseline. Larger limits require explicit configuration; benchmark results do not silently change defaults.

| Setting | Baseline |
| --- | --- |
| Master / omitted subfeature / explicitly selected subfeature | Disabled / disabled / enabled subject to master and dependency validation |
| Request sampler / prefix / blacklist | AllRequests / empty / `/_bdk/**`, `/health*`, `/swagger/**`, `/scalar/**`, `/openapi/**`; maximum 128 patterns of 256 characters |
| Request body observation / lifecycle logging | Off / Trace through Microsoft logging, equivalent to Verbose |
| Active roots / active recording payload / per-root payload | 1,024 / 32 MiB / 64 KiB |
| Distinct segment paths / live segments per root / depth | 128 / 256 / 32 |
| Recording duration / cleanup tick | 15 minutes / 5 seconds, configurable positive finite durations |
| Dimensions, measurements, source fields | 32 each per applicable scope |
| Key, kind, display name / string value / unit | 128 / 256 / 32 characters |
| Safe error message / failure categories per path | 1,024 characters / 8 plus Other |
| Queue plus in-flight records / payload | 8,192 / 64 MiB |
| Flush interval / batch limits | 1 second / 512 records and 4 MiB |
| Batch starts per tick / next-batch budget | 8 including retries / 250 ms |
| Write timeout / extra attempts / shutdown drain | 5 seconds / 2 after 1 and 2 seconds / 5 seconds |
| Writer lease / renewal / clear poll | 30 seconds / 5 seconds / every writer tick, including idle |
| Clear preparation / active leases / unfinished fences | 10 seconds / 128 / 64 per provider scope |
| Memory operation retention | 10,000 roots, 128 MiB, or 24 hours, first limit reached |
| EF operation retention | 100,000 roots or 7 days, first limit reached |
| Maintenance tick / delete batch / next-batch budget | 5 seconds / 512 roots / 250 ms |
| Query page default/max / exact analysis selection max | 50/200 / 10,000 operations per selection |
| Query timeout / concurrent builds / cursor lifetime | 5 seconds / 4 per serving process without a waiting queue / 5 minutes |
| Dimension predicates / grouping dimensions / overlay snapshots | 8 / 4 / 2,000 |
| Dashboard initial window / outcome / result count | Last 15 minutes by completion UTC / Completed / 25 |
| Dashboard refresh choices / default | Paused, 2, 5, 10, 30 seconds / 5 seconds |
| Runtime interval/minimum / duration / participation/grace | 1 second/500 ms / 30 seconds with automatic stop / 1 second each |
| Runtime unpinned retention | 20 terminal sessions or 7 days |

## 2. Implementation Steps

Every phase declares its dependencies and exit criteria. A phase starts only after all dependencies pass. Within a phase, only tasks with satisfied `Depends on` entries are eligible to execute; tasks editing the same file must execute sequentially even if their logical dependencies are otherwise independent. Mark a task completed only after its stated evidence exists; fill Date with its UTC completion date. No implementation task is marked completed by creating this plan.

### Implementation Phase 1

- GOAL-001: Establish the executable baseline and a complete profiling-only rename inventory.
- Dependencies: None.
- Completion criteria: Baseline build/test outcomes and symbol consumers are recorded; no source implementation changes occur in this phase.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-001 | Inspect the canonical spec and run `.vscode/tasks.json` tasks `Solution - build` and the focused profiling tests listed in section 6, sequentially. Create `plan/pln-feature-profiling-runtime-and-operations-1-evidence.md` with the revision, commands, environment, exit codes, failures, and container availability. Record pre-existing failures separately; unavailable database infrastructure is not a passing test. Depends on: none. | [x] | 2026-10-07 |
| TASK-002 | Inventory profiling references in `src/`, `tests/`, `examples/`, and `docs/` using `rg`. Record exact old-to-final symbols from spec section 2.4 in the evidence file, including Runtime types, store facets, options, console routes, JSON fields, marker conversions, and host registrations. Identify `KeyGenerator.CreateLowercase` calls in `src/Common.Utilities/Profiling/ProfilingModels.cs`; generated identity factories must stay outside the abstractions assembly. Distinguish unrelated storage extensions before renaming. Depends on: TASK-001. | [x] | 2026-10-07 |

### Implementation Phase 2

- GOAL-002: Establish compilable public contracts and complete the Runtime naming/dependency cutover.
- Dependencies: GOAL-001.
- Completion criteria: The solution builds; existing Runtime regression tests pass with final type names. `Common.Abstractions` has no dependency on Results, Utilities, EF, ASP.NET, or Broadcast implementation types. New operation capture is not enabled by Runtime registration.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-003 | Extract shared identities/value records and add final Runtime contracts in `src/Common.Abstractions/Profiling/ProfilingModels.cs` and `RuntimeProfilingAbstractions.cs`, replacing their declarations in `src/Common.Utilities/Profiling/ProfilingModels.cs` and `ProfilingAbstractions.cs`. Use `IResult` interfaces in control/query/store signatures. Move transitive query/archive/evaluation value DTOs referenced by these public interfaces into sibling abstraction files; keep implementations and concrete error factories in Utilities. Create `src/Common.Utilities/Profiling/ProfilingIdentityFactory.cs` for GUID/readable-key generation using existing key generation; model constructors retain BCL-only validation. Move Broadcast-facing interfaces with their adapters into Utilities instead of exposing Broadcast types in common DTOs. Update all consuming signatures and identity factory calls. Depends on: TASK-002. | [x] | 2026-10-07 |
| TASK-004 | Create `src/Common.Abstractions/Profiling/OperationProfilingAbstractions.cs`, `OperationProfilingModels.cs`, and `ProfilingValueModels.cs`. Define `IOperationProfiler`, operation/segment scopes with `IsRecording`, typed dimensions/measurements, outcome/quality descriptors, structured paths, immutable root/summary records, safe adapter envelopes, and the pure-data HTTP metadata projection. Do not reference HttpContext. Define UTC timestamps, operation ID behavior, case comparison, reducer data, incomplete observation reasons, and typed JSON scalar representations. Depends on: TASK-003. | [x] | 2026-10-07 |
| TASK-005 | Create `src/Common.Abstractions/Profiling/ProfilingStorageAbstractions.cs` and `OperationProfilingQueryModels.cs`. Define runtime/operation facets, provider capabilities, `ClearAsync`, `ResumeMaintenanceAsync`, lease/open/synchronize/acknowledge/append/close operations, write envelopes, per-record results, and query boundary DTOs. Distinguish writer completion sequence, segment-local completion sequence, and provider commit watermark. Public asynchronous result signatures use `Task<IResult>` or `Task<IResult<T>>` as appropriate; no EF entities, IQueryable, or database clients cross this boundary. Depends on: TASK-004. | [x] | 2026-10-07 |
| TASK-006 | Refactor `src/Common.Utilities/Profiling/Runtime/ProfilingNodeIdentityProvider.cs` to cache one process GUID/key, PID, host/display metadata, and process-start UTC without I/O. Add `RuntimeProfilingNodeRegistrationAdapter.cs` beside it to attach private Broadcast correlation to that cached node through background/control persistence. Rename implementations in `Control/`, `Runtime/`, and `Scopes/` to Runtime-prefixed symbols per spec section 2.4. Keep Runtime measurement async and store-owning; update `ProfilingSegmentContext` to a Runtime-owned context. Depends on: TASK-003. | [x] | 2026-10-07 |
| TASK-007 | Update `src/Common.Utilities/Profiling/Archive/ProfilingArchiveService.cs`, `ProfilingArchiveModels.cs`, `Export/ProfilingPerfettoExportService.cs`, `Query/ProfilingQueryService.cs`, `Evaluation/`, and `Metrics/ProfilingCustomMetricListener.cs` to final Runtime names. Merge instantaneous phase/action marker models into scoped `ProfilingMarker`; retain Runtime interval `ProfilingSegment` and parent/metric references. Archive format remains `bitdevkit.profiling.archive`, version becomes 2, and version 1 rejects before import mutation. Update matching Runtime tests and JSON fixtures. Depends on: TASK-003, TASK-006. | [x] | 2026-10-07 |
| TASK-008 | Update `src/Common.Utilities/Profiling/ProfilingOptions.cs`, `ProfilingRegistration.cs`, `ProfilingServiceCollectionExtensions.cs`, existing runtime store implementations, EF runtime mappings, `src/Presentation.Web/Profiling/ConsoleCommands/`, current dashboard consumers, and `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Program.cs` for `WithRuntimeProfiling`, nested Runtime options, and `profiling runtime` commands. Retain reusable Runtime store logic as internal `InMemoryRuntimeProfilingStore` and `EntityFrameworkRuntimeProfilingStore<TContext>` components for the final provider façades. Rename `IProfilingContext.cs` to `IProfilingDbContext.cs`; update existing test contexts. Preserve Runtime-only behavior and defaults. Complete the symbol cutover across current consumers without compatibility aliases or duplicate public DTO definitions. Final combined-provider registration is completed in TASK-029. Depends on: TASK-005, TASK-006, TASK-007. | [x] | 2026-10-07 |

### Implementation Phase 3

- GOAL-003: Implement the generic recorder without HTTP, Runtime control, or database access.
- Dependencies: GOAL-002.
- Completion criteria: Operation/segment unit tests prove exactly-once delegate execution, isolation, arithmetic, expiry, bounded state, and fault isolation using controlled time and a test completion sink.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-009 | Create `src/Common.Utilities/Profiling/Operations/OperationProfiler.cs`, `OperationProfilingContext.cs`, and `ProfilingValueValidator.cs`. Implement BeginOperation, SetKey, SetDimension, SetMeasurement, immutable option snapshots, execution-local context, cached identity, admission accounting, and no-op/rejected/suppressed boundaries. Validate typed values, UTC, lengths, counts, byte budgets, and canonical `.ToUpperInvariant()` comparison keys; preserve display casing and rejected replacement values. No I/O occurs in these methods. Depends on: TASK-004, TASK-006. | [x] | 2026-10-07 |
| TASK-010 | Create `Operations/ProfilingOperationScope.cs`, `ProfilingSegmentScope.cs`, and `OperationProfilingHelpers.cs` under `src/Common.Utilities/Profiling/`. Implement synchronous/asynchronous RunOperation and RunSegment helpers, explicit Complete/Fail/Cancel, result classification, original exception propagation, matched-token cancellation, nested-root restoration, and explicit handles for parallel branches. Use explicitly named native ValueTask helper methods for pipeline behaviors without ambiguous Task/ValueTask lambda overloads. Freeze/enqueue once and never rerun a business delegate after an instrumentation fault. Depends on: TASK-009. | [x] | 2026-10-08 |
| TASK-011 | Create `src/Common.Utilities/Profiling/Operations/ProfilingSegmentAccumulator.cs`. Aggregate by structured full path with per-invocation parent handles, count/outcome buckets, inclusive duration, min/max, and self duration computed from the union of direct-child intervals. Bound active children/depth/paths; rejected segment descendants remain suppressed. Closing a parent closes live children incomplete without counting later disposal twice. Depends on: TASK-010. | [x] | 2026-10-08 |
| TASK-012 | Implement measurement reducers, weighted means, Last completion sequence/UTC, safe failure-category limits, mixed/missing dimensions, and overflow/conflict quality in `ProfilingSegmentAccumulator.cs` and new `src/Common.Utilities/Profiling/Operations/ProfilingValueCodec.cs`. Persist typed numeric filter values losslessly; strings remain ordinal case-sensitive values while names/keys are case-insensitive. Add cases for large Int64 values, decimal versus double, empty versus absent, culture differences, and outcome-specific measurements. Depends on: TASK-011. | [x] | 2026-10-08 |
| TASK-013 | Create `src/Common.Utilities/Profiling/Operations/ProfilingWallTimeAccumulator.cs`. Update bounded top-level buckets online: one live invocation credits its key, multiple credit Parallel once, none credit Outside segments. Clip at finalization/deadline, label truncated coverage, and handle zero elapsed time. Test sequential repeated keys, same-key overlap, nested parallel work, and totals equal to observed root wall time. Depends on: TASK-011. | [x] | 2026-10-08 |
| TASK-014 | Create `src/Common.Utilities/Profiling/Operations/OperationProfilingCleanupService.cs`. Enforce 15-minute default MaxRecordingDuration with 5-second cleanup independent of the writer/provider. Expire at the logical deadline, persist Incomplete/CaptureDeadlineExceeded once, release aggregates/capacity, leave only closed-context state, and preserve business execution. Close remaining captures HostStopping before bounded drain. Test stalled writers, late disposal, parallel children, and overlapping Blazor actions. Depends on: TASK-010, TASK-012, TASK-013. | [x] | 2026-10-08 |
| TASK-015 | Add `src/Common.Utilities/Profiling/Operations/ProfilingLifecycleLogger.cs` with stable ProfilingOperationStarted/Stopped and ProfilingSegmentStarted/Stopped IDs/templates at Trace. Include operation ID, key/path, invocation sequence, outcome, duration, and bounded code/type only. Guard formatting with IsEnabled; omit all events for disabled/suppressed/rejected capture; contain logger faults without recursive logging. Add `OperationProfilingLoggingTests.cs` under `tests/Common.UnitTests/Utilities/Profiling/`. Depends on: TASK-010. | [x] | 2026-10-08 |

### Implementation Phase 4

- GOAL-004: Implement the complete in-memory provider and provider-neutral coordination conformance cases.
- Dependencies: GOAL-002.
- Completion criteria: Memory provider tests pass for writes, queries, hard limits, clear/retry/retention races, stable paging, and Runtime lifecycle coexistence.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-016 | Create `src/Common.Utilities/Profiling/Storage/InMemoryProfilingStorageProvider.cs`, composing the Runtime component from TASK-008 and an operation store behind the same provider. Append immutable whole root graphs atomically with stable-ID idempotency and typed per-record results. Keep independent Runtime and operation retention budgets, shared node references, and bounded charged-payload accounting. Depends on: TASK-005, TASK-008. | [x] | 2026-10-08 |
| TASK-017 | Create `src/Common.Utilities/Profiling/Storage/ProfilingWriterRegistry.cs` and `ProfilingClearCoordinator.cs` for the memory implementation. Implement store epoch, registration generations, stable open-attempt IDs, 30-second leases, renewal, per-writer settlement, cached immutable clear cutoffs, and atomic append admission. Never reopen an expired token or relabel old envelopes under a new lease. Depends on: TASK-016. | [x] | 2026-10-08 |
| TASK-018 | Implement ClearAsync and ResumeMaintenanceAsync in `InMemoryProfilingStorageProvider.cs`: prepare, collect cutoffs, seal, bounded delete, writer disposal, completion; reject Runtime-inclusive clears when Runtime is active and guard racing start/import. Preserve newer/post-cutoff and unmatched records. Recover expired preparation/sealed clears, compact only safe fences, protect unsettled records from retention, and return RetentionCapacity when safe eviction cannot admit a write. Enforce all-history versus half-open completion-UTC range semantics. Depends on: TASK-017. | [x] | 2026-10-08 |
| TASK-019 | Implement memory QueryRecordsAsync, QueryGroupsAsync, exact lookup, and bounded analysis-input queries in new `src/Common.Utilities/Profiling/Storage/InMemoryOperationProfilingQueries.cs`. Apply typed predicates to the same matching summary path; use immutable publication watermarks, deletion revisions, canonical GUID sorting, and query-bound cursors. Root removal increments the deletion revision. Counts cover the full filtered boundary, not a visible page. Depends on: TASK-018. | [x] | 2026-10-08 |
| TASK-020 | Add `OperationProfilingStoreContractTests.cs`, `ProfilingWriterLeaseTests.cs`, and `ProfilingClearContractTests.cs` under `tests/Common.UnitTests/Utilities/Profiling/`. Establish reusable scenario inputs/expected outcomes for both providers: partial batches, lost acknowledgements, two writers, expiry after acknowledgement, new registrations during clear, retention-before-retry, uncertain settlement, epoch reset, bounded fences, and queries racing mutation. Keep backend harness setup separate from invariant assertions. Depends on: TASK-019. | [x] | 2026-10-08 |

### Implementation Phase 5

- GOAL-005: Implement EF persistence with the same observable semantics on SQLite, SQL Server, and PostgreSQL.
- Dependencies: GOAL-004.
- Completion criteria: EF model/unit tests and the shared behavioral scenarios pass on all three actual database engines; profiling never creates or migrates host tables automatically.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-021 | Create `src/Infrastructure.EntityFramework/Profiling/EntityFrameworkProfilingStorageProvider.cs` and compose the Runtime EF component. Extend `IProfilingDbContext.cs`, `ProfilingModelBuilderExtensions.cs`, and `Entities/` with `OperationProfilingEntities.cs` and `ProfilingCoordinationEntities.cs`. Map roots, summaries, typed dimensions/measurements, adapter projections, writer/open-attempt/clear state, epoch, publication position, and deletion revision. Add unique identities, owner references, completion/node/key/path/filter indexes, and provider-portable ordinal/binary comparison projections. Do not store operation collections in Runtime JSON or add a row per segment invocation. Depends on: TASK-020. | [x] | 2026-10-08 |
| TASK-022 | Implement AppendOperationsAsync and node upsert in `EntityFrameworkProfilingStorageProvider.cs` and `ProfilingEntityMapper.cs` with a fresh context per attempt, transactionally atomic root graphs, stable-ID conflict handling, typed partial results, and retention admission. Coordinate commit visibility with publication watermarks; a later commit cannot appear below an issued query boundary. Do not use database default text/GUID collation as the common comparer. Depends on: TASK-021. | [x] | 2026-10-08 |
| TASK-023 | Add `src/Infrastructure.EntityFramework/Profiling/EntityFrameworkProfilingCoordination.cs` for transactional lease registration/renewal/settlement, clear acknowledgements/fences, Runtime maintenance gates, and bounded recovery/retention. Settlement and clear sealing must serialize against append validation across independent contexts/processes. Retain an acknowledged cutoff even after its writer expires. Bounded metadata cleanup must continue rejecting stale tokens and deleted-root replays. Depends on: TASK-022. | [x] | 2026-10-08 |
| TASK-024 | Add `src/Infrastructure.EntityFramework/Profiling/EntityFrameworkOperationProfilingQueries.cs`. Translate validated filters, stable keyset pages, complete counts/rankings, and bounded analysis selections server-side. Preserve dimension types and same-summary matching. Validate cursor epoch/watermark/deletion revision/expiry and propagate cancellation/timeouts. Avoid deserializing all history, client-side unbounded grouping, and N+1 node/segment loads. Depends on: TASK-023. | [x] | 2026-10-08 |
| TASK-025 | Update `tests/Infrastructure.UnitTests/EntityFramework/Profiling/ProfilingTestDbContext.cs`, model/store tests, and `tests/Infrastructure.IntegrationTests/EntityFramework/Profiling/EntityFrameworkProfilingStoreTestsBase.cs` plus its Sqlite/SqlServer/Postgres subclasses to the final context/provider. Run TASK-020 scenarios with two service providers and skewed clocks. Verify ConfigureProfiling and migration SQL in isolated test databases, atomic root deletion, startup schema non-mutation, and preservation of unrelated host tables. Do not execute schema resets against developer application databases. Depends on: TASK-024. | [x] | 2026-10-08 |

### Implementation Phase 6

- GOAL-006: Wire production recording, periodic persistence, recovery, health, and fluent capability composition.
- Dependencies: GOAL-003, GOAL-005.
- Completion criteria: Both providers use one bounded writer lifecycle; registration combinations and saturation/shutdown tests pass without business-path I/O or Runtime/Broadcast dependencies in operation-only hosts.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-026 | Create `src/Common.Utilities/Profiling/Operations/OperationProfilingCompletionQueue.cs`. Transfer payload ownership from live scope to immutable envelope exactly once; assign lease/sequence and publish under one short local boundary. Enforce 8,192-record/64-MiB queue-plus-in-flight limits with explicit reject-incoming results. Drop pre-lease completions as writer-unavailable; preserve active scopes that finish after lease activation. No channel mode may report success while silently dropping data. Depends on: TASK-014, TASK-017. | [x] | 2026-10-08 |
| TASK-027 | Create `src/Common.Utilities/Profiling/Operations/OperationProfilingWriterService.cs`. Synchronize/poll clear state even on idle ticks; capture a lease-qualified queue watermark; flush sequential batches at one-second intervals with 512/4-MiB, eight-start, and 250-ms limits. Apply five-second attempt timeout and two bounded retries after one/two seconds, counting retries against tick budgets. Settle successful members, fence exhausted uncertain commits while reporting Unknown, reject stale leases, coalesce ticks, and never overlap replacement calls with a still-running attempt. Add writer tests using controlled time and fault-injected providers. Depends on: TASK-025, TASK-026. | [x] | 2026-10-08 |
| TASK-028 | Create `src/Common.Utilities/Profiling/Storage/ProfilingMaintenanceService.cs` and `Operations/OperationProfilingHealth.cs`. Resume maintenance independently of enabled capture capabilities; use five-second ticks, 512-root batches, and 250-ms next-batch budget. Report local queue/admission/sample/flush/loss counters separately from shared stored counts/leases. Suppress self-instrumentation, contain worker exceptions, and perform cleanup-before-five-second-drain shutdown ordering. Depends on: TASK-027. | [x] | 2026-10-08 |
| TASK-029 | Finalize `src/Common.Utilities/Profiling/ProfilingServiceCollectionExtensions.cs`, `ProfilingRegistration.cs`, and `ProfilingOptions.cs`, plus `src/Infrastructure.EntityFramework/Profiling/ServiceCollectionExtensions.cs`. Add WithOperationProfiling, WithInMemoryProvider, WithProvider, and WithEntityFrameworkProvider; map both store facets to the same provider façade. Register singleton façade, independent recorder cleanup, operation writer only when enabled, shared retained-history queries/maintenance, and Runtime workers/Broadcast only with Runtime enabled. Validate final composed options regardless of fluent call order and prevent duplicate registrations or conflicting providers. Remove interim Runtime-only wiring superseded by the provider façade. Depends on: TASK-028. | [x] | 2026-10-08 |
| TASK-030 | Add `tests/Common.UnitTests/Utilities/Profiling/ProfilingRegistrationTests.cs` and extend `ProfilingFoundationTests.cs`. Cover omitted AddProfiling, master disabled, each subfeature combination, repeated/reordered fluent calls, invalid limits, two explicit providers, operation-only startup with no Broadcast, initial database outage, disabled façade, and broken explicitly registered DI graphs. Inspect service lifetimes and assert no scoped context is captured. Depends on: TASK-029. | [x] | 2026-10-08 |

### Implementation Phase 7

- GOAL-007: Implement HTTP capture and sampling without changing application request behavior.
- Dependencies: GOAL-006.
- Completion criteria: Full-host middleware tests cover sampled/skipped requests, errors and re-execution, streaming/cache/body paths, expiry, and disabled/omitted setup.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-031 | Create `src/Presentation.Web/Profiling/Requests/RequestProfilingOptions.cs`, `RequestProfilingSampling.cs`, and `RequestProfilingPathMatcher.cs`; extend `ProfilingServiceCollectionExtensions.cs` with WithRequestProfiling. Implement required Operations dependency, anchored bounded wildcard matching before prefix stripping, canonical overlong-path hashing, AllRequests, independent Probability, and monotonic node-local token bucket. Validate custom singleton samplers and count invalid/throwing decisions without changing request execution. Depends on: TASK-029. | [x] | 2026-10-08 |
| TASK-032 | Add `Requests/RequestProfilingMiddleware.cs`, `IRequestProfilingFeature.cs`, and `RequestProfilingApplicationBuilderExtensions.cs` under `src/Presentation.Web/Profiling/`. Implement UseRequestProfiling, incoming-path key, one eligibility/sampling decision, selected-request concurrency, entry timestamps, original route, explicit handle, and one root ID including admission rejection. OnStarting replaces cached profiling IDs or removes them for skipped/disabled capture. Blacklist/sampling skips suppress nested capture; disabled Requests leaves manual operations available. Depends on: TASK-031. | [x] | 2026-10-08 |
| TASK-033 | Add `src/Presentation.Web/Profiling/Requests/RequestProfilingExceptionObserverMiddleware.cs` and UseRequestProfilingExceptionObserver. Enforce/document outer profiler, host error handler, inner exception observer, routing, CORS/auth, compression/cache/endpoints order. Capture original exception/route before re-execution; preserve the feature across replacement DI scopes; expose safe ReportException for consuming filters. Normal 4xx is Completed; observed exceptions and final 5xx fail; transport abort wins while preserving failure evidence. Restore ambient context after downstream unwind; completion callbacks use explicit handles. Depends on: TASK-032. | [x] | 2026-10-08 |
| TASK-034 | Add `src/Presentation.Web/Profiling/Requests/ProfilingResponseBodyFeature.cs` and `ProfilingRequestBodyObserver.cs`. Cover Stream, BodyWriter, send-file, flush, compression and normal request reads without buffering or double counting. Default request observation off. Store declared/observed lengths separately and report partial/unsupported paths honestly. Coordinate OnCompleted/abort/fallback/expiry cleanup once without disposing the live transport; upgraded/streaming connections continue after recording expires. Depends on: TASK-033. | [x] | 2026-10-08 |
| TASK-035 | Add `RequestProfilingMiddlewareTests.cs`, `RequestProfilingSamplingTests.cs`, and `RequestProfilingInjectionTests.cs` under `tests/Presentation.UnitTests/Web/Profiling/`. Use a real test host with handlers, cache, compression, static/auth short circuits, unmatched routes, canceled transport, and controller/minimal API DI. Verify exact header/record IDs, handled 200/4xx exception evidence, no response mutation, optional injection, and no duplicate capture during re-execution. Depends on: TASK-034. | [x] | 2026-10-08 |

### Implementation Phase 8

- GOAL-008: Connect Jobs, Pipelines, and Orchestrations through optional existing behavior boundaries.
- Dependencies: GOAL-006.
- Completion criteria: Each integration runs unchanged without AddProfiling and records correct owned/joined work when enabled, with one next-delegate invocation.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-036 | Add `src/Application.Jobs/Behaviors/JobProfilingBehavior.cs` implementing `IJobBehavior.HandleAsync` and test it in `tests/Application.UnitTests/JobScheduling/JobProfilingBehaviorTests.cs`. Use existing `WithBehavior<TBehavior>()` registration, optional IOperationProfiler, stable job-definition keys, safe trigger/attempt metadata, and result/timeout/cancellation classification. Join an active operation or own one at an independent job boundary; record the actual behavior boundary rather than inventing retry attempts. Depends on: TASK-030. | [x] | 2026-10-08 |
| TASK-037 | Add `src/Common.Utilities/Pipeline/Behaviors/PipelineProfilingBehavior.cs` implementing `IPipelineBehavior<PipelineContextBase>.ExecuteAsync` and ExecuteStepAsync. Register with existing AddBehavior. Root key is `pipeline:<definition-name>`; child key is `step:<step-name>`. Preserve ValueTask, carried Result and control outcomes; repeated attempts aggregate, never-run steps add no sample, and delays outside step delegates stay in outer time. Add `tests/Common.UnitTests/Utilities/Pipeline/PipelineProfilingBehaviorTests.cs`. Depends on: TASK-030. | [x] | 2026-10-08 |
| TASK-038 | Add `src/Application.Orchestrations/Behaviors/OrchestrationProfilingBehavior.cs` and `Execution/OrchestrationProfilingExecutionScope.cs`; integrate the companion scope in `Execution/InMemoryOrchestrationExecutor.cs`. Wrap one bounded execution slice, closing on completion/failure/cancel/wait/pause; regular/signal/compensation activity attempts contribute nested segments keyed by state/name/kind. Restore context in finally, retain only safe IDs/metadata, and start a new correlated scope on resume. Depends on: TASK-030. | [x] | 2026-10-08 |
| TASK-039 | Update `src/Application.Orchestrations/ServiceCollectionExtensions.cs` to enable the behavior and companion adapter together through a profiling-specific WithProfilingBehavior extension on the existing builder. Do not install profiling services from any feature. Add `tests/Application.UnitTests/Orchestrations/OrchestrationProfilingBehaviorTests.cs`; cover omitted profiler, failed results, synchronous/asynchronous exceptions, wait/resume, retries/compensation, concurrency, explicit suppression, and exactly-once execution across all three integrations. Depends on: TASK-036, TASK-037, TASK-038. | [x] | 2026-10-08 |

### Implementation Phase 9

- GOAL-009: Implement bounded operation analysis, freshness, and Runtime correlation services.
- Dependencies: GOAL-005, GOAL-006, GOAL-008.
- Completion criteria: Refined JSON storage passes all actual engines, instrumentation fault tests preserve business execution and worker/host survival, and query tests return matching results with both providers, enforce limits, and never derive invocation percentiles or cluster counters from unavailable data.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-056 | Replace profiling-specific DbSet properties in `src/Infrastructure.EntityFramework/Profiling/IProfilingDbContext.cs` with inherited `Set<TEntity>()`; update every profiling EF consumer/context. Remove `OperationProfilingMeasurementEntity`, its navigation, mapping, and append projections. Preserve complete bounded root/segment measurements in `RecordJson`/`SummaryJson` and indexed relational segment/dimension filters. Update canonical spec, provider documentation, migration/model checks and actual SQLite/SQL Server/PostgreSQL roundtrip/atomic-delete tests. Depends on: TASK-025, TASK-039. | [x] | 2026-10-08 |
| TASK-057 | Audit `Operations/`, HTTP middleware/features and participating behaviors for observation-owned exceptions outside guards. Contain scope-status, clock, worker-timer/initialization/warning/shutdown and response callback faults; discard inconsistent capture and release capacity immediately. Preserve exact-once business delegates and original results/exceptions/cancellation. Add fault-injection tests in Common, Application and Presentation suites, and document the recoverable-vs-process-fatal guarantee. Depends on: TASK-039. | [x] | 2026-10-08 |
| TASK-040 | Add `src/Common.Utilities/Profiling/Query/OperationProfilingQueryService.cs` and `OperationProfilingQueryValidator.cs`. Implement Slow/Recent/By count selection, full retained group counts, supported typed predicates, same-summary matching, exact lookup, and five-minute boundary paging over provider queries. Use one five-second/four-concurrent admission per view build including subordinate work; no waiting queue. Exact lookup ignores list-window/outcome filters but retains provider scope. Depends on: TASK-024, TASK-029, TASK-056, TASK-057. | [x] | 2026-10-08 |
| TASK-041 | Add `src/Common.Utilities/Profiling/Query/OperationProfilingAnalysis.cs` for bounded 10,000-root selections, median midpoint, nearest-rank p95, weighted segment invocation means, per-operation segment distributions, outcomes, and compatible measurement reductions. Retain sample counts, mixed dimensions, unavailable data, and sampling-policy labels. Never average percentiles, fabricate invocation-level percentiles, or extrapolate sampled totals. Depends on: TASK-040. | [x] | 2026-10-08 |
| TASK-042 | Add `src/Common.Utilities/Profiling/Query/OperationRuntimeCorrelationService.cs`. Match process identity and observed interval overlap, bound overlays to 2,000 snapshots, label surrounding samples/gaps and interval metrics, support reverse overlap navigation, and reject false correlations from restarts/imported identities. Do not use session-at-start hints as ownership or attribute process totals to an operation. Depends on: TASK-040, TASK-007. | [x] | 2026-10-08 |
| TASK-043 | Add `tests/Common.UnitTests/Utilities/Profiling/OperationProfilingQueryTests.cs` and `OperationRuntimeCorrelationTests.cs`. Cover concurrent flush, delayed completion-time inserts, deletion revision changes, expiry, UTC boundaries, canonical tie sorting, same-name processes, incomplete records, reduced sampling, local versus shared health, and limit/Busy/timeout results. Extend EF query tests with the same expected data sets and representative query-plan/roundtrip evidence. Depends on: TASK-041, TASK-042. | [x] | 2026-10-08 |

### Implementation Phase 10

- GOAL-010: Build authorized dashboard pages and internal endpoints on the shared query services.
- Dependencies: GOAL-007, GOAL-009.
- Completion criteria: Runtime/Operations/Requests initial rendering, refresh, detail, and JSON agree on data/filters/boundaries; refresh causes no flush or direct queue access.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-044 | Update `src/Presentation.Web/Profiling/Dashboard/DashboardPageProvider.cs` and `DashboardEndpoints.cs`. Move Runtime routes under `/profiling/runtime`, add `/operations` and `/requests` lists/details/content and the internal `/operations/api` routes from the spec. Select the landing view by enabled capture while preserving retained history. Resolve disabled Runtime controls optionally; Requests adapters fix kind to HttpRequest. Apply dashboard enablement, authorization, cancellation, and route-prefix configuration everywhere. Add no external-client or Operations clearing HTTP API. Depends on: TASK-035, TASK-043. | [x] | 2026-10-08 |
| TASK-045 | Add `Dashboard/Pages/Operations/Index.cshtml`, `Content.cshtml`, `Data.cshtml`, and `OperationProfilingViewModelBuilder.cs` under `src/Presentation.Web/Profiling/`; reuse this builder/renderer for Requests with HTTP columns. Implement Slow/Recent/By count, result choices 10/25/50/100, filters/grouping, node identity, outcome/quality indicators, full group counts, and UTC labels. Use DI query calls on both initial render and content requests, never HTTP loopback or EF from Razor. Depends on: TASK-044. | [x] | 2026-10-08 |
| TASK-046 | Add `src/Presentation.Web/Profiling/Dashboard/Pages/Operations/Detail.cshtml` and `DetailContent.cshtml`. Render aggregated segment tree/table, weighted statistics, wall-time bars with Parallel/Outside/partial states, keyboard-accessible time tooltips, and Runtime charts/coverage. Add both navigation directions while preserving node/time filters. No occurrence trace or continuous aggregated-segment timeline is rendered. Depends on: TASK-045. | [x] | 2026-10-08 |
| TASK-047 | Extend the existing refresh integration in `src/Presentation.Web/Profiling/Dashboard/Pages/Index.cshtml` and the new Operations index through `window.bdkDashboard.createRefresher`. Keep one in-flight refresh, pause hidden tabs, cancel obsolete filters, preserve selection/expansion/scroll, and retain visibly stale content after errors. Show last refresh versus last flush and unavailable exact-ID lookup honestly. Extend `tests/Presentation.UnitTests/Web/Profiling/ProfilingDashboardEndpointsTests.cs` for authorization, internal DTO consistency, landing/history behavior, limits, and no persistence triggered by reads. Depends on: TASK-046. | [x] | 2026-10-08 |

### Implementation Phase 11

- GOAL-011: Complete host examples, documentation, and executable public API examples.
- Dependencies: GOAL-008, GOAL-010.
- Completion criteria: Documented examples compile; the reference host demonstrates the final setup and all docs use Segment terminology and the renamed canonical spec.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-048 | Update `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Program.cs` to explicit Runtime/Operations/Requests configuration with dashboard/health/Swagger exclusions and supported middleware ordering. Keep its in-memory provider choice and existing authorization. In `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Modules/Core/Endpoints/WeatherEndpoints.cs`, enrich the compare endpoint with key `weather:compare`, numeric `cityCount`, and a `Query` segment around its existing requester call. In `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Modules/Core/Jobs/WeatherProfilingStressJob.cs`, add optional operation injection: join an active operation or own `weather:profiling-stress`, then record Cpu, Allocate, and Retain segments around existing workload boundaries while preserving explicit Runtime measurement behavior. Update `examples/WeatherFiesta/WeatherFiesta.UnitTests/Modules/Core/Application/Jobs/WeatherProfilingStressJobTests.cs` for absent/enabled operation profiling. Keep business results and cancellation unchanged; no shared SystemEndpoints or application schema change is required. Depends on: TASK-039, TASK-047. | [x] | 2026-10-08 |
| TASK-049 | Update `docs/features-profiling.md`, `docs/features-jobs.md`, `docs/features-pipelines.md`, and `docs/features-orchestrations.md` with final setup, injection, segment aggregation, sampling, provider configuration, clear semantics, health/limits, and dashboard behavior. Document app-owned EF migration/ConfigureProfiling and the version-2 archive break. Update reference links to `docs/specs/spec-profiling-runtime-and-requests.md`; do not rewrite completed historical plans as implementation evidence for this work. Depends on: TASK-048. | [x] | 2026-10-08 |
| TASK-050 | Add `tests/Presentation.UnitTests/Web/Profiling/ProfilingPublicApiExamplesTests.cs` to compile/use minimal API/controller injection, service/Blazor scopes, fluent provider alternatives, optional behavior construction, and enum/Result examples. Verify public XML examples and final names across profiling-specific call sites. Keep legacy PhaseMarker names only where documenting old-to-new migration inputs. Depends on: TASK-049. | [x] | 2026-10-08 |

### Implementation Phase 12

- GOAL-012: Produce correctness and overhead evidence and close every requirement.
- Dependencies: GOAL-011.
- Completion criteria: Every requirement maps to passing evidence; all required engines, middleware paths, behaviors, exports, and load scenarios are validated; no placeholder implementation or compatibility alias remains.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-051 | Run the section 6 focused suites and required SQL Server/PostgreSQL/SQLite integration cases sequentially. Extend race tests with two independent writer hosts/service providers, clock skew, crash-before/after-seal, retained/new work, lease expiry, unknown commits, and concurrent paging. Record commands, counts, engine versions, failures, and repair results in the evidence file. Missing mandatory infrastructure leaves this task incomplete. Depends on: TASK-050. | [x] | 2026-10-08 |
| TASK-052 | Add `tests/Infrastructure.IntegrationTests/EntityFramework/Profiling/ProfilingLoadHarness.cs` and `ProfilingPerformanceEvidenceTests.cs` in the same directory, using existing database fixtures and the project's transitive Presentation.Web reference. Require `BITDEVKIT_PROFILING_PERF=1` to run; otherwise report an explicit skip. Use a local Kestrel host, fixed synthetic data, random seed 1729, and an offered rate of 1,000 operations/second with measured achieved completions, four sequential/nested segments, and at most 4 KiB charged payload, for 60 seconds after 10 seconds warm-up, with three measured repetitions per case. Compare omitted registration with installed behaviors, disabled capture, empty capture, nested/parallel capture, and saturated queues for memory and each EF engine. Include polling, persistence, and filtered/enabled lifecycle-log cases. Save raw measurements and environment metadata under the evidence path; do not add operation export functionality. Depends on: TASK-051. | [x] | 2026-10-09 |
| TASK-053 | Report p50/p95 endpoint latency, throughput, allocation, retained memory, queue age/depth, persisted rate, loss/unknown outcomes, retention removals, and profiler on/off deltas from TASK-052. Prove scheduler ceilings with controlled-time tests independently of database speed. If an engine cannot sustain the baseline, record its measured limit and a tested explicit sampling/configuration adjustment; never claim the nominal 4,096-record ceiling as observed throughput. Document limitations instead of inventing a universal overhead percentage. Depends on: TASK-052. | [x] | 2026-10-09 |
| TASK-054 | Run `.vscode/tasks.json` tasks `Solution - build`, `Solution - tests (unit)`, and `Solution - tests (integration)` sequentially after focused checks. Validate Runtime Broadcast, probes, measurement ownership, restart/reconciliation, pinning, custom metrics, evaluations, archive v2 roundtrips/v1 rejection, and JSON/Perfetto exports. Check source/project dependencies, diagram/link references, and profiling-only rename scope. Depends on: TASK-053. | [x] | 2026-10-09 |
| TASK-055 | Complete the requirement-to-evidence matrix in `plan/pln-feature-profiling-runtime-and-operations-1-evidence.md`. Update this plan's completion cells/dates and status only for verified outcomes; update feature docs to implemented behavior. Run `pwsh -File docs/site/scripts/build-pages.ps1` (or the documented equivalent synchronization plus Docker MkDocs/API build if PowerShell is unavailable), verify generated Profiling guide/navigation/API pages, and fix relevant broken links and documentation warnings. Record commands and outputs; do not publish the site. Keep unresolved tests/performance limitations explicit. No deployment, database reset, commit, or publication is implied by this task. Depends on: TASK-054. | [x] | 2026-10-09 |


### Implementation Phase 13

- GOAL-013: Complete and verify WeatherFiesta application integration after feature acceptance and documentation/site validation.
- Dependencies: GOAL-012, including passing MkDocs validation in TASK-055.
- Completion criteria: The example application demonstrates Runtime, Operations and Requests with unchanged business behavior; HTTP enrichment and job profiling appear in the authorized dashboard; tests and documentation match the application setup.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-058 | Review and finalize the existing Phase 11 wiring in `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Program.cs`, `Modules/Core/Endpoints/WeatherEndpoints.cs` and `Modules/Core/Jobs/WeatherProfilingStressJob.cs` against the accepted API. Keep development enablement, in-memory storage, dashboard authorization and middleware ordering. Demonstrate `weather:compare`/`cityCount`/`Query` request enrichment, and owned or joined `weather:profiling-stress` jobs with Cpu/Allocate/Retain segments and existing Runtime measurement. Reuse these paths; add no unrelated endpoints or schema changes. Depends on: TASK-055. | [x] | 2026-10-09 |
| TASK-059 | Add or extend `examples/WeatherFiesta/WeatherFiesta.IntegrationTests/Modules/Core/Presentation/ProfilingEndpointsTests.cs` using the existing application factory and isolated fixture. Verify profiled request ID lookup, default path and enriched key/dimensions/segments, periodic persistence visibility, blacklist exclusion, enabled Runtime/Operations/Requests views and preserved authorization. Use deterministic fixture data and controlled collaborators instead of external weather calls. Run the WeatherFiesta unit/integration suites sequentially, validate the application dashboard in a browser, and record evidence. Keep `docs/features-profiling.md` application-neutral; record verified example setup in `examples/WeatherFiesta/WeatherFiesta-README.md` and implementation evidence, rerun affected site validation, then mark the complete plan finished only after every task passes. Commit/push this application phase separately. Depends on: TASK-058. | [x] | 2026-10-09 |

### Implementation Phase 14

- GOAL-014: Add optional repository and complete Active Entity profiling boundaries using the existing operation recorder and periodic persistence.
- Dependencies: Phase 13 complete. TASK-060 and TASK-061 can execute independently; final validation depends on both.
- Completion criteria: all repository overloads forward arguments unchanged; complete entity operations close capture on early failure, exceptions and cancellation; existing and new tests pass; generic docs remain application-neutral; MkDocs/API generation and relevant links pass.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-060 | Add `src/Domain/Repositories/Behaviors/RepositoryProfilingBehavior.cs` implementing all 25 `IGenericRepository<TEntity>` overloads. Use optional `IOperationProfiler`, join-or-start helpers, stable type/operation keys, bounded scalar metadata and exactly-once forwarding. Do not enumerate results, retain entities or access storage. | [x] | 2026-10-09 |
| TASK-061 | Add `IActiveEntityOperationBehavior<TEntity>` and `ActiveEntityProfilingBehavior<TEntity>` under `src/Domain/ActiveEntity/Behaviors/`. Wrap complete actions in `ActiveEntityContextScope`, retaining its original two-argument overload and existing hook behavior. Forward cancellation at all 61 cancellation-aware entry points in the Count, Exists, Find, Projection, Transactions and Write partials; name the two custom-context boundaries through the new overload with the default token. Add idempotent `AddProfilingBehavior` to `ActiveEntityConfiguratorExtensions.cs`. Close scopes on failed `IResult`, early returns, exceptions and cancellation without mutable behavior stacks. | [x] | 2026-10-09 |
| TASK-062 | Add `tests/Domain.UnitTests/Domain/Profiling/DomainProfilingBehaviorTests.cs`. Cover omitted DI activation, token and result identity, independent roots, repeated and nested parallel segments, no sequence enumeration, before/provider/after failures, synchronous and asynchronous exceptions, matching caller cancellation, suppression and injected profiler faults. Run focused checks followed by the complete Domain unit suite and existing Active Entity integration suites. Depends on: TASK-060, TASK-061. | [x] | 2026-10-09 |
| TASK-063 | Enable `AddProfilingBehavior()` for all seven entity registrations in `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Modules/Core/CoreModuleExtensions.cs`. Extend `ProfilingEndpointsTests.cs` to verify entity segments beneath Query without assuming a single segment. Update the application README, `docs/features-profiling.md`, both Domain feature guides and the final-state spec with registration examples and precise timing/outcome boundaries. Run application integration checks. Depends on: TASK-060, TASK-061. | [x] | 2026-10-09 |
| TASK-064 | Run the solution build and complete MkDocs/API pipeline sequentially with tests, check generated profiling and Domain guide/API links, and record commands, counts and remaining limitations in the implementation evidence. Mark this phase and plan complete only after successful validation, then commit/push the extension on the existing feature branch. Preserve the user's separate `.gitignore` edit. Depends on: TASK-062, TASK-063. | [x] | 2026-10-09 |

## 3. Alternatives

- **ALT-001**: Reuse Runtime's measurement service for Operations. Rejected because it owns Runtime sessions and durable interval writes, violating execution-independent nonblocking operation capture.
- **ALT-002**: Make HTTP requests the root abstraction. Rejected because services, Blazor interactions, jobs, pipelines, and orchestration slices require the same independent recorder.
- **ALT-003**: Persist every segment invocation. Rejected because loops/parallel work require bounded aggregates rather than an execution trace.
- **ALT-004**: Write directly to memory while batching only EF. Rejected because both providers must share visibility, loss, shutdown, and clear semantics.
- **ALT-005**: Clear with timestamps or a local lock alone. Rejected because delayed remote writes, clock skew, and uncertain commits can restore removed history.
- **ALT-006**: Keep legacy public names or import version-1 archives implicitly. Rejected because this unused feature adopts the final API/schema and explicit archive version 2.
- **ALT-007**: Add third-party telemetry or mandatory profiling dependencies to behaviors. Rejected by scope and optional-registration requirements.
- **ALT-008**: Load dashboard data by browser-only queries or HTTP loopback for initial rendering. Rejected in favor of shared DI query services and existing RazorSlices refresh infrastructure.

## 4. Dependencies

- **DEP-001**: Canonical spec `docs/specs/spec-profiling-runtime-and-requests.md`, including sections 2.4, 8.7, and 10. It supersedes earlier filename/Phase terminology in saved context.
- **DEP-002**: Existing `src/Common.Abstractions/IResult.cs` and `src/Common.Results/`. Public abstraction signatures use IResult; factories use concrete Result in implementation assemblies.
- **DEP-003**: Existing Common.Utilities Runtime Profiling and Broadcast services. Broadcast remains a Runtime dependency only; its general feature APIs/transports are not redesigned.
- **DEP-004**: `IPipelineBehavior<TContext>` in `src/Common.Utilities/Pipeline/Behaviors/IPipelineBehavior.cs`, `IJobBehavior` in `src/Common.Abstractions/Jobs/Behaviors/IJobBehavior.cs`, and `IOrchestrationBehavior` in `src/Application.Orchestrations/Behaviors/IOrchestrationBehavior.cs`.
- **DEP-005**: Existing EF packages, independent DbContext scopes, and SQL engine test fixtures under `tests/Infrastructure.IntegrationTests/EntityFramework/Jobs/`. Use their configured test databases, never production connections.
- **DEP-006**: Dashboard authorization, RazorSlices, route grouping, and `window.bdkDashboard.createRefresher` already used in `src/Presentation.Web/Profiling/Dashboard/Pages/Index.cshtml`.
- **DEP-007**: Existing .NET 10 SDK and `.vscode/tasks.json` build/test commands. No background implementation or test run is scheduled by this document.
- **DEP-008**: Phase graph: GOAL-001 → GOAL-002; GOAL-002 → GOAL-003 and GOAL-004; GOAL-004 → GOAL-005; GOAL-003 plus GOAL-005 → GOAL-006; GOAL-006 → GOAL-007, GOAL-008, and GOAL-009; GOAL-007 plus GOAL-009 → GOAL-010; GOAL-008 plus GOAL-010 → GOAL-011 → GOAL-012 → GOAL-013. Shared-file edits remain serialized.

## 5. Files

Paths labeled new are planned targets, not claims that files exist. Directory entries identify bounded groups already enumerated by the implementation tasks.

| Identifier | Existing source or new target | Action |
| --- | --- | --- |
| FILE-001 | `src/Common.Abstractions/Profiling/` | New public contracts, pure records, typed metadata, storage/query DTOs. |
| FILE-002 | `src/Common.Utilities/Profiling/ProfilingAbstractions.cs`, `ProfilingModels.cs`, `ProfilingErrors.cs` | Extract contracts/records, retire duplicate declarations, retain implementation-specific error factories. |
| FILE-003 | `src/Common.Utilities/Profiling/ProfilingOptions.cs`, `ProfilingRegistration.cs`, `ProfilingServiceCollectionExtensions.cs` | Final shared/subfeature options, fluent composition, lifetimes and workers. |
| FILE-004 | `src/Common.Utilities/Profiling/Control/`, `Runtime/`, `Scopes/` | Runtime naming, independent identity attachment, preserved control/measurement semantics. |
| FILE-005 | `src/Common.Utilities/Profiling/Archive/`, `Export/`, `Evaluation/`, `Metrics/`, `Query/` | Runtime serialization/version mapping plus new bounded Operation query/correlation services. |
| FILE-006 | `src/Common.Utilities/Profiling/Operations/` | New recorder, scopes, accumulators, metadata codec, logging, expiry, queue, writer and health. |
| FILE-007 | `src/Common.Utilities/Profiling/InMemoryProfilingStore.cs`, new `Storage/` beneath Profiling | Extract Runtime component; add shared memory provider, operation queries, lease/clear coordination and maintenance. |
| FILE-008 | `src/Infrastructure.EntityFramework/Profiling/` | Final EF provider/facets, context, mappings, entities, transactional coordination and queries. |
| FILE-009 | `src/Presentation.Web/Profiling/Requests/` | New HTTP registration, matcher, samplers, middleware, feature and body observers. |
| FILE-010 | `src/Presentation.Web/Profiling/Dashboard/`, `Models/`, `ConsoleCommands/` | Final Runtime routes/commands and Operations/Requests builders, pages and internal endpoints. |
| FILE-011 | `src/Application.Jobs/Behaviors/JobProfilingBehavior.cs` | New optional job behavior using existing registration. |
| FILE-012 | `src/Common.Utilities/Pipeline/Behaviors/PipelineProfilingBehavior.cs` | New optional pipeline/step behavior. |
| FILE-013 | `src/Application.Orchestrations/Behaviors/OrchestrationProfilingBehavior.cs`, `Execution/OrchestrationProfilingExecutionScope.cs`, `Execution/InMemoryOrchestrationExecutor.cs`, `ServiceCollectionExtensions.cs` | Activity behavior plus bounded outer execution scope and paired opt-in registration. |
| FILE-014 | `tests/Common.UnitTests/Utilities/Profiling/`, `tests/Common.UnitTests/Utilities/Pipeline/` | Runtime regression, core recording, provider/writer/query/conformance, pipeline integration. |
| FILE-015 | `tests/Infrastructure.UnitTests/EntityFramework/Profiling/`, `tests/Infrastructure.IntegrationTests/EntityFramework/Profiling/` | EF model/contracts, independent writers, all database engines, and opt-in HTTP load evidence. |
| FILE-016 | `tests/Presentation.UnitTests/Web/Profiling/`, `tests/Presentation.UnitTests/ConsoleCommands/ProfilingConsoleCommandTests.cs` | HTTP, DI, dashboard, public examples and Runtime commands. |
| FILE-017 | `tests/Application.UnitTests/JobScheduling/`, `tests/Application.UnitTests/Orchestrations/` | Optional behavior construction and execution preservation. |
| FILE-018 | `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Program.cs`, `Modules/Core/Endpoints/WeatherEndpoints.cs`, `Modules/Core/Jobs/WeatherProfilingStressJob.cs` beneath that server; `examples/WeatherFiesta/WeatherFiesta.UnitTests/Modules/Core/Application/Jobs/WeatherProfilingStressJobTests.cs` | Reference registration/middleware, HTTP enrichment, non-HTTP workload segments, and example regression tests. |
| FILE-019 | `docs/features-profiling.md`, `docs/features-jobs.md`, `docs/features-pipelines.md`, `docs/features-orchestrations.md`, `docs/features-domain-repositories.md`, `docs/features-domain-activeentity.md` | Final API usage and integration documentation. |
| FILE-020 | `plan/pln-feature-profiling-runtime-and-operations-1-evidence.md` | New implementation evidence, rename inventory, requirements coverage, commands and performance results. |
| FILE-021 | `mkdocs.yml`, `docs/site/scripts/sync-docs.ps1`, `docs/site/scripts/build-pages.ps1`, `docs/site/reference/features-profiling.md` | Validate generated documentation and navigation; fix profiling documentation generation if required. |
| FILE-022 | `examples/WeatherFiesta/WeatherFiesta.IntegrationTests/Modules/Core/Presentation/ProfilingEndpointsTests.cs` | New complete-application profiling integration checks. |
| FILE-023 | `examples/WeatherFiesta/WeatherFiesta-README.md` | Application-specific profiling setup and verification workflow; shared profiling documentation remains application-neutral. |
| FILE-024 | `src/Domain/Repositories/Behaviors/RepositoryProfilingBehavior.cs`, `src/Domain/ActiveEntity/Behaviors/ActiveEntityProfilingBehavior.cs`, `IActiveEntityOperationBehavior.cs`, context scope/configurator and operation partials | Optional repository and complete entity boundaries. |
| FILE-025 | `tests/Domain.UnitTests/Domain/Profiling/DomainProfilingBehaviorTests.cs`, `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Modules/Core/CoreModuleExtensions.cs` | Behavior contract proof and example registration. |


## 6. Testing

| Test | Required evidence | Owning tasks |
| --- | --- | --- |
| TEST-001 | Fluent ordering/repetition, dependency validation, absent/disabled services, provider selection, scopes/lifetimes, operation-only host. | TASK-008, TASK-030, TASK-035, TASK-050 |
| TEST-002 | Root IDs, UTC/monotonic timing, immutable records, cached process identity, enrichment and no request-scope dependency. | TASK-009, TASK-010 |
| TEST-003 | Concurrent async branches/Blazor actions, context restoration/suppression, faults, expiry/shutdown, business delegate once. | TASK-010, TASK-014 |
| TEST-004 | Repeated/nested/parallel arithmetic, child union subtraction, wall-time bars, typed dimensions/reducers, limits and rejection. | TASK-011, TASK-012, TASK-013 |
| TEST-005 | Queue accounting, lease-qualified fixed watermark, batch/time/count budgets, retries, partial outcomes, idle ticks, no overlapping calls. | TASK-026, TASK-027 |
| TEST-006 | Memory provider idempotency, lease/settlement, bounded clear/retention, recovery and stable query boundaries. | TASK-016–TASK-020 |
| TEST-007 | Trace events emitted at most once, no log-only allocations when filtered, omitted/suppressed silence, logger-fault isolation. | TASK-015 |
| TEST-008 | Same provider invariants on SQLite, SQL Server and PostgreSQL; binary/typed comparisons, context isolation, root graph atomicity. | TASK-021–TASK-025 |
| TEST-009 | Multiple writers, skew, lease expiry before/after acknowledgement, new registrations, lost commit/ack, clear/retention races, epoch reset. | TASK-020, TASK-025, TASK-051 |
| TEST-010 | Runtime Broadcast/control/collector/measurement/evaluation regressions; markers and segments preserve distinct meanings. | TASK-006–TASK-008, TASK-054 |
| TEST-011 | Path/prefix/wildcard rules, all/probability/rate sampling, no endpoint opt-in, ID/header rules, suppression and sampler failures. | TASK-031, TASK-032, TASK-035 |
| TEST-012 | Full middleware order, handled/re-executed errors, final status, stream/pipe/send-file/compression/cache, abort and long-lived expiry. | TASK-033–TASK-035 |
| TEST-013 | Full retained counts, stable page boundaries, typed predicates, weighted statistics, exact limits, timeout/Busy and cancellation. | TASK-019, TASK-024, TASK-040, TASK-041, TASK-043 |
| TEST-014 | Authorized initial/content/JSON consistency, Runtime/Operations/Requests landing and filters, refresh limits, stale UI, accessible bars. | TASK-044–TASK-047 |
| TEST-015 | Node/time correlation, surrounding samples/gaps, imported/restarted identities, overlap navigation, independent retention. | TASK-042, TASK-043 |
| TEST-016 | Jobs/pipeline/orchestration optional injection, failed results, retries/skip/wait/resume, next delegate once and unchanged behavior. | TASK-036–TASK-039 |
| TEST-017 | Version-2 archive/import remapping, reject version 1 before mutation, final JSON/Perfetto and console behavior. | TASK-007, TASK-054 |
| TEST-018 | Compile/documentation examples and all requirement IDs covered by reproducible test evidence. | TASK-050, TASK-055 |
| TEST-019 | On/off/saturation/polling/logging overhead for memory and EF; actual sustainable throughput and bounded memory/loss. | TASK-052, TASK-053 |
| TEST-020 | MkDocs guide/navigation and API output build; no relevant broken links. | TASK-055, TASK-059 |
| TEST-021 | Complete WeatherFiesta HTTP/job/dashboard integration and authorization with deterministic data. | TASK-058, TASK-059 |
| TEST-022 | Optional repository/entity DI, full boundaries and early failure cleanup, unchanged results/tokens/exceptions, lazy sequences, nested concurrent capture, suppression and observer faults; Domain and Active Entity regression suites. | TASK-060, TASK-061, TASK-062, TASK-063, TASK-064 |

Run from the repository root. The workspace tasks are the preferred full checks. These commands are the focused equivalents for implementation work; execute them sequentially and record failures rather than suppressing them:

```bash
dotnet build bITdevKit.slnx --nologo
dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --no-build --filter 'FullyQualifiedName~Profiling'
dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --no-build --filter 'FullyQualifiedName~Profiling'
dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --no-build --filter 'FullyQualifiedName~Profiling'
dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-build --filter 'FullyQualifiedName~Profiling'
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-build --filter 'FullyQualifiedName~Profiling'
```

The opt-in performance harness runs only when `BITDEVKIT_PROFILING_PERF=1` is present. Execute `BITDEVKIT_PROFILING_PERF=1 dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --no-build --filter 'FullyQualifiedName~ProfilingPerformanceEvidenceTests'` in TASK-052 with its workload configuration recorded. A skip does not satisfy TEST-019. Existing runtime tests retain their coverage after renaming; new tests are not a replacement for them.


## 7. Risks & Assumptions

- **RISK-001**: Assembly extraction can create a cycle through concrete Result types, KeyGenerator, or Broadcast records. TASK-003 keeps interfaces/value data dependency-free and moves factories/adapters outward; verify project references and public parameter types before continuing.
- **RISK-002**: Ambient mutable stacks leak across parallel branches or long-lived Blazor scopes. Test sibling/child/root ownership and closed/suppressed boundaries with controlled interleavings, not only sequential using blocks.
- **RISK-003**: A database acknowledgement can be lost after commit. Preserve envelope identity, idempotency, atomic settlement, and retention protection; distinguish unknown persistence from confirmed diagnostic loss.
- **RISK-004**: Clear/retention/paging coordination can pass memory tests yet fail on a shared database. Two independent providers/writers and real engine tests are mandatory. A process-local lock is insufficient for EF.
- **RISK-005**: Profiling can distort the measured workload despite asynchronous writes. TASK-052 measures recorder, logging, polling, and provider cost; hard bounds and a measured lower capture rate are preferable to unbounded buffers or unsupported performance claims.
- **RISK-006**: Middleware placement or body-feature replacement can lose exception/byte evidence. Test real handler/cache/compression/transport integration and preserve unavailable/partial quality instead of substituting Content-Length or zero.
- **RISK-007**: Database limits/collation/precision can merge groups or produce client-side scans. Persist canonical binary comparison representations and typed values; exercise translations/plans with each engine and large numeric dimensions.
- **RISK-008**: Breaking Runtime names/marker models/archive formats can damage retained fixtures or examples. Use the explicit inventory, final migrations in isolated fixtures, version rejection before mutation, and Runtime regression suites. Never reset unrelated host data.
- **ASSUMPTION-001**: The feature is unused as stated in the spec, so final API/schema changes need no legacy aliases or version-1 import compatibility. Implementation still never silently deletes an old schema at startup.
- **ASSUMPTION-002**: Application hosts own production EF contexts/migrations and security policy. This plan validates mappings and fixture migrations; it does not authorize production deployment or database alteration.
- **ASSUMPTION-003**: Existing feature behavior APIs and the orchestration executor remain available at implementation time. If their signatures change after the inspected revision, update the affected task paths/signatures while preserving its contract and evidence; do not replace the feature pipeline.
- **ASSUMPTION-004**: New files/classes named in this plan are target implementation locations. Existing entry points are repository-verified. Public method return types follow the corrected IResult abstraction boundary.

## 8. Related Specifications / Further Reading

- [Canonical Runtime and Operation Profiling specification](../docs/specs/spec-profiling-runtime-and-requests.md)
- [Current Profiling feature documentation](../docs/features-profiling.md)
- [Runtime snapshot dashboard specification](../docs/specs/spec-performance-snapshot-dashboard.md)
- [Orchestration integrations specification](../docs/specs/spec-application-orchestration-integrations.md)
- [Existing Runtime portability plan](pln-feature-profiling-session-portability-1.md)
- [Clean/Onion architecture](../docs/adr/0001-clean-onion-architecture.md)
- [Dependency injection lifetimes](../docs/adr/0018-dependency-injection-service-lifetimes.md)
- [Create implementation plan skill](../.agents/skills/create-implementation-plan/SKILL.md)
