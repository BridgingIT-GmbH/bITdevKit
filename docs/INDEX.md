# Documentation Index

This lists the feature documentation pages in `docs/features-*.md` plus the shared building-block and contributor guides that support them. If you are new to the kit, a good starting path is `Domain` -> `Results` -> `Requester and Notifier` -> `Modules` -> `Presentation Endpoints`.

## Common Infrastructure

- [Common Extensions](./common-extensions.md): Provides a package-level map of the extension helpers in `Common.Abstractions/Extensions`, grouped by collection, fluent composition, date/time, text, reflection, configuration, and async utility areas.
- [Common Serialization](./common-serialization.md): Documents the shared serializer abstraction, built-in serializers, default JSON conventions, and the converters that make devkit types serialize consistently.
- [Common Options Builders](./common-options-builders.md): Explains the lightweight fluent options-builder pattern reused across many devkit packages and how it differs from `Microsoft.Extensions.Options`.
- [Common Mapping](./common-mapping.md): Covers the devkit's boundary mapping abstraction, Mapster registration, result mapping helpers, and the explicit manual mapper fallback.
- [Common Caching](./common-caching.md): Covers the in-process cache abstraction, the default memory-cache provider, expiration settings, and prefix-based invalidation patterns.
- [Common Observability Tracing](./common-observability-tracing.md): Covers the activity decorator, tracing attributes, naming schemas, OpenTelemetry expectations, and the practical limits of the low-level tracing helper.

## Core Domain and Application

- [Domain](./features-domain.md): Introduces the core DDD building blocks in the devkit, including smart enumerations, strongly typed entity IDs, and fluent aggregate update patterns for enforcing invariants.
- [Domain Events](./features-domain-events.md): Explains how aggregates raise immutable domain events so side effects can be handled asynchronously and with low coupling.
- [Domain Repositories](./features-domain-repositories.md): Covers the repository abstractions, specifications, includes, paging, optimistic concurrency, and sequence support used to query and persist aggregates.
- [ActiveEntity](./features-domain-activeentity.md): Describes an optional active-record-style model where entities perform their own CRUD and query operations through pluggable providers and behaviors.
- [Rules](./features-rules.md): Documents the fluent rule engine for business validation, conditional checks, and collection processing that returns structured `Result` failures instead of exceptions.
- [Results](./features-results.md): Describes the devkit's `Result` pattern for explicit success and failure flows, typed errors, paged results, functional chaining, and HTTP mapping helpers.
- [Application Commands and Queries](./features-application-commands-queries.md): Shows how application use cases are modeled as command and query handlers dispatched through the request pipeline.
- [DataPorter](./features-application-dataporter.md): Provides a configurable import and export pipeline for formats like Excel, CSV, JSON, XML, and PDF with profiles, converters, validation, and streaming.

## Execution, Messaging, and Modularity

- [Requester and Notifier](./features-requester-notifier.md): Defines the in-process request/response and pub/sub abstractions with typed handlers, metadata, and configurable pipeline behaviors for cross-cutting concerns.
- [Messaging](./features-messaging.md): Provides transport-backed asynchronous messaging with broker abstractions, publisher and handler behaviors, and an outbox pattern for reliable delivery.
- [Modules](./features-modules.md): Explains how the devkit structures a modular monolith through self-contained modules that register their own services and endpoints while staying isolated.
- [Filtering](./features-filtering.md): Describes the JSON-based filter model for expressing filtering, sorting, paging, includes, and advanced predicates that translate into repository queries.
- [Extensions](./features-extensions.md): Documents fluent helper extensions for null-safe composition, conditional query building, sync and async chaining, and async-enumerable processing.

## Security and Access

- [Entity Permissions](./features-entitypermissions.md): Covers fine-grained entity authorization with type-level and instance-level permissions, inheritance, runtime grant management, and ASP.NET Core authorization integration.
- [Fake Identity Provider](./features-identityprovider.md): Documents the lightweight development identity provider for OAuth2 and OpenID Connect flows, JWT issuance, and test users and clients.

## Presentation and Host

- [Presentation Endpoints](./features-presentation-endpoints.md): Defines the composable Minimal API endpoint model with DI discovery, route grouping, authorization options, and `Result` to HTTP mapping helpers.
- [Console Commands](./features-presentation-console-commands.md): Adds a DI-driven command framework for non-interactive and interactive console workflows with argument binding, help output, and grouped commands.
- [CORS Configuration](./features-presentation-cors.md): Provides configuration-driven CORS registration with named policies, environment-specific defaults, wildcard subdomain support, and global or per-endpoint usage.
- [Exception Handling](./features-presentation-exception-handling.md): Standardizes exception-to-HTTP handling through configurable handler chains, fluent mappings, Problem Details responses, logging, and database exception support.
- [AppState](./features-presentation-appstate.md): Supplies a Blazor-oriented application state base class with persistence, debounced saving, change notifications, and undo/redo history.

## Storage, Scheduling, and Utilities

- [StartupTasks](./features-startuptasks.md): Defines an ordered startup pipeline for initialization work such as seeding, validation, warm-up tasks, and environment-specific bootstrap behavior.
- [JobScheduling](./features-jobscheduling.md): Wraps Quartz.NET in fluent ASP.NET Core integration for registering, persisting, monitoring, and controlling scheduled background jobs.
- [DocumentStorage](./features-storage-documents.md): Provides a typed document-store abstraction with pluggable providers and optional client behaviors for caching, retries, logging, and fault handling.
- [FileStorage](./features-storage-files.md): Exposes a provider-based file storage abstraction for file operations, metadata, compression, health checks, and cross-provider transfers with progress reporting.
- [Utilities](./features-utilities.md): Collects shared utility features such as time-provider abstractions and resiliency primitives including retries, throttling, circuit breaking, lightweight background work, and timeout helpers.

## Testing and Contributor Guides

- [Fake Authentication for Integration Tests](./testing-fake-authentication.md): Documents the lightweight ASP.NET Core fake-auth scheme used in integration tests, including setup, request headers, claim composition, and default-user behavior.
- [Testing Common XUnit](./testing-common-xunit.md): Documents the contributor-facing xUnit utilities for test setup, web host factories, fake time, traits, and `Result` assertion helpers.
