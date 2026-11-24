---
goal: Implement LiteDB storage for Jobs, orchestrations, messaging, queueing, and notifications
version: 1.0
date_created: 2026-11-19
last_updated: 2026-11-20
owner: bITdevKit
status: 'Completed'
tags: [feature, infrastructure, litedb, jobs, orchestrations, messaging, queueing, notifications]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-green)

Implement COV-017 Jobs, COV-016 orchestrations, COV-014 messaging, COV-015 queueing, and COV-019 notifications from the LiteDB provider specification. Reuse the existing named database owner and native transaction session. Keep application scheduling, orchestration execution, handler dispatch, and notification transport in their existing packages.

COV-018 legacy `Application.JobScheduling` is excluded. COV-004 sequence numbers and COV-006 event sourcing remain excluded. This plan does not require those packages or Quartz persistence.

## 1. Requirements & Constraints

- **REQ-001**: Implement every store exposed by `src/Application.Jobs/Persistence/IJobStoreProvider.cs` and `IJobStoreContracts.cs`. Preserve occurrence and accepted-event deduplication, atomic batch membership and child creation, dependency updates, history, previous-execution lookup, and scheduler queries.
- **REQ-002**: Implement every store exposed by `src/Application.Orchestrations/Storage/IOrchestrationStorageProvider.cs` and `OrchestrationStores.cs`. Preserve instance version checks, active concurrency keys, leases, ordered history, signal idempotency, due timers, filtering, metrics, archive, purge, and administrative recovery.
- **REQ-003**: Implement `IMessageBroker`, `IMessageBrokerRuntime`, `IMessageBrokerService`, and `Application.Messaging.IOutboxMessageWorker`. Persist message content, handler snapshots, per-handler outcomes, waiting-for-handler state, delivery attempts, expiration, claims, pause state, archive, and purge. Outgoing-message outbox selection must remain independent of the receiving broker.
- **REQ-004**: Implement `IQueueBroker`, `IQueueBrokerRuntime`, `IQueueBrokerBackgroundProcessor`, and `IQueueBrokerService`. Preserve one active consumer, delayed eligibility, retries, expiration, dead letters, queue/type controls, retained queries, archive, and purge.
- **REQ-005**: Implement `INotificationStorageProvider` and `INotificationEmailOutboxService`. Persist email content, attachments, attempt state, pending eligibility, operational filters, statistics, retry, archive, and purge. Delivery uses the selected `INotificationService<EmailMessage>` transport.
- **REQ-006**: Claim, renew, finalize, retry, archive, and purge inside native transactions. A completion or renewal must compare the current claim token and expiration. Run external handlers and transports outside database transactions.
- **REQ-007**: Persist code-owned payload type tokens and serialized payloads. Reopening a database must preserve committed work. Return detached values, never native cursors. Do not persist service providers, delegates, cancellation tokens, or handler instances.
- **REQ-008**: Register providers through the existing `JobBuilderContext`, `OrchestrationBuilderContext`, `MessagingBuilderContext`, `QueueingBuilderContext`, and `NotificationServiceBuilder`. Add provider-only extension methods without changing their application contracts.
- **REQ-009**: Use stable feature collection prefixes `jobs`, `orchestrations`, `messaging`, `queueing`, and `notifications`. Prefixes may be configured before registration. Validate names and reject collisions in `LiteDbDatabaseRegistry` before opening a database.
- **REQ-010**: Default unpaged materialization limit to `10000` records, probe one extra record, and fail explicitly on overflow. Default durable worker batch size to `128`. Observe cancellation at admission, between records, and before commit. Maintenance must process bounded batches.
- **SEC-001**: Operational queries must return the existing safe DTOs. Do not place message bodies, attachment bytes, notification addresses, credentials, or serialized payloads in routine logs.
- **CON-001**: All runtime implementation belongs in `src/Infrastructure.LiteDB`. Keep namespace casing `BridgingIT.DevKit.Infrastructure.LiteDb`. Add references only to the application packages owning the contracts. Add no EF dependency.
- **CON-002**: Preserve the official pinned `LiteDB 6.0.0-prerelease.355`. Do not fork, patch, or implicitly acknowledge its upstream development guard. Tests explicitly acknowledge use of disposable data. Production readiness remains blocked by the upstream build.
- **CON-003**: Do not change ActiveEntity core, source generators, generic repository contracts, or the implementation of the existing feature engines. Do not implement excluded COV-018.
- **CON-004**: Prefer public/protected APIs with XML documentation and private implementation details. Introduce no `internal` declarations.
- **CON-005**: Keep all LiteDB usage documentation in `docs/features-infrastructure-litedb.md`, grouped by feature. Preserve `src/Infrastructure.LiteDB/README.md`. This plan and the parent specification contain execution status and requirements only.
- **CON-006**: Preserve existing working-tree changes. Execute top-level builds and tests sequentially. Use Docker-free temporary database integration tests and fictional models.
- **PAT-001**: Reuse `LiteDbDatabaseOwner`, `LiteDbDatabaseSession`, `Result`, feature serializers, `TimeProvider`, existing broker base classes, handler factories, feature builders, and hosted services where their contracts apply.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Establish isolated durable feature commands and registration. No prerequisites. Execute TASK-001 before TASK-002; execute TASK-003 after TASK-002. Completion requires an XML-enabled project build and passing storage transaction tests.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Add `src/Infrastructure.LiteDB/Durable/LiteDbFeatureOptions.cs`, `LiteDbFeatureDatabase.cs`, and `LiteDbKeyedCollection.cs`. Store one keyed logical record per native document with stable ordinal key normalization and detached serialized payloads. Execute synchronous record commands through an owned `LiteDbDatabaseSession`; atomically persist all changed records and roll back on failure or cancellation. Bound enumerations and reject asynchronous callbacks on the native worker. | Yes | 2026-11-19 |
| TASK-002 | Add `src/Infrastructure.LiteDB/Durable/LiteDbDurableServiceCollectionExtensions.cs`. Freeze database, prefix, serializer, and materialization settings. Register isolated feature collections in `LiteDbDatabaseRegistry`. Add required project references to `Application.Jobs`, `Application.Orchestrations`, `Application.Messaging`, `Application.Queueing`, and `Application.Notifications` through the .NET CLI. Restore without changing the central LiteDB version. | Yes | 2026-11-19 |
| TASK-003 | Add `tests/Infrastructure.IntegrationTests/LiteDB/Durable/LiteDbDurableTestHost.cs` and `LiteDbFeatureDatabaseTests.cs`. Verify multi-record rollback, cancellation, detached values, bounded enumeration, named isolation, scope disposal, and file reopen. | Yes | 2026-11-19 |

### Implementation Phase 2

- GOAL-002: Complete Jobs persistence. Depends on Phase 1. TASK-004 and TASK-005 execute sequentially. Completion requires all twelve `IJobStoreProvider` facets to resolve and pass normal and failure cases.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-004 | Add `src/Infrastructure.LiteDB/Jobs/LiteDbJobStoreProvider.cs`. Implement `RuntimeStates`, `TriggerRuntimeStates`, `Occurrences`, `Executions`, `Dependencies`, `Batches`, `Leases`, `ExecutionHistory`, `BatchHistory`, `AcceptedEvents`, `PreviousExecutions`, and `Queries`. Reuse the existing in-memory provider's persistence semantics with native transactions and serialized detached records. Validate duplicate occurrence IDs as well as occurrence keys before any atomic batch mutation. Compare lease owner, token, and expiration on renew and verify. Compare the current owner and token on release. | Yes | 2026-11-19 |
| TASK-005 | Add `WithLiteDb` for `JobBuilderContext` in `src/Infrastructure.LiteDB/Jobs/LiteDbJobsBuilderExtensions.cs`. Add `tests/Infrastructure.IntegrationTests/LiteDB/Durable/LiteDbJobStoreProviderTests.cs`. Exercise every facet, competing deduplication and claims, stale tokens, failed batch attachment rollback, accepted-event cursor ordering, query composition, and reopen. Verify composition with the existing scheduler. | Yes | 2026-11-19 |

### Implementation Phase 3

- GOAL-003: Complete orchestration storage. Depends on Phase 1 and may proceed independently of Phase 2. Execute TASK-006 before TASK-007. Completion requires all seven facets and reconstructed persisted contexts to pass.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-006 | Add `src/Infrastructure.LiteDB/Orchestrations/LiteDbOrchestrationStorageProvider.cs` and `LiteDbOrchestrationBuilderExtensions.cs`. Implement instance create/save with version and active-key checks; lease acquire/renew/verify/release; history append; signal persist/idempotency/status; timer schedule/due/status; context reconstruction; query/metrics; and archive/purge/release/requeue administration. Reuse existing snapshot serializers and actor/clock contracts. | Yes | 2026-11-19 |
| TASK-007 | Add `tests/Infrastructure.IntegrationTests/LiteDB/Durable/LiteDbOrchestrationStorageProviderTests.cs`. Verify stale saves, active-key conflicts, lease fences, ordered histories, signal deduplication and eligibility, timer ordering, query filters, metrics, administrative rollback, active lease protection, and full context reconstruction after reopen. Run a persisted transition through the existing orchestration runtime. | Yes | 2026-11-19 |

### Implementation Phase 4

- GOAL-004: Complete durable messaging and the independently selectable outgoing outbox. Depends on Phase 1. Execute TASK-008 before TASK-009 before TASK-010. Completion requires fan-out progress, worker ownership, retained operations, and independent outbox delivery to pass.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-008 | Add `src/Infrastructure.LiteDB/Messaging/LiteDbMessageBroker.cs`, `LiteDbMessageBrokerOptions.cs`, and `LiteDbBrokerMessage.cs`. Derive the broker from existing `MessageBrokerBase`; persist outgoing publications and handler snapshots, support waiting-for-handler state, and invoke existing publisher/handler behaviors and handler factories. Keep delivery outside native commands. | Yes | 2026-11-19 |
| TASK-009 | Add `src/Infrastructure.LiteDB/Messaging/LiteDbMessageBrokerWorker.cs`, `LiteDbMessageBrokerService.cs`, and `LiteDbMessagingBuilderExtensions.cs`. Claim bounded eligible records, renew leases during delivery, fence progress and completion, enforce retry and expiry rules, persist controls, and implement every `IMessageBrokerService` method using the existing DTOs. Register the runtime and background processing through `MessagingBuilderContext`. | Yes | 2026-11-19 |
| TASK-010 | Add outgoing capture and worker adapters under `src/Infrastructure.LiteDB/Messaging/Outbox/`. Reuse `Application.Messaging.IOutboxMessageWorker` and `OutboxMessageService`; publish through the independently configured `IMessageBroker`. Add `LiteDbMessagingTests.cs`, `LiteDbMessageOutboxTests.cs`, and `LiteDbDeliveryLeaseTests.cs` under `tests/Infrastructure.IntegrationTests/LiteDB/Durable/`, covering fan-out, partial handler failure, late subscriptions, lease renewal/loss, retries, controls, search/statistics, archive/purge, transaction rollback, and reopen. | Yes | 2026-11-19 |

### Implementation Phase 5

- GOAL-005: Complete durable queueing. Depends on Phase 1 and may proceed independently of Phases 2 through 4. Execute TASK-011 before TASK-012. Completion requires one consumer per current claim and every operational API to pass.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-011 | Add `src/Infrastructure.LiteDB/Queueing/LiteDbQueueBroker.cs`, `LiteDbQueueMessage.cs`, `LiteDbQueueBrokerOptions.cs`, `LiteDbQueueBrokerService.cs`, and `LiteDbQueueingBuilderExtensions.cs`. Reuse `QueueBrokerBase` and queue handlers; implement `IQueueBrokerBackgroundProcessor`. Persist delayed eligibility, expiry, retry/dead-letter outcomes, owner/token leases, queue/type pause state, retained DTO queries, retry, release, archive, purge, and statistics. | Yes | 2026-11-19 |
| TASK-012 | Add `tests/Infrastructure.IntegrationTests/LiteDB/Durable/LiteDbQueueingTests.cs`. Verify competing consumers, delayed and expired work, handler failures, renewal and stale completion, pause/resume, retained search/statistics, retry/archive/purge, and reopen recovery through the existing queueing handler pipeline. | Yes | 2026-11-19 |

### Implementation Phase 6

- GOAL-006: Complete notification storage and email outbox operations. Depends on Phase 1 and may proceed independently of Phases 2 through 5. Execute TASK-013 before TASK-014. Completion requires byte-exact attachments and persisted delivery outcomes through configured transport.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-013 | Add `src/Infrastructure.LiteDB/Notifications/LiteDbNotificationStorageProvider.cs`, `LiteDbNotificationEmailOutboxService.cs`, and `LiteDbNotificationBuilderExtensions.cs`. Implement generic notification save/update/delete/pending selection and every email outbox query, content, statistics, retry, archive, auto-archive, and purge operation. Preserve attachment bytes and existing notification status/attempt fields. Reuse the existing notification outbox pipeline and selected transport. | Yes | 2026-11-19 |
| TASK-014 | Add `tests/Infrastructure.IntegrationTests/LiteDB/Durable/LiteDbNotificationTests.cs`. Verify save/reopen, attachment round trips, pending bounds, failed and successful delivery state, query filters, retry, archive, purge, cancellation, and transport integration. | Yes | 2026-11-19 |

### Implementation Phase 7

- GOAL-007: Reconcile supported coverage and documentation. Depends on Phases 2 through 6. TASK-015 precedes TASK-016. Completion requires all declared supported registrations to pass their feature checks and the existing LiteDB suite.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-015 | Build `src/Infrastructure.LiteDB/Infrastructure.LiteDB.csproj` with XML documentation enabled, then run `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter FullyQualifiedName~LiteDB`. Inspect `obj/project.assets.json` for EF dependencies. Verify no changes under `src/Domain/ActiveEntity` and byte-identical default project README. Save execution evidence outside `docs/`. | Yes | 2026-11-19 |
| TASK-016 | Update COV-014 through COV-019 status in `docs/specs/spec-infrastructure-litedb-providers.md`, marking COV-018 excluded and marking other items complete only after their contract and integration checks pass. Add Jobs, Orchestrations, Messaging, Queueing, and Notifications sections only in `docs/features-infrastructure-litedb.md`. Update task completion dates and plan status to match observed results. | Yes | 2026-11-19 |

Verification on 2026-11-19 passed 189 LiteDB integration cases, including 42 new durable-provider cases. The XML documentation build passed with zero warnings and errors. Dependency and source checks found no EF runtime dependency, no changes to ActiveEntity core, and an unchanged default project README. The focused build used existing dependency outputs with `BuildProjectReferences=false`; it does not certify a fresh solution rebuild. Production availability remains blocked by the pinned upstream development build.

### Implementation Phase 8

- GOAL-008: Correct the provider integration findings from the 2026-11-20 code review. Keep changes in Infrastructure.LiteDB and preserve application contracts, hosted services, notification transport, and audit behaviors.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-017 | Add the native notification email worker and immutable claim token. Select bounded candidate identities, acquire each claim immediately before delivery, renew ownership during delivery, and cancel stale work. Delegate processed-only purge to native sent/failed status selection without claiming pending emails. | Yes | 2026-11-20 |
| TASK-018 | Resolve message type display names and CLR identities through known subscriptions for both brokers. Reject unknown or ambiguous names instead of silently recording ineffective controls. | Yes | 2026-11-20 |
| TASK-019 | Add ten regression cases covering the four failing review probes, purge selection, renewal, stale ownership, ambiguous types, the real notification service and selected behavior, and retry-limit failure reporting. Pass all 199 LiteDB cases and an XML documentation build with zero warnings and errors. Update only the central usage guide and planning records. | Yes | 2026-11-20 |

The 2026-11-20 verification passes 199 LiteDB cases, including all ten review regression cases. Dependency and source checks continue to show no EF dependency, no changes to application contracts or ActiveEntity core, and an unchanged default project README. The focused build uses existing dependency outputs. Execution evidence remains outside the documentation tree.

### Implementation Phase 9

- GOAL-009: Correct the second review findings for COV-014 and COV-015. Preserve the existing publisher and handler contracts, transaction session, and ownership checks.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-020 | Run the scoped outgoing publisher through MessageBrokerBase before native capture. Resolve admission behaviors from its current scope and outbox options. Capture enriched payloads and allow guard failures to roll back entity transactions. Preserve deferred producer auditing during downstream forwarding. | Yes | 2026-11-20 |
| TASK-021 | Dead-letter permanent deserialization errors in both native brokers. Persist a separate failure budget for exceptions outside the normal handler outcome pipeline, enforce MaxDeliveryAttempts, and clear that budget on manual retry. Preserve completed handler states and current-token fencing. | Yes | 2026-11-20 |
| TASK-022 | Add nine regression cases covering the three failing probes, transient failure limits, retry after repair, budget persistence across file reopen, enriched capture, downstream behavior execution, and option-configured guards. Pass all 208 LiteDB cases and the XML documentation build with zero warnings and errors. Update the central guide and specification. | Yes | 2026-11-20 |

The second correction verification on 2026-11-20 passes 208 LiteDB cases, including all nine new delivery failure regression cases. The focused XML documentation build has zero warnings and errors. No application contract, ActiveEntity core, default README, or EF dependency changes are required.

## 3. Alternatives

- **ALT-001**: Referencing `Infrastructure.EntityFramework` adapters is rejected because it adds EF runtime dependencies and database-context semantics.
- **ALT-002**: Replacing feature engines with LiteDB-specific schedulers, workflows, handlers, or transports is rejected because storage selection must preserve application APIs.
- **ALT-003**: Persisting one whole in-memory provider snapshot is rejected because unrelated records would share one growing document and non-atomic memory/database state.
- **ALT-004**: Implementing legacy JobScheduling beside Jobs is rejected by user direction. Quartz's internal persistence is not part of COV-017.
- **ALT-005**: Exposing placeholder registrations before contract checks is rejected. Incomplete phases stay explicitly in progress.

## 4. Dependencies

- **DEP-001**: Existing LiteDB owner/session and COV-001/COV-002/COV-003/COV-005 implementations in the current working tree.
- **DEP-002**: The five existing application projects named in TASK-002 and their Common and Domain dependencies.
- **DEP-003**: Official pinned LiteDB prerelease and .NET versions in `global.json`, `Directory.Build.props`, and `Directory.Packages.props`.
- **DEP-004**: xUnit, Shouldly, NSubstitute for external delivery doubles, and real temporary database files. No Docker or external transport credentials are required.

## 5. Files

- **FILE-001**: `src/Infrastructure.LiteDB/Durable/*.cs` and `src/Infrastructure.LiteDB/Infrastructure.LiteDB.csproj` for shared native record commands and project references.
- **FILE-002**: `src/Infrastructure.LiteDB/Jobs/*.cs` for COV-017.
- **FILE-003**: `src/Infrastructure.LiteDB/Orchestrations/*.cs` for COV-016.
- **FILE-004**: `src/Infrastructure.LiteDB/Messaging/*.cs` and `Messaging/Outbox/*.cs` for COV-014.
- **FILE-005**: `src/Infrastructure.LiteDB/Queueing/*.cs` for COV-015.
- **FILE-006**: `src/Infrastructure.LiteDB/Notifications/*.cs` for COV-019.
- **FILE-007**: `tests/Infrastructure.IntegrationTests/LiteDB/Durable/*.cs` for contract, concurrency, restart, and integration checks.
- **FILE-008**: `docs/features-infrastructure-litedb.md`, `docs/specs/spec-infrastructure-litedb-providers.md`, and this plan for usage and progress.

## 6. Testing

- **TEST-001**: Verify the existing 147 LiteDB cases continue to pass; report the updated count from the final test result, not a planned count.
- **TEST-002**: Run all Jobs facet tests, including atomic batch rejection, deduplication races, lease replacement, accepted-event cursors, scheduler query composition, and reopen.
- **TEST-003**: Run orchestration store tests, including concurrency-key and version conflicts, ownership fences, signals/timers, administration, and reconstructed contexts.
- **TEST-004**: Run messaging tests with independent handler outcomes and an independent outgoing broker double. Test persisted waiting state and retry without redelivering already completed handler states.
- **TEST-005**: Run queueing tests with two worker calls against one file. Confirm only the valid lease owner finalizes and single-consumer semantics hold.
- **TEST-006**: Run notification tests with fictional addresses and byte-exact attachments. Use a transport double and verify persisted outcomes.
- **TEST-007**: Verify feature collection and named database isolation, bounded failure instead of truncation, cancellation rollback, and no implicit prerelease acknowledgement.
- **TEST-008**: Verify XML documentation build, documentation links, unchanged default README, no EF runtime dependency, and no excluded provider references.

## 7. Risks & Assumptions

- **RISK-001**: The upstream prerelease remains development-only. Passing tests do not establish production support.
- **RISK-002**: Handlers and transports have at-least-once delivery boundaries. A process can exit after an external side effect and before durable completion; consumers must tolerate redelivery.
- **RISK-003**: Long entity transactions can delay background claims and renewal on the shared owner. Expired claims must fail ownership checks even if the same worker resumes later.
- **RISK-004**: Existing EF implementations contain backend-specific query and discovery code. Only portable behavioral semantics may be reused.
- **ASSUMPTION-001**: DevKit feature storage models remain stable, and the LiteDB package has no deployed consumers requiring old API or model compatibility.
- **ASSUMPTION-002**: One application process owns each Direct-mode local file. Multiple workers within that process share named ownership and independent command sessions.
- **ASSUMPTION-003**: Definition catalogs and handler registrations remain code-owned. Persistence stores execution state and stable identifiers, not executable definitions.

## 8. Related Specifications / Further Reading

- [LiteDB storage provider specification](../specs/spec-infrastructure-litedb-providers.md)
- [Central LiteDB infrastructure guide](../features-infrastructure-litedb.md)
- [Existing ActiveEntity implementation record](pln-feature-litedb-activeentity-1.md)
- [Clean Architecture](../adr/0001-clean-onion-architecture.md)
- [Modular monolith architecture](../adr/0003-modular-monolith-architecture.md)
