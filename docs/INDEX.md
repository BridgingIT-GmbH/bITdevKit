<img class="bdk-doc-icon" src="assets/features/dashboard.svg" width="32" height="32" alt="" align="right">

# Documentation Index

This lists the feature documentation pages in `docs/features-*.md` plus the shared building-block and contributor guides that support them. If you are new to the kit, a good starting path is `Domain` -> `Results` -> `Requester and Notifier` -> `Modules` -> `Presentation Endpoints`.

Browse the [feature icon catalog](./site/feature-icons.md) for the complete set and SVG downloads.

## Common Infrastructure

- <img class="bdk-doc-icon" src="assets/features/common-extensions.svg" width="20" height="20" alt=""> [Common Extensions](./common-extensions.md): Reuse a broad set of shared helper extensions for composition, collections, async flows and more.
- <img class="bdk-doc-icon" src="assets/features/common-utilities.svg" width="20" height="20" alt=""> [Common Utilities](./common-utilities.md): Collect low-level utility building blocks for resiliency, activity helpers, ids, hashing, cloning and more.
- <img class="bdk-doc-icon" src="assets/features/serialization.svg" width="20" height="20" alt=""> [Common Serialization](./common-serialization.md): Share consistent serializer abstractions and JSON conventions across the devkit.
- <img class="bdk-doc-icon" src="assets/features/options-builders.svg" width="20" height="20" alt=""> [Common Options Builders](./common-options-builders.md): Use a lightweight fluent builder convention for feature-specific configuration objects.
- <img class="bdk-doc-icon" src="assets/features/mapping.svg" width="20" height="20" alt=""> [Common Mapping](./common-mapping.md): Keep boundary mapping explicit and testable through a small mapper abstraction with Mapster integration.
- <img class="bdk-doc-icon" src="assets/features/caching.svg" width="20" height="20" alt=""> [Common Caching](./common-caching.md): Provide a small, shared in-process caching abstraction with a default memory-cache implementation.
- <img class="bdk-doc-icon" src="assets/features/tracing.svg" width="20" height="20" alt=""> [Common Observability Tracing](./common-observability-tracing.md): Add lightweight `Activity`-based tracing around services without pulling in a full observability framework.
- <img class="bdk-doc-icon" src="assets/features/metrics.svg" width="20" height="20" alt=""> [Metrics](./features-metrics.md): Record devkit and application measurements through the standard .NET metrics runtime.
- <img class="bdk-doc-icon" src="assets/features/profiling.svg" width="20" height="20" alt=""> [Profiling](./features-profiling.md): Collect bounded runtime snapshots and compute deterministic CPU, memory, allocation, and GC evidence for local development.
- <img class="bdk-doc-icon" src="assets/features/utilities.svg" width="20" height="20" alt=""> [Utilities](./features-utilities.md): Group shared application-layer utility services for startup tasks, log-entry contracts and time-provider registration.

## Core Domain and Application

- <img class="bdk-doc-icon" src="assets/features/domain.svg" width="20" height="20" alt=""> [Domain](./features-domain.md): Build domain models with the core tactical patterns of DDD, from aggregates to typed ids and value objects.
- <img class="bdk-doc-icon" src="assets/features/domain-events.svg" width="20" height="20" alt=""> [Domain Events](./features-domain-events.md): Capture business-significant events in aggregates and publish side effects outside the domain model.
- <img class="bdk-doc-icon" src="assets/features/event-sourcing.svg" width="20" height="20" alt=""> [Event Sourcing](./features-event-sourcing.md): Persist aggregates as immutable event streams and rebuild state through replay and snapshots.
- <img class="bdk-doc-icon" src="assets/features/change-history.svg" width="20" height="20" alt=""> [ChangeHistory](./features-domain-change-history.md): Record property-level entity changes, query grouped change sets and restore selected values through explicit policies.
- <img class="bdk-doc-icon" src="assets/features/repositories.svg" width="20" height="20" alt=""> [Domain Repositories](./features-domain-repositories.md): Access aggregates through type-safe repositories with rich querying, paging and loading options.
- <img class="bdk-doc-icon" src="assets/features/specifications.svg" width="20" height="20" alt=""> [Domain Specifications](./features-domain-specifications.md): Model reusable business criteria as composable specifications for queries and in-memory evaluation.
- <img class="bdk-doc-icon" src="assets/features/active-entity.svg" width="20" height="20" alt=""> [ActiveEntity](./features-domain-activeentity.md): Combine entity-centric CRUD convenience with provider-based persistence and Result-driven outcomes.
- <img class="bdk-doc-icon" src="assets/features/policies.svg" width="20" height="20" alt=""> [Domain Policies](./features-domain-policies.md): Encapsulate domain decisions as reusable, context-aware policy objects.
- <img class="bdk-doc-icon" src="assets/features/rules.svg" width="20" height="20" alt=""> [Rules](./features-rules.md): Express business rules as composable validations with consistent `Result`-based outcomes.
- <img class="bdk-doc-icon" src="assets/features/results.svg" width="20" height="20" alt=""> [Results](./features-results.md): Represent success, failure, messages and errors explicitly with immutable `Result` types.
- <img class="bdk-doc-icon" src="assets/features/commands-queries.svg" width="20" height="20" alt=""> [Application Commands and Queries](./features-application-commands-queries.md): Separate application writes and reads into focused handlers with shared behaviors and clear boundaries.
- <img class="bdk-doc-icon" src="assets/features/application-events.svg" width="20" height="20" alt=""> [Application Events](./features-application-events.md): Publish and handle application-layer events through `INotifier` with explicit `Result`-based outcomes.
- <img class="bdk-doc-icon" src="assets/features/dataporter.svg" width="20" height="20" alt=""> [DataPorter](./features-application-dataporter.md): Import and export structured data through a flexible, format-agnostic data transfer framework.

## Execution, Messaging and Modularity

- <img class="bdk-doc-icon" src="assets/features/requester.svg" width="20" height="20" alt=""> <img class="bdk-doc-icon" src="assets/features/notifier.svg" width="20" height="20" alt=""> [Requester and Notifier](./features-requester-notifier.md): Dispatch requests and notifications through handler pipelines with reusable cross-cutting behaviors.
- <img class="bdk-doc-icon" src="assets/features/messaging.svg" width="20" height="20" alt=""> [Messaging](./features-messaging.md): Decouple producers and consumers with resilient asynchronous messaging and outbox-backed delivery.
- <img class="bdk-doc-icon" src="assets/features/queueing.svg" width="20" height="20" alt=""> [Queueing](./features-queueing.md): Process single-consumer work items through in-process or durable queue brokers with retained-message inspection, retry/archive controls and queue/type pause-resume management.
- <img class="bdk-doc-icon" src="assets/features/broadcasting.svg" width="20" height="20" alt=""> [Broadcasting](./features-broadcasting.md): Send short-lived typed notifications to every active application node in one or more scopes.
- <img class="bdk-doc-icon" src="assets/features/notifications.svg" width="20" height="20" alt=""> [Notifications](./features-notifications.md): Send and queue application notifications through transport-agnostic contracts with clear delivery boundaries.
- <img class="bdk-doc-icon" src="assets/features/modules.svg" width="20" height="20" alt=""> [Modules](./features-modules.md): Structure modular monoliths as independently configurable feature modules within one host.
- <img class="bdk-doc-icon" src="assets/features/pipelines.svg" width="20" height="20" alt=""> [Pipelines](./features-pipelines.md): Build structured, observable multi-step workflows with low-friction defaults.
- <img class="bdk-doc-icon" src="assets/features/orchestrations.svg" width="20" height="20" alt=""> [Orchestrations](./features-orchestrations.md): Model long-running stateful workflows with durable state, signals, timers, query services and operational endpoints.
- <img class="bdk-doc-icon" src="assets/features/filtering.svg" width="20" height="20" alt=""> [Filtering](./features-filtering.md): Simplify complex entity queries with a unified filtering solution.
- <img class="bdk-doc-icon" src="assets/features/extensions.svg" width="20" height="20" alt=""> [Extensions](./features-extensions.md): Use focused LINQ and helper extensions to write cleaner, more expressive application code.

## Security and Access

- <img class="bdk-doc-icon" src="assets/features/authentication.svg" width="20" height="20" alt=""> [Presentation Authentication](./features-presentation-authentication.md): Validate JWT bearer tokens and expose the current HTTP principal through `ICurrentUserAccessor`.
- <img class="bdk-doc-icon" src="assets/features/entity-permissions.svg" width="20" height="20" alt=""> [Entity Permissions](./features-entitypermissions.md): Enforce fine-grained, entity-level authorization with fluent configuration and runtime evaluation.
- <img class="bdk-doc-icon" src="assets/features/identity-provider.svg" width="20" height="20" alt=""> [Fake Identity Provider](./features-identityprovider.md): Documents the lightweight development identity provider for OAuth2 and OpenID Connect flows, JWT issuance and test users and clients.

## Presentation and Host

- <img class="bdk-doc-icon" src="assets/features/host.svg" width="20" height="20" alt=""> [Presentation Host](./features-presentation.md): Configure ASP.NET Core applications through the DevKit web application builder and package-owned starter extensions.
- <img class="bdk-doc-icon" src="assets/features/configuration.svg" width="20" height="20" alt=""> [Presentation Configuration](./features-presentation-configuration.md): Compose JSON, Azure, and environment-variable configuration providers in the devkit host order.
- <img class="bdk-doc-icon" src="assets/features/logging.svg" width="20" height="20" alt=""> [Presentation Logging](./features-presentation-logging.md): Configure Serilog once for a devkit host and change its global minimum level at runtime.
- <img class="bdk-doc-icon" src="assets/features/correlation-ids.svg" width="20" height="20" alt=""> [Presentation Correlation IDs](./features-presentation-correlationid.md): Correlate use cases across inbound HTTP, async execution, logs, activities, outbound HTTP and supported transport boundaries.
- <img class="bdk-doc-icon" src="assets/features/endpoints.svg" width="20" height="20" alt=""> [Presentation Endpoints](./features-presentation-endpoints.md): Define minimal API endpoints as modular classes with automatic discovery and mapping.
- <img class="bdk-doc-icon" src="assets/features/cli.svg" width="20" height="20" alt=""> [DevKit CLI](./features-cli.md): Use the `bdk` command-line tool for host discovery, workspace-scoped host selection and host Console Command forwarding.
- <img class="bdk-doc-icon" src="assets/features/mcp.svg" width="20" height="20" alt=""> [DevKit MCP](./features-cli-mcp.md): Use `bdk mcp` from local coding agents to inspect and operate running DevKit applications.
- <img class="bdk-doc-icon" src="assets/features/mcp-clients.svg" width="20" height="20" alt=""> [MCP Client Configuration](./features-cli-mcp-clients.md): Configure common MCP clients to start the `bdk mcp` STDIO server.
- <img class="bdk-doc-icon" src="assets/features/dashboard.svg" width="20" height="20" alt=""> [Presentation Dashboard](./features-presentation-dashboard.md): Host modular developer dashboard pages with RazorSlice content, grouped navigation and live cards.
- <img class="bdk-doc-icon" src="assets/features/console-commands.svg" width="20" height="20" alt=""> [Console Commands](./features-presentation-console-commands.md): Expose operational and administrative actions through discoverable console commands and an interactive shell.
- <img class="bdk-doc-icon" src="assets/features/cors.svg" width="20" height="20" alt=""> [CORS Configuration](./features-presentation-cors.md): Configure browser cross-origin access through fluent, settings-driven CORS policies.
- <img class="bdk-doc-icon" src="assets/features/exception-handling.svg" width="20" height="20" alt=""> [Exception Handling](./features-presentation-exception-handling.md): Convert exceptions into consistent Problem Details responses with configurable handlers and mappings.
- <img class="bdk-doc-icon" src="assets/features/app-state.svg" width="20" height="20" alt=""> [AppState](./features-presentation-appstate.md): Manage Blazor application state with persistence, history and change notifications.

## Storage, Scheduling and Utilities

- <img class="bdk-doc-icon" src="assets/features/startup-tasks.svg" width="20" height="20" alt=""> [StartupTasks](./features-startuptasks.md): Run application startup work in a structured, observable and dependency-aware way.
- <img class="bdk-doc-icon" src="assets/features/jobs.svg" width="20" height="20" alt=""> [Jobs](./features-jobs.md): Schedule durable background work through code-first jobs, triggers, batches, history and source-level integrations.
- <img class="bdk-doc-icon" src="assets/features/job-scheduling.svg" width="20" height="20" alt=""> [JobScheduling](./features-jobscheduling.md): Legacy Quartz-backed scheduling feature for existing applications that have not migrated yet.
- <img class="bdk-doc-icon" src="assets/features/storage-blobs.svg" width="20" height="20" alt=""> [BlobStorage](./features-storage-blobs.md): Store binary content through Result-native, stream-first, named blob clients.
- <img class="bdk-doc-icon" src="assets/features/storage-documents.svg" width="20" height="20" alt=""> [DocumentStorage](./features-storage-documents.md): Store typed documents with metadata, ETags, integrity, transforms, expiration/retention, named clients, and managed provider resources.
- <img class="bdk-doc-icon" src="assets/features/storage-permalinks.svg" width="20" height="20" alt=""> [StoragePermalinks](./features-storage-permalinks.md): Give Blob, Document, and File Storage resources stable opaque download links with optional expiration.
- <img class="bdk-doc-icon" src="assets/features/storage-files.svg" width="20" height="20" alt=""> [FileStorage](./features-storage-files.md): Read, write, move and monitor files through extensible storage providers and behaviors.
- <img class="bdk-doc-icon" src="assets/features/storage-monitoring.svg" width="20" height="20" alt=""> [Storage Monitoring](./features-storage-monitoring.md): Detect file changes and process storage events through configurable monitoring pipelines.
- <img class="bdk-doc-icon" src="assets/features/log-entries.svg" width="20" height="20" alt=""> [Log Entries](./features-log-entries.md): Query, stream, export and manage persisted application logs through a stable application API.

## Testing and Test Utilities

- <img class="bdk-doc-icon" src="assets/features/fake-authentication.svg" width="20" height="20" alt=""> [Fake Authentication for Integration Tests](./testing-fake-authentication.md): Simulate authenticated ASP.NET Core requests in integration tests with a lightweight fake-auth scheme.
- <img class="bdk-doc-icon" src="assets/features/testing.svg" width="20" height="20" alt=""> [Testing Common XUnit](./testing-common-xunit.md): Reuse shared xUnit test helpers for setup, web hosts, fake time, traits and `Result` assertions.
