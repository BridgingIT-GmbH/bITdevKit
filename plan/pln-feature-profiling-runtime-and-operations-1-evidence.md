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
- TASK-003 through TASK-055: incomplete.

## Requirement acceptance

REQ-001 through REQ-024 remain unverified for the new implementation. Baseline tests describe existing Runtime behavior only.
