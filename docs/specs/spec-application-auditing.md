---
created: 2026-10-28
status: draft
---

# Design Specification: Auditing Feature (Application.Auditing)

> Capture who performed an execution, which actions ran inside it, and which entity values changed, with extensible behaviors, explicit scopes, pluggable storage, optional endpoints, and a searchable dashboard.

[TOC]

## 1. Context and purpose

Auditing answers a business investigation question: who did what, to which resources, when, through which execution, and with what result?

Business auditors, process owners, authorized business users, and support staff use the trail to establish who approved, requested, viewed, or changed something and which follow-up work resulted. A business auditor should be able to find an invoice approval, identify the approving user and reason, see that the handler requested a posting job, and follow that job's later actions and outcome.

Requests, commands, handlers, services, and jobs can all capture business actions. The same model works without HTTP, repositories, Active Entities, or ChangeHistory. Those entity-related features are optional participants, not the foundation of auditing.

The feature has its own application module, `Application.Auditing`. Devkit features participate through explicitly selected auditing behaviors and small Common contracts (Abstractions). The developer registers those behaviors on each feature's own builder, choosing which pipelines, repositories, entities, and operations participate. No corresponding integration-enablement call on the Auditing builder is required. Features do not need to reference the recorder implementation.

The design follows bITdevKit conventions for behavior composition, public contracts, storage providers, and operational tooling.

This is a proposed design. New types and fluent APIs below describe the intended public contract, not existing functionality. Existing symbols and source references appear in section 20.

### 1.1 Existing capabilities to reuse

| Existing capability                                                                  | Auditing relationship                                                                                                                                            |
| ------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AuditState` and repository or Active Entity audit-state behaviors                   | Continue setting entity creation and modification metadata. Auditing adds the execution trail.                                                                   |
| Optional ChangeHistory feature                                                       | When installed and enabled, supplies its existing property-change abilities to enrich audit actions with actual change details. Auditing works fully without it. |
| Requester and Notifier                                                               | Behaviors capture dispatch and handler boundaries without changing handler business logic.                                                                       |
| Current Jobs, Pipelines, Orchestrations, Messaging, and Queueing                     | Existing behaviors contribute business actions, dispatch relationships, or linked execution records.                                                             |
| `ICurrentUserAccessor`                                                               | Supplies an optional actor snapshot. Do not introduce a competing `ICurrentUser` service.                                                                        |
| Results, serializers, filtering, module context, activity context, and typed logging | Reuse existing conventions. Preserve Result failures as observable outcomes.                                                                                     |
| Dashboard page providers, Razor slices, and endpoint options                         | Reuse the existing dashboard and authorization model.                                                                                                            |

### 1.2 Goals

- Capture requests and their work, including successful, failed, rejected, and canceled executions.
- Record business operations, commands, queries, selected handlers, feature actions, and named code scopes in one navigable trail.
- Show jobs explicitly requested by a command or handler in that request's trail, including their subsequent linked executions.
- Support business auditing through meaningful action labels, reasons, resource references, actor identity, outcomes, and authorized searches.
- Allow every devkit feature to participate without a hard dependency on the implementation.
- Offer a small details interface for requests and entities, plus enrichers for types that cannot implement it.
- Attach available ChangeHistory deltas when its audit-evidence bridge is explicitly selected for configured source contexts/entities, subject to its source policies and Auditing's capture policy.
- Supply provider-backed in-memory and EF Core storage with a compact two- or three-table relational model and JSON for owned details.
- Deliver optional read endpoints and a dashboard with server-side search.
- Define capture limits, persistence failures, transaction evidence, and data exposure explicitly.

### 1.3 Non-goals

- Replacing ChangeHistory, entity audit fields, event sourcing, or domain events.
- Performance monitoring, SQL inspection, or technical diagnostics.
- Restoring entities from audit JSON. Restore remains a separately authorized ChangeHistory operation.
- Automatically capturing every method, HTTP body, object graph, or external side effect.
- Making a request-wide audit record atomic with arbitrary databases, remote calls, and brokers.
- Claiming lossless, tamper-proof, or exactly-once auditing from a process-local recorder and an ordinary database table.
- Normalizing every property, actor, resource, error, or metadata value into its own table.

## 2. Scope and required behavior

The complete feature includes its recorder, abstractions, behaviors, both storage providers, an explicitly selectable adapter for the optional ChangeHistory feature, query services, endpoints, dashboard, documentation, tests, and working WeatherFiesta integration for user city actions. Delivery phases organize this work; they do not defer the operational UI beyond the feature.

Public endpoints are an explicit AddAuditing builder option. The dashboard is part of the delivered feature and appears automatically when the host enables AddDashboard and Auditing is registered. Both must be implemented and tested. No Auditing-specific dashboard opt-in or policy setup is required.

| ID      | Requirement                                                                                                                                                                                                                                                                                  |
| ------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| REQ-001 | Own one root record per admitted HTTP or independent execution, with nested action occurrences.                                                                                                                                                                                              |
| REQ-002 | Once Requester's audit behaviors are explicitly selected, capture commands by default and selected business queries/handlers. Distinguish dispatch, rejection, invocation, retry attempt, and short-circuit. |
| REQ-003 | Provide join-or-start behaviors that work without HTTP and pass through when Auditing is absent or disabled.                                                                                                                                                                                 |
| REQ-004 | Provide manual nested scopes, callback helpers, independent roots, and suppression. Supply AuditScopeOptionsBuilder with derived type/member keys and labels, Business kind, and JoinOrStart defaults. |
| REQ-005 | Snapshot actors from optional `ICurrentUserAccessor` without requiring authentication or an identity provider.                                                                                                                                                                               |
| REQ-006 | Supply a fluent AuditDescriptorBuilder with derived naming and typed resource defaults, explicit business details, and registered enrichers under the shared redaction and size policies. |
| REQ-007 | Consume available ChangeHistory evidence only through an explicitly selected source-feature audit bridge. Retain source, change-set identity, property path, allowed old/new values, and commit evidence. |
| REQ-008 | Provide a replaceable storage provider and conformance suite. Supply in-memory and EF Core implementations.                                                                                                                                                                                  |
| REQ-009 | Use a compact EF storage model; two or three audit tables are acceptable. Keep extensible details and property deltas in JSON, with the logical audit document independent of table count.                                                                                                   |
| REQ-010 | Apply every search predicate in the provider before ordering, paging, counting, and aggregation.                                                                                                                                                                                             |
| REQ-011 | Supply authorized endpoints and a business-audit dashboard for actions, related jobs, optional change details, and evidence completeness.                                                                                                                                                    |
| REQ-012 | Prevent audit writes, audit queries, dashboard refresh, and maintenance from recursively auditing themselves.                                                                                                                                                                                |
| REQ-013 | Preserve business execution and original failures under the default audit-failure policy. Expose delivery loss and truncation.                                                                                                                                                               |
| REQ-014 | Separate root outcome, child failures, entity persistence evidence, and audit delivery state.                                                                                                                                                                                                |
| REQ-015 | Bound payload bytes, collection sizes, active scopes, query ranges, and optional buffers. Never store invalid partial JSON.                                                                                                                                                                  |
| REQ-016 | Retain current feature behavior APIs, layering, module isolation, and Result conventions.                                                                                                                                                                                                    |
| REQ-017 | Capture business actions for auditors and process owners through meaningful labels, reasons, actors, resources, and outcomes.                                                                                                                                                                |
| REQ-018 | Record explicitly requested jobs in the originating request and make their later execution records navigable through preserved origin/causation.                                                                                                                                             |
| REQ-019 | Keep core auditing and its audit-DbSet EF storage independent of repositories, Active Entities, and optional ChangeHistory.                                                                                                                                                                  |
| REQ-020 | Support a provider-owned in-process queue that saves finalized records asynchronously without blocking requests on database writes.                                                                                                                                                          |
| REQ-021 | Support batch/time-triggered, explicit, and graceful-shutdown flush, distinguishing queue acceptance from confirmed persistence.                                                                                                                                                             |
| REQ-022 | Capture the optional current module on roots/actions and preserve initiating module context across linked jobs without requiring module registration.                                                                                                                                        |
| REQ-023 | Capture and preserve application correlation IDs through nested actions, independent executions, optional ChangeHistory enrichment, and queued persistence.                                                                                                                                  |
| REQ-024 | Keep Auditing policy, storage, HTTP options, contributors, and public endpoints under AddAuditing. Select each source feature's audit behaviors/bridges explicitly on that feature's own builder, allowing local opt-outs without a second integration list on AddAuditing. |
| REQ-025 | Automatically expose the Auditing dashboard page through AddDashboard when Auditing is registered, inheriting global dashboard access rules without feature-specific policies.                                                                                                               |
| REQ-026 | Accept the devkit FilterModel directly on POST search, reuse its filtering/ordering/page conventions, and return the standard ResultPaged response.                                                                                                                                          |
| REQ-027 | Deliver working WeatherFiesta integration for real user city-add/create and update actions, with EF-persisted evidence, automatic dashboard discovery, and end-to-end verification.                                                                                                          |
| REQ-028 | Provide configurable age-based archive-then-remove retention for both EF and in-memory providers. Keep archived data out of normal active query paths and enforce total in-memory count/byte bounds.                                                                                         |
| REQ-029 | Support explicit Active/Archived/All search selection through the existing FilterModel API and dashboard, defaulting ordinary queries to Active without loading archive payloads.                                                                                                            |

## 3. Terminology and identity

| Term                 | Meaning                                                                                                                                           |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| Audit record         | One execution's immutable, completed document, identified by `AuditId`.                                                                           |
| Business audit trail | A request or business operation and the related actions and executions it caused, including jobs that run later. Can span multiple audit records. |
| Root scope           | Owns collection and finalization of one record. Usually HTTP middleware or an independent execution behavior.                                     |
| Audit action         | One occurrence of work, such as a handler invocation, repository operation, or manual scope.                                                      |
| Child scope          | Recording handle for an action. Closing it cannot finalize the root.                                                                              |
| Actor                | Snapshot of the user, service, or anonymous caller performing the current execution.                                                              |
| Initiating actor     | Trusted snapshot of the business user or service that initiated the trail, retained separately from a downstream job's executing actor.           |
| Resource             | Typed entity or business-resource reference with a stable ID and optional safe display text.                                                      |
| Contributor          | Adds execution context at root start or completion.                                                                                               |
| Enricher             | Supplies details about a request, entity, handler, or feature type.                                                                               |
| Change evidence      | Sanitized ChangeHistory snapshot or reference with an explicit persistence state.                                                                 |
| Provider             | Implements append, lookup, search, aggregation, facets, and configurable archive/remove retention.                                                |
| Causation            | Non-owning link to the earlier audit and action that caused an independent execution.                                                             |

An action has a unique `ActionId`, optional `ParentActionId`, stable `Key`, `Kind`, start/completion time, and capture sequence. Repeated calls produce separate occurrences and are not merged by key. Business action labels and reasons explain their purpose without requiring a reader to understand handler class names.

Use stable keys such as `customers.update`. Put customer IDs, request IDs, and attempt numbers in structured details. Type identities use full names or configured aliases, never assembly-qualified names as a deserialization instruction.

`AuditId`, `OriginAuditId`, correlation ID, flow ID, request ID, job occurrence ID, and job execution ID are separate identities. `OriginAuditId` identifies the execution that began the business trail and equals `AuditId` on its first record. `CausationAuditId` and `CausationActionId` identify the immediate initiating execution/action. Equal correlation IDs alone do not establish causation or merge records.

Linked executions form a read projection over immutable records. They do not require a request scope to stay open, a dedicated relationship table, or later mutation of the originating record. Causation links do not cascade deletion or authorize access to another record.

## 4. Architecture and dependency boundaries

### 4.1 Component ownership

| Location                                  | Responsibility                                                                                                                                                                                                        |
| ----------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Common.Abstractions/Auditing`            | Small contracts usable by Domain and feature behaviors: `IAuditContext`, scope handles, descriptors, safe values, resource references, and `IAuditDetails`. No EF, ASP.NET, or application implementation references. |
| `Application.Auditing`                    | Recorder, ambient execution management, policies, contributors, enrichment, provider contracts, in-memory provider, query services, Result errors, fluent builder, and maintenance coordination.                      |
| Participating feature packages | Provide explicitly selectable adapters for their behavior contracts. The host chooses where to register them. Resolve Common auditing contracts optionally and pass through when Auditing is unavailable or disabled. |
| `Infrastructure.EntityFramework/Auditing` | EF provider working directly with a DbContext exposing audit entities, model configuration, and provider queries. ChangeHistory exposes explicit selection of its optional audit-evidence bridge through its own builder. |
| `Presentation.Web.Auditing`               | HTTP middleware, read endpoints, DTOs, dashboard page provider, Razor slices, and HTTP capture options.                                                                                                               |
| Host and modules                          | Select provider, actor and redaction policies, behaviors, authorization policies, and exposed UI. Own EF migrations.                                                                                                  |

Common contracts do not move the feature out of `Application.Auditing`. They avoid reverse references when Common Requester or Domain repository behaviors participate. The recorder stays in Application. Domain never references Application, Infrastructure, or Presentation.

The application module has no mandatory dependency on repositories, Active Entities, Jobs, Queueing, Messaging, Orchestrations, or ChangeHistory. Integration extensions live in the package that owns the behavior/bridge and its required references; the host selects them explicitly on that feature's builder. Application query handlers use auditing abstractions rather than a concrete DbContext. The EF storage provider accesses its audit DbSets directly in Infrastructure; it requires no repository registration or domain entity base type.

### 4.2 Lifetimes and execution flow

The coordinator and in-memory provider can be singleton services. Ambient pointers are asynchronous-flow-local. Each root owns its mutable collector. A singleton must never hold one shared current-record field. Actor resolution and scoped contributors use the active execution's DI scope.

The EF provider resolves a fresh context through the configured scope or context factory for each storage operation. It never retains business entities or shares a DbContext across parallel actions. A host can use an existing context type or a dedicated audit context.

Adapters start or join scopes, add safe details, execute their next delegate, classify the observed result, and close only their own scope. The root sanitizes and freezes the document before calling the provider. Storage and query operations run under internal suppression.

The confirmed lifecycle persists one immutable logical record only when its owning root scope finalizes. Finalization can represent success, failure, cancellation, or incomplete capture. Child-scope completion does not create an independent persisted record. There are no separate persisted start/update states or in-progress audit records. In buffered mode a finalized record becomes searchable after the background writer confirms persistence.

## 5. Audit document model

### 5.1 Root fields

| Group            | Fields and rules                                                                                                                                                                                                         |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Identity         | `AuditId`, `SchemaVersion`, stable `Key`, display name, `Kind`.                                                                                                                                                          |
| Business context | Business-operation name, optional reason, process/workflow reference, and related resources, supplied through descriptors, contributors, or manual scopes.                                                               |
| Application      | Source application, optional current/entry module, per-action module, and optional initiating module on downstream work.                                                                                                 |
| Correlation      | Origin audit ID, immediate causation audit/action IDs, correlation/flow IDs, source request ID, and optional job occurrence/execution IDs.                                                                               |
| Time             | `StartedUtc` and `CompletedUtc`, with optional elapsed execution time. API datetimes use ISO 8601 UTC.                                                                                                                   |
| Actor            | Executing actor kind, authentication state, optional user ID and display name. A separate initiating-actor snapshot preserves business attribution across jobs. Tenant/client identifiers require explicit contributors. |
| Outcome          | `Succeeded`, `Failed`, `Rejected`, `Canceled`, or `Unknown`, with safe code and classification source.                                                                                                                   |
| Child summary    | Failed-action, exception, and entity-change flags, plus total and retained counts. A recovered child failure need not fail the root.                                                                                     |
| HTTP             | Optional method, route template, bounded sanitized path, status, endpoint name, and approved client metadata.                                                                                                            |
| Owned data       | Business actions, job-dispatch references, optional change evidence, safe errors, resource references, details, and capture-completeness notes.                                                                          |
| Completeness     | `Complete`, `Truncated`, or `Incomplete`, reason codes, and omitted counts. This describes captured instrumentation, not all activity in the application.                                                                |

Reading a record does not require a live identity lookup. Exclude email, roles, claims, authentication headers, and cookies by default. An absent accessor produces an anonymous or explicitly configured service actor, never an invented user ID.

### 5.2 Action fields

An action retains identity and parent, key, kind, business display name/reason, feature/module, request type, selected handler type, execution status, start time, completion time, outcome, safe errors, resources, and details. A job-dispatch action also records the requested job, accepted occurrence/dispatch identity when available, and causation metadata. Optional retry fields identify the policy and attempt when the owning feature exposes them.

`ExecutionStatus` distinguishes `Invoked`, `ShortCircuited`, `Skipped`, and `Incomplete`. A selected handler is not necessarily an invoked handler. Validation, authorization, or a cache can prevent invocation.

Sequence provides deterministic display order, not a total causal order across parallel tasks. Preserve parent IDs and causation rather than inferring hierarchy from timestamps. Start/completion times establish the chronology of business events.

### 5.3 Entity-change evidence

Each evidence object retains source store key, change-set ID, source row IDs, optional bulk-operation ID, entity type/ID, resource details, operation, capture strategy/source/status, timestamp, and associated action ID. Property entries retain path, collection action/item identity, permitted old/new values or policy markers, and value type.

`PersistenceState` is `Committed`, `RolledBack`, or `Unknown`. Only confirmed committed evidence appears in the default changed-entity and changed-property views. The confirmed policy for an unobservable transaction outcome is an `Unknown` reference with identifiers/capture notes and no old/new values, including when value exposure is requested. A successful handler result cannot promote it to committed. Rolled-back evidence also omits value snapshots by default.

Preserve ChangeHistory limitations. Summary-only bulk inserts must not become invented property deltas. Hash-only, redacted, excluded, oversized, and unavailable values differ from an actual JSON null.

### 5.4 Safe values and schema evolution

The confirmed capture policy records request/handler identity automatically. Business values require explicit projection through `IAuditDetails`, registered enrichers, contributors, or manual scopes. Whole command/query parameters and result payloads are not serialized automatically. Optional ChangeHistory remains the source of actual deltas and applies its existing value policies before Auditing can further restrict them.

Details accept bounded scalars, UTC timestamps, string arrays, and explicitly projected JSON values. Completed documents retain no live entities, requests, delegates, streams, exception objects, or principals.

Reuse the serializer abstraction with a deterministic JSON profile. The persisted format has a numeric schema version and stable names. Readers tolerate additive fields and return a typed error for incompatible versions. Reading JSON never activates application CLR types.

### 5.5 Optional current module

Capture the current business module when available. Module context is optional: auditing, manual scopes, jobs, and EF audit storage work without module registration, and missing context is stored as null rather than an invented module name.

Reuse existing optional module services and module context automatically at each supported boundary; no extra module-capture switch is required. The optional module integration can resolve request/handler/job types through `IModuleContextAccessor` and HTTP boundaries through `IRequestModuleContextAccessor`. Existing module metadata can be read when it is already established, but capturing a module must not require tracing or parsing log messages.

The root snapshots its entry `ModuleName`. If that context becomes known only when the entry request/handler is resolved, fill it once from that boundary. Each action captures the module actually current for its own work, so a request spanning modules retains those distinctions without repeatedly overwriting the root's entry module.

A dispatched job preserves `InitiatingModuleName` separately from its own current `ModuleName`. A job running in another module is still linked to the original request. Search and dashboard views expose current/action modules and the initiating module where available, under the same access policies as other business context.

Module values are resolved and copied during capture, before the record enters an asynchronous persistence queue. A writer's own module context must never replace the business module.

### 5.6 Correlation ID

Capture the application correlation ID explicitly on every audit root and action. Reuse `CorrelationId.Current` and its existing validation/propagation conventions. Do not replace it with an audit ID or another identifier.

When a root begins without a valid current correlation ID, establish one through the existing `CorrelationIdGenerator` and a bounded correlation scope for that execution. Restore the prior context when the owner closes. Existing valid IDs are preserved exactly; nested actions inherit them without generating new values.

If application code deliberately establishes another valid correlation scope, capture that value on its action while preserving the root's original value. Correlation IDs are references, not authentication or proof of causation.

Job dispatch, notification, message, and queue adapters propagate the current correlation ID through their existing server-owned metadata conventions. Downstream executions capture it alongside origin and causation IDs. An explicit supported dispatch override is recorded rather than silently replacing the request's correlation.

Snapshot correlation before asynchronous persistence. The provider writer never derives a correlation ID from its own ambient context. List/detail views, exact searches, related-work navigation, and optional ChangeHistory enrichment retain the captured correlation ID.

## 6. Public scopes and object details

### 6.1 Common contracts

These signatures describe the proposed contract. Supporting types follow section 5. Production public symbols require XML documentation and usage examples.

```csharp
public interface IAuditContext
{
	IAuditScope Current { get; }
	IAuditScope BeginScope(AuditScopeOptions options);
	IAuditScope BeginScope(Action<AuditScopeOptionsBuilder> configure);
	IDisposable Suppress();
	Task<T> RunAsync<T>(
		AuditScopeOptions options,
		Func<IAuditScope, CancellationToken, Task<T>> action,
		CancellationToken cancellationToken = default);
	Task<T> RunAsync<T>(
		Action<AuditScopeOptionsBuilder> configure,
		Func<IAuditScope, CancellationToken, Task<T>> action,
		CancellationToken cancellationToken = default);
}

public interface IAuditScope : IDisposable, IAsyncDisposable
{
	Guid AuditId { get; }
	Guid? ActionId { get; }
	bool IsRecording { get; }
	void SetDetail(string name, AuditValue value);
	void AddResource(AuditResourceReference resource);
	void Complete(AuditOutcome outcome = AuditOutcome.Succeeded);
	void Fail(Exception exception);
}

public interface IAuditDetails
{
	AuditDescriptor DescribeAudit();
}
```

`AuditDescriptor` supplies a stable key, business display text, optional reason, resources, and explicit details. Implementing `IAuditDetails` enriches enabled capture. It does not enable capture, authorize a read, bypass redaction, or serialize the object.

The existing `Domain.Model.IAuditable` remains the entity `AuditState` contract. The new Common `IAuditDetails` contract is separate and does not change that marker.

Entities and requests can implement the same Common interface. Existing types can use registered `IAuditDetailsProvider<T>` enrichers instead. Conventions supply defaults, object details add approved values, then registered enrichers add approved values. A later value replaces an earlier duplicate within that object scope. Reserved identity, actor, outcome, and correlation fields cannot be replaced through details.

Contributor start/completion hooks add execution context. They cannot save the record or retain scoped services after finalization. Contributor failures produce safe capture notes without causing business work to run twice.

#### 6.1.1 Fluent descriptor builder and defaults

Provide `AuditDescriptorBuilder` beside the Common descriptor contracts. `For<T>()` and its runtime `For(Type)` overload establish the described type without taking an object instance, resolving services, or enabling capture. A useful descriptor needs no property initializer or explicit options:

```csharp
public AuditDescriptor DescribeAudit() =>
	AuditDescriptorBuilder.For<RenameCustomerCommand>().Build();
```

Add explicit business details while keeping the derived key/display name:

```csharp
public AuditDescriptor DescribeAudit() => AuditDescriptorBuilder
	.For<RenameCustomerCommand>()
	.Reason(this.Reason)
	.Resource<Customer>(this.CustomerId)
	.Build();
```

Optional `.Key("customers.rename").DisplayName("Customer name change")` calls override the derived names when the application needs its own business vocabulary.

| Builder setting | Default and override behavior |
| --- | --- |
| Key | The described type's full name, falling back to its simple name, without an assembly-qualified name or per-instance identifiers. `Key(...)` supplies a stable business key. |
| Display name | A readable type name, removing conventional Command/Query/Notification suffixes and separating words; `RenameCustomerCommand` becomes Rename customer. `DisplayName(...)` supplies an explicit label. |
| Reason | Absent until supplied with `Reason(...)`. Null/empty optional text leaves it absent. |
| Resources | Empty. `Resource<TResource>(id, displayName: ...)` derives the resource type; `Resource(type, id, displayName: ...)` supports explicit/external types. Repeated calls add references, and display text is optional. IDs use the stable ID conversion, never whole-entity serialization. |
| Details | Empty. `Detail(name, AuditValue)` adds approved values, with the same per-object duplicate-key rules as other enrichment. |
| Build | Validates structural values and returns an immutable descriptor with copied collections. Empty optional collections are valid. Repeated builds do not share mutable lists with callers. |

Omitting optional methods retains the defaults. Explicit key/resource overrides must be structurally valid; display text can fall back to its default when omitted. A descriptor built with defaults behaves the same as one with those values explicitly supplied.

The builder does not infer actor, correlation, outcome, commit evidence, or parameter/result payloads. Descriptors still pass through the normal redaction/size/exposure policies when applied to a recording scope. The builder cannot bypass exclusions or make an omitted feature behavior participate. Object-initializer construction can remain supported for compatibility, but public documentation and examples use the fluent builder.

#### 6.1.2 Shared naming conventions and derived defaults

Use one naming convention for descriptors, scope options, and explicitly selected feature adapters. Capture boundaries already know their request/handler/entity types, feature operation, route template, or job identity; callers should not repeat names that can be derived from that context.

| Context | Default stable key | Default display name |
| --- | --- | --- |
| Request/notification | Qualified request/notification type name. | Readable type name with conventional Command/Query/Notification suffixes removed. |
| Handler | Qualified handler type name. Retain its associated request identity separately. | The request's business label when available; otherwise the readable handler type with composite suffixes such as CommandHandler removed. |
| Repository/entity operation | Qualified entity type plus the observed operation. | Readable entity type and operation. Entity IDs remain resource values rather than part of the action key. |
| HTTP root | HTTP method plus normalized route template; use the existing bounded fallback when no template exists. | The endpoint display name when supplied, otherwise the readable method/route. |
| Job or named feature execution | The existing stable job/definition/feature key plus its operation where needed. | The feature's existing display name, otherwise its readable key/type. |
| Typed manual scope | Qualified type name, optionally followed by an operation/member name. | Readable type name; with an operation, use a readable type/operation label such as Customer: Rename. |
| Typed resource | Configured resource-type alias, otherwise the qualified resource type name. | Readable resource type; callers can optionally supply an approved resource display value. |

`For<T>()` supplies type context. The scope builder's instance `For<T>(operation)`/`For(Type, operation)` and standalone `Create<T>(operation)`/`Create(Type, operation)` factories accept an operation name; prefer `nameof(...)` when it identifies an existing member. The factory combines type and member identity into the key and derives the label. Readable labels remove conventional Async suffixes from operation names; keys retain the supplied member identity. This avoids stack inspection, source paths, or request-payload serialization.

Provide typed resource conveniences on `AuditDescriptorBuilder` and `IAuditScope`: `Resource<TResource>(id, displayName: ...)` and the Common extension `AddResource<TResource>(id, displayName: ...)`. Both derive the resource type through the same convention, use the normal stable ID conversion, and keep the ID explicit. String-based resource overloads remain available for external business resources.

Explicit `Key(...)`, `DisplayName(...)`, or resource-type overrides take precedence. Naming convention/type aliases can be replaced through Auditing configuration for application-wide business vocabulary. Builders use the shared default convention without requiring DI; the capture layer can apply a configured convention to derived values while preserving explicit overrides. Keep that distinction in capture metadata rather than guessing whether a supplied string was intentional.

Resolve names when an action/scope is admitted and freeze those resolved values with the document. Queued writers and detail readers never derive them again from another ambient context or newer aliases. Naming changes affect new capture, not historical records.

Derived keys are deterministic type/operation identities, never timestamps, correlation IDs, user IDs, or per-entity IDs. Applications can supply stable business aliases when a key must survive type/member renames. Reason, business decisions, actor attribution, outcome, and actual change values still come from their explicit sources; naming conventions do not invent them.

### 6.2 Manual scopes

#### 6.2.1 Fluent scope-options builder and defaults

Provide `AuditScopeOptionsBuilder` beside the Common scope contracts. Prefer the configuration-callback overloads on `BeginScope(...)` and `RunAsync(...)`: they supply the builder and call Build internally. Its instance `For<T>(operation)`/`For(Type, operation)` methods establish type/member context; `Key(...)`, `DisplayName(...)`, `Kind(...)`, and `Mode(...)` remain fluent overrides.

Standalone factory overloads remain available for reusable options. `Create(key)` supplies an explicit key; `Create<T>(operation)` and `Create(Type, operation)` establish the same context as the callback's instance For methods. Parameterless `Create()` returns an unconfigured builder for composition, requiring type context or a valid key before Build:

```csharp
var options = AuditScopeOptionsBuilder
	.Create("customers.rename.apply")
	.Build();

var typeOptions = AuditScopeOptionsBuilder
	.Create<RenameCustomerCommandHandler>()
	.Build();
```

The standalone runtime-type factory and callback instance For overload support the same derived naming. An optional operation argument supplies a more specific scope key and label:

```csharp
var renameOptions = AuditScopeOptionsBuilder
	.Create<Customer>(nameof(Customer.Rename))
	.Build();
```

Fluent `Key(...)`, `DisplayName(...)`, `Kind(...)`, and `Mode(...)` remain optional overrides. The derived rename scope already has a stable type/member key, the label Customer: Rename, Business kind, and JoinOrStart mode.

| Builder setting | Default and override behavior |
| --- | --- |
| Key | `Create(key)` or `Key(...)` supplies a stable explicit key. Instance `For<T>()` and standalone `Create<T>()` derive the qualified type identity; an operation appends its member identity. A valid key is required, but typed/callback configuration need not provide a literal key. |
| Display name | Readable type/operation context when supplied, otherwise a readable form of the final key. `DisplayName(...)` overrides it; omitted/null/empty text retains the derived default. |
| Kind | `Business`. `Kind(...)` can select Service or another valid custom kind without requiring a new integration. |
| Mode | `AuditScopeMode.JoinOrStart`: join a live root or own a new root when none exists. `Mode(AuditScopeMode.RequiresNew)` explicitly requests an independent record under the existing causation rules. |
| Build | Validate key/kind/mode and return an immutable options snapshot. A built instance is unaffected by subsequent builder changes. No scope is opened and no record is emitted. |

Default values and validation are shared by the options type and builder. Minimal, partially configured, and explicit-default builds produce equivalent effective options. Business values and resources can be added through the returned scope's `SetDetail(...)` and `AddResource(...)`; building options does not project an object or infer those values.

Actor, correlation, current module, parentage, and IDs come from the recorder when the scope opens. The builder does not set a successful outcome, bypass suppression, or enable omitted feature behaviors. Manual handles still require explicit completion/failure; callback helpers still classify their delegate's actual result. Existing object-initializer construction can remain supported, but public examples use the fluent builder.

The configuration-callback overload is the preferred direct call:

```csharp
await using var renameScope = auditing?.BeginScope(s => s
	.For<Customer>(nameof(Customer.Rename)));
```

When capture is eligible, the context creates a fresh builder, invokes this synchronous configuration callback once, builds/validates its options once, and delegates to the same scope-opening path as prebuilt options. Callers neither construct the builder nor call Build. The callback configures metadata only; it does not execute the business operation. The configuration callback is required and must provide a type or valid key. It can use a normal statement body or fluent expression because the returned builder is not needed by the Action callback.

Disabled/suppressed capture returns a no-op scope without invoking the configuration callback. Optional `auditing?.BeginScope(...)` also skips the call when the context is absent. Never retain the callback/builder in the collector or persistence queue. Configuration failures use the existing audit-failure policy; default ReportOnly must preserve business execution and report the capture failure. The RunAsync overload still invokes its business delegate exactly once when capture is unavailable or configuration fails under ReportOnly.

A configuration callback does not imply successful completion of a manual handle. Call Complete/Fail explicitly or use RunAsync for result/exception classification. Prebuilt options remain useful when the same configuration is reused; both overloads have identical naming, validation, nesting, and suppression semantics.

#### 6.2.2 Callback and manual-handle examples

A handler or service can add an action within an HTTP execution, or own a record when called independently:

```csharp
var result = await auditing.RunAsync(
	s => s
		.For<Customer>(nameof(pricing.RecalculateAsync))
		.Kind("Service"),
	async (scope, token) =>
	{
		scope.AddResource<Customer>(customerId);
		scope.SetDetail("pricing.mode", AuditValue.From("annual"));
		return await pricing.RecalculateAsync(customerId, token);
	},
	cancellationToken);
```

The helper calls the delegate exactly once per invocation, returns its result unchanged, and preserves exceptions and cancellation. It classifies `IResult` without serializing the result payload. Other result types use registered classifiers. A plain returned value counts as success by default.

Manual handles support code where the business operation runs outside RunAsync; their configuration can still use the fluent callback:

```csharp
await using var scope = auditing.BeginScope(s => s
	.For<Customer>(nameof(notifications.NotifyAsync))
	.Kind("Service"));
try
{
	await notifications.NotifyAsync(customerId, cancellationToken);
	scope.Complete();
}
catch (Exception exception)
{
	scope.Fail(exception);
	throw;
}
```

Disposal records duration and restores the prior ambient frame. The confirmed manual-scope contract records `Unknown` and `Incomplete` when a handle is disposed without an explicit outcome. It never infers success or failure from disposal alone. A using statement cannot detect an exception from its body. Prefer callback helpers for automatic result/exception classification, or explicitly call `Complete(...)`/`Fail(...)`.

Async root disposal awaits provider acceptance. For buffered EF persistence this is queue admission, not database persistence or flush. For inline persistence it awaits the write. Synchronous root disposal freezes and restores context, then reports that async finalization was required under inline delivery. It must not silently pretend the record was persisted. Synchronous child disposal is supported because it performs no storage I/O. Document this constraint beside `BeginScope`.

### 6.3 Nesting and independent work

`AuditScopeMode.JoinOrStart` is the default. An active root produces a child under the current action. Otherwise the scope owns a root. `RequiresNew` owns a distinct record and can retain a causation link to the earlier scope.

Parallel branches share a thread-safe root collector but have separate ambient stack frames. Each child captures its parent ID at creation. Double disposal is harmless. Out-of-order closure produces a capture note without corrupting other branches.

Awaited in-process work can join the current execution. Work dispatched to a worker owns its execution scope even when a caller waits for it. Broker deliveries, scheduled jobs, detached notifications, and resumed orchestration slices clear inherited ambient state at explicit execution boundaries. They receive approved origin/causation metadata and initiating-actor context, not a live scope, principal, or DbContext.

A command's request to start a job is an action in that command's trail. Clearing ambient state for the worker does not remove this relationship. The job's independent execution record links back to the dispatch action and is discoverable from the originating request as specified in section 8.1.

Closed roots reject late writes and never reopen. Closing with active children marks those actions incomplete and releases capture state. Auditing does not wait indefinitely or cancel business work.

### 6.4 Suppression and unavailable services

`Suppress()` disables recording for the current flow and descendants until disposal. Nested behaviors cannot create replacement roots. Nested suppression restores correctly.

HTTP exclusion establishes suppression for the whole request, including inner feature behaviors. Independent worker boundaries end the prior execution's ambient state and apply their own configured policy. Inbound payloads cannot bypass capture or redaction policy.

The host explicitly registers selected auditing behaviors through each feature's builder, independently of whether `AddAuditing` has already been called. Merely enabling a source feature never registers an audit behavior. Behaviors resolve `IAuditContext` optionally at execution time. When absent, they call the next delegate directly. A disabled recorder returns no-op scopes with `IsRecording = false`. Neither path serializes details or registers a hidden provider. Applications can explicitly register a no-op implementation when directly injecting the contract without enabling Auditing.

## 7. HTTP, request, and handler capture

### 7.1 HTTP boundary

The HTTP adapter owns the root for each eligible request. Commands and business mutations are captured by default after request blacklisting. Read-only requests, including GET endpoints and POST-based reads, require explicit selection as meaningful business reads; routine city-list refreshes and polling are not captured automatically. Use the same request-path blacklist contract, matching behavior, and defaults as `RequestLoggingMiddleware` and `RequestLoggingOptions.PathBlackListPatterns`. Capture decisions apply once at entry and flow to children.

The proposed HTTP options expose `PathBlackListPatterns`, initialized to the following exact defaults:

```csharp
[
	"/*.js", "/*.css", "/*.map", "/*.html", "/swagger*", "/favicon.ico", "/_framework*", "/_vs*", "/health*",
	"/notificationhub*", "/_content*", "/signalrhub*", "/_bdk/*"
]
```

Obtain the path as request logging does: use `IHttpRequestFeature.Path`, falling back to `HttpRequest.Path.ToString()` when empty. Blacklisting does not use the query string, raw URL, or route template. Call the shared `MatchAny(PathBlackListPatterns)` helper with its default case-insensitive behavior. Its `*` wildcard can span path separators and matching covers the whole path; do not introduce separate glob, `**`, regular-expression, or route-template semantics. A null or empty collection matches no request path, and null pattern entries are ignored, as in the shared helper.

`WithHttpRequests()` uses these defaults without requiring the host to repeat them. Its options overload can replace `PathBlackListPatterns`, including with an empty collection; custom lists replace rather than silently append to the defaults. Keep the built-in default collection shared with request logging so future changes cannot drift, while each feature receives its own configurable copy. Reusing this blacklist requires no logging middleware registration or Serilog dependency, and changing one feature's configured list does not mutate the other's list.

A matched request establishes the execution-wide suppression described in section 6.4, including nested feature behaviors and manual scopes. Consequently, `/_bdk/*` excludes dashboard requests by default. Hosts that expose business mutations beneath that prefix must adjust their Auditing blacklist explicitly; WeatherFiesta's required proof does so. Internal suppression of Auditing's own reads, writes, refreshes, and maintenance remains in force even when that path pattern is removed.

Root metadata contains method, route template when available, endpoint name, sanitized path, status, duration, and safe correlation. Route templates form stable keys; raw resource IDs stay in approved resource fields. Query strings, bodies, headers, IP addresses, and user-agent strings require explicit capture policy. Bodies are disabled in the built-in default.

Middleware ordering must observe the authenticated actor, authorization denials, handled exceptions, and final status. The documented host recipe is routing, authentication, Auditing, exception handling, authorization, then endpoint execution. The audit middleware wraps the exception handler so it observes the resulting status. The exception integration supplies a safe error feature when an exception is handled downstream. If the host uses another order, document the evidence available rather than inferring an exception from status alone.

Enrich identity again after downstream processing when authentication was unavailable at entry. Do not overwrite a captured authenticated initiating actor with a later incidental principal. Login/logout or impersonation changes require explicit actor-transition events.

Unhandled exceptions are classified and rethrown unchanged. An observed request cancellation is `Canceled`; arbitrary `OperationCanceledException` instances are classified against their token/source. A timeout remains a failure with a timeout code. HTTP 401/403 or a known authorization rejection is `Rejected`. A 5xx status is `Failed` even when no exception escaped. Store lower-level Result outcomes on their actions even when an endpoint maps them to a different status.

HTTP response completion can occur before audit persistence. Finalization captures the observed status and submits the frozen record to the provider even if the client disconnected. With buffered EF persistence, this means queue admission rather than a database wait or an implicit flush. Do not claim that the status proves client receipt or change an already-started response after an audit failure.

Optional `X-Audit-Id` response headers expose an opaque identifier only when configured. Knowing the ID never authorizes a record lookup.

### 7.2 Requester and handler boundaries

Provide separate `AuditingRequestBehavior<TRequest,TResponse>` and `AuditingRequestHandlerBehavior<TRequest,TResponse>` using Requester's existing `IsHandlerSpecific()` distinction. The host explicitly selects each through Requester's `WithBehavior(...)` mechanism. Dispatch and handler capture can be selected independently; omitting one omits only that boundary.

The outer request behavior records logical dispatch once, outside validation, authorization, caching, retries, timeouts, and transaction behaviors. It captures request identity, command/query classification when known, dispatch outcome, and selected handler information. The handler behavior surrounds actual invocation inside policies that can short-circuit, and inside the retry boundary when per-attempt invocation is exposed.

One logical request can therefore contain one dispatch action and several invoked handler actions. A validation failure records a rejected dispatch with no invoked handler action. A cache hit records a short-circuited dispatch with no fictitious handler invocation. Retries retain failed attempts and the final attempt while the dispatch reports the final outcome.

Do not assume all Requester requests are commands. Support configured type selectors/aliases and a request-kind resolver for command, query, or generic request. No marker interface is mandatory for basic capture.

The confirmed default captures commands and explicitly selected business reads. Apply selection at the behavior boundary before creating a read action/root, and align HTTP read admission so an unselected polling request does not leave an empty transport-only audit. An ordinary unselected read is a local eligibility skip, not global suppression: selected nested commands or explicit manual scopes can still record. Explicit route/feature exclusions continue to suppress the whole excluded execution.

### 7.3 Supported request integration

Built-in command/query auditing targets current Requester and its handler behavior pipeline. Legacy Application.Commands, Application.Queries, and Application.JobScheduling do not participate in this feature. Current Application.Jobs dispatch and execution remain supported.

### 7.4 Outcomes and error details

Capture failed `Result` values, not only thrown exceptions. Store approved error codes/categories and safe text, never entire result payloads. A recovered nested failure remains visible through `HasFailedActions` while the root can succeed.

The same propagating exception can cross multiple actions. Store one bounded exception description with action references rather than repeating its full stack at every level. Different exceptions with equal text are still separate occurrences. Stack traces and inner-exception text are disabled unless explicitly approved and sanitized.

## 8. Devkit feature participation

Every behavior follows one shared protocol:

1. If Auditing is absent, disabled, suppressed, or ineligible, invoke the next delegate directly.
2. At an independent execution boundary, isolate inherited state and apply the new execution's policy.
3. Join a live root with a child action, or own a root when none exists.
4. Add bounded feature identity and safe details, invoke the business delegate once, and classify its actual outcome.
5. Close only the action or root the behavior owns.

Existing feature retry policies can invoke their next delegate repeatedly. Auditing does not add retries to business execution. Repeated handler attempts receive distinct action IDs and explicit attempt relationships.

| Boundary                                | Captured information                                                                                                                         | Ownership                                                                                                                                |
| --------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Requester dispatch and handlers         | Request kind/type/ID, selected/invoked handler, rejection, cache outcome, attempts.                                                          | Join HTTP/manual root or own a request root.                                                                                             |
| Notifier dispatch and awaited handlers  | Notification type, dispatch, each handler, per-handler outcomes.                                                                             | Nested actions; parallel awaited handlers retain separate parent frames.                                                                 |
| Detached notification handler           | Notification and handler identity, approved causation.                                                                                       | Independent root.                                                                                                                        |
| Current Jobs initiation                 | Requested job, business reason, acceptance/rejection, dispatch/occurrence ID, initiating actor.                                              | Dispatch/scheduling behavior adds an action beneath the requesting command/handler.                                                      |
| Current Jobs execution                  | Job, occurrence/execution ID, attempt, executing actor, initiating actor, outcome, origin and immediate cause.                               | Worker execution owns a linked root. Direct in-process execution can join when it has no worker boundary.                                |
| Pipelines and steps                     | Pipeline identity, each exposed step, branching/skip outcomes.                                                                               | One run action/root with child steps.                                                                                                    |
| Orchestrations                          | Instance, definition, execution slice, action, retry/resume metadata.                                                                        | Each worker slice owns a root; correlation links slices without keeping one scope open across waits.                                     |
| Messaging and Queueing                  | Publish/enqueue, message identity, destination, delivery and handler attempt.                                                                | Publishing joins the caller; consumption owns a root. No message body by default.                                                        |
| Repositories                            | Rough entity actions such as created, updated, deleted, or read, with entity type/ID, safe counts, and outcome. No inferred property deltas. | Optional behavior joins or owns a root. Read capture requires explicit business-read selection.                                          |
| Active Entities                         | Rough entity action and outcome across its operation lifecycle. No inferred property deltas.                                                 | Optional behavior joins or owns a root; avoid duplicate reports of the same underlying action when repository adapters also participate. |
| Entity bulk insertion                   | Operation ID, counts, policy, summary versus detailed change capture.                                                                        | Join or own. Preserve bulk limits.                                                                                                       |
| Storage clients                         | Provider/client, logical operation, safe resource key, counts, result.                                                                       | Optional existing client behaviors. Never capture file/blob bytes or secrets.                                                            |
| Other features and application services | Stable kind/key, safe details, resources, outcome.                                                                                           | Custom behavior, registered adapter, or manual scope.                                                                                    |

Provide first-party adapters for the boundaries above that already have behavior extension points. When a feature lacks a suitable boundary, add a small hook at that feature's operation executor. Do not require dynamic proxies or interception of arbitrary services.

Each supported feature provides auditing behaviors or operation hooks that the host explicitly registers through that feature's own builder. This applies to Requester, Notifier, current Jobs, repositories, Active Entities, pipelines, orchestrations, messaging, queueing, bulk operations, and supported storage clients. Feature registration alone never installs an audit behavior. ChangeHistory exposes an explicit audit-evidence bridge registration on its own builder. New features provide the same small optional-contract adapters; the Auditing builder does not maintain a central list of feature opt-ins.

For example, Requester uses explicit `WithBehavior(typeof(AuditingRequestBehavior<,>))` and `WithBehavior(typeof(AuditingRequestHandlerBehavior<,>))` calls. An individual repository uses `WithBehavior<RepositoryAuditingBehavior<TEntity>>()`. Register that decorator only on the repositories that need rough entity-action capture. Per-type/operation selectors further narrow an enabled behavior. Do not add these behaviors to a feature's default list.

Participation requires the source feature, an explicitly registered auditing behavior/bridge at that boundary, and an enabled Auditing recorder. Registering Auditing or a source feature alone does not install audit behaviors into other pipelines. Selection, suppression, redaction, and read-eligibility rules still apply. Omitting a behavior is a local opt-out: it does not suppress a surrounding HTTP/manual scope or another explicitly selected boundary. An unaudited repository can therefore run inside an audited command without producing a rough repository action.

Explicit behavior registration must work before or after `AddAuditing`. It must not inspect whether Auditing is already registered, build a temporary service provider, or treat an assembly's presence as an opt-in. Resolve the optional Common contract during execution and use the source feature's own configuration. Registering the same audit behavior/bridge repeatedly is idempotent and preserves the ordering in section 7. Other decorators or business behaviors must not remove that explicit selection.

Cross-process adapters propagate origin, causation, and the approved initiating-actor snapshot through their trusted server-owned metadata fields. Consumers resolve the executing actor independently. Arbitrary inbound payloads cannot assert authenticated identity, initiating user, or audit access scope. Adapters validate the provenance of attribution metadata before preserving it.


### 8.1 Jobs explicitly started by a request

Job initiation and job execution are separate business audit actions. Capturing only the worker's execution is insufficient when a user command or handler explicitly requests that job.

Provide explicitly selectable auditing adapters around job dispatch/scheduling APIs as well as worker execution. The host selects both through Jobs' own registration to obtain the complete request-to-job trail. For Jobs, this includes `IJobSchedulerService.DispatchAsync` and `DispatchAndWaitAsync`. Where these APIs have no dispatch behavior hook, add one at the dispatch boundary. A worker-only `IJobBehavior` cannot capture a rejected start request or the caller's intent.

The originating command/handler records a `JobDispatch` action containing the business job name, optional reason/resources, start-request time, and accepted/rejected/failed outcome. When supplied by the scheduler, retain the accepted occurrence/dispatch identifier. Acceptance means the scheduler accepted the request, not that the job ran or succeeded.

Before a job can be accepted or begin executing, its server-owned occurrence metadata receives `OriginAuditId`, `CausationAuditId`, `CausationActionId`, the current correlation ID, optional initiating module, and permitted initiating-actor snapshot. These IDs are assigned while the request is active, even though its audit record has not yet been finalized or persisted. Carry it with that occurrence so delayed execution, retries, node changes, and job chains preserve the connection. Never modify a reusable job definition with one request's actor or audit IDs. An independent timer occurrence is not attributed to whichever user last configured its schedule.

A worker starts its own execution record with the same origin and a link to the dispatch action. Record the executing service/user separately from the original initiating business user. Retain job occurrence ID, execution ID, and attempt. Each worker retry is visible without changing the request's original dispatch outcome.

For example, a user approves an invoice and the handler requests a posting job. The request's trail shows the approval and accepted posting request. Later, the same trail view shows the linked posting execution, its business actions, optional changed values, and final outcome. The original request can have succeeded even when the posting job later fails.

The trail view assembles linked records through origin/causation queries. It does not append the job's future outcome into the finalized request document. It also works when an audit record is persisted after another linked record. A missing or not-yet-persisted worker record is shown as unavailable execution evidence, not proof that the job never ran.

A direct in-process job invocation can contribute nested actions when it shares the execution boundary. Dispatching to a worker and waiting for completion retains the dispatch plus linked worker record. Waiting alone does not turn worker execution into an inherited request scope. If the wait times out, distinguish that caller outcome from the job's eventual result.

### 8.2 Optional rough entity-action behaviors

Repository and Active Entity adapters capture actions such as entity created, updated, deleted, or read, including entity identity, business label, and operation outcome. They do not calculate property diffs or require ChangeHistory.

These features are optional participants alongside handlers, services, jobs, and manual scopes. The host explicitly registers auditing behaviors for the selected repositories or Active Entity configurations; other registrations remain unaudited at that boundary. The audit recorder and EF audit storage continue to work when no entity feature is configured. When both adapters observe the same underlying action, invocation identity prevents duplicate business evidence.

A successful entity operation is the observed action outcome. Do not infer an enclosing transaction's commit or an exact value change from it. Optional ChangeHistory enrichment adds those details when its own capture and commit evidence are available.

### 8.3 HTTP exclusions and independent scheduled executions

A request-path blacklist excludes one HTTP execution and all work that remains inside it. Selected Requester/repository behaviors and manual scopes are no-ops for that execution and cannot open replacement roots, including RequiresNew. This is stronger than omitting a feature behavior, which skips only its own boundary.

A timer-triggered scheduled job is a separate execution without an incoming HTTP path. It applies its own capture policy even when it invokes exactly the commands or repositories used by a blacklisted endpoint. At a real worker boundary, isolate inherited audit/suppression state from the originating execution without mutating that execution's collector. Isolation itself does not create an audit record or implicitly register a capture behavior.

| Execution | Audit ownership and subsequent calls |
| --- | --- |
| Blacklisted HTTP request invokes commands/repositories directly | Suppression flows to every nested call; no Requester, repository, or manual replacement root starts. |
| Timer-triggered job with its execution auditing behavior explicitly selected | The job owns a root. Selected Requester/repository behaviors and manual scopes join it as nested actions. |
| Timer-triggered job without its own execution auditing behavior | The first selected Requester/repository behavior or explicit manual scope can own a root. The omitted job boundary contributes no job action/root of its own. |
| Job explicitly excluded as a whole execution | Execution-level suppression applies to its descendants too. Merely omitting the job audit behavior or locally skipping one action is not such an exclusion. |
| Blacklisted request dispatches a job to an independent worker | The request produces no audit dispatch record. The worker applies its own policy; retain independently propagated correlation where available, but do not invent an originating audit/action reference. |
| Job calls the blacklisted endpoint over HTTP | That server request and its descendants remain suppressed. The calling job's own audit is a separate execution and can remain eligible. |

An independent timer occurrence uses its actual executing service/user attribution. It does not inherit the identity of whoever last configured the schedule. Capture current module/correlation when available, establishing a correlation at a new admitted root under the existing convention when needed. Trusted causation/initiating-user metadata applies only when a real originating execution supplied it.

## 9. Optional ChangeHistory integration

### 9.1 Responsibilities and explicit participation

ChangeHistory is an optional devkit feature. Source capture and audit enrichment are separate selections. The proposed `AddChangeHistory(...).WithAuditing<TSourceContext>()` explicitly registers its audit-evidence bridge on the ChangeHistory builder. The already configured source contexts/entities remain authoritative; the host does not repeat a mandatory entity list on the Auditing builder. The bridge accepts optional entity/operation selectors when only some source evidence should enter the trail.

Only configured source capture plus an explicitly selected bridge and an enabled Auditing recorder contributes change evidence. Source ChangeHistory still captures and persists under its own policy when its audit bridge is omitted. Core auditing, its EF provider, manual scopes, and request/job capture work without ChangeHistory.

Source ChangeHistory contexts are independent of the audit-storage DbContext. Consume evidence from each configured source through its own bridge; an audit provider using `AppDbContext` does not restrict evidence to ChangeHistory rows in that context. Contexts without enabled ChangeHistory or an explicitly selected audit bridge supply no deltas to Auditing. Selecting a bridge never enables source history tracking for additional entities.

Repository and Active Entity behaviors can report rough entity actions without ChangeHistory. When available, ChangeHistory augments those actions with actual property-change details using its own capture abilities. Auditing alone adds no entity snapshot reads, ChangeTracker scans, or history rows.

ChangeHistory remains responsible for comparison, direct-mutation strategies, create/delete/restore semantics, collection/owned-path identity, bulk behavior, redaction, and transactional history persistence. The adapter consumes its captured evidence. It does not calculate a second diff.

The confirmed default, when optional ChangeHistory contributes evidence, is `Snapshot`: embed bounded, policy-filtered committed deltas together with source IDs. Preserve source exclusion, redaction, and hash-only restrictions. `ReferenceOnly` remains available and retains source references/counts without values. Embedded audit evidence remains understandable after source ChangeHistory rows expire; optional source navigation follows that feature's existing access rules and can report unavailable source history.

Do not change ChangeHistory's table layout. Optional audit/action correlation can use its existing `Properties` JSON. Source store key plus change-set and row IDs disambiguate equal IDs across module contexts.

### 9.2 Commit evidence and transaction boundaries

A successful repository return or `SaveChanges` does not prove that an enclosing transaction committed. The adapter needs capture and transaction-lifecycle hooks in ChangeHistory and the existing transaction behavior. These hooks are proposed integration work, not capabilities assumed to exist today.

| Observation                                                         | Audit behavior                                                                                |
| ------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| No explicit enclosing transaction; successful atomic save confirmed | Promote captured evidence to `Committed` after save completion.                               |
| ChangeHistory owns and successfully commits a transaction           | Promote evidence after that commit.                                                           |
| Caller owns the transaction                                         | Keep pending evidence until its owner confirms commit/rollback.                               |
| Rollback or rollback to savepoint                                   | Discard affected pending value snapshots; retain only safe rollback capture notes.            |
| Failed save or failed retry attempt                                 | Do not publish committed deltas from that attempt.                                            |
| External transaction with no observable completion                  | Retain an `Unknown` reference without old/new values; exclude it from committed-change views. |
| Root closes before transaction outcome                              | Freeze unknown evidence and a limitation capture note. Do not later mutate the record.        |

Capture associates each change set with the audit/action active when the change was made, not whichever action happens to be current during commit.

EF transaction interceptors can observe EF-managed commits. External connection transactions and `TransactionScope` need an explicit owner adapter when their completion is otherwise invisible. A `SavedChanges` callback alone is insufficient. Preserve savepoint boundaries and attempt identity so rolled-back attempts cannot leak values into a successful retry.

If business data commits and later request work fails, the audit can have a failed root and committed changes. If all entity work rolls back, a failed root persists independently with no committed-change claims.

### 9.3 Accuracy, deduplication, and limits

- Reuse the source's equality and path semantics. Same-value assignments produce no invented property changes.
- Deduplicate source evidence by store key, change-set ID, and row ID within the audit record.
- Do not merge separate committed change sets on the same entity into one misleading before/after pair.
- Preserve create, update, delete, restore, collection, owned-value, and bulk capture distinctions when supplied by ChangeHistory.
- Audit redaction can further restrict source values. It cannot recover or expose values ChangeHistory redacted, hashed, or excluded.
- Apply audit byte/count limits before storing snapshots. Retain source references and omitted counts when a delta cannot fit.
- Do not call restore or mutate entities from an audit contributor.

A source capture error follows ChangeHistory's own required/best-effort policy for the business operation. A later audit-copy failure follows Auditing's delivery policy. The selected bridge must not weaken ChangeHistory's existing guarantees.

## 10. Storage provider model

### 10.1 Provider contract

Use one DI-selected provider for writes and reads. A host can replace it with a custom implementation. Avoid a named-provider factory until a concrete need requires multiple independent audit stores.

Proposed provider operations are:

| Operation                                       | Contract                                                                                                                                           |
| ----------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AppendAsync(AuditRecord)`                      | Accept one frozen document. Distinguish queued receipt from confirmed persisted/already-present acknowledgement; otherwise return a typed failure. |
| `FlushAsync(CancellationToken)`                 | Confirm persistence for accepted records up to the call's acceptance boundary, or report failed/pending records. No-op when nothing is pending.    |
| `FindAsync(AuditId)`                            | Return one record, not-found, or a typed failure.                                                                                                  |
| `FindRelatedAsync(AuditRelatedExecutionsQuery)` | Return a bounded, access-scoped page of executions linked by origin or immediate cause, including later job executions.                            |
| `SearchAsync(AuditSearchPlan)`                  | Execute the validated FilterModel-derived selection, ordering, paging, and count; return paged audit summaries.                                    |
| `AggregateAsync(AuditAggregateQuery)`           | Return exact scoped counts and approved groupings, or an explicit timeout/limit error.                                                             |
| `FindFacetsAsync(AuditFacetQuery)`              | Return bounded, scoped suggestions for actors, keys, kinds, modules, and types.                                                                    |
| `ArchiveBeforeAsync(cutoff, batchSize)`         | Archive eligible finalized records as complete owned graphs outside the active query path.                                                         |
| `RemoveArchivedBeforeAsync(cutoff, batchSize)`  | Remove age-eligible archived records and all owned data. Automatic scheduling remains host-configured.                                             |

Operations use the existing `Result`, `Result<T>`, or `ResultPaged<T>` shapes. Providers never expose `IQueryable`, EF entities, or database JSON expressions to Application/Presentation.

Record identity is assigned before append. Duplicate in-flight ID/content reuses its queued receipt rather than consuming another queue slot. A provider returns `AlreadyPresent` only when storage presence is known. Buffered writing performs the same durable idempotency check before confirming completion. Same ID with different content returns an identity conflict at the stage where it is detected and never overwrites stored content. Retries reuse the same frozen content and ID; queue acceptance alone does not claim that a durable conflict check has completed.

Provider capabilities are explicit. Both built-ins must support all required filters, nested action/change predicates, JSON detail predicates, aggregates, facets, and deterministic paging. A custom provider cannot silently ignore unsupported criteria or apply them after paging.

### 10.2 In-memory provider

The in-memory provider stores immutable document snapshots and uses the same predicates and comparison rules as EF. Search observes a stable snapshot for one operation.

It is intended mainly for tests/local development: process-local, non-durable, and not shared between nodes. Advertise those evidence limitations on the dashboard. Enforce record-count and byte bounds across active and archived data together. Support the same configurable age-based archive/remove retention as EF, commonly with short local intervals. Capacity eviction is a separate final bound and its effect on evidence coverage is reported.

Default registration uses this provider for ease of local use. Selecting EF replaces the implicit in-memory default. Provider initialization failure never falls back to memory.

### 10.3 EF Core provider and context contract

The EF provider works directly with a host/module or dedicated DbContext that exposes the audit entity sets. Its storage contract has no repository, Active Entity, or ChangeHistory requirement.

The proposed three-table mapping exposes these sets:

```csharp
public interface IAuditingDbContext
{
	DbSet<AuditRecordEntity> AuditRecords { get; set; }
	DbSet<AuditActionEntity> AuditActions { get; set; }
	DbSet<AuditEntityChangeEntity> AuditEntityChanges { get; set; }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
	: DbContext(options), IAuditingDbContext
{
	public DbSet<AuditRecordEntity> AuditRecords { get; set; }
	public DbSet<AuditActionEntity> AuditActions { get; set; }
	public DbSet<AuditEntityChangeEntity> AuditEntityChanges { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.ConfigureAuditing();
	}
}
```

These are Infrastructure persistence entities, not business-domain entities. `AuditRecordEntity` owns the execution header, `AuditActionEntity` represents an action occurrence, and `AuditEntityChangeEntity` represents one captured entity/change-set group. The application-facing `AuditRecord` remains one immutable logical document, independent of how many tables its provider uses.

The provider's generic constraint is `DbContext, IAuditingDbContext` and storage operations use those audit DbSets directly. Exposing an audit change set does not require the ChangeHistory feature; without change-detail enrichment it can remain empty. No source ChangeHistory entities are part of this context contract.

Register through `WithEntityFrameworkProvider<TContext>()`; its options callback is optional. This selects buffered persistence with all queue, batching, retry, and shutdown defaults already populated. Calling `Buffered()` explicitly is equivalent, and its optional callback overrides only selected writer settings. Infrastructure owns mappings and context-scoped storage operations; the host owns migrations. Schema/table naming is configured before model creation.

Use a fresh context for audit appends and reads. Do not reuse the business operation's tracked context or transaction. Audit persistence remains independent so a failed or rolled-back business operation can still leave an audit record. A context used solely for audits requires no other devkit entity features.

### 10.4 Compact relational model

Storage is not constrained to one table. A compact model with two or three audit tables is acceptable. Choose the split for business searches, action identity, and ownership rather than forcing every child into JSON or normalizing every detail.

The proposed default uses three tables:

| Table                      | Row boundary                                                                                         | Main fields and owned details                                                                                                                                                                                                                                                                                                                                                                                                                        |
| -------------------------- | ---------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `__Auditing_Records`       | One execution record.                                                                                | Identity/version/content hash; origin and immediate-cause IDs; application and entry/initiating module; timestamps and outcome; executing/initiating actor identifiers; correlation/flow/request IDs; optional HTTP fields; completeness and counts; provider-owned StorageState/ArchivedUtc metadata outside the document content hash. Actor snapshots, business context, root resources/details, safe errors, and completeness notes remain JSON. |
| `__Auditing_Actions`       | One action occurrence owned by an audit record.                                                      | Audit/action/parent IDs; business key/kind/label; current module and correlation; request/handler identity; timestamps and outcome; job-dispatch identity when present. Reasons, resource references, safe error references, and extensible details remain JSON.                                                                                                                                                                                     |
| `__Auditing_EntityChanges` | One entity/change-set evidence group owned by an audit record, optionally associated with an action. | Entity type/ID; source store/change-set/bulk IDs; operation, capture source, timestamp, and persistence state. Property/path deltas, collection details, source row references, and allowed old/new values remain JSON.                                                                                                                                                                                                                              |

This keeps actions and entity-change groups directly searchable without adding a table for each property, actor, resource, exception, or metadata entry. Actual change details remain optional. The table is an audit snapshot/reference store, not a replacement for ChangeHistory's own storage.

A two-table alternative can keep records and actions separate while embedding entity-change groups as JSON owned by their record. It is acceptable if it satisfies the same business filters, ownership, and provider conformance contract. The specification does not require shipping configurable table-layout variants; the final default mapping is reviewed before implementation.

#### 10.4.1 Ownership, append, and query rules

All rows belonging to one logical record derive from the same frozen document. Append the header and its owned actions/change groups atomically in one audit-store transaction. A queued record becomes confirmed persisted only when that complete graph commits. Readers must not observe a header whose child rows have not yet committed.

Idempotency covers the whole logical document, not just the header. A retry cannot duplicate child actions/change groups or treat a partially written graph as a completed append. Stable audit/action/evidence IDs and the document content hash support that check.

Each action/change group belongs to exactly one audit record. Origin and causation references across executions remain non-owning and allow related executions to arrive in any order. They must not cascade-delete another execution or require its record to have been persisted first.

Common root, action, job, module, correlation, and entity searches use their scalar fields. Extensible details and property deltas use JSON predicates inside the provider, still before paging/counting. The logical query semantics and returned audit document do not depend on table count.

Keep indexes focused on business lookups, including actor/time, module/correlation, action identity, entity/change-set identity, and origin/immediate cause. Database-specific mapping and query translation remain Infrastructure implementation concerns.

### 10.5 Asynchronous EF persistence and flush

The EF provider can own a bounded in-process queue of finalized audit documents and a hosted background writer. Buffered EF persistence is explicitly best effort: an abrupt process crash can lose accepted documents before they are saved. This crash-loss window is an accepted design decision. Buffered mode is the EF default, including when `WithEntityFrameworkProvider<TContext>()` has no options callback, so request/handler completion does not wait for audit database writes. Inline mode remains an explicit option. The in-memory provider stores records immediately.

Finalize, sanitize, copy, and freeze the record before enqueueing it. The queue retains only the immutable audit document and provider-owned receipt metadata. It must not retain the request scope, DbContext, principal, entity instances, or scoped contributors.

`AppendAsync` performs nonblocking queue admission in buffered mode and returns `Queued` with an acceptance receipt. It returns `Persisted` or `AlreadyPresent` only when storage completion is actually confirmed. A queued record is not durable and is not visible in audit searches until saved. The provider contract makes that distinction visible to callers.

The queue and writer belong to the selected provider. Do not introduce a second Application queue in front of the EF queue. A direct call to the EF provider follows the same configured persistence path.

### 10.6 Background writing and batch flush

The background writer uses a fresh DbContext to persist accepted documents in batches. Flush a batch when the configured batch size is reached or its flush interval expires, so low traffic is saved without waiting for a full batch.

Defaults are 100 records per batch and a one-second flush interval, with bounded total queue capacity of 1,000 records or 64 MiB. Parameterless provider registration activates these settings and the background writer without requiring an explicit `Buffered(...)` call. All settings remain configurable. The writer runs independently of the originating request's cancellation token.

A batch append writes each logical record's header and owned action/change rows through the audit DbSets, retaining JSON-owned details. It confirms persistence only after the complete record graph commits and preserves per-record/child IDs and idempotency. Writer retries keep the same frozen IDs/content and use the existing resilience conventions. A batch failure must not mark unsaved documents as persisted. If some records are already present, recognize them without overwriting them.

Preserve FIFO admission as the normal processing order within a provider instance, without claiming a global order across nodes. Business-trail links remain valid when related records become visible in another order.

### 10.7 Explicit and shutdown flush

Expose `FlushAsync` through the provider abstraction so application shutdown, controlled process lifecycles, and tests can request graceful persistence without depending on the concrete EF provider. In-memory/inline providers return immediately when no accepted writes are pending.

A flush captures the acceptance boundary at the time of the call and waits for those accepted records, including any current batch or retry, to reach confirmed persistence. Records accepted afterward do not postpone that flush indefinitely. Completion is not defined as the queue merely becoming empty.

Return an `AuditFlushResult` that distinguishes confirmed records, failed records, and records still pending at cancellation/timeout. A successful flush confirms all records covered by its acceptance boundary. Repeated or concurrent flushes are safe. Cancellation stops the caller's wait, not business execution or an already-started database operation that the writer can still complete.

On graceful host shutdown, stop new business producers and coordinate finalization of active audit roots before closing queue admission. The writer continues draining during this stage. Complete admission, drain accepted records, and perform a final flush within the configured shutdown budget. The proposed default drain budget is ten seconds. Dispose writer resources and contexts only after draining completes or the budget expires. Report any remaining unpersisted records honestly. Abrupt termination cannot flush an in-process queue.

Explicit flush can be awaited when required by the caller, but ordinary request finalization does not invoke a blocking flush. Consumers needing synchronous confirmation can select inline persistence or request and await a flush deliberately.

### 10.8 Overflow, failures, and acknowledgement semantics

The confirmed default is `ReportOnly`: ordinary business actions continue when audit storage is unavailable or admission fails. Queue admission never waits for capacity by default. On capacity exhaustion, return a typed `AuditQueueFullError` to the auditing caller, preserve the original business result, and explicitly report that this audit was not accepted. Background delivery failures are also reported explicitly without failing an already completed business operation. Never silently evict an accepted but unpersisted document to admit another. An inline fallback may be an explicit host policy, but it changes the nonblocking guarantee and is not the buffered default.

The provider handles background storage failures through finite retries and a safe host error-reporting hook/log. The default writer retry budget is three retries after the initial attempt, with exponential backoff starting at 250 milliseconds and capped at two seconds. Retry settings remain configurable; retries preserve immutable IDs and the existing idempotency rules. Exhausted records produce a terminal delivery failure. A subsequent flush cannot report success for records it covers that failed delivery. No exception can be retroactively thrown into an already-completed request.

An optional `Propagate` policy can expose immediate admission or inline-write failures to an owning manual/requester operation. It cannot surface a later background failure through that completed operation, roll back committed business work, replace an existing business exception, or change a started HTTP response.

In-process buffering has an explicit crash-loss window. Accepted documents can be lost on process termination before persistence. Hosts requiring durable acknowledgement must choose an appropriate provider/mode rather than interpreting `Queued` as durable success.

Audit writes remain independent of the business transaction. Provider writes, flushes, retries, reads, and maintenance run under internal suppression. No audit-store operation creates a recursive audit record.

## 11. Search and query semantics

### 11.1 Supported filters

Public POST search uses the existing `FilterModel`. The application validates its approved business fields and criteria into an internal `AuditSearchPlan` for provider execution; that plan is not a second public filtering contract.

| Filter group          | Required criteria                                                                                                                                      |
| --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Time                  | Inclusive root-start lower bound and exclusive upper bound.                                                                                            |
| Actor                 | Executing/initiating/either actor role, actor kind, authenticated/anonymous, exact user ID, user-name search, optional authorized tenant/client scope. |
| Execution             | Audit ID, kind, stable key, application, node, module, root outcome, completeness.                                                                     |
| Storage lifecycle     | StorageState Active or Archived; an explicit selection of both supplies the All view. Removed audits are unavailable.                                  |
| HTTP                  | Method, route, status or status class, endpoint name.                                                                                                  |
| Business context      | Operation label, reason, business-process reference, and approved details.                                                                             |
| Related work          | Origin/immediate-cause audit ID, initiating action, job name, occurrence ID, execution ID, and dispatch outcome.                                       |
| Correlation           | Correlation ID, flow ID, and source request ID.                                                                                                        |
| Action                | Key, kind, feature/module, request type, handler type, execution status, outcome, safe error code.                                                     |
| Resource              | Resource type and ID, at root or within matching actions.                                                                                              |
| Entity change         | Entity type/ID, operation, property path, change-set ID, bulk-operation ID, persistence state.                                                         |
| Details               | Registered searchable detail key plus typed equality, set membership, prefix, or range operators appropriate to its type.                              |
| Evidence completeness | Has failed actions, safe errors, committed changes, truncation, or unknown transaction evidence.                                                       |

Free text searches approved display names, keys, type aliases, resource labels, and safe error text. It does not search raw old/new values, secrets, arbitrary JSON, or excluded details. Value search can be added only as a separately authorized, explicitly registered field capability.

The dashboard user selector defaults to matching either the initiating or executing actor through approved FilterModel predicates. Explicit `UserId` and `InitiatingUserId` fields select that role exactly, and the dashboard labels the choice. A downstream service job is not represented as authenticated under the initiating user.

Default root criteria combine with AND; explicit OR/grouping follows FilterModel's existing semantics. Criteria within one same-action predicate must match the same action. An entity type/ID/property filter must match the same evidence object and property entry. This prevents one handler and an unrelated action's failure from satisfying a combined handler-failure search.

Multiple action/evidence predicates can intentionally match different children, using the Filtering feature's supported collection and registered specification constructs. The UI describes those choices and uses the existing logic operators. Independent root flags, such as `HasFailedActions`, intentionally do not imply failure of the action selected by another filter.

Entity-change filters default to `Committed`. Resource references alone do not mean an entity changed. Search results state whether matching evidence was complete, reference-only, or truncated.

### 11.2 Provider execution and comparison rules

The provider first applies general host access scope and the selected Active/Archived/All dataset, then scalar/child predicates, ordering, count, and paging. It never loads a page and then removes nonmatching children in memory. Counts, facets, and aggregates use the same predicate semantics as searches.

Action keys, kinds, type aliases, and detail names use ordinal case-insensitive comparison. User IDs, tenant IDs, entity IDs, and opaque correlation IDs use exact ordinal comparison. User-name display searches use documented invariant case-insensitive rules. EF uses explicit normalized projections or collations rather than inheriting arbitrary database defaults. Provider conformance tests include mixed-case and Unicode examples.

Details must be registered as searchable with a name, type, supported operators, and exposure policy. Unknown keys or unsupported operators return validation errors. Parameterize values and resolve JSON paths from the registered field catalog. Client input cannot bypass the registered field catalog.

### 11.3 Paging and query bounds

POST search uses `FilterModel.Page`, `PageSize`, and `Orderings`, with the standard `ResultPaged<AuditRecordSummary>` response. Do not reinterpret page numbers as cursors or add an incompatible audit-only paging payload.

Normalize omitted/default paging to page 1 and size 50. Enforce a maximum page size of 200 and reject invalid values after normalization. The default search range is the last 24 hours, with a configurable maximum range of 31 days. Filtering itself imposes no application-specific size/range cap, so Auditing supplies these bounds.

Order by `StartedUtc DESC, Id DESC` when no ordering is supplied. Allowlisted caller orderings use their declared precedence and append an ID tie-breaker when needed. Execute filtering, exact count, ordering, and page selection in the provider. Return the normal total count/page metadata; a count timeout is a typed query failure, not an invented zero.

Paging is a live view of immutable completed records. Later appends can change the rows and counts seen on another page. Do not promise a snapshot across separate requests. Return an as-of time for each search result.

Simple GET searches can map their query parameters into the same FilterModel criteria/paging so they share semantics. Exact-ID lookup is independent of the search range cap. Related-execution traversal may retain its own bounded continuation contract; it is not encoded into FilterModel's page number.

Return summaries without full child payloads. Detail reads reconstruct one bounded logical document. Search predicates operate only on retained evidence; completeness notes distinguish omitted evidence from proof that an action never happened.

### 11.4 Active, archived, and combined searches

The confirmed search contract offers Active, Archived, and All through the same query service, dashboard, and FilterModel POST endpoint. Active is the default dataset. Archived evidence remains searchable with the same business fields until retention removes it.

Expose `StorageState` as a case-sensitive logical field name, with persisted values `Active` and `Archived`. This is provider lifecycle metadata, separate from business outcomes and the immutable audit content.

Use normal FilterModel criteria to select the dataset. `StorageState eq Archived` selects archived records. An approved enum-values filter containing both states expresses All. If no storage-state selection is supplied, inject the Active constraint server-side. Do not require another POST body wrapper or a separate archive search language.

For example, All can be selected with:

```json
{
  "page": 1,
  "pageSize": 50,
  "filters": [
    {
      "customType": "enumvalues",
      "customParameters": {
        "field": "StorageState",
        "values": "Active;Archived"
      }
    }
  ]
}
```

Lifecycle selection cannot widen general host data-access scope. Searches, counts, facets, aggregates, and related-work queries honor the chosen dataset consistently. All returns a logical audit ID only once, including during a concurrent archive transition.

The dashboard labels the selected dataset and each record's state. Archived detail uses the same business action/actor/resource view. Preserve an explicit historical time range when navigating archived evidence; the Active list's last-24-hours default must not be presented as evidence that no historical archives exist. Existing range/page bounds still apply.

Normal Active queries use the provider's active query path and must not materialize archived payloads. Archived or All is an explicit broader query, not an automatic background expansion of every search. Removed records remain unavailable without changing the original business facts.

## 12. Optional endpoints

### 12.1 Registration and routes

Provide `AddAuditing(...).WithEndpoints(...)` through the new web feature package, with `MapAuditingEndpoints()` for application-time route mapping. `WithEndpoints()` requires no options callback: it registers the documented read routes with default group path `/_bdk/api/auditing`, standard Result/FilterModel contracts, and the shared search limits in section 14.2. Partial endpoint overrides preserve the remaining defaults. Follow `EndpointsOptionsBase`, configurable group paths, DTOs, Result mapping, validation, and general host route policies; no new Auditing-specific policy configuration is required.

| Method and route                 | Purpose                                                                                                                                 |
| -------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /records`                   | Simple summary filters mapped to the same bounded paging/search semantics.                                                              |
| `POST /records/search`           | Existing FilterModel JSON body for criteria, orderings, and page/pageSize; returns ResultPaged audit summaries. Read-only despite POST. |
| `GET /records/{auditId}`         | One execution summary and owned actions, with value visibility applied.                                                                 |
| `GET /records/{auditId}/changes` | Change evidence with separately authorized values.                                                                                      |
| `GET /records/{auditId}/related` | Access-scoped linked executions, including jobs explicitly requested by that execution and their subsequent work.                       |
| `POST /aggregates`               | Counts and approved groupings under the same search/access scope.                                                                       |
| `GET /facets`                    | Scoped, bounded suggestions with prefix and time filters.                                                                               |
| `GET /capabilities`              | Supported fields/operators, limits, provider durability, and evidence modes.                                                            |

POST search binds `FilterModel` and maps through `IAuditQueryService` rather than opening a DbContext. Response items are audit summary DTOs in the existing `ResultPaged` shape. Dashboard handlers use the same application service and filtering semantics. Query execution is suppressed even if invoked through Requester.

There are no built-in public write, arbitrary export, restore, delete, or clear-all routes. Retention is host-controlled maintenance. New operational mutations require their own specification and permission.

### 12.2 Host access rules and response behavior

Public endpoints use the existing endpoint options and the host's general authentication/authorization configuration. This specification introduces no required Auditing-specific policy names or separate permission system. General access design can be addressed centrally later.

Dashboard pages and their internal read handlers inherit `AddDashboard` access rules. They do not require additional read/value policies, a feature-specific authorization setup, or new policy names before appearing.

Host-wide data scope, when provided by the general access layer, applies consistently to searches, lookups, related work, facets, and aggregates. Client filters can narrow a supplied scope, not widen it. Auditing does not require a new `IAuditAccessPolicy` registration.

Captured redaction and configured value-exposure settings still apply to every response. Change paths and redaction markers can be shown without old/new values. Detailed value exposure is a capture/presentation option, not a mandatory dashboard-specific policy. Links into another feature use that feature's existing access rules.

Return validation failures for invalid ranges, unsupported fields, operators, or tokens. Map not-found and provider-unavailable outcomes consistently with existing endpoint conventions. Errors contain no stored payloads or storage credentials.

### 12.3 POST search with FilterModel

`POST /records/search` accepts the existing devkit `FilterModel` directly as its JSON body, following [Filtering](../features-filtering.md). There is no separate public `AuditSearchRequest` filter grammar.

Use the existing `FromBodyFilterAsync()` parsing conventions and advertise `WithFilterSchema(isRequestBody: true)` in OpenAPI. The endpoint passes the model to `IAuditQueryService` and returns `ResultPaged<AuditRecordSummary>` through existing Result-to-HTTP mapping.

For example:

```json
{
  "page": 1,
  "pageSize": 50,
  "filters": [
    { "field": "InitiatingUserId", "operator": "eq", "value": "user-42" },
    { "field": "ModuleName", "operator": "eq", "value": "Billing", "logic": "and" },
    { "field": "CorrelationId", "operator": "eq", "value": "invoice-2026-42", "logic": "and" },
    { "field": "StartedUtc", "operator": "gte", "value": "2026-10-28T00:00:00Z", "logic": "and" },
    { "field": "StartedUtc", "operator": "lt", "value": "2026-10-29T00:00:00Z", "logic": "and" },
    {
      "field": "Actions",
      "operator": "any",
      "value": { "field": "Kind", "operator": "eq", "value": "JobDispatch" },
      "logic": "and"
    }
  ],
  "orderings": [
    { "field": "StartedUtc", "direction": "desc" },
    { "field": "Id", "direction": "desc" }
  ]
}
```

Filter fields address the approved logical audit search model, not physical table/JSON column names. Publish its property paths, including `StartedUtc`, `ModuleName`, `CorrelationId`, actor fields, `Actions`, and `EntityChanges`. Field-path segments use the Filtering feature's case-sensitive CLR names. This is separate from the documented comparison rules for field values.

Reuse supported `FilterOperator`, `FilterLogicOperator`, custom-filter types, and registered named/composite specifications. Preserve their existing semantics. Express time boundaries with standard comparisons when half-open ranges are required, as above.

Allowlist searchable fields, orderings, operators, custom filters, and named specifications before executing the provider query. Bound filter count, nesting, page size, and body size. Empty includes/hierarchy are sufficient for summary search; reject unsupported loading paths or traversal options rather than letting callers load arbitrary entity graphs. Audit reads remain no-tracking.

Same-action and same-change matching must remain explicit. Two separate `Actions any` criteria can match different actions. A multi-field predicate that must match one action/change group uses a supported nested criterion or an approved audit named specification through FilterModel's existing extension point. Publish its arguments in the filter catalog. Do not assume that putting several nested filters into the shared builder automatically evaluates all of them; the current collection path can select only its first nested criterion. Prove the supported combinations and reject unsupported shapes without silently dropping conditions.

`FilterModel` is defined in Common. Reusing it does not require an audit repository or make audit persistence entities inherit a domain entity base. Reuse shared filtering helpers where their contracts fit; provider adapters translate approved criteria into the validated audit search plan and table/JSON predicates. In-memory and EF must satisfy the same filter/paging conformance tests.

The dashboard can build the same FilterModel with `FilterModelBuilder` and use the same application query service. It does not need the public POST route enabled to reuse those semantics.

## 13. Dashboard

### 13.1 Automatic dashboard participation

Provide an `IDashboardPageProvider` and Razor-slice pages in `Presentation.Web.Auditing` using the existing dashboard discovery/registration conventions.

When Auditing is registered and the host enables `AddDashboard`, the Auditing page, navigation entry, and index card appear automatically. No separate Auditing dashboard registration, per-feature enable switch, or additional policy is required.

The global dashboard owns styling, grouping, routing, and access controls. The default Auditing page path is `/_bdk/auditing`, derived from its configured prefix. Registration order between `AddAuditing` and `AddDashboard` must not change discovery or duplicate the page.

Without `AddDashboard` there are no Auditing dashboard routes. Without Auditing registration the page does not appear. Public endpoints are independently selected by `WithEndpoints(...)` and are not required for the dashboard, which uses the application query service through its own normal dashboard handlers.

### 13.2 Search page

The list opens with the last 24 hours and provides:

- Active / Archived / All selection, defaulting to Active, plus initiating/executing user ID/name, actor kind, UTC range, business action, module, and outcome filters.
- An advanced filter builder for business reasons/resources, requested jobs and related executions, request/handler types, HTTP route/status, correlation, optional entity/property/change-set identity, persistence state, and registered details.
- Filter chips, a reset action, and a URL that preserves nonsensitive filter selections.
- A result table with time, initiating/executing actor, business action, affected resources, outcome, related-job indicators, and action/change counts.
- Clear indicators for failed children, exceptions, incomplete capture, reference-only changes, and truncated records.
- Scoped business summaries and optional counts by outcome, action, or actor, labeled with their range and as-of time.
- Standard FilterModel page-based navigation, loading/empty/error states, and manual refresh.

Auto-refresh is opt-in, pauses while viewing a historical page, and does not reset user filters or selection. Store UTC consistently and label it in controls and results. No full usernames or other sensitive filters need to be copied into share links when the host disables that option.

### 13.3 Execution detail

The confirmed presentation leads with the named business action, such as Add city, its actor, resource, and outcome. Show its reason, initiating/executing actor context, chronology, and related work. HTTP, command, handler, and entity boundaries remain expandable supporting evidence rather than equally prominent business entries. This is a presentation projection: preserve captured occurrences and their actual parent/causation relationships. Use business display names where supplied so auditors do not need to interpret CLR type names.

An expandable action tree shows every retained occurrence, parent relationship, business label, action time, execution status, outcome, and approved details. Request/handler identity can be expanded when needed. Failed attempts remain visible when the final attempt succeeded. The chronological view explains the business sequence and its related work.

The entity-change panel groups by entity and source change set, then shows property path, operation, source, persistence state, and value-policy markers. Authorized users can request allowed old/new values. Metadata-only evidence cannot be presented as a complete diff. Provide separately authorized ChangeHistory links when its dashboard is configured.

Show safe errors with their action references. Correlation and causation links carry the active access scope; unavailable linked records have an explicit unavailable state.

Record-level warnings explain omitted actions/values, unresolved transaction evidence, incomplete scopes, and instrumentation limits. A successful root with a failed child must be visually distinct from a failed root.

### 13.4 Related jobs and follow-up work

The request detail page shows a related-work branch under each job-dispatch action. Display the requested job, acceptance/rejection, occurrence identity, initiating user, and available execution records with their business actions and outcomes. A job's later failure does not rewrite the request's original successful outcome.

Related-work queries use origin and immediate-cause identities. Expand downstream job chains through bounded pages, checking authorization on every record. The same page remains useful when the request and job persist at different times or the job runs days later. Exact related-work lookup is not limited by the list's default 24-hour range.

A linked record outside the caller's access scope is not exposed through its label, counts, or details. If execution evidence is unavailable, retain the dispatch fact without asserting that the job is pending, failed, or never ran.

### 13.5 Evidence coverage and availability

The dashboard identifies the source store, whether it is durable or process-local, the available evidence period, and known capture omissions. Buffered records become searchable after persistence; queued acceptance is not presented as a stored audit record. These facts let a business auditor judge the completeness of the trail.

Provider failures show an unavailable state rather than an empty list. An absent downstream execution record does not prove that a requested job never ran. The detail page distinguishes the recorded dispatch fact from available execution evidence.

Auditing dashboard queries, refreshes, and audit-store writes do not create audit records. If a host needs an audit-access trail, it can provide a separate sink that avoids recursive use of this store.

## 14. Configuration, defaults, and composition

### 14.1 Fluent composition

Auditing policy, storage, HTTP capture options, contributors, and optional public endpoints belong to one `AddAuditing(...)` fluent builder. The developer explicitly registers participating behaviors/hooks on each source feature's own builder, with local control over which boundaries participate. Dashboard participation follows the host's existing `AddDashboard` setup automatically.

The minimum EF-backed web audit-services setup uses defaults without configuration callbacks. Source-feature behaviors are selected separately on their own builders:

```csharp
services.AddAuditing()
	.WithEntityFrameworkProvider<AppDbContext>()
	.WithHttpRequests()
	.WithEndpoints();

services.AddDashboard(options => options.Enabled());
```

This assumes the host has registered `AppDbContext`, exposed/configured the audit entity sets, and applied its schema as described in section 10.3. Public endpoints remain optional; omit `WithEndpoints()` when they are not needed. `AddDashboard()` enables the automatically discovered page through the normal dashboard setup.

Service-registration callbacks are optional, including those on `AddAuditing`, `WithEntityFrameworkProvider<TContext>`, `Buffered`, `WithHttpRequests`, and `WithEndpoints`. An omitted callback, an empty callback, and explicitly supplying the documented defaults produce equivalent effective options. A partial override changes only the named settings. Builders and options classes use one source of defaults rather than maintaining separate values.

The minimal setup enables Auditing with ReportOnly failure handling and buffered EF writes. It uses batches of 100 records, a one-second flush interval, the bounded queue and shutdown defaults in section 14.2, the request-logging blacklist defaults in section 7.1, and endpoint group `/_bdk/api/auditing`. No `Enabled()`, `OnFailure(ReportOnly)`, `Buffered(...)`, or default `GroupPath(...)` call is required.

Application name is optional: an explicit `ApplicationName(...)` overrides the available DevKit/generic-host application name. Without host information, use the entry assembly's simple name, falling back to `Application`. Resolve this once through the existing host abstractions; no ASP.NET dependency or custom contributor is required.

Configuration is for overrides. For example, this changes the business application name, batch size, interval, and public endpoint path, and adds an application-specific contributor:

```csharp
services.AddAuditing(options => options
	.ApplicationName("CustomerPortal"))
	.WithEntityFrameworkProvider<AppDbContext>(options => options
		.Buffered(writer => writer
			.BatchSize(250)
			.FlushInterval(TimeSpan.FromSeconds(2))))
	.WithHttpRequests()
	.WithContributor<BusinessContextAuditContributor>()
	.WithEndpoints(options => options
		.GroupPath("/api/auditing"));
```

The host configures Requester, Notifier, current Jobs, optional ChangeHistory, and other features through their own builders and explicitly adds the auditing behaviors/bridges it wants. Source features register no audit behaviors by default. The selected adapters resolve optional Common contracts at execution time, so explicit selection works before or after `AddAuditing`. No duplicate feature-enablement list belongs on the Auditing builder. Appendix A shows the full Requester/repository/ChangeHistory opt-ins.

The explicitly registered behaviors determine which operations are captured. Once Requester auditing is selected, its default captures commands and only explicitly selected business queries, using behavior-level type selectors/predicates or approved endpoint/request metadata. Source-feature behavior options and global Auditing capture policy retain exclusions and selectors. No separate command/query capture switches are introduced. With Auditing absent or disabled, these behaviors pass through without collecting or serializing audit details.

An explicitly selected ChangeHistory bridge consumes its configured source contexts/entities, independently of which DbContext stores audits. Snapshot is the default audit-copy policy; changing it to reference-only is configuration, not adapter enablement. For example, this proposed option changes policy without registering ChangeHistory or selecting a source context:

```csharp
services.AddAuditing(options => options
	.ChangeHistory(changes => changes
		.Mode(AuditChangeHistoryMode.ReferenceOnly)))
	.WithInMemoryProvider();
```

Configuring that policy while ChangeHistory or its explicit audit bridge is absent is valid and produces no change evidence. Copy mode does not register or enable the bridge. ChangeHistory's own exclusions, redaction, transaction evidence, and entity/context configuration remain authoritative.

Explicit adapter registration is idempotent and preserves the ordering in section 7. Repeated registration must not duplicate the same boundary. A feature with no selected audit behavior contributes no automatic boundary action; manual scopes and other selected capture remain available. Registration does not inspect whether Auditing has already been registered or build an intermediate service provider.

`WithEndpoints()` registers public endpoint options/services with sane defaults and can be omitted. Its optional callback customizes them; configuring a group path is not required. The Auditing dashboard page is discovered through global `AddDashboard`; there is no Auditing-specific dashboard registration method.

ASP.NET middleware and route mapping still run at application construction time. `WithHttpRequests()` supplies the shared request-logging blacklist defaults, and its options overload configures `PathBlackListPatterns` as described in section 7.1; `UseAuditing()` installs the boundary in section 7.1's middleware order. `MapAuditingEndpoints()` maps the optional registered public endpoints; the existing dashboard route builder maps the automatically discovered Auditing page under general dashboard configuration.

Simple local composition is `AddAuditing()`, which uses the default bounded in-memory provider; `WithInMemoryProvider()` can make that choice explicit and also accepts optional configuration. Custom storage uses `WithProvider<TProvider>()`, with a factory overload for dependencies. Repeating the same registration is idempotent; selecting two different explicit providers fails startup.

New participating features expose explicitly selectable auditing adapters through their own builder and the same optional Common contracts. Core Auditing remains independent of their packages; adding a feature requires no new integration-enablement method on the Auditing builder.

### 14.2 Default policies and resource limits

These are the default design settings. Parameterless fluent calls use them directly; every options object starts with these values and supports partial overrides. They bound captured evidence and storage use.

| Setting                                            | Proposed default                                                                                                                                                                                           |
| -------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Feature enabled / failure handling                 | `AddAuditing()` enables capture with `ReportOnly`; no explicit `Enabled()` or `OnFailure(...)` is needed. Absent registration does no work.                                                                |
| Application name                                   | Explicit override, otherwise DevKit host name, generic-host name, entry assembly simple name, then `Application`.                                                                                          |
| Configuration callbacks                            | Optional throughout root/provider/buffer/HTTP/endpoint setup; empty callbacks retain all defaults and partial overrides retain unspecified values.                                                         |
| Provider                                           | In-memory until an explicit provider is selected.                                                                                                                                                          |
| Manual scope options | Fluent key/type factory; readable display name, `Business` kind, `JoinOrStart` mode. Runtime details/resources are explicit; disposal alone never implies success. |
| Feature participation | Off at each source boundary until its audit behavior/bridge is explicitly registered on that feature's builder. Omission is local; no implicit registrations. |
| Command/query/notification capture | Selected by explicit Requester/Notifier audit behavior registrations. Commands then default to captured; business queries require selection. No duplicate capture switches. |
| HTTP request blacklist                             | Same `PathBlackListPatterns` defaults and path/`MatchAny` semantics as RequestLoggingMiddleware, including `/_bdk/*`; host replacement is explicit.                                                        |
| HTTP reads and polling                             | Meaningful business reads require explicit selection after blacklisting; routine refresh/polling requests do not create automatic audit records.                                                           |
| Anonymous actors                                   | Included for selected adapters and eligible routes.                                                                                                                                                        |
| Module context                                     | Capture when available; no modules required.                                                                                                                                                               |
| Correlation                                        | Preserve current valid application correlation ID; establish one at root entry if missing.                                                                                                                 |
| Sampling                                           | None. Exclusions are explicit and documented.                                                                                                                                                              |
| ChangeHistory enrichment | Requires configured source capture and explicit `WithAuditing<TSourceContext>()` on its builder; Snapshot copy mode by default, ReferenceOnly configurable as audit policy. |
| Parameter/result capture                           | Request/handler identity automatically; business values only through explicit details/enrichers/scopes or optional ChangeHistory. No automatic whole-payload serialization.                                |
| HTTP bodies, query strings, headers, IP/user-agent | Disabled until explicitly approved.                                                                                                                                                                        |
| EF persistence/failure                             | Provider-owned asynchronous queue, report-only; inline mode is opt-in.                                                                                                                                     |
| Automatic EF flush                                 | Buffered writer starts with provider registration; 100 records per batch or a one-second interval, with no `Buffered(...)` call required.                                                                  |
| EF writer retries                                  | Three retries after the initial attempt; exponential backoff starting at 250 ms and capped at two seconds. The shutdown drain remains bounded; canceling a flush wait does not cancel writer work.         |
| Public endpoints                                   | Opt in with parameterless `WithEndpoints()`; default group `/_bdk/api/auditing`, documented read routes and standard FilterModel/Result contracts, general host access rules, shared query defaults below. |
| Maximum record                                     | 1 MiB serialized UTF-8 JSON, including all child data.                                                                                                                                                     |
| Action limits                                      | 500 retained occurrences, depth 32, and at most 128 simultaneously active child scopes per root.                                                                                                           |
| Change limits                                      | 100 entity change sets and 1,000 property entries per root, subject to the byte cap.                                                                                                                       |
| Details                                            | 32 keys per root/action, 128 characters per key, 4 KiB per value.                                                                                                                                          |
| Error details                                      | 20 safe exception descriptions, 1 KiB text each, stack traces off.                                                                                                                                         |
| Active roots                                       | 1,024 per process; admission beyond capacity passes business work through and reports omitted audit evidence.                                                                                              |
| Recording lifetime                                 | 30 minutes; expiry stops capture, marks incomplete, and never cancels execution.                                                                                                                           |
| In-memory capacity                                 | 10,000 finalized active-plus-archived records or 128 MiB total, whichever is reached first; bounds remain enforced independently of age retention.                                                         |
| EF queue and drain                                 | 1,000 records or 64 MiB, finite retry budget, explicit flush, ten-second shutdown drain.                                                                                                                   |
| Search                                             | Last 24 hours, maximum 31-day range, page size 50, maximum 200, ten-second query budget.                                                                                                                   |
| Retention                                          | Required archive/remove capability on both providers; automatic schedules off until host-configured. Same age/interval/batch options, with short local/test profiles.                                      |

Count actions when admitted and bound active capture even before completion. Limits produce reason codes and omitted counts. Preserve root identity/outcome and admitted action summaries first, then optional details and value snapshots. If the minimal safe document itself cannot fit, report append rejection rather than truncate raw JSON text.

Recording expiry freezes an incomplete record and releases capture state. Later completion cannot reopen it or claim its recorded duration was full business duration. Action helpers still call their business delegates normally after capture stops.

Validate positive bounds and reasonable relationships at startup. Invalid source ChangeHistory configuration, a configured source missing its required evidence hooks, or incompatible selected provider capabilities fails early with an actionable configuration error. Absence of the optional ChangeHistory feature is valid and requires no audit-specific context registration. Dashboard availability does not depend on registering any additional policies.

### 14.3 Retention, archiving, and immutability

Retention is a required configurable capability of both built-in providers. It supports an age-based lifecycle of active, archived, and removed audit records. Automatic archive/removal schedules are disabled until configured by the host; the capability itself is part of the feature.

Configure the same `ArchiveAfter`, `RemoveAfter`, maintenance interval, and batch-size options for EF and in-memory storage through the AddAuditing builder. Short intervals suit local/test in-memory use. No separate in-memory retention semantics or audit-specific dashboard policy is introduced.

The proposed age anchor is the record's immutable `CompletedUtc`. `ArchiveAfter` and `RemoveAfter` are total ages from that anchor, not two durations silently added together. Validate nonnegative thresholds and, when both are set, `RemoveAfter > ArchiveAfter`. A retention configuration can archive indefinitely by omitting removal. Removal operates on records that have entered the archived state. Exact threshold values belong to the host.

Archiving retains the complete logical audit evidence while moving it out of the provider's normal active query path. It is a provider storage transition, not just a hidden row in the dashboard. Normal active searches/counts/facets must restrict their provider dataset before materializing archived records or child graphs.

EF may use a compact storage-state/index strategy or a distinct cold representation without changing the public audit document or requiring a new external storage service. The physical mapping must preserve the agreed compact model where practical and prove that routine active queries do not load the archived payload. In-memory storage separates active and archived lookup/query collections.

Archive/remove operations apply to the whole record and its owned actions, changes, and JSON details. Archive preserves IDs, timestamps, actors, module/correlation, source references, and capture completeness. Removal cleans up the entire owned graph without orphaned children. Cross-record origin/causation references remain non-owning and may resolve to unavailable evidence after removal.

Lifecycle state and archive/removal bookkeeping are provider-owned metadata, separate from the immutable captured document and its content hash. Archiving does not rewrite business facts, turn unknown outcomes into committed changes, or invalidate idempotent retries of the same document.

A bounded maintenance worker applies the configured thresholds using the selected provider. It runs under audit suppression, processes finite batches, supports cancellation, and makes concurrent/multi-node attempts safe and idempotent. It does not archive a live scope or an unpersisted queue envelope.

Coordinate maintenance with appends and explicit flush so delayed/retried writes do not restore removed history or insert archive-aged records back into the active path. A failed archive must retain the original complete evidence; removal must not treat a partially archived graph as successful.

ChangeHistory retention is independent. Audit snapshots can outlive source rows, and source references can become unavailable. Archiving/removing one store never silently changes the other's retention policy.

#### In-memory capacity and local profiles

In-memory storage has mandatory record-count and byte bounds even when age retention is disabled. Count active and archived records together, including their full owned payloads, so archiving cannot create an unbounded second collection.

At capacity, evict oldest archived records first, then oldest finalized active records as needed under the documented capacity policy. Report capacity loss separately from age-based archive/removal. These are completed records in an ephemeral store; this must not evict documents accepted into the EF provider's unpersisted writer queue.

The existing default bounds of 10,000 records or 128 MiB remain proposed local defaults. Tests can use much smaller capacities and short archive/remove ages. For example, a local profile can archive after one minute, remove after ten minutes, and sweep every few seconds using the same options as EF.

The confirmed archive-access contract keeps archived audits searchable through the same dashboard and FilterModel POST endpoint via explicit Active / Archived / All selection. Ordinary searches default to Active. Exact-ID lookup can retrieve retained archived evidence and identifies its storage state; this bounded lookup does not broaden an ordinary active search.

## 15. Security and privacy

All capture paths, including descriptors, contributors, manual scopes, exception text, ChangeHistory values, and buffered documents, pass through the same policy pipeline before persistence.

Use explicit field projections and centralized exclude/redact/hash policies. Built-in protections cover passwords, credentials, bearer tokens, connection strings, cookies, and known secret properties. A secret-name deny list supplements explicit projection; it is not permission to serialize an arbitrary object.

Sanitize before buffering and before typed logging. Reapply exposure policy when querying so a later restriction can hide previously captured values. Previously omitted or redacted values cannot be recovered by relaxing a read policy.

Actor and tenant fields come from trusted server context. A details interface cannot impersonate a user or alter access scope. Multi-tenant filtering is required when the host enables tenant context; the feature does not require a tenancy module for single-tenant applications.

Treat display text and JSON as untrusted content in the dashboard. HTML-encode values and never execute or render stored markup. Parameterize queries and enforce response size limits. Storage error reports contain safe identities/counts, not audited payloads.

Append-only application APIs do not prevent database administrators from changing records. Cryptographic signing, external immutable storage, legal hold, and regulatory certification are separate provider/application requirements.

## 16. Evidence reliability and capture completeness

A business auditor needs to distinguish a complete captured story from missing or unavailable evidence. Every record exposes its capture scope, omitted action/change counts, unknown transaction outcomes, and any incomplete scopes. Business labels must not imply that every external effect was observed.

Delivery failure handling preserves the business result under the default policy. The host receives a safe error identifying the audit record and failed storage operation. Do not write the audited payload or sensitive values to application logs.

Buffer admission is not proof of durable storage. If buffered mode is selected, communicate its evidence-loss limits. Process-local stores and retention rules also limit which historical evidence is available.

Business outcomes and evidence completeness are separate. A successful business action can have incomplete capture, and a failed request can contain committed actions or an accepted job that later succeeded. Related execution records add evidence without rewriting those original facts.

Business actions, attribution, related work, and trustworthy evidence are the feature's subject.

## 17. Acceptance criteria

### Story 1: Investigate an incoming request

As a business auditor or authorized investigator, I want one execution trail so that I can establish who requested a business operation and which actions followed.

1. Given eligible HTTP capture, when a request dispatches a command and an explicitly selected business query, then one root contains both dispatches and their invoked handlers.
2. When validation rejects a command or a selected business query returns from cache, then the dispatch records that outcome without inventing a handler invocation.
3. When a handler returns a failed Result without throwing, then its action records the failure and the root retains the actual HTTP outcome.
4. When an error is handled by middleware, then the record contains the resulting status and any supplied safe exception evidence.
5. When the route is excluded, then neither its HTTP root nor its nested feature behaviors create records.

### Story 2: Instrument application code

As a developer, I want nested scopes so that services and handlers can describe business work directly.

1. When code opens nested and repeated scopes, then each retained occurrence has the correct parent and distinct ID.
2. When awaited parallel branches create children, then parentage and details remain isolated across branches.
3. When a callback returns or throws, then the wrapper preserves that result/exception and classifies the action.
4. When an uncompleted manual scope is disposed, then it is unknown/incomplete and ambient context is restored.
5. When Auditing is absent or suppressed, then selected feature behaviors execute normally without persistence or payload projection.
6. When the RenameCustomerCommandHandler example runs, then its named business scope/details nest beneath the invoked handler and its repository action nests beneath that business scope. Missing-customer, domain-rejection, cancellation, and exception paths complete/fail the scope explicitly; no path infers success from disposal.
7. When standalone `AuditScopeOptionsBuilder.Create(key).Build()`/`Create<T>().Build()` or equivalent callback For configuration is used, then options have a valid stable key, readable display name, Business kind, and JoinOrStart mode without further configuration. Fluent overrides preserve unspecified defaults and invalid structural values are rejected.
8. When scope options are built or the builder is subsequently changed, then no recording starts, the built options remain unchanged, and suppression/explicit feature selections/outcome rules still apply when a scope is eventually opened.
9. When `BeginScope(s => s.For<T>(nameof(...)))` or equivalent standalone `AuditScopeOptionsBuilder.Create<T>(nameof(...)).Build()` is used, then type/member context supplies the key and readable label without literal naming fields. Typed resources use the same convention and normal explicit ID/value policies.
10. When eligible callback-based BeginScope/RunAsync capture configures options, then a fresh builder is configured/built once with the same effective options as the standalone path. Disabled/suppressed capture skips the configuration callback; RunAsync still runs its business delegate once. Configuration failures follow the audit-failure policy and ReportOnly never repeats/prevents the business delegate. A configuration callback alone does not complete a manual handle.

### Story 3: Capture identity and object details

As a developer, I want optional identity and details so that the trail carries useful business context without requiring an identity module.

1. Given a current-user accessor, when an execution starts, then the record snapshots permitted user identity.
2. Given no accessor, when an execution runs, then it records an anonymous or explicitly configured service actor.
3. When requests/entities implement the details interface or use an enricher, then their approved descriptors are attached.
4. When a descriptor throws or supplies a secret, oversized value, or reserved field, then capture reports the limitation and applies policy without changing the business delegate's execution.
5. When `AuditDescriptorBuilder.For<T>().Build()` is used, then the descriptor has a stable type-derived key, readable display name, absent reason, and empty resources/details. Fluent overrides and partial configuration retain unspecified defaults.
6. When the descriptor builder adds resources/details or builds repeatedly, then copied immutable collections, stable ID conversion, structural validation, and normal capture policies apply. Building a descriptor alone never enables capture.
7. When no explicit names are supplied, then builders and selected request/handler/repository/HTTP/job adapters share the documented type/operation/route naming convention. Explicit overrides take precedence; replacing a convention changes only derived names on new capture, never frozen/queued/historical records.

### Story 4: Inspect actual entity changes

As a business auditor, I want optional committed change evidence so that I can distinguish attempted actions from actual changes to business data.

1. When ChangeHistory or its audit bridge is absent/disabled for a context/entity, then Auditing performs no independent entity comparison or history capture. With source capture, suitable hooks, an explicitly selected source-feature bridge, and Auditing enabled, the captured changes enrich the trail.
2. When an enabled change set commits, then its safe source identity, property paths, and permitted old/new values attach to the originating action.
3. When an enclosing transaction or savepoint rolls back, then its evidence is not shown as committed.
4. When transaction completion cannot be observed, then evidence is Unknown, contains no old/new snapshots, and is excluded from committed-change views.
5. When a retry reuses a capture path or bulk mode supplies only a summary, then the audit contains no duplicated or fabricated deltas.

### Story 5: Select and extend storage

As an application developer, I want interchangeable providers so that local and durable deployments use the same audit model.

1. When in-memory or EF storage is selected, then append, lookup, filters, grouping, and paging satisfy the same conformance tests.
2. When EF migrations are generated, then a compact two- or three-table mapping is acceptable, with explicit ownership and JSON for nested details/deltas; the proposed default has records, actions, and entity-change groups.
3. When the same frozen ID/content is retried, then append is idempotent; different content under that ID fails without overwrite.
4. When EF is unavailable, then there is no fallback to in-memory and the configured delivery-failure policy applies.
5. When a business transaction rolls back, then an independently appended failed execution record can still be read.

### Story 6: Search the trail

As a business auditor or process owner, I want combined filters so that I can find work by initiating user, time, business action, resource, related job, and outcome.

1. When filters select children, then the provider applies them before paging and uses the documented same-child semantics.
2. When searches, aggregates, and facets use the same selection, then all apply the same access scope and comparison rules.
3. When POST search receives FilterModel criteria, ordering, and paging, then both providers return standard ResultPaged metadata with deterministic tie-breakers.
4. When a filter is unsupported or a query exceeds limits, then the API returns a typed error rather than silently ignoring criteria.
5. When retained evidence is truncated, then results expose that limitation and do not claim complete negative evidence.

### Story 7: Operate through the dashboard and endpoints

As an authorized business auditor, I want protected read tools so that I can review business activity and related work without custom UI.

1. When enabled and authorized, then business-action search, related-job navigation, action tree, optional change details, facets, and completeness views work with either built-in provider.
2. When AddDashboard is enabled with Auditing registered, then its page appears automatically; when AddDashboard is absent, then no dashboard routes appear while capture still works.
3. When host-wide dashboard or data-access rules apply, then internal reads follow those general rules; captured redaction and configured value exposure apply without requiring new Auditing dashboard policies.
4. When a provider fails, then the UI shows an unavailable state rather than a false empty result.
5. When a user searches or refreshes Auditing, then those operations create no recursive records.

### Story 8: Participate from devkit features

As a feature developer, I want optional adapters so that new and existing features can add evidence without implementing storage.

1. When a participating operation runs within a root, then it contributes actions without finalizing its parent.
2. When a job, delivery, detached handler, or resumed orchestration executes independently, then it starts a new root with approved causation.
3. When existing policies retry work, then auditing records observable attempts without introducing additional execution.
4. When capture limits or contributor failures occur, then business work continues under the default policy and capture notes expose loss.
5. When an audit behavior/bridge is explicitly registered twice, then idempotent registration/invocation ownership prevents duplicate evidence.
6. When explicitly selected source-feature adapters and Auditing are registered in either order, then their selected boundaries participate identically. Feature registration without an audit behavior adds no such action. Omitting a behavior is a local opt-out and does not suppress manual or other selected capture. An absent/disabled recorder causes pass-through without audit serialization.


### Story 9: Follow jobs started by a business request

As a business auditor, I want to see jobs requested by a user operation so that I can follow the resulting business work.

1. When a command or handler dispatches a job, then that request contains a job-dispatch action with the requested job and observed acceptance/rejection.
2. When the job executes after the request completes or on another node, then its record preserves origin/immediate-cause links and initiating-actor attribution.
3. When the request's detail page is reopened, then authorized linked job executions and outcomes appear in the same business trail without modifying the original record.
4. When the job fails, retries, or the caller stops waiting, then dispatch acceptance, caller outcome, and job execution outcomes remain distinct.
5. When execution evidence is unavailable, delayed, or outside access scope, then the view does not invent an execution outcome or expose unauthorized work.

### Story 10: Capture business actions without entity infrastructure

As a business process owner, I want auditing to work across services and handlers so that the trail does not depend on how business data is stored.

1. Given no repository, Active Entity, or ChangeHistory registration, when a handler/service records business actions, then those actions are captured and searchable.
2. Given an audit-only DbContext exposing `AuditRecords`, when the EF provider persists an audit, then no business-entity repository or entity feature is required.
3. When optional repository/Active Entity behaviors are enabled, then they add rough entity actions without asserting property deltas.
4. When ChangeHistory capture and its explicitly selected audit bridge are enabled for the affected source context/entity, then the existing captured changes enrich relevant actions without a second diff engine or a matching audit-storage context.
5. When business labels/reasons are supplied, then list and detail views present them without requiring auditors to interpret implementation type names.

### Story 11: Persist finalized audits asynchronously

As an application developer, I want a provider-owned queue and graceful flush so that audit database writes do not block request completion.

1. Given buffered EF persistence, when a finalized audit is accepted, then append returns a queued receipt without awaiting a database write.
2. When the batch threshold or flush interval is reached, then the background writer persists accepted frozen documents through a fresh audit DbContext.
3. When explicit flush completes successfully, then all records accepted before its boundary are confirmed persisted, including records already being written.
4. When the queue is full, storage retries fail, or flush times out, then the caller/host receives an honest admission or delivery result and the original business outcome follows the configured policy.
5. When graceful shutdown begins, then admission stops and accepted records drain within the shutdown budget; unpersisted remainder and abrupt-termination limits are explicit.

### Story 12: Preserve module and correlation context

As a business auditor, I want module and correlation context so that related actions remain identifiable across requests, services, and jobs.

1. When a current module is available, then the root/action captures it; without module services, auditing still works and stores no invented module.
2. When a request crosses modules or dispatches a job in another module, then current and initiating module attribution remain distinct.
3. When a valid correlation ID exists, then root/actions and downstream job metadata preserve it; a missing root correlation is established once through the existing convention.
4. When records are queued and persisted later, then module/correlation values come from the captured business execution, not the writer's ambient context.
5. When users search or open related work, then the captured module and correlation fields remain visible and searchable under general host access rules.

### Story 13: Compose the feature and discover its dashboard

As an application developer, I want one Auditing setup and normal dashboard discovery so that enabling the feature requires no duplicate capture or permission configuration.

1. When Requester, Notifier, current Jobs, repositories, ChangeHistory, or another supported feature is enabled, then auditing participation requires explicit behavior/bridge registration on that feature's own builder. `AddAuditing` and ordinary feature setup register none of those behaviors implicitly. There are no per-feature integration calls on the Auditing builder or duplicate command/query capture switches.
2. When optional public endpoints are selected, then their options are configured on that same builder.
3. When AddDashboard and Auditing are registered in either order, then the Auditing page/card/navigation appear once without a feature-specific dashboard call or policy.
4. When AddDashboard is omitted, then capture still works and no dashboard routes appear.
5. When legacy Commands, Queries, or JobScheduling are registered, then this feature adds no auditing integration for those legacy pipelines.
6. When HTTP capture uses its default blacklist, then it matches the exact RequestLoggingMiddleware default patterns and path/matcher behavior, including dashboard exclusion through `/_bdk/*`. Selected nested Requester/repository behaviors and manual scopes emit no audit record/action and cannot open a replacement root, including RequiresNew. Custom lists replace the defaults without mutating request logging; a null/empty list disables only path blacklisting, not internal audit suppression.
7. When `AddAuditing().WithEntityFrameworkProvider<AppDbContext>().WithHttpRequests().WithEndpoints()` is used against a configured context, then capture is enabled with ReportOnly, bounded buffered writes/batching/flush/retries, the standard blacklist, and endpoints at `/_bdk/api/auditing`, without configuration callbacks or a custom contributor.
8. When callbacks are omitted, empty, or explicitly set to all documented defaults, then effective settings are equivalent. Partial overrides preserve the remaining defaults, including buffered-writer settings and endpoint contracts.
9. When one repository opts in and another omits its audit decorator, then only the selected repository emits rough actions. Omitting either Requester auditing behavior or the ChangeHistory audit bridge similarly removes only that boundary/evidence; surrounding HTTP/manual and other selected capture continue.

### Story 14: Audit real WeatherFiesta city actions

As a WeatherFiesta business user or auditor, I want city additions and updates to leave a readable trail so that I can verify who performed them and what happened.

1. When a user adds/creates a city or edits its details through the application, then EF-backed auditing captures the request and actual city/subscription actions with actor, module, correlation, resource identity, and outcome.
2. When city creation reuses an existing city or input is rejected, then the audit reflects that path without inventing a creation, handler invocation, or committed change.
3. When AddDashboard is enabled, then the Auditing page appears automatically and can find these records through the same search semantics as the FilterModel POST endpoint.
4. When audit writes are buffered, then application completion is independent of persistence and a successful bounded flush makes the full record graph available for assertions.
5. When optional ChangeHistory enrichment is enabled for a real city update, then permitted actual deltas appear; with it disabled, the base business audit still works.
6. When City Management mutations execute beneath the dashboard prefix, then WeatherFiesta's explicit audit-blacklist override admits them while retaining the remaining standard blacklist defaults. Auditing's own requests remain internally suppressed.

### Story 15: Archive and remove audit evidence

As a host operator or business auditor, I want configurable age-based retention and explicit archive searches so that normal auditing stays focused while older evidence remains reviewable until removal.

1. When archive and later removal ages are configured on either built-in provider, then eligible finalized audits progress through those stages with their whole owned graph.
2. When no archive scope is supplied, then searches/counts/facets use Active without loading archived payloads; explicit Archived/All selections preserve the same business filters and host access rules.
3. When a record is archived, then its IDs, captured facts, and content hash remain unchanged, and All returns it only once.
4. When removal becomes eligible, then the archived record and owned data are removed without orphaned children or delayed writes restoring them.
5. When the in-memory provider reaches its combined active-plus-archive count/byte limit, then it remains bounded independently of age settings and exposes capacity loss; short local/test retention profiles use the same options.

## 18. Verification strategy

The implementation must prove the acceptance criteria with focused tests.

| Area                          | Required evidence                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Recorder unit tests           | Nested/parallel frames, repeated actions, suppression, no-op behavior, callback result preservation, manual unknown outcome, expiry, late writes, duplicate disposal, limits.                                                                                                                                                                                                                                                                                                            |
| Descriptor builder | Minimal type-based defaults, fluent overrides, immutable/copy-on-build resources and details, stable keys/IDs, null optional text, invalid values, reserved fields, policy application, and no capture enablement from descriptor construction. |
| Scope-options builder | Fluent BeginScope/RunAsync callbacks, eligible configuration/build once, no configuration callback under suppression/disabled capture, ReportOnly configuration failures preserve single business execution, standalone key/type/member factories, equivalent defaults, immutable snapshots, derived labels, custom kinds/modes, and no outcome inference or suppression bypass. |
| Naming conventions | Shared descriptor/scope/feature naming, type/member keys, typed resource IDs, readable suffix handling, route/definition-derived names, explicit-override precedence, configured aliases, collision-safe qualified identities, and frozen naming through queueing and later reads. |
| Request/handler tests | Explicit dispatch/handler opt-ins and omissions; commands captured by default once selected; business-read selectors; no polling/refresh records; local skips preserve nested selected work; selected versus invoked handler, rejection, cache hits, retries, Result failures. |
| Explicit feature composition | Source-feature setup alone installs no audit adapters. Explicit per-feature/per-repository selection, local omission, dispatch/handler choices, both registration orders, idempotent repeated registration, absent/disabled recorder pass-through, and preservation of manual/other selected capture. |
| Default fluent setup          | Parameterless EF/HTTP/endpoints setup captures and persists with default buffered writer and default routes; omitted/empty/explicit-default callbacks are equivalent; partial overrides retain all unspecified values; source application name resolves with and without host services; no custom contributor is required.                                                                                                                                                               |
| Provider conformance          | Identical supported FilterModel operators/logic/orderings/paging, ResultPaged counts, same-child predicates, allowlist validation, related-work continuations, aggregate/facet scope, idempotent append, immutable snapshots, configured archive/remove age semantics.                                                                                                                                                                                                                   |
| Retention lifecycle           | Complete graph archive/removal, Active default without archive-payload loading, equivalent Archived/All filters and counts, no duplicates in All, no partial archive/orphans, maintenance/append/flush races, unchanged hash, combined in-memory bounds, short local/test intervals.                                                                                                                                                                                                     |
| EF integration                | Audit-only DbContext exposing the selected audit entity sets, compact table model with JSON details, atomic header/child append, idempotent graph retries, filtering before paging, independent context/transaction, no orphaned children after retention.                                                                                                                                                                                                                               |
| ChangeHistory integration | Explicit source-context bridge opt-in and omission while source capture continues; source/audit-context independence; Snapshot/ReferenceOnly policies; supported direct mutations, commits, rollback/savepoint, retries, unknown external transactions, redaction and deduplication. |
| HTTP integration              | Exact parity with request logging's default blacklist and shared MatchAny helper, case-insensitive full-path matching, wildcard traversal across separators, feature-path/fallback selection without queries, null/empty/custom replacement lists, independently configured copies, nested suppression, explicit WeatherFiesta dashboard override, authentication/authorization ordering, handled/unhandled failures, final status, cancellation, optional header, no recursive capture. |
| Dashboard/endpoints           | Automatic AddDashboard discovery in either registration order, no additional feature-policy requirement, optional public endpoints, host-wide access rules, scoped related work, labels/reasons, module/correlation filters, initiating/executing actor searches, evidence completeness.                                                                                                                                                                                                 |
| Job and worker boundaries | Explicit job/Requester/repository selection, independent timer roots, nested ownership, worker suppression isolation without implicit capture, direct versus HTTP calls to blacklisted routes, no fabricated origin/actor, dispatch/causation, initiating versus executing actors, retries/wait-timeouts, and out-of-order persistence. |
| Architecture                  | Domain/Common contain no Auditing implementation or EF/Web dependencies; Application queries use abstractions; Presentation never exposes persistence entities.                                                                                                                                                                                                                                                                                                                          |
| Queued EF persistence         | No database wait during admission, count/time batch flush, explicit flush boundaries including in-flight batches, queue-full result, ambiguous retries, terminal failures, concurrent flush, cancellation, graceful shutdown and crash-loss limits.                                                                                                                                                                                                                                      |
| Business audit usability      | Meaningful labels/reasons/resources, actor-role searches, request-to-job trail navigation, visible capture limits, entity-feature-free setup.                                                                                                                                                                                                                                                                                                                                            |
| WeatherFiesta proof           | Real city-add/create/update application paths, actual created-versus-existing outcomes, optional ChangeHistory enrichment, buffered EF persistence/flush, FilterModel lookup, automatic dashboard page, and no recursive audit reads.                                                                                                                                                                                                                                                    |

Use existing xUnit, NSubstitute, Shouldly, and integration-host conventions. Use the provider's supported real database to prove JSON query and transaction behavior. Run repo-wide builds/tests sequentially in one worktree.

### 18.1 Required WeatherFiesta integration proof

Integrate the implemented feature into the existing WeatherFiesta application as required acceptance work. Business users must be able to perform city actions through the application and then inspect their persisted audit trail.

#### Application wiring

Configure Auditing through the host's `AddAuditing` builder. Explicitly add the selected auditing dispatch/handler behaviors to WeatherFiesta's Requester/Notifier builders; their existing registrations alone do not opt in. Reuse optional current-user/module services and correlation conventions. Use the EF provider against WeatherFiesta's `CoreDbContext`, expose the selected audit entity sets, configure their model, and include the schema changes in the example application's migration/setup workflow.

Use provider-owned buffered writes in the example. The existing `AddDashboard` registration must discover the Auditing page automatically, without an Auditing dashboard registration or additional feature-specific policies. Enable the optional POST search endpoint in this proof so its FilterModel contract is exercised against the same persisted records.

Supply business labels and explicit safe details through `IAuditDetails`, registered detail providers, or narrowly placed scopes. Include the city ID/name/country where appropriate, the actual initiating actor, current Core module, correlation ID, request/handler identity, action outcome, and capture completeness. Attach newly generated city IDs when they become available; do not require them at request entry or serialize whole request/entity graphs.

Explicitly register the Active Entity auditing behavior for the City/UserCity configurations selected for this proof to demonstrate rough city/subscription actions. Other entity configurations can omit it. Ensure it does not duplicate the command's business action or another adapter's observation of the same underlying operation.

#### Required user scenarios

| Scenario                              | Existing flow and integration work                                                                                                                                                 | Required audit evidence                                                                                                                                       |
| ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Add a city to a user's list           | `POST /api/core/cities` invokes `CityCreateCommand`. It can create a city, reuse one by external identity, and create/reactivate a user-city subscription.                         | One user-request trail with the actual city/subscription actions and resources. Reusing a city does not claim a new city was created.                         |
| Create a city through City Management | The existing dashboard add action dispatches `AdminCityCreateCommand`, or returns an already known city.                                                                           | Business label, actor, module/correlation, selected/invoked handler when applicable, created resource ID, and truthful created-versus-existing outcome.       |
| Update a city                         | Wire `AdminCityUpdateCommand` into a user-accessible City Management edit action or appropriate application endpoint. It already calls `City.ChangeDetails` and persists the city. | The user's update request, city identity, handler/manual/rough entity actions as configured, outcome, and the same captured actor/module/correlation context. |
| Reject or fail a city action          | Submit invalid city input or update a missing city through the same application surface.                                                                                           | The actual rejection/failure, no invented invoked handler after pre-handler rejection, and no falsely committed entity change.                                |
| Repeat or make no effective change    | Re-add an existing city or submit unchanged city details.                                                                                                                          | The actual business request and outcome remain visible without fabricated creation or property deltas.                                                        |

Keep the example changes focused on these business actions. The existing admin update command is not yet exposed by the City Management page, so implementing the user-facing update path is part of this proof. A direct command unit test alone does not satisfy it.

Business mutations reached through WeatherFiesta's dashboard must remain auditable. The shared default blacklist includes `/_bdk/*`, so the example must explicitly replace its Auditing `PathBlackListPatterns` with the default collection minus that entry, retaining the other standard patterns. This is a host override; the feature's defaults remain identical to request logging. Suppress Auditing's own reads/refreshes and designated infrastructure requests internally, while allowing City Management create/update requests to pass through the HTTP and Requester auditing boundaries. Verify both the default exclusion and the example's intentional override.

#### Optional ChangeHistory enrichment proof

Prove the base city-action trail without ChangeHistory enabled. Then use an explicit example/test configuration that enables ChangeHistory for City through that feature's own setup. Explicitly select its audit-evidence bridge on the ChangeHistory builder for the source context. Without a duplicate Auditing-builder integration or repeated mandatory entity list, demonstrate enrichment of a real update with safe source change-set identity, property paths, and allowed old/new values.

Use ChangeHistory's supported capture strategy for `City.ChangeDetails`, which currently changes properties directly. Do not add an independent diff engine to the WeatherFiesta handler or to Auditing. Same-value updates produce no fabricated property deltas, and failed/rolled-back work must not appear as committed changes.

#### Reviewable evidence and verification

1. Exercise real authenticated application paths to add/create and update fresh cities, using deterministic geocoding responses where needed.
2. Confirm that responses complete with buffered audit acceptance independently of the background database write. Use bounded flush in automated verification rather than arbitrary sleeps.
3. Query the actual persisted audit through the application query service and FilterModel POST search by actor, time, Core module, correlation ID, city resource, action, and outcome.
4. Open the automatically discovered Auditing dashboard page and verify its business labels, request/handler hierarchy, city resources, and optional change details. Audit-page viewing/refresh must not recursively create records.
5. Restart/reopen the example and verify that the EF-backed city audit remains available. Preserve capture-policy and source-availability distinctions.
6. Add integration coverage alongside the existing WeatherFiesta city endpoint/application tests, plus UI coverage for the new edit action and audit-page navigation. Use a supported real relational backend to prove EF persistence/filtering; a fake audit provider alone is insufficient.

Document the example setup and the exact create/update review steps in WeatherFiesta's existing README or REST examples. The feature is not accepted until these application scenarios and their persisted audit evidence are reviewable.

## 19. Design decisions, trade-offs, and delivery

### 19.1 Proposed decisions for review

The table contains initial design recommendations. Confirmed decisions in sections 19.4 and 19.5 take precedence. None of these statements claims completed implementation.

| Decision                                                         | Reason                                                                                                                                                             | Cost or alternative                                                                                                                                             |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| One immutable record per execution, joined into a business trail | Preserves individual actions and work that continues after a request.                                                                                              | Related-execution reads must assemble the trail without modifying old records.                                                                                  |
| Common contracts, Application implementation                     | Allows Domain/Common features to participate without reverse dependencies.                                                                                         | Requires disciplined separation of small contracts from recorder logic.                                                                                         |
| Compact EF model with JSON details                               | Separating records, actions, and entity-change groups supports business searches while keeping nested detail storage small.                                        | Two or three tables require atomic graph append and owned-row cleanup. A two-table mapping can embed change groups where its query contract remains sufficient. |
| Independent core with optional entity integrations               | Captures business work without repositories, Active Entities, or ChangeHistory. Optional behaviors add rough entity actions; ChangeHistory enriches actual deltas. | Detailed changes are available only where ChangeHistory itself captures them.                                                                                   |
| Explicit audit DbSets on the EF context                          | Makes storage requirements visible on a host or dedicated DbContext without repository coupling.                                                                   | Hosts expose and map the selected audit entity sets in their context.                                                                                           |
| Provider-owned buffered EF persistence by default                | Request finalization admits a frozen record without awaiting database I/O; batches and flush provide graceful persistence.                                         | In-process queueing has a crash-loss window. Explicit flush or inline mode is needed for confirmed storage before continuing.                                   |
| Metadata and explicit details by default                         | Gives useful identity/context with bounded exposure.                                                                                                               | Developers must project business-specific parameters rather than obtain whole payload capture.                                                                  |
| Explicit business-read selection                                 | Retains meaningful read activity while keeping incidental polling out of the business trail.                                                                       | Hosts identify relevant reads through behavior-level rules and align HTTP eligibility with them.                                                                |
| Automatic global-dashboard participation                         | Business auditors can use the feature as soon as the host enables AddDashboard.                                                                                    | Page discovery must work without separate feature setup or registration-order dependence.                                                                       |

Promote significant accepted decisions to ADRs using the repository workflow after review. Existing ADR-0004, ADR-0005, and the EF code-first migration guidance continue to apply.

### 19.2 Delivery sequence

1. Use the confirmed grill decisions as the behavioral baseline and finalize the public API/mapping details against the implementation checks.
2. Add Common contracts and the Application recorder, policies, Result errors, and in-memory provider.
3. Add provider conformance tests, the audit-DbSet EF model, its queued writer/batch/flush lifecycle, and shared configurable archive/remove retention with bounded in-memory storage.
4. Add HTTP and explicitly selectable current Requester/handler behaviors, with omission, ordering, and registration-order tests.
5. Add ChangeHistory capture/commit hooks and expose its explicit audit-bridge opt-in on the ChangeHistory builder.
6. Add current Jobs dispatch/execution causation and the other supported adapters, including rough entity-action behaviors, with explicit selection on each feature's own builder.
7. Deliver query services, optional public endpoints, the automatically discovered dashboard page, and unified registration examples.
8. Integrate Auditing into WeatherFiesta city-add/create/update user flows and complete the application proof in section 18.1.
9. Verify business-trail completeness, provider behavior, optional composition, and access boundaries, then update feature/XML and example documentation.

The feature is complete only when both providers, the operational surfaces, and the required WeatherFiesta application proof satisfy acceptance criteria.

### 19.3 Implementation follow-through

The grill session resolved the business-behavior decisions in section 19.4. Implementation must preserve them while making the remaining routine API and mapping choices concrete.

- Compile the fluent, context, scope, and FilterModel examples, including the complete Requester/repository/ChangeHistory walkthrough in Appendix A, against the implemented public APIs.
- Use the WeatherFiesta city-create/update proof to verify labels, resource/actor attribution, failure paths, and explicitly selected business reads.
- Finalize the compact EF mapping within the accepted two- or three-table approach and verify active/archive query separation.
- Verify the proposed batch/flush budgets and capture limits under the required application scenarios; keep those settings configurable.
- Complete the specified adapters and transaction-owner hooks, retaining Unknown evidence where commit cannot be observed.
- Run provider, retention, queued-writer, dashboard-discovery, and end-to-end example checks before marking the feature implemented.

### 19.4 Confirmed grill-me decisions

The grill session completed on 2026-10-28 with all 11 decisions below confirmed. This records agreed design behavior; implementation and its verification remain outstanding.

| Decision                        | Confirmed behavior                                                                                                                                                                                                                                                                             |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Buffered EF durability          | Buffered mode is explicitly best effort. A process crash can lose queued audits even after the business change committed. This is accepted; graceful flush does not provide crash durability or a guarantee of audit evidence for every committed change.                                      |
| Audit delivery failures         | Ordinary business actions continue under ReportOnly when storage is unavailable or the queue is full. Report the audit failure explicitly. Reject a new audit at capacity rather than silently evicting one already accepted.                                                                  |
| Record lifecycle                | Persist one immutable record when the owning root scope finalizes, including failed, canceled, and incomplete outcomes. No separate start/update persistence or in-progress audit view. Job dispatch remains part of its originating request.                                                  |
| Read capture | Once Requester audit behaviors are explicitly selected, commands are captured by default. Meaningful business reads require behavior-level selection; routine city-list refreshes/polling create no automatic records. HTTP read eligibility follows the same rule, without duplicate capture switches. |
| Business-first presentation     | Lead with the named business action, actor, resource, and outcome. Retain HTTP/request/handler/entity evidence as expandable supporting detail, preserving the stored relationships and occurrences.                                                                                           |
| Business value capture          | Capture request/handler identity automatically. Capture business values through explicit details/enrichers/scopes, with optional ChangeHistory deltas under their value policies. No automatic whole-parameter or result serialization.                                                        |
| ChangeHistory enrichment mode   | When enabled, default to bounded, policy-filtered snapshots plus source IDs. Keep ReferenceOnly available. Embedded evidence can outlive source history without relaxing source value restrictions.                                                                                            |
| Unconfirmed transaction outcome | Retain Unknown references without old/new values and exclude them from committed-change views. Handler success alone is not commit evidence; finalized records are not subsequently promoted.                                                                                                  |
| Retention lifecycle             | Both providers support configurable age-based archiving followed by removal. Automatic schedules require host configuration. Archived records leave the normal active query path. In-memory has hard combined count/byte bounds and supports the same options with short local/test intervals. |
| Archive access                  | Keep archived audits searchable through the same dashboard and FilterModel endpoint with explicit Active / Archived / All selection. Normal queries default to Active; broader scope preserves general host access rules.                                                                      |
| Manual scope outcome            | Disposing a scope without explicit completion/failure records Unknown and Incomplete. Use callback helpers for automatic classification; disposal alone never implies success.                                                                                                                 |

### 19.5 Confirmed composition refinement

Feature participation is explicit. The host selects auditing behaviors through each source feature's own builder, including Requester dispatch/handler behaviors and individual repository decorators. Ordinary feature setup and `AddAuditing` register no audit behaviors implicitly. Omitting a behavior opts out only that boundary, preserving surrounding HTTP/manual scopes and other selected features. Repeated registration is idempotent, and explicitly selected adapters work in either registration order.

`AddAuditing` owns audit policy, provider selection, HTTP options, contributors, and optional public endpoints. No central per-feature enablement list belongs on it. ChangeHistory exposes an explicit `WithAuditing<TSourceContext>()` bridge opt-in on its own builder; source capture and source context/entity configuration remain separate. Snapshot versus ReferenceOnly is audit-copy policy and does not enable that bridge.

The automatically discovered dashboard remains part of global `AddDashboard` composition. Explicit source-feature behavior selection introduces no additional dashboard opt-in or policies.

HTTP request blacklisting follows `RequestLoggingMiddleware`, including its exact default patterns, path extraction, and shared `MatchAny` behavior. The `/_bdk/*` pattern is excluded by default, and WeatherFiesta removes that pattern explicitly from its audit configuration to demonstrate dashboard business actions.

Fluent setup uses sane defaults throughout. Configuration callbacks are optional, parameterless EF registration enables the bounded buffered writer, and parameterless endpoint registration supplies the standard group path and contracts. Explicit configuration overrides selected values. Appendix A shows both explicit source-feature registrations and a handler adding its own business scope/details.

## 20. Repository references

- [Repository architecture](../../ARCHITECTURE.md)
- [WeatherFiesta host setup](../../examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Program.cs)
- [WeatherFiesta Core DbContext](../../examples/WeatherFiesta/WeatherFiesta.Infrastructure/Modules/Core/EntityFramework/CoreDbContext.cs)
- [WeatherFiesta city endpoints](../../examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Modules/Core/Endpoints/CityEndpoints.cs)
- [WeatherFiesta City Management dashboard](../../examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/Modules/Core/Dashboard/CoreDashboard.cs)
- [Add city and subscription command](../../examples/WeatherFiesta/WeatherFiesta.Application/Modules/Core/Commands/CityCreateCommand.cs)
- [Admin city creation command](../../examples/WeatherFiesta/WeatherFiesta.Application/Modules/Core/Commands/AdminCityCreateCommand.cs)
- [Admin city update command](../../examples/WeatherFiesta/WeatherFiesta.Application/Modules/Core/Commands/AdminCityUpdateCommand.cs)
- [WeatherFiesta City domain behavior](../../examples/WeatherFiesta/WeatherFiesta.Domain/Modules/Core/Model/City.cs)
- [WeatherFiesta city integration tests](../../examples/WeatherFiesta/WeatherFiesta.IntegrationTests/Modules/Core/Presentation/CityEndpointsTests.cs)
- [Existing entity audit-state marker](../../src/Domain/Model/Markers/IAuditable.cs)
- [Repository decorator behaviors ADR](../adr/0004-repository-decorator-behaviors.md)
- [Requester and Notifier ADR](../adr/0005-requester-notifier-mediator-pattern.md)
- [EF code-first migrations ADR](../adr/0007-entity-framework-core-code-first-migrations.md)
- [ChangeHistory specification](change-history.md)
- [Queueing specification](spec-application-queueing.md)
- [Filtering feature](../features-filtering.md)
- [FilterModel contract](../../src/Common.Abstractions/Filtering/FilterModel.cs)
- [FilterModel builder](../../src/Common.Abstractions/Filtering/FilterModelBuilder.cs)
- [Filtering specification builder](../../src/Domain/Filtering/SpecificationBuilder.cs)
- [Current-user accessor](../../src/Common.Abstractions/ICurrentUserAccessor.cs)
- [DevKit host environment](../../src/Common.Abstractions/Hosting/IDevKitHostEnvironment.cs)
- [Application correlation context](../../src/Common.Abstractions/CorrelationId.cs)
- [Correlation ID generator](../../src/Common.Utilities/CorrelationIdGenerator.cs)
- [Module context accessor](../../src/Common.Modules/Accessors/IModuleContextAccessor.cs)
- [HTTP module context accessor](../../src/Common.Modules/Accessors/IRequestModuleContextAccessor.cs)
- [Requester behavior contract](../../src/Common.Utilities/Requester/Behaviors/IPipelineBehavior.cs)
- [Requester request/handler contracts](../../src/Common.Utilities/Requester/Requester.cs)
- [EF repository registration](../../src/Infrastructure.EntityFramework/Repositories/ServiceCollectionExtensions.cs)
- [Repository behavior builder](../../src/Domain/Repositories/RepositoryBuilderContext.cs)
- [Generic repository contract](../../src/Domain/Repositories/IGenericRepository.cs)
- [Request logging middleware](../../src/Presentation.Web/Middleware/RequestLoggingMiddleware.cs)
- [Request logging options and default blacklist](../../src/Presentation.Web/Middleware/RequestLoggingOptions.cs)
- [Shared wildcard matching](../../src/Common.Abstractions/Extensions/Match.cs)
- [Job scheduler dispatch contract](../../src/Application.Jobs/IJobSchedulerService.cs)
- [Job dispatch result](../../src/Application.Jobs/Models/JobDispatchResult.cs)
- [Job dispatch options](../../src/Application.Jobs/Models/JobDispatchOptions.cs)
- [Job execution context](../../src/Common.Abstractions/Jobs/IJobExecutionContext.cs)
- [Requester database transaction behavior](../../src/Infrastructure.EntityFramework/Requester/DatabaseTransactionPipelineBehavior.cs)
- [ChangeHistory repository behavior](../../src/Infrastructure.EntityFramework/ChangeHistory/RepositoryChangeHistoryBehavior.cs)
- [ChangeHistory persistence model](../../src/Infrastructure.EntityFramework/ChangeHistory/ChangeHistoryEntry.cs)
- [ChangeHistory context contract](../../src/Infrastructure.EntityFramework/ChangeHistory/IChangeHistoryContext.cs)
- [ChangeHistory application contracts](../../src/Application.Entities/ChangeHistory/ChangeHistoryContracts.cs)
- [ChangeHistory dashboard provider](../../src/Presentation.Web.EntityFramework/ChangeHistory/Dashboard/DashboardPageProvider.cs)

## Appendix A. Auditing, Requester, repositories, and ChangeHistory

### A.1 Business use case and composition

A CustomerPortal user renames an existing customer and supplies a business reason. A business auditor later needs to establish who requested the rename, which customer it concerned, whether the handler ran, whether the repository update succeeded, what its Name changed from/to, and which request/correlation context connects the evidence.

This example combines Auditing, current Requester, an EF-backed generic repository, and the optional ChangeHistory feature configured for Customer. It uses the proposed Auditing APIs from this specification alongside the existing Requester/repository conventions. Authentication and general host access rules are already configured by the application. The customer exists before the request; creating or seeding it is separate application setup.

The example types are shown in the global namespace, so its derived keys are `RenameCustomerCommand` and `Customer.Rename`; namespaced application types include their namespace unless a configured business alias overrides it.

| Feature       | Application setup                                                                                        | Explicit participation                                                                                                                          |
| ------------- | -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| Requester | Register the handler and explicitly add dispatch/handler auditing behaviors with `WithBehavior(...)`. | Only the selected dispatch/handler boundaries join the HTTP root and observe invocation/results. |
| Repository | Register `IGenericRepository<Customer>` through the EF builder and explicitly add `RepositoryAuditingBehavior<Customer>`. | Records rough update actions only for this selected repository; others can omit the decorator. |
| ChangeHistory | Track Customer, select its source capture decorator/DbSet, and explicitly call `WithAuditing<AppDbContext>()` on its builder. | The selected bridge contributes captured deltas and commit evidence, with Snapshot copy mode by default. |
| Auditing      | Select the EF audit provider, HTTP boundary, and optional endpoints.                                     | Supplies the recorder, policies, bounded buffered writer, query services, and default endpoint options.                                          |
| Dashboard     | Enable the existing global dashboard.                                                                    | The Auditing page appears through normal discovery.                                                                                              |
| Current user  | Reuse the optional HTTP current-user accessor.                                                           | Auditing snapshots the authenticated actor at execution time. Without it, capture still works with the configured anonymous/service attribution. |

The application explicitly selects Requester's dispatch and handler audit behaviors, the Customer repository's audit decorator, and the ChangeHistory bridge. Other pipelines/repositories can omit them. The Auditing builder has no second feature list. The selected adapters can be registered before or after `AddAuditing`; merely registering a feature never opts it in.

### A.2 Service registration

```csharp
// Existing host setup supplies authentication, authorization, and endpoint discovery.
var services = builder.Services;

services.AddDbContext<AppDbContext>(options =>
	options.UseSqlServer(
		builder.Configuration.GetConnectionString("CustomerPortal")));

services.AddHttpContextAccessor();
services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();

services.AddRequester()
	.AddHandler<RenameCustomerCommand, Guid, RenameCustomerCommandHandler>()
	.WithBehavior(typeof(AuditingRequestBehavior<,>))
	.WithBehavior(typeof(AuditingRequestHandlerBehavior<,>))
	.WithBehavior(typeof(ValidationPipelineBehavior<,>));

services.AddChangeHistory(options => options
	.Track<Customer>()
		.CaptureDirectMutations(
			strategy: ChangeHistoryCaptureStrategy.RepositorySnapshot,
			mode: ChangeHistoryCaptureMode.Required))
	.WithAuditing<AppDbContext>();

services.AddEntityFrameworkRepository<Customer, AppDbContext>()
	.WithBehavior<RepositoryAuditingBehavior<Customer>>()
	.WithBehavior<RepositoryChangeHistoryBehavior<Customer, AppDbContext>>();

services.AddAuditing(options => options
	.ApplicationName("CustomerPortal"))
	.WithEntityFrameworkProvider<AppDbContext>()
	.WithHttpRequests()
	.WithEndpoints();

services.AddDashboard(options => options.Enabled());
services.AddProblemDetails();
```

Alongside the explicit participation choices, the only default value overridden here is the application name. Auditing is already enabled with ReportOnly failure handling. The EF provider already starts its buffered writer with batches of 100, a one-second interval, bounded queue, finite retries, and graceful-shutdown flush. The endpoint group already defaults to `/_bdk/api/auditing`. A custom contributor is unnecessary for this use case.

The two Requester auditing behaviors, `RepositoryAuditingBehavior<Customer>`, and ChangeHistory's proposed `WithAuditing<AppDbContext>()` are explicit audit opt-ins. `RepositoryChangeHistoryBehavior` separately enables source comparison/history persistence. The validation behavior remains an ordinary business choice. These selections live on the relevant feature builders, as specified in sections 8–9; none is inferred by `AddAuditing`.

The example chooses Required source capture deliberately: inability to obtain the configured baseline/change evidence can fail the business update under ChangeHistory's own policy. Auditing's ReportOnly mode governs audit delivery and does not weaken that source guarantee. Existing business behaviors and the ChangeHistory decorator must preserve the explicitly selected audit boundaries without duplicating them.

### A.3 Domain entity and audit-capable DbContext

```csharp
public sealed class Customer : Entity<Guid>
{
	private Customer() { }

	public string Name { get; private set; }

	public Result Rename(string name)
	{
		if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
		{
			return Result.Failure("A customer name of at most 200 characters is required.");
		}

		this.Name = name.Trim();
		return Result.Success();
	}
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
	: DbContext(options), IAuditingDbContext, IChangeHistoryContext
{
	public DbSet<Customer> Customers { get; set; }
	public DbSet<ChangeHistoryEntry> ChangeHistory { get; set; }
	public DbSet<AuditRecordEntity> AuditRecords { get; set; }
	public DbSet<AuditActionEntity> AuditActions { get; set; }
	public DbSet<AuditEntityChangeEntity> AuditEntityChanges { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<Customer>(entity =>
		{
			entity.HasKey(customer => customer.Id);
			entity.Property(customer => customer.Name)
				.IsRequired()
				.HasMaxLength(200);
		});

		modelBuilder.ConfigureAuditing();
	}
}
```

Create and apply the application's normal EF migration before running this setup. The context exposes Customers, ChangeHistory's existing `ChangeHistoryEntry` set, and the audit entity sets. ChangeHistoryEntry already carries its source table/key/index mapping; include that entity in the migration. `ConfigureAuditing()` configures only the audit entities. The compact two-/three-table requirement belongs to Auditing; ChangeHistory's own `__ChangeHistory_Entries` table remains separate.

The repository and its ChangeHistory decorator use the business context. For this ordinary update, source change rows are prepared before the repository's normal autosave and persist with that business save. The audit provider accesses its audit DbSets directly through a fresh context, independently of the handler's tracked context and business transaction.

Using one context type does not make the customer/source-history write and the audit write one transaction. Audit persistence still occurs asynchronously after finalization. This example needs neither an audit-entity repository nor the Active Entity feature. ChangeHistory is configured through its own services/decorator, with no duplicate context/entity list on the Auditing builder.

### A.4 Request details and handler

```csharp
public sealed class RenameCustomerCommand(
	Guid customerId,
	string name,
	string reason) : RequestBase<Guid>, IAuditDetails
{
	public Guid CustomerId { get; } = customerId;
	public string Name { get; } = name;
	public string Reason { get; } = reason;

	public AuditDescriptor DescribeAudit() => AuditDescriptorBuilder
		.For<RenameCustomerCommand>()
		.Reason(this.Reason)
		.Resource<Customer>(this.CustomerId)
		.Build();
}

public sealed class RenameCustomerCommandHandler(
	IGenericRepository<Customer> repository,
	IAuditContext auditing = null)
	: RequestHandlerBase<RenameCustomerCommand, Guid>
{
	protected override async Task<Result<Guid>> HandleAsync(
		RenameCustomerCommand request,
		SendOptions options,
		CancellationToken cancellationToken)
	{
		await using var renameScope = auditing?.BeginScope(s => s
			.For<Customer>(nameof(Customer.Rename)));

		try
		{
			if (renameScope?.IsRecording == true)
			{
				renameScope.AddResource<Customer>(request.CustomerId);
				renameScope.SetDetail("rename.reason", AuditValue.From(request.Reason));
			}

			var customer = await repository.FindOneAsync(
				request.CustomerId, cancellationToken: cancellationToken);

			if (customer is null)
			{
				renameScope?.Complete(AuditOutcome.Failed);
				return Result<Guid>.Failure("Customer not found.");
			}

			var renamed = customer.Rename(request.Name);
			if (renamed.IsFailure)
			{
				renameScope?.SetDetail("rename.decision", AuditValue.From("Rejected"));
				renameScope?.Complete(AuditOutcome.Rejected);
				return renamed.Wrap<Guid>();
			}

			renameScope?.SetDetail("rename.decision", AuditValue.From("Accepted"));
			await repository.UpdateAsync(customer, cancellationToken);

			renameScope?.Complete();
			return Result<Guid>.Success(customer.Id);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			renameScope?.Complete(AuditOutcome.Canceled);
			throw;
		}
		catch (Exception exception)
		{
			renameScope?.Fail(exception);
			throw;
		}
	}
}
```

The command uses Requester's existing request identity/timestamp and handler signature. With the Requester auditing behaviors explicitly registered, its approved command convention selects it by default. Its `IAuditDetails` builder derives the key/label from the command type and the resource type from Customer; only the reason and resource ID are supplied as business values. All supplied text still passes through capture/redaction/size policy.

The handler uses `BeginScope(s => s.For<Customer>(nameof(Customer.Rename)))`, which builds the options internally. Customer/member context derives the key and Customer: Rename label while retaining Business kind and JoinOrStart mode. It adds that child scope and records its typed customer resource, approved reason, and Accepted/Rejected decision. It completes or fails the scope on every path, including observed cancellation, so disposal never invents success. The optional `IAuditContext` parameter keeps the same handler usable without Auditing; disabled/suppressed capture supplies no-op scopes.

The explicitly registered Requester/repository behaviors still observe their own dispatch, invocation, and mutation boundaries. The handler's business scope is an additional action beneath the invoked handler; the repository update nests beneath that business scope. HTTP continues to own finalization. The handler does not set actor/correlation, calculate property diffs, or save audit records. Its internal lookup creates no repository-read action unless separately selected as a meaningful business read.

The requested new name is not copied into the audit merely because it is a command property. In this example, old/new Name values arrive through explicitly configured ChangeHistory, subject to its value policy and Auditing's restrictions. Customer.Rename mutates a property directly, so RepositorySnapshot obtains its persisted baseline through ChangeHistory's supported comparison path. The handler adds no manual before/after snapshots and Auditing implements no second diff engine.

Removing the optional ChangeHistory configuration still leaves the request, handler, and rough repository-action trail intact; the old/new evidence then disappears rather than being inferred from the command.

### A.5 HTTP pipeline and endpoint

```csharp
var app = builder.Build();

app.UseRouting();
app.UseRequestCorrelation();
app.UseAuthentication();
app.UseAuditing();
app.UseExceptionHandler();
app.UseAuthorization();

app.MapPut("/api/customers/{customerId:guid}/name",
	async (Guid customerId, RenameCustomerBody body,
		IRequester requester, CancellationToken cancellationToken) =>
		(await requester.SendAsync(
			new RenameCustomerCommand(customerId, body.Name, body.Reason),
			cancellationToken: cancellationToken))
		.MapHttpOk())
	.RequireAuthorization();

app.MapAuditingEndpoints();
app.MapEndpoints(); // Normal host mapping includes the enabled dashboard pages.

app.Run();

public sealed record RenameCustomerBody(string Name, string Reason);
```

The HTTP boundary runs after authentication and wraps the exception handler and authorization, following section 7.1. `UseRequestCorrelation()` establishes/reuses the application's correlation context before auditing consumes it. The optional current module is captured when the host already supplies module context; module registration is not required for this example.

The business route is outside the default request blacklist, including `/_bdk/*`. The audit read endpoints and default dashboard remain excluded and internally suppressed. If the application moves customer mutations beneath the dashboard prefix, it must adjust its own Auditing blacklist as in the WeatherFiesta proof; the standard defaults do not change.

### A.6 What happens for one rename

Assume an authenticated actor `user-42` renames customer `8b3d3124-2acd-45c4-913e-836beddf8497` from Northwind to Northwind Services, supplying the reason “Customer requested updated trading name.” Assume the current application correlation is `customer-rename-demo`.

1. HTTP middleware admits the non-blacklisted mutation and opens one root. It captures actor, application, correlation, route, and available module context.
2. Requester's explicitly registered auditing behavior joins that root and records dispatch of `RenameCustomerCommand`. The descriptor derives Rename customer and supplies the approved reason/customer reference.
3. The explicitly selected handler behavior records invocation. The handler opens its own derived Customer: Rename child scope, adds the reason/resource, and records the business decision. Its lookup runs normally without a separately selected business-read action.
4. The repository's explicitly registered auditing behavior records the update beneath the handler's business scope. Its ChangeHistory capture decorator obtains the persisted baseline, detects the Name delta, and includes the source history rows in the normal business save. The audit behavior surrounds source capture/write, so the evidence retains the original repository action identity. Internal comparison reads do not become unrelated business-read actions.
5. ChangeHistory's explicitly selected auditing bridge reports the source/change-set identity and policy-filtered delta. For this example's successful implicit atomic save without an enclosing transaction, the proposed commit hook confirms Committed evidence before root finalization. Handler success alone is not that confirmation.
6. Handler and dispatch behaviors classify their actual Results. HTTP completion supplies its observed status. The root sanitizes and freezes one immutable logical record with the allowed committed snapshot and source references.
7. The EF provider accepts that finalized document into its bounded queue. The request need not wait for an audit database write. The background writer later commits the complete header/action/change-evidence graph and makes it searchable.

The stored relationships for a successful operation are:

| Occurrence              | Parent                                 | Business evidence                                                                                                                                       |
| ----------------------- | -------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| HTTP root `R1`          | None                                   | Actor, application, optional module, correlation, route, observed status and outcome.                                                                   |
| Request dispatch `A1` | Root `R1` | Derived type key `RenameCustomerCommand`, label Rename customer, approved reason/resource, request identity, and dispatch outcome. |
| Handler invocation `A2` | Dispatch `A1`                          | Invoked handler identity and observed Result outcome.                                                                                                   |
| Business scope `B1` | Handler `A2` | Derived Customer: Rename label and type/member key, typed customer resource, approved reason, decision, and explicit outcome. |
| Repository update `A3` | Business scope `B1` | Customer type/ID, rough update operation, and observed repository outcome. |
| Change evidence `E1`    | Associated with repository action `A3` | Customer Name: Northwind → Northwind Services, source change-set/row IDs, capture strategy, and Committed persistence state, subject to value policies. |

`R1`, `A1`–`A3`, `B1`, and `E1` are readable example labels, not actual ID formats. E1 is evidence attached to A3, not an additional independently executed action. The real record retains separate audit/action/evidence and source-history identities. The dashboard leads with Rename customer and its actor/resource/outcome, with the HTTP, handler, named business scope, and repository evidence available as supporting detail. One root with nested occurrences remains one immutable logical record.

The repository update outcome remains distinct from the property diff and its commit evidence. ChangeHistory supplies the Name delta; Auditing embeds its bounded, policy-filtered committed snapshot with source IDs by default. If an enclosing transaction's outcome cannot be observed before finalization, retain an Unknown reference without old/new values instead. Rollback/failure never produces falsely committed change evidence, and a finalized record is not later promoted.

| Alternative execution                         | Expected evidence                                                                                                                                                 |
| --------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Customer is missing                           | Invoked handler returns failure; no repository update occurrence or invented change evidence.                                                                     |
| Domain validation rejects the name            | The handler's failure is retained; no update is reported.                                                                                                         |
| A pre-handler policy rejects the request      | Dispatch rejection is retained; no invoked handler or repository update is invented.                                                                              |
| Repository throws                             | The observed update/handler failure is retained and the business exception follows normal handling.                                                               |
| Same name is submitted again                  | The actual request and any executed repository update remain visible; ChangeHistory supplies no effective Name delta, so no property change is fabricated.        |
| Source ChangeHistory capture fails            | This example's Required source policy can fail the business update; the audit records the observed failure and does not invent committed deltas.                  |
| Business transaction rolls back               | Retain actual execution/rollback evidence without committed Name values. An unobservable external transaction yields an Unknown reference without old/new values. |
| Audit queue is full or storage delivery fails | Default ReportOnly preserves the business result and explicitly reports the audit-delivery failure; the trail must not claim confirmed persistence.               |
| Auditing is absent or disabled                | Source-feature behaviors pass through and business execution continues without audit detail collection.                                                           |

### A.7 Find and review the audit

After persistence, a business auditor opens the automatically discovered Auditing page and filters by actor, time, customer resource, action, or correlation. The same selection is available through `POST /_bdk/api/auditing/records/search` with a direct FilterModel body:

```json
{
  "page": 1,
  "pageSize": 50,
  "filters": [
    { "field": "UserId", "operator": "eq", "value": "user-42" },
    { "field": "CorrelationId", "operator": "eq", "value": "customer-rename-demo", "logic": "and" },
    { "field": "StartedUtc", "operator": "gte", "value": "2026-10-28T00:00:00Z", "logic": "and" },
    { "field": "StartedUtc", "operator": "lt", "value": "2026-10-29T00:00:00Z", "logic": "and" },
    {
      "field": "Actions",
      "operator": "any",
      "value": { "field": "Key", "operator": "eq", "value": "RenameCustomerCommand" },
      "logic": "and"
    }
  ],
  "orderings": [
    { "field": "StartedUtc", "direction": "desc" },
    { "field": "Id", "direction": "desc" }
  ]
}
```

Adjust the example UTC range to the operation being reviewed. The provider applies all predicates before counting/paging and returns `ResultPaged<AuditRecordSummary>`. Active is the default storage dataset. Opening the result shows the business action and nested evidence from A.6, including the committed Name change panel, without auditing the read itself. The same approved entity/property/commit-state filters are available through the shared search catalog.

For deterministic application verification, complete the real rename request, call provider `FlushAsync` with a bounded cancellation token, and assert that its acceptance boundary is fully persisted before searching. A queued receipt alone is not proof that the row is already searchable. Verify the customer value and committed source ChangeHistory row, then the matching source IDs and allowed Name delta in the persisted audit. Also verify failures/no-op outcomes, one logical record, no duplicate occurrences/evidence, and continued business behavior when audit delivery fails.

The handler already demonstrates a manual business scope and `SetDetail(...)`. A callback-based equivalent can use `IAuditContext.RunAsync` from section 6.2 for automatic Result/exception classification. Both approaches add to the explicitly selected feature boundaries without changing root ownership. A handler can also add an approved value to its current scope when a separate business step is unnecessary.

### A.8 Source history, audit snapshots, and variations

The example writes two kinds of evidence at different points:

| Evidence                                         | Owner and storage                                                                                   | Completion boundary                                       |
| ------------------------------------------------ | --------------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| Customer Name change                             | ChangeHistory's existing `ChangeHistoryEntry` rows in the business context.                         | The business save/observed transaction outcome.           |
| Business execution with embedded change evidence | Auditing's record/actions/entity-change groups, using its compact tables and JSON property details. | Root finalization, then buffered audit-store persistence. |

The audit snapshot is a permitted copy of ChangeHistory evidence, not a second source diff. Source history can later expire under its own retention while the embedded snapshot remains available under Auditing's archive/remove policy. Source navigation can become unavailable independently. A crash before the audit buffer is written can still lose the execution audit even though the business and its source ChangeHistory committed.

No ChangeHistory read/restore endpoint or separate ChangeHistory dashboard is required to display the allowed embedded snapshot in Auditing. Enabling those additional source-feature surfaces remains their own configuration and access concern. The audit dashboard uses its existing global access rules and capture/value-exposure settings.

Useful composition checks are:

- Omit `WithAuditing<AppDbContext>()` from the ChangeHistory builder: source history still persists, but no change evidence is copied into Auditing.
- Omit `RepositoryAuditingBehavior<Customer>` while retaining the selected ChangeHistory bridge: request/handler/manual actions remain, and permitted change evidence associates with the active business scope rather than an invented repository action. Other repositories can independently opt in or out.
- Omit either Requester auditing behavior: the corresponding dispatch or handler action disappears; HTTP, manual scopes, repository actions, and selected ChangeHistory evidence remain eligible.
- Remove ChangeHistory's services/capture decorator, bridge, and source mapping from an otherwise valid host: Requester/repository audits still work, without old/new values.
- Keep ChangeHistory configured but remove/disable Auditing: the customer/source-history behavior remains governed by ChangeHistory, and the optional audit bridge passes through.
- Configure Auditing's ChangeHistory copy policy as ReferenceOnly: retain source IDs, counts, capture/commit metadata, and policy markers without embedding Name values.
- Move audit storage to a dedicated audit DbContext: the Customer source context remains authoritative; its configured bridge still supplies evidence without re-registering it on the Auditing builder.
- Apply a redaction/exclusion/hash-only policy to Name in ChangeHistory: the audit copy preserves that restriction and cannot recover the original value from command parameters.
