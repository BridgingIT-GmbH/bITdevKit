# Documentation Index

This lists the feature documentation pages in `docs/features-*.md` plus the shared building-block and contributor guides that support them. If you are new to the kit, a good starting path is `Domain` -> `Results` -> `Requester and Notifier` -> `Modules` -> `Presentation Endpoints`.

## Common Infrastructure

- [Common Extensions](./common-extensions.md): Reuse a broad set of shared helper extensions for composition, collections, async flows, and more.
- [Common Utilities](./common-utilities.md): Collect low-level utility building blocks for resiliency, activity helpers, ids, hashing, cloning, and more.
- [Common Serialization](./common-serialization.md): Share consistent serializer abstractions and JSON conventions across the devkit.
- [Common Options Builders](./common-options-builders.md): Use a lightweight fluent builder convention for feature-specific configuration objects.
- [Common Mapping](./common-mapping.md): Keep boundary mapping explicit and testable through a small mapper abstraction with Mapster integration.
- [Common Caching](./common-caching.md): Provide a small, shared in-process caching abstraction with a default memory-cache implementation.
- [Common Observability Tracing](./common-observability-tracing.md): Add lightweight `Activity`-based tracing around services without pulling in a full observability framework.

## Core Domain and Application

- [Domain](./features-domain.md): Build domain models with the core tactical patterns of DDD, from aggregates to typed ids and value objects.
- [Domain Events](./features-domain-events.md): Capture business-significant events in aggregates and publish side effects outside the domain model.
- [Event Sourcing](./features-event-sourcing.md): Persist aggregates as immutable event streams and rebuild state through replay and snapshots.
- [Domain Repositories](./features-domain-repositories.md): Access aggregates through type-safe repositories with rich querying, paging, and loading options.
- [Domain Specifications](./features-domain-specifications.md): Model reusable business criteria as composable specifications for queries and in-memory evaluation.
- [ActiveEntity](./features-domain-activeentity.md): Combine entity-centric CRUD convenience with provider-based persistence and Result-driven outcomes.
- [Domain Policies](./features-domain-policies.md): Encapsulate domain decisions as reusable, context-aware policy objects.
- [Rules](./features-rules.md): Express business rules as composable validations with consistent `Result`-based outcomes.
- [Results](./features-results.md): Represent success, failure, messages, and errors explicitly with immutable `Result` types.
- [Application Commands and Queries](./features-application-commands-queries.md): Separate application writes and reads into focused handlers with shared behaviors and clear boundaries.
- [DataPorter](./features-application-dataporter.md): Import and export structured data through a flexible, format-agnostic data transfer framework.

## Execution, Messaging, and Modularity

- [Requester and Notifier](./features-requester-notifier.md): Dispatch requests and notifications through handler pipelines with reusable cross-cutting behaviors.
- [Messaging](./features-messaging.md): Decouple producers and consumers with resilient asynchronous messaging and outbox-backed delivery.
- [Notifications](./features-notifications.md): Send and queue application notifications through transport-agnostic contracts with clear delivery boundaries.
- [Modules](./features-modules.md): Structure modular monoliths as independently configurable feature modules within one host.
- [Pipelines](./features-pipelines.md): Build structured, observable multi-step workflows with low-friction defaults.
- [Filtering](./features-filtering.md): Simplify complex entity queries with a unified filtering solution.
- [Extensions](./features-extensions.md): Use focused LINQ and helper extensions to write cleaner, more expressive application code.

## Security and Access

- [Entity Permissions](./features-entitypermissions.md): Enforce fine-grained, entity-level authorization with fluent configuration and runtime evaluation.
- [Fake Identity Provider](./features-identityprovider.md): Documents the lightweight development identity provider for OAuth2 and OpenID Connect flows, JWT issuance, and test users and clients.

## Presentation and Host

- [Presentation Endpoints](./features-presentation-endpoints.md): Define minimal API endpoints as modular classes with automatic discovery and mapping.
- [Console Commands](./features-presentation-console-commands.md): Expose operational and administrative actions through discoverable console commands and an interactive shell.
- [CORS Configuration](./features-presentation-cors.md): Configure browser cross-origin access through fluent, settings-driven CORS policies.
- [Exception Handling](./features-presentation-exception-handling.md): Convert exceptions into consistent Problem Details responses with configurable handlers and mappings.
- [AppState](./features-presentation-appstate.md): Manage Blazor application state with persistence, history, and change notifications.

## Storage, Scheduling, and Utilities

- [StartupTasks](./features-startuptasks.md): Run application startup work in a structured, observable, and dependency-aware way.
- [JobScheduling](./features-jobscheduling.md): Schedule and run background jobs with flexible timing, DI integration, and operational visibility.
- [DocumentStorage](./features-storage-documents.md): Store and query JSON-like documents through a simple, provider-agnostic abstraction.
- [FileStorage](./features-storage-files.md): Read, write, move, and monitor files through extensible storage providers and behaviors.
- [Storage Monitoring](./features-storage-monitoring.md): Detect file changes and process storage events through configurable monitoring pipelines.
- [Log Entries](./features-log-entries.md): Query, stream, export, and manage persisted application logs through a stable application API.

## Testing and Contributor Guides

- [Fake Authentication for Integration Tests](./testing-fake-authentication.md): Simulate authenticated ASP.NET Core requests in integration tests with a lightweight fake-auth scheme.
- [Testing Common XUnit](./testing-common-xunit.md): Reuse shared xUnit test helpers for setup, web hosts, fake time, traits, and `Result` assertions.
