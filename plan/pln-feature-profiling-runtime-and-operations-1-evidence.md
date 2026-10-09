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
- TASK-007 through TASK-059: complete; detailed phase evidence follows. Resumed at the user's request on 2026-10-09, preserved completed capacity measurements, completed workspace/site acceptance, then verified WeatherFiesta integration and rebuilt the site. All 59 task rows and all 21 acceptance-test rows are covered. Earlier incomplete/pause statements below are historical checkpoints.

## Requirement acceptance

The final requirement-to-evidence matrix was closed in TASK-055 after capacity measurements and complete workspace verification. Phase 13 closes application acceptance afterward. The following sections record passing implementation checks; baseline tests describe the original Runtime behavior only.

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

## Phase 10 — retained Operations and Requests dashboard (2026-10-08)

TASK-044–TASK-047 are implemented and verified. Phase 11–12 remain outstanding.

- The shared Profiling landing chooses Runtime or Operations by enabled capture. The navigation provider describes the overarching feature. Disabled subfeature capture retains query access; disabled Runtime controls resolve optionally. All new routes inherit the dashboard prefix and authorization; internal JSON is excluded from API description, with no Operation export or clearing endpoint.
- Initial render, content refresh and details use the same injected query facade/model builder under one query admission. Queries finish before Razor streaming starts, so validation, Busy, timeout and missing-ID statuses are set cleanly. Requests enforces `HttpRequest` independently of supplied filters. Exact lookup ignores list windows/outcomes. Reads do not flush or expose pending completions.
- Slow/Recent/By count views show full retained counts, typed grouping, executing node, outcome/quality, HTTP response bytes and sampling metadata. Expanding a bucket preserves general predicates with a separately bounded four-value exact/missing group selector. The memory and all EF providers apply it before ranking/counts and fingerprint it for paging. This resolves expansion of groups with missing values or already-full general predicate sets.
- Details show complete-path repeated/nested segment aggregates, weighted means, outcome buckets, dimensions, reducers and safe failures. Exclusive root bars distinguish Parallel and Outside, use consistent key colors and keyboard tooltips, and make zero-duration percentages unavailable. Runtime CPU/memory/allocation/GC charts show non-owning process context, actual sample intervals, gaps and operation UTC shading. Both navigation directions preserve node/time selection. Runtime labels/chart timestamps are UTC.
- The shared refresher maintains one browser fetch at a time, coalesces obsolete refreshes, cancels changed selectors, pauses hidden pages and retains visibly stale content on failure. It preserves group expansion and available scroll position. Paged selections turn off/disable automatic refresh until an explicit new selection or Refresh latest; chart instances are purged on replacement.

| Verification | Result | Evidence |
| --- | --- | --- |
| Presentation `Profiling\|Dashboard`, including target build | 165 passed, 0 failed/skipped | [log](evidence/profiling-runtime-and-operations-1/phase-10/presentation-tests.log) |
| Common `Profiling\|Pipeline`, including target build | 398 passed, 0 failed/skipped | [log](evidence/profiling-runtime-and-operations-1/phase-10/common-tests.log) |
| Actual SQLite/SQL Server/PostgreSQL profiling tests, including target build | 164 passed, 0 failed/skipped | [log](evidence/profiling-runtime-and-operations-1/phase-10/engine-tests.log) |
| Visible Chromium browser, desktop/mobile | All 14 checks pass; maximum one active refresh, no script errors | [result](evidence/profiling-runtime-and-operations-1/phase-10/result.json), [reproduction](evidence/profiling-runtime-and-operations-1/phase-10/README.md) |

The browser check caught a discarded chart-layout overlay, which now uses the shared theme plus explicit axes/shading. Endpoint checks caught setting response status after Razor had begun streaming, which is now resolved before rendering. The scroll assertion measures actual available scroll position instead of an unclamped requested offset. These checks do not replace Phase 12 overhead evidence.

## Phase 11 — host examples and public API guides (2026-10-08)

TASK-048–TASK-050 are implemented and verified. Phase 12 remains outstanding.

- WeatherFiesta explicitly enables Runtime, Operations and the HTTP adapter with path-prefix stripping and dashboard/health/API-description exclusions. The outer middleware owns the request; the inner observer sees failures inside the host handlers. The compare endpoint enriches the owner and captures its existing requester call as Query. The stress job optionally joins/owns an operation and adds Cpu, Allocate and Retain without changing its Runtime measurements, result or cancellation.
- The Profiling guide documents final contracts, independent capture, provider selection, safe failure boundaries, loop/nesting/parallel aggregates, sampling, nonblocking persistence, bounded reads/health, clear fences, UTC navigation and the version-2 archive break. Jobs, pipelines and orchestration guides describe optional behaviors and preserve native execution. A stale jobs XML registration example and two stale Runtime interface names were corrected.
- The new public API examples execute both MVC and minimal API injection, service/Blazor-style scopes, repeated nested segments, default/switched singleton sampling, optional behavior construction and both provider registrations. The EF model check uses relational SQLite, inherited generic Set and actual JSON root mapping; real database I/O remains in the three-engine contract suite. The example fixture activates the writer before capture and accesses the sampler through its prepared singleton runtime.

| Verification | Result | Evidence |
| --- | --- | --- |
| Presentation Profiling/Dashboard, including compilation of public examples | 174 passed, 0 failed/skipped | [log](evidence/profiling-runtime-and-operations-1/phase-11/presentation-tests.log) |
| Weather stress job, including example host build | 6 passed, 0 failed/skipped | [log](evidence/profiling-runtime-and-operations-1/phase-11/weather-tests.log) |

The initial example checks caught incorrect test assumptions about writer activation, sampler DI ownership, nonrelational EF support and owned model counts; those fixtures were repaired and the complete focused suite rerun. No provider capacity claim is made before Phase 12.

## Phase 12 — final correctness and measured overhead (in progress)

TASK-051 is verified. TASK-052–TASK-055 remain outstanding.

The user requested a pause on 2026-10-08 UTC. The running benchmark was cancelled and its processes stopped. There are 53 completed canonical trials: memory 21, SQLite 21 and SQL Server 11. SQL Server `nested` repetition 3 was interrupted; no PostgreSQL capacity trials have run. Cancellation is not a passing completion of the performance test. The raw results and [restart checkpoint](evidence/profiling-runtime-and-operations-1/phase-12/performance/PAUSED.md) are retained locally. Phase 12 changes remain uncommitted on `feature/profiling-runtime-and-operations`; earlier completed phases are already pushed. Do not resume until requested.

The focused suites ran sequentially with `dotnet test tests/<project>/<project>.csproj --filter FullyQualifiedName~Profiling --no-restore --nologo`: Common 339, Infrastructure unit 35, Application 28, Presentation 150, and real-engine Infrastructure integration 173 passed (725 total, zero failures/skips). The source/projection plan check was rerun on each engine to record exact versions. Logs and version files are in [phase-12 evidence](evidence/profiling-runtime-and-operations-1/phase-12/).

The three-engine contract suite now creates two independent DI writer providers with skewed recorder clocks, persists both roots, concurrently acquires pinned pages, fixes clear cutoffs, retains post-cutoff work, and rejects continued paging after deletion. Fresh providers recover sealed partial deletion and expired pre-seal preparation using database time rather than a skewed host clock. Existing shared tests cover lease expiry, new registrations, unknown commit/acknowledgement, retention and delayed retries. Initial assertions were corrected for optional failure payloads; final focused checks pass.

### Resume — 2026-10-09 UTC

The user explicitly requested resuming stress testing and completing feature phases, documentation and MkDocs before final WeatherFiesta application integration. The 53 completed measurements are preserved. Resume validates environment/workload, required raw latency artifacts and previous trial assertions before reuse, records a separate resume environment with preserved-file hashes, and reruns the interrupted trial. The original cancellation remains historical evidence, not a successful performance test. TASK-058–TASK-059 are added after TASK-055; no final application integration completion is claimed yet.

### Capacity run and retention-counter review — 2026-10-09 UTC

The resumed canonical performance test passed (2 tests, zero failures/skips; 40 minutes 3 seconds), bringing the original matrix to 84 complete trials. Reused raw artifacts are hashed in the resume environment file. All original trial application failure/capture-fault assertions pass. Control-case dashboard polls return 503 when profiling is unavailable; enabled-case polling statuses and costs remain recorded rather than hidden.

The final review found that in-memory append-pressure evictions were not included in health retention removals. The provider now reports these once through `ProfilingMaintenanceResult.CapacityEvictedOperations`, independently of actual per-call maintenance removal work/budgets, and the maintenance worker adds both retention sources to node-local health. Focused provider and worker regressions cover reporting, no repeated counting and later age-based removal. The memory matrix is being rerun in a separate evidence directory after this correction; the original matrix remains immutable historical evidence. EF append/query paths are unchanged by this counter correction. TASK-052–TASK-055 remain incomplete until the updated memory evidence and complete acceptance checks pass.

### TASK-052–TASK-053 — completed capacity evidence

The corrected memory run passed (3 tests, zero failures/skips; 25 minutes 15 seconds), completing 21 fresh trials. The accepted 84-trial matrix uses these memory measurements plus the unchanged EF-provider measurements from the original 84-trial matrix. The original artifacts remain retained separately. The focused counter/worker/scheduler suite passes 341 tests; harness resume/provider-selection checks pass 2 tests.

The [capacity report](evidence/profiling-runtime-and-operations-1/phase-12/capacity-report.md) records p50/p95, gross allocation, retained heap, throughput, queue records/bytes/age, persistence, drops/unknowns, retention removals, polling and logging, on/off deltas, recording-session scope and the tested explicit 10% sampling adjustment. `report-capacity.py` validates required trial coverage and regenerates the tables. All accepted trial application failure/shed/capture-fault assertions pass. Scheduler ceilings are separately verified by controlled-time tests, not inferred from database throughput. No HTTP metadata/TLS/production or universal overhead claim is made.

TASK-052 and TASK-053 are complete. TASK-054 (full workspace checks), TASK-055 (final feature/site acceptance), and TASK-058–TASK-059 (final WeatherFiesta app integration) remain incomplete.

### Workspace regression repairs — 2026-10-09 UTC

The first complete integration attempt exposed a queue test publication race: its handler flag became visible before the broker terminal state. The tests now wait for both, preserving their success assertions; all three focused checks pass. The test-only repair is committed separately as `eb43a0c62`.

A later attempt and an isolated scheduler baseline exposed a pre-existing Quartz repeated-execution defect (5 of 10 baseline cases failed). Successful execution writes nullable trigger metadata; the next execution called `.ToString()` on these null values before entering business processing. A focused repeated-execution regression fails with `NullReferenceException` at that conversion before the fix and passes after null-safe conversion. All ten scheduler integration checks then pass, preserving data/non-overlap checks and testing the exact two-second cron schedule separately from bounded execution-count waits. This user-authorized repair is committed separately as `9ed1f8689`. Failed/cancelled attempts remain evidence and are not counted as passing full-suite verification. The complete workspace tasks are rerun after these repairs.

The complete Application integration attempt subsequently passed 264 cases and exposed 20 unrelated storage failures. Eighteen were caused by local path helpers hardcoding Windows separators on Linux; physical nested-directory regressions failed in all four input/root variants before the fix and pass afterward. Native separator normalization and `Path.GetRelativePath` restore portable directories and canonical provider-relative listings. The two Windows factory tests now assert `PlatformNotSupportedException` on unsupported platforms and retain their original Windows checks. All 92 affected storage integration checks pass with zero skips. The separate repair is committed as `04e2ba5be`; full workspace tasks are rerun afterward.

A subsequent full attempt passed all 284 Application and 43 Domain integration tests, then exposed a pre-existing Cosmos conditional-token mismatch. Creation returned the quoted HTTP ETag while emulator query reads returned the same opaque value unquoted, so the provider rejected a fresh update. A focused creation/read assertion reproduced that exact mismatch. The provider now normalizes exposed tokens, accepts both forms in comparisons and formats native conditional headers; stale mutations remain rejected. The real Cosmos document-store suite passes 11 tests (13 pre-existing EF Cosmos skips), and two quoted/unquoted conditional update/stale mutation/fresh delete cases pass with zero skips. The separate repair is committed as `216ec9fc6`. The cancelled full attempt is not a passing integration task; final workspace checks continue afterward.

## Phase 12 capacity results

The accepted matrix contains 84 trials: three repetitions of six baseline cases and the tested 10% sampling adjustment for each of memory, SQLite, SQL Server and PostgreSQL. The original four-provider matrix passed; the 21 memory trials were rerun after correcting append-time retention eviction reporting. The accepted memory rows come from `performance-memory-retention/`; the EF rows come from `performance/`. Both directories retain their raw measurements, environment, gzip latency arrays and incremental summaries. The original memory rows are historical evidence, not the accepted retention-count results.

Each trial offered 1,000 operations/s for 60 measured seconds after 10 seconds warm-up. The workload runs the real optional pipeline behavior, generic operation recorder, four nested/parallel segment invocations (except root-only/control cases), a 4 KiB charged-record bound, periodic writer/retention and dashboard polling. It measures the general operation core through loopback HTTP/1.1, **not** the larger HTTP metadata projection. Trace uses a formatting/counting sink without disk I/O. Endpoint percentiles and allocations include the collocated client, Kestrel and test process; allocation is not recorder-only. GC was workstation, with eight reported processors and .NET 10.0.12. Exact engine versions: SQLite 3.53.3, SQL Server 16.0.4265.3, PostgreSQL 16.15.

The full matrix spans the explicit pause/resume with validated environment/workload and hashes of reused raw artifacts. The memory rerun is a separate recording session after the counter correction. These measurements establish this fixture's behavior; machine load, data volume, queries, real logging sinks, network transport and workload complexity can change results. No universal overhead percentage or production throughput guarantee follows from them.

### Endpoint and persistence results

Values are medians of the three per-trial measurements; p95 columns are medians of trial p95 values, not population percentiles. Drops, unknowns and removals are sums across the three measured windows. Persisted/s is observed before unmeasured draining and can include earlier warm-up backlog. Retention removals are node-local observations, distinct from incoming diagnostic loss.

| Provider | Case | p50 ms | p95 ms | Completed/s | Persisted/s | Diagnostic drops | Unknown commits | Retention removals | p95 delta vs omitted % |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| memory | omitted | 0.092 | 0.172 | 1000.0 | 0.0 | 0 | 0 | 0 | 0.0 |
| memory | disabled | 0.110 | 0.199 | 1000.0 | 0.0 | 0 | 0 | 0 | 16.0 |
| memory | empty | 0.112 | 0.209 | 1000.0 | 991.4 | 0 | 0 | 176867 | 21.3 |
| memory | nested | 0.136 | 0.228 | 1000.0 | 994.9 | 0 | 0 | 176812 | 32.8 |
| memory | nested-trace | 0.154 | 0.307 | 1000.0 | 995.1 | 0 | 0 | 176796 | 78.5 |
| memory | saturated | 0.136 | 0.304 | 1000.0 | 0.8 | 179856 | 0 | 0 | 76.7 |
| memory | nested-p010 | 0.112 | 0.256 | 1000.0 | 101.6 | 0 | 0 | 0 | 49.0 |
| sqlite | omitted | 0.086 | 0.132 | 1000.0 | 0.0 | 0 | 0 | 0 | 0.0 |
| sqlite | disabled | 0.101 | 0.150 | 1000.0 | 0.0 | 0 | 0 | 0 | 13.4 |
| sqlite | empty | 0.107 | 0.170 | 1000.0 | 999.5 | 0 | 0 | 0 | 28.5 |
| sqlite | nested | 0.138 | 0.262 | 1000.0 | 964.3 | 0 | 0 | 0 | 98.3 |
| sqlite | nested-trace | 0.158 | 0.298 | 1000.0 | 913.1 | 0 | 0 | 0 | 125.8 |
| sqlite | saturated | 0.131 | 0.190 | 1000.0 | 0.8 | 179848 | 0 | 0 | 43.6 |
| sqlite | nested-p010 | 0.108 | 0.178 | 1000.0 | 101.5 | 0 | 0 | 0 | 34.9 |
| sqlserver | omitted | 0.086 | 0.133 | 1000.0 | 0.0 | 0 | 0 | 0 | 0.0 |
| sqlserver | disabled | 0.098 | 0.145 | 1000.0 | 0.0 | 0 | 0 | 0 | 9.7 |
| sqlserver | empty | 0.130 | 0.459 | 1000.0 | 512.0 | 83961 | 0 | 0 | 246.0 |
| sqlserver | nested | 0.305 | 2.312 | 1000.0 | 290.1 | 119376 | 0 | 0 | 1643.8 |
| sqlserver | nested-trace | 0.170 | 0.392 | 1000.0 | 452.3 | 93163 | 0 | 0 | 195.7 |
| sqlserver | saturated | 0.135 | 0.307 | 1000.0 | 0.9 | 179832 | 0 | 0 | 131.5 |
| sqlserver | nested-p010 | 0.117 | 0.268 | 1000.0 | 101.5 | 0 | 0 | 0 | 102.1 |
| postgres | omitted | 0.094 | 0.228 | 1000.0 | 0.0 | 0 | 0 | 0 | 0.0 |
| postgres | disabled | 0.110 | 0.205 | 1000.0 | 0.0 | 0 | 0 | 0 | -10.3 |
| postgres | empty | 0.114 | 0.236 | 1000.0 | 999.4 | 0 | 0 | 0 | 3.2 |
| postgres | nested | 0.138 | 0.294 | 1000.0 | 512.0 | 79440 | 0 | 0 | 29.0 |
| postgres | nested-trace | 0.156 | 0.328 | 1000.0 | 512.0 | 79440 | 0 | 0 | 43.6 |
| postgres | saturated | 0.139 | 0.229 | 1000.0 | 0.9 | 179837 | 0 | 0 | 0.5 |
| postgres | nested-p010 | 0.118 | 0.201 | 1000.0 | 103.0 | 0 | 0 | 0 | -11.9 |

### Allocation, heap and queue observations

Allocation per successful completion is gross process allocation during the window. Retained heap change is measured after full collection at the window boundaries; it can be negative and does not identify an individual operation's allocation. Peak queue includes queued and in-flight records. The age metric is the maximum sampled oldest age, not a percentile. Raw samples retain poll durations/statuses and full before/after/post-drain health.

| Provider | Case | Alloc KiB/completion | Alloc delta % | Heap change MiB | Peak queue records | Peak queue MiB | Peak oldest age ms | End queue (median) | Poll failures (sum) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| memory | omitted | 10.17 | 0.0 | -0.19 | 0 | 0.00 | 0.0 | 0 | 180 |
| memory | disabled | 16.72 | 64.5 | 0.33 | 0 | 0.00 | 0.0 | 0 | 180 |
| memory | empty | 39.27 | 286.3 | 0.55 | 889 | 1.82 | 888.9 | 538 | 0 |
| memory | nested | 54.32 | 434.3 | 0.76 | 1000 | 3.14 | 1000.4 | 554 | 0 |
| memory | nested-trace | 58.27 | 473.2 | 0.22 | 1000 | 3.14 | 999.6 | 556 | 0 |
| memory | saturated | 31.88 | 213.6 | -0.28 | 8 | 0.03 | 9091.8 | 8 | 0 |
| memory | nested-p010 | 25.54 | 151.2 | 9.92 | 45 | 0.14 | 389.9 | 3 | 0 |
| sqlite | omitted | 10.17 | 0.0 | -1.15 | 0 | 0.00 | 0.0 | 0 | 180 |
| sqlite | disabled | 16.72 | 64.5 | 0.39 | 0 | 0.00 | 0.0 | 0 | 180 |
| sqlite | empty | 72.58 | 613.9 | 0.92 | 1058 | 2.16 | 1057.6 | 1041 | 0 |
| sqlite | nested | 150.17 | 1377.0 | 5.79 | 5460 | 17.15 | 5460.6 | 3163 | 0 |
| sqlite | nested-trace | 147.53 | 1351.1 | 6.66 | 7514 | 23.60 | 7510.5 | 6629 | 0 |
| sqlite | saturated | 32.78 | 222.5 | 0.49 | 8 | 0.03 | 9993.5 | 8 | 0 |
| sqlite | nested-p010 | 34.07 | 235.2 | -1.89 | 124 | 0.39 | 1136.1 | 96 | 0 |
| sqlserver | omitted | 10.17 | 0.0 | -2.10 | 0 | 0.00 | 0.0 | 0 | 180 |
| sqlserver | disabled | 16.72 | 64.5 | 0.40 | 0 | 0.00 | 0.0 | 0 | 180 |
| sqlserver | empty | 47.49 | 367.1 | 0.43 | 8192 | 16.75 | 20558.8 | 8012 | 8 |
| sqlserver | nested | 65.44 | 543.7 | 2.21 | 8192 | 25.73 | 31056.9 | 8192 | 6 |
| sqlserver | nested-trace | 87.65 | 762.1 | 6.01 | 8192 | 25.73 | 20612.5 | 7988 | 6 |
| sqlserver | saturated | 33.12 | 225.8 | -3.52 | 8 | 0.03 | 10002.0 | 8 | 0 |
| sqlserver | nested-p010 | 33.83 | 232.8 | -1.72 | 154 | 0.48 | 1283.1 | 93 | 0 |
| postgres | omitted | 10.17 | 0.0 | -4.15 | 0 | 0.00 | 0.0 | 0 | 180 |
| postgres | disabled | 16.72 | 64.5 | 0.39 | 0 | 0.00 | 0.0 | 0 | 180 |
| postgres | empty | 73.07 | 618.8 | 10.14 | 1131 | 2.31 | 1128.6 | 1041 | 0 |
| postgres | nested | 95.37 | 838.1 | 21.51 | 8192 | 25.73 | 15983.0 | 8192 | 0 |
| postgres | nested-trace | 99.36 | 877.4 | 23.96 | 8192 | 25.73 | 16025.2 | 8192 | 0 |
| postgres | saturated | 32.44 | 219.1 | -3.39 | 8 | 0.03 | 10009.9 | 8 | 0 |
| postgres | nested-p010 | 33.98 | 234.2 | -9.55 | 108 | 0.34 | 1064.3 | 5 | 0 |

### Interpretation and explicit adjustment

- All accepted trials preserved application responses: zero failed/shed offers and zero capture faults. Unknown persistence outcomes were zero in these runs; correctness tests separately exercise unknown commits and cancellation-ignoring providers.
- Default nested memory capture kept up with the offered rate without incoming drops. Its 10,000-root retention cap evicted older history; the corrected counter reports those removals on subsequent successful maintenance observations. An end-of-window queue larger than one batch also triggers the conservative sampling experiment, even when the writer is keeping up.
- SQLite nested capture produced variable backlog; SQL Server and PostgreSQL nested capture could not sustain 1,000 stored profiles/s with these defaults. Their bounded queues eventually dropped incoming diagnostics while application responses continued. This is an observed persistence limit, not evidence that every workload or deployment has that limit.
- A **tested explicit** configuration is `WithRequestProfiling(requests => requests.WithSampling(sampling => sampling.Probability(0.1)))`. The fixture exercises that prepared head-sampling policy at generic-owner admission. In all providers' three adjustment trials it recorded no drops or unknown commits and ended below one batch of queued work. Request sampling applies to eligible HTTP requests in applications; manual operations are not automatically HTTP sampled. The feature default remains AllRequests.
- Saturation cases deliberately used eight queued records, eight-record batches and a ten-second flush interval. They demonstrate bounded overflow and visible loss rather than sustainable throughput.
- Control-case dashboard polls returned 503 (profiling unavailable). Enabled-case failures, including bounded/too-large analyses, remain visible in raw statuses and the table. At 10% sampling, all adjustment trials had zero polling failures. Dashboard reads do not force a flush.
- Lifecycle Trace formatting was exercised and counted. Real file/network logging sinks were not measured. SQL request profiling payloads, TLS, remote network latency and application-specific heavy work require their own capacity tests.

Scheduler ceilings are proven independently of database speed by `OperationProfilingWriterTests`, including `DefaultTick_AtMostEight512RecordBatches_LeavesLaterWorkQueued`, fixed arrival watermarks, next-batch time budget, ignored timeout ownership, retries and bounded shutdown. The passing 341-test focused Common log includes these checks and the new retention reporting regressions. Eight starts of 512 records are a scheduling ceiling; this report never treats 4,096 records/s as observed throughput.

Reproduce the tables with `python3 plan/profiling/report-capacity.py` after preserving both output directories under `plan/evidence/profiling-runtime-and-operations-1/phase-12/`. Canonical workloads are opt-in; smoke mode is not acceptance evidence. The benchmark supports explicit validated provider subsets and resume of matching completed artifacts, with interrupted trials run again.

Raw trial files and build/test logs remain local under the user-ignored `plan/evidence/**`; the harness, report generator and this results summary are versioned.

### TASK-054 — final workspace checks

- `Solution - build`: passed at final regression-repair commit `216ec9fc6`, zero warnings/errors (2 minutes 28 seconds).
- `Solution - tests (unit)`: 5,531 passed, zero failures, 18 existing skips (four opt-in serializer benchmarks and 14 Windows-only checks). The complete task passed after the scheduler/storage repairs; after the isolated Cosmos repair the affected Infrastructure unit suite was repeated: 428 passed, zero failures, 14 Windows-only skips.
- `Solution - tests (integration)`: 1,622 passed, zero failures: Application 284, Domain 43, Infrastructure 1,295. Infrastructure reports 128 skips: 121 legacy Cosmos checks whose existing shared fixture intentionally does not start its Cosmos container, six existing unsupported InMemory EF cases, and one opt-in capacity test already passed separately. The separate real Cosmos document-store fixture and all three profiling database-engine suites passed.
- Source/project dependencies retain the Onion boundary. Active profiling APIs use Segment/Marker and final Runtime names; old names occur only in migration documentation. No production package or unrelated feature alias was added. Failed/cancelled attempts are preserved separately.

Final logs remain local as `phase-12/solution-build.log`, `solution-unit.log`, `solution-integration.log` and `infrastructure-unit-final.log`. TASK-054 is complete; TASK-055 and Phase 13 remain pending.

### Final feature acceptance — 2026-10-09 UTC

All requirement rows below are covered by the passing workspace/focused suites and the phase-specific evidence above. The database operation contract and shared analysis suites execute on SQLite, SQL Server and PostgreSQL. The Phase 12 capacity report records the accepted matrix and scope limitations; it does not replace correctness assertions. Phase 13 verifies the final WeatherFiesta host integration separately.

| Requirement | Passing evidence | Accepted behavior |
| --- | --- | --- |
| REQ-001 | ProfilingRegistrationTests; OperationProfilingDashboardTests; ProfilingDashboardEndpointsTests | One registration and three dashboard views. |
| REQ-002 | OperationProfilerTests; OperationProfilingHelpersTests; OperationProfilingFailureIsolationTests | Independent non-HTTP ownership and fault containment. |
| REQ-003 | ProfilingRegistrationTests; RequestProfilingInjectionTests | Explicit subfeatures, dependency validation and ordering. |
| REQ-004 | RequestProfilingSamplingTests; RequestProfilingMiddlewareTests | AllRequests admission without endpoint opt-in. |
| REQ-005 | OperationProfilerTests; OperationProfilingMetadataTests; OperationProfilingSegmentTests | Stable IDs/keys, immutable typed dimensions and structured paths. |
| REQ-006 | OperationProfilingSegmentTests; OperationProfilingWallTimeTests; OperationProfilingHelpersTests | Repeated/nested/parallel arithmetic and branch restoration. |
| REQ-007 | RequestProfilingMiddlewareTests; OperationProfilingHttpMetadataTests | Lifecycle/status/header, handled errors, response quality and body adapters. |
| REQ-008 | OperationProfilingWriterTests; OperationProfilingStoreContractTests; EntityFrameworkOperationProfilingContractTestsBase | Bounded periodic queue, atomic providers, leases and retries. |
| REQ-009 | OperationProfilingQueryTests; OperationProfilingDashboardTests; Phase 10 browser evidence | Slow/Recent/By count, shared filters and coalesced refresh. |
| REQ-010 | OperationProfilingWallTimeTests; OperationProfilingDashboardTests; Phase 10 browser evidence | Exclusive wall-time, parallel overlap, partial coverage and bars. |
| REQ-011 | OperationRuntimeCorrelationTests; ProfilingWorkflowIntegrationTests | Exact process identity and observed interval, bidirectional navigation. |
| REQ-012 | ProfilingAnalysisContractTests; OperationProfilingQueryTests; OperationProfilingDashboardTests | Exact ID, full counts, weighted statistics and bounded selections. |
| REQ-013 | OperationProfilingValueTests; OperationProfilerTests; ProfilingAnalysisContractTests | Literal-Z UTC serialization and monotonic elapsed arithmetic. |
| REQ-014 | OperationProfilingMetadataTests; OperationProfilingValueTests; OperationProfilingHttpMetadataTests | Typed dimensions/reducers and bounded adapter fields. |
| REQ-015 | OperationProfilingWriterTests; ProfilingClearContractTests; OperationProfilingDashboardTests; Phase 12 capacity report | Drop/unknown/retention/freshness accounting, including inline memory evictions. |
| REQ-016 | RequestProfilingSamplingTests; RequestProfilingMiddlewareTests | Path default, prefix boundaries and blacklist wildcard rules. |
| REQ-017 | OperationProfilingLoggingTests; OperationProfilingFailureIsolationTests | Trace lifecycle, filtered allocation and throwing-logger containment. |
| REQ-018 | RequestProfilingSamplingTests; Phase 12 sampled trials | AllRequests, probability and node-local rate limit. |
| REQ-019 | JobProfilingBehaviorTests; PipelineProfilingBehaviorTests; OrchestrationProfilingBehaviorTests | Optional feature behavior participation and exactly-once business execution. |
| REQ-020 | ProfilingRegistrationTests; RequestProfilingInjectionTests; JobProfilingBehaviorTests; PipelineProfilingBehaviorTests; OrchestrationProfilingBehaviorTests | Absent/disabled DI works without hidden registrations. |
| REQ-021 | ProfilingClearContractTests; ProfilingWriterLeaseTests; EntityFrameworkOperationProfilingContractTestsBase | Bounded fenced clear/ranges, independent writers, skew and restart recovery. |
| REQ-022 | OperationProfilerTests; ProfilingBroadcastServiceTests; ProfilingControlServiceTests; ProfilingCollectorTests | Cached executing-node identity and retained Runtime Broadcast semantics. |
| REQ-023 | OperationProfilingSegmentTests; ProfilingAnalysisContractTests; EntityFrameworkOperationProfilingContractTestsBase | Case-insensitive canonical keys with original display casing across providers. |
| REQ-024 | OperationProfilingCleanupTests; OperationProfilingFailureIsolationTests; RequestProfilingMiddlewareTests | Expiry and shutdown release capture while business work continues. |

TEST-001–TEST-018 are covered by the named correctness/example suites and Phase 10 browser proof. TEST-019 is covered by the accepted 84-trial matrix and controlled-time writer tests. TEST-020 is covered by the complete MkDocs/API build below and the final Phase 13 rerun. TEST-021 is covered by the complete-application tests and browser verification in Phase 13 below. Runtime archive version 2 roundtrip/remapping, version 1 rejection before mutation, JSON/Perfetto, measurement/evaluation/custom metric/probe and console regressions pass in the workspace unit task. No new production profiling package, compatibility alias, occurrence trace or custom-client endpoint was introduced.

### TASK-055 — documentation and site acceptance

`pwsh -File docs/site/scripts/build-pages.ps1` passed through the strict native-command wrapper. Docker MkDocs synchronized 62 guides and built without warnings. The Release API build staged 65 assemblies and reported five pre-existing unrelated XML-tag warnings (EnumerationValueObject, LocationOptions, ICosmosSqlProvider and CosmosSqlGenericRepository); no profiling warning. DocFX finished with zero warnings/errors and generated 25,683 symbol index entries. The generated site landing, Profiling guide, API landing and IOperationProfiler/ProfilingBuilderContext pages exist; all their relative links resolve. Plan links are rewritten to repository blob links by the documentation synchronization script. Generated namespace parent links without pages are now kept as plain labels by a tested post-generation script integrated into the build. No site was published.

TASK-051–TASK-055 and all feature acceptance requirements are complete. At this checkpoint Phase 13 (TASK-058–TASK-059) remained pending; its completed results follow. Raw stress files and logs remain local under the user-ignored evidence path; the versioned report generator and capacity tables preserve reviewable findings.

### Phase 13 — final application integration and completion, 2026-10-09 UTC

TASK-058 and TASK-059 are complete. The existing development-only WeatherFiesta registration, in-memory provider, OpenID Connect administrator policy, HTTP middleware ordering, compare endpoint and manual stress job remain the application wiring. The job's XML terminology now distinguishes Runtime measurement and operation segments. No application endpoint, database schema or production authentication override was added.

The new `ProfilingEndpointsTests` uses the existing isolated SQL Server application fixture and real Kestrel HTTP responses. It restores the shared ActiveEntity/Rule logger state between child hosts, explicitly enables profiling and its required hosted services, uses fixture-only role authentication and bounds the real job workload. External service collaborators stay controlled. It verifies:

- Default path keys without endpoint opt-in, the response-header ID, method/path/status, actual response bytes/quality, UTC interval, duration and executing-node identity.
- `weather:compare`, typed numeric `cityCount` and `Query` segment outcomes for both rejected and accepted comparisons while preserving business results.
- Natural periodic persistence and exact ID lookup through the authorized dashboard JSON read; no test forces an operation flush.
- Anonymous 401, non-administrator 403 and administrator 200 across Runtime/Operations/Requests, content and internal JSON routes; dashboard/health exclusion from capture.
- Disabled capture/dashboard with an unchanged business endpoint, plus the real non-HTTP job's Cpu/Allocate/Retain segments and Runtime correlation by matching node and timestamps.

| Final application check | Result | Local evidence |
| --- | --- | --- |
| Integration project build | Zero warnings/errors | `phase-13/build.log` |
| Focused profiling application tests | 5 passed, zero failures/skips | `phase-13/profiling-tests.log` |
| Complete WeatherFiesta unit suite | 55 passed, zero failures/skips | `phase-13/weather-unit.log` |
| Complete WeatherFiesta integration suite | 109 passed, zero failures/skips | `phase-13/weather-integration.log` |
| Visible Chromium dashboard verification | 12 checks passed; zero JavaScript errors; maximum one auto-refresh request in flight | `phase-13/browser-results.json`, `browser.log` |
| Complete MkDocs/API pipeline rerun | Exit 0; MkDocs built in 5.68 seconds; DocFX zero warnings/errors; 65 assemblies and 25,683 indexed symbols | `phase-13/site-build.log` |
| Generated-page links and guide scope | 313 relative links resolve across five landing/guide/API pages; generated profiling guide has no WeatherFiesta wording | `phase-13/api-links.log`, `verify-generated-links.py` |

Browser checks cover Slow/Recent/By count, typed grouping, header-ID navigation, horizontal segment bars and duration/percentage tooltips, selectable coalesced refresh, job segments/Runtime link, Runtime session visibility, a mobile viewport, and actual HTTP authorization denial. Six saved desktop/detail/mobile screenshots were visually inspected; operation detail renders all four Runtime overlay charts. The temporary host ran on `http://localhost:5000` with an isolated SQL container; the unrelated existing server was left alone. Both owned hosts stopped and the owned SQL container was deleted after verification.

Initial fixture attempts failed because the client used the wrong Kestrel address, a disposed child logger remained in shared state, and a role-denial expectation incorrectly assumed a redirect. The final fixture and both complete application suites passed after those corrections. Browser attempts also exposed an assertion made before asynchronous content replacement, a script variable shadowing `URL`, and fixture authentication headers being sent to external CDN assets. The final browser script awaits replaced content and attaches its test header only to the local origin; it changes no production asset or authentication behavior. Failed attempts remain in local evidence. Temporary paired-factory teardown emitted a handled Broadcast unregister/disposed-provider warning; shutdown still completed with exit 0 and container cleanup. This is recorded rather than treated as a failed operation or a reason to change Runtime Broadcast semantics.

The strict `docs/site/scripts/build-pages.ps1` rerun exercised the integrated namespace-link postprocessor: 3,717 API pages were repaired before the symbol index was produced. The Release API compiler also reported 36 pre-existing XML-tag warnings in non-profiling Requester, Mapping, Domain, Storage and Cosmos code; none concern profiling. MkDocs emitted its vendor announcement banner but no documentation build/link warning. No site was published.

Per the user's final documentation refinement, `docs/features-profiling.md` and the generic specification remain application-neutral. WeatherFiesta setup and its verified workflow are in `examples/WeatherFiesta/WeatherFiesta-README.md`; the README's local links resolve and its compare route now correctly says POST. Shared feature docs do not mention that example application.

Final review confirmed all 59 tasks complete, all requirement/test mappings covered, no obsolete active Phase APIs, no new production dependency, and no outstanding implementation task. Workspace build/unit/integration evidence and its existing skips are recorded in TASK-054; capacity results and synthetic-workload limitations remain explicit above. Raw measurements, browser harness/screenshots and logs remain local under the user-ignored evidence directory. The committed report generator, result tables, tests and this summary provide the versioned evidence. The user's `.gitignore` edit is preserved separately.

### Post-acceptance defaults refinement, 2026-10-09 UTC

Request Profiling now defaults to the user-selected blacklist: `/_bdk/**`, `/health*`, `/swagger/**`, `/scalar/**` and `/openapi/**`. Explicit `Blacklist(...)` calls replace this collection; `Blacklist()` clears it. Existing wildcard semantics remain unchanged: `/health*` matches root health names such as `/healthz`, while `/health/live` requires a separate pattern. Excluded requests suppress nested operation capture without affecting business execution or subsequent eligible requests.

The shared in-memory provider was already the implicit fallback. Registration tests now explicitly verify that fallback across capability combinations and that a custom provider replaces it and remains selected after repeated setup. Default setup examples, the application registration and its integration fixture omit redundant provider and blacklist calls. The guide, specification and plan defaults describe the same contract; the generic guide and specification remain application-neutral.

| Defaults validation | Result | Local evidence |
| --- | --- | --- |
| Regression before the default change | Seven default-exclusion cases failed as expected | `defaults/red.log` |
| Full solution build | Zero warnings/errors | `defaults/build.log` |
| Common profiling tests | 342 passed, zero failures/skips | `defaults/common.log` |
| Presentation profiling tests | 159 passed, zero failures/skips | `defaults/presentation.log` |
| Application profiling integration using implicit defaults | Five passed, zero failures/skips | `defaults/weather.log` |
| Complete MkDocs/API pipeline | Exit 0; DocFX zero warnings/errors; 65 assemblies and 25,683 indexed symbols | `defaults/site.log` |
| Generated-page links and updated guide | 313 relative links resolve across five pages; the guide includes all five exclusions and replacement/clearing guidance | `defaults/links.log` |

These checks cover default exclusions, case and trailing-slash matching, replacement and clearing, absence of suppression leakage, normal business request capture, provider replacement and real application dashboard access. Queue, persistence and sampling algorithms are unchanged, so the accepted capacity results above remain applicable to their original workload. The user's separate `.gitignore` edit remains excluded from the feature commit.

The full documentation pipeline rerun includes the namespace-link postprocessor (3,717 pages repaired). The Release API compiler reported the same 36 pre-existing XML-tag warnings in non-profiling code; no profiling warning. The generated guide remains application-neutral. No site was published.

### Phase 14 — repository and Active Entity behaviors, 2026-10-09 UTC

TASK-060–TASK-064 are complete. `RepositoryProfilingBehavior<TEntity>` forwards all 25 generic repository overloads through the existing failure-isolated join-or-start helper. It records the awaited call, keeps argument/token/result identity, and never enumerates returned sequences. Independent calls own a `Repository` operation; calls within a live operation contribute child segments. Bounded metadata identifies the full entity type and operation name without retaining entities, IDs, query expressions or results.

`ActiveEntityProfilingBehavior<TEntity>` wraps the complete action through the new optional `IActiveEntityOperationBehavior<TEntity>` contract. Existing lifecycle behavior implementations require no new member. The context scope applies wrappers in registration order, enclosing before hooks, provider execution and after hooks, including early failure returns that skip after hooks. Failed `IResult` values are classified without reading their payloads or raw messages. Repeated and parallel calls have independent scope ownership and aggregate through the existing complete-path rules. Collection-returning convenience operations keep their individual nested outcomes without enumerating the collection for classification.

The original two-argument `ActiveEntityContextScope.UseAsync` signature remains available with the stable `UseContext` profiling name. A cancellation-aware overload uses caller member metadata or an explicit operation name. All 61 cancellation-aware operations forward their original tokens; both custom-context boundaries supply the default token and their calling member name. No profiler or operation wrapper is registered implicitly. `AddProfilingBehavior()` prevents duplicate Active Entity registration and permits optional DI activation when Profiling is omitted.

The application registers profiling on its seven Core Active Entities. Its complete integration suite verifies business behavior, HTTP authorization and diagnostics while the compare endpoint now includes entity segments beneath `Query`. Both Domain guides, the general Profiling guide and the final-state spec describe registration, outcomes, nesting and exact measurement boundaries. Generic feature documentation remains application-neutral.

| Phase 14 validation | Result | Local evidence |
| --- | --- | --- |
| Full solution build | Zero warnings/errors | `phase-14/build.log` |
| Complete Domain unit suite | 593 passed, zero failures/skips; includes all 25 new behavior cases | `phase-14/domain-unit.log` |
| Existing Active Entity InMemory and Entity Framework integration suites | 72 passed, zero failures/skips | `phase-14/active-entity-integration.log` |
| Complete application unit suite | 55 passed, zero failures/skips | `phase-14/weather-unit.log` |
| Complete application integration suite | 109 passed, zero failures/skips, including nested entity profiling and enabled/disabled capture | `phase-14/weather-integration.log` |
| Complete MkDocs/API pipeline | Exit 0; DocFX zero warnings/errors; 65 assemblies and 25,714 indexed symbols | `phase-14/site-build.log` |
| Generated guide/API links | 906 relative links resolve across 11 pages, including both Domain guides and the new behavior/contract pages | `phase-14/links.log`, `verify-generated-links.py` |

The new behavior cases exercise omitted DI registration, independent ownership, repeated reads without enumeration, before/provider/after failed results, synchronous/asynchronous exceptions, matching caller cancellation, disabled capture, suppression, injected start/metadata/disposal faults, nested concurrent entity/repository work, and the legacy/explicit-name context APIs. An initial test draft exposed a private entity type inaccessible to the substitute proxy and repository spacing rules; the final public test entity and corrected assertions/style pass. Source review found no new production dependency, internal modifier, synchronous storage call or blocking async call. The shared queue/provider/sampling algorithms are unchanged. These correctness checks do not establish a production overhead percentage; the earlier capacity evidence retains its documented workload limits.

TASK-064 and Phase 14 are complete. All 64 tasks are implemented and verified. The documentation pipeline repaired 3,720 namespace links; its Release API compiler reported the same 36 pre-existing XML-tag warnings in non-profiling code and no profiling warning. The shared Profiling guide, spec and generated guide have no example-application wording. No site was published. The phase extends REQ-019/REQ-020 coverage with the new Domain behavior cases and preserves the earlier requirement/evidence mappings. Raw logs and the link checker remain local under the user-ignored evidence directory; this summary and the production/tests/docs changes are versioned. The separate `.gitignore` edit remains excluded from the phase commit.

### Phase 15 — Requester and Notifier behaviors, 2026-10-09 UTC

TASK-065–TASK-067 are complete. The three new Common.Utilities behaviors implement the existing two-parameter `IPipelineBehavior` contract with only optional `IOperationProfiler`. `ProfilingRequestBehavior<TRequest,TResponse>` measures a Requester pipeline, `ProfilingNotificationBehavior<TRequest,TResponse>` measures dispatch, and `ProfilingNotificationHandlerBehavior<TRequest,TResponse>` measures each executed handler. Repeated registration through the respective builders is idempotent without changing other behavior ordering. Omitted Profiling returns the original business task directly and registers no profiling dependency.

Independent calls own `Requester`, `Notifier` or `NotifierHandler` operations; awaited work within a caller contributes segments. Sequential dispatch records only executed handlers before an early failed result. Concurrent handlers have separate scopes and aggregate by complete path. Handler keys use short type names to fit normal capture limits; bounded full type names remain metadata, and equal short names share a group. Timing starts at behavior execution, after handler resolution. Retry placement controls full-policy versus per-attempt timing. Messages, options, request IDs and business result payloads are excluded.

Fire-and-forget dispatch measures scheduling. Each detached handler uses the existing safe independent execution boundary, clearing inherited operation, segment and suppression context. Its `NotifierHandler` operation can close after the dispatch and caller have closed, and its failure does not change the scheduling result. Disabled capability and admission limits still apply. The safe shared helpers classify `IResult` failures, preserve business exception/cancellation behavior and isolate recording startup, metadata, completion, disposal and boundary-restoration faults. These adapters do not access storage or start persistence work.

The application registers all three behaviors before its other Requester/Notifier behaviors. Its real HTTP profiling comparison now verifies a Requester segment beneath `Query` alongside nested Active Entity work. The application README, Requester/Notifier guide, shared Profiling guide and final-state specification describe the same boundaries; the shared profiling documentation remains application-neutral.

| Phase 15 validation | Result | Local evidence |
| --- | --- | --- |
| Initial Common.Utilities build | Zero warnings/errors | `phase-15/initial-build.log` |
| Full solution build | Zero warnings/errors, 2 minutes 54.18 seconds | `phase-15/build.log` |
| Complete Common unit suite | 2,752 passed, zero failures; four existing serializer benchmarks skipped; includes all 43 new behavior cases | `phase-15/common-unit.log` |
| Complete application unit suite | 55 passed, zero failures/skips | `phase-15/weather-unit.log` |
| Complete application integration suite | 109 passed, zero failures/skips; nested Requester capture, disabled capture and existing authorization/business behavior preserved | `phase-15/weather-integration.log` |
| Complete MkDocs/API pipeline | Exit 0; DocFX zero warnings/errors; 65 assemblies and 25,723 indexed symbols | `phase-15/site-build.log` |
| Generated guide/API links | 1,163 relative links resolve across 15 pages, including the new three behavior APIs and Requester/Notifier guide; the new profiling guide anchor exists | `phase-15/links.log`, `verify-generated-links.py` |

Focused checks first passed 40 cases. Three additional cases verify original task identity when Profiling is omitted; all 43 subsequently passed within the complete Common suite. Initial drafts failed because they used incorrect DTO property names, compared newly boxed Result structs by reference, or used a cancellation assertion helper that synthesized a different exception. The final fixtures use the actual duration/path contracts, preserve a single boxed result where reference identity is meaningful, and observe cancellation directly. Full handler-name keys exceeded the default key limit, so the final keys use short type names and metadata keeps the full names. A full-suite run exposed shared handler discovery caching the concrete test handler type; registering that fixture instance through both its concrete type and interface fixed the three affected cases. Failed attempts remain local as evidence.

Source review found no new package, internal modifier, synchronous storage access, business retry or raw payload capture. The shared recorder, sampling, queue and persistence algorithms are unchanged. Earlier capacity evidence retains its original synthetic-workload limits; these new opt-in behavior boundaries have correctness coverage, without a new stress benchmark or production overhead percentage.

TASK-068 and Phase 15 are complete. All 68 tasks are implemented and verified. The final API pipeline repaired 3,723 namespace links. Its Release compiler reported 36 pre-existing XML-tag warnings in non-profiling code, with none in the new behaviors; DocFX reported zero warnings/errors. No site was published. Source and generated profiling documentation remain application-neutral. Raw logs and the link checker remain local under the ignored evidence directory; this summary records the versioned results. The user's separate `.gitignore` edit remains excluded from the phase commit.


## Phase 16 — messaging and queue handler profiling (2026-10-09 UTC)

TASK-069 through TASK-071 implement and verify optional `MessageHandlerProfilingBehavior` and `QueueHandlerProfilingBehavior`. They use the existing handler contracts and shared failure-isolated join-or-start helpers. Keys identify the runtime message and handler types; bounded full type dimensions and an existing short string correlation ID describe the work without retaining message bodies, business IDs or arbitrary property values. Typed/instance setup is idempotent; factory registrations retain the existing builder semantics.

The common `IProfilingExecutionBoundaryBehavior` capability lets both broker bases establish a safe independent consumer boundary in `ProcessSubscriptionHandler`. This covers transports entering through `Process` and EF workers entering through individual stored subscriptions, without changing broker constructors or transport rules. Inherited producer operations, segments and suppression are cleared for each real consumer pipeline and restored afterward. Direct behavior calls join an existing owner and honor suppression. Nested work aggregates by complete path. Consumer capture can finish after producer capture closes.

Timing begins at the profiling behavior after handler resolution/deserialization and any pre-pipeline semaphore wait. Publishing/enqueuing, queue residence, transport receive and acknowledgement remain outside it. Behavior ordering determines complete retry-policy versus exposed-attempt timing; redelivery creates a new operation. Returned Tasks are completed outcomes, exceptions are failed outcomes, and only requested cancellation carrying the supplied handler token is canceled. Observation startup, metadata, terminal recording, disposal and restoration preserve exactly-once handler execution and its original exception.

Every operation already captures the cached executing-process node. The new adapters retain that recorder identity rather than trusting producer properties. Tests verify hostname, PID, process-start UTC, independent ownership, and query inclusion/exclusion by node GUID. The existing dashboard node GUID filter applies to records, groups and distributions. Shared EF storage supports several nodes; in-memory history remains local. Runtime correlation may enrich a stored node's private descriptor without changing its stable identity or public process fields.

The example server enables both behaviors before its other handler behaviors. Its real EF-backed messaging and queueing pipelines verify independent roots, periodic visibility and executing-node selection. The application README, shared Profiling guide, messaging/queueing guides and final-state spec describe these same boundaries. Generic profiling documentation remains application-neutral.

| Verification | Result | Local evidence |
| --- | --- | --- |
| Initial Application project build | Zero warnings/errors | `phase-16/initial-build.log` |
| Focused handler contract suite | 58 passed, zero failures/skips | `phase-16/focused.log` |
| Complete Application unit suite | 1,298 passed, zero failures/skips, including all 58 new cases | `phase-16/application-unit.log` |
| EF and RabbitMQ broker integration regressions | 70 passed, zero failures/skips; SQLite, SQL Server, PostgreSQL and RabbitMQ | `phase-16/infrastructure-integration.log` |
| Focused real application broker checks | Two passed, zero failures/skips | `phase-16/weather-focused-final.log` |
| Complete application unit suite | 55 passed, zero failures/skips | `phase-16/weather-unit.log` |
| Complete application integration suite | 111 passed, zero failures/skips; includes the two new broker cases and existing live external API cases | `phase-16/weather-integration-final.log` |
| Full solution build | Zero warnings/errors, 2 minutes 4.64 seconds | `phase-16/build.log` |
| Complete MkDocs/API pipeline | Exit 0; DocFX zero warnings/errors; 65 assemblies and 25,730 indexed symbols | `phase-16/site-build.log` |
| Generated guide/API links | 1,518 relative links resolve across 20 pages; handler/node guide anchors and all three new APIs present | `phase-16/links.log`, `verify-generated-links.py` |

Commands ran sequentially. Application units used `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore --nologo`; the focused run added `--filter FullyQualifiedName~BrokerHandlerProfilingBehaviorTests`. Infrastructure integration used `--filter 'FullyQualifiedName~EntityFramework&FullyQualifiedName~BrokerTests|FullyQualifiedName~RabbitMQ&FullyQualifiedName~BrokerTests'`. The final application integration run used `dotnet test examples/WeatherFiesta/WeatherFiesta.IntegrationTests/WeatherFiesta.IntegrationTests.csproj --no-build --no-restore --nologo`. The app unit and final focused fixture builds used `-p:BuildProjectReferences=false` after their unchanged dependencies had been built; the subsequent complete solution build verified every reference. The solution command was `dotnet build bITdevKit.slnx --no-restore --nologo`.

Initial contract drafts had an ambiguous messaging/queueing constants import, an incorrect path member name and missing analyzer-required blank lines. These were corrected before the 58-case and complete unit passes. The first complete application integration attempt passed 108 and failed three cases: a live Open-Meteo request timed out, while the two new fixtures assumed identical subscription startup and compared a complete stored node record against its unenriched cache. Source inspection established that EF messaging subscribes at broker construction and Queueing uses a hosted subscription service removed by this fixture. The final test uses the existing messaging subscription, explicitly supplies only the queue subscription and compares stable public node fields. A redundant unchanged retry was interrupted to apply those corrections. The focused tests and complete 111-case suite then passed, including the unchanged external API tests. Closed failed/interrupted logs remain local under the ignored evidence directory.

Source review found no new package, internal modifier, synchronous persistence, business retry or payload capture. Shared recording, sampling, queue and provider algorithms remain unchanged. Earlier synthetic capacity evidence keeps its original workload limits; this optional handler extension has correctness coverage without a new stress benchmark or production overhead claim. TASK-072 and Phase 16 are complete; all 72 tasks are implemented and verified. The complete documentation command was `/tmp/bitdevkit-pwsh/pwsh -NoLogo -NoProfile -File /tmp/bitdevkit-build-pages.ps1`, whose strict wrapper invokes `docs/site/scripts/build-pages.ps1`. Generated links were checked with `python3 plan/evidence/profiling-runtime-and-operations-1/phase-16/verify-generated-links.py`. The pipeline repaired 3,726 API namespace pages. The Release API compiler reported 36 existing XML-tag warnings outside the new profiling code; DocFX reported zero warnings/errors. No site was published. Raw logs and the link checker remain local under the ignored evidence directory. The user's separate `.gitignore` edit is excluded from this phase commit.
