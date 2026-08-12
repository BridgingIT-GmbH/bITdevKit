---
goal: Implement the approved Profiling Dashboard specification through bounded, review-gated agent phases
version: 1.0
date_created: 2026-08-07
last_updated: 2026-08-08
owner: bITdevKit maintainers
status: 'Completed'
tags: [feature, profiling, performance, diagnostics, dashboard, broadcasting, entity-framework, console-commands, developer-tooling]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

This plan transforms `docs/specs/spec-performance-snapshot-dashboard.md` into bounded, copy-paste execution prompts for AI coding agents. It does not implement the feature. The plan deliberately stabilizes contracts, runtime collection, concurrency, persistence, and distributed control before adding evaluation, queries, Console Commands, or dashboard UI. Every phase ends with sequential build, test, diff, and human-review checkpoints. No agent may continue into the next phase without explicit human approval.

The implementation shall use existing projects:

- `src/Common.Utilities/Profiling` for provider-neutral contracts, models, in-memory storage, runtime collection, control, scoped measurement, custom metric capture, queries, and deterministic evaluation.
- `src/Infrastructure.EntityFramework/Profiling` for Entity Framework entities, context capability, durable provider, transactions, concurrency, retention, and reset behavior.
- `src/Presentation.Web/Profiling` for Console Commands, dashboard endpoints, dashboard pages, rendering models, JSON/export responses, and registration.
- Existing test projects under `tests/Common.UnitTests`, `tests/Infrastructure.UnitTests`, `tests/Infrastructure.IntegrationTests`, and `tests/Presentation.UnitTests`.

Do not create a production project, test project, migration, general-purpose profiler, APM subsystem, or reusable abstraction not required by the approved specification.

## Architecture Analysis Prompt

```text
You are executing the architecture-analysis phase for the approved Profiling Dashboard.

Read completely:
- AGENTS.md
- .github/copilot-instructions.md
- docs/specs/spec-performance-snapshot-dashboard.md
- plan/pln-feature-performance-snapshot-dashboard-1.md
- src/Common.Utilities/Broadcasting/*
- src/Common.Utilities/Metrics/*
- src/Common.Utilities/Hosting/PeriodicBackgroundService.cs
- src/Infrastructure.EntityFramework/Broadcasting/*
- src/Presentation.Web/Broadcasting/*
- src/Presentation.Web/Metrics/*
- src/Presentation.Web/Dashboard/*
- src/Presentation/ConsoleCommands/*

Do not edit production or test files.

Implementation focus: validate architecture placement, existing extension points, requirement coverage, and baseline repository health.

Implementation exclusions: do not write code, alter contracts, add files, resolve contradictions by guessing, or begin a later phase.

Architectural rules: preserve Clean Architecture dependency direction, reuse the existing Broadcasting/Metrics/dashboard/Console Command seams, and introduce no new project or infrastructure subsystem.

Produce an architecture report that:
1. Confirms the dependency direction Common.Utilities -> no Infrastructure or Presentation dependency.
2. Confirms Entity Framework implements core store contracts without leaking DbContext or entity types.
3. Confirms Presentation.Web depends on core application-facing services and never implements a second lifecycle.
4. Maps each approved requirement to one implementation phase in this plan.
5. Lists the exact existing extension points that will be reused.
6. Identifies any contradiction between the approved specification and the current repository.
7. Confirms that no new project, package, migration, polling loop, AI analysis, overall score, or cross-node evaluation is needed.

Required tests:
- baseline repository build;
- Common unit tests;
- Presentation unit tests;
- read-only diff/worktree verification.

Validation expectations: distinguish pre-existing failures from plan blockers, cite exact files and commands, and require human approval before implementation.

Run these checkpoints sequentially:
- dotnet build
- dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --nologo
- dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --nologo
- git diff --check
- git status --short

Return the architecture report, command results, and any blocking contradiction. Stop after the report. Do not begin implementation.
```

## Shared Governance Instructions

```text
Apply these instructions to every implementation phase:

1. Treat docs/specs/spec-performance-snapshot-dashboard.md as the authoritative behavior contract.
2. Obey AGENTS.md, .github/copilot-instructions.md, .editorconfig, Clean Architecture boundaries, XML documentation requirements, and existing naming conventions.
3. Implement only the named phase. Do not anticipate later phases with placeholders, unused interfaces, speculative extension points, or future-proofing code.
4. Do not create a new production project, test project, NuGet dependency, migration, background polling mechanism, or general monitoring subsystem.
5. Preserve unrelated working-tree changes. Never reformat or modify files outside the phase scope.
6. Use internal Guid identifiers and public immutable eight-character lowercase keys from KeyGenerator.CreateLowercase(8) exactly as specified.
7. Keep provider-neutral runtime code under src/Common.Utilities/Profiling. It must not reference Entity Framework, ASP.NET Core, Razor, Spectre.Console, or Infrastructure types.
8. Keep EF entities and DbContext behavior under src/Infrastructure.EntityFramework/Profiling. Do not expose those types through core contracts.
9. Keep dashboard and Console Command code under src/Presentation.Web/Profiling. Reuse the one core control/query/evaluation implementation.
10. Reuse Common.Utilities Broadcasting for direct typed start, stop, manual snapshot, and GC commands. Do not poll the performance store or registry for commands and do not persist Broadcast envelopes or delivery history.
11. Preserve one logical deployment-wide active session, fixed expected participants, ad-hoc manual-snapshot contributors, best-effort stop, node-local collector replacement, idempotent finalization, and invalid-session write rejection.
12. Never overlap snapshot captures. Use node-local monotonic timing for rates and UTC only for display/cross-node alignment.
13. Keep evaluation deterministic, server-side, computed on demand, and unpersisted. Do not add AI, configurable rules, plugin rules, overall scores, cross-node evaluation, session comparison, or arbitrary intervals.
14. Keep Live analysis browser-wide, stored in localStorage, and off by default. It controls only automatic dashboard evaluation.
15. Keep normal raw snapshot JSON export separate from computed evaluation. Do not add evaluation export, copy, or persistence.
16. Do not change existing diag perf or diag gc semantics.
17. Add focused tests in the existing matching test project during the same phase as behavior. Do not defer phase-critical tests.
18. Run top-level build and test commands sequentially. Never run concurrent dotnet build or dotnet test commands in the same worktree.
19. End every phase with: focused tests, dotnet build, git diff --check, git status --short, an architecture-boundary review, and a human-review stop.
20. Do not start the next phase until a human explicitly approves the current phase.
21. Prefer public accessibility for profiling feature contracts, models, services, and injectable runtime adapters. Use private or file-scoped implementation details where possible; use `internal` only when there is a concrete assembly-boundary reason. All public symbols must have XML documentation and client-facing usage examples. Do not add public symbols without documentation.
22. For Broadcast-backed control, use a Profiling-owned adapter that prepares one immutable active-target snapshot, validates store capability against that snapshot, and delegates publication to the unchanged Broadcast service using that exact snapshot. Do not add Profiling-specific APIs or behavior to the standalone Broadcasting feature, and do not perform a second live registry read that can change the target set between validation and delivery.
23. Correlate each Broadcast registration by its private Broadcast node identity plus process-start timestamp to one stable Profiling node record. Public communication continues to use only the Profiling node's eight-character key.
24. Scheduled collection uses absolute monotonic deadlines with explicit missed-opportunity accounting. Reuse hosting and TimeProvider conventions, but do not implement the schedule as a fixed-delay `PeriodicBackgroundService` loop.
25. The reusable provider contract remains public in `Common.UnitTests`; `Infrastructure.UnitTests` references that test project normally. Do not source-link test files.
```

## 1. Requirements & Constraints

- **REQ-001**: Implement the approved specification without changing its functional scope.
- **REQ-002**: Keep core public contracts, models, runtime behavior, provider abstraction, in-memory provider, evaluation, and programmatic services in `src/Common.Utilities/Profiling`.
- **REQ-003**: Keep durable storage in `src/Infrastructure.EntityFramework/Profiling` behind provider-neutral core interfaces.
- **REQ-004**: Keep dashboard and Console Commands in `src/Presentation.Web/Profiling`; both shall delegate to the same core services.
- **REQ-005**: Generate internal `Guid` and public eight-character lowercase alphanumeric keys for sessions, nodes, and snapshots.
- **REQ-006**: Support idle, running, completed, completed-with-warnings, stopped, and failed session states.
- **REQ-007**: Permit only one logical deployment-wide active session and serialize start, active checks, phase-marker writes, and clear-all through the store.
- **REQ-008**: Support scheduled collection, manual one-off snapshots, manual GC, shared phase markers, scoped segments, custom metrics, session metadata, restart, deletion, retention, pinning, export, and clear-all.
- **REQ-009**: Use node-local monotonic time for CPU, allocation, GC rate, capture duration, and sampling delay calculations.
- **REQ-010**: Record successful, skipped, and failed capture totals and prohibit overlapping node-local capture work.
- **REQ-011**: Capture the complete approved runtime snapshot metric set, direct latest-GC/latest-Gen2 evidence, and immutable per-session node runtime context.
- **REQ-012**: Use the existing `IMetricsService`/DevKit meter surface for custom metrics without adding a second application-facing metrics API.
- **REQ-013**: Use DevKit Broadcasting direct typed delivery for start, stop, manual snapshot, and GC; `Accepted` remains admission rather than handler completion.
- **REQ-014**: Reject multi-node session start and standalone manual snapshot before store mutation or broadcast when the store does not support multi-node operation.
- **REQ-015**: Treat start accepted nodes as the fixed expected participant set; registered nonparticipants may contribute only ad-hoc manual snapshots.
- **REQ-016**: Implement best-effort stop, node-local replacement, late-write window validation, compare-and-set finalization, and startup reconciliation.
- **REQ-017**: Provide in-memory and Entity Framework stores with the same observable lifecycle contract.
- **REQ-018**: Make Entity Framework start/clear/active coordination transactional and concurrency-safe without adding repository-owned migrations.
- **REQ-019**: Apply retention to unpinned terminal sessions only; support explicit selected deletion, unpinned bulk deletion, and confirmed complete reset.
- **REQ-020**: Compute deterministic single-node analysis for either two snapshots or the complete available node timeline.
- **REQ-021**: Implement the exact approved CPU, memory, allocation, GC, confidence, data-quality, label, and suggested-action rules.
- **REQ-022**: Return evaluation groups `Scope`, `DataQuality`, `KPIs`, `Signals`, and `Limitations`; never persist results.
- **REQ-023**: Provide application-facing control, scoped measurement, query, raw export, and evaluation services using readable public keys.
- **REQ-024**: Register grouped `profiling`/`prof` Console Commands with status, start, stop, snapshot, GC, mark, clear, and analyze operations.
- **REQ-025**: Provide the approved dashboard layout with one selected node, current cards, exactly two primary Plotly charts, segments/markers, comparison, analysis, metadata, export, and lifecycle controls.
- **REQ-026**: Store dashboard node/filter/refresh state and the browser-wide Live analysis switch in localStorage; default Live analysis to off.
- **REQ-027**: Apply periodic dashboard responses as keyed live-region updates; do not replace the complete Profiling content tree or reset browser-owned interaction state.
- **REQ-027**: Keep the feature opt-in and disabled by default; unavailable infrastructure shall fail safely without crashing the host.
- **SEC-001**: Reuse dashboard authorization; do not weaken host authentication or add anonymous management endpoints.
- **SEC-002**: Do not capture request payloads, business content, command-line arguments, environment values, filesystem paths, credentials, or other sensitive machine data.
- **SEC-003**: Do not log or expose stack traces, sensitive values, or internal GUIDs through public results.
- **CON-001**: Do not create new projects, migrations, packages, profilers, tracing systems, alerting systems, or production monitoring infrastructure.
- **CON-002**: Do not add request analytics, session-to-session comparison, cross-node aggregation/evaluation, node ranking, baselines, configurable thresholds, health scores, or AI.
- **CON-003**: Do not persist evaluation output, custom metrics inside runtime snapshots, or generic Broadcast history.
- **CON-004**: Do not add automatic retries or durable commands solely to compensate for missed stop broadcasts.
- **PAT-001**: Follow `src/Common.Utilities/Broadcasting` for provider-neutral runtime registration, typed handlers, capability models, safe Results, and source-generated logs.
- **PAT-002**: Follow `src/Infrastructure.EntityFramework/Broadcasting` for application-owned DbContext capability and operation-owned EF scopes.
- **PAT-003**: Follow `src/Presentation.Web/Broadcasting/ConsoleCommands` for grouped commands and concise Spectre.Console output.
- **PAT-004**: Follow `src/Presentation.Web/Broadcasting/Dashboard` and `src/Presentation.Web/Metrics/Dashboard` for authorized Razor-slice plugins, refresh behavior, and action endpoints.
- **PAT-005**: Use `window.bdkDashboard.loadPlotly` and `createPlotlyLayout` from `src/Presentation.Web/Dashboard/Pages/_DashboardLayout.cshtml`; do not add a second charting library.
- **PAT-006**: Use existing Result, TimeProvider, hosted-service, logging, metrics, and DI conventions.

## 2. Implementation Steps

### Implementation Phase 0 — Architecture Analysis

- **GOAL-000**: Validate file placement, dependency direction, existing extension points, and baseline health before editing code.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Execute the Architecture Analysis Prompt without changing production or test files. | Yes | 2026-08-07 |
| TASK-002 | Map every approved specification section to Phases 1-14 and identify shared-file ownership. | Yes | 2026-08-07 |
| TASK-003 | Run baseline build, Common unit tests, Presentation unit tests, diff check, and worktree inspection sequentially. | Yes | 2026-08-07 |
| TASK-004 | Record any contradiction or unavailable dependency as a blocker; do not resolve it by inventing scope. | Yes | 2026-08-07 |
| TASK-005 | Obtain human approval of the architecture report before Phase 1. | Yes | 2026-08-07 |

Completion criteria:

- **GATE-000**: Baseline build and selected tests pass or pre-existing failures are documented with evidence.
- **GATE-001**: Human reviewer approves the project placement and phase boundaries.
- **GATE-002**: No implementation file changed.

### Implementation Phase 1 — Foundation Contracts

- **GOAL-001**: Establish the minimal provider-neutral type system, options, errors, identities, store contract, and registration surface.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-006 | Add `src/Common.Utilities/Profiling/ProfilingOptions.cs` with approved defaults and validation. | Yes | 2026-08-07 |
| TASK-007 | Add `ProfilingModels.cs` for sessions, stable Profiling nodes, private Broadcast identity/process-start correlation, participation, snapshots, runtime context, segments, markers, custom metrics, query models, and public-key references. | Yes | 2026-08-07 |
| TASK-008 | Add `ProfilingAbstractions.cs` for the store and the minimal probe, node identity, collector, control, query, scoped measurement, and evaluator seams required by the approved phases. | Yes | 2026-08-07 |
| TASK-009 | Add `ProfilingErrors.cs` with safe typed Result errors for disabled, unavailable, invalid key, invalid state, shared-store-required, and validation outcomes. | Yes | 2026-08-07 |
| TASK-010 | Add `ProfilingRegistration.cs` and `ProfilingServiceCollectionExtensions.cs` with re-entrant `AddProfiling()` options registration and no active runtime behavior. | Yes | 2026-08-07 |
| TASK-011 | Add contract/options/identity tests under `tests/Common.UnitTests/Utilities/Profiling`. | Yes | 2026-08-07 |
| TASK-012 | Document every new public symbol with XML comments and usage examples. | Yes | 2026-08-07 |

#### Prompt 1 — Foundation Contracts

```text
Implement Phase 1 only: provider-neutral Profiling foundation contracts.

Read the approved spec, this plan, AGENTS.md, KeyGenerator.cs, Common.Utilities/Broadcasting contracts, Metrics options/registration, and existing Result error patterns.

Create only:
- src/Common.Utilities/Profiling/ProfilingOptions.cs
- src/Common.Utilities/Profiling/ProfilingModels.cs
- src/Common.Utilities/Profiling/ProfilingAbstractions.cs
- src/Common.Utilities/Profiling/ProfilingErrors.cs
- src/Common.Utilities/Profiling/ProfilingRegistration.cs
- src/Common.Utilities/Profiling/ProfilingServiceCollectionExtensions.cs
- focused tests under tests/Common.UnitTests/Utilities/Profiling

Define only types required by the approved spec. Use BridgingIT.DevKit.Common for core public types and Microsoft.Extensions.DependencyInjection for registration extensions. Use internal Guid IDs and public keys from KeyGenerator.CreateLowercase(8). Model all terminal states, participant/ad-hoc roles, immutable runtime context, snapshots, markers, segments, custom observations, provider SupportsMultiNode, evaluation contract groups, and safe operation Results. Model Broadcast node identity plus process-start timestamp as private correlation data for resolving one stable Profiling node record; never expose that correlation in public transport models.

Do not implement a store, collector, timer, evaluator, Broadcast handler, EF entity, endpoint, command, or UI. Do not create placeholder implementations for later phases.

Required tests:
- exact option defaults and invalid minimum interval/duration values
- eight-character lowercase session/node/snapshot keys
- immutable public-key behavior and internal/public identifier separation
- state and role enum coverage
- disabled registration performs no runtime work
- repeated registration is idempotent and does not add competing providers

Checkpoints:
1. dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --nologo
2. dotnet build
3. git diff --check
4. git status --short
5. Review the dependency graph and prove Common.Utilities has no new Infrastructure or Presentation reference.

Return the changed-file list, tests, command results, and unresolved issues. STOP and wait for human approval.
```

Completion criteria:

- **GATE-003**: The contracts describe the approved feature without unused extension points.
- **GATE-004**: Common unit tests and repository build pass.
- **GATE-005**: Human reviewer approves naming and contract boundaries.

### Implementation Phase 2 — In-Memory Store and Atomic Lifecycle

- **GOAL-002**: Implement the provider contract in memory with atomic session lifecycle, immutable observations, deletion, reset, and retention behavior.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-013 | Add `src/Common.Utilities/Profiling/InMemoryProfilingStore.cs`. | Yes | 2026-08-07 |
| TASK-014 | Implement one process-local lifecycle lock covering active lookup, create, phase-marker admission, stop transition, and clear-all. | Yes | 2026-08-07 |
| TASK-015 | Implement immutable append/read operations for participation, context, snapshots, markers, segments, action markers, and custom metrics. | Yes | 2026-08-07 |
| TASK-016 | Implement selected deletion, unpinned bulk deletion, full reset, invalid-session write rejection, and retention. | Yes | 2026-08-07 |
| TASK-017 | Implement public-key lookup without exposing internal GUIDs. | Yes | 2026-08-07 |
| TASK-018 | Add a reusable provider-contract test fixture under `tests/Common.UnitTests/Utilities/Profiling/ProfilingStoreContractTests.cs`. | Yes | 2026-08-07 |
| TASK-019 | Add concurrent start/clear/write, stable Broadcast-registration-to-Profiling-node mapping, and retention tests for the in-memory provider. | Yes | 2026-08-07 |

#### Prompt 2 — In-Memory Store and Atomic Lifecycle

```text
Implement Phase 2 only: the in-memory performance store and provider contract tests.

Use the Phase 1 contracts exactly. Add InMemoryProfilingStore and tests under tests/Common.UnitTests/Utilities/Profiling. Keep all state process-local and guard lifecycle mutations with one explicit process-local lock/coordination mechanism.

Implement:
- atomic get-or-create active session
- atomic active-state validation for phase markers
- one active logical session
- participant/ad-hoc participation
- atomic get-or-create Profiling node mapping by private Broadcast node identity plus process-start timestamp
- immutable runtime context and observations
- key and Guid lookup
- stop/completion/failure transitions
- late-write acceptance only for existing sessions and original collection windows
- rejection after deletion, clear, or retention
- selected deletion, unpinned bulk deletion, clear-all, and retention
- pinned-session exclusion from automatic retention
- SupportsMultiNode = false

Do not implement sampling, timers, Broadcast, EF, evaluation, Console Commands, endpoints, or UI. Do not add a repository abstraction above the store.

Required tests:
- competing starts return one active session
- start versus clear cannot interleave into partial state
- marker write versus stop is atomic
- duplicate immutable append is idempotent only where the contract requires it
- deleted/cleared/expired sessions reject every late record type
- pinned retention and deletion semantics
- empty clear succeeds with zero removals
- provider contract covers all record types and public-key lookup
- repeated resolution of one Broadcast registration returns one stable Profiling node while a changed process-start timestamp creates a new node

Checkpoints:
1. dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --nologo
2. dotnet build
3. git diff --check
4. git status --short
5. Review locking scope, immutable-record enforcement, and absence of EF/Presentation dependencies.

Return evidence and STOP for human review.
```

Completion criteria:

- **GATE-006**: Provider-contract and concurrency tests pass deterministically.
- **GATE-007**: No lifecycle check-then-act race remains in the in-memory implementation.
- **GATE-008**: Human reviewer approves the observable store contract before runtime work.

### Implementation Phase 3 — Runtime Snapshot Probe

- **GOAL-003**: Capture one accurate runtime snapshot and immutable runtime context without session scheduling or distributed control.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-020 | Add `src/Common.Utilities/Profiling/Runtime/ProfilingNodeIdentityProvider.cs`. | Yes | 2026-08-07 |
| TASK-021 | Add `Runtime/ProfilingRuntimeContextFactory.cs`. | Yes | 2026-08-07 |
| TASK-022 | Add `Runtime/ProfilingSnapshotProbe.cs` with approved process, memory, GC, allocation, thread-pool, and socket metrics. | Yes | 2026-08-07 |
| TASK-023 | Add `Runtime/ProfilingGcObservationState.cs` for cumulative pause and direct latest-GC/latest-Gen2 evidence. | Yes | 2026-08-07 |
| TASK-024 | Add deterministic monotonic timing and unavailable-value handling. | Yes | 2026-08-07 |
| TASK-025 | Add probe/context tests using injectable process/runtime/platform adapters only where required for deterministic tests. | Yes | 2026-08-07 |
| TASK-026 | Verify probe capture does not mutate session state or perform storage. | Yes | 2026-08-07 |

#### Prompt 3 — Runtime Snapshot Probe

```text
Implement Phase 3 only: capture one runtime snapshot and one runtime-context record.

Create files under src/Common.Utilities/Profiling/Runtime and focused Common unit tests. Reuse TimeProvider/Stopwatch and standard .NET runtime/process/network APIs. Add only narrow public adapters needed to make platform-dependent values testable; keep implementation-only state private or file-scoped.

Capture every metric listed in the approved specification, including cumulative process CPU duration, processor count, allocation total/rate inputs, GC counts, pause state, latest GC and latest Gen2 index/generation/post-collection heap/LOH/compacting/concurrent evidence, memory, fragmentation, thread-pool, and socket counts. Represent unavailable values as unavailable, never zero.

Capture immutable node context once through a separate factory. Exclude command-line arguments, environment values, paths, credentials, and business data.

The probe captures one sample only. It does not schedule, persist, start sessions, broadcast, collect GC manually, or evaluate signals.

Required tests:
- stable process-lifetime node Guid/key/hostname/PID
- UTC display timestamp plus monotonic elapsed fields
- CPU/allocation raw cumulative inputs
- direct latest-Gen2 evidence mapping
- unavailable platform metric behavior
- debugger/runtime/OS/architecture context
- no sensitive context fields
- no exception escapes for ordinary unsupported metrics

Checkpoints:
1. dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --nologo
2. dotnet build
3. git diff --check
4. git status --short
5. Review allocation behavior, platform safety, XML documentation, and absence of scheduling/storage concerns.

Return evidence and STOP for human review.
```

Completion criteria:

- **GATE-009**: One probe produces complete, bounded, provider-neutral data.
- **GATE-010**: Unsupported metrics are unavailable rather than false zero values.
- **GATE-011**: Human reviewer approves metric correctness before scheduling.

### Implementation Phase 4 — Node-Local Collector and Session Lifecycle

- **GOAL-004**: Implement single-flight scheduled collection, local replacement, auto-stop, finalization, and reconciliation over the in-memory provider.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-027 | Add `src/Common.Utilities/Profiling/Runtime/ProfilingCollector.cs`. | Yes | 2026-08-07 |
| TASK-028 | Add `Runtime/ProfilingCollectorHostedService.cs` for lifecycle ownership without idle store polling. | Yes | 2026-08-07 |
| TASK-029 | Add `Control/ProfilingSessionFinalizer.cs` and `Control/ProfilingStartupReconciler.cs`. | Yes | 2026-08-07 |
| TASK-030 | Enforce one active node-local collector and atomic replacement by a valid newer session. | Yes | 2026-08-07 |
| TASK-031 | Implement absolute monotonic-deadline sample timing, missed/no-overlap skipping, failed counts, final participation totals, and automatic duration stop. | Yes | 2026-08-07 |
| TASK-032 | Implement idempotent local start/stop/manual capture entry points used by later control layers. | Yes | 2026-08-07 |
| TASK-033 | Add fake-time lifecycle, overlap, replacement, late-write, finalization, and startup reconciliation tests. | Yes | 2026-08-07 |
| TASK-034 | Verify idle runtime performs no session-store command polling. | Yes | 2026-08-07 |

#### Prompt 4 — Node-Local Collector and Lifecycle

```text
Implement Phase 4 only: node-local collection and lifecycle runtime using the in-memory store.

Create ProfilingCollector, its hosted lifecycle adapter, session finalizer, and startup reconciler. Use TimeProvider/fake time for deterministic tests. Schedule from absolute monotonic deadlines so slow captures do not silently convert the cadence into fixed-delay sampling. The collector must execute at most one capture at a time. If one or more scheduled opportunities arrive while capture is active, account for each missed opportunity as skipped and do not overlap. Count failed captures separately. Snapshot sequence increments only on successful captures. Reuse hosting lifecycle conventions, but do not derive the schedule from `PeriodicBackgroundService` because its post-iteration delay cannot represent scheduled opportunities missed by slow capture.

Implement valid-start idempotency, atomic replacement of an older local collector, duration-based stop, explicit local stop, final participation totals, and manual local capture. Implement compare-and-set finalization after end plus grace and startup reconciliation for abandoned sessions. Reconciliation may inspect overdue lifecycle state at startup; do not add continuous command polling.

Do not add Broadcast publication/handlers, EF, segments, custom metrics, evaluation, commands, endpoints, or UI.

Required tests:
- exact 500 ms minimum and default timing behavior
- no overlapping captures under a deliberately slow probe
- skipped/failed/success totals and sequence semantics
- manual capture while scheduled collection is active
- duplicate start and stop idempotency
- newer valid start replaces the older local collector
- missed stop continues only until original end
- any finalizer caller can complete once; competing calls are harmless
- startup reconciliation completes overdue sessions
- no idle command polling

Checkpoints:
1. dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --nologo
2. dotnet build
3. git diff --check
4. git status --short
5. Review time semantics, cancellation, single-flight behavior, hosted-service shutdown, and store boundaries.

Return evidence and STOP for human review.
```

Completion criteria:

- **GATE-012**: Fake-time lifecycle tests are stable and prove no overlapping capture.
- **GATE-013**: Finalization and reconciliation are idempotent.
- **GATE-014**: Human reviewer approves local runtime semantics before durable/distributed work.

### Implementation Phase 5 — Entity Framework Persistence and Concurrency

- **GOAL-005**: Implement durable shared storage with transactional lifecycle coordination, optimistic concurrency, query support, retention, and reset.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-035 | Add `src/Infrastructure.EntityFramework/Profiling/IProfilingContext.cs`. | Yes | 2026-08-07 |
| TASK-036 | Add six physical Profiling entities for session aggregates, stable nodes, mutable participations, immutable snapshots, immutable custom observations, and invalid-session tombstones; model low-volume session-owned records as owned JSON collections. | Yes | 2026-08-07 |
| TASK-037 | Map the six tables with the `__Profiling_` prefix, session-owned JSON columns, hot/high-volume row relationships and indexes, bounded lengths, and aggregate/participation concurrency. | Yes | 2026-08-07 |
| TASK-038 | Add `EntityFrameworkProfilingStore.cs` using operation-owned scopes/DbContexts. | Yes | 2026-08-07 |
| TASK-039 | Add transactional get-or-create active, phase-marker admission, clear-all, terminal transitions, and compare-and-set finalization. | Yes | 2026-08-07 |
| TASK-040 | Add retention, deletion, immutable append, public-key lookup, and late-write rejection. | Yes | 2026-08-07 |
| TASK-041 | Add `Profiling/ServiceCollectionExtensions.cs` with explicit provider conflict validation and `SupportsMultiNode = true`. | Yes | 2026-08-07 |
| TASK-042 | Reference `Common.UnitTests` from `Infrastructure.UnitTests`, reuse its public provider-contract fixture without source-linking, and add EF model/provider tests under `tests/Infrastructure.UnitTests/EntityFramework/Profiling`. | Yes | 2026-08-07 |
| TASK-043 | Add SQLite-first integration tests plus SQL Server/PostgreSQL variants under `tests/Infrastructure.IntegrationTests/EntityFramework/Profiling`. | Yes | 2026-08-07 |

#### Prompt 5 — Entity Framework Persistence and Concurrency

```text
Implement Phase 5 only: the Entity Framework performance store.

Follow Infrastructure.EntityFramework/Broadcasting and existing Jobs storage conventions. Add IProfilingContext, EntityFrameworkProfilingStore<TContext>, fluent provider registration, and a model-builder extension that the consuming DbContext invokes from OnModelCreating. The consuming application owns migrations; do not add one.

Use operation-owned DI scopes and DbContexts. Never retain a scoped DbContext in singleton runtime services. Serialize active-session creation, active checks, phase-marker admission, stop/finalize transitions, and clear-all through relational transactions plus a concurrency constraint/token. Handle expected optimistic-concurrency or unique-active-session races with one bounded reload/retry where existing repository conventions support it.

Use exactly six physical Profiling tables: session aggregates, stable nodes, mutable node participations, immutable runtime snapshots, immutable custom metric observations, and invalid-session tombstones. Map tags, runtime contexts, phase/action markers, and segments with nested tags as owned JSON collections on the session. Keep snapshots and custom observations relational because they are append-only high-volume timelines; keep participations relational because counters are updated on the collection hot path and nodes must not contend on the entire session document; keep nodes relational because they are reused between sessions; keep tombstones relational because they outlive deleted sessions. Advance the session concurrency token for every owned-document mutation, retain participation concurrency, use the bounded transactional retry path, and validate referenced node identities in the store because JSON children cannot carry relational node foreign keys.

Persist all approved records. Keep runtime snapshots immutable. Preserve node context once per session/node. Reject writes for missing/deleted/expired sessions. Implement SupportsMultiNode = true, retention, pinned rules, selected deletion, unpinned bulk deletion, and atomic full reset.

Do not implement Broadcast, commands, dashboard, evaluation, or migrations.

Make the provider-contract fixture in `Common.UnitTests` public and add a normal `ProjectReference` from `Infrastructure.UnitTests` to `Common.UnitTests`. Do not source-link or duplicate the fixture.

Required tests:
- exactly six physical Profiling tables plus the approved owned JSON columns
- EF model key/index/relationship/length/aggregate-concurrency metadata
- same provider contract as in-memory
- competing start, clear, phase-marker, and finalization transactions
- immutable append and public-key lookup
- late-write rejection after delete/clear/retention
- operation-owned DbContext scopes
- SQLite integration always
- SQL Server/PostgreSQL contracts when their established test environments are available; report unavailable providers explicitly

Checkpoints:
1. dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --nologo
2. dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --nologo --filter FullyQualifiedName~Profiling
3. dotnet build
4. git diff --check
5. git status --short
6. Review SQL portability, transaction boundaries, no migration, and no EF leakage into Common.Utilities.

Return evidence and STOP for human review.
```

Completion criteria:

- **GATE-015**: In-memory and EF providers pass one observable contract.
- **GATE-016**: Transaction/concurrency tests prove one active session and atomic clear.
- **GATE-017**: Human reviewer approves schema and provider behavior before distributed control.

### Implementation Phase 6 — Distributed Broadcast Control

- **GOAL-006**: Implement deployment-wide start, stop, manual snapshot, and GC using the existing typed Broadcasting feature.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-044 | Add `src/Common.Utilities/Profiling/Control/ProfilingBroadcastModels.cs`. | Yes | 2026-08-07 |
| TASK-045 | Add `Control/ProfilingBroadcastHandlers.cs` for node-local start, stop, snapshot, and GC admission. | Yes | 2026-08-07 |
| TASK-046 | Add `Control/ProfilingControlService.cs` as the single dashboard/console/programmatic control path. | Yes | 2026-08-07 |
| TASK-047 | Add a Profiling-owned Broadcast adapter that prepares one immutable active-target snapshot, validates store capability against it, and delegates publication to the unchanged Broadcast service using that exact snapshot before session creation or standalone manual snapshot. | Yes | 2026-08-07 |
| TASK-048 | Record accepted nodes as the fixed expected participants and preserve immediate delivery outcomes separately from handler completion. | Yes | 2026-08-07 |
| TASK-049 | Implement all-registration active-session manual snapshot and ad-hoc contributor semantics. | Yes | 2026-08-07 |
| TASK-050 | Implement best-effort stop and manual `GC.Collect()` without finalizer wait or LOH compaction. | Yes | 2026-08-07 |
| TASK-051 | Register typed handlers through the existing shared Broadcasting builder without creating a registry or transport. | Yes | 2026-08-07 |
| TASK-052 | Add single-node, multi-node, duplicate-delivery, mixed-outcome, and provider-capability tests. | Yes | 2026-08-07 |

#### Prompt 6 — Distributed Broadcast Control

```text
Implement Phase 6 only: deployment-wide control through existing Common.Utilities Broadcasting.

Create typed payloads and exactly one handler for start, stop, manual snapshot, and GC. Implement ProfilingControlService as the only application-facing control orchestrator. Add a Profiling-owned adapter over the existing Broadcast service and handler registration. Keep the standalone Broadcasting contracts and implementation unchanged. Do not access an EF type or add another transport.

Before store mutation or broadcast, prepare one immutable active-target snapshot through the Profiling-owned adapter. Validate the performance store capability against that exact snapshot, then delegate delivery to the unchanged Broadcast service through a snapshot-backed registry view so it cannot reread a changing live target set. If more than one target exists and the performance store reports SupportsMultiNode=false, return shared-store-required and send nothing. Do not apply this restriction to GC. Add focused Profiling tests proving validation and delivery use the same target set and proving the public Broadcasting contract remains unchanged.

Start must atomically resolve/create one logical session, publish once, and record Accepted nodes as fixed expected participants. Nodes missing/rejecting the new start remain nonparticipants. Stop is best effort. Manual snapshot always targets all current registrations; active-session accepting nonparticipants become ad-hoc contributors without affecting expected-participant warnings. Standalone manual snapshot creates one session and derives participants from Accepted results. GC performs only GC.Collect().

Accepted means queue admission, not completed local work. Keep broadcasts idempotent by broadcast/session identity. Persist no generic Broadcast history.

Do not add segments, custom metrics, evaluation, Console Commands, endpoints, or UI.

Required tests:
- no broadcast or store mutation on shared-store-required
- one active session under concurrent starts
- fixed accepted participant set and mixed delivery outcomes
- late registration does not join automatically
- active manual snapshot targets expected plus nonparticipant registrations
- ad-hoc contributor does not change completion warnings
- standalone manual snapshot completion
- best-effort stop and original-end behavior
- GC without session and no session creation
- duplicate delivery safety

Checkpoints:
1. dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --nologo
2. dotnet build
3. git diff --check
4. git status --short
5. Review Broadcast semantics against docs/specs/spec-common-utilities-broadcasting.md and prove no polling/history/handler-completion claim.

Return evidence and STOP for human review.
```

Completion criteria:

- **GATE-018**: Distributed control tests preserve every accepted/nonparticipant/ad-hoc distinction.
- **GATE-019**: Provider validation happens before mutation and publication.
- **GATE-020**: Human reviewer approves distributed semantics.

### Implementation Phase 7 — Programmatic Scopes, Segments, Markers, and Custom Metrics

- **GOAL-007**: Add the application authoring surface without coupling it to the runtime collector implementation.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-053 | Add `src/Common.Utilities/Profiling/Scopes/ProfilingMeasurementService.cs`. | Yes | 2026-08-07 |
| TASK-054 | Add `Scopes/ProfilingMeasurementScope.cs` and `Scopes/ProfilingSegmentContext.cs`. | Yes | 2026-08-07 |
| TASK-055 | Implement owning session scopes, active-session joining, segment outcomes, parent validation, interruption, and duration safety maximum. | Yes | 2026-08-07 |
| TASK-056 | Add phase-marker control to the shared application-facing service with active-session atomic validation. | Yes | 2026-08-07 |
| TASK-057 | Add `Metrics/ProfilingCustomMetricListener.cs` using `MeterListener` against the existing DevKit meter. | Yes | 2026-08-07 |
| TASK-058 | Associate stable custom metric observations with session, producing node, and ambient segment without a second metrics API. | Yes | 2026-08-07 |
| TASK-059 | Ensure no active session produces negligible metric-listener work and no stored observation. | Yes | 2026-08-07 |
| TASK-060 | Add scope/outcome/parent/overlap/interruption/marker/custom-metric tests. | Yes | 2026-08-07 |
| TASK-061 | Verify stack traces, dynamic metric names, and high-cardinality dimensions are not persisted. | Yes | 2026-08-07 |

#### Prompt 7 — Programmatic Authoring and Custom Metrics

```text
Implement Phase 7 only: scoped programmatic measurement, phase markers, and existing-metrics capture.

Build the authoring surface over IProfilingControlService and IProfilingStore; do not expose collector internals. An owning scope starts/stops a session only when none is active. A scope opened during an active session creates a node-owned segment and closes only that segment. Implement raw scope defaults, Measure/MeasureAsync helpers, success/failure/cancellation, exception type/message without stack trace, overlap, same-session/same-node parent validation, incomplete/interrupted finalization, and collection-ended-before-operation metadata.

Add immutable phase markers through the shared control service: active session only, trimmed nonempty name up to 100 characters, duplicate names allowed, no Broadcast, no individual edit/delete.

Capture custom metrics through a MeterListener on the existing BridgingIT DevKit meter. Do not add a second application API or require metric registration. Record stable identifiers, supported counter/gauge/duration observations, node, session, timestamp, kind/unit, and ambient segment. Keep callback work bounded and non-throwing; outside an active session perform only the minimal active-state check and retain nothing.

Do not implement evaluation, queries, commands, endpoints, or UI.

Required tests:
- owning versus joining scopes
- raw and helper outcomes
- failed/cancelled exception metadata without stack trace
- overlapping segments and valid/invalid parent references
- duration expiry while segment remains open
- interrupted open segment on node/session finalization
- marker validation/atomic active check/immutability
- custom metric session/node/ambient-segment association
- no custom metric retention while idle
- dynamic/high-cardinality rejection according to the spec

Checkpoints:
1. dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --nologo
2. dotnet build
3. git diff --check
4. git status --short
5. Review authoring/runtime separation and existing metrics API compatibility.

Return evidence and STOP for human review.
```

Completion criteria:

- **GATE-021**: Scopes cannot accidentally stop a session they do not own.
- **GATE-022**: Metrics capture requires no new caller API and stores nothing while idle.
- **GATE-023**: Human reviewer approves programmatic semantics.

### Implementation Phase 8 — Deterministic Evaluation

- **GOAL-008**: Implement the complete fixed single-node evaluation contract as a pure, on-demand application service.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-062 | Add `src/Common.Utilities/Profiling/Evaluation/ProfilingEvaluator.cs`. | Yes | 2026-08-07 |
| TASK-063 | Add `Evaluation/ProfilingEvaluationCalculations.cs` for monotonic rates, halves, p95, coverage, deltas, and data quality. | Yes | 2026-08-07 |
| TASK-064 | Add `Evaluation/ProfilingEvaluationRules.cs` containing only the approved fixed CPU/memory/allocation/GC thresholds. | Yes | 2026-08-07 |
| TASK-065 | Implement exact pair and complete-node-timeline modes with key resolution and same-session/node validation. | Yes | 2026-08-07 |
| TASK-066 | Implement provisional/terminal status, minimum sample windows, confidence caps, evidence, labels, actions, and limitations. | Yes | 2026-08-07 |
| TASK-067 | Suppress weaker duplicate signals and isolated CPU peaks exactly as specified. | Yes | 2026-08-07 |
| TASK-068 | Add table-driven rule-boundary tests for every threshold and just-below/at/above cases. | Yes | 2026-08-07 |
| TASK-069 | Add determinism, missing-data, counter-reset, debugger, clock-change, and data-quality tests. | Yes | 2026-08-07 |
| TASK-070 | Prove evaluation performs no writes, network calls, AI calls, rule discovery, or persistence. | Yes | 2026-08-07 |

#### Prompt 8 — Deterministic Evaluation

```text
Implement Phase 8 only: the pure deterministic ProfilingEvaluator.

Read the complete Deterministic Evaluation section of the approved spec before coding. Implement exactly two modes:
1. two snapshots from the same session/node, A before B, always Low confidence;
2. the complete available timeline for one node/session, provisional while active.

Use node-local monotonic deltas for CPU, allocation, GC rates, and pause burden. Implement time-weighted averages, temporal halves, nearest-rank p95, sampling coverage, invalid-interval exclusion, meaningful relative-plus-absolute memory floors, and direct latest-Gen2 evidence.

Implement every fixed CPU, memory, allocation, and GC rule with exact boundaries, stronger/weaker suppression, stable kebab-case IDs, Notable/Investigate labels, Low/Medium/High confidence, raw evidence, and the exact fixed action text. Add no rule configuration or version field. Return Scope, DataQuality, KPIs, Signals, and Limitations only. Include debugger and sampling-quality limitations/caps. No signal means “No notable behavior detected,” not healthy.

Do not persist, export, call a network, use AI, compare nodes/sessions, rank, score, evaluate arbitrary intervals, or interpret thread-pool/socket/custom metrics.

Required tests:
- one test row just below, exactly at, and above every threshold
- pair ordering and same-session/node rejection
- five-snapshot/five-second and ten-snapshot/ten-second confidence gates
- missing metrics and counter reset
- UTC clock movement with valid monotonic time
- sampling coverage/capture overhead/delay/debugger confidence caps
- stronger signal suppression
- stable results for identical input
- zero store writes and zero external calls

Checkpoints:
1. dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --nologo
2. dotnet build
3. git diff --check
4. git status --short
5. Manually cross-check every constant and action against the approved spec.

Return a rule-to-test matrix and STOP for human review.
```

Completion criteria:

- **GATE-024**: Every fixed rule has boundary coverage.
- **GATE-025**: Same input under the same build returns structurally equal output.
- **GATE-026**: Human reviewer signs off the rule-to-test matrix.

### Implementation Phase 9 — Query, Metadata, and Raw Export Services

- **GOAL-009**: Provide one provider-neutral query/read model and complete application-facing service surface before transport/UI work.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-071 | Add `src/Common.Utilities/Profiling/Query/ProfilingQueryService.cs`. | Yes | 2026-08-07 |
| TASK-072 | Add `Query/ProfilingQueryModels.cs` only if Phase 1 models are insufficient; remove duplication rather than maintaining two models. | Yes | 2026-08-07 |
| TASK-073 | Implement session list/detail, selected-node timeline, latest snapshot, context, segments, markers, custom metrics, participation, and sampling-status queries. | Yes | 2026-08-07 |
| TASK-074 | Implement session metadata edit, pin, restart parameter copying, selected deletion, unpinned bulk deletion, and clear-all through the existing control/store services. | Yes | 2026-08-07 |
| TASK-075 | Implement selected-node and complete-session raw snapshot JSON export excluding evaluation and auxiliary records. | Yes | 2026-08-07 |
| TASK-076 | Implement exactly-two-snapshot raw delta comparison with safe percentage behavior. | Yes | 2026-08-07 |
| TASK-077 | Add query, key-resolution, metadata, export-shape, and unavailable-infrastructure tests. | Yes | 2026-08-07 |

#### Prompt 9 — Query and Application Service Completion

```text
Implement Phase 9 only: provider-neutral query/read models, metadata operations, raw comparison, and raw export.

Create ProfilingQueryService under Common.Utilities/Profiling/Query and complete the application-facing service surface. Resolve public keys once and use internal GUIDs internally. Query one selected session/node at a time and return expected participants plus ad-hoc contributors without deployment aggregation.

Implement session selection/detail, node timeline/current snapshot, runtime context, sampling status, segments, action/phase markers, custom metrics, participation warnings, metadata edit, pin, restart copy rules, deletion, unpinned bulk deletion, confirmed clear, and evaluation delegation.

Implement raw JSON export only:
- selected-node export contains that node’s normal runtime snapshots;
- complete-session export contains normal runtime snapshots from expected and ad-hoc contributors;
- exclude session metadata, markers, segments, custom observations, runtime context, and computed evaluation.

Implement exactly two same-session/same-node raw snapshot deltas with absolute and safe percentage differences. Do not add session comparison or an overlaid comparison chart.

Do not add HTTP endpoints, Console Commands, Razor, browser state, or persistence logic outside the store.

Required tests:
- key not found/wrong node/wrong session
- expected plus ad-hoc node listing
- metadata edit does not mutate observations
- restart copies only name suffix, interval, duration, and tags
- active deletion/clear rejection
- raw export exact JSON shape and exclusions
- zero-baseline percentage unavailable
- safe unavailable-infrastructure Results

Checkpoints:
1. dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --nologo
2. dotnet build
3. git diff --check
4. git status --short
5. Review public DTOs for internal GUID leakage and evaluation/export separation.

Return evidence and STOP for human review.
```

Completion criteria:

- **GATE-027**: The application service surface is complete before Presentation work.
- **GATE-028**: Public output contains readable keys and no internal GUIDs.
- **GATE-029**: Human reviewer approves raw export and query boundaries.

### Implementation Phase 10 — Console Commands

- **GOAL-010**: Add complete terminal control and analysis through existing Console Commands without duplicating behavior.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-078 | Add `src/Presentation.Web/Profiling/ConsoleCommands/ProfilingConsoleCommandBase.cs`. | Yes | 2026-08-07 |
| TASK-079 | Add `ProfilingControlConsoleCommands.cs` for status, start, stop, snapshot, GC, mark, and clear. | Yes | 2026-08-07 |
| TASK-080 | Add `ProfilingAnalyzeConsoleCommand.cs` with human and JSON output. | Yes | 2026-08-07 |
| TASK-081 | Add `ProfilingDurationParser.cs` local to the feature for `ms`, `s`, `m`, `h`, and standard `TimeSpan`. | Yes | 2026-08-07 |
| TASK-082 | Add `src/Presentation.Web/Profiling/ProfilingServiceCollectionExtensions.cs` and register `profiling` with alias `prof` only through that feature registration extension. | Yes | 2026-08-07 |
| TASK-083 | Add concise immediate per-node outcome tables using existing minimal Spectre.Console styling. | Yes | 2026-08-07 |
| TASK-084 | Add Console Command binding, validation, disabled/unavailable, cancellation, output, and no-state-change tests. | Yes | 2026-08-07 |

#### Prompt 10 — Console Commands

```text
Implement Phase 10 only: grouped profiling/prof Console Commands.

Follow existing Broadcasting grouped commands and ConsoleCommandBinder conventions. Delegate every operation to the core control/query/evaluation services. Do not implement storage, collector, Broadcast, or evaluation logic in command classes.

Create the shared Presentation Profiling registration extension in this phase because it owns Console Command registration. Phase 11 may extend that same file for dashboard services and endpoints; it must not create a competing registration path.

Commands:
- profiling status
- profiling start [--name] [--interval] [--duration]
- profiling stop
- profiling snapshot [--name]
- profiling gc
- profiling mark --name
- profiling clear --yes
- profiling analyze --session --node [--snapshot-a and --snapshot-b] [--json]

Add a feature-local duration parser accepting ms/s/m/h and standard TimeSpan. Do not change the shared binder. Use public keys in input/output. Summarize immediate delivery outcomes without calling Accepted “completed.” JSON analysis is output only and is never stored.

Preserve existing diag perf and diag gc unchanged.

Required tests:
- grouped command and alias discovery
- duration formats and invalid values
- same validation/defaults as core services
- one snapshot argument rejected
- pair and timeline analysis selection
- --json exact serialization
- clear without --yes changes nothing
- mark without active session
- disabled/unavailable safe output
- cancellation leaves no second session
- immediate node outcome terminology
- existing diag command regression

Checkpoints:
1. dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --nologo --filter FullyQualifiedName~Profiling
2. dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --nologo --filter FullyQualifiedName~ConsoleCommands
3. dotnet build
4. git diff --check
5. git status --short
6. Review that commands contain presentation only.

Return sample outputs and STOP for human review.
```

Completion criteria:

- **GATE-030**: All command behavior is delegated to shared services.
- **GATE-031**: Existing diagnostic commands remain unchanged.
- **GATE-032**: Human reviewer approves command UX and JSON shape.

### Implementation Phase 11 — Dashboard Endpoints and Server Models

- **GOAL-011**: Add authorized server-side dashboard/query/action endpoints after all runtime behavior is stable.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-085 | Integrate Profiling through the dashboard's existing automatic endpoint/page-provider discovery; do not add a Profiling-specific `AddDashboard` extension or registration marker. | Yes | 2026-08-07 |
| TASK-086 | Add `Dashboard/DashboardEndpoints.cs` with page, content, data, analyze, export, and mutation routes under the existing dashboard group. | Yes | 2026-08-07 |
| TASK-087 | Add `Dashboard/DashboardPageProvider.cs` with enabled/available navigation gating. | Yes | 2026-08-07 |
| TASK-088 | Add request/response models under `src/Presentation.Web/Profiling/Models`. | Yes | 2026-08-07 |
| TASK-089 | Map start, stop, snapshot, GC, mark, restart, metadata, delete, bulk-delete, clear, compare, analyze, and export to shared services. | Yes | 2026-08-07 |
| TASK-090 | Use dashboard authorization and safe status/problem mapping; do not add anonymous management routes. | Yes | 2026-08-07 |
| TASK-091 | Preserve readable-key shareable URLs using session/node query parameters. | Yes | 2026-08-07 |
| TASK-092 | Add endpoint registration, authorization, validation, disabled/unavailable, and delegation tests. | Yes | 2026-08-07 |

#### Prompt 11 — Dashboard Endpoints and Server Models

```text
Implement Phase 11 only: Profiling dashboard endpoints, request/response models, page provider, and automatic integration with the existing dashboard plugin discovery.

Follow Presentation.Web/Broadcasting/Dashboard and Metrics/Dashboard. Map routes under the existing authorized dashboard group using hardcoded feature-local paths and Build*Path helpers. Register no second authentication scheme and no anonymous management endpoint.

Do not add a Profiling-specific `AddDashboard` extension or registration marker. The host calls `AddProfiling(...)` to enable Profiling and calls `services.AddDashboard(...)` once for the dashboard shell. The global dashboard discovers Profiling endpoints and the page provider through the standard plugin interfaces; the page provider lights up only while Profiling is enabled and its required services are available.

Add server endpoints for page/content/data, control actions, metadata, deletion/reset, raw compare, raw export, and analyze. Every endpoint delegates to core services and maps typed Results to safe HTTP responses. Public route/query/body/output identifiers use readable keys. Preserve shareable session/node query parameters.

Keep evaluation results unpersisted and disable dashboard evaluation export/copy routes. Raw snapshot export remains JSON. Destructive clear requires explicit confirmation from the request model and active-session rejection.

Do not implement the final Razor layout/charts in this phase beyond the minimal page shells needed for endpoint tests. Do not put business logic in endpoint handlers.

Required tests:
- automatic route discovery through the global dashboard registration
- enabled Profiling navigation visibility and disabled/unavailable navigation suppression
- dashboard authorization inheritance
- exact delegation for every action
- safe Result/status mapping
- public key validation and no internal GUID output
- clear confirmation and active rejection
- analysis no-store behavior
- raw export content type and evaluation-export absence
- shareable URL parameters
- disabled/unavailable navigation and responses

Checkpoints:
1. dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --nologo --filter FullyQualifiedName~Profiling
2. dotnet build
3. git diff --check
4. git status --short
5. Review endpoint authorization, thinness, and public contract safety.

Return route inventory and STOP for human review.
```

Completion criteria:

- **GATE-033**: Endpoints contain no lifecycle, storage, or evaluator implementation.
- **GATE-034**: Dashboard authorization applies to every performance route.
- **GATE-035**: Human reviewer approves route and response contracts.

### Implementation Phase 12 — Dashboard User Interface

- **GOAL-012**: Implement the approved developer-focused Razor dashboard and Plotly visualizations over the stable server contracts.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-093 | Add `Dashboard/Pages/Index.cshtml`, `Content.cshtml`, `Data.cshtml`, `_ViewImports.cshtml`, and `DashboardProfilingViewModel.cs`. | Yes | 2026-08-07 |
| TASK-094 | Implement lifecycle controls, session selection, node selection, metadata, pinning, deletion, clear confirmation, snapshot, GC, and phase markers. | Yes | 2026-08-07 |
| TASK-095 | Implement dense current cards, runtime context, sampling status, unavailable states, and immediate node outcomes. | Yes | 2026-08-07 |
| TASK-096 | Implement exactly two primary Plotly charts: Memory History and GC Pressure History. | Yes | 2026-08-07 |
| TASK-097 | Render segments, action markers, and shared phase markers distinctly on both charts. | Yes | 2026-08-07 |
| TASK-098 | Implement exactly-two-snapshot selection, deterministic analysis above the raw delta table, and collapsible full-session Analysis below the charts. | Yes | 2026-08-07 |
| TASK-099 | Implement Analyze now and browser-wide Live analysis localStorage behavior, default off, no overlapping calls, new-snapshot-only cadence. | Yes | 2026-08-07 |
| TASK-100 | Implement refresh/node/filter localStorage, shareable URLs, raw JSON export, and current-snapshot copy. | Yes | 2026-08-07 |
| TASK-100A | Implement keyed partial refresh for status, metrics, charts, snapshot options, segments, and custom metrics while preserving focus, unsaved fields, selections, file input, expanded details, analysis output, filters, and scroll. | Yes | 2026-08-09 |
| TASK-101 | Add Razor rendering, DOM contract, endpoint interaction, Plotly data, localStorage, live-analysis, and accessibility tests. | Yes | 2026-08-07 |

#### Prompt 12 — Dashboard User Interface

```text
Implement Phase 12 only: the Profiling Razor dashboard UI.

Use the existing dashboard shell, Bootstrap conventions, createRefresher, postAction, loadPlotly, and createPlotlyLayout. Add no frontend framework and no second chart library.

Implement:
- status and lifecycle controls
- session metadata/pin/restart/delete/bulk-delete/confirmed-clear
- manual snapshot, GC, and active-session phase marker
- selected expected/ad-hoc node dropdown
- compact Current Snapshot cards and immutable runtime context
- latest sampling status without triggering evaluation
- exactly two primary full-width Plotly charts
- distinct scoped segments, node action markers, and shared phase markers
- optional custom metric/segment details below primary panels
- pair selection and analysis above authoritative raw delta table
- collapsible session Analysis below charts
- Analyze now and browser-wide Live analysis switch
- raw selected-node/complete-session JSON export and current snapshot clipboard copy

Live analysis defaults off in localStorage. When off, make no automatic evaluation call. When on, evaluate only after a new snapshot, never overlap calls, and never exceed refresh cadence. The switch does not affect console/programmatic analysis. Do not expose evaluation copy/export/download.

No aggregate node view, ranking, overall score, health/critical label, request panels, session comparison, or arbitrary interval controls.

Required tests:
- dashboard page/navigation rendering
- exactly two primary chart containers and correct series
- unavailable metric rendering
- expected/ad-hoc node selection
- marker and segment shapes
- destructive confirmation
- pair validation/raw delta ordering
- analysis placement and empty state
- Live analysis default/off/on/no-overlap/new-snapshot behavior
- localStorage keys survive reload/session changes
- evaluation has no export/copy control
- basic labels, keyboard controls, and accessible names

Checkpoints:
1. dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --nologo --filter FullyQualifiedName~Profiling
2. dotnet build
3. git diff --check
4. git status --short
5. Perform a human visual review at desktop and narrow viewport using a real local host; record screenshots or observations without committing generated artifacts unless requested.

Return test results and visual-review notes. STOP for human approval.
```

Completion criteria:

- **GATE-036**: UI matches the written layout and contains exactly two primary charts.
- **GATE-037**: Live analysis consumes no automatic evaluation resources while off.
- **GATE-038**: Human reviewer approves usability and visual density.

### Implementation Phase 13 — Integration, Example, Retention, and Documentation

- **GOAL-013**: Prove end-to-end single-node and multi-node behavior and document actual registration/usage after the implementation is stable.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-102 | Add one Development-only example registration using the in-memory Profiling provider in an existing example host; do not modify unrelated examples. | Yes | 2026-08-08 |
| TASK-103 | Add end-to-end single-node start/collect/mark/analyze/stop/export/clear tests. | Yes | 2026-08-08 |
| TASK-104 | Add two-node shared-store tests for participation, ad-hoc snapshot, best-effort stop, finalization, and warnings. | Yes | 2026-08-08 |
| TASK-105 | Add provider-capability rejection tests proving no mutation/broadcast with process-local storage and multiple targets. | Yes | 2026-08-08 |
| TASK-106 | Add retention/startup reconciliation tests over the durable provider. | Yes | 2026-08-08 |
| TASK-107 | Update the appropriate canonical feature documentation and `docs/INDEX.md` with registration, dashboard, console, programmatic, security, and limitations guidance. | Yes | 2026-08-08 |
| TASK-108 | Document that consuming applications own EF migrations and multi-node setups require shared Profiling and Broadcast providers. | Yes | 2026-08-08 |
| TASK-109 | Verify generated OpenAPI changes are intentional and limited to mapped endpoints; do not overwrite unrelated existing changes. | Yes | 2026-08-08 |

#### Prompt 13 — Integration and Documentation

```text
Implement Phase 13 only: end-to-end verification, one Development example integration, and documentation.

Choose one existing example host after inspecting current registrations and dirty files. Do not edit examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/wwwroot/openapi.json unless the human explicitly confirms the existing modification belongs to this feature. Enable Profiling only in Development, use the in-memory Profiling provider, and follow existing Broadcasting/dashboard/Console Command patterns. Prove the EF provider through integration tests and documentation. Document that a consuming application choosing EF implements `IProfilingContext` and owns its migrations; do not add a repository migration.

Add deterministic single-node integration coverage and a real two-node shared-store/HTTP Broadcast test using existing Kestrel test infrastructure. Cover target snapshot, expected participants, ad-hoc contributor, missed stop, completion warnings, idempotent finalization, and clear/late-write rejection.

Update canonical docs with:
- in-memory registration
- EF shared-store registration
- required Broadcasting setup
- dashboard and shareable URLs
- programmatic scopes/segments/markers
- all Console Commands
- evaluation limitations and fixed rules
- raw export versus non-exportable evaluation
- local-development/security guidance

Required tests:
- single-node start/collect/mark/analyze/stop/export/clear workflow;
- real two-node shared-store/Broadcast participation and ad-hoc contribution;
- process-local provider capability rejection before mutation/publication;
- durable-provider retention and startup reconciliation;
- documentation and example registration smoke validation.

Do not add new feature behavior, packages, migrations, or examples beyond the one integration.

Checkpoints:
1. dotnet test tests/Common.UnitTests/Common.UnitTests.csproj --nologo
2. dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --nologo
3. dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --nologo
4. dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --nologo --filter FullyQualifiedName~Profiling
5. dotnet build
6. git diff --check
7. git status --short
8. Human review of registration API, example diff, documentation accuracy, and external-provider test availability.

Return evidence and STOP for human approval.
```

Completion criteria:

- **GATE-039**: Single-node and multi-node workflows are verified end to end.
- **GATE-040**: Documentation matches the implemented public API and no migration is committed.
- **GATE-041**: Human reviewer approves example and documentation.

### Implementation Phase 14 — Final Hardening and Cleanup

- **GOAL-014**: Review the complete implementation for semantic drift, concurrency defects, security, performance overhead, documentation, and repository cleanliness without adding features.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-110 | Run a requirement-to-code-to-test traceability review for every acceptance criterion. | Yes | 2026-08-08 |
| TASK-111 | Stress concurrent start/stop/clear/finalize/retention/manual-snapshot races for both providers. | Yes | 2026-08-08 |
| TASK-112 | Add core profiling BenchmarkDotNet coverage to the existing `Common.Benchmarks` project for runtime sampling, probe/model overhead, GC evidence, runtime context, and in-memory append. | Yes | 2026-08-07 |
| TASK-113 | Audit logs, JSON, routes, console output, and errors for internal GUIDs, secrets, paths, stack traces, and business data. | Yes | 2026-08-08 |
| TASK-114 | Audit evaluation constants, deterministic actions, confidence caps, and no-persistence guarantees against the approved spec. | Yes | 2026-08-08 |
| TASK-115 | Remove dead code, unused abstractions, duplicate models, speculative options, and phase-local compatibility shims. | Yes | 2026-08-08 |
| TASK-116 | Run formatting only on touched files and inspect public XML documentation. | Yes | 2026-08-08 |
| TASK-117 | Run repository build and all unit/integration suites sequentially; report unavailable external services explicitly. | Yes | 2026-08-08 |
| TASK-118 | Update plan status/tasks only after all gates pass and human review accepts the final implementation. | Yes | 2026-08-08 |
| TASK-119 | Run and record core benchmarks plus disabled, idle, 1-second, and 500-ms collector overhead; investigate budget regressions and verify no overlapping capture. | Yes | 2026-08-08 |

#### Final Hardening Prompt

```text
Execute final hardening only. Do not add features.

Read the approved specification, this plan, and the complete Profiling diff. Produce a traceability matrix from every acceptance criterion to implementation and tests. Treat any missing or contradictory behavior as a defect; fix only defects within approved scope.

Audit:
- Clean Architecture/project dependency direction
- one logical active session
- in-memory and EF lifecycle equivalence
- transaction and concurrency behavior
- no capture overlap and monotonic timing
- fixed participant/ad-hoc semantics
- best-effort stop and idempotent finalization/reconciliation
- retention/delete/clear/late-write rejection
- custom metric and scope behavior
- evaluator constants/determinism/no persistence
- dashboard Live analysis resource behavior
- authorization and destructive confirmations
- public keys versus internal GUIDs
- sensitive data/logging/XML docs
- core BenchmarkDotNet time/allocation budgets and disabled/idle/500-ms/1-second overhead
- no new project/package/migration/polling/APM/AI/score/cross-node evaluation

Required tests:
- concurrent lifecycle stress tests for both stores;
- core profiling BenchmarkDotNet results plus disabled, idle, 1-second, and 500-ms overhead measurements;
- complete unit and integration test suites;
- provider-specific Profiling integration tests;
- acceptance-criterion traceability verification.

Remove unused abstractions, duplicated DTOs, speculative options, and stale comments. Do not refactor unrelated repository code.

Checkpoints (run sequentially):
1. dotnet build
2. every UnitTests project using the repository’s Solution - tests (unit) command or its exact sequential equivalent
3. every IntegrationTests project using Solution - tests (integration) or its exact sequential equivalent
4. provider-specific Profiling integration tests
5. git diff --check
6. git status --short

Run the profiling benchmarks from the existing `benchmarks/Common.Benchmarks` project in Release mode. Report mean execution time and managed allocation for raw system sampling, fixed-source probe/model overhead, complete system-backed capture, GC observation, runtime-context capture, and in-memory append. Compare results with the approved spec budgets; investigate and justify any regression rather than adding machine-dependent unit-test assertions.

Report test totals, skipped/unavailable external suites, benchmark results, performance-overhead observations, traceability gaps, and final file list. STOP for final human review. Mark nothing implemented until the human approves.
```

Completion criteria:

- **GATE-042**: Every acceptance criterion maps to code and at least one verification.
- **GATE-043**: Repository build, unit tests, and available integration tests pass sequentially.
- **GATE-044**: No out-of-scope abstraction, package, migration, endpoint, or behavior remains.
- **GATE-045**: Human reviewer accepts the implementation and authorizes status updates.

Phase 14 and GATE-045 were accepted on 2026-08-08. The implementation plan is complete.

### Optional Fleet Execution Prompt

```text
Use fleet execution only after the prerequisite gates below are approved. Keep one coordinator responsible for integration. Never assign two agents the same file or test file.

Shared rules:
- Every agent reads the approved specification, this plan, AGENTS.md, and Shared Governance Instructions.
- Every agent owns only its listed paths.
- Agents do not edit shared registration files, project files, docs, or examples unless explicitly assigned.
- Agents commit nothing unless the human asks.
- Agents return patches and test evidence to the coordinator.
- The coordinator integrates one result at a time, runs tests sequentially, resolves no semantic conflict by guessing, and stops for human review after each wave.

Wave 1, after Phase 1:
- Agent A: Phase 2 files under Common.Utilities/Profiling/InMemoryProfilingStore.cs and its dedicated store tests.
- Agent B: Phase 3 files under Common.Utilities/Profiling/Runtime/ProfilingNodeIdentityProvider.cs, ProfilingRuntimeContextFactory.cs, ProfilingSnapshotProbe.cs, ProfilingGcObservationState.cs, and dedicated probe tests.
- Coordinator: integrates both, owns shared Profiling registration/models changes, executes Phase 2 and Phase 3 gates separately.

Wave 2, after Phases 4-6:
- Agent A: Phase 7 files under Common.Utilities/Profiling/Scopes and Metrics plus dedicated tests.
- Agent B: Phase 8 files under Common.Utilities/Profiling/Evaluation plus dedicated tests.
- Coordinator: owns shared abstractions/models adjustments, rejects duplicate DTOs, and executes both gates separately.

Wave 3, after Phase 9:
- Agent A: Phase 10 files under Presentation.Web/Profiling/ConsoleCommands and command tests.
- Agent B: Phase 11 Dashboard endpoint/model files and endpoint tests, excluding shared `ProfilingServiceCollectionExtensions.cs`.
- Coordinator: creates `ProfilingServiceCollectionExtensions.cs` with Phase 10, extends it after integrating Phase 11, integrates commands first, endpoints second, then runs Presentation tests sequentially.

Do not parallelize:
- Phase 4 collector/lifecycle
- Phase 5 EF transactions/concurrency
- Phase 6 distributed Broadcast control
- Phase 12 dashboard UI integration
- Phase 13 end-to-end tests/example/docs
- Phase 14 hardening

At the end of each wave, run dotnet build, all affected test projects sequentially, git diff --check, and human review. Do not launch the next wave automatically.
```

### Recommended Execution Order

1. Execute Phase 0 and approve architecture placement.
2. Execute Phase 1 and freeze public/core contracts.
3. Execute Phases 2 and 3 sequentially by default; optional fleet Wave 1 is safe after Phase 1.
4. Execute Phase 4 to combine the approved store and probe into a stable local runtime.
5. Execute Phase 5 and approve transactional durable storage.
6. Execute Phase 6 and approve distributed semantics.
7. Execute Phases 7 and 8 sequentially by default; optional fleet Wave 2 is safe after Phase 6.
8. Execute Phase 9 and freeze the application-facing read/control surface.
9. Execute Phases 10 and 11 sequentially by default; optional fleet Wave 3 is safe after Phase 9.
10. Execute Phase 12 only after server contracts are approved.
11. Execute Phase 13 integration/documentation.
12. Execute Phase 14 hardening and final human acceptance.

Default to sequential execution. Use optional fleet waves only when separate agents can honor exact file ownership and the coordinator can integrate and test one patch at a time.

## 3. Alternatives

- **ALT-001**: Put the feature directly in `Presentation.Web`. Rejected because programmatic scopes, background collection, evaluation, and provider contracts must also work without dashboard rendering and must not depend on ASP.NET Core.
- **ALT-002**: Create a new Profiling project/package. Rejected because the repository already places comparable cross-cutting runtime features in Common.Utilities and the approved scope does not justify another package.
- **ALT-003**: Put runtime contracts in an Application project. Rejected because the feature is host/runtime diagnostics rather than domain application behavior, and existing Broadcasting/Metrics seams are in Common.Utilities.
- **ALT-004**: Reuse the process-local metrics dashboard snapshot as persistence. Rejected because it has no session, node, immutable history, atomic lifecycle, or durable provider semantics.
- **ALT-005**: Poll the performance store for commands. Rejected because the approved design requires direct typed Broadcasting and no command polling.
- **ALT-006**: Add a general event-sourced or message-driven lifecycle. Rejected because sessions are short-lived local diagnostics and require only store coordination plus best-effort broadcasts.
- **ALT-007**: Implement evaluation in JavaScript. Rejected because dashboard, console, and programmatic callers must share one deterministic server implementation.
- **ALT-008**: Add configurable rules or an overall score. Rejected because the approved spec fixes independent evidence-backed signals.
- **ALT-009**: Add session-to-session or cross-node analysis. Rejected because the approved evaluator is one selected node and one session or pair only.
- **ALT-010**: Automatically capture traces or dumps. Rejected because this feature remains lightweight and does not become a profiler.

## 4. Dependencies

- **DEP-001**: `docs/specs/spec-performance-snapshot-dashboard.md` is the authoritative approved specification.
- **DEP-002**: `src/Common.Utilities/KeyGenerator.cs` supplies public readable keys.
- **DEP-003**: `src/Common.Utilities/Broadcasting` remains a standalone feature and supplies unchanged typed direct delivery, per-node immediate outcomes, node registration, and duplicate-safe handler dispatch. `src/Common.Utilities/Profiling/Control/ProfilingBroadcastService.cs` owns immutable target preparation and supplies the prepared target set to Broadcast through a snapshot-backed registry view.
- **DEP-004**: `src/Common.Utilities/Metrics` and the DevKit meter supply the existing custom metric surface.
- **DEP-005**: `src/Common.Utilities/Hosting` and `TimeProvider` patterns supply hosted lifecycle and testable time; the collector uses a dedicated absolute-deadline loop rather than `PeriodicBackgroundService` fixed-delay scheduling.
- **DEP-006**: `src/Infrastructure.EntityFramework` supplies EF Core, operation-owned context patterns, relational transactions, and provider integration.
- **DEP-007**: Consuming applications supply a `DbContext` implementing `IProfilingContext` and own migrations.
- **DEP-008**: `src/Presentation/ConsoleCommands` supplies grouping, binding, execution, Spectre.Console, and host forwarding.
- **DEP-009**: `src/Presentation.Web/Dashboard` supplies authorized Razor-slice plugins, refresh/actions, Plotly loading, layout, and local developer UX conventions.
- **DEP-010**: Existing Common, Infrastructure, Presentation, and integration test projects supply xUnit, Shouldly, NSubstitute, fake time, TestServer, relational-provider, and Kestrel patterns.
- **DEP-011**: `Infrastructure.UnitTests` references `Common.UnitTests` so its public provider-contract fixture is reusable without source-linking.

## 5. Files

- **FILE-001**: `src/Common.Utilities/Profiling/ProfilingOptions.cs` — fixed configuration and validation.
- **FILE-002**: `src/Common.Utilities/Profiling/ProfilingModels.cs` — provider-neutral state, observations, queries, and evaluation contracts.
- **FILE-003**: `src/Common.Utilities/Profiling/ProfilingAbstractions.cs` — provider, probe, control, query, scope, and evaluation contracts.
- **FILE-004**: `src/Common.Utilities/Profiling/ProfilingErrors.cs` — typed safe Result errors.
- **FILE-005**: `src/Common.Utilities/Profiling/ProfilingRegistration.cs` and `ProfilingServiceCollectionExtensions.cs` — re-entrant feature registration.
- **FILE-006**: `src/Common.Utilities/Profiling/InMemoryProfilingStore.cs` — process-local provider and atomic lifecycle.
- **FILE-007**: `src/Common.Utilities/Profiling/Runtime/*` — node identity, runtime context, GC observation, probe, collector, and hosted lifecycle.
- **FILE-008**: `src/Common.Utilities/Profiling/Control/*` — control service, typed broadcasts/handlers, finalization, and reconciliation.
- **FILE-009**: `src/Common.Utilities/Profiling/Scopes/*` — measurement scopes and ambient segments.
- **FILE-010**: `src/Common.Utilities/Profiling/Metrics/ProfilingCustomMetricListener.cs` — existing-meter observation.
- **FILE-011**: `src/Common.Utilities/Profiling/Evaluation/*` — pure calculations, rules, and evaluator.
- **FILE-012**: `src/Common.Utilities/Profiling/Query/*` — provider-neutral query, comparison, metadata, and raw export.
- **FILE-013**: `src/Infrastructure.EntityFramework/Profiling/IProfilingContext.cs` — application DbContext capability.
- **FILE-014**: `src/Infrastructure.EntityFramework/Profiling/Entities/*` — `__Profiling_*` persistence entities.
- **FILE-015**: `src/Infrastructure.EntityFramework/Profiling/EntityFrameworkProfilingStore.cs` — shared durable provider.
- **FILE-016**: `src/Infrastructure.EntityFramework/Profiling/ServiceCollectionExtensions.cs` — fluent provider selection.
- **FILE-017**: `src/Presentation.Web/Profiling/ConsoleCommands/*` — grouped terminal surface.
- **FILE-018**: `src/Presentation.Web/Profiling/Models/*` — dashboard request/response models.
- **FILE-019**: `src/Presentation.Web/Profiling/Dashboard/DashboardEndpoints.cs` — authorized page/action/data routes.
- **FILE-020**: `src/Presentation.Web/Profiling/Dashboard/DashboardPageProvider.cs` — navigation/index registration.
- **FILE-021**: `src/Presentation.Web/Profiling/Dashboard/Pages/*` — Razor UI, view model, charts, localStorage, and actions.
- **FILE-022**: `src/Presentation.Web/Profiling/ProfilingServiceCollectionExtensions.cs` — Presentation registration.
- **FILE-023**: `tests/Common.UnitTests/Utilities/Profiling/*` — contracts, stores, probe, runtime, scopes, custom metrics, evaluation, and queries.
- **FILE-024**: `tests/Infrastructure.UnitTests/EntityFramework/Profiling/*` — EF model/provider/concurrency tests.
- **FILE-025**: `tests/Infrastructure.IntegrationTests/EntityFramework/Profiling/*` — relational provider contracts.
- **FILE-026**: `tests/Presentation.UnitTests/Web/Profiling/*` — endpoint, dashboard, and UX tests.
- **FILE-027**: `tests/Presentation.UnitTests/ConsoleCommands/Profiling*` — Console Command tests.
- **FILE-028**: One existing Development example host — registration only after explicit dirty-file inspection.
- **FILE-029**: Canonical feature documentation and `docs/INDEX.md`.
- **FILE-030**: `docs/specs/spec-performance-snapshot-dashboard.md` — approved behavior contract; change only if implementation uncovers a human-approved contradiction.

## 6. Testing

- **TEST-001**: Options, identifiers, state, errors, and registration defaults.
- **TEST-002**: Shared in-memory/EF provider contract and public-key lookup.
- **TEST-003**: Concurrent start, marker, stop, finalize, clear, retention, and late-write behavior.
- **TEST-004**: Runtime probe metric correctness, unavailable values, direct GC evidence, and safe context.
- **TEST-005**: Single-flight collector, monotonic timing, skipped/failed accounting, replacement, duration stop, and reconciliation.
- **TEST-006**: Broadcast participant, ad-hoc contributor, mixed outcome, capability rejection, duplicate delivery, stop, snapshot, and GC behavior.
- **TEST-007**: Scoped ownership, outcomes, parent validation, interruption, marker, and custom metric association.
- **TEST-008**: Table-driven boundary coverage for every deterministic evaluation rule and confidence/data-quality gate.
- **TEST-009**: Query, metadata, restart-copy, deletion, comparison, export-shape, and public-key safety.
- **TEST-010**: Console Command discovery, binding, duration parsing, output, confirmation, JSON, and existing diagnostic regression.
- **TEST-011**: Dashboard route authorization, delegation, validation, and response contracts.
- **TEST-012**: Razor rendering, exactly two Plotly charts, markers, localStorage, Live analysis, compare/analysis placement, and destructive controls.
- **TEST-013**: Single-node end-to-end diagnostic session.
- **TEST-014**: Two-node shared-store/Broadcast integration.
- **TEST-015**: SQLite, SQL Server, and PostgreSQL EF provider contracts when environments are available.
- **TEST-016**: Disabled/idle overhead and no background command polling.
- **TEST-017**: Security and sensitive-data audit for logs, JSON, errors, dashboard, and console.
- **TEST-018**: Final sequential repository build, unit tests, integration tests, formatting, and diff checks.

## 7. Risks & Assumptions

- **RISK-001**: Runtime/GC APIs differ by platform. Mitigation: represent unavailable metrics explicitly and isolate only the minimum platform reads required for tests.
- **RISK-002**: Sampling work can distort measured behavior. Mitigation: single-flight capture, monotonic capture duration, skipped/failed totals, sampling-quality KPIs, and confidence caps.
- **RISK-003**: Direct post-Gen2 evidence may be unavailable on a runtime. Mitigation: suppress retention rules requiring it and report a limitation rather than infer false evidence.
- **RISK-004**: EF providers differ in transaction/isolation behavior. Mitigation: portable unique/concurrency constraints and relational provider contract tests.
- **RISK-005**: A stop broadcast can be missed. Mitigation: preserve original end time, best-effort semantics, idempotent finalization, and startup reconciliation; do not add durable commands.
- **RISK-006**: A process-local provider cannot support independent target processes. Mitigation: check target count and `SupportsMultiNode` before mutation/publication.
- **RISK-007**: Custom metric callbacks could add overhead. Mitigation: listen only to the existing DevKit meter, keep idle checks minimal, bound callback work, and persist no observations while idle.
- **RISK-008**: Live analysis could consume resources during collection. Mitigation: browser-wide default off, new-snapshot-only calls, no overlap, and refresh-cadence limit.
- **RISK-009**: The large dashboard could accumulate business logic. Mitigation: freeze core query/control/evaluation services before UI and test endpoint delegation.
- **RISK-010**: Existing unrelated working-tree changes may overlap generated OpenAPI or examples. Mitigation: inspect status before each phase and never overwrite unrelated files.
- **ASSUMPTION-001**: The feature remains inside existing Common.Utilities, Infrastructure.EntityFramework, and Presentation.Web projects.
- **ASSUMPTION-002**: The existing Broadcasting implementation is complete and available before Phase 6.
- **ASSUMPTION-003**: `MeterListener` can observe the DevKit meter without changing the application-facing `IMetricsService` API.
- **ASSUMPTION-004**: The existing dashboard Plotly loader is the standard chart technology required by the specification.
- **ASSUMPTION-005**: Application-owned migrations are generated by consuming applications after implementing `IProfilingContext`.
- **ASSUMPTION-006**: Approved fixed evaluator thresholds are implementation constants, not user configuration.
- **ASSUMPTION-007**: Public keys are practically unique as resolved by the specification; no collision protocol is added.
- **ASSUMPTION-008**: Phase markers are session-level UTC annotations and do not create analysis intervals.

## 8. Related Specifications / Further Reading

- [Approved Profiling Dashboard specification](../docs/specs/spec-performance-snapshot-dashboard.md)
- [Broadcasting specification](../docs/specs/spec-common-utilities-broadcasting.md)
- [Application Metrics API specification](../docs/specs/spec-application-metrics-api.md)
- [Console Commands documentation](../docs/features-presentation-console-commands.md)
- [DevKit CLI documentation](../docs/features-cli.md)
- [Repository architecture](../ARCHITECTURE.md)
- [Repository agent conventions](../AGENTS.md)
- [Core Broadcasting implementation](../src/Common.Utilities/Broadcasting)
- [Entity Framework Broadcasting provider](../src/Infrastructure.EntityFramework/Broadcasting)
- [Dashboard shell and Plotly helpers](../src/Presentation.Web/Dashboard)
- [Metrics dashboard pattern](../src/Presentation.Web/Metrics/Dashboard)
