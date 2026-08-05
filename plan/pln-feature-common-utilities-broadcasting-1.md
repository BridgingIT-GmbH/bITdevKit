---
goal: Implement shared-registry live-node broadcasting across Common.Utilities, Entity Framework, and ASP.NET Core
version: 1.0
date_created: 2026-08-05
last_updated: 2026-08-05
owner: bITdevKit maintainers
status: 'Completed'
tags: [feature, broadcasting, common-utilities, entity-framework, aspnet-core, distributed-systems, observability]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

This plan implements `docs/specs/spec-common-utilities-broadcasting.md`. The implementation adds typed, best-effort, short-lived broadcasts to every currently registered and reachable node in one or more scopes. It provides a re-entrant fluent registration builder, one shared and environment-configurable host runtime and registry, bounded node-local dispatch, an in-memory registry, an Entity Framework registry, an internal HTTP receiver and transport, built-in shared-secret authentication, authentication extension points, node lifecycle management, diagnostics, logs, metrics, a simple authorized operational dashboard, and deterministic tests. It does not add durable messages, message polling, an outbox, replay, handler-completion tracking, or a new project.

## Implementation Progress

Implementation completed on 2026-08-05.

- **Complete production slices**: Phases 1-6 provide the re-entrant core runtime, in-memory and EF registries, lifecycle/leases, bounded local dispatch, publication fan-out, HTTP transport/receiver, exact shared-secret authentication, custom authentication, and ordered address resolution.
- **Complete operational surface**: provider-neutral diagnostics, deny-by-default manual-removal authorization, safe structured logging helpers, and low-cardinality metrics hooks are implemented.
- **Dashboard complete**: the existing DevKit dashboard shows registrations and details and can publish the built-in no-op probe to the default scope through a compact header action.
- **Example integration complete**: An example host enables Broadcasting only in Development while exercising the Entity Framework registry and HTTP transport on its single node.
- **Verified provider coverage**: Common Broadcasting runtime tests, EF model/store tests, relational SQLite, SQL Server, and PostgreSQL registry contracts, Presentation HTTP/authentication-isolation tests, and a real two-node Kestrel delivery test pass.
- **Documentation complete**: the canonical common utilities guide contains core, Entity Framework, HTTP transport, authentication, and security guidance.
- **Final review corrections**: the implementation now includes sender identity plus target-scope and delivery-window result metadata, portable lease expiry across all three relational providers, credential-free address validation, stale-removal metrics, a compact dashboard probe action, and grouped console commands.
- **Verification complete**: the repository build, focused feature tests, provider contracts, formatting, and diff checks were run sequentially.

## 1. Requirements & Constraints

- **REQ-001**: Add the public core broadcasting API under `src/Common.Utilities/Broadcasting` without referencing Application-layer Messaging, Queueing, Jobs, Orchestration, Infrastructure, or Presentation types.
- **REQ-002**: Define `IBroadcastService.PublishAsync<TBroadcast>` to accept one typed payload, optional target scopes, optional per-publication settings, and a cancellation token, and to return `Task<Result<BroadcastResult>>`. An omitted, null, empty, or whitespace-only scope collection targets `default`.
- **REQ-003**: Represent target scopes as a normalized, distinct `IReadOnlyCollection<string>` in publication requests, envelopes, and results so one broadcast can target several scopes without duplicate node delivery.
- **REQ-004**: Return one `BroadcastNodeDeliveryResult` per selected node, the normalized target scopes, delivery start/completion timestamps, and aggregate counts for targets, responses, accepted outcomes, failures/unreachable outcomes, and expired outcomes. Per-node failures do not fail the outer `Result<BroadcastResult>`.
- **REQ-005**: Fail the outer Result only when publication cannot be meaningfully executed, including a disabled runtime, invalid input, an inactive/unregistered sender for a shared store, a target scope outside the sender registration, serialization failure, or unavailable registry infrastructure.
- **REQ-006**: Implement `AddBroadcasting` as a re-entrant fluent registration entry point returning `BroadcastingBuilderContext`. Repeated calls against one `IServiceCollection` reopen and mutate one host-wide registration state.
- **REQ-007**: Register exactly one host-wide `IBroadcastService`, effective `IBroadcastRegistryStore`, node lifecycle, receiver, transport, options instance, handler catalog, duplicate tracker, local dispatcher, and diagnostics service.
- **REQ-008**: Merge scopes contributed by repeated `AddBroadcasting` calls using trimmed, case-insensitive uniqueness while retaining one stable display value per scope. When no call contributes a scope, register in `default`; replace only that implicit fallback when the first explicit scope is contributed, while retaining an explicitly contributed `default`.
- **REQ-009**: Make repeated registration of the same broadcast CLR type and handler implementation idempotent. Throw `InvalidOperationException` when different handler implementations are registered for the same broadcast CLR type.
- **REQ-010**: Use the registered broadcast CLR full type name as the transport type identifier. Resolve types only from the local handler catalog; never activate a type from an untrusted envelope name.
- **REQ-011**: Use one effective registry provider for the host. Register the in-memory provider as the single-process fallback, allow the first explicit provider to replace that fallback, make repeated selection of the same explicit provider idempotent, and reject a different explicit provider.
- **REQ-012**: Use one effective transport for the host. Make repeated HTTP transport selection idempotent and reject a conflicting explicit transport.
- **REQ-013**: Keep the registry provider-neutral through `IBroadcastRegistryStore`; the core publisher, lifecycle, diagnostics, and receiver must not depend on EF entities or `DbContext`.
- **REQ-014**: Store node discovery state only. Registry providers must never persist envelopes, payloads, pending deliveries, duplicate records, handler results, broadcast history, or completion history.
- **REQ-015**: Implement the in-memory registry as a process-local singleton supporting registration upsert, unregister, active-scope snapshots, reachability updates, reactivation, lease renewal/expiry, inspection, and manual removal.
- **REQ-016**: Maintain one node registration per process and associate it with the distinct union of scopes contributed by all host modules.
- **REQ-017**: Default node identity to `<machine-or-container-name>:<process-id>` and permit replacement through `IBroadcastNodeIdentityProvider`.
- **REQ-018**: Register the node after the host and receiver address are available, update the existing registration idempotently, and attempt unregister during graceful shutdown.
- **REQ-019**: Do not poll the registry for messages. Registry access while idle is limited to registration lifecycle and optional coarse lease maintenance.
- **REQ-020**: Disable registration leasing by default. When enabled, default renewal to one minute and lease duration to three minutes; mark expired registrations inactive rather than deleting them.
- **REQ-021**: Mark a repeatedly unreachable registration inactive after three consecutive failed deliveries by default. Reset failure diagnostics after successful delivery or re-registration.
- **REQ-022**: Resolve one fixed active-registration snapshot per publication, deduplicate nodes matched by several scopes, dispatch the sender locally, and issue one direct transport attempt to every other selected node.
- **REQ-023**: Permit publication only when the sender is active in a shared registry and every target scope belongs to the sender registration. In-memory single-process publication must remain usable without an HTTP address.
- **REQ-024**: Return successful zero-delivery results for an empty target snapshot unless `BroadcastPublishOptions.RequireAtLeastOneTarget` is true.
- **REQ-025**: Bound serialized payloads to 64 KB by default, per-node requests to two seconds by default, delivery fan-out to 16 concurrent nodes by default, and lifetime to five seconds by default.
- **REQ-026**: Use one delivery attempt per node by default. Do not add automatic long-lived retries.
- **REQ-027**: Define receiver outcomes `Accepted`, `AlreadyProcessed`, `Expired`, `Unsupported`, `Rejected`, `Failed`, `Unreachable`, and `TimedOut`.
- **REQ-028**: Make `Accepted` mean only that validation, duplicate reservation, handler lookup, and bounded queue admission succeeded. Handler completion or failure must not alter the returned delivery result.
- **REQ-029**: Maintain at most 1,024 recent broadcast IDs for ten minutes by default and always validate retention as longer than the maximum broadcast lifetime.
- **REQ-030**: Reserve a broadcast ID atomically before queue admission, commit the reservation only after successful admission, and remove the reservation when admission is rejected so a later delivery can retry.
- **REQ-031**: Use one bounded queue per broadcast CLR type with capacity 32 by default. Preserve acceptance order for a type, allow different types to run concurrently, and create one DI scope per handler execution.
- **REQ-032**: Reject queue admission immediately with `Rejected` when a per-handler queue is full. Do not wait indefinitely for local capacity.
- **REQ-033**: Dispatch self-targeted broadcasts through the same receiver validation, duplicate, and bounded-queue path used by HTTP delivery; never call the sender's own HTTP endpoint.
- **REQ-034**: Use the registered DevKit `ISerializer` when available and `SystemTextJsonSerializer` as the fallback for typed payload serialization and deserialization.
- **REQ-035**: Implement `IBroadcastingContext` with `DbSet<BroadcastNodeRegistrationEntity> BroadcastNodeRegistrations` and `DbSet<BroadcastNodeScopeEntity> BroadcastNodeScopes`.
- **REQ-036**: Map `BroadcastNodeRegistrationEntity` to `__Broadcasting_NodeRegistrations` and `BroadcastNodeScopeEntity` to `__Broadcasting_NodeScopes` using EF attributes and conventions without a Broadcasting-specific `OnModelCreating` call.
- **REQ-037**: Store node identity, normalized identity, advertised address, process start, registration time, protocol version, active state, success/failure diagnostics, consecutive failure count, lease timestamps, and `Guid ConcurrencyVersion` once on `BroadcastNodeRegistrationEntity`.
- **REQ-038**: Implement `BroadcastNodeRegistrationEntity.AdvanceConcurrencyVersion()` and use `[ConcurrencyCheck]` consistently with Jobs and Messaging entities.
- **REQ-039**: Normalize node-to-scope associations in `BroadcastNodeScopeEntity`; use a composite key of registration ID and normalized scope, preserve the display scope, and add a normalized-scope lookup index.
- **REQ-040**: Make the application own all migrations. Do not add a production migration to this repository.
- **REQ-041**: Implement `EntityFrameworkBroadcastRegistryStore<TContext>` with operation-owned DI scopes. Never retain a scoped `TContext` inside singleton broadcasting services.
- **REQ-042**: Make EF registration/scope replacement atomic and handle duplicate-key or optimistic-concurrency races with one bounded reload-and-retry operation.
- **REQ-043**: Implement the HTTP receiver under `/_bdk/api/broadcasting` by default using the existing `IEndpoints`/`EndpointsBase` registration and `app.MapEndpoints()` flow.
- **REQ-044**: Add a direct `HttpBroadcastTransport` using `IHttpClientFactory`, one request per remote node, `HttpCompletionOption.ResponseHeadersRead`, a deadline bounded by request timeout and envelope expiry, and no automatic retry handler.
- **REQ-045**: Propagate the broadcast ID, publishing node identity, and application correlation ID in envelope fields and the middleware-compatible `CorrelationId` HTTP header. Treat the application correlation ID and distributed tracing `TraceId` as separate values, resolve the publisher value through `CorrelationId.Current`, and establish it as current while a receiver handler executes.
- **REQ-046**: Resolve advertised addresses in this order: explicit configured address, ordered custom resolvers, concrete Kestrel-bound address. Reject wildcard hosts, unsupported schemes, missing routes, and unresolved addresses for shared multi-node stores.
- **REQ-047**: Add a dedicated HTTP authentication abstraction that can apply credentials to outbound requests and authenticate inbound requests. Register allow-all authentication by default, provide shared-secret authentication as a built-in DevKit implementation configured with any string value from application configuration, and permit bearer, certificate, or platform implementations without changing core broadcast contracts.
- **REQ-048**: For built-in shared-secret authentication, canonicalize a null configured value to `string.Empty` but never trim or otherwise normalize non-null values. Transport the exact UTF-8 bytes as a Base64 `X-Bdk-Broadcast-Key` header value, decode the inbound representation, compare bytes with `CryptographicOperations.FixedTimeEquals`, accept a missing or empty header only when the configured secret is empty, and reject malformed, multiply-valued, or non-matching values before envelope payload deserialization or local queue admission.
- **REQ-049**: Expose provider-neutral operational diagnostics for nodes grouped by scope, node identity, advertised address, registration metadata, protocol version, reachability, failure count, lease state, and privileged manual removal. Add an authorized plugin to the existing DevKit dashboard that displays those details, process-local `Published` and `Accepted locally` metrics, and publishes only the built-in no-op `BroadcastProbe` to the default scope from a compact header action. Expose the same inspection and probe capabilities through grouped Console Commands using the established minimal table style.
- **REQ-050**: Emit low-cardinality metrics for registrations, broadcasts, target count, delivery outcomes, latency, expiry, duplicates, unsupported types, queue rejection, and stale removal. Dimensions may include normalized scope, broadcast type, and outcome but never node identity or address. Classify `broadcasting_*` series under the `broadcasting` feature in the shared metrics snapshot so the current process can report successful publications and local receiver admissions.
- **REQ-051**: Emit source-generated structured logs using `[LogKey] message (property=value)` templates. Logs may include scope, type, node identity, and outcome but never payloads, credentials, authentication headers, or full endpoint addresses.
- **REQ-052**: Document every new public class, interface, enum, method, and property with XML documentation and a usage example, consistent with repository policy.
- **REQ-053**: Use `BridgingIT.DevKit.Common` for core public types, `BridgingIT.DevKit.Infrastructure.EntityFramework.Broadcasting` for EF types, `BridgingIT.DevKit.Presentation.Web` for web types, and `Microsoft.Extensions.DependencyInjection` for fluent service-registration extensions.
- **REQ-054**: Default the one composed runtime to enabled after `AddBroadcasting`. Support `BroadcastingOptionsBuilder.Enabled(bool enabled = true)`, including environment-aware calls such as `Enabled(builder.Environment.IsDevelopment())`; the latest explicit value applied to the shared options controls the entire host runtime.
- **REQ-055**: When disabled, keep the shared options and additive module registrations in dependency injection but do not register or renew a node, map the receiver endpoint, start local dispatch or lease loops, access the registry, serialize publications, or invoke a transport. `PublishAsync` must return a typed Broadcasting-disabled Result error without invoking those dependencies, and enabled-only provider values must not be required for disabled-host startup.
- **REQ-056**: Isolate the broadcast receiver from application authentication and authorization. Do not call `AddAuthentication`, change default schemes, register or alter application policies, or use the application's bearer requirement for built-in shared-secret mode. Mark only the broadcast endpoint with `AllowAnonymous` metadata so default/fallback OAuth or bearer authorization is bypassed, then enforce the dedicated broadcast authentication before reading the payload; all non-broadcast endpoints retain their existing behavior.
- **SEC-001**: Accept only `http` and `https` advertised addresses for the HTTP provider and validate that each address identifies a specific process rather than a wildcard binding.
- **SEC-002**: Enforce the configured raw serialized-payload limit before dispatch and enforce a bounded HTTP request-body limit at the endpoint.
- **SEC-003**: Do not write credentials, payload content, transport authentication values, or complete endpoint addresses to logs, metrics, or Result errors. Operational diagnostics may expose the validated advertised address but must never expose credentials.
- **SEC-004**: Do not deserialize unknown CLR type names or call `Type.GetType` on untrusted envelope values.
- **SEC-005**: Keep operational manual-removal APIs behind an authorization abstraction; do not map an unauthenticated management endpoint in this plan.
- **SEC-006**: Keep the configured shared secret only in application configuration/options memory and its transient Base64 outbound authentication header. Never persist it in registry entities or envelopes, return it in diagnostics/Results, or emit it in telemetry. Empty and whitespace-only values are valid by design; Base64 is transport encoding, not encryption.
- **SEC-007**: Apply `AllowAnonymous` only as endpoint metadata to bypass host default/fallback authorization for the internal broadcast route. The dedicated broadcast authentication remains mandatory when selected, and registration must not weaken or modify authentication or authorization for any other application endpoint.
- **SEC-008**: Inherit the existing dashboard endpoint-group authorization for the Broadcasting page and probe action. Do not accept arbitrary CLR type names or payload JSON, do not expose credentials, and do not map a separate anonymous management route.
- **CON-001**: Do not create a new production or test project.
- **CON-002**: Do not add a dependency from Common.Utilities to Messaging, Queueing, Jobs, Orchestration, Entity Framework, or ASP.NET Core.
- **CON-003**: Do not add durable delivery, message polling, stored messages, an outbox, dead letters, event replay, exactly-once claims, cross-node ordering, handler-completion callbacks, or execution history.
- **CON-004**: Do not add named pipes, Unix-domain sockets, raw TCP, gRPC, load balancing, or a general service-discovery API.
- **CON-005**: Do not use a shared load-balanced URL as a node address.
- **CON-006**: Do not change Performance Snapshot Dashboard behavior in this plan. The shared metrics snapshot may classify existing `broadcasting_*` instruments for the Broadcasting operational page, but shall not add durable history or claim handler completion.
- **PAT-001**: Follow `MetricsServiceCollectionExtensions.AddMetrics` for reusing a mutable options instance across repeated registration calls.
- **PAT-002**: Follow `BlobStorageBuilderContext` for cross-project fluent provider extensions and explicit provider-conflict validation.
- **PAT-003**: Follow `IJobsContext`, Jobs entities, and `BrokerMessage` for EF capability contracts, table attributes, indexes, required/length metadata, and optimistic concurrency.
- **PAT-004**: Follow `EntityFrameworkJobStoreProvider<TContext>` for operation-owned service scopes and provider-neutral time through `TimeProvider`.
- **PAT-005**: Follow `EndpointsBase`, `IEndpoints`, `AddEndpoints<T>`, and `MapEndpoints` for receiver endpoint registration.
- **PAT-006**: Follow `PeriodicBackgroundService` and `IHostedLifecycleService` semantics for deterministic registration, lease, and shutdown behavior.
- **PAT-007**: Use `Result`/`Result<T>` and typed `IResultError` implementations for recoverable publication failures.
- **PAT-008**: Use DevKit `IMetricsService`, `Metrics.Series`, and source-generated logging instead of introducing a second telemetry stack.
- **GUD-001**: Use `TimeProvider` for expiry, retention, lease, and timeout calculations so tests can use fake time.
- **GUD-002**: Keep public application APIs transport- and persistence-neutral; do not expose `HttpClient`, URLs, EF entities, `DbContext`, or registry-store queries through `IBroadcastService`.
- **GUD-003**: Run repository-wide build and test commands sequentially to avoid transient `obj/ref` metadata failures.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Add transport-neutral contracts, options, validation, errors, and the re-entrant fluent registration shell.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create `src/Common.Utilities/Broadcasting/BroadcastingOptions.cs` and `BroadcastingOptionsBuilder.cs`. Implement all defaults from REQ-020, REQ-021, REQ-025, REQ-029, REQ-031, and REQ-054, including `Enabled = true` once `AddBroadcasting` creates the shared options. Builder methods mutate that existing instance and include `Enabled(bool enabled = true)`, `StartupDelay`, optional database-readiness coordination, additive `Scopes`, `NodeIdentity`, `DeliveryTimeout`, `MaximumConcurrentDeliveries`, `MaximumPayloadSize` as `ByteSize`, `DefaultLifetime`, `DuplicateCapacity`, `DuplicateRetention`, `UnreachableFailureThreshold`, `HandlerQueueCapacity`, and lease settings. Keep advertised-address and receiver-route configuration in Presentation.Web. Implement scalar validation immediately and ensure the enabled runtime always has an effective scope. | Yes | 2026-08-05 |
| TASK-002 | Create `src/Common.Utilities/Broadcasting/Models/BroadcastModels.cs` containing documented `BroadcastEnvelope`, `BroadcastPublishOptions`, `BroadcastContext`, `BroadcastNodeRegistration`, `BroadcastNodeRegistrationRequest`, `BroadcastNodeDeliveryResult`, `BroadcastResult`, `BroadcastDeliveryOutcome`, and registry capability/diagnostic models. Use `IReadOnlyCollection<string> TargetScopes`, raw `byte[] Payload`, publishing node identity, UTC `DateTimeOffset` delivery-window values, protocol version `1`, and no public advertised-address field in delivery results. | Yes | 2026-08-05 |
| TASK-003 | Create `src/Common.Utilities/Broadcasting/Abstractions/BroadcastingAbstractions.cs` containing `IBroadcastService`, `IBroadcastHandler<TBroadcast>`, `IBroadcastRegistryStore`, `IBroadcastTransport`, `IBroadcastReceiver`, `IBroadcastLocalDispatcher`, `IBroadcastNodeIdentityProvider`, `IBroadcastNodeAddressResolver`, `IBroadcastingDiagnostics`, and operational-authorization abstractions. Define cancellation and Result semantics exactly as REQ-002 through REQ-005 and REQ-013. | Yes | 2026-08-05 |
| TASK-004 | Create `src/Common.Utilities/Broadcasting/BroadcastErrors.cs` with typed `IResultError` implementations for Broadcasting disabled, validation, registry unavailable, sender not registered, scope forbidden, serialization, and no-target failures. Populate structured properties with safe scope/type/count data only. | Yes | 2026-08-05 |
| TASK-005 | Create `src/Common.Utilities/Broadcasting/Registration/BroadcastingRegistrationState.cs`. Store the shared handler map, effective registry provider type plus implicit/explicit state, effective transport type, and registration markers. Implement lock-protected methods that make exact duplicate registrations idempotent and throw on conflicts described by REQ-009, REQ-011, and REQ-012. | Yes | 2026-08-05 |
| TASK-006 | Create `src/Common.Utilities/Broadcasting/Registration/BroadcastingBuilderContext.cs` exposing `Services`, shared `Options`, and documented provider-extension methods that select an effective registry or transport through the encapsulated shared state. Add `AddHandler<TBroadcast,THandler>()` with scoped handler registration and exact duplicate/conflict behavior. Keep state mutation encapsulated while allowing Infrastructure.EntityFramework and Presentation.Web extensions to compose without `InternalsVisibleTo`. | Yes | 2026-08-05 |
| TASK-007 | Create `src/Common.Utilities/Broadcasting/Registration/BroadcastingServiceCollectionExtensions.cs` in the `Microsoft.Extensions.DependencyInjection` namespace. Implement `AddBroadcasting(Action<BroadcastingOptionsBuilder> configure = null)` by finding or creating one options/state implementation instance in `IServiceCollection`, applying additive callbacks in call order so the latest explicit enabled value wins, registering a resolvable but runtime-gated shell plus TimeProvider/serializer fallbacks and core services with `TryAdd*`, and returning a new context over the same state. Register the in-memory store and local-only transport as implicit fallbacks. | Yes | 2026-08-05 |
| TASK-008 | Create `tests/Common.UnitTests/Utilities/Broadcasting/BroadcastingOptionsTests.cs` and `BroadcastingRegistrationTests.cs`. Verify every default and invalid boundary, default enabled state, environment-style enable/disable values, latest explicit enabled value across repeated calls, shared option reuse, scope union/case-insensitive deduplication, one service descriptor per runtime component, identical handler idempotency, conflicting handlers, implicit provider replacement, same explicit provider idempotency, and explicit provider/transport conflicts. | Yes | 2026-08-05 |

Completion criteria:

- **GATE-001**: `Common.Utilities` compiles without references to EF, ASP.NET Core, Messaging, Queueing, Jobs, or Orchestration.
- **GATE-002**: Repeated registration tests resolve one shared runtime and prove deterministic additive/conflict and host-wide enablement semantics.
- **GATE-003**: Options tests prove every documented default, enabled/disabled behavior, and validation rule.

### Implementation Phase 2

- GOAL-002: Implement shared in-memory node discovery, identity, lifecycle, leases, and provider-neutral diagnostics.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-009 | Create `src/Common.Utilities/Broadcasting/Registry/BroadcastingNormalization.cs` with stable trimming and `ToUpperInvariant` normalized keys for scopes and node identities. Use display values separately from normalized keys in all providers. | Yes | 2026-08-05 |
| TASK-010 | Create `src/Common.Utilities/Broadcasting/Registry/DefaultBroadcastNodeIdentityProvider.cs`. Resolve configured identity first; otherwise use `Environment.MachineName` plus `Environment.ProcessId`. Validate the 256-character bound and reject empty/whitespace identities. | Yes | 2026-08-05 |
| TASK-011 | Create `src/Common.Utilities/Broadcasting/Registry/InMemoryBroadcastRegistryStore.cs`. Use a lock-protected or concurrent state model that atomically upserts one node with its complete scope set, returns immutable active snapshots, deduplicates scope matches, tracks success/failure diagnostics, deactivates at the configured threshold, renews/expires leases, reactivates on registration, unregisters, lists diagnostics, and removes nodes. Expose capabilities indicating process-local/non-shared operation and no required advertised HTTP address. | Yes | 2026-08-05 |
| TASK-012 | Create `src/Common.Utilities/Broadcasting/Registry/BroadcastingDiagnostics.cs` over `IBroadcastRegistryStore`. Return operational models grouped by scope with the validated advertised address, authorize manual removal through `IBroadcastOperationalAuthorizer`, and never expose payloads or credentials. Register a deny-by-default authorizer for mutations. | Yes | 2026-08-05 |
| TASK-013 | Create `src/Common.Utilities/Broadcasting/Hosting/BroadcastNodeLifecycleService.cs` as a non-blocking `BackgroundService`. When Broadcasting is disabled, return without resolving identity/address or accessing the registry. Otherwise wait for `ApplicationStarted`, apply the configured startup delay, optionally await database readiness, resolve the one node identity and effective address, register the union of scopes, fail clearly when a shared provider has no direct address, and attempt unregister during stopping without converting shutdown cancellation into a host failure. | Yes | 2026-08-05 |
| TASK-014 | Create `src/Common.Utilities/Broadcasting/Hosting/BroadcastRegistrationLeaseService.cs` using `PeriodicBackgroundService`. Disable execution unless both Broadcasting and leasing are enabled; renew the local registration every configured interval and mark expired shared registrations inactive through the store. Never read or poll for broadcast messages. | Yes | 2026-08-05 |
| TASK-015 | Complete core registration in `BroadcastingServiceCollectionExtensions.cs` by registering the lifecycle and lease hosted services once with `TryAddEnumerable`, mapping the in-memory fallback to `IBroadcastRegistryStore`, and ensuring all repeated `AddBroadcasting` calls resolve the same instances. | Yes | 2026-08-05 |
| TASK-016 | Create `tests/Common.UnitTests/Utilities/Broadcasting/InMemoryBroadcastRegistryStoreTests.cs`, `BroadcastNodeLifecycleServiceTests.cs`, and `BroadcastingDiagnosticsTests.cs`. Use `FakeTimeProvider` and deterministic host-lifetime tokens to verify atomic upsert, scope replacement, snapshots, duplicate-scope deduplication, success/failure state, threshold deactivation, reactivation, startup delay without blocked host startup, optional database-readiness gating, leases, expiry, unregister, shared-address failure, process-local startup, disabled lifecycle/lease no-ops, authorization, and manual removal. | Yes | 2026-08-05 |

Completion criteria:

- **GATE-004**: One process maintains one registration containing the union of scopes regardless of registration-call count.
- **GATE-005**: The in-memory provider passes the complete provider contract without a database or network.
- **GATE-006**: Idle operation performs no message-discovery polling; only enabled lease maintenance is periodic, and a disabled runtime performs no registry maintenance.

### Implementation Phase 3

- GOAL-003: Implement safe type resolution, duplicate protection, and bounded asynchronous local execution.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-017 | Create `src/Common.Utilities/Broadcasting/Dispatch/BroadcastHandlerCatalog.cs`. Materialize immutable mappings from CLR full type name and payload type to the single registered handler type. Validate missing full names, duplicate type-name collisions, and conflicting registrations before dispatch starts. | Yes | 2026-08-05 |
| TASK-018 | Create `src/Common.Utilities/Broadcasting/Dispatch/RecentBroadcastTracker.cs`. Implement atomic `TryReserve`, `Commit`, and `Release` operations with a capacity-bounded, expiry-aware record set driven by `TimeProvider`. Ensure duplicate concurrent requests yield one reservation and expired entries are evicted without an unbounded collection. | Yes | 2026-08-05 |
| TASK-019 | Create `src/Common.Utilities/Broadcasting/Dispatch/BroadcastLocalDispatcher.cs`. Create one bounded `Channel<AcceptedBroadcast>` per catalog entry with `FullMode = Wait`, use non-blocking `TryWrite` for admission so a full queue returns `false`, assign one reader per type, and create one DI scope per execution. Start work in acceptance order for each type; permit channels for different types to drain concurrently; log handler failures without reporting completion to senders. | Yes | 2026-08-05 |
| TASK-020 | Create `src/Common.Utilities/Broadcasting/Dispatch/BroadcastLocalDispatchHostedService.cs` to start and stop all dispatcher readers exactly once and complete channels during shutdown. When Broadcasting is disabled, do not start readers or allocate active queue-processing work. Register it once as `IHostedService`. | Yes | 2026-08-05 |
| TASK-021 | Create `src/Common.Utilities/Broadcasting/Dispatch/BroadcastReceiver.cs`. Validate protocol, scopes, timestamps, expiry, payload size, type catalog membership, and duplicate state in that order. Deserialize only using the catalog's known payload type, reserve the ID, attempt queue admission, commit on acceptance, release on rejection/failure, and return the structured outcome without awaiting handler execution. | Yes | 2026-08-05 |
| TASK-022 | Create `tests/Common.UnitTests/Utilities/Broadcasting/RecentBroadcastTrackerTests.cs`, `BroadcastLocalDispatcherTests.cs`, and `BroadcastReceiverTests.cs`. Use completion gates rather than sleeps to prove capacity/retention, concurrent duplicate exclusion, reservation rollback, queue rejection, exact type whitelist behavior, malformed/oversized/expired/unsupported outcomes, one service scope per execution, same-type start order, cross-type concurrency, cancellation, and handler-failure isolation. | Yes | 2026-08-05 |

Completion criteria:

- **GATE-007**: Unknown envelope type names cannot cause runtime type activation or deserialization.
- **GATE-008**: Accepted work is bounded per type and begins in per-type acceptance order.
- **GATE-009**: Duplicate and queue-full races deterministically execute a handler at most once per accepted ID while allowing a rejected ID to be retried.

### Implementation Phase 4

- GOAL-004: Implement publication orchestration, bounded fan-out, result aggregation, and reachability feedback.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-023 | Create `src/Common.Utilities/Broadcasting/Transport/LocalOnlyBroadcastTransport.cs`. Return `Unreachable` for non-local targets and serve only as the implicit fallback when no remote transport is selected; never attempt network I/O. | Yes | 2026-08-05 |
| TASK-024 | Create `src/Common.Utilities/Broadcasting/BroadcastService.cs`. Check the shared enabled state first and return `BroadcastingDisabledError` without validating payloads or invoking serializer, registry, receiver, or transport dependencies when disabled. When enabled, validate input/options, normalize scopes, serialize once through `ISerializer`, enforce raw payload size, create one immutable envelope and registry snapshot, validate sender registration/scopes for shared stores, deduplicate nodes by normalized identity, route self through `IBroadcastReceiver`, and route remote nodes through `IBroadcastTransport`. | Yes | 2026-08-05 |
| TASK-025 | In `BroadcastService`, bound remote work with `Parallel.ForEachAsync` or an equivalent fixed worker set using `MaximumConcurrentDeliveries`. Derive each attempt deadline from the earlier of request timeout, envelope expiry, and caller cancellation; do not retry. Continue other nodes after expected remote failures. | Yes | 2026-08-05 |
| TASK-026 | In `BroadcastService`, map transport exceptions and cancellations to `Unreachable`, `TimedOut`, or `Failed`; record success/failure reachability after each remote outcome; aggregate counts; order node results by normalized node identity for deterministic callers/tests; and return zero targets successfully unless `RequireAtLeastOneTarget` is true. | Yes | 2026-08-05 |
| TASK-027 | Create `tests/Common.UnitTests/Utilities/Broadcasting/BroadcastServiceTests.cs`. Verify disabled typed Result behavior and zero dependency calls, invalid input, serialization and payload limits, registry failure outer Results, inactive sender, forbidden scopes, target snapshot immutability, multi-scope node deduplication, local self-dispatch without transport, mixed remote outcomes, bounded concurrency, caller cancellation, timeout/expiry deadlines, zero-target behavior, deterministic ordering, concurrent independent publications, and reachability updates. | Yes | 2026-08-05 |

Completion criteria:

- **GATE-010**: Every selected node has exactly one result and every non-self node receives at most one transport call.
- **GATE-011**: One slow or failed node does not prevent attempts to other nodes, and active remote attempts never exceed the configured limit.
- **GATE-012**: The sender's own delivery uses the receiver/local queue path and never invokes the transport.

### Implementation Phase 5

- GOAL-005: Add the shared Entity Framework registry with repository-standard context and entity conventions.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-028 | Add an explicit `src/Common.Utilities/Common.Utilities.csproj` project reference to `src/Infrastructure.EntityFramework/Infrastructure.EntityFramework.csproj`. Do not add a NuGet package or new project. | Yes | 2026-08-05 |
| TASK-029 | Create `src/Infrastructure.EntityFramework/Broadcasting/IBroadcastingContext.cs` with the two exact `DbSet` properties from REQ-035, full XML documentation, and an application `DbContext` example. | Yes | 2026-08-05 |
| TASK-030 | Create `src/Infrastructure.EntityFramework/Broadcasting/Entities/BroadcastNodeRegistrationEntity.cs`. Apply `[Table("__Broadcasting_NodeRegistrations")]`, a unique normalized-identity index, active/lease and failure-state indexes, required/length annotations, navigation to scopes, `[ConcurrencyCheck] Guid ConcurrencyVersion`, and `AdvanceConcurrencyVersion()`. Use maximum lengths: identity/normalized identity 256, address 2048, protocol 32, and last failure 4000. | Yes | 2026-08-05 |
| TASK-031 | Create `src/Infrastructure.EntityFramework/Broadcasting/Entities/BroadcastNodeScopeEntity.cs`. Apply `[Table("__Broadcasting_NodeScopes")]`, `[PrimaryKey(nameof(NodeRegistrationId), nameof(NormalizedScope))]`, a normalized-scope/registration lookup index, required/length annotations of 256 for display and normalized scope, and a required foreign-key navigation to the registration. Rely on conventions for cascade deletion. | Yes | 2026-08-05 |
| TASK-032 | Create `src/Infrastructure.EntityFramework/Broadcasting/EntityFrameworkBroadcastRegistryStore.cs` with generic `EntityFrameworkBroadcastRegistryStore<TContext> where TContext : DbContext, IBroadcastingContext`. Implement the complete store contract with `IServiceScopeFactory`, `AsNoTracking` projections for reads, atomic registration plus scope replacement, reachability diagnostics, threshold deactivation, reactivation, lease renewal/expiry, unregister, diagnostics, and manual removal. | Yes | 2026-08-05 |
| TASK-033 | Implement one bounded reload-and-retry for registration and reachability writes that lose a `DbUpdateConcurrencyException` or encounter a duplicate normalized identity. Advance `ConcurrencyVersion` on every update and preserve cancellation. Do not add delivery retries or retain contexts across operations. | Yes | 2026-08-05 |
| TASK-034 | Create `src/Infrastructure.EntityFramework/Broadcasting/ServiceCollectionExtensions.cs` with `WithEntityFrameworkRegistry<TContext>()`. Validate the context constraint, mark the provider selection explicit in `BroadcastingRegistrationState`, replace only the implicit in-memory descriptor, register the generic provider once, and make repeated selection of the same `TContext` idempotent while rejecting a different explicit context/provider. Registration may compose while Broadcasting is disabled, but no disabled runtime service may resolve a `TContext` or access its database. | Yes | 2026-08-05 |
| TASK-035 | Create `tests/Infrastructure.UnitTests/EntityFramework/Broadcasting/BroadcastingEntityModelTests.cs`, `EntityFrameworkBroadcastRegistryStoreTests.cs`, and `EntityFrameworkBroadcastingRegistrationTests.cs`. Verify exact tables, keys, indexes, lengths, concurrency token, relationship/cascade, context contract, operation-owned scopes, provider replacement/conflicts, disabled host startup without context/database resolution, CRUD/snapshot semantics, scope normalization, reachability thresholds, reactivation, lease expiry, and concurrency retry. | Yes | 2026-08-05 |
| TASK-036 | Create `tests/Infrastructure.IntegrationTests/EntityFramework/Broadcasting/EntityFrameworkBroadcastRegistryStoreTestsBase.cs` plus SQLite, SQL Server, and PostgreSQL subclasses and a shared fixture following `EntityFramework/Jobs`. Use `EnsureCreated` in test databases, verify both table names, cross-provider registration/scope queries, atomic scope replacement, competing updates, lease expiry, and shared snapshots from independent service scopes. Do not create or commit an application migration. | Yes | 2026-08-05 |

Completion criteria:

- **GATE-013**: EF model tests prove the two entities require no Broadcasting-specific `OnModelCreating` call.
- **GATE-014**: SQLite, SQL Server, and PostgreSQL provider contract tests return identical observable registry semantics.
- **GATE-015**: Singleton broadcast services never retain or concurrently share a scoped `DbContext`.
- **GATE-016**: EF tables contain registration and scope state only.

### Implementation Phase 6

- GOAL-006: Add the enablement-aware ASP.NET Core HTTP receiver, built-in shared-secret authentication, authentication extension point, address resolver chain, and direct transport.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-037 | Create `src/Presentation.Web/Broadcasting/BroadcastingHttpOptions.cs` and `BroadcastingHttpOptionsBuilder.cs`. Expose `AdvertisedAddress`, `ReceiverRoute`, and built-in `SharedSecret(string secret = null)` configuration, default the route to `/_bdk/api/broadcasting` and the authentication header to `X-Bdk-Broadcast-Key`, allow only HTTP/HTTPS, and validate normalized route/address values. Canonicalize null to `string.Empty`, preserve every non-null value exactly without trimming, and hold it only in the shared in-memory HTTP options; empty, whitespace-only, and control-character values remain valid because header transport uses Base64. Derive the endpoint request-body limit as `16_384 + (4 * ((MaximumPayloadBytes + 2) / 3))` bytes to bound envelope metadata plus Base64 JSON payload; use core `BroadcastingOptions.DeliveryTimeout` for HTTP requests. Repeated builder calls mutate one shared HTTP options instance. | Yes | 2026-08-05 |
| TASK-038 | Create `src/Presentation.Web/Broadcasting/Authentication/IBroadcastHttpAuthentication.cs`, `AllowAllBroadcastHttpAuthentication.cs`, and `SharedSecretBroadcastHttpAuthentication.cs`. The authentication interface defines outbound `ApplyAsync(HttpRequestMessage, CancellationToken)` and inbound `AuthenticateAsync(HttpContext, CancellationToken)` without requiring envelope/body metadata, allowing authentication before body reads; the default adds nothing and succeeds. The built-in implementation reads the exact configured secret from `BroadcastingHttpOptions`, writes `Convert.ToBase64String(Encoding.UTF8.GetBytes(secret))` to `X-Bdk-Broadcast-Key`, requires at most one inbound value, decodes valid Base64, compares decoded bytes with `CryptographicOperations.FixedTimeEquals`, and treats a missing or empty header as empty bytes. It never stores the secret outside options or emits raw/encoded secret values to logs, metrics, diagnostics, envelopes, or Results. `BroadcastingHttpOptionsBuilder.SharedSecret(...)` selects this implementation; retain `WithHttpAuthentication<TAuthentication>()` for bearer, certificate, or platform extensions, replace only the allow-all default, make repeat selection idempotent, and reject conflicting explicit implementations. | Yes | 2026-08-05 |
| TASK-039 | Create `src/Presentation.Web/Broadcasting/Addressing/ConfiguredBroadcastNodeAddressResolver.cs`, `KestrelBroadcastNodeAddressResolver.cs`, and `BroadcastNodeAddressResolverChain.cs`. Enforce precedence explicit -> ordered custom -> Kestrel; read Kestrel addresses through `IServer`/`IServerAddressesFeature`; reject wildcard hosts and shared invalid schemes; append the configured receiver route exactly once. Add a fluent method for `TryAddEnumerable` custom resolvers with explicit order. | Yes | 2026-08-05 |
| TASK-040 | Create `src/Presentation.Web/Broadcasting/Transport/HttpBroadcastTransport.cs`. Register a named HttpClient through `IHttpClientFactory`; POST the envelope as JSON to the target's exact receiver URI; propagate the broadcast ID and application correlation ID using the middleware-compatible `CorrelationId` header; invoke `IBroadcastHttpAuthentication.ApplyAsync` before sending so built-in shared-secret mode adds `X-Bdk-Broadcast-Key`; use response-headers-read mode; bound cancellation to caller, request timeout, and expiry; deserialize the structured response; and map HTTP/network failures without logging payloads or credentials. | Yes | 2026-08-05 |
| TASK-041 | Create `src/Presentation.Web/Broadcasting/BroadcastingEndpointsOptions.cs` and `BroadcastingEndpoints.cs` derived from existing endpoint abstractions. Do not map the receiver when the shared Broadcasting options are disabled. When enabled, map one POST route with endpoint-specific `AllowAnonymous()` metadata so application default/fallback OAuth or bearer authorization does not run for this route; enforce the dedicated broadcast authentication before reading/deserializing the payload, enforce request size, parse only the fixed envelope DTO, call `IBroadcastReceiver`, and return structured response bodies for all receiver outcomes with appropriate 200/400/401/403/413 status behavior. | Yes | 2026-08-05 |
| TASK-042 | Create `src/Presentation.Web/Broadcasting/ServiceCollectionExtensions.cs` with `WithHttpTransport(Action<BroadcastingHttpOptionsBuilder> configure = null)`. Reuse one shared HTTP options instance, apply callbacks in call order, mark the transport explicit, replace `LocalOnlyBroadcastTransport`, register the named HttpClient, address resolver chain, authentication default/built-in selection, and `BroadcastingEndpoints` exactly once, and remain idempotent across repeated calls. Register the shell while using the final shared enabled value to gate endpoint mapping and runtime work. Do not call or reconfigure ASP.NET Core `AddAuthentication`/`AddAuthorization`, default schemes, bearer handlers, or application policies. | Yes | 2026-08-05 |
| TASK-043 | Create `tests/Presentation.UnitTests/Web/Broadcasting/BroadcastingHttpRegistrationTests.cs`, `BroadcastNodeAddressResolverTests.cs`, `HttpBroadcastTransportTests.cs`, and `BroadcastingEndpointsTests.cs`. Verify repeated registration, transport conflicts, enabled route mapping once, disabled route absence, explicit/custom/Kestrel precedence, wildcard rejection, route appending, timeout and network mapping, correlation headers, response mapping, authentication-before-body-read/deserialization, allow-all defaults, exact Base64 shared-secret outbound header, null/empty/missing equivalence, exact whitespace/control-character preservation, malformed/multiple/mismatching inbound rejection, fixed-time comparison paths, malformed/oversized/expired/unsupported/duplicate/queue-full outcomes, and absence of raw or encoded credential logging. Host endpoints under a fallback bearer policy and prove that matching shared-secret-only broadcast requests succeed, valid-bearer/wrong-secret requests fail, and protected non-broadcast endpoints still require bearer authentication. | Yes | 2026-08-05 |
| TASK-044 | Create `tests/Presentation.UnitTests/Web/Broadcasting/BroadcastingTwoNodeTests.cs` using two real Kestrel hosts with distinct dynamic addresses and one shared test implementation of `IBroadcastRegistryStore`. Register distinct node identities, the same scopes, and the same built-in shared secret from test configuration; enable a fallback bearer policy without supplying bearer credentials to broadcast transport; publish from node A; and assert node A accepts locally, node B accepts through shared-secret-only HTTP, each handler starts once, the result contains two nodes, and node A's address receives no self-request. Add a disabled-host case proving no receiver route or registry registration exists. | Yes | 2026-08-05 |

Completion criteria:

- **GATE-017**: The receiver is mapped once under the configured `/_bdk/api/*` route when enabled even if multiple modules register Broadcasting, and is not mapped when disabled.
- **GATE-018**: Built-in shared-secret authentication and custom authentication, body-size, envelope, scope, type, expiry, duplicate, and queue checks all occur before handler execution; null/empty/whitespace matching is deterministic and raw/encoded secret values are absent from observable output.
- **GATE-019**: A two-node Kestrel test proves shared-secret-only remote HTTP delivery and local self-delivery in one publication while host fallback bearer authorization remains enabled.
- **GATE-020**: Wildcard or unresolved shared-store addresses prevent node registration with a clear diagnostic.

### Implementation Phase 7

- GOAL-007: Add safe observability, runtime diagnostics, and failure visibility across all providers and transports.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-045 | Create `src/Common.Utilities/Broadcasting/Diagnostics/BroadcastingMetrics.cs` using optional `IMetricsService`. Define stable series for registration, broadcast, targets, delivery outcome, delivery duration, expiry, duplicate, unsupported, queue rejection, handler failure, and stale removal. Normalize only scope/type/outcome dimensions and exclude identity/address. | Yes | 2026-08-05 |
| TASK-046 | Create `src/Common.Utilities/Broadcasting/Diagnostics/BroadcastingTypedLogger.cs` using `[LoggerMessage]` methods for registration lifecycle, registry failures, publication start/completion, node outcome, receiver rejection, duplicate, queue rejection, handler failure, lease expiry, and stale removal. Use `Constants.LogKey` and safe structured fields only. | Yes | 2026-08-05 |
| TASK-047 | Instrument `BroadcastService`, `BroadcastReceiver`, `BroadcastLocalDispatcher`, lifecycle/lease services, `InMemoryBroadcastRegistryStore`, and `BroadcastingDiagnostics` with the metrics/logger helpers. Never pass payloads, credentials, authentication headers, or full addresses to telemetry. | Yes | 2026-08-05 |
| TASK-048 | Instrument `EntityFrameworkBroadcastRegistryStore<TContext>`, `HttpBroadcastTransport`, address resolution, authentication rejection, and `BroadcastingEndpoints`. Record provider/transport outcomes without high-cardinality metric dimensions. | Yes | 2026-08-05 |
| TASK-049 | Create `tests/Common.UnitTests/Utilities/Broadcasting/BroadcastingTelemetryTests.cs` and extend Infrastructure/Presentation broadcasting tests with recording metrics and log providers. Assert required series/events and assert that sentinel payload, secret, authorization header, and full endpoint URI values are absent from messages, state, scopes, and metric series. | Yes | 2026-08-05 |

Completion criteria:

- **GATE-021**: Every required operational event has a deterministic metric or structured log.
- **GATE-022**: Automated tests prove payload and credential sentinels never appear in telemetry.
- **GATE-023**: Node identity and endpoint addresses are absent from metric dimensions.

### Implementation Phase 8

- GOAL-008: Document registration, provider setup, semantics, and consumer integration, then complete repository-wide verification.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-050 | Update only `docs/common-utilities.md` with typed publish/handler examples, repeated additive `AddBroadcasting` composition, `Enabled(builder.Environment.IsDevelopment())`, disabled publication semantics, in-memory usage, defaults, Result/outcome semantics, duplicate/idempotency guidance, and non-goals. | Yes | 2026-08-05 |
| TASK-051 | Document `IBroadcastingContext`, both `DbSet` properties, fluent `WithEntityFrameworkRegistry<TContext>()`, application-owned migration instructions, exact table names, and direct-address requirements in `docs/common-utilities.md`. | Yes | 2026-08-05 |
| TASK-052 | Document `WithHttpTransport`, built-in `SharedSecret(configuration["Broadcasting:SharedSecret"])`, null/empty/whitespace semantics, Base64 `X-Bdk-Broadcast-Key` transport encoding, receiver mapping through `MapEndpoints`, endpoint-specific `AllowAnonymous` isolation from application OAuth/bearer fallback policies, disabled route behavior, address resolution precedence, invalid load-balanced addresses, custom authentication guidance, HTTPS guidance, secret-management guidance, and production warnings for allow-all or empty-secret configurations in `docs/common-utilities.md`. | Yes | 2026-08-05 |
| TASK-053 | Add a concise usage section to `docs/specs/spec-common-utilities-broadcasting.md` only if implementation signatures differ from its conceptual example; do not weaken or rewrite resolved product requirements during execution. | Yes (not required) | 2026-08-05 |
| TASK-054 | Run targeted formatting for the new Broadcasting files using the repository formatting task or `dotnet format` with explicit file/folder scope. Confirm no unrelated formatting changes. | Yes | 2026-08-05 |
| TASK-055 | Run `dotnet build` from the repository root, then run Common, Infrastructure, and Presentation unit tests sequentially, then run the Broadcasting EF integration tests sequentially for available providers. Do not run top-level build/test processes in parallel. | Yes | 2026-08-05 |
| TASK-056 | Inspect `git diff --check`, `git status --short`, public XML documentation warnings, package locks, and the final project dependency graph. Confirm no migration, new project, message table, or payload log is present; retain the approved correlation-ID lifecycle and outbound-propagation changes that Broadcasting uses. | Yes | 2026-08-05 |
| TASK-057 | Integrate an example host as a single-node Development setup using its application `DbContext` as the Entity Framework registry, the HTTP transport with an empty configuration-backed development secret, and no enabled runtime outside Development. | Yes | 2026-08-05 |
| TASK-058 | Add a Broadcasting plugin to the existing DevKit dashboard with a refreshable registrations/detail view, active/inactive summaries, and a navigation/index card backed by `IBroadcastingDiagnostics`. | Yes | 2026-08-05 |
| TASK-059 | Register a built-in no-op `BroadcastProbe` handler and add an authorized dashboard POST action that publishes it to the default scope through `IBroadcastService`, then reports aggregate and per-node immediate outcomes without arbitrary type or payload input. | Yes | 2026-08-05 |
| TASK-060 | Move `IDatabaseReadyService` to `Common.Abstractions`; keep its Domain implementation and health integration; make it an optional Broadcasting dependency; and automatically select the Entity Framework registry `DbContext` readiness name. | Yes | 2026-08-05 |
| TASK-061 | Make registration and publication scopes optional. Use `default` when no explicit scope is supplied, replace only the implicit fallback when the first explicit scope is contributed, and cover registration plus publication behavior with unit tests. | Yes | 2026-08-05 |
| TASK-062 | Add grouped `broadcasting list` and `broadcasting probe` Console Commands, enable them in WeatherFiesta, and align the dashboard probe, panel surfaces, summary cards, and enabled-only badge-free navigation with the established feature-page patterns. | Yes | 2026-08-05 |
| TASK-063 | Classify `broadcasting_*` instruments in the shared process-local metrics snapshot, show `Published` and accurately labelled `Accepted locally` dashboard counters, and align Broadcasting Console Command tables with the established minimal border style. | Yes | 2026-08-05 |

Completion criteria:

- **GATE-024**: Documentation contains complete single-process and authenticated multi-node registration examples, including environment-aware enablement, disabled behavior, empty development secrets, and isolation from application OAuth/bearer policies.
- **GATE-025**: Repository build and all targeted unit tests pass.
- **GATE-026**: Available SQLite, SQL Server, and PostgreSQL Broadcasting integration suites pass; unavailable external providers are reported explicitly rather than silently skipped.
- **GATE-027**: Final diff contains Broadcasting implementation, required project references/tests/docs, and the approved correlation-ID lifecycle and outbound-propagation support used by Broadcasting.

## 3. Alternatives

- **ALT-001**: Use DevKit Messaging or Queueing. Rejected because those features provide durable or competing-consumer semantics and do not guarantee live delivery to every node.
- **ALT-002**: Poll a database table for pending broadcast messages. Rejected because idle polling traffic is a primary problem the feature must avoid.
- **ALT-003**: Create one registry/runtime per `AddBroadcasting` call or consuming feature. Rejected because embedded modules must compose one shared host registration, receiver, publisher, and provider.
- **ALT-004**: Store scope collections as JSON on the node row. Rejected because normalized scope lookup and portable indexing are required across supported EF providers.
- **ALT-005**: Advertise a load-balanced application URL. Rejected because repeated requests may reach the same process and miss other nodes.
- **ALT-006**: Permit several handlers per broadcast type. Rejected because per-node acceptance, queue capacity, and ordering require one effective handler; that handler may fan out internally.
- **ALT-007**: Persist duplicate IDs or handler completion. Rejected because broadcasts are short-lived and the core explicitly owns no delivery or execution history.
- **ALT-008**: Create a separate Broadcasting project or package. Rejected because the specification fixes placement inside existing Common.Utilities, Infrastructure.EntityFramework, and Presentation.Web projects.
- **ALT-009**: Use gRPC, raw TCP, named pipes, or Unix-domain sockets as the standard transport. Rejected because ASP.NET Core hosts already provide HTTP routing, security, timeouts, diagnostics, and cross-platform networking.
- **ALT-010**: Publish shared-secret authentication only as sample code or require an application-defined secret-provider type. Rejected because shared-secret HTTP authentication is a standard DevKit capability and should be configurable directly from normal application configuration.

## 4. Dependencies

- **DEP-001**: `src/Common.Utilities/Common.Utilities.csproj` supplies Result types, options, serialization, metrics, logging, hosting abstractions, and the core implementation.
- **DEP-002**: `src/Common.Serialization/SystemTextJsonSerializer.cs` supplies the default payload serializer.
- **DEP-003**: `src/Common.Utilities/Metrics/MetricsService.cs` and `Metrics.cs` supply optional telemetry.
- **DEP-004**: `src/Common.Utilities/Hosting/PeriodicBackgroundService.cs` supplies lease-service scheduling.
- **DEP-005**: `src/Infrastructure.EntityFramework/Infrastructure.EntityFramework.csproj` supplies EF Core and gains an explicit Common.Utilities project reference.
- **DEP-006**: The consuming application supplies a registered `DbContext` implementing `IBroadcastingContext` and owns its migration.
- **DEP-007**: `src/Presentation.Web/Presentation.Web.csproj` supplies ASP.NET Core, `IHttpClientFactory`, Kestrel server features, and endpoint abstractions through its framework reference.
- **DEP-008**: Applications using the receiver call the existing `app.MapEndpoints()` flow.
- **DEP-009**: Unit tests use xUnit, Shouldly, NSubstitute, `FakeTimeProvider`, TestServer, and the existing Kestrel WebApplicationFactory helper.
- **DEP-010**: Cross-provider EF integration tests use the existing SQL Server, PostgreSQL, and SQLite test environment patterns; external containers must be available for their respective suites.

## 5. Files

- **FILE-001**: `src/Common.Utilities/Broadcasting/BroadcastingOptions.cs` and `BroadcastingOptionsBuilder.cs` — shared configuration and validation.
- **FILE-002**: `src/Common.Utilities/Broadcasting/Models/BroadcastModels.cs` — envelopes, registrations, publication options, outcomes, results, and diagnostics.
- **FILE-003**: `src/Common.Utilities/Broadcasting/Abstractions/BroadcastingAbstractions.cs` — public transport-neutral contracts.
- **FILE-004**: `src/Common.Utilities/Broadcasting/BroadcastErrors.cs` — typed Result errors.
- **FILE-005**: `src/Common.Utilities/Broadcasting/Registration/*` — re-entrant builder, shared state, handlers, and DI registration.
- **FILE-006**: `src/Common.Utilities/Broadcasting/Registry/*` — normalization, identity, in-memory registry, and diagnostics.
- **FILE-007**: `src/Common.Utilities/Broadcasting/Hosting/*` — node lifecycle and optional lease maintenance.
- **FILE-008**: `src/Common.Utilities/Broadcasting/Dispatch/*` — catalog, duplicate tracker, receiver, queues, and hosted execution.
- **FILE-009**: `src/Common.Utilities/Broadcasting/Transport/LocalOnlyBroadcastTransport.cs` and `BroadcastService.cs` — publication and fallback transport.
- **FILE-010**: `src/Common.Utilities/Broadcasting/Diagnostics/*` — metrics and source-generated logs.
- **FILE-011**: `src/Infrastructure.EntityFramework/Infrastructure.EntityFramework.csproj` — explicit Common.Utilities project reference.
- **FILE-012**: `src/Infrastructure.EntityFramework/Broadcasting/IBroadcastingContext.cs` — application DbContext capability contract.
- **FILE-013**: `src/Infrastructure.EntityFramework/Broadcasting/Entities/*` — normalized node and scope entities.
- **FILE-014**: `src/Infrastructure.EntityFramework/Broadcasting/EntityFrameworkBroadcastRegistryStore.cs` — shared EF registry provider.
- **FILE-015**: `src/Infrastructure.EntityFramework/Broadcasting/ServiceCollectionExtensions.cs` — fluent EF provider registration.
- **FILE-016**: `src/Presentation.Web/Broadcasting/BroadcastingHttpOptions.cs`, `BroadcastingHttpOptionsBuilder.cs`, and `ServiceCollectionExtensions.cs` — shared HTTP configuration and fluent registration.
- **FILE-017**: `src/Presentation.Web/Broadcasting/Authentication/*` — outbound/inbound authentication abstraction, allow-all default, and built-in shared-secret implementation.
- **FILE-018**: `src/Presentation.Web/Broadcasting/Addressing/*` — configured, custom, and Kestrel address resolution.
- **FILE-019**: `src/Presentation.Web/Broadcasting/Transport/HttpBroadcastTransport.cs` — direct remote delivery.
- **FILE-020**: `src/Presentation.Web/Broadcasting/BroadcastingEndpoints.cs` and `BroadcastingEndpointsOptions.cs` — internal receiver endpoint.
- **FILE-021**: `tests/Common.UnitTests/Utilities/Broadcasting/*` — core contracts, registration, registry, lifecycle, dispatch, publication, and telemetry tests.
- **FILE-022**: `tests/Infrastructure.UnitTests/EntityFramework/Broadcasting/*` — EF model/provider/registration tests.
- **FILE-023**: `tests/Infrastructure.IntegrationTests/EntityFramework/Broadcasting/*` — SQLite, SQL Server, and PostgreSQL provider contract tests.
- **FILE-024**: `tests/Presentation.UnitTests/Web/Broadcasting/*` — HTTP registration, address, transport, endpoint, security, and two-node tests.
- **FILE-025**: `docs/common-utilities.md` — canonical developer documentation.
- **FILE-026**: `docs/specs/spec-common-utilities-broadcasting.md` — authoritative specification, changed only for implementation-signature alignment when required.
- **FILE-027**: Example host `DbContext`, startup, and development settings — single-node Development integration using the EF registry.
- **FILE-028**: `src/Presentation.Web/Broadcasting/Dashboard/*` — authorized dashboard endpoints, page provider, registrations view, and built-in probe action.

## 6. Testing

- **TEST-001**: Options and fluent registration tests cover every default, invalid value, environment-aware enable/disable state, repeated call, additive scope, idempotent registration, and conflict.
- **TEST-002**: In-memory provider contract tests cover registration, lookup, diagnostics, failure thresholds, leases, reactivation, and removal.
- **TEST-003**: Lifecycle tests prove one enabled host registration, scope union, post-start address resolution, graceful unregister, shared-store startup failure, and zero registry/address activity when disabled.
- **TEST-004**: Duplicate tracker tests cover capacity, retention, concurrent reservation, commit, release, and fake-time expiry.
- **TEST-005**: Receiver/dispatcher tests cover safe type lookup, validation order, queue bounds, service scopes, ordering, concurrency, duplicates, and handler failure.
- **TEST-006**: Publisher tests cover the disabled typed Result with zero dependency calls, snapshots, scope authorization, node deduplication, local self-delivery, remote fan-out, deadlines, mixed results, registry failure, and cancellation.
- **TEST-007**: EF metadata tests verify exact table/key/index/length/concurrency/relationship mappings without feature-specific model-builder calls.
- **TEST-008**: EF provider tests verify operation-owned contexts, atomic scope replacement, competing updates, reachability, lease expiry, reactivation, and cleanup.
- **TEST-009**: SQLite, SQL Server, and PostgreSQL integration contracts verify shared observable registry behavior.
- **TEST-010**: Address resolver tests verify precedence, direct addresses, schemes, wildcard rejection, route normalization, and unresolved failure.
- **TEST-011**: HTTP transport tests verify one direct request, exact Base64 shared-secret header application for null/empty/whitespace/non-empty values, the middleware-compatible `CorrelationId` header, separation from `TraceId`, serialization, timeout/expiry cancellation, response mapping, and network failure mapping.
- **TEST-012**: Endpoint tests verify enabled/disabled route mapping, null/empty/whitespace/mismatch shared-secret behavior, malformed and multiple-header rejection, authentication-before-body-read/deserialization, fallback bearer-policy bypass only on the broadcast route, unchanged protection of other endpoints, body bounds, scope/type/expiry/duplicate/queue outcomes, and route registration once.
- **TEST-013**: Two-node Kestrel tests verify one local and one shared-secret-only remote accepted delivery from a fixed shared registry snapshot while a fallback bearer policy is active, plus disabled-host route and registration absence.
- **TEST-014**: Telemetry tests verify required low-cardinality series/logs and the absence of payload, credential, identity/address metric dimensions, and full URI values.
- **TEST-015**: Repository verification runs build, unit tests, available integration tests, formatting checks, and diff checks sequentially.
- **TEST-016**: Dashboard tests verify registration- and enabled-state navigation gating without a badge, node summaries without a runtime card, process-local `Published` and `Accepted locally` metric cards, endpoint mapping, default-scope probe publication, compact header placement, JSON action feedback, and use of the built-in probe payload.
- **TEST-017**: Console Command tests verify idempotent registration, node diagnostics output, built-in probe publication, default-scope behavior, and the established minimal table style.
- **TEST-018**: Metrics snapshot tests verify that Broadcasting counters and duration histograms are classified under the `broadcasting` feature.

## 7. Risks & Assumptions

- **RISK-001**: A platform may expose only a shared frontend address. Mitigation: fail shared-store registration unless an explicit/custom resolver returns a process-specific address.
- **RISK-002**: The default hostname-plus-process-ID identity can collide in unusual environments sharing a database. Mitigation: normalize and uniquely index identity and document the custom identity provider requirement for those environments.
- **RISK-003**: Concurrent delivery results can race while updating reachability diagnostics. Mitigation: use optimistic concurrency with one bounded reload/retry and treat diagnostics as lightweight state.
- **RISK-004**: A sender can receive an HTTP response while the accepted handler later fails. Mitigation: preserve the explicit Accepted-versus-completed distinction in APIs, logs, tests, and documentation.
- **RISK-005**: Authentication remains allow-all by default when HTTP transport is enabled. Mitigation: provide built-in shared-secret authentication, emit a production warning for an enabled HTTP runtime using allow-all authentication, document HTTPS/secret requirements, and support disabling the complete feature through environment-aware configuration.
- **RISK-006**: JSON envelope overhead is larger than raw payload size. Mitigation: enforce the 64 KB raw payload limit and a separately calculated bounded request-body limit that includes envelope overhead.
- **RISK-007**: External SQL Server or PostgreSQL integration environments may be unavailable locally. Mitigation: always run SQLite; report unavailable external suites and run them in CI where containers exist.
- **RISK-008**: An empty shared secret intentionally provides no meaningful caller authentication. Mitigation: support it for frictionless development, document that limitation, and emit a production warning when an enabled HTTP runtime selects built-in shared-secret authentication with an empty value.
- **RISK-009**: `AllowAnonymous` metadata could be mistaken for an unauthenticated receiver. Mitigation: apply it only to isolate the broadcast route from application fallback policies, run dedicated transport authentication before reading the body, and prove route isolation plus other-endpoint protection in tests.
- **ASSUMPTION-001**: Singular “target scope” wording in parts of the specification represents the complete target-scope set; implementation models it as `IReadOnlyCollection<string>` to satisfy explicit multi-scope behavior.
- **ASSUMPTION-002**: Scope and node identity comparisons are case-insensitive and provider-independent through stored normalized keys while display values preserve the first configured casing.
- **ASSUMPTION-003**: The first `AddBroadcasting` call registers an implicit in-memory/local-only fallback; later explicit EF/HTTP selections replace only those fallbacks for the shared host runtime.
- **ASSUMPTION-004**: `app.MapEndpoints()` remains the required existing host step; `WithHttpTransport` registers the receiver endpoint service but does not mutate the built application pipeline.
- **ASSUMPTION-005**: The Broadcasting Razor dashboard is a consumer of the provider-neutral diagnostics and publishing contracts and inherits the existing dashboard endpoint-group authorization.
- **ASSUMPTION-006**: The fixed EF contract/entity names in the specification are public and must not be renamed during execution.
- **ASSUMPTION-007**: Calling `AddBroadcasting` opts the one shared runtime in with `Enabled = true`; the latest explicit `Enabled(...)` call controls the host-wide runtime so application host configuration can override earlier embedded-feature registration.
- **ASSUMPTION-008**: A null shared-secret configuration value and an empty string are intentionally equivalent. Non-null values, including whitespace and control characters, are compared exactly after Base64 header transport.
- **ASSUMPTION-009**: Standard ASP.NET Core default/fallback authorization is the application-auth interaction in scope. Custom middleware that independently blocks requests before endpoint execution remains the host application's responsibility.

## 8. Related Specifications / Further Reading

- [Broadcasting design specification](../docs/specs/spec-common-utilities-broadcasting.md)
- [Performance Snapshot Dashboard specification](../docs/specs/spec-performance-snapshot-dashboard.md)
- [Repository architecture](../ARCHITECTURE.md)
- [Repository agent and implementation conventions](../AGENTS.md)
- [Common Utilities documentation](../docs/common-utilities.md)
- [Jobs Entity Framework context convention](../src/Infrastructure.EntityFramework/Jobs/IJobsContext.cs)
- [Jobs Entity Framework entity convention](../src/Infrastructure.EntityFramework/Jobs/Entities/JobLeaseEntity.cs)
- [Messaging Entity Framework entity convention](../src/Infrastructure.EntityFramework/Messaging/Entities/BrokerMessage.cs)
- [Modular endpoint contract](../src/Presentation.Web/Endpoints/IEndpoints.cs)
