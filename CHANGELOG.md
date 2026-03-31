# Changelog

This changelog includes unreleased changes on `main` and the full tagged release history from the `main` branch.

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
