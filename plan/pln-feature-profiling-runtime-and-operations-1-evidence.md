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
- TASK-007 through TASK-025: complete; detailed evidence follows. TASK-026 through TASK-055 remain incomplete. Execution is paused before Phase 6 at the user's request.

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

### Phase 4 — TASK-016 through TASK-020 — 2026-10-08 UTC

- Added the complete process-local provider façade with separate Runtime and Operation facets. Operation appends defensively freeze bounded whole root graphs, validate ownership/paths/typed metadata/HTTP projections, share cached process descriptors with Runtime, and return one typed disposition per submitted root. Publication positions are assigned atomically with visibility.
- Added provider-authoritative epochs, bounded idempotent writer registration, original-token renewal, monotonically advancing settlement, and retirement. Replays under stale original leases are rejected; records with uncertain unexpired-writer settlement cannot be evicted to admit new history. Settled roots can be evicted without allowing their replay to recreate them.
- Added serialized clear preparation, fixed per-writer cutoffs, registration generations, atomic sealed fences, half-open completion-UTC selection, resumable bounded deletion, Runtime maintenance gates and active-session rejection. Acknowledged cutoffs remain fixed after lease expiry; unmatched, post-cutoff and newly registered work survives. Unreachable preparation expires without deletion. Fence capacity rejects new maintenance until safe compaction.
- Retention uses an ordered completion index rather than sorting the entire history on each pressured append. Maintenance shares one root/time scheduling budget across recovery and retention. Root graph removals update the deletion revision, including Runtime removals that can affect an overlay boundary.
- Added authenticated query-bound cursors containing epoch, publication position, deletion revision, fixed evaluated UTC window and expiry. Counts cover the full matching retained selection. Slow/Recent limit occurrences; ByCount limits groups. Scalar type/value distinctions and canonical name/path equality survive filtering/grouping. Segment predicates must match one summary; exact lookup bypasses list windows/outcome defaults; oversized exact analysis returns an explicit limit failure.
- Backend-independent invariant scenarios live under `tests/Shared/Profiling/` and are linked into Common unit tests; EF fixtures will reuse them in TASK-025. New lease/recovery/HTTP metadata tests cover identity tampering, lost-response retries, settlement/retention, fixed acknowledgements, expiry before/after acknowledgement, new leases during clear, delayed fenced writes, partial deletion recovery, preparation failure, Runtime gating, stable pages, cursor tampering/expiry, canonical GUID ordering, typed values, full group counts and same-summary filters.
- Initial regression testing found that the Runtime store synchronization constructor was not public enough for existing DI activation. Restored its public parameterless constructor and retained the internal shared-lock constructor; the regression suite now passes.
- `dotnet build bITdevKit.slnx --no-restore --nologo`: **0 warnings / 0 errors**, 1:31.98; `/tmp/bitdevkit-profiling-phase4-build.log`.
- Post-build focused suites: **Common 269**, **Infrastructure unit 18**, **Presentation 48** passed; **335 total, 0 failed, 0 skipped**. Logs `/tmp/bitdevkit-profiling-phase4-{common,infrastructure,presentation}.log`; TRX `/tmp/bitdevkit-profiling-contract-results/phase4-{common,infrastructure,presentation}.trx`.
- TASK-016–TASK-020 complete. EF storage, production writer/registration, HTTP middleware, feature behaviors, query services, dashboards, documentation/examples and load evidence remain incomplete (TASK-021–TASK-055). This phase does not claim that production DI wiring or the complete feature is finished.

### Phase 5 — TASK-021 through TASK-025 — 2026-10-08 UTC

- Added `EntityFrameworkProfilingStorageProvider<TContext>` with independent Runtime and Operation facets. Fourteen profiling tables map whole operation roots, aggregated segment summaries, typed dimensions/measurements, HTTP projections, shared process descriptors, and durable epoch/writer/clear state. Foreign keys remove the complete owned root graph atomically. No row is stored per segment invocation.
- Appends resolve fresh scoped contexts per attempt and serialize publication, lease validation, settlement, and clear sealing through a durable operation coordination row. Normal operation writes do not acquire the separate Runtime maintenance gate. Combined maintenance acquires Runtime then Operation locks consistently. Writer identity, original tokens, stable open attempts, fixed acknowledged cutoffs, and non-replayable settlement survive independent provider instances.
- Partial batches return typed per-root dispositions. Known pre-commit database failures permit one fresh rollback retry; lost commit acknowledgements and commit cancellation report `MayHaveCommitted` without an internal replay. Bounded clear/recovery/retention preserves unmatched, new-generation, post-cutoff, and unsettled work. Root removal advances query deletion revisions; Runtime removals use their independent revision.
- Shared cursor validation and normalization now serve both providers. Database queries apply typed filters and same-summary predicates before bounded materialization; keyset pages, full counts, and group ranking execute server-side. Portable binary projections preserve ordinal string values, case-insensitive names/keys/paths, canonical GUID ordering, and missing-versus-typed dimension grouping. Exact duration sums avoid double precision loss and return unavailable on Int64 overflow. Queries load bounded records and labels without N+1 lookups or deserializing the complete history.
- Lease deadlines use database-authoritative UTC. Cached process identity and private Broadcast correlation retain independent, exact UTC ticks across database timestamp precision. Runtime can attach Broadcast metadata to an Operation-owned process descriptor without changing the executing process identity.
- Added shared conformance fixtures with two independent service providers and controlled database-clock observations. Actual SQLite, SQL Server 2022, and PostgreSQL 16 each passed **43 Operation provider cases**. Coverage includes lost acknowledgements after real commit, fixed-cutoff/expiry races, publication/cursor boundaries, typed comparisons, same-summary filtering, exact sums, complete graph cascades, process identity precision and Runtime attachment, migration SQL, startup schema non-mutation, and preservation of unrelated host tables. Schema creation/deletion is confined to isolated test fixtures; production profiling performs no automatic DDL or migrations.
- Repaired issues found by actual-engine and regression checks: duplicate coordination saves during recovery, SQL Server decimal promotion in duration aggregation, PostgreSQL serializable update conflicts (replaced with explicit row locks under ReadCommitted), timestamp precision affecting cached identity, and a retry later in a batch incorrectly using an identity evicted by an earlier member. Added a regression for that within-batch eviction and for independent Runtime correlation timestamps.
- Final solution build: `dotnet build bITdevKit.slnx --no-restore --nologo` succeeded with **0 warnings / 0 errors**, **1:44.24**. Log: `/tmp/bitdevkit-profiling-phase5-build.log`.
- Final sequential profiling regression checks: **Common 271**, **Infrastructure unit 34**, **Presentation 48**, **Infrastructure integration 149**; **502 total, 0 failed, 0 skipped**. Integration includes the 129 new cross-engine provider cases and 20 Runtime/lifecycle regressions. Command per suite: `dotnet test tests/<Project>/<Project>.csproj --no-build --no-restore --nologo --filter 'FullyQualifiedName~Profiling'`. Logs: `/tmp/bitdevkit-profiling-phase5-{common,infrastructure,presentation,integration}.log`; TRX: `/tmp/bitdevkit-profiling-contract-results/phase5-{common,infrastructure,presentation,integration}.trx`.
- Final diff checks pass; changed production code contains no TODO, placeholder or NotImplementedException. This phase delivers the provider implementation; production completion queue, writer, maintenance worker and final fluent registration remain TASK-026–TASK-030.

### Paused checkpoint — before Phase 6

TASK-001–TASK-025 are complete. At the user's request, work stops before TASK-026; TASK-026–TASK-055 remain incomplete. No Phase 6 implementation has begun. Resume with the bounded completion queue, then periodic writer/health/registration, HTTP integration, feature behaviors, query services, dashboards, examples/documentation and final load/acceptance verification. The full profiling overhaul is not yet complete.


### Phase 6 — TASK-026 through TASK-030 — 2026-10-08 UTC

- Resumed at the user's request. Added the nonblocking completion queue, periodic writer, independent provider maintenance worker and local health source. Accepted queue, retry and in-flight payload share the 8,192-record/64-MiB bounds. Original leases and strictly increasing completion sequences survive retries; pre-lease completions and expired buffers have distinct loss counters.
- Writer ticks synchronize even while idle, retain fixed clear acknowledgements after lost responses, and select below a lease-qualified watermark. Count/byte/time ceilings, one/two-second retry eligibility, stable partial-batch identities, idempotent unknown-commit retries and settlement fencing are verified. Cancellation-ignoring providers never overlap a replacement call or premature settlement. Shutdown closes active captures before the five-second drain and distinguishes unconfirmed in-flight writes from known queued loss.
- `WithOperationProfiling`, `WithInMemoryProvider`, `WithProvider<TProvider>` and `WithEntityFrameworkProvider<TContext>` now compose the final provider facade. Both facets resolve from the same singleton. Omitted setup installs no profiler; disabled capture is inert. Runtime installs Broadcast independently; reordered/repeated options preserve explicit choices. EF contexts remain scoped per attempt. Provider capabilities report their effective retention policy so generic maintenance uses EF's 100,000/seven-day defaults while preserving explicit settings; no backend-type switch is required in the core worker.
- Local health distinguishes capture, queue/in-flight bytes and age, writer admission/stalls, persistence/retry faults, administrative drops, unknown commits, original-lease loss, and provider/node scope. Provider maintenance now distinguishes retention removals from clear recovery removals.
- New public queue/writer/maintenance/health building blocks and members have XML documentation. New implementation avoids internal modifiers where practical, following the user's refinement. Existing private/internal recording implementation remains behind the facade.
- Regression repairs: added host lifetime to the strict Broadcast DI test fixture; independent test arrivals establish their own ambient boundary rather than bypassing writer suppression; updated the old Runtime clear fixture to assert removal behavior instead of equality with a result lacking progress metadata.
- Final solution build: `dotnet build bITdevKit.slnx --no-restore --nologo`, **0 warnings / 0 errors**, **1:48.68**. Log: `/tmp/bitdevkit-profiling-phase6-build.log`.
- Final sequential profiling suites: **Common 300**, **Infrastructure unit 34**, **Presentation 48**, **actual-engine integration 149**; **531 passed, 0 failed, 0 skipped**. Logs: `/tmp/bitdevkit-profiling-phase6-{Common.UnitTests,Infrastructure.UnitTests,Presentation.UnitTests,Infrastructure.IntegrationTests}.log`; TRX: `/tmp/bitdevkit-profiling-contract-results/phase6-*.trx`. Controlled-time tests verify the default eight batches of 512 records (4,096 scheduler ceiling), byte/time limits, fixed arrivals, partial retries, unknown settlement, expiry, clear cutoff retries, host closure and ignored-cancellation deadlines. This ceiling is not a database-throughput claim.
- TASK-026–TASK-030 complete. Continue with TASK-031–TASK-055. The complete feature and performance acceptance remain unfinished.


## Phase 7 — HTTP capture and sampling (2026-10-08)

TASK-031–TASK-035 are implemented. The HTTP adapter requires explicitly enabled Operations and composes independently from Runtime. Its public setup, feature, samplers, matcher and transport adapters have XML documentation; no new HTTP source uses `internal` declarations.

- Prepared original-path blacklist matching precedes prefix removal and canonical overlong-key hashing. Default AllRequests, controlled independent Probability, monotonic node-local RateLimit and validated singleton custom samplers execute once per request. Throwing or invalid decisions preserve application execution and have separate health counters.
- The outer observer records middleware-entry UTC/monotonic timing, selected-request concurrency before admission, one full-GUID header and explicit root handle. The inner exception observer preserves original routes and failure evidence through handled responses, replacement scopes and re-execution. Skips suppress nested capture; disabling HTTP leaves manual instrumentation available.
- Stream, BodyWriter, send-file and optionally consumed request Stream/PipeReader bytes are observed without buffering. Successful output, declared lengths and partial coverage stay distinct. Cache hits replace or remove prior profiling IDs. Aborts, normal completion and expiry finalize once while transport ownership and business responses remain unchanged.
- The real TestServer suite covers compression, output cache, static files, send-file, authorization challenges, unmatched routes, controller/minimal API DI, handled 200/422 errors, cancellation, abort, admission rejection, expiry, detached pipe consumption, entry sampling time and concurrent executions.

Verification:

- `dotnet build bITdevKit.slnx --no-restore --nologo`: **0 warnings, 0 errors**, 1:15.92; `/tmp/bitdevkit-profiling-phase7-solution.log`.
- Focused HTTP suite: **70 passed, 0 failed, 0 skipped**; `/tmp/bitdevkit-profiling-phase7-http.log` and `phase7-http.trx`.
- Sequential profiling regressions: Common **300 passed**, Presentation **118 passed**, no failures/skips. Logs `/tmp/bitdevkit-profiling-phase7-{Common.UnitTests,Presentation.UnitTests}.log`, TRX files under `/tmp/bitdevkit-profiling-contract-results`.
- `git diff --check` passes. Phase 8 and subsequent tasks remain outstanding.

## Phase 8 — optional feature behaviors (2026-10-08 UTC)

TASK-036–TASK-039 are implemented. Jobs and Pipelines use their existing behavior registrations; Orchestrations has an idempotent `WithProfilingBehavior()` pairing action instrumentation with bounded executor slices. Omitted Profiling returns the original delegate Task/ValueTask without installing dependencies. Registered-disabled and suppressed capture execute unchanged. Existing owners receive segments; independent workers clear inherited ownership before starting their own operation. Task-based adapters preserve their native Task contract, and Pipeline ValueTask sources are consumed exactly once.

The integrations record stable job/pipeline/orchestration definition keys, nested action/step paths and bounded metadata. Repeated attempts aggregate, never-invoked pipeline steps produce no samples, and durable orchestration waits close the current slice. Resume creates another correlated slice. Domain failures, timeouts, cancellation, exceptions and compensation retain business behavior and independent root/segment outcomes. Instrumentation cannot dispose an owner's outer scope.

Verification used a stable final solution build followed by sequential `--no-build` tests:

| Check | Result | Evidence |
| --- | --- | --- |
| `dotnet build --no-restore` | Zero warnings/errors; 2:30.35 | `/tmp/bitdevkit-profiling-phase8-solution-final.log` |
| Common Profiling/Pipeline tests | 375 passed, zero failures/skips | `/tmp/bitdevkit-profiling-phase8-common-final.log` |
| Application Jobs/JobScheduling/Orchestration tests | 425 passed, zero failures/skips | `/tmp/bitdevkit-profiling-phase8-application-final.log` |
| Presentation Profiling tests | 118 passed, zero failures/skips | `/tmp/bitdevkit-profiling-phase8-presentation-final.log` |
| New behavior tests | 43 passing cases included above | `JobProfilingBehaviorTests`, `PipelineProfilingBehaviorTests`, `OrchestrationProfilingBehaviorTests` |
| New public adapter declarations | XML documented; no internal declarations | Source inspection and `git diff --check` |

A cancellation assertion originally used Shouldly's synthetic canceled-task exception; direct-await checks now prove original exception identity and token preservation. A pre-existing lost-clear-ack test had a scheduling-dependent administrative-counter assertion. It now explicitly waits for clear sealing before the append, proving the intended fence rather than relying on scheduling order. The final complete rerun above passes both checks.

The overhaul remains incomplete. Continue with TASK-040–TASK-055; no query/dashboard/performance completion is claimed by this phase.

## Phase 9 prerequisites: storage and failure-isolation refinements — 2026-10-08 UTC

TASK-056 and TASK-057 are complete. TASK-040–TASK-055 remain incomplete; this checkpoint does not complete Phase 9 or the overall assignment.

- `IProfilingDbContext` now exposes only inherited `Set<TEntity>()`. All EF consumers and fixture contexts use the generic contract. `ConfigureProfiling()` explicitly registers every retained Runtime entity, including invalid-session tombstones, rather than depending on host DbSet discovery.
- Removed the redundant operation-measurement entity/table/navigation and append projections. Root `RecordJson` and segment `SummaryJson` preserve measurements and reducer/outcome information. Indexed relational segment/dimension projections remain available for bounded filters and grouping. Actual-engine tests verify JSON round trips, absence of the measurement table, and atomic root deletion.
- Unexpected observation faults discard inconsistent capture under its own lock and release admission immediately. Discarded captures are counted as diagnostic loss. Queue health labels clock-dependent age unavailable and keeps record/byte counts; shutdown distinguishes pending uncertain commits from confirmed loss.
- HTTP startup, metadata, status/ID access, callbacks, finalization and exception observation are guarded. Worker initialization, timers, warnings and shutdown are guarded. Independent job/pipeline/orchestration boundary restoration uses an idempotent safe wrapper. Faulty instrumentation cannot rerun a business delegate or replace its result/exception.
- An injected `TimeProvider.CreateTimer` failure initially exposed a real test-host crash in `PeriodicTimer.Finalize()` (`NullReferenceException`). The inspected [.NET implementation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Threading/PeriodicTimer.cs) creates the injected timer before its finalizer can dispose it. Profiling workers now use a finalizer-free coalescing `ProfilingPeriodicTimer`. Tests cover failed factories plus forced finalization, coalescing, one consumer, cancellation, disposal, and survival of the actual registered hosted workers.
- A new orchestration fault test initially failed because its fixture supplied a null service provider. The fixture was corrected; the final actual adapter test preserves both the action result and original business exception.
- Recoverable profiling faults are isolated; invalid explicit setup remains a validation failure. Process-fatal failures and arbitrary application code used to compute metadata are outside this guarantee.

Final validation was sequential against the final built artifacts:

| Command / selector | Result | Log |
| --- | --- | --- |
| `dotnet build bITdevKit.slnx --nologo --no-restore` | Exit 0; 0 warnings, 0 errors; 2:41.42 | `/tmp/bitdevkit-profiling-refinement-final-build.log` |
| Common UnitTests, `Profiling` or `Pipeline`, `--no-build` | 382 passed; 0 failed/skipped | `/tmp/bitdevkit-profiling-refinement-common-final.log` |
| Application UnitTests, `Jobs`, `JobScheduling` or `Orchestration`, `--no-build` | 426 passed; 0 failed/skipped | `/tmp/bitdevkit-profiling-refinement-application-final.log` |
| Presentation UnitTests, `Profiling`, `--no-build` | 129 passed; 0 failed/skipped | `/tmp/bitdevkit-profiling-refinement-presentation-final.log` |
| Infrastructure UnitTests, `Profiling`, `--no-build` | 35 passed; 0 failed/skipped | `/tmp/bitdevkit-profiling-refinement-infrastructure-final.log` |
| Infrastructure IntegrationTests, `Profiling`, `--no-build` | 149 passed; 0 failed/skipped; actual SQLite/SQL Server/PostgreSQL | `/tmp/bitdevkit-profiling-refinement-engines-final.log` |

Total: **1,121 passing tests**. Docker server verified available (`29.7.2`). No host application database was migrated or reset. No new production package was introduced.


## Phase 9 — bounded analysis and Runtime correlation (2026-10-08)

TASK-040–TASK-043 are implemented and verified. Phase 10–12 remain outstanding.

- `OperationProfilingQueryService` owns four non-waiting admissions and one five-second deadline per view build. Sequential subordinate reads share that admission. Timed-out, cancellation-ignoring and unawaited provider work retains its original capacity until actual unwind; escaped sessions cannot start new reads. Provider code runs outside the short bookkeeping lock and outside the caller synchronization context, with inherited execution context suppressed. Reads suppress capture and never flush the writer.
- Bounded exact analysis computes midpoint median and nearest-rank p95/p99 from retained roots, invocation-weighted segment means, and distributions of each owner's accumulated segment durations. Compatible measurement sums/counts remain weighted, mixed dimensions remain labeled, and HTTP policy counts are never extrapolated. Last-value ties use observed UTC, canonical owner ID, then the owner's local sequence.
- Optional `IRuntimeProfilingCorrelationStore` is implemented by both built-in Runtime facets. Exact GUID/key/hostname/PID/process-start identity and actual collection windows select at most 2,000 interval plus surrounding snapshots; session/participation metadata is also bounded. EF filters indexed lossless UTC ticks in storage. Restarts/remapped identities, zero-duration observations, half-open reverse overlap, missing evidence, sparse sampling and clock jumps have explicit results. A rate interval is unavailable if its predecessor is absent; process metrics are never attributed to an operation.
- Shared provider contracts run identical analysis and persisted-operation correlation checks on memory, SQLite, SQL Server and PostgreSQL. Existing provider tests cover publication boundaries during new writes, delayed completion-time inserts, deletion revisions, expiry, stable canonical ties and same-summary predicates.
- Additional fault proofs cover synchronous blocking providers, cancellation-ignoring calls, fire-and-forget subordinate calls, retained admission, safe provider errors and caller cancellation. The blocking-provider test exposed synchronization-context execution and a bookkeeping lock held over provider code; both were corrected and the regression now passes. An initially invalid synthetic record had inconsistent wall-time totals; the fixture was corrected before the final persisted-record roundtrip checks.

| Verification | Final result | Evidence |
| --- | --- | --- |
| `dotnet build bITdevKit.slnx --nologo --no-restore` | 0 warnings, 0 errors; 2:39.29 | `/tmp/bitdevkit-phase9-build-proof.log`; subsequent Common and Infrastructure test builds verify the final deadline/lock corrections |
| Common `Profiling\|Pipeline` (build and test) | 397 passed, 0 failed/skipped | `/tmp/bitdevkit-phase9-common-proof.log` |
| Infrastructure unit `Profiling` | 35 passed, 0 failed/skipped | `/tmp/bitdevkit-phase9-infrastructure-final.log` |
| Actual-engine integration `Profiling` (build and test) | 161 passed, 0 failed/skipped; 1:17 | `/tmp/bitdevkit-phase9-engines-proof.log` |
| Publication-window engine plans | SQLite indexed search; SQL Server index seek; PostgreSQL index scan with range condition | Raw artifacts below |

Actual small-fixture query plans are saved in [SQLite](evidence/profiling-runtime-and-operations-1/phase-9/query-plan-sqlite.txt), [SQL Server](evidence/profiling-runtime-and-operations-1/phase-9/query-plan-sqlserver.xml), and [PostgreSQL](evidence/profiling-runtime-and-operations-1/phase-9/query-plan-postgresql.txt). These establish actual engine compilation and index selection for the representative range query; they do not establish production throughput or guarantee optimizer choices for other distributions. Phase 12 load evidence remains required.
