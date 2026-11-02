# Changelog

This changelog includes unreleased changes on `main` and the full tagged release history from the `main` branch.

## [10.0.120] - 2026-11-02

- Added `Application.Auditing` for finite business executions with explicit context, nested actions, resource history, retention, diagnostics, and optional HMAC-SHA-256 integrity. `AddAuditing()` uses bounded in-memory storage by default; Entity Framework persistence requires client-owned audit/control tables and migrations.
- Added opt-in auditing contributors for execution, messaging, repositories, storage, and HTTP boundaries, plus `Presentation.Web.Auditing` endpoints, console commands, dashboard review, and NDJSON/CSV exports. CEF export requires explicit formatter registration.
- Added `NdjsonReader` and `NdjsonWriter` for bounded streaming serialization, plus `AddNdjsonStreaming()`, `NdjsonHttpResult`, `MapHttpNdjson()`, and `WithNdjson<T>()` for HTTP streaming with matching OpenAPI metadata.
- Added an Entities dashboard that discovers repository and ActiveEntity registrations, with filtering, incremental loading, detail views, exports, property masking, and configurable references. `AddEntitiesDashboard()` configures access and per-entity write options; editing requires an explicit policy or role configuration.
- Fixed Change History dashboard discovery and read/restore operations to work independently of optional REST endpoint registration. Added `AddChangeHistoryDashboard<TEntity, TContext>()` for explicit settings and Entities dashboard links.
- Added filtered, paged `FilterModel` searches for jobs, orchestrations, messaging, queueing, notification email outboxes, log entries, and broadcasting nodes. Added search endpoints and separate `AddBroadcastingManagementEndpoints()` registration for broadcasting management.
- Added `Infrastructure.FileSystem` with blob and document providers registered through `WithFileSystemClient()`, configurable durable or buffered writes, bounded queries and retention, and cross-process coordination. SMB and NFS deployments require environment-specific qualification.
- Changed local file storage and monitoring registration. Breaking change: `LocalFileStorageProvider` and `LocalLocationHandler` moved from `Application.Storage` to the `BridgingIT.DevKit.Infrastructure.FileSystem` namespace and package. Add the new package and update imports; the file-storage and monitoring `UseLocal()` methods are now infrastructure extension methods.
- Fixed blob and document retry behaviors to recognize `ITransientStorageError` and avoid retrying indeterminate mutations.
- Improved Entity Framework blob reads for blobs up to `ChunkSize` with one compiled query, earlier lease release, and fewer buffer copies in `DownloadBytesAsync`. Missing chunks now fail with `InvalidDataException`.
- Added `DownloadBytesManyAsync`, the optional `IBlobStoreBatchDownloadProvider`, and `BlobStoreOptions.DownloadBatchSize`, defaulting to 25. The Entity Framework provider batches small-blob reads while retaining per-key client behaviors; other providers read key by key.
- Changed Entity Framework blob row ids to time-ordered GUIDs to reduce index fragmentation. Existing ids remain valid and no migration is required.
- Added document `GetManyAsync` for exact reads with per-key results in input order, `IDocumentStoreBatchGetProvider`, and `DocumentStoreOptions.GetManyBatchSize`, defaulting to 25. The Entity Framework provider uses compiled queries with up to `GetManyMaxParallelism` concurrent batches, defaulting to 4.
- Added `IDocumentStoreBulkWriteProvider` support for `UpsertManyAsync` and `DeleteManyAsync`. The Entity Framework provider loads existing rows and commits each batch together, preserving the stop-at-first-failure contract through a per-operation fallback when the batch commit fails.
- Changed Entity Framework document row ids to use provider-generated values and single-document reads to use a compiled query with fewer payload copies. Existing row ids remain valid.
- Added the `(TypeHash, PartitionKey, RowKey)` index to `StorageDocument` and changed full-type continuation queries to support index seeks. Added `ModelBuilder.ApplyDocumentStoreConfiguration()` for a filtered expiration index. Generate a migration to apply these schema changes.
- Improved document metrics coverage across client operations, with `document.operation.failures`, `document.bytes`, `document.bytes_per_second`, and `DocumentInfo.ContentLength` for logical serialized size. Added `blobstorage_bytes_per_second` for successful non-empty transfers and throughput-specific histogram buckets.
- Added remembered Explorer and Diagnostics views to storage dashboards, with live operation, failure, latency, and transfer charts. Added file-storage diagnostics and configurable storage load tests with stop and cleanup controls.
- Fixed messaging and queueing dashboard type labels to omit assembly metadata, and improved their layouts at narrow widths.
- Changed dashboard overview cards to use sidebar feature icons, removed the redundant Broadcasting registrations header, and added shared view navigation for dashboard pages.
- Updated MudBlazor to 9.12.0, Scalar.AspNetCore to 2.18.7, and System.Security.Cryptography.Pkcs to 10.0.14.

## [10.0.119] - 2026-10-27

- Changed NuGet package publishing to run for version tags only, so preview packages are no longer published from `main`. Branch publishing can be turned back on with the `PUBLISHABLE_BRANCHES` repository variable, and a tag can be built and published again with a manual run of the CI workflow.

## [10.0.118] - 2026-10-27

- Improved NuGet package publishing so releases no longer fail on nuget.org push limits: tagged commits are published once by the tag run, versions that already exist are skipped, throttled pushes retry with backoff, and a quota-exceeded response waits before the job stops with a summary of what was pushed.

## [10.0.117] - 2026-10-27

- Updated the MCP client configuration documentation for current VS Code and GitHub Copilot: the workspace-root `.mcp.json` with `mcpServers` (enabled through `chat.mcp.workspaceRootConfig.enabled`), Visual Studio through `.vs/mcp.json`, and the `diagnostics,operations,admin` toolsets in every documented configuration.

## [10.0.116] - 2026-10-27

- Updated the package baseline to ASP.NET Core, EF Core and Microsoft.Extensions 10.0.14, OpenTelemetry 1.20.1, MailKit 4.18.2, Azure Cosmos 3.63.3, and Azure App Configuration 8.6.1.

## [10.0.115] - 2026-10-27

- Added CBOR serialization (`CborSerializer`) for compact binary payloads, with the `[CborConstructor]` attribute for constructor-based mapping and the `IBufferSerializer` contract for serializers that work on complete buffers. `System.Formats.Cbor` is now a dependency of `Common.Serialization`.
- Changed the dashboard sidebar to show the feature SVG icons, falling back to the previous Bootstrap icon for items without one.

## [10.0.114] - 2026-10-20

- Added the `project_mcp_tools` guidance topic to `bdk_guidance_get`, explaining how to add project-owned MCP operations: capability metadata, argument schemas, toolset selection, bounded responses, registration, and verification with `bdk_project_operations` and `bdk_project_call`.

## [10.0.113] - 2026-10-19

- Added `AddLogEntryServices<TContext>(...)` to register the log entry service, maintenance queue, and background maintenance service with fluent options (including enabling or disabling them per environment). It returns a builder context for exposing endpoints.
- Added the matte-black dashboard theme and improved the accessibility of the theme picker.
- Fixed hosted services of profiling and log entry maintenance starting during build-time OpenAPI generation.
- Updated the .NET SDK baseline to 10.0.402 and refreshed packages, including OpenTelemetry 1.20.0. `PDFsharp-MigraDoc` stays pinned to 6.2.4 because 6.5 requires AGPL or commercial license activation.

- Changed the retry, timeout, circuit-breaker, and chaos behaviors of commands, queries, the requester, startup tasks, message handlers, job scheduling, jobs, and file storage to run on `Microsoft.Extensions.Resilience` pipelines. The direct `Polly`, `Polly.Contrib.Simmy`, and `Microsoft.Extensions.Http.Polly` references are removed because direct Polly references fall under the new Polly Open Source Maintenance Fee. Timeouts stay pessimistic, `BackoffExponential` now really doubles the delay between attempts, and circuit breakers open after the configured number of consecutive failures within one execution (at least 2, break duration at least 500 ms).
- Note: `Microsoft.Extensions.Resilience` brings in `Microsoft.Extensions.Telemetry.Abstractions`, which replaces the standard `[LoggerMessage]` source generator with the stricter `Microsoft.Gen.Logging` one in consuming projects. The repository keeps the standard generator through its `Directory.Build.targets`; consumers who want the same can copy that file's property and `RemoveTelemetryAbstractionsAnalyzers` target into their build.
- Fixed the file storage retry behavior so operations returning a typed result (`ReadFileAsync`, `ListFilesAsync`, `GetFileMetadataAsync`, and others) retry retryable failures with exponential backoff. Previously the result was rewrapped as a success, so those operations never retried.
- Added Operation Profiling, which records how a single execution spends its time in named segments. It works without HTTP, Runtime Profiling, or Broadcast, and captured data is aggregated per operation rather than stored as raw traces.
- Added HTTP request profiling that captures request lifecycle, route, status, and response size. Every request is captured by default, with configurable path blacklisting, prefix stripping, and sampling (all requests, probability, or a rate limit).
- Changed HTTP profiling exclusions and sampling skips to leave business operation recording independent. Added `Whitelist(...)` to allow selected blacklisted HTTP paths through the configured sampling policy; profiling internals remain explicitly suppressed.
- Added optional profiling behaviors for jobs, pipelines, orchestrations, repositories, Active Entities, Requester, Notifier, messaging and queue handlers. They record independent operations or nested segments and run unchanged when profiling is not registered.
- Added profiling for file, document and blob storage, grouped by storage location, document type or container instead of individual paths and keys.
- Added profiling storage with in-memory and Entity Framework providers, bounded background writes, retention limits, and clearing of Runtime and Operation history, either fully or by time range.
- Added Operations and Requests dashboard views with slow, recent, and by-count lists, grouping, filters, per-operation segment breakdowns, wall-time bars, and Runtime overlays correlated by process and time.
- Added Timeline and Insights dashboard views for activity by operation kind, latency hotspots, failure rates and aggregate segment costs, with saved filters and access to execution details and comparison dialogs.
- Added filtered group deletion to Operations and Requests, removing all matching retained executions and their owned segments.
- Added read-only MCP profiling query, details, insights and comparison tools using the runtime's shared services. Correlation investigations include profiling recordings, and profiling guidance explains recording and segment usage during coding.
- Fixed a Broadcasting shutdown race that could throw when the same host was stopped concurrently.
- Changed Operations and Requests to append older recordings with **Load more** and prepend live recordings with **Tail**, preserving loaded groups and filters. Manual refresh remains; the interval selector was removed. The centered Load more control uses the current row choice (50, 100, 250, or 500) without restarting paging.
- Changed Runtime profiling deletion and clear confirmations to compact dashboard dialogs with inline errors and retry support.
- Changed browser confirmations and alerts across the dashboard to shared, themed dialogs, including purge actions, document and permalink deletion, and Change History restore errors.
- Changed the profiling Timeline to update automatically while following now, without a refresh-interval selector. Fixed historical ranges use manual refresh.
- Changed the dashboard MCP page to open from a robot icon in the header bar instead of a sidebar item. The icon turns accent-coloured while an MCP server is connected to the runtime and updates live.
- Changed profiling registration to `AddProfiling(...).WithRuntimeProfiling(...).WithOperationProfiling(...).WithRequestProfiling(...)`. Request profiling requires Operation Profiling.
- Changed: renamed the Runtime Profiling API, options, store types, and Entity Framework registration (`WithRuntimeProfiling`, Runtime-prefixed types). Runtime dashboard pages moved under `/profiling/runtime` and console commands under `profiling runtime`.

## [10.0.112] - 2026-09-03

- Added startup diagnostics that write deterministic debug-level summaries of registered application and DevKit system endpoints.
- Fixed module state leaking across hosts. Each host now has its own module registry, reuses the same module instance for registration, middleware, and endpoint mapping, and manages activity logging correctly when multiple hosts run together.

## [10.0.111] - 2026-09-02

- Added broadcasting for in-process and distributed delivery, with HTTP transport, node registration, dashboard and console controls, and correlation ID propagation.
- Added the profiling dashboard and console commands for collecting, inspecting, archiving, and controlling performance snapshots.
- Added Entity Framework change-history tracking with operational endpoints and dashboard support.
- Added bounded high-volume blob uploads with configurable concurrency, backpressure, batch EF chunk flushing, and telemetry.
- Expanded metrics across jobs, messaging, queueing, orchestrations, repositories, and storage clients.
- Added lower- and uppercase alphanumeric key generators.
- Updated SharpCompress integration for its 1.0 API so archive compression continues to work.
- Removed the DinnerFiesta example application.

## [10.0.110] - 2026-08-03

- public documentation restyled (multiple themes) and updated for clarity, accuracy, and consistency.
- Added blob storage support with upload and download capabilities for binary content, including content-type-aware transfer behavior and reusable storage helpers.
- Added storage permalink support for blobs, files, and documents, with registry, metrics, and change-queue integration to make stored content easier to reference and manage.
- Improved storage paging and document/file storage behavior for more predictable handling of larger content sets.
- Refined storage dashboard experience with clearer UI elements and aligned storage page titles for a more consistent experience.
- added storage console commands for blob, file, and document storage operations.
- Redesigned the preview Entity Framework bulk-insert feature as the Domain-owned `IEntityBulkInserter<TEntity>` capability with independent registration, explicit ordered decorators, root-table mapping preflight, and one-operation transactions.
- Moved the preview `IEntityBulkInserter<TEntity>` contract from `Infrastructure.EntityFramework` to `Domain.Repositories` without a compatibility alias. This is an intentional preview-breaking namespace change; the feature had no known consumers.
- Native bulk inserts no longer infer repository decorators or EF interceptors. Providers without a native writer return typed precondition failures; inputs remain detached and database-generated identity/default/computed/rowversion values are not returned.

## [10.0.109] - 2026-07-09

- Added local .NET tool manifest support for `bridgingit.devkit.cli` and updated the DevKit CLI documentation.
- Updated the package baseline to .NET 10.0.109, refreshed OpenTelemetry packages to 1.16.0, and upgraded Microsoft.OpenApi to 2.10.0 for improved security and dependency freshness.
- Improved document storage paging and query pipeline behavior across storage providers.

## [10.0.108] - 2026-07-03

- Generate API Reference documentation for the devkit assembly set.
- Add API Reference documentation for the devkit assembly set to the public website.
- Added the new `Application.Orchestrations` feature for code-first, durable workflows with activities, state transitions, timers, signals, behaviors, in-memory execution, recovery services, administration APIs, query services, diagrams, and test harness support.
- Added Entity Framework orchestration persistence and orchestration operational endpoints for definitions, instances, history, context, signals, timers, metrics, diagrams, SVG rendering, pause/resume, cancel/terminate, repair, lease release, purge, and timer requeue operations.
- Added metrics infrastructure across utilities, requester/notifier behaviors, repositories, job scheduling, messaging, queueing, and ASP.NET Core presentation, including request metrics middleware and platform/bITdevKit metrics endpoints.
- Added the Entity Framework file storage provider and storage operational endpoints for file and directory operations, metadata, checksums, existence checks, provider health, and file event inspection.
- Added notification email persistence, attachment storage, outbox processing, and operational endpoints for querying, reading content, retrying, archiving, purging, and viewing notification email statistics.
- Added Service Bus, RabbitMQ, and Azure Queue Storage queue broker implementations, plus Azure Queue Storage messaging broker support.
- Improved messaging and queueing operations with broker runtime summaries, capabilities, subscriptions, pause/resume controls, waiting-message views, and endpoint registration builders.
- Improved Entity Framework outbox, domain-event, message-broker, queue-broker, document-storage, and file-storage behavior for more reliable multi-host and provider-backed processing.
- Added `Common.Utilities` composition helpers and diagram builders/renderers for Mermaid and SVG output.
- Added DataPorter file-content export support.
- Improved fake identity provider support with cookie-based single sign-on and configurable endpoint options.
- Added new `bdk` CLI and MCP agent tooling for command-line workflows and local agent development.
- Added dashboard pages and operational endpoints for file storage, queueing, metrics, identity, log entries, system performance, and health checks.
- Added CSV export for weather forecasts, weather ingestion pipeline reporting, and support for wind gusts plus additional forecast parameters.
- Added Jobs dashboard pages, updated job occurrence lifecycle handling, and fixed background scheduling sweep startup behavior.
- Expanded the DoFiesta and WeatherFiesta samples with richer dashboards, weather operations, log entry filtering, messaging, notifications, and file operations.
- Updated package baselines including MailKit, MimeKit, OpenTelemetry, SharpCompress, Azure SDK packages, ASP.NET Core/EF Core packages, Kiota, MudBlazor, NSwag, Serilog, and test tooling.

## [10.0.107] - 2026-04-16

- Added the new `Application.Queueing` feature for single-consumer work items with in-process and Entity Framework-backed durable queue brokers.
- Added queueing operational endpoints and models for inspecting queue messages, viewing stats, retrying work items, releasing leases, archiving messages, purging retained data, and pausing or resuming queues and message types.
- Added durable Entity Framework-backed messaging with persisted broker messages, background processing workers, and operational broker services.
- Added messaging operational endpoints and models for inspecting broker messages, viewing stats, retrying handlers, releasing leases, archiving messages, and purging retained data.

## [10.0.106] - 2026-04-10

- Added bulk update and bulk delete support to generic repositories and repository extensions across the supported repository implementations.

## [10.0.105] - 2026-03-31

- Added source-generated Requester authoring for commands and queries so handlers, request wiring, and validation integration can be defined from a single partial request type.
- Added attribute-based FluentValidation authoring for simple command and query property rules, including collection element validation, while keeping full `[Validate]` support for more complex scenarios.

## [10.0.104] - 2026-03-31

- Added a new Pipeline feature in `Common.Utilities` with fluent definition and registration APIs, runtime behaviors, hooks, execution tracking, inline step support, and source-generated attributes to reduce registration boilerplate.

## [10.0.103] - 2026-03-19

- Added the new `Application.DataPorter` feature for multi-format import and export.
- Introduced profile-based and attribute-based configuration, validation, streaming, compression, templates, typed row interception, progress reporting, and extensible custom formats.
- Added fluent import, export, and template option builders for a more discoverable API.

## [10.0.102] - 2026-02-25

- Updated core package dependencies including Cosmos, Scalar.AspNetCore, and the test SDK.

## [10.0.101] - 2026-02-20

- Updated the .NET 10 toolchain and package baseline to 10.0.103.

## [10.0.100] - 2026-02-20

- Merged the main .NET 10 update baseline for the SDK and package set.

## [9.0.30] - 2025-12-19

- Updated permission evaluation responses to return collections consistently.
- Added implicit string-to-permission conversion support.
- Performed general maintenance and dependency refresh work.

## [10.0.1] - 2025-11-26

- Added string-based include options and refined permission evaluation behavior.
- Improved permission APIs with implicit string conversion support.
- Refreshed EF Core 10 and related package dependencies.

## [9.0.29] - 2025-11-18

- Refreshed NuGet dependencies.

## [9.0.28] - 2025-11-18

- Added new result operation saga scope helpers and related task extensions for multi-step workflows.
- Continued the `Result<T>`-based workflow improvements around operation scopes.

## [10.0.0] - 2025-11-12

- Started the .NET 10 release line.
- Improved logging and transaction pipeline behavior, especially around EF Core and scoped behavior execution.
- Added requester and notifier authorization pipeline attributes, `ClaimsPrincipal` support for `ICurrentUserAccessor`, and a new CORS configuration feature for presentation projects.

## [9.0.27] - 2025-10-30

- Added tracing support through the new `TracingBehavior` naming and improved job-related logging.
- Refreshed dependencies.

## [9.0.26] - 2025-10-29

- Added a new interactive console command feature and expanded console command support.
- Improved EF permission-provider handling for typed entity IDs.

## [9.0.25] - 2025-10-21

- Refactored authentication configuration into a clearer options model.
- Added JWT authentication extension methods.
- Improved OpenAPI metadata and Scalar integration.

## [9.0.24] - 2025-10-19

- Added richer `ProblemDetails` support including schema and document transformers.
- Improved JSON serialization safety and error handling.
- Tightened confidentiality for token-validation logging.

## [9.0.23] - 2025-10-17

- Added `FilterModel` parsing support for Minimal APIs.
- Standardized `ProblemDetails` and improved result-related error handling and logging.
- Updated dependencies to address security issues.

## [9.0.22] - 2025-10-13

- Changed filter paging defaults so `page = 0` and `pageSize = 0` mean no paging.

## [9.0.21] - 2025-10-09

- Expanded the `Result` API with `Bind` and `BindAsync` methods.
- Updated `DeleteResultAsync` to return both the result and the entity.
- Added `ModuleDbContextFactory` support and continued paging and tracking configuration improvements.

## [9.0.20] - 2025-10-02

- Minor maintenance release focused on internal cleanup and documentation updates.

## [9.0.19] - 2025-10-01

- Added OpenAPI document-generation startup paths and related example integration work.

## [9.0.18] - 2025-09-30

- Improved the `Notifier` and `Requester` builder APIs.
- Added `INotificationHandler` support to `DomainEventHandlerBase`.
- Added a new repository-backed domain-event publisher behavior.

## [9.0.17] - 2025-09-30

- Refined Roslyn generator project setup and refreshed dependencies.

## [9.0.16] - 2025-09-29

- Reduced mediator dependencies in the domain layer and improved result usage in the example application.

## [9.0.15] - 2025-09-28

- Updated the DoFiesta example application.

## [9.0.14] - 2025-09-24

- Removed the `Infrastructure.Azure.HealthChecks` project.

## [9.0.13] - 2025-09-24

- Removed health check support that depended on external packages.

## [9.0.12] - 2025-09-24

- Prepared the codebase for .NET 10 by updating obsolete web and OpenAPI integration points.

## [9.0.11] - 2025-09-23

- Removed the Pulsar message broker integration.

## [9.0.10] - 2025-09-22

- Added a new Active Record capability.
- Added processing jitter support for domain and message outboxes.

## [9.0.9] - 2025-08-19

- Added batched delete support for log maintenance.

## [9.0.8] - 2025-08-07

- Added delay jitter support to the notification outbox.

## [9.0.7] - 2025-08-05

- Follow-up stabilization release with no additional notable user-facing changes beyond `9.0.6`.

## [9.0.6] - 2025-08-05

- Fixed the Quartz configuration key handling.
- Improved `Requester` and `Notifier` performance and supporting infrastructure.
- Continued domain-event handling enhancements.

## [9.0.5] - 2025-07-09

- Maintenance release focused on repository housekeeping and project-structure cleanup.

## [9.0.4] - 2025-07-08

- Improved file compression option handling.
- Refined file-storage decompression defaults based on archive extension.
- Improved CSV and text logging output.

## [9.0.3] - 2025-07-04

- Added support for skipping SMTP server certificate validation.

## [9.0.2] - 2025-07-02

- Updated the framework baseline to .NET 9.

## [9.0.1] - 2025-05-19

- Documentation refresh release for the .NET 9 line.

## [3.0.4] - 2025-01-25

- Added entity permissions and a new identity provider capability.
- Enhanced repository behavior, including concurrency support for Cosmos, EF, and in-memory repositories.
- Renamed `PagedResult` and continued repository reliability improvements.

## [3.0.3] - 2024-10-11

- Improved module matching for inbound HTTP requests.
- Delivered a broad set of tracing and activity-correlation fixes.
- Added a more useful `Result.ToString()` implementation.

## [3.0.2] - 2024-04-25

- Enabled request logging with corrected Serilog setup.
- Applied post-release fixes and workflow cleanup.

## [3.0.1] - 2024-04-25

- Initial tagged release.
