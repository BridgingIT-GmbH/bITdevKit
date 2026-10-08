# Profiling implementation evidence

## Execution baseline

- Repository revision: `20111ecfe`.
- Plan: [Runtime and Operation Profiling implementation](pln-feature-profiling-runtime-and-operations-1.md).
- Specification: [Runtime and Operation Profiling](../docs/specs/spec-profiling-runtime-and-requests.md).
- SDK: .NET 10.0.401. Runtime: .NET 10.0.12.
- Baseline solution build succeeded with zero warnings and zero errors.
- Original database baseline failed because the agent lacked Docker socket access. Docker 29.7.2 is now accessible after the session obtained docker group membership. The repeated database suite passed all 17 tests with no failures or skips.
- Phase 1 recorded the unmodified source baseline before implementation changes.

## Baseline checks

| Check | Passed | Failed | Exit code | Evidence |
| --- | --- | --- | --- | --- |
| Solution build | Build succeeded | 0 | 0 | `/tmp/bitdevkit-profiling-baseline-build.log` |
| Common profiling unit tests | 129 | 0 | 0 | `/tmp/bitdevkit-profiling-baseline-Common.UnitTests.log` |
| Infrastructure profiling unit tests | 18 | 0 | 0 | `/tmp/bitdevkit-profiling-baseline-Infrastructure.UnitTests.log` |
| Infrastructure profiling integration tests before access repair | 7 | 10 fixture failures | 1 | `/tmp/bitdevkit-profiling-baseline-Infrastructure.IntegrationTests.log` |
| Infrastructure profiling integration tests after access repair | 17 | 0 | 0 | `/tmp/bitdevkit-profiling-baseline-Infrastructure.IntegrationTests-DockerRestored.log` |
| Presentation profiling unit tests | 48 | 0 | 0 | `/tmp/bitdevkit-profiling-baseline-Presentation.UnitTests.log` |
| Application profiling filter | No matching tests | 0 | 0 | New adapter tests remain required. |

Commands ran sequentially. The filtered test command for each project was `dotnet test tests/<project>/<project>.csproj --no-build --filter FullyQualifiedName~Profiling`. TRX files are in `/tmp/bitdevkit-profiling-baseline-results`. The application filter returning no tests is not evidence that future job or orchestration adapters work.

## Rename inventory

The following map covers profiling declarations discovered with `rg` in the current source. Marker rows merge into one scoped marker model instead of generating compatibility aliases. Runtime interval segments remain `ProfilingSegment`. Node identities stay shared. Internal Runtime storage components are composed by the final provider facade.

| Existing declaration | Final declaration |
| --- | --- |
| `EntityFrameworkProfilingStore` | `EntityFrameworkRuntimeProfilingStore` |
| `IProfilingArchiveService` | `IRuntimeProfilingArchiveService` |
| `IProfilingBroadcast` | `IRuntimeProfilingBroadcast` |
| `IProfilingBroadcastService` | `IRuntimeProfilingBroadcastService` |
| `IProfilingCollector` | `IRuntimeProfilingCollector` |
| `IProfilingContext` | `IProfilingDbContext` |
| `IProfilingControlService` | `IRuntimeProfilingControlService` |
| `IProfilingEvaluationService` | `IRuntimeProfilingEvaluationService` |
| `IProfilingMeasurementScope` | `IRuntimeProfilingMeasurementScope` |
| `IProfilingMeasurementService` | `IRuntimeProfilingMeasurementService` |
| `IProfilingPerfettoExportService` | `IRuntimeProfilingPerfettoExportService` |
| `IProfilingQueryService` | `IRuntimeProfilingQueryService` |
| `IProfilingRuntimeContextFactory` | `IRuntimeProfilingContextFactory` |
| `IProfilingRuntimeContextSource` | `IRuntimeProfilingContextSource` |
| `IProfilingRuntimeSnapshotSource` | `IRuntimeProfilingSnapshotSource` |
| `IProfilingSnapshotProbe` | `IRuntimeProfilingSnapshotProbe` |
| `IProfilingStore` | `IRuntimeProfilingStore` |
| `IProfilingStressService` | `IRuntimeProfilingStressService` |
| `InMemoryProfilingStore` | `InMemoryRuntimeProfilingStore` |
| `ProfilingActionMarker` | `ProfilingMarker` |
| `ProfilingActionMarkerEntity` | `ProfilingMarkerEntity` |
| `ProfilingActiveSession` | `RuntimeProfilingActiveSession` |
| `ProfilingActiveSessionContext` | `RuntimeProfilingActiveSessionContext` |
| `ProfilingAmbientSegment` | `RuntimeProfilingAmbientSegment` |
| `ProfilingArchive` | `RuntimeProfilingArchive` |
| `ProfilingArchiveFormat` | `RuntimeProfilingArchiveFormat` |
| `ProfilingArchiveImportResult` | `RuntimeProfilingArchiveImportResult` |
| `ProfilingArchiveKind` | `RuntimeProfilingArchiveKind` |
| `ProfilingArchiveMetricObservation` | `RuntimeProfilingArchiveMetricObservation` |
| `ProfilingArchiveSegment` | `RuntimeProfilingArchiveSegment` |
| `ProfilingArchiveService` | `RuntimeProfilingArchiveService` |
| `ProfilingBroadcastExecutionTracker` | `RuntimeProfilingBroadcastExecutionTracker` |
| `ProfilingBroadcastService` | `RuntimeProfilingBroadcastService` |
| `ProfilingBroadcastTargetSnapshot` | `RuntimeProfilingBroadcastTargetSnapshot` |
| `ProfilingCaptureRequest` | `RuntimeProfilingCaptureRequest` |
| `ProfilingCollector` | `RuntimeProfilingCollector` |
| `ProfilingCollectorHostedService` | `RuntimeProfilingCollectorHostedService` |
| `ProfilingControlResult` | `RuntimeProfilingControlResult` |
| `ProfilingControlService` | `RuntimeProfilingControlService` |
| `ProfilingCustomMetricHostedService` | `RuntimeProfilingCustomMetricHostedService` |
| `ProfilingCustomMetricListener` | `RuntimeProfilingCustomMetricListener` |
| `ProfilingDataSufficiency` | `RuntimeProfilingDataSufficiency` |
| `ProfilingEvaluationDataQuality` | `RuntimeProfilingEvaluationDataQuality` |
| `ProfilingEvaluationMode` | `RuntimeProfilingEvaluationMode` |
| `ProfilingEvaluationRequest` | `RuntimeProfilingEvaluationRequest` |
| `ProfilingEvaluationResult` | `RuntimeProfilingEvaluationResult` |
| `ProfilingEvaluationScope` | `RuntimeProfilingEvaluationScope` |
| `ProfilingEvaluator` | `RuntimeProfilingEvaluator` |
| `ProfilingGarbageCollectionBroadcast` | `RuntimeProfilingGarbageCollectionBroadcast` |
| `ProfilingGarbageCollectionBroadcastHandler` | `RuntimeProfilingGarbageCollectionBroadcastHandler` |
| `ProfilingGcObservation` | `RuntimeProfilingGcObservation` |
| `ProfilingGcObservationResult` | `RuntimeProfilingGcObservationResult` |
| `ProfilingGcObservationState` | `RuntimeProfilingGcObservationState` |
| `ProfilingInvalidSessionEntity` | `RuntimeProfilingInvalidSessionEntity` |
| `ProfilingKpi` | `RuntimeProfilingKpi` |
| `ProfilingMeasurementScope` | `RuntimeProfilingMeasurementScope` |
| `ProfilingMeasurementService` | `RuntimeProfilingMeasurementService` |
| `ProfilingMetricObservationEntity` | `RuntimeProfilingMetricObservationEntity` |
| `ProfilingNodeCorrelation` | `RuntimeProfilingNodeCorrelation` |
| `ProfilingNodeEntity` | `RuntimeProfilingNodeEntity` |
| `ProfilingNodeOutcome` | `RuntimeProfilingNodeOutcome` |
| `ProfilingNodeParticipation` | `RuntimeProfilingNodeParticipation` |
| `ProfilingNodeRole` | `RuntimeProfilingNodeRole` |
| `ProfilingNodeSessionData` | `RuntimeProfilingNodeSessionData` |
| `ProfilingParticipationEntity` | `RuntimeProfilingParticipationEntity` |
| `ProfilingParticipationState` | `RuntimeProfilingParticipationState` |
| `ProfilingPerfettoExportService` | `RuntimeProfilingPerfettoExportService` |
| `ProfilingPhaseMarker` | `ProfilingMarker` |
| `ProfilingPhaseMarkerEntity` | `ProfilingMarkerEntity` |
| `ProfilingQueryService` | `RuntimeProfilingQueryService` |
| `ProfilingRuntimeContext` | `RuntimeProfilingContext` |
| `ProfilingRuntimeContextEntity` | `RuntimeProfilingRuntimeContextEntity` |
| `ProfilingRuntimeContextFactory` | `RuntimeProfilingContextFactory` |
| `ProfilingRuntimeContextValues` | `RuntimeProfilingContextValues` |
| `ProfilingRuntimeSample` | `RuntimeProfilingSample` |
| `ProfilingSamplingStatus` | `RuntimeProfilingSamplingStatus` |
| `ProfilingSegmentContext` | `RuntimeProfilingSegmentContext` |
| `ProfilingSegmentEntity` | `RuntimeProfilingSegmentEntity` |
| `ProfilingSegmentTagEntity` | `RuntimeProfilingSegmentTagEntity` |
| `ProfilingSession` | `RuntimeProfilingSession` |
| `ProfilingSessionBroadcast` | `RuntimeProfilingSessionBroadcast` |
| `ProfilingSessionCreateRequest` | `RuntimeProfilingSessionCreateRequest` |
| `ProfilingSessionData` | `RuntimeProfilingSessionData` |
| `ProfilingSessionEntity` | `RuntimeProfilingSessionEntity` |
| `ProfilingSessionFinalizer` | `RuntimeProfilingSessionFinalizer` |
| `ProfilingSessionIdentity` | `RuntimeProfilingSessionIdentity` |
| `ProfilingSessionMetadata` | `RuntimeProfilingSessionMetadata` |
| `ProfilingSessionReference` | `RuntimeProfilingSessionReference` |
| `ProfilingSessionResolution` | `RuntimeProfilingSessionResolution` |
| `ProfilingSessionState` | `RuntimeProfilingSessionState` |
| `ProfilingSessionTagEntity` | `RuntimeProfilingSessionTagEntity` |
| `ProfilingSignal` | `RuntimeProfilingSignal` |
| `ProfilingSignalConfidence` | `RuntimeProfilingSignalConfidence` |
| `ProfilingSignalEvidence` | `RuntimeProfilingSignalEvidence` |
| `ProfilingSignalLabel` | `RuntimeProfilingSignalLabel` |
| `ProfilingSnapshot` | `RuntimeProfilingSnapshot` |
| `ProfilingSnapshotBroadcast` | `RuntimeProfilingSnapshotBroadcast` |
| `ProfilingSnapshotBroadcastHandler` | `RuntimeProfilingSnapshotBroadcastHandler` |
| `ProfilingSnapshotComparison` | `RuntimeProfilingSnapshotComparison` |
| `ProfilingSnapshotEntity` | `RuntimeProfilingSnapshotEntity` |
| `ProfilingSnapshotIdentity` | `RuntimeProfilingSnapshotIdentity` |
| `ProfilingSnapshotMetricDelta` | `RuntimeProfilingSnapshotMetricDelta` |
| `ProfilingSnapshotProbe` | `RuntimeProfilingSnapshotProbe` |
| `ProfilingSnapshotReference` | `RuntimeProfilingSnapshotReference` |
| `ProfilingStartBroadcast` | `RuntimeProfilingStartBroadcast` |
| `ProfilingStartBroadcastHandler` | `RuntimeProfilingStartBroadcastHandler` |
| `ProfilingStartRequest` | `RuntimeProfilingStartRequest` |
| `ProfilingStartupReconciler` | `RuntimeProfilingStartupReconciler` |
| `ProfilingStatus` | `RuntimeProfilingStatus` |
| `ProfilingStopBroadcast` | `RuntimeProfilingStopBroadcast` |
| `ProfilingStopBroadcastHandler` | `RuntimeProfilingStopBroadcastHandler` |
| `ProfilingStressRequest` | `RuntimeProfilingStressRequest` |
| `ProfilingStressResult` | `RuntimeProfilingStressResult` |
| `ProfilingStressService` | `RuntimeProfilingStressService` |
| `SystemProfilingRuntimeContextSource` | `SystemRuntimeProfilingContextSource` |
| `SystemProfilingRuntimeSnapshotSource` | `SystemRuntimeProfilingSnapshotSource` |

### Registration and configuration

- `AddProfiling` becomes shared master/provider configuration. Existing sampling, duration, stop, participation, grace, and Runtime retention settings move into `RuntimeProfilingOptions` through `WithRuntimeProfiling`.
- `WithEntityFrameworkStore<TContext>` changes only on the ProfilingBuilderContext extension to `WithEntityFrameworkProvider<TContext>`. The DoFiesta CoreModule call belongs to FileMonitoring and remains unchanged.
- `ProfilingContext` implementers in Infrastructure unit/integration test fixtures move to `IProfilingDbContext`. Production hosts remain responsible for migrations.
- `ProfilingSessionIdentity.Create`, `ProfilingNodeIdentity.Create`, and `ProfilingSnapshotIdentity.Create` currently call `KeyGenerator.CreateLowercase(8)` in Common.Utilities. Factories move to `ProfilingIdentityFactory` while identity values move to Common.Abstractions.
- Public control, store, and query abstractions return `IResult` interfaces. Concrete result factories and Broadcast-specific contracts stay in Common.Utilities.

### Serialization and presentation

- `PhaseMarkers` and `ActionMarkers` merge into `Markers` with explicit scope and kind. `Segments`, `ParentSegmentId`, and metric segment references remain Runtime interval data.
- Archive format stays `bitdevkit.profiling.archive`; version changes from 1 to 2. Reject version 1 before import writes.
- Existing `AddPhaseMarkerAsync` becomes `AddMarkerAsync`. Node actions create node-scoped markers through the same storage contract.
- Existing profiling console command names gain the `profiling runtime` prefix.
- Existing dashboard `/profiling` actions/content/data/export paths move under `/profiling/runtime`; `/profiling` becomes the capability-aware entry.
- WeatherFiesta Program.cs is the verified AddProfiling host. WeatherEndpoints and WeatherProfilingStressJob are the planned operation-enrichment examples.

### Consumer inventory

The following source, test, example, and documentation files contain Profiling references. Historical completed plans are not rewritten as evidence for this implementation.

- `tests/Presentation.UnitTests/Web/Profiling/ProfilingWorkflowIntegrationTests.cs`
- `tests/Presentation.UnitTests/Web/Profiling/ProfilingDashboardEndpointsTests.cs`
- `tests/Presentation.UnitTests/ConsoleCommands/ProfilingConsoleCommandTests.cs`
- `docs/specs/spec-profiling-runtime-and-requests.md`
- `tests/Infrastructure.UnitTests/EntityFramework/Profiling/EntityFrameworkProfilingModelTests.cs`
- `tests/Infrastructure.UnitTests/EntityFramework/Profiling/ProfilingTestDbContext.cs`
- `tests/Infrastructure.UnitTests/EntityFramework/Profiling/EntityFrameworkProfilingStoreContractTests.cs`
- `examples/WeatherFiesta/WeatherFiesta.UnitTests/Modules/Core/Application/Jobs/WeatherProfilingStressJobTests.cs`
- `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Program.cs`
- `tests/Infrastructure.IntegrationTests/EntityFramework/Profiling/EntityFrameworkSqliteProfilingStoreTests.cs`
- `tests/Infrastructure.IntegrationTests/EntityFramework/Profiling/EntityFrameworkSqlServerProfilingStoreTests.cs`
- `tests/Infrastructure.IntegrationTests/EntityFramework/Profiling/EntityFrameworkProfilingStoreTestsBase.cs`
- `tests/Infrastructure.IntegrationTests/EntityFramework/Profiling/EntityFrameworkProfilingLifecycleTests.cs`
- `tests/Infrastructure.IntegrationTests/EntityFramework/Profiling/EntityFrameworkPostgresProfilingStoreTests.cs`
- `src/Presentation.Web/Profiling/ProfilingServiceCollectionExtensions.cs`
- `src/Presentation.Web/Profiling/Models/ProfilingDashboardModels.cs`
- `src/Presentation.Web/Profiling/Dashboard/DashboardPageProvider.cs`
- `src/Presentation.Web/Profiling/Dashboard/DashboardEndpoints.cs`
- `src/Presentation.Web/Profiling/Dashboard/Pages/Index.cshtml`
- `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Modules/Core/CoreModule.cs`
- `src/Presentation.Web/Profiling/Dashboard/Pages/Data.cshtml`
- `src/Presentation.Web/Profiling/Dashboard/Pages/Content.cshtml`
- `src/Presentation.Web/Profiling/Dashboard/Pages/DashboardProfilingViewModel.cs`
- `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Modules/Core/Jobs/WeatherProfilingStressJob.cs`
- `src/Presentation.Web/Profiling/ConsoleCommands/ProfilingDurationParser.cs`
- `src/Presentation.Web/Profiling/ConsoleCommands/ProfilingControlConsoleCommands.cs`
- `src/Presentation.Web/Profiling/ConsoleCommands/ProfilingConsoleCommandBase.cs`
- `src/Presentation.Web/Profiling/ConsoleCommands/ProfilingArchiveConsoleCommands.cs`
- `src/Presentation.Web/Profiling/ConsoleCommands/ProfilingAnalyzeConsoleCommand.cs`
- `docs/specs/spec-performance-snapshot-dashboard.md`
- `docs/prd/profiling/prd-0000-PROFILING-portable-session-archives.md`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingEvaluatorTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingCustomMetricListenerTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingBroadcastServiceTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingStressServiceTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingStoreContractTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingRuntimeProbeTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingQueryServiceTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingPerfettoExportServiceTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingMeasurementTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingFoundationTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingControlServiceTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingCollectorTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/ProfilingArchiveServiceTests.cs`
- `tests/Common.UnitTests/Utilities/Profiling/InMemoryProfilingStoreTests.cs`
- `docs/features-metrics.md`
- `docs/features-profiling.md`
- `src/Common.Utilities/Profiling/ProfilingServiceCollectionExtensions.cs`
- `src/Common.Utilities/Profiling/Scopes/ProfilingSegmentContext.cs`
- `src/Common.Utilities/Profiling/Scopes/ProfilingMeasurementService.cs`
- `src/Common.Utilities/Profiling/Scopes/ProfilingMeasurementScope.cs`
- `src/Common.Utilities/Profiling/Runtime/ProfilingSnapshotProbe.cs`
- `src/Common.Utilities/Profiling/Runtime/ProfilingStressService.cs`
- `src/Common.Utilities/Profiling/Runtime/ProfilingRuntimeContextFactory.cs`
- `src/Common.Utilities/Profiling/Runtime/ProfilingNodeIdentityProvider.cs`
- `src/Common.Utilities/Profiling/Runtime/ProfilingGcObservationState.cs`
- `src/Common.Utilities/Profiling/Runtime/ProfilingCollectorHostedService.cs`
- `src/Common.Utilities/Profiling/Runtime/ProfilingCollector.cs`
- `src/Common.Utilities/Profiling/Runtime/ProfilingActiveSessionContext.cs`
- `src/Common.Utilities/Profiling/Query/ProfilingQueryService.cs`
- `src/Common.Utilities/Profiling/Query/ProfilingQueryModels.cs`
- `src/Common.Utilities/Profiling/ProfilingRegistration.cs`
- `src/Common.Utilities/Profiling/ProfilingOptions.cs`
- `src/Common.Utilities/Profiling/ProfilingModels.cs`
- `src/Common.Utilities/Profiling/ProfilingErrors.cs`
- `src/Common.Utilities/Profiling/ProfilingAbstractions.cs`
- `src/Common.Utilities/Profiling/Metrics/ProfilingCustomMetricListener.cs`
- `src/Common.Utilities/Profiling/InMemoryProfilingStore.cs`
- `src/Common.Utilities/Profiling/Export/ProfilingPerfettoExportService.cs`
- `src/Common.Utilities/Profiling/Evaluation/ProfilingEvaluationRules.cs`
- `src/Common.Utilities/Profiling/Evaluation/ProfilingEvaluationCalculations.cs`
- `src/Common.Utilities/Profiling/Evaluation/ProfilingEvaluator.cs`
- `src/Common.Utilities/Profiling/Control/ProfilingStartupReconciler.cs`
- `src/Common.Utilities/Profiling/Control/ProfilingSessionFinalizer.cs`
- `src/Common.Utilities/Profiling/Control/ProfilingControlService.cs`
- `src/Common.Utilities/Profiling/Control/ProfilingBroadcastService.cs`
- `src/Common.Utilities/Profiling/Control/ProfilingBroadcastModels.cs`
- `src/Common.Utilities/Profiling/Control/ProfilingBroadcastHandlers.cs`
- `src/Common.Utilities/Profiling/Archive/ProfilingArchiveService.cs`
- `src/Common.Utilities/Profiling/Archive/ProfilingArchiveModels.cs`
- `src/Infrastructure.EntityFramework/Profiling/ServiceCollectionExtensions.cs`
- `src/Infrastructure.EntityFramework/Profiling/ProfilingModelBuilderExtensions.cs`
- `src/Infrastructure.EntityFramework/Profiling/ProfilingEntityMapper.cs`
- `src/Infrastructure.EntityFramework/Profiling/IProfilingContext.cs`
- `src/Infrastructure.EntityFramework/Profiling/EntityFrameworkProfilingStore.cs`
- `src/Infrastructure.EntityFramework/Profiling/Entities/ProfilingSnapshotEntity.cs`
- `src/Infrastructure.EntityFramework/Profiling/Entities/ProfilingSessionEntities.cs`
- `src/Infrastructure.EntityFramework/Profiling/Entities/ProfilingNodeEntities.cs`
- `src/Infrastructure.EntityFramework/Profiling/Entities/ProfilingAnnotationEntities.cs`
- `docs/INDEX.md`

## Task progress

- TASK-001: complete. Solution build and focused checks recorded; restored Docker access verified with all 17 integration tests passing.
- TASK-002: complete. Declaration, registration, serialized field, route, identity-factory, and consumer inventory recorded.
- TASK-003: complete. Pure Runtime contracts/DTOs extracted; concrete Result and identity factories stay in Utilities. Solution build: zero warnings/errors. Focused checks: Common 131, Infrastructure 18, Presentation 48, database integration 17 passing. Logs: `/tmp/bitdevkit-profiling-contract-*`; TRX: `/tmp/bitdevkit-profiling-contract-results`. Assembly dependency and identity generation tests added. Benchmark consumers included in the cutover.
- TASK-004: complete. Generic facade/scopes and immutable value/root/summary/HTTP projection contracts added without web dependencies. Common profiling checks: 139 passing, including eight scalar/path/UTC/assembly tests. Runtime terminal outcomes now use the shared vocabulary; open intervals have no terminal outcome. Evidence: `/tmp/bitdevkit-profiling-operation-contract-tests.log`.
- TASK-005: complete. Provider/runtime/operation facets, writer leases and immutable envelopes, fixed clear acknowledgements, settlement, stable query boundaries, health and typed analysis DTOs defined in Abstractions. Common profiling checks: 140 passing. Backend-independent asynchronous Result contract verified. Evidence: `/tmp/bitdevkit-profiling-storage-contract-tests.log`. Concrete provider conformance remains TASK-020/TASK-025.
- TASK-006: complete. Cached no-I/O identity moved behind the pure common contract. Runtime Broadcast adapter and best-effort background registration reuse the cached GUID/key; remote targets resolve persisted identities instead of inventing a local identity. Runtime Control/Collector/Scopes implementations renamed. Node display/version/actual-start metadata survives EF roundtrip. Checks: Common 141, Presentation 48, Infrastructure 18, real-engine integration 20 passing. Evidence: `/tmp/bitdevkit-profiling-runtime-identity-*`.
- TASK-007 through TASK-055: incomplete.

## Requirement acceptance

REQ-001 through REQ-024 remain unverified for the new implementation. Baseline tests describe existing Runtime behavior only.

### TASK-007 — scoped Runtime markers and version-2 archives

- Runtime query, evaluation, metrics, archives and Perfetto implementations use Runtime-prefixed names.
- One `Markers` collection and one EF owned JSON collection preserve explicit Session/Node scope and kind. Session markers have no node identity; node markers validate their real owner. Imports retain both scopes and reject malformed or duplicate markers before mutation.
- Runtime interval state is separate from its optional terminal outcome. Perfetto uses `profiling.marker` with scope/kind. Archives retain the format identifier, use version 2 and reject version 1 before storage mutation.
- Verification: Common 145, Infrastructure unit 18, Presentation 48 and real SQLite/SQL Server/PostgreSQL integration 20 tests passed. TRX files: `/tmp/bitdevkit-profiling-contract-results/markers-*.trx`; logs: `/tmp/bitdevkit-profiling-markers-*.log`.

### TASK-008 / Phase 2 exit — explicit Runtime configuration and naming

- `AddProfiling` no longer enables Runtime implicitly. Nested Runtime options retain the approved collection/retention defaults; `WithRuntimeProfiling` composes repeated/reordered setup and explicit disablement. Disabled Runtime removes only its owned Broadcast handlers and workers, preserving an independently configured Broadcast feature. Shared cached node metadata remains available without Runtime.
- Runtime stores are internal `InMemoryRuntimeProfilingStore` and `EntityFrameworkRuntimeProfilingStore<TContext>` components. The context contract is `IProfilingDbContext`; the profiling-only EF extension is `WithEntityFrameworkProvider`. Shared `ProfilingNodeEntity` deliberately remains shared, rather than using the mechanical inventory's Runtime node name. Combined provider selection is TASK-029.
- Runtime commands use `profiling runtime` (alias `prof runtime`); console dispatch matches complete group prefixes while preserving existing single-part groups. Runtime dashboard routes live under `/profiling/runtime`, and scoped marker filters no longer duplicate annotations. Dashboard refresh configuration is owned by `DashboardEndpointsOptions`.
- Added operation/query option defaults and validation for the independent capabilities. Recorder/provider wiring remains in the later planned tasks; Runtime setup does not enable operation capture.
- Build: `dotnet build bITdevKit.slnx --no-restore --nologo`, exit 0, zero warnings/errors, 1:32. Log `/tmp/bitdevkit-profiling-phase2-build.log`.
- Focused tests: Common 149, Infrastructure unit 18, Presentation profiling plus console commands 68, actual SQLite/SQL Server/PostgreSQL integration 20; all passed (255 total). TRX `/tmp/bitdevkit-profiling-contract-results/phase2-*.trx`; logs `/tmp/bitdevkit-profiling-phase2-*.log`.
- Intermediate build/test failures were repaired: the benchmark assembly needed friend access to the internal Runtime component; two Runtime route/alias expectations still used the old names; the Broadcast coexistence test needed to preserve the pre-existing built-in handler.

### TASK-009 — operation capture core

- Added a singleton-capable recorder with execution-local immutable context frames, explicit ownership, rejected/suppressed boundaries, cached node identity, and snapshotted options. Capture/enrichment perform no provider or Runtime control calls. Optional Runtime hint faults leave admitted operations available.
- Typed metadata uses canonical case-insensitive names without trimming; invalid replacements preserve accepted values. Count, Unicode, payload and shared admission limits apply before mutation. Completed graphs freeze read-only collections and release live admission. Adapter collections copy a bounded indexed selection instead of trusting arbitrary enumerators.
- Foundational scope/segment arithmetic types are present to keep this recording slice compilable; helper, concurrency, reduction, expiry and logging acceptance are verified in TASK-010 through TASK-015. Those tasks remain incomplete.
- Eight new core tests pass alongside existing regression coverage: 157 Common profiling tests, exit 0. Log `/tmp/bitdevkit-profiling-operation-core-tests.log`, TRX `/tmp/bitdevkit-profiling-contract-results/operation-core.trx`.

### TASK-010 — outcome scopes and delegate helpers

- Added synchronous/Task and internal ValueTask helpers, explicit Result classification, matching-token cancellation and optional injection fallback. Business delegates run once; observation failures cannot replace values or exceptions.
- Async scope creation occurs inside an async execution boundary so caller ownership is restored, including overlapping Blazor actions.
- Common profiling suite: **170 passed, 0 failed, 0 skipped**. TRX: `/tmp/bitdevkit-profiling-contract-results/operation-helpers.trx`. Initial compilation exposed value-type method-group boxing; examples now use explicit classifier lambdas. Cancellation identity assertions use direct await/catch because the test assertion wrapper substitutes TaskCanceledException.

### TASK-011 — repeated, nested and parallel segments

- Aggregates full structured paths, first labels, count/outcome statistics and parent self-time from direct-child coverage union. Each invocation retains its own live parent; closing a parent forces children incomplete once. Rejected descendants stay suppressed.
- Tests cover the 100 ms parent with 150 ms overlapping child work, 16 concurrent branches, 10,000 constant-state repetitions, late disposal, recovered failures, and depth/path/live limits.
- Common profiling suite: **177 passed, 0 failed, 0 skipped**; `/tmp/bitdevkit-profiling-contract-results/operation-segments.trx`.

### TASK-012 — typed metadata, reducers and bounded failures

- Implemented all five reducers with contributing counts, weighted-average sum/count, Last completion order/UTC, and outcome-specific values. Conflicts and integer/decimal/double overflow are unavailable; missing values are not zero.
- Tested large Int64 losslessness, invariant culture, decimal/double distinctions, mixed/missing/empty dimensions, failure-category limits, Unicode-safe message clipping, rejected category keys, and failed sanitization.
- Common profiling suite: **194 passed, 0 failed, 0 skipped**; `/tmp/bitdevkit-profiling-contract-results/operation-metadata.trx`.

### TASK-013 — mutually exclusive wall-time coverage

- Online top-level accounting credits one segment, Parallel for multiple live invocations including identical keys, and Outside segments for no active invocation. Nested overlap stays inside its top-level owner. Rounding is reconciled against monotonic root duration.
- Sequential/repeated, same-key overlap, nested parallel children, partial coverage and zero-duration cases pass. Every breakdown sums to observed root duration.
- Common profiling suite: **199 passed, 0 failed, 0 skipped**; `/tmp/bitdevkit-profiling-contract-results/operation-wall-time.trx`.

### TASK-014 — independent expiry and shutdown cleanup

- Added a TimeProvider-driven cleanup worker independent of persistence. Abandoned roots end at their logical deadline; open children become incomplete and active payload/admission is released. Closed handles reject late updates and do not start replacement roots. Host shutdown closes active scopes before the registered writer drains.
- Tests prove deadline clipping, overlapping actions continuing after expiry, shutdown closure, completion-sink rejection/failure, finalization-clock failure and UTC clock discontinuity.
- Common profiling suite: **206 passed, 0 failed, 0 skipped**; `/tmp/bitdevkit-profiling-contract-results/operation-cleanup.trx`.

### TASK-015 — fault-isolated Trace lifecycle events

- Added independently filterable operation/segment log categories and stable 6101–6104 named events, using source-generated parameterized templates. Logs include bounded identity/path, invocation sequence, outcome and duration, with code/type only for failures.
- At-most-once stop logging shares finalization, including parent-forced incomplete children. Suppressed, disabled and rejected scopes remain silent. Filter checks precede path formatting and log-only state; 10,000 filtered lifecycle rounds allocate **0 bytes** in the focused check. Filter/sink exceptions increment bounded logging-fault accounting and preserve business outcomes.
- Common profiling suite: **212 passed, 0 failed, 0 skipped**; `/tmp/bitdevkit-profiling-contract-results/operation-logging.trx`.

### Phase 3 exit verification — 2026-10-08 UTC

- TASK-009 through TASK-015 implemented and checked. Generic operation capture remains internal and will be registered through the final fluent setup in TASK-029; the completion sink remains an internal seam for the real bounded queue in TASK-026. These later tasks are still incomplete.
- `dotnet build bITdevKit.slnx --no-restore --nologo`: succeeded, **0 warnings / 0 errors**, 1:37. Log: `/tmp/bitdevkit-profiling-phase3-build.log`.
- Post-build focused Common profiling regression suite: **212 passed, 0 failed, 0 skipped**. TRX: `/tmp/bitdevkit-profiling-contract-results/phase3-common.trx`.
- Phase changes reviewed for scope, bounds, ownership, exception preservation and rejected/closed-context behavior. No production TODO, placeholder or NotImplementedException introduced.
