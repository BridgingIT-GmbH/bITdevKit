---
status: draft
---

# Design Specification: Presentation.Web MCP Diagnostics

> This design document specifies an optional Model Context Protocol (MCP) diagnostics surface for DevKit-based ASP.NET Core applications. It focuses on online Streamable HTTP MCP hosted by the running web application and backed by the same diagnostics services used by dashboard and operational features.

[TOC]

## 1. Introduction

The DevKit dashboard exposes valuable runtime diagnostics for developers and operators: logs, errors, health checks, jobs, orchestrations, and correlation-id based flow context. That information is useful for humans, but an AI agent working on a local issue cannot use it efficiently unless the developer copies logs, screenshots, database rows, or dashboard output into the conversation.

This feature adds an optional MCP diagnostics surface that makes selected DevKit diagnostics available to AI agents as structured tools and resources. The MCP server is hosted inside the ASP.NET Core web application and uses the Streamable HTTP transport. The feature is intended primarily for local development and issue investigation while the application is running.

The dashboard and MCP surface are separate presentation features. They must not call each other. Both should use shared backend services and feature services.

```text
ASP.NET Core application
  /api/...              normal application API
  /_bdk/dashboard/...   human diagnostics UI
  /_bdk/mcp             MCP diagnostics endpoint for agents

Dashboard pages -> diagnostics readers -> existing feature services
MCP tools       -> diagnostics readers -> existing feature services
```

## 2. Goals

### 2.1 Make local DevKit applications agent-readable

The MCP surface shall allow local AI agents to inspect runtime diagnostics without scraping dashboard HTML or requiring manual copy/paste from the developer.

### 2.2 Reuse existing backend services

The implementation shall use existing DevKit services where possible:

- `ILogEntryService` for logs and errors
- ASP.NET Core health check services for health snapshots
- Jobs query and administration services for job definitions and occurrences
- Orchestration query and administration services for orchestration instances

The MCP feature shall not introduce a parallel data model or raw database access surface.

### 2.3 Keep dashboard and MCP separate

The dashboard remains the human UI. MCP is an agent-facing protocol surface. They may share query/read services, but they must not depend on each other's endpoint classes, RazorSlice pages, or JavaScript.

### 2.4 Focus on read-only diagnostics first

The first version shall focus on read-only diagnostics:

- logs
- errors
- health
- correlation-id inspection
- jobs
- orchestrations

Mutating actions such as purge, pause, cancel, retry, or manual dispatch are out of scope for the first version unless explicitly enabled by a later capability.

### 2.5 Fit existing Presentation.Web feature boundaries

The implementation shall live in existing `Presentation.Web.*` projects rather than a new standalone core runtime feature.

Examples:

- shared MCP registration in `src/Presentation.Web`
- logging MCP tools in `src/Presentation.Web`
- health MCP tools in `src/Presentation.Web`
- jobs MCP tools in `src/Presentation.Web.Jobs`
- orchestration MCP tools in `src/Presentation.Web.Orchestrations`

## 3. Non-goals

### 3.1 No offline stdio diagnostics host

The initial feature shall not provide an offline stdio MCP server that runs without the web application. Offline diagnostics would require reconstructing application configuration, module registration, feature registration, database contexts, and provider setup outside the application process. That is intentionally out of scope.

### 3.2 No stdio proxy in the first version

A future generic stdio-to-HTTP proxy may improve compatibility with MCP clients that prefer stdio. It would still require the web application to be running. This proxy is out of scope for the initial feature.

### 3.3 No replacement for dashboard pages

MCP does not replace the dashboard. The dashboard remains the browser-based operational UI.

### 3.4 No OpenAPI exposure

MCP endpoints are protocol endpoints, not application REST API endpoints. They must not be included in generated OpenAPI documents.

### 3.5 No production exposure by default

The feature is development-focused. It should be enabled explicitly and should default to development-only mapping in examples.

## 4. Transport Model

The initial MCP server uses Streamable HTTP hosted by ASP.NET Core.

```text
Agent / IDE MCP client
  -> HTTP MCP transport
  -> /_bdk/mcp
  -> registered MCP tool
  -> shared diagnostics reader
  -> existing DevKit service
```

The web application must be running for the MCP endpoint to be available. This is an accepted tradeoff for the first version because the running application already contains the correct dependency injection container, configured features, DbContexts, connection strings, health checks, and application-specific options.

## 5. Capability Layers

### 5.1 Shared Diagnostics Reader Layer

The shared diagnostics reader layer contains transport-neutral services used by MCP tools and potentially by dashboard pages.

Candidate services:

- `IDiagnosticsLogReader`
- `IDiagnosticsErrorReader`
- `IDiagnosticsHealthReader`
- `IDiagnosticsCorrelationInspector`
- `IDiagnosticsJobReader`
- `IDiagnosticsOrchestrationReader`

These services should return plain DTOs designed for diagnostics, not Razor models or protocol-specific MCP objects.

### 5.2 MCP Registration Layer

The registration layer configures the MCP server and registers tools for available DevKit diagnostics features.

It should support fluent configuration:

```csharp
builder.Services.AddDiagnosticsMcp(mcp => mcp
    .WithLogs() // includes correlation inspection
    .WithErrors()
    .WithHealth()
    .WithJobs()
    .WithOrchestrations());
```

Feature-specific projects may contribute extensions:

```csharp
builder.Services.AddDiagnosticsMcp(mcp => mcp
    .WithJobs()
    .WithOrchestrations());
```

### 5.3 MCP Endpoint Mapping Layer

The endpoint mapping layer maps the MCP transport endpoint into ASP.NET Core:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapDevKitMcp("/_bdk/mcp");
}
```

The mapped endpoint must be excluded from endpoint descriptions and generated OpenAPI output.

### 5.4 Feature Tool Layer

Feature tool classes expose MCP tools and resources. They should be small adapters over shared diagnostics readers.

Examples:

- `LogMcpTools`
- `ErrorMcpTools`
- `HealthMcpTools`
- `CorrelationMcpTools`
- `JobMcpTools`
- `OrchestrationMcpTools`

Tools should return compact, structured responses suitable for an agent. They should avoid returning unbounded data by default.

## 6. Public Configuration

### 6.1 Service Registration

Target API:

```csharp
builder.Services.AddDiagnosticsMcp(options => options
    .Enabled(builder.Environment.IsDevelopment())
    .WithLogs() // includes correlation inspection
    .WithErrors()
    .WithHealth()
    .WithJobs()
    .WithOrchestrations());
```

The exact builder shape may change during implementation, but it should preserve these concepts:

- enable/disable MCP diagnostics
- enable individual feature areas
- configure default result limits
- configure whether mutating tools are allowed in future versions

### 6.2 Endpoint Mapping

Target API:

```csharp
app.MapDevKitMcp("/_bdk/mcp");
```

The default path should be:

```text
/_bdk/mcp
```

The dashboard default path remains:

```text
/_bdk/dashboard
```

### 6.3 Example Application Configuration

WeatherFiesta and DoFiesta should enable MCP diagnostics in development as examples.

```csharp
builder.Services.AddDiagnosticsMcp(mcp => mcp
    .WithLogs() // includes correlation inspection
    .WithErrors()
    .WithHealth()
    .WithJobs()
    .WithOrchestrations());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapDevKitMcp("/_bdk/mcp");
}
```

## 7. MCP Client Configuration

An MCP client connects to the running web application over Streamable HTTP.

Example agent configuration for WeatherFiesta:

```json
{
  "mcpServers": {
    "weatherfiesta": {
      "type": "streamable-http",
      "url": "http://localhost:5000/_bdk/mcp"
    }
  }
}
```

Example agent configuration for DoFiesta:

```json
{
  "mcpServers": {
    "dofiesta": {
      "type": "streamable-http",
      "url": "http://localhost:5000/_bdk/mcp"
    }
  }
}
```

The application must already be running. The MCP client does not start the web application.

## 8. Tool Catalog

The exact names may evolve during implementation, but the initial tool surface should follow this shape.

### 8.1 Capabilities

```text
bdk.diagnostics.capabilities
```

Returns the enabled and available diagnostics features.

Example response:

```json
{
  "logs": true,
  "errors": true,
  "health": true,
  "correlationInspection": true,
  "jobs": true,
  "orchestrations": false
}
```

### 8.2 Health

```text
bdk.health.snapshot
```

Returns the current health check snapshot, including overall status, duration, and registered check results.

### 8.3 Logs

```text
bdk.logs.query
bdk.logs.tail
```

`bdk.logs.query` accepts filters such as:

- from
- to
- level
- search text
- correlation id
- log key
- module
- row limit

`bdk.logs.tail` returns the newest matching entries and should be bounded.

### 8.4 Errors

```text
bdk.errors.recent
bdk.errors.details
```

`bdk.errors.recent` returns recent error and fatal log entries with compact details.

`bdk.errors.details` returns one error plus related logs based on correlation id, trace id, or span id where available.

### 8.5 Correlation Inspection

```text
bdk.correlation.inspect
```

Inputs:

- correlation id
- optional from/to
- optional row limit

Returns:

- matching logs
- matching errors
- related job occurrences when discoverable
- related orchestration instances when discoverable
- summary counts and time range

This tool is the primary high-value workflow for agent-assisted debugging.

### 8.6 Jobs

```text
bdk.jobs.list
bdk.jobs.occurrences
bdk.jobs.occurrenceDetails
```

These tools expose job definitions, triggers, recent occurrences, status, duration, messages, properties, and correlation id where available.

### 8.7 Orchestrations

```text
bdk.orchestrations.list
bdk.orchestrations.instances
bdk.orchestrations.instanceDetails
```

These tools expose orchestration definitions, instances, status, state, instance id, correlation id, execution details, messages, and persisted context where available.

## 9. Resource Catalog

MCP resources provide stable context snapshots for agents.

Initial resources:

```text
bdk://diagnostics/capabilities
bdk://health
bdk://logs/recent
bdk://errors/recent
bdk://jobs
bdk://orchestrations
bdk://correlations/{correlationId}
```

Resources must be bounded and should avoid large unfiltered result sets.

## 10. Availability Behavior

The MCP endpoint should start even when optional diagnostics features are not registered.

If a tool is enabled but its backend service is missing, the tool should return a structured unavailable result instead of throwing a generic exception.

Example:

```json
{
  "available": false,
  "reason": "ILogEntryService is not registered."
}
```

This mirrors the dashboard preference for graceful unavailable states.

## 11. Security and Exposure

The first version is local-development focused and does not require dashboard authentication.

Recommended defaults:

- map only in development examples
- do not expose in OpenAPI
- no destructive tools by default
- keep result sets bounded
- do not include secrets
- redact known sensitive values in structured properties where practical

Applications that choose to map MCP in non-development environments are responsible for their own transport and network security. A future version may add optional authentication/authorization hooks.

## 12. Relationship to Dashboard

Dashboard and MCP are sibling presentation surfaces.

Allowed:

```text
Dashboard page -> diagnostics reader -> feature service
MCP tool       -> diagnostics reader -> feature service
```

Not allowed:

```text
MCP tool       -> dashboard endpoint
MCP tool       -> RazorSlice page
Dashboard page -> MCP tool
```

Shared logic currently embedded in dashboard pages should be moved into diagnostics readers before MCP tools depend on it.

## 13. Relationship to OpenAPI

MCP endpoints must be excluded from endpoint descriptions.

The endpoint mapping should apply the same principle as dashboard endpoints:

- add exclusion metadata at the route group or endpoint level
- verify generated `openapi.json` does not contain `/_bdk/mcp`

WeatherFiesta build-time OpenAPI generation should continue to produce application API documentation only.

## 14. Implementation Notes

### 14.1 Project Placement

Use existing presentation projects:

```text
src/Presentation.Web/Mcp
src/Presentation.Web/Logging/Mcp
src/Presentation.Web/Health/Mcp
src/Presentation.Web.Jobs/Mcp
src/Presentation.Web.Orchestrations/Mcp
```

If the MCP SDK requires a package dependency, the dependency should be added only to projects that host or register MCP tooling.

### 14.2 DTOs

MCP responses should use dedicated diagnostics DTOs rather than dashboard view models.

DTOs should be:

- compact
- serializable
- bounded
- easy for an agent to interpret
- stable enough for scripted use

### 14.3 Limits

Default limits:

- logs query: 100 rows
- errors recent: 50 rows
- correlation inspection logs: 200 rows
- job occurrences: 100 rows
- orchestration instances: 100 rows

Maximum limits should be enforced server-side.

### 14.4 Time Defaults

When no date range is supplied:

- logs default to current date
- errors default to current date
- correlation inspection may search current date first
- jobs and orchestrations default to recent persisted data

### 14.5 XML Documentation

All public classes, methods, builders, options, DTOs, and extension methods introduced by this feature must include XML documentation comments and examples where useful.

## 15. Testing Strategy

### 15.1 Unit Tests

Unit tests should cover:

- diagnostics reader filtering and limits
- unavailable-service behavior
- correlation inspection aggregation
- tool result mapping
- options and builder behavior

### 15.2 Integration Tests

Integration tests should cover:

- MCP endpoint mapping when enabled
- MCP endpoint not mapped when disabled
- OpenAPI exclusion
- WeatherFiesta or test-host tool execution against registered services

### 15.3 Example App Verification

WeatherFiesta should demonstrate:

- logs query
- recent errors
- health snapshot
- correlation inspection
- jobs list and occurrences
- orchestration instances and details

DoFiesta should demonstrate the same capabilities where the corresponding services are registered.

## 16. Example Agent Workflows

### 16.1 Investigate a failing flow

```text
Developer: Investigate why this request failed. Correlation id is e9af87d989e9.
Agent: calls bdk.correlation.inspect
Agent: calls bdk.errors.details for any error found
Agent: reads related logs, jobs, and orchestration data
Agent: explains likely failure and proposes code changes
```

### 16.2 Inspect recent failures

```text
Developer: What failed in WeatherFiesta after I ran the hello-world job?
Agent: calls bdk.errors.recent
Agent: calls bdk.jobs.occurrences
Agent: follows correlation ids through bdk.correlation.inspect
```

### 16.3 Check app readiness

```text
Developer: Is the app ready for dashboard testing?
Agent: calls bdk.health.snapshot
Agent: reports unhealthy checks and related diagnostic logs
```

## 17. Open Questions

- Which MCP .NET SDK version and API shape should be adopted when implementation begins?
- Should the default MCP endpoint be enabled by `AddDiagnosticsMcp` or only by explicit `MapDevKitMcp`?
- Should future mutating tools be part of this feature or a separate `OperationsMcp` capability?
- Should a generic stdio-to-HTTP proxy be provided later for clients that do not support Streamable HTTP?
- Should DevKit provide sample MCP client configuration files for common IDEs and agents?

## 18. References

- Model Context Protocol overview: https://modelcontextprotocol.io/docs/getting-started/intro
- Model Context Protocol Streamable HTTP transport: https://modelcontextprotocol.io/specification/2025-06-18/basic/transports
- .NET MCP SDK overview: https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/
