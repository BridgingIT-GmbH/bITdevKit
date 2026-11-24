# LiteDB Infrastructure

> Persist DevKit entities and durable feature state in an owned local LiteDB database.

[TOC]

## Overview

`Infrastructure.LiteDB` contains the LiteDB-specific implementations for DevKit persistence. This is the central guide for their configuration, behavior, examples, and support limits. It covers repositories, bulk insertion, ActiveEntity, the domain-event outbox, change history, Jobs, orchestrations, messaging, queueing, email notifications, document storage, Common Caching, virtual files, permalinks, file monitoring, auditing, profiling, Application Caching, entity permissions, and the broadcasting registry. Blob storage remains deferred. Sequence numbers, event sourcing, legacy JobScheduling, and log-entry persistence are excluded from this extension.

The project name is `Infrastructure.LiteDB`. Its assembly and root namespace are `BridgingIT.DevKit.Infrastructure.LiteDb`.

## Challenges

Local persistence must preserve each feature's storage contract without requiring an external database service or changing application entities. Entity mutations, their outgoing domain events, and their history must commit together. Workers need bounded claims and lease checks while delivery runs outside the database transaction.

## Solution

Register an owned database with `AddLiteDbDatabase`, then select the providers needed by the application. Repositories, ActiveEntity, outbox capture, and history capture share its scoped session and native transaction boundary.

## Key Features

- generic repositories with atomic set operations, projections, includes, paging, and explicit bulk inserters
- ActiveEntity CRUD, atomic set operations, queries, and paging
- ActiveEntity typed identities, audit state, concurrency checks, callbacks, and behaviors
- named database ownership and transaction scopes across awaited repository and ActiveEntity operations
- atomic domain-event capture with durable retry, leases, renewal, archive, and purge
- history capture, grouped queries, protected values, authorization, and transactional restore
- Jobs runtime state, occurrences, executions, dependencies, batches, leases, histories, and accepted events
- orchestration snapshots, version checks, signals, timers, leases, history, and administration
- durable messaging with independent handler progress and an optional outgoing-message outbox
- durable queues with delayed eligibility, retry, dead letters, and operational controls
- email notification storage, attachments, delivery claims, and outbox operations
- exact document keys, conditional writes, expiration, native counts, and key-only paging
- Common Caching through the existing document-store cache adapters
- Application Caching with publication fences, chunked payloads, protected readers, metadata queries, quotas, and tracked operations
- entity permission grants, native hierarchy traversal, and existing Identity evaluation
- persistent local Broadcasting registrations, delivery observations, and leases
- chunked virtual files, explicit directories, atomic publication, and stable permalinks
- file-event history, processor outcomes, and current file presence by location
- original audit graphs, trusted scoped reads, resumable extraction, and retention
- complete Runtime and Operation profiling graphs with durable leases, query boundaries, and clear fences
- native collection and identity-index initialization without relational schema migrations

## Architecture

`LiteDbContext` wraps a native `LiteDatabase`. Named registration owns its mapper, context, and dedicated native worker. Feature providers share transaction state through a scoped `LiteDbDatabaseSession`. Native commands remain on one worker thread while application delegates can await outside it.

Durable feature providers store one keyed logical record per BSON document. Commands use independent scoped sessions and commit all record changes atomically. Handlers, notification transports, and orchestration activities run outside those commands. The outgoing application-message publisher joins the caller's scoped session so entity writes and captured publications can commit together. Repository and ActiveEntity transactions reserve their database owner until completion. Configure a separate named database for independent storage or profiling calls awaited inside such a transaction, so those calls can acquire their own owner.

## Use Cases

Use the selected feature providers for development against disposable local files. Selecting LiteDB changes persistence while application use cases continue to use their existing DevKit contracts.

## Basic Usage

The pinned `6.0.0-prerelease.355` package is a development build that upstream excludes from production use. Before opening a disposable development database, the application must explicitly acknowledge the risk:

```csharp
global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
```

The context and provider registration preserve that guard. Production adoption requires a suitable upstream release.

Register the database once before adding feature providers:

```csharp
using BridgingIT.DevKit.Infrastructure.LiteDb;

services.AddLiteDbDatabase(options =>
{
	options.ConnectionString = Path.Combine(Path.GetTempPath(), "fictional-ledger.db");
});
```

Dispose service providers and scopes asynchronously. All providers selected for the same database share one owner; separate database files have separate transactions.

## Repositories

`LiteDbGenericRepository<TEntity>` implements `IGenericRepository<TEntity>`. `LiteDbReadOnlyGenericRepository<TEntity>` implements the read-only contract. Both are in `BridgingIT.DevKit.Infrastructure.LiteDb.Repositories`.

### Setup

Select the repository and an optional native bulk inserter after registering the database:

```csharp
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

services.AddLiteDbRepository<LedgerDocument>(configure: options =>
{
	options.CollectionName = "ledger_documents";
	options.MaxMaterializedRows = 10000;
});
services.AddLiteDbEntityBulkInserter<LedgerDocument>(configure: options =>
	options.CollectionName = "ledger_documents");

public class LedgerDocument : AggregateRoot<Guid>, IConcurrency
{
	public string Name { get; set; }

	public int Amount { get; set; }

	public Guid ConcurrencyVersion { get; set; }
}
```

`AddLiteDbRepository` returns the existing repository behavior builder. Bulk insertion returns the existing bulk-inserter builder. Behaviors remain explicit choices. The read-only contract resolves the selected repository. Pass a database name as the first argument to select another registered database.

### Reads and writes

```csharp
await using var scope = serviceProvider.CreateAsyncScope();
var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<LedgerDocument>>();
var document = await repository.InsertAsync(new LedgerDocument
{
	Name = "Quarterly summary",
	Amount = 25
});

var selected = await repository.FindAllAsync(
	new Specification<LedgerDocument>(item => item.Amount >= 10),
	new FindOptions<LedgerDocument>
	{
		Order = new OrderOption<LedgerDocument>(item => item.Amount),
		Take = 20
	});
var names = await repository.ProjectAllAsync(item => item.Name);
await repository.UpdateSetAsync(
	new Specification<LedgerDocument>(item => item.Amount < 10),
	set => set.Set(item => item.Amount, 10));
```

Multiple specifications combine with AND. Ordering and distinct selection precede paging. `FindOneAsync` by specification applies ordering and skip before selecting its first match. Includes, `ThenInclude`, and string paths resolve through the database mapper, including `DbRef` references and collection navigations. Hierarchy loading supports depths from one to 32. Reads are detached; `NoTracking` requires no extra action. Unsupported native filter expressions fail explicitly.

`InsertAsync` and `UpdateAsync` retain repository upsert semantics: a missing identity inserts, and an existing identity updates. `UpsertAsync` also returns `RepositoryActionResult`. `DeleteAsync` accepts an entity or identity and returns `Deleted` or `None`; an entity delete compares its concurrency version. Missing records and null identities return `None`.

All six set-update and set-delete overloads are implemented. Selection occurs before mutation, preserving specifications, ordering, distinct selection, skip, and take. A failure rolls back the whole selection. `InsertSetAsync` also commits atomically and skips null entries.

GUID, string, integer, long, and typed `EntityId<T>` identities use scalar BSON keys. Default numeric identities allocate natively. `IConcurrency` updates compare and advance versions atomically; stale entity updates and deletes fail. Identity-only deletion has no caller version to compare. `EnableOptimisticConcurrency` controls comparison.

### Transactions, bulk insertion, and limits

Use the existing transaction contract in the same DI scope as the repositories:

```csharp
var transaction = scope.ServiceProvider.GetRequiredService<IRepositoryTransaction<LedgerDocument>>();
await transaction.ExecuteScopedAsync(async () =>
{
	await repository.InsertAsync(new LedgerDocument { Name = "First", Amount = 10 });
	await Task.Yield();
	await repository.InsertAsync(new LedgerDocument { Name = "Second", Amount = 20 });
});
```

Repositories and ActiveEntity providers using the same database and DI scope share transaction ownership. Nested transaction adapters join the owner; only the owner commits. Failed or abandoned scopes roll back, and caller concurrency versions are restored. External side effects are outside this boundary. Delegates are not retried.

`IEntityBulkInserter<TEntity>` performs a native atomic insertion in batches of 128. Null entries, conflicting identities, cancellation, or a configured row-limit failure prevent a successful partial batch. Empty input returns zero. Supplied concurrency versions remain unchanged. Validation, auditing, version generation, and domain-event behavior require explicitly selected decorators.

`MaxMaterializedRows` defaults to `10000`. Large list reads, set selections, bulk inputs, and local compound-order/distinct fallbacks fail when the bound is exceeded. Smaller native pages and scalar counts can query larger collections. Cancellation is checked before admission, between rows or batches, and before completion; executing synchronous engine calls finish before cleanup.

Direct construction with `LiteDbContext` remains available for callers owning the context and mapper. Such callers must serialize access and manage its lifetime. Named registration supplies the shared worker, awaited transaction adapters, and atomic outbox/history capture. Do not independently open a context against an already owned file. `Autosave` does not defer native persistence; configure BSON mapping through `ConfigureMapper` or the supplied context rather than `IEntityMapper`.

## Active Entities

`LiteDbActiveEntityProvider<TEntity, TId>` implements the full `IActiveEntityEntityProvider<TEntity, TId>` contract directly through LiteDB APIs and the shared context. It does not wrap the generic repository classes.

### Registration

The namespace is `BridgingIT.DevKit.Infrastructure.LiteDb`. After registering the database, select the provider for an entity:

```csharp
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Infrastructure.LiteDb;

services.AddActiveEntity(configuration => configuration
	.For<LedgerEntry, Guid>()
	.UseLiteDbProvider(options =>
	{
		options.CollectionName = "ledger_entries";
		options.MaxMaterializedRows = 10000;
	})
	.AddLoggingBehavior());

public class LedgerEntry : ActiveEntity<LedgerEntry, Guid>
{
	public string Name { get; set; }

	public int Amount { get; set; }
}
```

Initialize the existing ActiveEntity host integration after building the application, such as `app.UseActiveEntity()` in an ASP.NET Core host. A non-web host can use `ActiveEntityConfigurator.SetGlobalServiceProvider(serviceProvider)`. Dispose service providers and scopes asynchronously so session rollback and worker shutdown finish before files reopen.

Enabled entities share the `default` database owner and use separate collections. Omitted collection names derive from a stable hash of the entity's assembly name and full type name. `AddLiteDbDatabase("archive", ...)` creates an additional owner. Select it with `options.DatabaseName = "archive"`. Duplicate file ownership and Shared mode are rejected. Use one application process per file.

### Stored state and queries

Collections and their unique identity indexes initialize through native LiteDB operations without EF models or migrations. Supported identities are GUIDs, strings, integers, longs, and `EntityId<T>` types exposing `Value` and a public static `Create(primitive)` factory. Numeric allocation is native and atomic. Custom generation uses the existing `IEntityIdGenerator<TEntity>` contract.

The mapper preserves embedded values, audit state, and concurrency versions. It excludes pending `DomainEvents`. Configure additional mappings through `LiteDbDatabaseOptions.ConfigureMapper` before first use. Explicit `DbRef` mappings support direct expression or string-path includes. Reads return detached values, so `NoTracking` requires no extra work.

Specifications combine with AND. Ordering and distinct selection precede paging, and paged totals exclude paging. Paged calls default to ten rows. Compound ordering, distinct selection, and computed updates can use a bounded in-memory selection. `MaxMaterializedRows` defaults to `10000` and must be positive. Exceeding the limit returns `InvalidOperationError`, without truncating successful results. Simple paged reads and native scalar counts can operate over larger matching collections.

Mapper-aware `ThenInclude`, reference collections, and hierarchy loading are supported. Hierarchy depth must be between one and 32. Unsupported native filter expressions return failures. Embedded graphs load with their documents. Relational graph merge and tracking are outside this provider.

The existing string-ID source generator currently emits duplicate members. Handwritten `EntityId<string>` types with the documented factory shape work with the provider. This implementation does not change the generator.

### Transactions and concurrency

Use the existing `WithTransactionAsync` APIs and pass their context to each entity operation. The session keeps native begin, writes, and completion on one dedicated thread while application callbacks can await. Providers for the same named database in one DI scope share transaction state. Fresh scopes wait for ownership and do not join an existing transaction.

Standalone writes commit after supplied callbacks succeed. Callback or native failures roll back writes and restore caller concurrency versions. An explicit transaction that fails cannot commit until it has been rolled back. Pending transactions roll back when their scope is disposed. The provider does not retry user delegates or roll back external side effects.

Entities implementing `IConcurrency` compare stored and supplied versions atomically for update and entity delete. Stale writes return `ConcurrencyError`. Successful inserts and updates generate new versions, including set updates. `EnableOptimisticConcurrency` and `VersionGenerator` control this behavior. Set operations capture their target selection before mutation and commit it atomically, with cancellation checks between bounded batches.

The provider invokes supplied lifecycle callbacks. Existing ActiveEntity code invokes registered behaviors, including validation, audit state, logging, and domain-event publishing. The provider does not publish domain events separately. Selecting the outbox below captures pending events after callbacks succeed; configure the immediate publishing behavior consistently to avoid duplicate publication.

## Domain Event Outbox

### Setup and capture

Register the existing DevKit notifier and its handlers, then add the outbox to the entity database:

```csharp
services.AddLiteDbDomainEventOutbox(configure: options =>
{
	options.ProcessingCount = 50;
	options.RetryCount = 3;
	options.LeaseDuration = TimeSpan.FromSeconds(30);
	options.LeaseRenewalInterval = TimeSpan.FromSeconds(10);
	options.AutoArchiveAfter = TimeSpan.FromDays(7);
});
```

Capture supports aggregate `DomainEvents` and ActiveEntity events, including events registered by supplied after-write callbacks. Event identity, type, content, content hash, event properties, and tracing baggage persist in the entity transaction. Pending events are acknowledged after commit and remain pending after rollback. Select outbox capture in place of immediate domain-event publishing for those mutations.

Normal native bulk insertion does not capture domain events. Opt in explicitly when configuring its builder:

```csharp
services.AddLiteDbEntityBulkInserter<LedgerDocument>(configure: options =>
	options.CollectionName = "ledger_documents")
	.WithBehavior((inner, provider) => new LiteDbBulkOutboxBehavior<LedgerDocument>(
		inner,
		provider.GetRequiredKeyedService<LiteDbDatabaseSession>("default")));
```

### Delivery and maintenance

The implementation exposes `IOutboxDomainEventWorker`. The default database also registers its ordinary, unkeyed worker contract; named databases expose keyed workers. `ProcessAsync(eventId)` selects that exact event. Each invocation processes at most `min(BatchSize, ProcessingCount)` events, with `BatchSize` defaulting to 128.

Workers atomically claim eligible events and deliver through `INotifier` outside the database transaction. Leases renew during awaited delivery. Completion and renewal require the current, unexpired claim token, so stale workers cannot finalize a replacement claim. Exceptions and failed notifier results retain retry state; exhausted attempts leave a terminal failure record. Payload hashes and event identities are checked before delivery.

DevKit's existing `OutboxDomainEventService` supplies scheduled polling. A LiteDB hosted adapter forwards `Immediate` commit signals to the same durable worker. Polling recovers events missed by an in-memory signal. Committed pending events survive reopening; expired leases allow recovery. Delivery is at least once, so handlers should tolerate redelivery after a process exits between handling and finalization.

`PurgeAsync(processedOnly: true)` removes completed records; the default removes all eligible records. `ArchiveAsync` marks records older than `AutoArchiveAfter`. Both use bounded transactions and skip live claims. Disabling the outbox disables capture and processing. Integrated capture always follows the entity transaction's commit boundary, including when `AutoSave` is false.

## Change History

### Setup and capture policies

Register the entity provider before configuring history:

```csharp
services.AddLiteDbChangeHistory(options =>
{
	options.Track<LedgerDocument>()
		.CaptureChanges()
		.AllowRestoreUsingValidatedSetters(item => new { item.Name, item.Amount });
});
```

The adapter implements `IChangeHistoryService<TEntity, LiteDbContext>` from `Application.Entities`. It captures pending `EntityChangeBuilder` change sets and confirmed repository or ActiveEntity mutations inside their native transaction. Rollback retains pending change sets. `RepositorySnapshot` and `EntityChangeOnly` strategies are supported; selecting `EfChangeTracker` is rejected at registration.

Portable options control create capture, direct-mutation mode, set updates, native bulk insertion, value policies, restore allowlists, and concurrency. Native bulk history is disabled until `CaptureBulkInserts` is selected. Summary capture stores one operation record; detailed capture enforces its entity limit. Required set-capture limits abort the entity transaction; best-effort overflow records a diagnostic summary. Identity and concurrency fields cannot become restorable history values.

Sensitive property names default to hashes. Explicit exclusion, redaction, hashing, maximum stored length, and oversized-value policies apply to captured values. Redacted, hashed, truncated, create/delete, and summary rows are not restorable. Configured owned values and embedded graphs support nested scalar capture. Collections use stable item identities, preserving item paths across reordering. A configured domain `IChangeHistoryGraphRestorePlan<TEntity>` handles restore for owned, collection, and graph paths. These paths do not introduce EF relationship tracking or graph-merge semantics.

### Reads, authorization, and restore

```csharp
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

var history = scope.ServiceProvider.GetRequiredService<
	IChangeHistoryService<LedgerDocument, LiteDbContext>>();
var changes = await history.FindAllChangeSetsAsync(new ChangeHistoryFindAllQuery
{
	EntityId = document.Id.ToString(),
	Operation = "Update",
	PageSize = 20,
	IncludeValues = false
});
```

Queries filter by entity, property, change set, bulk operation, user, time, operation, source, strategy, and status. Grouped paging selects whole change sets. `IncludeValues = false` removes old and new payloads from returned DTOs. Native row paging supports larger histories; grouped queries and restore plans enforce `MaxMaterializedRows` and fail without truncating results.

When `ReadAuthorizationPolicy` or `RestoreAuthorizationPolicy` is configured, register `ILiteDbChangeHistoryAuthorizer` to evaluate the host policy. A missing authorizer fails closed. Read authorization runs before loading values. Restore also honors the entity's `IChangeHistoryRestoreAuthorizer<TEntity>` and configured expected-version policy.

Restore selects a change set belonging to the requested entity, validates all selected rows, then applies allowed setters, domain methods, handlers, or graph plans to a detached entity. Change-set restore applies captured old values. Point-in-time restore also reverses later captured changes to reconstruct the state before the selected change set. Successful persistence and its new restore history commit together, with the supplied reason and original change-set identity. Failed authorization, validation, domain logic, concurrency, capture, or commit leaves no partial restore.

Use a single unambiguous entity collection per tracked type in the selected database. History services and capture policies are scoped to that registration; the same type can use an independent provider in another named database. LiteDB usage and configuration for all history features belong on this page.

## Jobs

Select `LiteDbJobStoreProvider` through the existing Jobs builder:

```csharp
services.AddJobScheduler()
	.WithLiteDb(options => options.CollectionPrefix = "ledger_jobs");
```

The provider implements every `IJobStoreProvider` facet. It persists job and trigger runtime state, occurrences, executions, dependencies, batches, leases, execution history, batch history, accepted events, previous executions, and scheduler queries. Definitions and triggers remain code-owned in `Application.Jobs`.

Occurrence keys and accepted-event idempotency keys retain the existing case-insensitive semantics. Batch creation and child membership commit together. Conflicting child IDs or keys fail before inserting any child. Duplicate external batch identities fail without creating a second batch. Lease renewal and ownership verification compare the current owner, token, and expiry. An old owner cannot release its replacement's lease.

Collection names use the `jobs` prefix by default. Legacy `Application.JobScheduling` and Quartz persistence are excluded under COV-018.

## Orchestrations

Select all seven orchestration storage facets through the existing builder:

```csharp
services.AddOrchestrations()
	.WithLiteDb(options => options.CollectionPrefix = "ledger_workflows");
```

`LiteDbOrchestrationStorageProvider` persists instances, leases, history, signals, and timers. Query and administration contracts use those same records. The existing orchestration runtime and workflow definitions continue to execute activities and transitions.

Instance saves require the current version. Active instances cannot share a concurrency key within one orchestration definition. Snapshots preserve data, properties, context, and stable type identifiers for reconstruction after reopen. Signals support idempotency keys and state eligibility. Due timer selection excludes consumed timers.

Administrative operations support archive, purge, lease release, and timer requeue. Purge skips instances with live leases. Ownership changes fence stale renewals and releases. Collection names use the `orchestrations` prefix by default.

## Messaging

### Durable broker

```csharp
services.AddMessaging()
	.WithLiteDbBroker(options =>
	{
		options.BatchSize = 128;
		options.MaxDeliveryAttempts = 5;
		options.MessageExpiration = TimeSpan.FromHours(1);
	});
```

`LiteDbMessageBroker` implements `IMessageBroker`, `IMessageBrokerRuntime`, and `IMessageBrokerService` through the existing publisher and handler pipelines. It stores message content, integrity hashes, request properties, and a separate outcome for each handler. Retries skip handlers that already succeeded. Permanent payload deserialization failures dead-letter immediately. Transient payload failures and unexpected processing exceptions use a persisted `ProcessingFailureCount`, bounded by `MaxDeliveryAttempts`. Manual retry resets that count, and reopening a file preserves it. Handler attempt summaries continue to report handler attempts.

A publication without a handler remains pending with a persisted waiting flag. A later subscription activates waiting messages. The hosted `LiteDbMessageBrokerWorker` claims bounded batches through native predicates, renews leases during delivery, and checks the current token and expiry before recording progress or completion. Expired work remains available for operational inspection.

The service supports retained queries, search, content inspection, statistics, subscriptions, waiting-message inspection, whole-message and handler retry, lease release, type pause and resume, archive, and purge. Routine DTO queries omit message bodies. Purge skips live claims. Paused types are excluded before the native batch limit. Pause and resume accept the unambiguous type names returned by subscription queries, CLR full names, and assembly-qualified tokens. Unknown or ambiguous names fail explicitly.

Collection names use the `messaging` prefix by default. Set `Enabled = false` to disable background processing. `ProcessPendingAsync` uses the same claims when invoked explicitly.

### Outgoing application-message outbox

Select the receiving broker first, then add outgoing capture:

```csharp
services.AddMessaging()
	.WithLiteDbBroker()
	.WithLiteDbOutbox(options =>
	{
		options.CollectionPrefix = "ledger_publications";
		options.MessageExpiration = null;
	});
```

The receiving broker can be any existing `IMessageBroker` implementation. `WithLiteDbOutbox` preserves that selection and replaces the application's publisher with a scoped `LiteDbOutboxMessagePublisher`. Resolve `IMessageBroker` within a dependency injection scope. The preserved receiving runtime keeps its existing lifetime and subscriptions.

To capture atomically with repository or ActiveEntity writes, resolve the entity provider and publisher from the same scope and select the same named database. Publish inside the entity transaction delegate. The scoped publisher runs the existing `MessageBrokerBase` validation, correlation, audit, and publisher behavior pipeline before capturing the outgoing envelope. Behaviors come from the current dependency injection scope and `WithLiteDbOutbox` publisher options. A guard rejection rolls back the entity transaction. Enriched message values are part of the captured payload. Rollback removes both entity changes and outgoing publications. Standalone publication commits its own native transaction.

`LiteDbOutboxMessageWorker` implements `Application.Messaging.IOutboxMessageWorker`. It reuses the native broker's claims, retry state, and ownership checks to forward typed envelopes through the preserved receiving broker. The existing `OutboxMessageService` polls the worker. Exact message-ID processing leaves unrelated work pending. Downstream publication still runs the receiving broker's publisher behaviors, so behaviors must tolerate both admission and delivery. Selected producer auditing uses its deferred-transport boundary when forwarding. Its default collection prefix is `message_outbox`.

External delivery is at least once. A process can stop after a handler or receiving broker accepts work but before the completion commit. Consumers must tolerate redelivery.

## Queueing

```csharp
services.AddQueueing()
	.WithLiteDbBroker(options =>
	{
		options.BatchSize = 128;
		options.MaxDeliveryAttempts = 5;
	});
```

`LiteDbQueueBroker` implements `IQueueBroker`, `IQueueBrokerRuntime`, `IQueueBrokerBackgroundProcessor`, and `IQueueBrokerService`. It reuses `QueueBrokerBase` validation, enqueuer behaviors, handler behaviors, and scoped handler factories. The existing queueing service runs the background processor.

Each current claim has one consumer. A future message `Timestamp` delays eligibility. A missing handler preserves `WaitingForHandler` state until subscription. Native selection excludes paused queues and types before applying the batch limit. Processing renews leases, verifies content integrity, and fences stale completion. Failed deliveries retain attempt counts and reach dead-letter state at the configured limit. Permanent payload deserialization failures dead-letter immediately. Other processing exceptions use the same persisted failure-budget rules as messaging. Expired messages remain inspectable.

Operational APIs provide retained queries, content, statistics, subscriptions, waiting messages, retry, lease release, queue and type pause or resume, archive, and purge. Purge skips live claims. Type pause and resume use the same identifier rules as messaging. Circuit reset is an optional capability: `SupportsCircuitManagement` is false, and `ResetMessageTypeCircuitAsync` throws `NotSupportedException`.

Collection names use the `queueing` prefix by default. Queue-name prefixes and suffixes are independent options. Delivery is at least once across a process stop after an external side effect.

## Notifications

```csharp
using BridgingIT.DevKit.Application.Notifications;

services.AddNotificationService<EmailMessage>(configure: builder => builder
	.WithSmtpClient()
	.WithLiteDbStorageProvider(outbox: new OutboxNotificationEmailOptions()));
```

Configure the selected SMTP transport through the existing notification settings. `WithLiteDbStorageProvider` replaces `INotificationStorageProvider` and `INotificationEmailOutboxService`. Supplying `outbox` options selects `LiteDbNotificationEmailOutboxWorker` with the existing notification email queue and hosted service. Omit them to select persistence and operational APIs without starting that pipeline.

The provider supports `EmailMessage`, including attachment bytes, content, properties, attempts, and delivery outcomes. Other notification message types return an invalid-input result. The selected `INotificationService<EmailMessage>` performs delivery and persists its resulting status through the storage contract.

The worker selects a bounded snapshot of pending identities, then claims each email immediately before delivery. It renews ownership during the send and cancels transport work when renewal loses the claim. Updates and deletes from a claimed email require its current token and an unexpired lease. Expired claims remain recoverable. The worker preserves the existing notification operation behaviors and audit boundaries. A transport must tolerate redelivery after lease expiry or a process stop.

Email outbox operations include filtered queries, body and attachment inspection, statistics, retry, lease release, archive, auto-archive, and purge. Automatic retention and purge skip live claims. Processed-only worker purge selects sent and failed emails directly. It preserves pending emails and live claims without acquiring new leases. Storage deletion archives the email, while explicit purge removes selected records. Attachments share the email's native record and remain subject to the engine's document-size limit. The default collection prefix is `notifications`.

## Documents

Register typed clients through the existing Document Storage builder:

```csharp
using BridgingIT.DevKit.Application.Storage;

services.AddDocumentStorage()
    .WithLiteDbClient<LedgerSnapshot>();

public sealed class LedgerSnapshot
{
    public string Label { get; set; }
    public decimal Total { get; set; }
}
```

`WithLiteDbClient<T>` selects `LiteDbDocumentStoreProvider` while retaining the existing typed client, named resolution, behaviors, serialization, payload transforms, and query safety options. All clients share the `documents` namespace by default, with separate identities for each document type. Call `AddLiteDbDocumentStore(options => ...)` once before registering clients to select another named database, collection prefix, or materialization bound.

The provider supports exact partition/row keys, prefix and suffix selection, deterministic continuation pages, metadata, ETags, conditional creation/replacement/deletion, and atomic property updates. It uses encoded ordinal keys for native selection so the database's string collation cannot merge differently cased logical identities. Continuations bind the operation, document type, selection, page size, and visibility cutoff. Counts execute natively; key pages project metadata without deserializing payloads. The typed client continues to require explicit permission for full scans.

Expiration is applied consistently to reads, pages, counts, and existence checks at the supplied cutoff. An expired physical record still conflicts with create-only publication until removed. `IDocumentStoreRetentionProvider.SweepExpiredAsync` deletes bounded native batches. Metadata updates preserve content and its hashes while allocating a new ETag. Native collections initialize on demand without relational migrations.

Stored payloads are limited to 4 MiB. The typed client's configured logical payload limit still applies before persistence. A page needs room for its requested records and one continuation probe within `MaxMaterializedRows`. Batch-get and bulk-write operations use the existing client's ordered fallback paths; this provider does not advertise separate native batch facets.

## Common Caching

Select the document-backed composition after registering a native database:

```csharp
services.AddCaching()
    .WithLiteDbDocumentStoreProvider();
```

This selects the existing `DocumentStoreCacheProvider` and `DocumentStoreCache` facade over a native `CacheDocument` client. `ICacheProvider` and `IDistributedCache` use the same document namespace. The registration replaces their default services and selects the native cache-document client explicitly. Additional typed document clients can share that native namespace.

Cache keys support enumeration and prefix removal through the existing adapter. Absolute expiration, relative absolute expiration, and sliding expiration are stored as document expiration. Reads and explicit refresh renew sliding expiration without exceeding an absolute cap. Conditional metadata refresh preserves concurrent replacements and cannot recreate a deleted entry. Setting an entry without expiration clears an earlier deadline. The shared document-cache facade applies these expiration rules for every backend.

The composition has durable local storage and the same one-process ownership limit as the other providers. The separate Application Caching provider is described below.

## Application Caching

`LiteDbCacheStoreProvider` implements the complete `ICacheStoreProvider` contract. The existing Application Caching engine owns value contracts, encoding, source loading, refresh scheduling, and typed or binary collection access.

### Setup

Register a named database and select the native provider for declared cache collections:

```csharp
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.LiteDb;

services.AddLiteDbDatabase("application-cache", options =>
    options.ConnectionString = "/data/fictional-cache.db");

services.AddApplicationCaching()
    .WithOptions(options => options.With(value => value.Namespace, "ledger"))
    .WithValueContract<string>("text")
    .WithLiteDbProvider("ledger-cache", storage: options =>
        options.DatabaseName = "application-cache")
    .WithCollection<string>("summaries", collection =>
        collection.With(options => options.Provider, "ledger-cache"));

var summaries = serviceProvider.GetRequiredService<IApplicationCache>()
    .GetCollection<string>("summaries");
var stored = await summaries.SetAsync(new("ledger:2026-11"), "Posted");
```

Each provider alias receives its own deterministic collection prefix. Set `storage.CollectionPrefix` to choose one explicitly. Initialization freezes the host catalog and checks the stable contract aliases and dimension schemas retained for each contract version. Multiple aliases can select different named databases or isolated namespaces in one database.

### Publication and reads

Writes reserve storage as staging grows, persist bounded chunks, and publish a complete generation in one native transaction. A captured fence, optional expected version or create-only condition, and fixed source deadline are checked again at publication. Exact removal fences absent keys. Collection invalidation fences previously captured source work even when its final dependency metadata was unknown when invalidation began.

Metadata and payload chunks use separate collections. Lookup validates expiry before opening content. Reads use independently positioned, seekable streams over immutable chunks. Open readers retain obsolete generations across replacement, invalidation, and maintenance. Disposal releases the reader pin; a later maintenance batch can reclaim the old chunks. Sliding reads and explicit touch preserve the persisted absolute expiry cap.

The default chunk size is one MiB. A configured `CacheProviderOptions.ChunkSize` must be positive and at most one MiB. Process resource budgets charge staging, reader buffers, and metadata-query work. Payloads are never loaded as one stored BSON document.

### Queries and maintenance

Native metadata indexes narrow queries by collection, publication sequence, exact key, typed dimensions, publication time, and stored size. Prefix and glob selections evaluate bounded metadata candidates within the indexed collection. Queries do not read payload chunks. Signed continuations bind the collection, selection, purpose, cutoff, and publication boundary, so later publications at the same timestamp stay outside the original walk.

Metadata pages use serialized metadata bytes, independently of the conservative charges used for quota accounting. A page stops at its byte limit and continues from the next unreturned entry.

The provider stores current, retired, and staging byte counters, conservative metadata charges, write receipts, and revision-checked invalidation operation records. `GetQuotaAsync` reports `IsExact = false` because metadata and index charges are conservative. The logical quota is separate from the engine's physical file allocation. Old generations remain charged until native cleanup confirms their reclamation.

`MaintainAsync` bounds expiration cleanup, chunk reclamation, stale-process authority recovery, and terminal operation retention. Each reclaimed chunk consumes a cleanup unit. Current-process staging and reader pins remain protected. Reopening preserves acknowledged versions, write receipts, tracked operations, and counters. Staging from a previous provider lifetime can be recovered without treating it as a successful publication.

Use the normal Application Caching collection and provider limits for query pages, candidate budgets, transfer admission, and storage ceilings. `LiteDbFeatureOptions.MaxMaterializedRows` additionally bounds native metadata batches. Each query call consumes at most its supplied remaining candidate allowance. Invalidation continuation units each receive a fresh bounded allowance while retaining the original publication boundary. An exhausted allowance returns explicit incomplete progress.

Operation listings place Pending and Running records before retained terminal or paused records. The bounded maintenance runner can therefore continue active invalidations while earlier operation statuses remain available.

### Coordination scope

Capabilities are persistent storage, streaming and seekable reads, synchronous native I/O, and local ownership. `SharedStoreCoordination` is false; shared-node ownership methods fail with `CacheErrorCode.Unsupported`. Application Caching retains its existing process-local source coordination. A LiteDB file does not provide shared-node cache authority.

## Files

Register the native content owner, then select locations through the existing file factory:

```csharp
services.AddLiteDbFileStorage();
services.AddFileStorage(factory => factory
    .RegisterProvider("ledger-files", files => files
        .UseLiteDb("ledger-files")));
```

`AddLiteDbFileStorage` accepts `LiteDbFeatureOptions`. The default collection prefix is `files`. Each configured location has an isolated logical namespace. File paths use `/` separators and exact ordinal casing; backslashes normalize to `/`. A leading slash denotes the virtual root. Traversal segments, control characters, and paths longer than 4096 characters are rejected. Logical paths never become host filesystem paths.

`LiteDbFileStorageProvider` implements `IFileStorageProvider` with explicit empty directories, ancestor creation, file and directory listing, copy/move/rename, recursive deletion, metadata, SHA-256 checksums, and health checks. The existing bulk and cross-provider helpers continue to operate through that interface. File copies share immutable content until a later replacement. Metadata updates can change the modification timestamp; changing the path or actual content length requires the corresponding file operation.

### Streams and publication

Content is stored in separate 64 KiB chunks. Read streams retain one chunk buffer, support seeking within the published length, and continue reading their original generation after replacement or deletion. The content owner protects active same-process readers from collection.

Write streams are forward-only. Both `OpenWriteFileAsync` modes stage content and publish the complete file plus metadata on successful disposal. `FlushAsync` persists staged chunks without publishing the file. A failed or canceled upload preserves the previous generation. Publication checks the original file generation and namespace fence, so a competing publisher or destructive namespace change rejects the stale upload. A directory move cannot be undone by a delayed upload that recreates the old path.

`WriteFileAsync` abandons staged content when reading its source fails. Callers using `OpenWriteFileAsync` directly can call `LiteDbFileWriteStream.AbortAsync` when external work fails before close. Dispose streams before their application service provider.

### Listing and maintenance

File listings return at most 100 paths per page with continuations bound to their location and query. Pattern matching and recursive selection preserve exact casing. Directory lists and matching-subtree metadata materialization fail explicitly above `MaxMaterializedRows`. Configure at least 16 records for file storage.

`LiteDbFileContentStore.SweepContentAsync` reclaims bounded batches of chunks from unreferenced published generations and abandoned or expired uploads. It skips active readers. Uploads expire after 30 minutes without a staged write. Disposing a failed write marks its generation reclaimable. Cleanup is explicit; select its scheduling through the host's existing maintenance or Jobs facilities.

The provider reports `SupportsNotifications = false`. File Monitoring can scan this provider through its existing factory integration and persist observations through the separate event store.

## Permalinks

Select the native registry and enable the existing behavior on chosen file registrations:

```csharp
services.AddStoragePermalinks().UseLiteDb();
services.AddLiteDbFileStorage();
services.AddFileStorage(factory => factory
    .RegisterProvider("ledger-files", files => files
        .UseLiteDb("ledger-files")
        .WithPermalinks()));
```

`LiteDbStoragePermalinkRegistryProvider` persists stable identities, unique active locations, ETags, expiration, deleted entries, and exact/prefix deletion fences. The default collection prefix is `permalinks`. Its registration accepts `LiteDbFeatureOptions`. The same registry supports file, document, and blob resource locations through the existing permalink contracts.

Moves preserve the original permalink identity. Directory moves update descendant mappings under one native transaction. Delete and prefix-delete events persist tombstones so delayed earlier events cannot recreate removed mappings. Conditional expiration updates and administrative deletion check the current ETag. Listing includes the existing status, registration, kind, and location filters and remains bounded by `MaxMaterializedRows`.

The existing behavior queues storage-change notifications for the hosted permalink dispatcher. A completed file mutation can therefore precede its registry update. The selected registry also supports the existing inline fallback when the queue is unavailable. Provider persistence preserves that dispatch model.

## File monitoring

Select `LiteDbFileEventStore` independently of the file-content provider:

```csharp
using BridgingIT.DevKit.Application.Storage;

services.AddFileMonitoring(_ => { })
    .WithLiteDbStore(options =>
    {
        options.DatabaseName = "default";
        options.CollectionPrefix = "file_monitoring";
    });
```

Configure monitoring locations and processors through the existing file-monitoring builder. This registration replaces `IFileEventStore` and leaves those components in control of scanning and processing. It stores event history and processing results; file contents remain with the selected file provider.

Queries support latest-event lookup across locations or within one location, location history, path history, and inclusive UTC timestamp bounds. Location names and logical paths use exact case-sensitive identities. Paths are stored as opaque values. History returns newest events first, ordered by detection time and then committed arrival sequence.

Each event publication commits its event, sequence, and current presence together. A deletion removes the path from the present-file query. An older event arriving later cannot overwrite a newer presence decision. For equal detection times, the later committed arrival wins. Republishing the same identity and content succeeds without adding another event; different content under an existing identity fails.

Processing results persist separately by result identity and must reference a stored event. `LiteDbFileEventStore.GetProcessingResultsAsync(eventId)` reads complete outcomes in processing-time order. The provider rejects conflicting result identities and orphan references. Event lists, processing-result lists, and presence queries fail explicitly when their selected result exceeds `MaxMaterializedRows`; native filtering applies before that check. File monitoring requires a materialization bound of at least three records for event, presence, and sequence state. The default collection prefix is `file_monitoring`.

## Auditing

Register a dedicated named database before selecting the audit provider:

```csharp
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;

services.AddLiteDbDatabase("auditing", options =>
{
    options.ConnectionString = Path.Combine(Path.GetTempPath(), "fictional-ledger-audit.db");
});

services.AddAuditing()
    .WithReadScope((_, cancellationToken) => Task.FromResult(
        Result<AuditReadScope>.Success(new AuditReadScope(
            new FilterModel
            {
                Filters = [new("ModuleName", FilterOperator.Equal, "Ledger")]
            },
            "ledger-reader-v1"))))
    .WithLiteDbProvider();
```

`WithLiteDbProvider` defaults to database name `auditing` and collection prefix `auditing`. Audit appends commit independently of business changes. Use a separate database file when audit finalization can run inside a business transaction: sharing its owner would make the audit append wait for that transaction to release the database. A business rollback does not remove an audit graph already committed to the dedicated file.

The existing auditing runtime owns capture, limits, read authorization, exposure, integrity policy, and export formatting. Resolve `IAuditQueryService` and `IAuditExportService` for application reads and exports. Configure a trusted read scope for restricted access. The existing auditing runtime uses unrestricted access when no resolver is configured; a configured resolver returning a failure or null denies reads. The example grants access only to the fictional Ledger module. Configure the scope resolver from the host's actual authorization policy. Provider methods accept already trusted predicates and plans.

### Evidence and delivery

`LiteDbAuditingStorageProvider` stores the complete original canonical document, including actions, resources, entity-change evidence, details, content hash, integrity protection, and unknown additive JSON. Lifecycle metadata is stored outside that document. Archive operations preserve the original graph and its signature.

Append validates the content hash and configured record-size limit before publication. When HMAC integrity is configured through the existing auditing builder, new admissions and duplicate retries require successful verification with the host's external keys. Keys remain outside the database. Reusing an audit identity with different content returns `AuditIdentityConflict`.

Append returns a persisted receipt after the native transaction commits. Duplicate admissions return `AlreadyPresent` without allocating another publication position. Receipt epochs and admission sequences describe this provider process; durable store identity, store epoch, and publication positions survive reopening the file. `FlushAsync` reports completed inline admissions. Capabilities report durable, unbuffered, process-local storage with queries and resumable extraction.

A hosted initializer creates the native collections and control state without relational migrations. Direct provider hosts can call `InitializeAsync`. `CheckReadinessAsync` checks existing state without creating a missing file, initializing collections, or advancing publication state.

### Queries, extraction, and retention

Search, resource history, related-execution queries, aggregates, and facets use the existing approved field catalog and predicate evaluator. Scope applies before results and counts. Resource type aliases follow the auditing evaluator; resource identifiers remain exact. Search counts the selected records before paging and uses a stable audit-identity tie-breaker.

Diagnostic queries materialize at most `MaxMaterializedRows` records across the audit collection and fail explicitly when that bound is exceeded. The default is `10000`. Raising the bound increases memory use during selection. Single-record lookup, extraction, and retention use native keyed or bounded range selection and remain available beyond that diagnostic-query bound. Auditing requires a bound of at least two records to hold a record and control state.

Extraction opens a fixed committed upper position. Later appends belong to a subsequent window, including late arrivals whose event timestamps are older. Whole graphs are read in publication order within record and byte budgets. A batch containing no matching records can still advance its scanned position. Cursors retain the store identity, epoch, lifecycle generation, and removal boundary across reopening.

Archiving changes lifecycle membership without rewriting evidence. Removal deletes entire archived graphs. Both operations run bounded native batches and persist their cutoffs so delayed admissions are archived or rejected when appropriate. Lifecycle-sensitive cursors fail after membership changes; removal that crosses a required extraction range returns `AuditHistoryGap`. Final export validation retains the original lower boundary so removing already emitted evidence also invalidates publication.

After an operator-controlled restore, `RotateStoreEpochAsync(expectedEpoch)` changes the durable position epoch through a compare-and-set operation and invalidates prior cursors. Keep the existing audit retention and integrity policies when operating these APIs.

## Profiling

Select native storage through the existing profiling builder:

```csharp
services.AddProfiling(options => options.Enabled())
    .WithOperationProfiling()
    .WithRuntimeProfiling()
    .WithLiteDbProvider();
```

Capture capabilities remain explicit builder choices. `WithLiteDbProvider` replaces `IProfilingStorageProvider`, including its Runtime and Operations facets and the Runtime correlation store. It accepts `LiteDbFeatureOptions` and uses the `profiling` prefix by default. Provider identity reports `LiteDB`, a stable file/namespace scope, and no sharing between processes.

### Graphs and ownership

The provider persists complete runtime sessions, nodes, participation, context, snapshots, markers, segments, and metric observations. Operation records retain their complete child summaries, typed dimensions, measurements, quality, and failures. The storage codec preserves private identifiers and correlations omitted from response JSON. Returned operation graphs retain immutable collections.

Writer attempts, lease tokens, generation, expiry, and settled completion sequences survive reopening. Admission enforces lease ownership, occurrence and sequence identity, batch budgets, retention, and replay fences. Duplicate admissions return their original publication watermark. Runtime and Operation records preserve shared process-node identity during maintenance.

### Queries, clear, and maintenance

Queries use the existing approved filters, groups, timelines, analysis selections, and exact-process Runtime correlation rules. Signed publication boundaries and continuation cursors survive reopening the same file. Later appends remain outside an existing boundary; deletions invalidate it. The signature secret and store epoch are durable native coordination state.

Clear preparation records eligible writers and their acknowledgements before sealing the cutoff. Waiting happens outside native commands so writers can acknowledge. Sealed clears delete complete matching root graphs in bounded maintenance batches, retain late-write fences, and resume after reopening through `ResumeMaintenanceAsync`. Active Runtime sessions and pinned history retain the existing contract's protections.

Each operation reconstructs a bounded command frame from separately persisted native records, evaluates the existing profiling rules, and commits changed records atomically. `MaxMaterializedRows` bounds the complete profiling namespace loaded by a command, including identity indexes and lifecycle state. Exceeding that bound returns a persistence failure without partial mutation. Retention count and byte policies also apply; choose namespace bounds with those policies in mind. This implementation prioritizes functionality. It does not claim fixed throughput or latency.

## Entity permissions

`LiteDbEntityPermissionProvider` implements `IEntityPermissionProvider`. The existing Identity evaluators own direct, inherited, role, wildcard, and default permission resolution. The native provider stores grants and resolves parent identities from registered native entity collections.

### Setup

Register hierarchical entities with either the native repository or ActiveEntity provider, then configure the existing permission options:

```csharp
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

services.AddLiteDbRepository<LedgerFolder>();
services.AddLiteDbEntityPermissions(options =>
{
    options.AddHierarchicalEntity<LedgerFolder>(folder => folder.ParentId, Permission.Read);
    options.EnableCaching = true;
});

public sealed class LedgerFolder : Entity<Guid>
{
    public Guid? ParentId { get; set; }
}
```

`WithLiteDbEntityPermissions` also selects the provider through the existing authorization builder. The optional storage callback selects `DatabaseName`, `CollectionPrefix`, and traversal/materialization bounds. The default prefix is `permissions`.

### Grants, hierarchy, and transactions

User and role grants are idempotent. A null entity identity grants access across that entity type. Exact identities, principal names, and permission names retain ordinal matching. Revocation supports one grant or every grant belonging to one user or role. Returned permission records are detached snapshots.

Hierarchy traversal reads the selected database's registered entity collection through its mapper. It returns parent identities nearest first, stops at missing nodes, and prevents cycles from repeating identities. It uses no EF hierarchy services. Register each hierarchical type against an unambiguous collection in the chosen database.

The provider is scoped and joins the caller's native entity transaction. Entity and grant changes can commit or roll back together. The default evaluator bypasses its shared cache inside an explicit native transaction, so an uncommitted grant cannot enter that cache. Custom evaluators keep their existing registration and behavior.

When caching is enabled, grant mutations evict the permission-result prefix, including inherited and wildcard results. Caller cancellation governs native admission and mutation. A successful mutation finishes cache eviction independently of subsequent caller cancellation. An existing host `ICacheProvider` is preserved. Otherwise the registration supplies a local memory cache that uses public key enumeration for prefix eviction. Grant storage remains native in both cases. Grant enumeration and hierarchy traversal fail explicitly when they exceed `MaxMaterializedRows`.

## Broadcasting registry

`LiteDbBroadcastRegistryStore` implements `IBroadcastRegistryStore` for the existing Broadcasting runtime:

```csharp
services.AddLiteDbDatabase("registry", options =>
    options.ConnectionString = "/data/fictional-registry.db");

services.AddBroadcasting(options => options.Scopes("Ledger"))
    .WithLiteDbRegistry(storage => storage.DatabaseName = "registry");
```

The registry persists complete node registrations, normalized scopes, delivery success and failure observations, lease renewal, expiration, and operational lists. Repeated upsert replaces the scope set and reactivates the node. Failure thresholds deactivate unreachable nodes; successful delivery reactivates them. Active queries exclude expired leases before an explicit expiration sweep. The default collection prefix is `broadcasting`.

Capabilities report `IsShared = false` and `RequiresAdvertisedAddress = false`. Selecting this registry preserves the chosen transport and the existing handler runtime. It adds local registry persistence; broadcasts retain their existing short-lived delivery behavior. Node listings and expiration sweeps use the configured materialization bound.

## Durable feature settings

`LiteDbFeatureOptions` selects `DatabaseName`, `CollectionPrefix`, `Serializer`, and `MaxMaterializedRows`. Register the named database before selecting its providers. Settings are copied at registration, and collection-name collisions fail before a database opens. Separate named databases have independent sessions and records.

Unpaged queries and command-local collections default to a maximum of `10000` materialized records. The provider fails explicitly on overflow. Worker eligibility filtering and retention use native bounded selection before reading payloads. Diagnostic filters operate on bounded detached models. Increase the limit deliberately when the local dataset requires it.

Messaging and queueing default to batches of `128`, five delivery attempts, a 30-second lease, 10-second renewal, a 15-second startup delay, and a 30-second polling and retry interval. Renewal must be positive and shorter than the lease. Outgoing application-message capture uses the same settings with no expiration by default. Notification batch and lease settings use `OutboxNotificationEmailOptions`.

One application process owns each Direct-mode file. Concurrent workers within that process share the database owner and use separate sessions. A long entity transaction can delay background claims and renewals. Handlers and transports must tolerate retries when ownership expires.

## Other feature providers

The remaining work is tracked in the [LiteDB Storage Providers specification](specs/spec-infrastructure-litedb-providers.md). Blob storage is deferred. Further document-storage work is also deferred; the existing document provider remains available. Log-entry persistence is excluded by user direction because the host keeps its SQL Server logging sink. Key/Value Storage waits for its owning contract. Any later LiteDB setup and behavior documentation belongs in feature sections on this page.

## Engine baseline and v5 files

The current package is pinned to upstream commit `b3c4406231908c6ca29f6d4b2769aa08351d6fc2`. A bounded check opened disposable copies of representative plain and encrypted LiteDB `5.0.21` files through the selected v6 integration. Identities, values, indexes, filtered queries, updates, and reopen checks passed without a separate conversion tool.

That result applies to the checked samples. It does not establish compatibility for every v5 file or determine whether the engine changes its internal format on open. Use disposable copies for further compatibility checks. A deployed-consumer migration service and historical model compatibility framework are outside the current extension's scope.

General repository and ActiveEntity concepts are documented in [Domain Repositories](features-domain-repositories.md) and [ActiveEntity](features-domain-activeentity.md). LiteDB-specific usage remains on this page. The [provider specification](../docs/specs/spec-infrastructure-litedb-providers.md) and [COV-002 implementation record](../docs/plan/pln-feature-litedb-activeentity-1.md) retain requirements and delivery history.
