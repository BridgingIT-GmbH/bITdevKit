---
created: 2026-11-19
status: in progress
---

# LiteDB Storage Providers

Provide LiteDB v6 persistence wherever DevKit exposes a replaceable storage backend. First migrate existing LiteDB v5 integration to v6, then extend provider coverage. Applications can run durable DevKit features against local database files without a database server, Entity Framework Core, or relational schema migrations.

## 1. Purpose and scope

The target is LiteDB v6 coverage of the required DevKit persistence contracts, excluding sequence numbers (COV-004), event sourcing (COV-006), legacy JobScheduling (COV-018), and log-entry persistence (COV-023), with all existing and new LiteDB implementation code in `Infrastructure.LiteDB`. The v5-to-v6 dependency and code migration is the first required phase, including a representative v5 file-compatibility check and any needed upstream conversion guidance. Provider expansion follows that migration, and completion requires the full inventory in section 4.

The dependency upgrade and 19 provider coverage items are implemented and verified for development use as recorded in section 10. COV-008 Blob Storage is the only missing provider for an existing contract in the current inventory, and its implementation is deferred. COV-007 document storage is implemented; further document enhancements are deferred. COV-025 Key/Value Storage remains a draft DevKit feature and waits for its owning contracts and runtime. COV-004, COV-006, COV-018, and COV-023 are excluded by user direction.

### Agreed assumptions

- Assume no existing consumers of the DevKit LiteDB package. Its current public API, context design, mapping, and helpers may be refactored or replaced freely. Existing provider-neutral DevKit contracts remain authoritative.
- DevKit models used for feature storage are stable. The implementation does not need a compatibility layer for historical model formats.
- Database initialization creates collections and ensures indexes automatically. Consuming applications do not maintain schema migrations for LiteDB providers.
- Enabled LiteDB features share one default named database registration, with separate feature collections. Additional named databases require explicit configuration.
- DevKit model-format versions, upcasters, legacy model readers, and a general-purpose model migration framework are outside this scope. The explicitly required v5-to-v6 dependency and code migration is included. No deployed v5 application databases or consumer rollout must be supported.
- LiteDB v6 is the implementation baseline, including a published prerelease while v6 is not stable. The exact v6 package is selected, pinned, and validated when implementation starts.
- Use official published upstream packages. A DevKit-maintained LiteDB fork, patched engine build, or vendored engine is outside scope. Engine defects block affected providers until a suitable upstream package is available.
- Independently complete providers may ship incrementally. Full coverage remains the overall completion goal, and every release must identify supported, unfinished, and upstream-blocked coverage items.
- Functionality comes first. Correctness and bounded resource use are release requirements. Performance measurement and optimization follow functional delivery, without fixed throughput or latency promises.
- ETags, aggregate event sequence numbers, cache publication fences, and lease ownership tokens remain required where existing contracts use them. These represent concurrency or feature state, rather than storage-format compatibility.
- Existing feature behavior and provider contracts remain authoritative. Choosing LiteDB changes persistence, while callers continue using the same application APIs.

### Meaning of provider coverage

Coverage includes server-side persistence contracts named `Provider`, `Store`, `Repository`, or `Service`, plus the persistence required by durable brokers and outbox workers. It includes features whose only current implementation is in memory and the legacy persistence contracts still shipped by DevKit, except the explicitly excluded COV-004, COV-006, COV-018, and COV-023.

It excludes providers for transport delivery, authentication protocols, export formats, serialization, mapping, clocks, and other behavior that does not store application or feature data. Browser-owned AppState storage and code-registration catalogs also remain outside the LiteDB storage backend.

The first supported runtime release targets production use with one application process per local database file, including concurrent requests and background workers. A selected development-only upstream package does not establish production support. Shared-file access by multiple processes, cross-machine database-file sharing, and distributed database hosting are outside this release's scope.

## 2. Current state and definitions

The existing [Infrastructure.LiteDB project](../../src/Infrastructure.LiteDB/Infrastructure.LiteDB.csproj) targets the repository runtime framework and publishes the assembly `BridgingIT.DevKit.Infrastructure.LiteDb`. The initial dependency upgrade changes [Central Package Management](../../Directory.Packages.props) from `5.0.21` to `6.0.0-prerelease.355`, published from upstream commit `b3c4406231908c6ca29f6d4b2769aa08351d6fc2`. The project contains native repository and ActiveEntity adapters, shared named ownership and scoped sessions, repository transactions, bulk insertion, domain-event outbox capture and delivery, and portable change-history capture, queries, and restore. ActiveEntity remains independent of the generic repositories.

This selected package identifies itself as a development prerelease and requires the process-wide call `LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS()` before database construction. Upstream explicitly excludes production use for this build. Disposable-data tests acknowledge the risk explicitly. `LiteDbContext` preserves the guard, and future provider registration must not acknowledge it automatically. Production support remains blocked until a suitable upstream package is available.

Use the existing project and directory spelling, `Infrastructure.LiteDB` and `src/Infrastructure.LiteDB`, and preserve the existing assembly and namespace casing. All LiteDB implementation code belongs in this project, including existing integrations found elsewhere during implementation. This extension does not require a package or directory rename.

At specification capture, the upstream [v6 milestone](https://github.com/litedb-org/LiteDB/milestone/6) is open and [published releases](https://github.com/litedb-org/LiteDB/releases) include v6 prereleases. Milestone work is not proof that a published package contains a change. Resolve the actual API, format, and supported behavior against the pinned package and its source revision.

- **Storage provider**: A replaceable implementation of a DevKit persistence contract.
- **BSON**: Binary JSON, the document representation used by LiteDB.
- **v5-to-v6 migration**: The dependency upgrade, source refactoring, representative v5 file-compatibility check, and documentation of any upstream conversion requirement. This does not imply a deployed-consumer upgrade service or a DevKit model-schema migration framework.
- **Database registration**: A named LiteDB database configuration and its owned runtime instance.
- **Storage namespace**: The identity separating a feature, module, named client, collection, or location within a database.
- **Durable**: Committed feature state remains available after the owning process stops and the same database reopens.
- **Atomic publication**: Readers observe complete committed content and its matching metadata.
- **ETag**: An opaque token used to condition a write or delete on the version a caller read.

## 3. Requirements, constraints, and guidelines

### v6 migration and code consolidation

- **MIG-001**: Target LiteDB v6 for all new providers. Select an exact official published upstream v6 package, including a prerelease while necessary, and pin it through Central Package Management using the repository's package-management workflow. Record the package, source revision, supported platforms, and relevant upstream limitations. Do not use a floating development branch, maintain a fork, vendor or patch the engine, or silently fall back to v5.
- **MIG-002**: Migrate or replace the existing `LiteDbContext`, repositories, mapping configuration, and query helpers for the selected v6 API before expanding provider coverage. Refactor the LiteDB package freely under the no-existing-consumers assumption. Preserve the behavior required by existing provider-neutral contracts and verify restore, build, and focused integration tests on v6. Public API compatibility shims for the old LiteDB integration are unnecessary.
- **MIG-003**: Inventory all direct LiteDB dependencies, imports, database operations, and backend-specific helpers across `src/`. Consolidate both existing and new LiteDB contexts, providers, adapters, brokers, workers, mapping, logging sinks, and backend helpers in `src/Infrastructure.LiteDB`. Consumers select the backend through registration and existing contracts. Keep test code in test projects, central versions in `Directory.Packages.props`, and generic assembly-filter strings in their current owner. Update affected project references, imports, registrations, and documentation without introducing additional LiteDB implementation projects.
- **MIG-004**: Verify the selected v6 engine's actual handling of v5 database files. Establish whether files open unchanged, upgrade on open, or require an upstream conversion or rebuild. Document file and index transitions, mapper and query changes, timestamp semantics, encryption behavior, and transaction ownership where they affect DevKit. Do not infer file compatibility from an unchanged C# API or an upstream milestone item.
- **MIG-005**: Scope the migration to the toolkit integration. There is no existing consumer upgrade, mixed-version rollout, or production v5 dataset to preserve. Do not build old-API adapters, an operator cutover workflow, or a historical database migration service for hypothetical consumers.
- **MIG-006**: Run a bounded compatibility check with representative files created by LiteDB `5.0.21`, using disposable working copies. Record whether the selected v6 engine reads them directly, requires upstream conversion, or rejects them. Verify representative identities, indexed queries, content, and timestamp instants for any supported transition. Document the observed result without promising permanent v5 file support.
- **MIG-007**: Use and document upstream conversion mechanisms if the compatibility check establishes that conversion is necessary. Add custom migration tooling only if that verification demonstrates a concrete DevKit requirement that upstream tooling cannot satisfy. Preserve the original test fixture when checking conversion. A general-purpose migration framework and deployed-consumer rollback tooling remain outside scope.
- **MIG-008**: Review the upstream v6 release-readiness checklist against the pinned candidate. Verify the engine behavior used by DevKit, particularly ownership, transaction boundaries, recovery, mapping, indexes, and dates. An open proposal or merged PR does not establish package support. A candidate defect that prevents a required guarantee blocks the affected provider until a suitable upstream v6 package ships. Track that blocker explicitly against its coverage item. Unaffected providers can progress once the common migration baseline passes, without weakening the blocked provider's guarantees or maintaining an engine fork.

### Registration and ownership

- **REQ-001**: Every implemented DevKit persistence extension point must have a LiteDB implementation or an explicit composition path through another LiteDB-backed provider. Section 4 is the baseline inventory, and implementation must reconcile it against the source tree before declaring coverage complete.
- **REQ-002**: Each feature must be independently selectable through its existing registration pattern. An application can combine LiteDB providers with other backends without replacing unrelated registrations.
- **REQ-003**: Provide one default named database registration shared by enabled LiteDB features, with separate feature collections. Permit additional named registrations and files only when explicitly configured. Namespace identities must prevent collisions between features, modules, named clients, document types, and file locations.
- **REQ-004**: The database registration owns database lifetime. Providers and workers reuse that instance and do not independently open competing Direct connections to the same file or dispose a database owned by another registration.
- **REQ-005**: Initialization must be idempotent and safe for concurrent callers. It creates the required collections and indexes, preserves existing data, and permits retry after initialization failure.
- **REQ-006**: A fresh application must initialize and use all selected providers without a `DbContext`, EF model configuration, migration assembly, schema script, or external database service.
- **REQ-007**: Validate database paths, namespace options, incompatible duplicate registrations, and connection modes before starting feature workers. Reject Shared mode in the first release. Database files persist when the service provider is disposed.

### Contracts and data

- **REQ-008**: Implement existing contracts with their current return types, filtering, ordering, paging, metadata, expiration, and error semantics. Do not introduce a parallel LiteDB application API or leave required operations as stubs.
- **REQ-009**: Reuse stable DevKit storage models where their ownership permits. Use small LiteDB persistence records when mapping is needed, without adding LiteDB references or mapping attributes to Domain or Application models.
- **REQ-010**: Persist Document Storage's serialized and optionally transformed bytes unchanged. `IDocumentStoreClient<T>` continues to own serialization, transforms, integrity validation, and public input validation. LiteDB object mapping must not bypass that pipeline.
- **REQ-011**: Map identifiers, typed IDs, enumerations, nulls, empty values, timestamps, binary content, and property dictionaries without changing their contract meaning. Explicitly configure mapper defaults when needed to preserve values.
- **REQ-012**: Use indexed identities and bounded queries for point reads, paging, due work, leases, and retention. Keep payloads and growing histories out of unbounded embedded arrays. Declare payload limits and index constraints through existing capability or options models.
- **REQ-013**: Support stable-model v6 persistence without a DevKit model migration or historical-format compatibility subsystem. Normal startup performs initialization only. Reopening committed v6 data under the same models must preserve it.

### Concurrency and durability

- **REQ-014**: Conditional writes, create-only operations, counter updates, claims, lease renewal, and ownership checks must be atomic in the database. A process-local lock alone does not establish persistent ownership.
- **REQ-015**: Transactions must preserve the atomic boundaries required by the feature. Repository changes and their outbox records must commit or roll back together when they use the same database registration. Separate database files do not provide a shared transaction.
- **REQ-016**: Honor the caller-owned asynchronous scopes required by `IDatabaseTransaction`, `IRepositoryTransaction<TEntity>`, and ActiveEntity transaction APIs. Preserve transaction ownership across their awaited repository operations using execution boundaries supported by the pinned engine. Independent per-operation commits do not satisfy an explicitly requested multi-operation transaction. Keep external transport I/O and whole streamed uploads outside short persistence transactions. If the engine cannot uphold a required scope safely, apply MIG-008 rather than weakening the contract.
- **REQ-017**: Messaging, queueing, jobs, and orchestrations must recover committed pending work after restart. Expired claims become eligible again, and an old owner cannot finalize work after a newer owner has acquired it. Preserve each feature's retry, deduplication, and delivery semantics.
- **REQ-018**: Stream binary content in bounded chunks. Stage replacements and publish matching content metadata atomically. Failed, cancelled, or interrupted writes must not expose incomplete content or destroy the previous committed value. Maintenance must reclaim abandoned staging data without deleting active content.
- **REQ-019**: Support concurrent requests and workers within one owning process through Direct mode. A second process must fail to acquire the same database file before accepting work against it. Report single-process local scope in capability models and documentation. Shared mode is deferred.
- **REQ-020**: Honor cancellation before admission and between cooperative work units. Expose the existing asynchronous contracts honestly over LiteDB operations. Do not claim cancellation can interrupt an already committed synchronous database operation.

### Operations and integration

- **REQ-021**: Support the operational services used by existing dashboards, endpoints, Console Commands, and MCP tools, including retained-state inspection and permitted maintenance. Provider selection must not make those surfaces display an empty in-memory substitute for persisted state.
- **REQ-022**: Preserve feature authorization, auditing hooks, structured logging, correlation, metrics, retention, and health reporting. Database passwords and persisted payloads must not appear in diagnostic configuration or routine logs.
- **REQ-023**: Report optional capabilities truthfully. Implement a supported native facet where applicable, or use the existing bounded fallback when the contract defines one. Required contract behavior cannot be removed through a capability flag.
- **REQ-024**: Database initialization, cleanup, and retention must remain bounded and scoped to the selected registration and namespace. Disposing a provider or reconfiguring one feature must not delete another feature's data.
- **REQ-025**: Keep Core Domain and Application dependencies pointing inward. Add only the feature contract references needed by LiteDB adapters. No LiteDB persistence implementation may depend on EF Core, SQL provider packages, or EF storage entities.
- **REQ-026**: Permit incremental releases of independently complete providers after the common v6 migration baseline passes. A released provider must satisfy its required contract and operational integration tests. Maintain an explicit support checklist against the coverage inventory. Unfinished or upstream-blocked implementations must not be exposed as selectable supported providers or replaced by placeholders.
- **REQ-027**: Prioritize complete functional behavior, correctness, and bounded resource use. A provider does not need a fixed throughput target, latency target, or completed benchmark campaign to ship. After functional delivery, measure representative workloads on identified hardware before making performance claims or optimizing storage behavior.

## 4. Interfaces and coverage inventory

Each coverage item is required unless it is explicitly excluded or identified as a planned-contract target. COV-004, COV-006, COV-018, and COV-023 are excluded by user direction. Supporting operational facets belong to the same item as their persistence backend. Type names below are existing symbols, not proposed registration APIs.

### Domain persistence

- **COV-001, repositories and bulk insertion**: **Implemented and verified for development use.** Implements [IGenericRepository<TEntity>](../../src/Domain/Repositories/IGenericRepository.cs) and [IGenericReadOnlyRepository<TEntity>](../../src/Domain/Repositories/IGenericReadOnlyRepository.cs), including specifications, projections, paging, set operations, and the existing transaction contracts. Provides LiteDB support for [IEntityBulkInserter<TEntity>](../../src/Domain/Repositories/IEntityBulkInserter.cs) with its existing validation and explicit decorator semantics.
- **COV-002, ActiveEntity**: **Implemented and verified for development use.** Implement [IActiveEntityEntityProvider<TEntity, TId>](../../src/Domain/ActiveEntity/Provider/IActiveEntityEntityProvider.cs), including transactions and the existing behavior pipeline. Implement persistence directly through `LiteDbContext` and native LiteDB APIs, without using the generic repository classes. Reuse shared LiteDB persistence mechanics where this preserves the ActiveEntity contract. Follow the completed [provider implementation plan](../plan/pln-feature-litedb-activeentity-1.md). Keep implementation provider scoped, with no changes to ActiveEntity core, contracts, behaviors, query DSL, or source generators.
- **COV-003, domain event outbox**: **Implemented and verified for development use.** Implements [IOutboxDomainEventWorker](../../src/Domain.Outbox/IOutboxDomainEventWorker.cs) and the repository or ActiveEntity capture integrations. Persist event identity, content, context, and processing state with the originating mutation. The existing [EF outbox implementation](../../src/Infrastructure.EntityFramework/Repositories/Outbox) is a behavioral reference, not a runtime dependency.
- **COV-004, sequences**: **Excluded from this extension by user direction.** No LiteDB sequence-number provider is planned in this scope.
- **COV-005, change history**: **Implemented and verified for development use.** Implements [IChangeHistoryService<TEntity, TContext>](../../src/Application.Entities/ChangeHistory/ChangeHistoryContracts.cs) for a LiteDB context, with capture, change sets, queries, restore authorization, restore concurrency, and atomic entity mutation plus history capture. Existing EF interceptors and EF-specific dashboard discovery must not become dependencies of the storage adapter.
- **COV-006, event sourcing**: **Excluded from this extension by user direction.** LiteDB event sourcing and its legacy message-outbox path are outside this scope. COV-003 domain-event outbox remains required.

### Storage and caching

- **COV-007, documents**: **Implemented and verified for development use.** Implements [IDocumentStoreProvider](../../src/Application.Storage/Documents/IDocumentStoreProvider.cs) and its retention facet with exact-key access, bounded deterministic paging, metadata, ETags, conditional mutation, expiration, and initialization. Batch-get and bulk-write use the existing client fallback paths, preserving partial-result and ordering rules. Additional native batch-get and bulk-write facets are optional enhancements and remain deferred with further document-storage work.
- **COV-008, blobs**: **Deferred by user direction; not implemented.** This is the only missing provider for an existing contract in the current coverage inventory. When work resumes, implement [IBlobStoreProvider](../../src/Application.Storage/Blobs/IBlobStoreProvider.cs), the container catalog, and retention. Support streaming, conditional publication, range reads, metadata, integrity, and bounded listing according to the contract. LiteDB `FileStorage` is a possible storage mechanism, but its per-chunk transactions alone do not satisfy atomic whole-object publication.
- **COV-009, files**: **Implemented and verified for development use.** Implements [IFileStorageProvider](../../src/Application.Storage/Files/IFileStorageProvider.cs) as a virtual filesystem with explicit directories, normalized logical paths, streaming, metadata, listing, copy, move, rename, and delete behavior. Register through the existing factory and preserve cross-provider helper behavior. Logical file paths must not become host filesystem paths.
- **COV-010, file monitoring**: **Implemented and verified for development use.** Implements [IFileEventStore](../../src/Application.Storage/Monitoring/Events/IFileEventStore.cs) for event history, processing results, location queries, and present-file detection. The event store remains independently selectable from the file-content provider.
- **COV-011, storage permalinks**: **Implemented and verified for development use.** Implements [IStoragePermalinkRegistryProvider](../../src/Application.Storage/Permalinks/IStoragePermalinkRegistryProvider.cs), including stable resource identity, uniqueness, move and delete handling, expiration, and maintenance.
- **COV-012, Application Caching**: **Implemented and verified for development use.** Implements [ICacheStoreProvider](../../src/Application.Caching/Providers/ICacheStoreProvider.cs), including read validation, streaming write sessions, publication fences, expiration, truthful ownership capabilities, invalidation, quotas, maintenance, and persisted operational state. Shared-node ownership may report Unsupported when the native provider advertises no shared-store coordination. Preserve the source-loading and refresh behavior owned by Application Caching.
- **COV-013, Common Caching**: **Implemented and verified for development use.** Supplies an explicit LiteDB composition path for [ICacheProvider](../../src/Common.Abstractions/ICacheProvider.cs) through the existing [DocumentStoreCacheProvider](../../src/Application.Storage/Caching/DocumentStoreCacheProvider.cs) and its distributed-cache facade. Preserve key enumeration, prefix removal, and sliding and absolute expiration. A second independent cache implementation is unnecessary when this composition satisfies the contract.

### Durable execution and delivery

- **COV-014, messaging**: **Implemented and verified for development use.** Implement [IMessageBroker](../../src/Common.Abstractions/Messaging/IMessageBroker.cs), the [messaging runtime](../../src/Application.Messaging/IMessageBrokerRuntime.cs), and [IMessageBrokerService](../../src/Application.Messaging/IMessageBrokerService.cs). Persist messages and handler progress, and preserve fan-out, waiting-for-handler state, claims, retries, expiration, pause controls, retained queries, archive, and purge. Include durable outgoing-message outbox capture and [IOutboxMessageWorker](../../src/Application.Messaging/Outbox/IOutboxMessageWorker.cs), independently selectable from the broker receiving the published message.
- **COV-015, queueing**: **Implemented and verified for development use.** Implement [IQueueBroker](../../src/Common.Abstractions/Queueing/IQueueBroker.cs), the [queueing runtime](../../src/Application.Queueing/IQueueBrokerRuntime.cs), and [IQueueBrokerService](../../src/Application.Queueing/IQueueBrokerService.cs). Preserve single-consumer claims, lease renewal, retries, delayed eligibility, expiration, dead-letter state, pause controls, retained queries, archive, and purge.
- **COV-016, orchestrations**: **Implemented and verified for development use.** Implement [IOrchestrationStorageProvider](../../src/Application.Orchestrations/Storage/IOrchestrationStorageProvider.cs) and all its stores for instances, leases, history, signals, timers, queries, and administration. Preserve resumable state transitions and ownership checks across restart.
- **COV-017, Jobs**: **Implemented and verified for development use.** Implement [Application.Jobs.IJobStoreProvider](../../src/Application.Jobs/Persistence/IJobStoreProvider.cs) and every exposed store, including accepted events, leases, occurrences, dependencies, batches, execution history, previous-execution lookup, and scheduler queries. Scheduler definitions remain code-owned where the existing feature requires them.
- **COV-018, legacy JobScheduling**: **Excluded from this extension by user direction.** No LiteDB `Application.JobScheduling` provider or Quartz store is required. COV-017 Jobs remains in scope.
- **COV-019, notifications**: **Implemented and verified for development use.** Implement [INotificationStorageProvider](../../src/Application.Notifications/Provider/INotificationStorageProvider.cs), notification outbox persistence, and [INotificationEmailOutboxService](../../src/Application.Notifications/INotificationEmailOutboxService.cs). Preserve pending selection, persisted attempts and outcomes, attachments, and existing operational queries. Delivery continues through the configured notification transport.

### Diagnostics, access, and discovery

- **COV-020, auditing**: **Implemented and verified for development use.** Implements [IAuditingStorageProvider](../../src/Application.Auditing/Storage/IAuditingStorageProvider.cs), including recording, read scopes, search, export reads, integrity state, and retention. Preserve audit evidence guarantees and authorization policies.
- **COV-021, profiling**: **Implemented and verified for development use.** Implements [IProfilingStorageProvider](../../src/Common.Abstractions/Profiling/ProfilingStorageAbstractions.cs) with its runtime and operation stores, applicable correlation queries, writer leases, clear fencing, and maintenance. Store the complete runtime and operation graphs needed by the existing diagnostics and export services.
- **COV-022, entity permissions**: **Implemented and verified for development use.** Implements [IEntityPermissionProvider](../../src/Application.Identity/IEntityPermissionProvider.cs), preserving user and role grants, revocation, hierarchy traversal, and the existing permission-resolution rules. Domain entities and permission records must be resolvable through the selected LiteDB database without EF hierarchy-query services.
- **COV-023, log entries**: **Excluded from this extension by user direction.** The host keeps its SQL Server logging sink. No LiteDB log-entry provider or Serilog sink is planned in this scope.
- **COV-024, broadcasting registry**: **Implemented and verified for development use.** Implements [IBroadcastRegistryStore](../../src/Common.Utilities/Broadcasting/BroadcastingAbstractions.cs), preserving node identities, scopes, lease expiry, delivery observations, and operational listing. Report local database scope truthfully. This stores registry state and does not turn short-lived broadcasts into durable messages or provide cross-machine file access.

### Planned contracts and existing bridges

- **COV-025, Key/Value Storage**: **Draft owning feature; waiting for DevKit contracts and runtime.** The [Key/Value Storage specification](spec-application-storage-keyvalue.md) is a future coverage target. It is not a ready LiteDB provider implementation task. After the owning feature implements its contracts and runtime, add LiteDB persistence for values, conditional writes, counters, expiration, and the contract's required coordination state. Keep the owning feature's contract and runtime implementation outside this extension. Existing Common Caching and Application Caching coverage does not satisfy this future item.
- Server-hosted AppState may compose its existing custom storage adapter with a LiteDB-backed application storage client where the host supports that integration. Browser `localStorage` and session storage remain browser implementations. `Infrastructure.LiteDB` must not reference Presentation to implement a browser contract.
- Runtime catalogs, including code-first job registrations, handler registrations, and delegates, retain their existing lifecycle. Persisting registry data such as COV-024 does not imply serializing executable objects.

## 5. Acceptance criteria

### Story M: Adopt v6 before adding providers

As a DevKit maintainer, I want the existing LiteDB implementation migrated to a pinned v6 build so that every new provider uses the same verified baseline.

- **AC-018**: Given the current v5 dependency and existing repository integration, when the migration phase completes, then the centrally pinned dependency is v6, existing LiteDB behavior passes on that package, and no new provider still targets v5. Covers MIG-001, MIG-002, and MIG-008.
- **AC-019**: Given representative files created by LiteDB `5.0.21`, when the selected v6 engine checks disposable copies, then the migration record states whether direct reading succeeds, upstream conversion is required, or the file is unsupported. For a supported transition, the expected records and indexed query results remain correct. Covers MIG-004 and MIG-006.
- **AC-020**: Given the no-existing-consumers assumption, when the LiteDB integration is refactored for v6, then it satisfies the current provider-neutral contracts without requiring old LiteDB public API compatibility. Any required file conversion uses documented upstream mechanisms, and custom tooling requires a demonstrated gap. Covers MIG-002, MIG-005, and MIG-007.
- **AC-021**: Given a source-tree dependency inventory, when code consolidation completes, then all non-test LiteDB implementation resides in `Infrastructure.LiteDB`, references resolve with that exact project casing, and consumers use the existing contracts and registration surface. Covers MIG-003.
- **AC-022**: Given the selected official v6 package, when its API and storage behavior are compared with the required DevKit guarantees, then the migration record identifies verified behavior, relevant unresolved upstream dependencies, and blocked providers without treating milestone proposals as shipped features. A blocked provider waits for an upstream package fix, and unaffected providers can progress without a DevKit engine fork. Covers MIG-001, MIG-004, and MIG-008.

### Story A: Select LiteDB without database migrations

As an application developer, I want to select LiteDB for DevKit storage so that durable features run without a separate database server or schema migrations.

- **AC-001**: Given an empty writable directory, when the host initializes its selected LiteDB providers, then it creates the required database resources and can persist feature data without a migration command or EF dependency. Covers REQ-002 through REQ-007.
- **AC-002**: Given committed stable-model records in a v6 database, when the host stops and reopens that database, then those records remain readable and initialization preserves them without model compatibility handlers or conversion steps. Covers REQ-005, REQ-009, and REQ-013.
- **AC-003**: Given enabled LiteDB features without explicit database overrides, when they initialize, then they resolve the same default named database with separate feature collections. Given explicit additional registrations or distinct storage namespaces, equal logical keys remain isolated and cleanup in one leaves the other intact. Covers REQ-003 and REQ-024.
- **AC-004**: Given an unwritable path or incompatible registration, when initialization runs, then the host receives a useful configuration or storage failure before feature workers accept work. Covers REQ-007 and REQ-022.

### Story B: Use every existing persistence feature

As a DevKit consumer, I want LiteDB adapters for all persistence contracts so that I can select a consistent embedded backend across features.

- **AC-005**: Given each required implemented-contract item COV-001 through COV-024 except COV-004, COV-006, COV-018, and COV-023, when a consumer registers its LiteDB implementation, then all required contract operations and operational queries run against LiteDB. Composition-based items must demonstrate that path explicitly. Covers REQ-001, REQ-008, and REQ-021.
- **AC-006**: Given independently selected providers, when a host uses LiteDB documents alongside a different broker or cache, then registrations compose without silently replacing other backends. Covers REQ-002 and REQ-025.
- **AC-007**: Given an optional capability, when a caller selects an operation requiring it, then the declared capability matches observed behavior or the existing contract returns its defined unsupported result. No required operation throws `NotImplementedException`. Covers REQ-008 and REQ-023.
- **AC-008**: Given the provider inventory changes before implementation completes, when coverage is reviewed, then every new persistence contract is added or explicitly classified, and COV-025 remains tracked until its owning contract exists. Covers REQ-001.
- **AC-023**: Given a release containing a completed subset of providers, when support is reviewed, then each supported provider has passing contract and operational integration tests, and the support checklist identifies the remaining coverage items and upstream blockers. Incomplete providers are not exposed as supported registration choices. Covers REQ-026.
- **AC-024**: Given a functionally complete provider with passing correctness and bounded-resource checks, when release readiness is reviewed, then missing performance measurements alone do not block the release. Later performance reports state the hardware, engine version, payloads, concurrency, durability settings, throughput, tail latency, and memory use behind their results. Covers REQ-027.

### Story C: Preserve concurrent and durable behavior

As an operator, I want LiteDB-backed work and state to remain correct during concurrent execution and restarts so that embedded deployment preserves DevKit's persistence guarantees.

- **AC-009**: Given competing conditional writes or claims, when they race, then only valid owners or matching versions succeed. A stale owner cannot finalize another worker's claim. Covers REQ-014, REQ-017, and REQ-019.
- **AC-010**: Given pending messages, queue items, jobs, or orchestrations, when a worker process restarts, then committed pending state remains available and expired leases permit recovery according to the feature contract. Covers REQ-017.
- **AC-011**: Given an entity change and its outbox or history records in one database, when the transaction rolls back, then none of those changes becomes committed. On commit, all required records become available together. Covers REQ-015, COV-003, and COV-005.
- **AC-012**: Given existing content, when a replacement upload fails, is cancelled, or the writer process exits before publication, then readers still see the previous complete content and maintenance can reclaim abandoned staging data. Covers REQ-018 and REQ-020.
- **AC-013**: Given expired data and a concurrent successful refresh or expiration extension, when retention processes a stale candidate, then it rechecks current state and preserves the refreshed item. Covers REQ-008, REQ-014, and REQ-024.

### Story D: Operate LiteDB through existing tools

As an operator, I want existing DevKit tools to inspect and maintain LiteDB state so that backend selection does not require separate operational workflows.

- **AC-014**: Given persisted LiteDB state, when existing authorized diagnostic services query it, then they return the selected backend's records, identities, counts, and actual capabilities. Covers REQ-019, REQ-021, and REQ-023.
- **AC-015**: Given denied access, when a caller requests protected data or maintenance, then the existing authorization policy prevents that operation. Covers REQ-022.
- **AC-016**: Given a large stream, result set, or backlog, when storage or maintenance runs, then it uses bounded chunks, pages, or batches and observes configured cancellation boundaries. Covers REQ-012, REQ-018, REQ-020, and REQ-024.
- **AC-017**: Given database ownership shared by several selected providers, when one provider scope is disposed, then the others remain usable until the owning registration shuts down. Shutdown releases the database so a subsequent process can reopen it. Covers REQ-004.

## 6. Test automation strategy

- Complete the v5-to-v6 dependency and code migration tests before starting provider expansion. Produce representative v5 files in an isolated test tool or process and check disposable copies with the pinned v6 package. Keep both engines out of the same application dependency graph.
- Keep the v5 compatibility check focused on representative records, indexes, and mapped values. Record the supported path and any required upstream conversion. No deployed-consumer rollout, old public API compatibility suite, production rollback campaign, or historical DevKit model fixtures are required.
- Validate the selected v6 transaction, mapper, query, ownership, and binary-storage behavior directly. Older public documentation is supplementary when it differs from the selected v6 package.
- Use xUnit and Shouldly, with NSubstitute only where an external dependency needs isolation. Tests exercise observable contracts against real temporary LiteDB files.
- Run the existing [document concurrency scenarios](../../tests/Shared/Storage/DocumentStoreConcurrencyContractTests.cs), [cache provider contract suite](../../tests/Shared/Caching/CacheProviderContractTestsBase.cs), and [profiling contract suites](../../tests/Shared/Profiling) against LiteDB fixtures. Reuse other feature suites where available and extract shared contract scenarios when an existing suite is coupled to an implementation.
- Cover every COV item with its normal flow and at least one failure or concurrency case. Required checks include exact byte round trips, mapper value preservation, conditional writes, namespace isolation, ordering, expiration, and bounded queries.
- Use real process reopen tests for durability and deterministic worker races for ownership. Add subprocess termination tests at staged-publication and transaction boundaries. These tests target restart behavior under stable models and do not require historical model fixtures.
- Direct-mode tests must prove that providers share the owned database instance, concurrent workers remain correct, a second process cannot acquire an owned file, and Shared-mode configuration is rejected. Reopen tests transfer ownership only after the previous owning process releases the file or exits.
- Exercise representative existing operational endpoints through a test host. Use fictional records and routes, and keep application-specific execution evidence outside `docs/`.
- Follow the repository's sequential build and test policy. Core LiteDB integration tests must run without Docker, a SQL server, cloud credentials, or a network service.
- Treat performance measurement as follow-up work after functional delivery. Reuse the storage load-test tools where applicable and include a mixed workload of documents, blobs, logs, and background processing against the shared default database. Record throughput, tail latency, and memory use with hardware and workload settings. Avoid hardware-independent performance promises and speculative optimization.

## 7. Rationale and context

The benefit of this extension is durable DevKit infrastructure with automatic local initialization and no relational schema migrations. Stable feature-storage models are an explicit assumption, so a general-purpose model compatibility subsystem is outside the requested outcome. The v5-to-v6 dependency and code migration is required before expansion. With no existing package consumers, the LiteDB integration can be redesigned without preserving its old public API or building a deployed-database upgrade service.

Building new providers on v6 avoids creating a larger v5 integration that immediately needs another engine migration. Prerelease adoption requires pinning and verification against actual package contents. The upstream [release-readiness checklist](https://github.com/litedb-org/LiteDB/issues/2623) tracks unresolved release work and does not establish that every v6 prerelease has the final behavior.

SQLite already provides local relational persistence through EF Core. LiteDB offers a direct object and document persistence option without requiring an EF model or migration set for each consuming host.

Storage behavior still matters independently of schema management. Conditional writes, claims, crash recovery, and atomic binary publication must retain their feature guarantees when implemented over LiteDB.

## 8. Dependencies and implementation boundaries

- Consolidate all existing and new LiteDB implementation in the existing `Infrastructure.LiteDB` project and reuse its migrated context where appropriate. Shared registration, namespace, transaction, mapping, and chunk mechanics belong there. Test fixtures remain in test projects.
- Reference the Common, Domain, and Application packages that own each implemented contract. Keep unrelated runtime features inactive until selected. Integrations for older abstractions also live in `Infrastructure.LiteDB` and remain free of EF dependencies.
- COV-006 event sourcing and its legacy message-outbox path are excluded. No event-sourcing adapter, contract relocation, or event-sourcing dependency refactor is required by this extension. COV-003 uses the existing `Domain.Outbox` contract; COV-005 uses `Application.Entities` and portable Domain policies. Neither adapter references EF.
- Upgrade the centrally managed LiteDB dependency from v5 to an explicitly pinned v6 build as the first phase. Use the .NET CLI and repository package-management guidance for Central Package Management and applicable lock files. Resolve the runtime and language versions from repository build files when implementation starts. Unrelated package upgrades remain outside scope.
- Reuse Result types, requester and notifier, serializers, `TimeProvider`, feature behaviors, FluentValidation, and logging conventions. A persistence adapter must not reimplement its feature's scheduling, transport, or workflow engine.
- Preserve Presentation's dependency direction. Add focused registration or discovery integration only where existing operational surfaces otherwise assume EF. No browser-specific LiteDB runtime is required.

## 9. Examples and edge cases

### Embedded order processing

A fictional order service selects LiteDB repositories, the domain event outbox, queueing, Jobs, and orchestration storage against one named local database. Creating an order and its outgoing event commits atomically. Restarting the service preserves pending work. Adding an optional stored field under the stable-model assumption requires no schema migration.

### Local document and attachment storage

A fictional records service uses separate named document and blob clients against one database registration. Documents retain the existing serialization and integrity pipeline. Replacing an attachment stages chunks before publication. An interrupted replacement leaves the previous committed attachment readable.

### Boundaries

- Representative v5 files establish the selected engine's compatibility behavior. New providers target v6 databases, and the compatibility check does not create a consumer database migration service.
- The same logical key in two clients identifies separate records when the existing feature's namespace rules require isolation.
- Large content must not rely on fitting a complete payload into one BSON document. Metadata and chunking must respect engine size limits.
- LiteDB's documented `FileStorage` transactions apply per chunk. The blob or file adapter must supply any stronger publication guarantee required by DevKit.
- Direct mode owns an exclusive file connection. Registering several features must reuse that connection rather than opening the file separately for each provider.
- A standalone outgoing-message outbox must retain messages while its downstream broker is unavailable and resume publication through the existing worker behavior.
- Legacy JobScheduling and Quartz persistence are excluded. Jobs persistence follows the existing `Application.Jobs` contracts.
- Permalinks identify feature resources through the existing registry contract. They must not expose local database paths.

## 10. Validation and implementation slices

### Coverage completion

#### Repositories, COV-001: implemented for development use

- [x] Upgrade the existing repository integration to the pinned v6 package.
- [x] Verify the implemented entity CRUD, read-only queries, specifications, projections, ordering, paging, counts, and file reopen behavior with 53 repository regression cases.
- [x] Complete all six `UpdateSetAsync` and `DeleteSetAsync` overloads.
- [x] Complete awaited repository transactions, nested ownership, cancellation, version checks, and rollback.
- [x] Support ordering, distinct selection, mapped `ThenInclude`, reference collections, hierarchy, and bounded materialization.
- [x] Pass 13 new repository contract cases alongside the 53 existing regression cases.
- [x] Implement `IEntityBulkInserter<TEntity>` and verify its validation and decorator semantics. Existing `InsertSetAsync` does not complete this item.

#### ActiveEntity, COV-002: implemented for development use

- [x] Implement the provider contract, including CRUD, set operations, queries, projections, IDs, counts, existence, and paging.
- [x] Implement shared named ownership, scoped sessions, awaited transaction scopes, rollback, and cleanup.
- [x] Verify typed identities, mappings, concurrency, callbacks, and the existing behavior pipeline without ActiveEntity core or generic repository changes.
- [x] Preserve the 57 engine and ActiveEntity integration cases and 10 previously verified Domain ActiveEntity unit cases. Add a provider-scoped callback/outbox/history capture case; the current combined LiteDB run passes 404 cases.
- [x] Provide public, XML-documented APIs and centralize usage documentation in [LiteDB Infrastructure](../features-infrastructure-litedb.md).

Production availability remains blocked by the selected upstream development package. Other coverage items and the broader migration review remain unfinished. Support limits belong in the central guide.

#### Domain-event outbox, COV-003: implemented for development use

- [x] Implement `IOutboxDomainEventWorker` and keyed/default database registration.
- [x] Capture aggregate and ActiveEntity events atomically after successful callbacks, acknowledging pending events after commit.
- [x] Keep bulk event capture behind an explicit decorator.
- [x] Implement bounded delivery, exact event selection, persisted retry state, payload verification, expiring claims, renewal, fenced completion, archive, and purge.
- [x] Pass ten outbox cases covering rollback, failed delivery results and exceptions, competing claims, renewal, stale owners, lease recovery, file reopen, archive/purge, bulk capture, request context, and hosted immediate delivery.

#### Change history, COV-005: implemented for development use

- [x] Implement `IChangeHistoryService<TEntity, LiteDbContext>` with capture from repository and ActiveEntity native mutations.
- [x] Support portable change sets, snapshot capture, nested owned and identified collection paths, sensitive-value policies, and explicit bulk/set capture limits.
- [x] Implement filtered and grouped queries, payload suppression, fail-closed host policy evaluation, and entity restore authorization.
- [x] Restore through allowed setters, domain methods, handlers, or configured graph plans under expected-version policies, atomically recording restore provenance.
- [x] Pass 13 history cases covering queries, protected values, rollback and pending buffers, required limits, bulk summary, rejected restore, point-in-time restore, custom plans, concurrency, and authorization.

#### Durable feature providers, COV-014 through COV-017 and COV-019: implemented for development use

- [x] Complete the [durable providers implementation plan](../plan/pln-feature-litedb-durable-providers-1.md) before extending the source.
- [x] Implement native keyed records, bounded command materialization, cancellation rollback, detached values, and shared named ownership. Preserve typed payloads and property bags across repeated commands.
- [x] Implement COV-017 with all twelve Jobs store facets, occurrence and accepted-event deduplication, atomic batches, dependencies, histories, current-token leases, scheduler queries, and file reopen. Verify dispatch and execution through the existing scheduler.
- [x] Implement COV-016 with all seven orchestration facets, version checks, active concurrency keys, leases, signals, timers, history, query and administration contracts. Verify context reconstruction after reopen and a terminal transition through the existing runtime.
- [x] Implement COV-014 with durable fan-out, independent handler progress, waiting subscriptions, retry and expiry, renewed claims, stale-worker fences, persisted controls, retained DTO queries, archive, purge, and reopen. Preserve the existing messaging handler pipeline.
- [x] Implement independently selected outgoing application-message capture and `IOutboxMessageWorker`. Verify entity-transaction rollback, idempotent capture, exact message selection, and forwarding to a separately selected receiving broker.
- [x] Implement COV-015 with the existing queue handler pipeline, one current claim, delayed eligibility, retry and dead letters, expiry, renewal, stale-worker fences, queue and type controls, retained operations, and reopen. Report unsupported optional circuit management through the existing capability contract.
- [x] Implement COV-019 email storage and operational outbox contracts, byte-exact attachments, current-token updates, pending selection, retained queries, retry, archive, retention, and reopen. Verify delivery and persisted outcomes through the native notification worker, existing hosted service, operation behaviors, and selected transport.
- [x] Pass 61 durable-provider cases and all 147 existing LiteDB cases. Build with XML documentation enabled and no warnings or errors. Keep application contracts and engines unchanged, add no EF dependency, and preserve the default project README.
- [x] Keep all LiteDB setup, behavior, and support limits in feature sections of [LiteDB Infrastructure](../features-infrastructure-litedb.md).

#### File monitoring, COV-010: implemented for development use

- [x] Implement `IFileEventStore` independently of the file-content provider, with history, latest-event lookup, inclusive time ranges, location queries, and present-file detection.
- [x] Persist processing results, enforce event references and immutable identities, and commit presence and arrival ordering atomically with each event.
- [x] Preserve exact location/path identities and prevent late older events from replacing newer presence state.
- [x] Pass seven real-file cases covering selection, ordering, idempotence, detached payloads, processing results, cancellation, query bounds, and reopen.

#### Auditing, COV-020: implemented for development use

- [x] Implement `IAuditingStorageProvider` through the existing auditing builder, capture runtime, query service, integrity policy, and export pipeline. Preserve application contracts and engines.
- [x] Store complete canonical original graphs, including additive JSON and integrity envelopes, with separate durable publication, lifecycle, and extraction state.
- [x] Use a dedicated named audit owner by default and verify independent audit persistence while a business transaction rolls back.
- [x] Provide read-only readiness, hosted initialization, trusted predicate evaluation, scoped searches, resource history, explicit related-execution links, aggregates, and facets.
- [x] Implement bounded extraction with fixed committed windows, whole-graph byte budgets, reopen checkpoints, lifecycle and removal validation, and compare-and-set epoch rotation.
- [x] Implement bounded archive/removal with durable replay cutoffs and retain original hashes and signatures through archival.
- [x] Pass 22 real-file cases covering the existing capture runtime, canonical and additive evidence, corruption rejection, HMAC, receipts, scope enforcement, diagnostic bounds, retention, cursors, reopen, and NDJSON export.
- [x] Build with XML documentation and zero warnings or errors. Pass all 237 LiteDB integration cases, including the 208 prior cases and 29 new file-monitoring/auditing cases. Preserve the default project README and keep usage documentation in the central guide.

#### Storage and profiling, COV-007, COV-009, COV-011, COV-013, and COV-021: implemented for development use

- [x] Include native document persistence, COV-007, as the prerequisite for the existing Common Caching composition. Preserve exact identities, native paging/count/key projection, ETags, conditional writes, metadata, expiration, and bounded retention. Use the existing client's ordered fallback for batch and bulk facets.
- [x] Implement COV-013 through `DocumentStoreCacheProvider` and `DocumentStoreCache`. Correct relative expiration and sliding-only renewal in the shared facade, cap renewal at absolute expiration, and use conditional metadata refresh to prevent concurrent replacement or deletion from being undone. Preserve the original cache constructor.
- [x] Implement COV-009 through the existing factory with explicit directories, normalized logical paths, bounded chunk streams, metadata, paging, copy/move/rename/delete, and cross-provider helpers. Fence stale publication, retain active readers' generations, and provide bounded abandoned-content cleanup.
- [x] Implement COV-011 with durable unique locations, stable permalink identities, prefix moves/deletes, event-order fences, expiration, conditional administration, and the existing queued file behavior and fallback dispatcher.
- [x] Implement COV-021 with complete Runtime and Operation graphs, private identifiers and correlations, immutable operation collections, durable writer attempts/leases/settlement, signed query boundaries, grouping, timelines, analysis, correlation, clear fencing, and resumable maintenance. Keep waiting outside native commands.
- [x] Pass 109 new cases: 21 document/cache cases including 13 shared concurrency cases, nine file cases, five permalink cases, and 74 profiling cases including the shared backend contract suite and three native reopen cases.
- [x] Pass all 346 LiteDB integration cases, including the 237 prior cases. Build `Infrastructure.LiteDB` with XML documentation and `Application.Storage` with its normal project settings, with zero warnings or errors. Preserve ActiveEntity core, the default project README, and the upstream prerelease guard. Keep all feature setup and limits in the central infrastructure guide.

#### Application Caching, entity permissions, and Broadcasting registry: implemented for development use

- [x] Implement COV-012 through the existing Application Caching engine and complete provider contract. Persist independent metadata, chunked generations, fences, quota counters, write receipts, and tracked invalidation state. Preserve declared native capabilities.
- [x] Pass the 19 shared cache contract cases and ten native cases covering reopen, publication integrity, protected readers, staged reads, authority recovery, namespace isolation, signed paging, refresh backoff, and named registration.
- [x] Implement COV-022 with native user and role grants, revocation, registered-entity hierarchy traversal, and the existing Identity evaluators and policies. Preserve scoped entity transactions and prevent uncommitted grants from entering the default evaluator cache.
- [x] Pass seven permission cases covering exact and wildcard grants, role/user queries, hierarchy cycles, inherited-cache eviction, transaction rollback, cancellation, and reopen.
- [x] Implement COV-024 with persistent local registry state and the existing Broadcasting runtime. Pass five cases covering scope replacement, delivery observations, failure thresholds, lease expiration/renewal, removal, and reopen.
- [x] Pass all 387 LiteDB integration cases, including the 346 prior cases and 41 new cases. Build `Infrastructure.LiteDB` with XML documentation enabled and zero warnings or errors, using existing dependency outputs. Keep all setup, behavior, and limits in the central infrastructure guide.

#### Review corrections for Application Caching and entity permissions: verified on 2026-11-23

- [x] Complete permission-cache eviction independently of caller cancellation after a successful native mutation. Verify all six user/role grant and revocation paths, including cached authorization after cancellation.
- [x] Apply each metadata call's remaining candidate allowance once. Verify ordinary query paging and preview, plus direct and tracked invalidation/clear continuations with small budgets and later publications at the same timestamp.
- [x] Prioritize Pending and Running operation records before retained terminal or paused statuses. Verify bounded listings and the existing operation runner for Completed, Cancelled, Failed, and Paused records.
- [x] Separate serialized metadata page bytes from conservative storage charges. Verify bounded pages and invalidations before and after reopen, with query-only quota counters preserved.
- [x] Add 17 regression cases in [cache regression tests](../../tests/Infrastructure.IntegrationTests/LiteDB/Caching/LiteDbCacheRegressionTests.cs) and [permission cancellation tests](../../tests/Infrastructure.IntegrationTests/LiteDB/Identity/LiteDbEntityPermissionCancellationTests.cs). All 17 fail before the corrections and pass afterward.
- [x] Pass all 404 LiteDB integration cases, including the 387 prior cases. Build `Infrastructure.LiteDB` with XML documentation enabled and zero warnings or errors, using existing dependency outputs. Keep the corrections provider-scoped and document their behavior only in the central infrastructure guide.

#### Excluded by user direction

- COV-004 sequence numbers: will not be implemented in this extension.
- COV-006 event sourcing and its legacy message outbox: will not be implemented in this extension. Domain-event outbox COV-003 remains included and implemented.
- COV-018 legacy JobScheduling: will not be implemented in this extension. COV-017 Jobs remains required.
- COV-023 log entries: will not be implemented in this extension. The host keeps its SQL Server logging sink.

### Provider implementation status as of 2026-11-23

- **Implemented and verified for development use:** 19 provider coverage items, COV-001 through COV-003, COV-005, COV-007, COV-009 through COV-017, COV-019 through COV-022, and COV-024.
- **Deferred existing-contract implementation:** COV-008 Blob Storage. Its DevKit contract exists, but no LiteDB provider has been implemented.
- **Deferred enhancements:** Further COV-007 document-storage work, including optional native batch-get and bulk-write facets. The existing document provider remains implemented and verified through its native operations and client fallback paths.
- **Waiting for the owning feature:** COV-025 Key/Value Storage is still a draft/specification. Its DevKit contracts and runtime must be implemented before LiteDB provider work becomes ready.
- **Excluded:** COV-004 sequences, COV-006 event sourcing, COV-018 legacy JobScheduling, and COV-023 log entries.

No additional provider implementation is ready under the current scope decisions. The active remaining work is the migration review and outstanding operational acceptance checks. The overall specification remains in progress; deferred and waiting items retain their status. The latest recorded verification remains 404 passing LiteDB integration cases and a clean XML-enabled native project build.

### Progress as of 2026-11-23

The v6 dependency upgrade, COV-001 through COV-003, COV-005, COV-007, COV-009 through COV-017, COV-019 through COV-022, and COV-024 are verified for development use. The broader migration review and provider rollout remain incomplete.

Completed work and verification:

- Pinned LiteDB centrally to `6.0.0-prerelease.355`. The package source revision and development-use restriction are recorded in section 2.
- Ran the initial 12 repository regression cases against both `5.0.21` and the selected v6 package. All 12 passed on each version.
- Expanded [LiteDbGenericRepositoryTests](../../tests/Infrastructure.IntegrationTests/LiteDB/LiteDbGenericRepositoryTests.cs) to 53 passing cases on v6. These cover implemented CRUD operations, GUID and string identities, invalid and missing inputs, specifications, projections, ordering, paging, counts, and file reopen with and without encryption.
- Fixed [identifier conversion](../../src/Infrastructure.LiteDB/Repositories/LiteDbEntityIdentity.cs) before LiteDB predicate translation. Malformed GUID strings now retain their `FormatException` instead of being wrapped as a query translation failure.
- Checked disposable copies of representative plain and encrypted files created by `5.0.21` in a separate process. The v6 integration read GUID and string identities, UTC instants, decimal values, binary content, named indexes, and filtered queries correctly. Updates persisted after reopen. These samples required no separate conversion tool. This establishes the bounded compatibility result for MIG-006, without promising compatibility for every v5 file or determining whether the engine changes its format internally on open.
- Built `Infrastructure.LiteDB` with XML documentation enabled and zero warnings or errors. The focused integration run passes 404 LiteDB cases, including the 346 prior cases plus 40 Application Caching cases, 13 entity-permission cases, and five Broadcasting registry cases. This does not establish complete branch or assembly coverage.
- Implemented COV-002 in [LiteDbActiveEntityProvider](../../src/Infrastructure.LiteDB/ActiveEntity/LiteDbActiveEntityProvider.cs), with `AddLiteDbDatabase` and `UseLiteDbProvider` registration. Persistence uses native LiteDB APIs through the existing context. ActiveEntity core and its contracts remain unchanged; the subsequent repository completion reuses shared session and native query mechanics.
- Added shared named database ownership, per-scope sessions, and a bounded native executor. Transaction begin, commands, commit, rollback, and cursor disposal stay on one native thread across application awaits. Tests cover callback reentry, independent scopes, failure and cancellation rollback, native completion failure, and shutdown.
- Added CRUD, set operations, all read, projection, ID, count, existence, and paged overloads, typed identity mapping, embedded and reference mappings, audit state, and optimistic concurrency. Existing behaviors and lifecycle callbacks work through the unchanged ActiveEntity APIs. Support APIs are public with XML documentation. The package has no EF dependencies.
- Added `Domain.Outbox`, `Application.Entities`, `Application.Storage`, and `Application.Auditing` references for the existing provider-neutral contracts. No EF runtime dependency was introduced.
- COV-002 verification includes the existing repository regression suite and Domain ActiveEntity tests. The completed [implementation plan](../plan/pln-feature-litedb-activeentity-1.md) records the focused counts, query limits, development restriction, and unchanged string-ID generator limitation.

Run the current LiteDB suite with:

```shell
dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter FullyQualifiedName~LiteDB
```

Validation used focused builds and tests with existing unchanged dependency outputs. A separate check forcing XML documentation generation on `Application.Storage` found ten existing XML-comment errors in unchanged files; its normal project build passes. Full dependency rebuilds encountered native compiler crashes in unrelated projects, so a clean solution rebuild is still unverified. Execution reports remain outside the documentation tree.

Review corrections verified on 2026-11-20:

- [x] Preserve pending notification emails during processed-only startup purge and purge processed records through native status selection.
- [x] Acquire notification leases at delivery time, renew current claims, cancel lost ownership, and retain stale-completion fences. Preserve the configured notification service and operation behaviors.
- [x] Make messaging and queueing type controls accept their subscription identifiers and reject ambiguous or unknown names.
- [x] Pass ten regression cases, including the four previously failing review probes, within the 208-case LiteDB suite. Keep application implementations and the default project README unchanged.

Further review corrections verified on 2026-11-20:

- [x] Execute outgoing publisher behaviors before transactional capture, preserve enriched values, and roll back entity writes when a publisher guard rejects a message.
- [x] Dead-letter permanent payload decoding errors in messaging and queueing. Persist and enforce retry limits for other processing failures, preserve the budget after reopen, and reset it on explicit retry.
- [x] Pass nine additional regression cases, including all three second-review probes, within the 208-case LiteDB suite. Preserve application contracts, ActiveEntity core, the default README, and the current dependency boundaries.

The upstream development-risk acknowledgement exists only in disposable-data tests. Application code retains the guard. Production support remains blocked by the selected upstream package's development-only restriction.

### Remaining work

- Finish the migration review under MIG-001 through MIG-008, including supported platforms, transaction ownership, recovery, and the source inventory and consolidation check. The dependency upgrade and sample file checks alone do not complete this phase.
- Keep COV-008 Blob Storage deferred until work is explicitly resumed. It is the only missing provider for an existing contract in the current inventory. COV-007 document storage is implemented and verified; additional native batch/bulk facets and other document enhancements remain deferred.
- Keep COV-025 Key/Value Storage waiting for the owning DevKit feature to implement its contracts and runtime. It remains a draft/specification and is not a ready LiteDB implementation task.
- No additional provider implementation is ready within the agreed scope. COV-004, COV-006, COV-018, and COV-023 remain excluded.
- COV-001 through COV-003, COV-005, COV-007, COV-009 through COV-017, COV-019 through COV-022, and COV-024 are implemented for development use. Production availability remains blocked by the selected upstream package. Bounded materialization and backend-specific capabilities are documented only in the [LiteDB Infrastructure guide](../features-infrastructure-litedb.md). The existing string-ID generator remains unchanged and requires a handwritten `EntityId<string>` shape until its duplicate-member issue is addressed separately.
- Complete concurrency, durability, recovery, and operational acceptance checks. Performance measurement remains follow-up work after functional delivery.

### Planned slices

Implement in slices, with completion tracked separately for migration and each coverage item:

1. Select and pin v6, freely refactor the existing integration, consolidate LiteDB implementation into `Infrastructure.LiteDB`, check representative v5 file compatibility, document any upstream conversion requirement, and record upstream-dependent behavior. This phase must pass before provider expansion.
2. Shared database registration, initialization, mapping, namespace isolation, and transaction ownership on the selected v6 engine.
3. Repository completion, ActiveEntity, documents, blobs, files, permalinks, and file monitoring. Sequence numbers are excluded.
4. Messaging, queueing, domain event outbox, Jobs, notifications, and orchestrations.
5. Caching, auditing, profiling, permissions, change history, and broadcasting registry. Event sourcing and log-entry persistence are excluded.
6. Operational integration, coverage reconciliation, feature documentation, and full contract verification. Key/Value Storage joins this inventory when its owning contract becomes available.

After the common v6 migration baseline passes, independently complete providers may ship without waiting for the entire inventory. Each release includes its provider contract and operational integration verification and a support checklist that distinguishes verified, unfinished, and upstream-blocked items. A release must not expose placeholder providers or claim full LiteDB coverage prematurely.

The extension is complete only when MIG-001 through MIG-008 pass, all LiteDB implementation code lives in `Infrastructure.LiteDB`, COV-001 through COV-024 except the excluded COV-004, COV-006, COV-018, and COV-023 have implemented and verified paths, the source inventory has been reconciled, and all acceptance criteria pass. Shipping an earlier slice does not satisfy the full coverage goal. Track COV-025 separately as waiting for the draft owning feature's contracts and runtime, then require LiteDB coverage there as well. The present implementation phase does not include starting that owning feature or resuming deferred blob/document work.

Performance measurement and optimization follow functional delivery and do not delay otherwise complete providers. Correctness, bounded resource use, required transaction behavior, and operational integration remain release gates.

The v5-to-v6 migration requires dependency and code verification plus a representative file-compatibility check and any necessary upstream conversion guidance. Old LiteDB API compatibility, deployed-consumer rollback tooling, a general-purpose DevKit model compatibility subsystem, and historical model-schema fixtures are not required. Fresh initialization, stable-model reopen on v6, concurrent behavior, restart recovery, and existing contract correctness remain required.

## 11. Related specifications and references

- [Clean Architecture](../adr/0001-clean-onion-architecture.md) and [modular monoliths](../adr/0003-modular-monolith-architecture.md).
- [Domain repositories](../features-domain-repositories.md), [ActiveEntity](../features-domain-activeentity.md), and [event sourcing](../features-event-sourcing.md).
- [Document Storage](../features-storage-documents.md), [BlobStorage](../features-storage-blobs.md), [FileStorage](../features-storage-files.md), and [StoragePermalinks](../features-storage-permalinks.md).
- [Messaging](../features-messaging.md), [Queueing](../features-queueing.md), [Jobs](../features-jobs.md), [Orchestrations](../features-orchestrations.md), and [Notifications](../features-notifications.md).
- [Application Caching specification](spec-application-caching.md), [Auditing specification](spec-application-auditing.md), and [Key/Value Storage specification](spec-application-storage-keyvalue.md).
- [LiteDB v6 milestone](https://github.com/litedb-org/LiteDB/milestone/6), [v6 release-readiness checklist](https://github.com/litedb-org/LiteDB/issues/2623), and [published releases](https://github.com/litedb-org/LiteDB/releases). Recheck these when selecting the implementation baseline.
- [v6 transaction ownership work](https://github.com/litedb-org/LiteDB/issues/2822) and [v6 DateTime semantics work](https://github.com/litedb-org/LiteDB/issues/2982). Validate the pinned package rather than assuming these proposals have shipped.
- [LiteDB data structure](https://www.litedb.org/docs/data-structure/), [object mapping](https://www.litedb.org/docs/object-mapping/), [connection modes](https://www.litedb.org/docs/connection-string/), and [FileStorage](https://www.litedb.org/docs/filestorage/).

Implementation of COV-017, COV-016, COV-014, COV-015, and COV-019 follows the [durable providers plan](../plan/pln-feature-litedb-durable-providers-1.md). The plan is complete for development use, with 61 durable-provider integration checks and the existing 147 LiteDB cases passing. COV-018 remains excluded. The focused project build used existing dependency outputs and does not establish a fresh solution rebuild.
