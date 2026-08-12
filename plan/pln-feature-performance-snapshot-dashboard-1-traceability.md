# Profiling acceptance-criteria traceability

This Phase 14 audit maps every acceptance-criteria bullet in
`docs/specs/spec-performance-snapshot-dashboard.md`, in canonical order, to its principal
implementation and automated verification. The matrix intentionally references behavior-focused
tests rather than line numbers so it remains useful after formatting and cleanup.

| ID | Requirement | Principal implementation | Automated verification | Status |
|---|---|---|---|---|
| AC-001 | Dashboard starts an enabled short session | `DashboardEndpoints`; `ProfilingControlService` | `OperationalRoutes_WithValidRequests_DelegateEveryOperationToSharedServices`; `SingleNodeWorkflow_StartCollectMarkAnalyzeStopExportClear_CompletesEndToEnd` | Covered |
| AC-002 | One logical active session | `IProfilingStore.GetOrCreateActiveSessionAsync`; both stores | `GetOrCreateActiveSessionAsync_CompetingRequests_ReturnOneSession`; `CompetingStarts_AcrossStoreInstances_CreateOneActiveSession` | Covered |
| AC-003 | Concurrent starts resolve once and broadcast once | `ProfilingControlService.StartAsync` | `StartAsync_ConcurrentRequests_CreateOneSessionAndPublishOnce` | Covered |
| AC-004 | Start uses Broadcast without command polling | `ProfilingBroadcastService`; `ProfilingCollectorHostedService` | `PublishAsync_LateRegistration_TargetsProfilingPreparedSetOnly`; `HostedService_IdleRuntime_PerformsOnlyOneStartupStoreInspection` | Covered |
| AC-005 | Any active node may initiate; no master | `ProfilingControlService`; `ProfilingBroadcastService` | `TwoNodeWorkflow_LateNodeSnapshot_JoinsAsAdHocWithoutChangingExpectedSet` | Covered |
| AC-006 | Current scoped registration snapshot is targeted | `ProfilingBroadcastService.PrepareTargetsAsync` | `PublishAsync_LateRegistration_TargetsProfilingPreparedSetOnly` | Covered |
| AC-007 | Timely accepted nodes form fixed participants | `ProfilingControlService.RecordExpectedParticipantsAsync` | `StartAsync_MixedOutcomes_RecordsOnlyAcceptedExpectedParticipants` | Covered |
| AC-008 | Replacement start atomically replaces local collection | `ProfilingCollector.StartAsync` | `StartAsync_NewerSession_ReplacesOlderLocalCollector`; `StartAsync_UnstoredNewerSession_DoesNotReplaceCurrentCollector` | Covered |
| AC-009 | Nonaccepted delivery outcomes are reported and excluded | `ProfilingControlService`; `ProfilingNodeOutcome` | `StartAsync_MixedOutcomes_RecordsOnlyAcceptedExpectedParticipants`; `Snapshot_WithAcceptedNode_UsesImmediateOutcomeTerminology` | Covered |
| AC-010 | Idempotent finalization and startup reconciliation | `ProfilingSessionFinalizer`; `ProfilingStartupReconciler` | `FinalizeAsync_CompetingCallers_CompleteSessionOnce`; `ReconcileAsync_OverdueDurableSession_FinalizesWithWarningsOnlyOnce` | Covered |
| AC-011 | Snapshot timing, counters, sequence, keys, and process metadata | `ProfilingSnapshot`; `ProfilingSnapshotProbe`; `ProfilingCollector` | `CaptureAsync_ValidRequest_PreservesUtcAndMonotonicTiming`; `ScheduledCapture_FailedThenSuccessful_PreservesTotalsAndSequence` | Covered |
| AC-012 | Capture is single-flight and busy opportunities are skipped | `ProfilingCollector` capture gate and absolute schedule | `ScheduledCapture_SlowProbe_SkipsAbsoluteOpportunitiesWithoutOverlap`; `CaptureAsync_WhileScheduledCaptureIsActive_RemainsSingleFlight` | Covered |
| AC-013 | CPU, allocation, and GC rates use monotonic time | `ProfilingSnapshotProbe`; `ProfilingEvaluationCalculations` | `CaptureAsync_ConsecutiveSamples_ComputesCpuAndAllocationRates`; `EvaluateAsync_UtcClockMovement_UsesMonotonicIntervals` | Covered |
| AC-014 | Latest GC and latest Gen2 evidence are preserved | `SystemProfilingRuntimeSnapshotSource`; `ProfilingGcObservationState` | `CaptureAsync_DirectGcEvidence_MapsLatestAndLatestGen2Independently`; `ObserveGcEvidence` benchmark | Covered |
| AC-015 | Immutable runtime context is stored once per session node | `ProfilingRuntimeContextFactory`; both stores | `Create_RuntimeValues_MapsOnlyApprovedNonSensitiveContext`; `Records_AllSupportedKinds_RoundTripWithoutMutation` | Covered |
| AC-016 | Public communication uses eight-character lowercase keys | identity models; query/control/dashboard/console adapters | `CreateIdentities_AlwaysUseEightCharacterLowercaseKeys`; `GetNodeSessionAsync_SerializedReadModel_ContainsNoInternalGuids`; `Data_WithSelectedSession_UsesPublicKeysAndOmitsInternalGuids` | Covered |
| AC-017 | Dashboard selects expected or ad-hoc node timelines | `ProfilingQueryService.GetNodeSessionAsync`; dashboard pages | `DashboardPage_WithExpectedAndAdHocNodes_RendersOneNodeSelectorAndShareableContext`; `GetSessionAsync_ExpectedAndAdHocContributors_ReturnsBoth` | Covered |
| AC-018 | Dashboard exposes supported runtime metric groups | `ProfilingSnapshot`; `Data.cshtml`; `Index.cshtml` | `DashboardPage_WithSelectedNode_RendersDeveloperWorkbenchAndTwoPrimaryCharts`; `CaptureAsync_SystemRuntime_CapturesCoreRawMetricsWithoutMutation` | Covered |
| AC-019 | Missing metrics render unavailable, not zero | nullable snapshot fields; dashboard formatting | `CaptureAsync_UnavailableRuntimeMetrics_RemainsSuccessfulWithNullEvidence`; `DashboardPage_WithSelectedNode_RendersDeveloperWorkbenchAndTwoPrimaryCharts` | Covered |
| AC-020 | Dense current grid and exactly two primary charts | dashboard Razor pages | `DashboardPage_WithSelectedNode_RendersDeveloperWorkbenchAndTwoPrimaryCharts` | Covered |
| AC-021 | Segments appear on the timeline | `ProfilingSegment`; dashboard chart model | `DashboardPage_WithAnnotations_RendersDistinctChartAndDetailContracts` | Covered |
| AC-022 | Phase markers work through dashboard, console, and API | `ProfilingControlService.AddPhaseMarkerAsync`; endpoint and command adapters | `AddPhaseMarkerAsync_ActiveSession_TrimsAndAllowsDuplicateNames`; `OperationalRoutes_WithValidRequests_DelegateEveryOperationToSharedServices`; `SingleNodeWorkflow_StartCollectMarkAnalyzeStopExportClear_CompletesEndToEnd` | Covered |
| AC-023 | Invalid or inactive phase markers do not mutate or broadcast | `ProfilingControlService.AddPhaseMarkerAsync` | `AddPhaseMarkerAsync_InvalidName_FailsWithoutStoreMutation`; `Mark_WithoutActiveSession_WritesCoreStateFailure` | Covered |
| AC-024 | Dashboard refresh interval is configurable | `ProfilingOptions.RefreshInterval`; dashboard view model | `AddProfiling_Defaults_UseApprovedConservativeValues`; `DashboardPage_WhenRendered_ContainsPersistenceConfirmationAndAccessibilityContracts` | Covered |
| AC-025 | Sub-500 ms intervals and nonpositive durations are rejected | `ProfilingOptions`; start validation | `SamplingInterval_BelowMinimum_ThrowsArgumentOutOfRangeException`; `Duration_NonPositive_ThrowsArgumentOutOfRangeException` | Covered |
| AC-026 | Session metadata and developer actions are available | `ProfilingQueryService`; `DashboardEndpoints`; dashboard pages | `UpdateMetadataAsync_DoesNotMutateStoredObservations`; `OperationalRoutes_WithValidRequests_DelegateEveryOperationToSharedServices`; `PathBuilders_WithSelections_PreserveReadableShareableKeys` | Covered |
| AC-027 | Confirmed dashboard clear includes pinned data | `DashboardEndpoints.ClearAsync`; store `ClearAsync` | `Clear_WithoutConfirmation_ReturnsBadRequestWithoutMutation`; `ClearAsync_TerminalPinnedAndUnpinnedData_RemovesEverythingAtomically` | Covered |
| AC-028 | Clear rejects active sessions without mutation | both stores; control/query adapters | `ClearAsync_ActiveSession_RejectsWithoutChangingState`; `Clear_WhenSessionIsActive_MapsCoreStateFailureToConflict` | Covered |
| AC-029 | Clear empties storage and tombstones delayed writes | both stores | `DeletedSession_DelayedRecordsCannotRecreateState`; `ClearAndDelayedSnapshot_Compete_ClearRemainsAtomicAndTombstoneRejectsLaterWrite` | Covered |
| AC-030 | Start, active check, and clear share lifecycle coordination | both stores | `StartAndClearAsync_CompetingMutations_LeaveOneCompleteActiveSession`; `ConcurrentLifecycleStress_StartStopClearFinalizeRetentionAndSnapshots_PreservesStoreInvariants` | Covered |
| AC-031 | Active session deletion is rejected | both stores; `ProfilingControlService` | `QueryLifecycle_ActiveDeleteAndClear_AreRejectedWithoutMutation` | Covered |
| AC-032 | Restart stops and preserves the selected session before replacement | `ProfilingControlService.RestartAsync` | `RestartAsync_CopiesOnlyApprovedSessionParameters` | Covered |
| AC-033 | Idle manual snapshot creates a completed one-shot session | `ProfilingControlService.SnapshotAsync` | `SnapshotAsync_WithoutActiveSession_CompletesStandaloneSession` | Covered |
| AC-034 | Manual snapshot targets all registrations and records ad-hoc contributors | `ProfilingControlService.SnapshotAsync`; snapshot handler | `SnapshotAsync_ActiveSession_TargetsLateNodeWithoutChangingExpectedSet`; `TwoNodeWorkflow_LateNodeSnapshot_JoinsAsAdHocWithoutChangingExpectedSet` | Covered |
| AC-035 | Local storage rejects multi-target start/snapshot before broadcast | `ProfilingControlService.PrepareTargetsAsync` | `StartAsync_MultipleTargetsAndLocalStore_DoesNotMutateOrPublish`; `ProcessLocalStores_TwoTargets_RejectBeforeMutationOrHttpPublication` | Covered |
| AC-036 | Exactly two same-node snapshots can be compared | `ProfilingQueryService.CompareSnapshotsAsync`; comparison UI | `CompareSnapshotsAsync_ProducesSignedDeltasAndSafePercentages`; `CompareSnapshotsAsync_WrongNodeOrOrder_ReturnsTypedFailure` | Covered |
| AC-037 | Application code creates scoped sessions and segments | `IProfilingMeasurementService`; `ProfilingMeasurementService` | `BeginAsync_WithoutActiveSession_OwnsAndStopsCreatedSession`; `BeginAsync_NestedScopes_AssignsSameNodeParentAndAllowsOverlap` | Covered |
| AC-038 | `profiling` and `prof` expose all required operations | Profiling console command registrations | `AddConsoleCommands_WhenCalledRepeatedly_RegistersOneOfEachProfilingCommand` | Covered |
| AC-039 | Console reuses the shared control service and Broadcast behavior | console command adapters | `Start_WithFriendlyOptions_DelegatesParsedOverridesToCoreService`; `Snapshot_WithAcceptedNode_UsesImmediateOutcomeTerminology` | Covered |
| AC-040 | Console start applies optional values and shared defaults | `ProfilingStartConsoleCommand` | `Start_WithFriendlyOptions_DelegatesParsedOverridesToCoreService`; `Start_WithoutOptions_LeavesDefaultsForCoreService` | Covered |
| AC-041 | Feature-local duration parser accepts supported formats | `ProfilingDurationParser` | `DurationParser_WithSupportedValue_ParsesExpectedDuration`; `DurationParser_WithUnsupportedValue_ReturnsFalse` | Covered |
| AC-042 | Console analysis supports timeline/pair/JSON without persistence | `ProfilingAnalyzeConsoleCommand`; evaluator | `Analyze_WithValidSelection_DelegatesTimelineOrPair`; `Analyze_WithJson_WritesExactComputedContractWithoutPersistingIt` | Covered |
| AC-043 | Existing `diag perf` and `diag gc` remain local | existing diagnostics plus separate Profiling commands | `DiagPerf_WithProfilingCommandsRegistered_RemainsLocalPointInTimeCommand` | Covered |
| AC-044 | Console status and delivery output use immediate outcome semantics | `ProfilingConsoleCommandBase` | `Snapshot_WithAcceptedNode_UsesImmediateOutcomeTerminology`; `Status_WhenControlServiceMissing_WritesUnavailableWithoutThrowing` | Covered |
| AC-045 | Disabled or unavailable console operations fail safely | console adapters and typed errors | `Start_WhenDisabled_WritesSafeTypedFailure`; `Status_WhenControlServiceMissing_WritesUnavailableWithoutThrowing` | Covered |
| AC-046 | Console clear requires `--yes` and uses shared reset | `ProfilingClearConsoleCommand`; `IProfilingControlService.ClearAsync` | `Clear_WithoutYes_ChangesNothingAndExplainsConfirmation`; dashboard/store clear tests | Covered |
| AC-047 | An outer scope owns a newly created session | `ProfilingMeasurementService.BeginAsync` | `BeginAsync_WithoutActiveSession_OwnsAndStopsCreatedSession` | Covered |
| AC-048 | A scope joins an existing active session | `ProfilingMeasurementService.BeginAsync` | `BeginAsync_WithActiveSession_JoinsWithoutStoppingSession` | Covered |
| AC-049 | Raw scopes default to success unless marked otherwise | `ProfilingMeasurementScope` | `DisposeAsync_RawFailure_StoresSafeExceptionMetadataWithoutStackTrace`; successful scope tests | Covered |
| AC-050 | Execution helpers record success, failure, and cancellation | `ProfilingMeasurementService.MeasureAsync` | `MeasureAsync_ThrowingOperation_RecordsFailureAndRethrowsOriginalException`; `MeasureAsync_CancelledOperation_RecordsCancellationAndRethrows` | Covered |
| AC-051 | Failed operations store type/message but no stack trace | `ProfilingMeasurementScope.MarkFailed` | `DisposeAsync_RawFailure_StoresSafeExceptionMetadataWithoutStackTrace` | Covered |
| AC-052 | Segment ownership, overlap, and parent constraints are enforced | both stores; ambient segment context | `BeginAsync_NestedScopes_AssignsSameNodeParentAndAllowsOverlap`; `UpsertSegmentAsync_CrossNodeOrCrossSessionParent_RejectsReference` | Covered |
| AC-053 | Collection may end while a measured segment remains open | `ProfilingMeasurementScope.CompleteCoreAsync` | `DisposeAsync_AfterCollectionDuration_ClosesSegmentWithoutStoppingSession` | Covered |
| AC-054 | Stop is best-effort and preserves the original end | `ProfilingControlService.StopAsync`; collector safety duration | `StopAsync_UnreachableNode_StopsLogicalSessionAndPreservesOriginalEnd`; `TwoNodeWorkflow_MissedStop_IsBestEffortAndPreservesOriginalEnd` | Covered |
| AC-055 | Incomplete expected participants produce warnings only | `ProfilingSessionFinalizer` | `ReconcileAsync_OverdueDurableSession_FinalizesWithWarningsOnlyOnce`; `FinalizeAsync_FailedAdHocContributor_DoesNotCreateCompletionWarning` | Covered |
| AC-056 | Custom metrics are stable, node-owned, separate, and segment-aware | `ProfilingCustomMetricListener`; metric observation storage | `FlushAsync_ActiveSession_StoresCounterGaugeAndDurationObservations`; `MetricCallback_InsideMeasuredScope_InheritsAmbientSegment`; high-cardinality rejection tests | Covered |
| AC-057 | In-memory provider supports local development | `InMemoryProfilingStore`; default `AddProfiling` registration | `InMemoryProfilingStoreContractTests`; `DoFiestaRegistration_Profiling_IsDevelopmentOnlyAndUsesDefaultStore` | Covered |
| AC-058 | EF provider is durable and shared-store capable | `EntityFrameworkProfilingStore` | EF provider contract tests; SQLite/SQL Server/PostgreSQL Profiling integration suites | Covered |
| AC-059 | Retention removes oldest unpinned terminal sessions first | both stores | `ApplyRetentionAsync_OldUnpinnedSessions_RemovesOldestAndPreservesPinned`; `ApplyRetentionAsync_DurableStore_PreservesPinnedAndNewestTerminalSessions` | Covered |
| AC-060 | Disabled feature leaves normal behavior unaffected | conditional `AddProfiling` registration | `AddProfiling_Disabled_RegistersOnlyInertApplicationSurfaces`; `AddDashboard_WhenDisabled_RegistersNoProfilingPresentationServices` | Covered |
| AC-061 | One evaluator supports timeline or same-node pair mode | `IProfilingEvaluationService`; `ProfilingEvaluator` | `EvaluateAsync_PairMode_ValidatesKeysScopeAndOrdering`; timeline evaluator tests | Covered |
| AC-062 | Evaluation is deterministic, on demand, local, fixed, unscored, and unpersisted | `ProfilingEvaluator`; fixed private rules | `EvaluateAsync_SameInput_ReturnsStructurallyEqualResult`; `EvaluateAsync_ReadsOnceAndPerformsNoStoreWrites`; contract-shape test | Covered |
| AC-063 | Provisional state and five-snapshot/five-second gate | `ProfilingEvaluationCalculations`; evaluator | `EvaluateAsync_MinimumTimelineWindow_GatesSignals`; `EvaluateAsync_SessionState_SetsProvisionalAndTerminalLimitations` | Covered |
| AC-064 | Fixed CPU/memory/allocation/GC labels, confidence, evidence, and actions | `ProfilingEvaluationRules` | `EvaluateAsync_FixedRuleBoundaries_AreInclusive`; `EvaluateAsync_FixedSignals_UseApprovedLabelsAndActions`; stronger-signal test | Covered |
| AC-065 | KPIs remain independent and secondary metrics emit no generic signals | `ProfilingEvaluationCalculations`; fixed rule set | `EvaluateAsync_CounterResetAndMissingMetrics_ExcludeAffectedEvidence`; evaluator contract and fixed-signal tests | Covered |
| AC-066 | Data quality exposes coverage, failures, p95 duration, and delay | `ProfilingEvaluationCalculations` | `EvaluateAsync_P95Values_UseNearestRank`; `EvaluateAsync_MissingSequence_AddsDataQualityLimitation` | Covered |
| AC-067 | Quality thresholds and debugger cap High confidence | `ProfilingEvaluationCalculations`; confidence helper | `EvaluateAsync_SamplingAndDebuggerLimitations_CapHighConfidence`; `EvaluateAsync_HighConfidenceWindow_RequiresTenSnapshotsAndSeconds` | Covered |
| AC-068 | Browser-wide Live analysis defaults off and is nonoverlapping/cadenced | dashboard JavaScript in `Index.cshtml` | `DashboardPage_WhenRendered_DoesNotEvaluateAndLiveAnalysisDefaultsOff`; `DashboardPage_WhenRendered_ContainsPersistenceConfirmationAndAccessibilityContracts` | Covered |
| AC-069 | Dashboard does not persist/copy/export evaluation; raw export remains | dashboard endpoints and UI | `Export_ReturnsRawSnapshotJsonAndNoEvaluationExportRouteExists`; `DashboardPage_WithComparisonAndAnalysis_RendersOrderingAndNoEvaluationExportControls` | Covered |
| AC-070 | Explicit non-goals remain absent | project boundaries; node-scoped query/evaluator contracts | architecture/static audit plus `EvaluationResult_ContainsOnlyApprovedTopLevelGroups`; dashboard route/UI tests | Covered |

## Phase 14 audit outcome

- All 70 canonical acceptance criteria have implementation and automated-verification evidence.
- No acceptance criterion requires a new package, project, migration, polling loop, AI integration,
  overall score, cross-node evaluation, or other deferred capability.
- Runtime and provider stress coverage is shared through the public provider contract fixture so the
  same lifecycle invariants execute against both the in-memory and Entity Framework providers.
- Phase 14 human acceptance was recorded on 2026-08-08; all implementation-plan gates are complete.

## Phase 14 validation record

Validation was run on 2026-08-08 with .NET 10.0.10 on Windows 11 and a 12th Gen Intel Core
i7-12800HX (24 logical processors).

| Benchmark | Mean | Managed allocation | Approved budget | Outcome |
|---|---:|---:|---:|---|
| Raw system runtime sample | 12.026 ms | 113,361 B | Informational component cost | Recorded |
| Fixed-source probe and snapshot model | 669.56 ns | 1,968 B | Below 1 ms | Pass |
| Complete system-backed snapshot | 12.244 ms | 114,697 B | Below 25 ms | Pass |
| GC observation | 29.13 ns | 64 B | Below 10 us | Pass |
| Runtime-context capture | 23.31 us | 1,872 B | Below 10 ms | Pass |
| In-memory snapshot append | 1.095 us | 414 B | Below 1 ms per snapshot | Pass |

The complete capture consumes approximately 1.22% of a 1-second interval and 2.45% of the minimum
500-ms interval on this machine. Disabled registration resolves no collector or hosted service, and
idle hosting performs one startup store inspection without starting a recurring loop. Dedicated
collector tests verify that scheduled and manual captures remain single-flight, slow captures skip
absolute opportunities, and maximum observed probe concurrency remains one.

The solution build passed with zero warnings and errors. All five unit-test projects produced a clean
pass after one unrelated transient Broadcast HTTP timeout was rerun (5,072 passed and 5 intentionally
skipped). Domain integration tests passed 43/43. Application integration tests passed 284/284, and
Infrastructure integration tests passed 1,136 with 127 configuration-gated Cosmos tests skipped.
Profiling-specific integration tests passed 17/17 across SQLite, SQL Server, and PostgreSQL.

The two repository-integration failures found during the initial hardening run were resolved before
recording the final totals. The SQLite change-history schema test queried the obsolete
`__ChangeHistory` table name and now verifies the mapped `__ChangeHistory_Entries` table. The
real-time file-watcher test used fixed 500-ms delays and now waits, with a bounded timeout, for the
required observable event states. A subsequent full Application run exposed a process-wide messaging
subscription race; subscription mutations are now synchronized and all broker/provider consumers
enumerate stable snapshots. Focused messaging tests passed 6/6, followed by the clean complete
Application and Infrastructure integration runs above.
