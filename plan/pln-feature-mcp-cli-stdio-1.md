---
goal: Implement the bdk STDIO MCP command module and app-side MCP IPC bridge
version: 1.0
date_created: 2026-07-01
last_updated: 2026-07-01
owner: bITdevKit
status: 'Completed'
tags: [feature, cli, mcp, diagnostics, presentation]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-green)

This plan implements the `bdk mcp` STDIO MCP server described by `docs/specs/spec-presentation-web-mcp-diagnostics.md`. The CLI exposes a stable MCP tool catalog, selects local DevKit runtimes through host descriptors, forwards runtime operations over local IPC, and keeps app-side handlers protocol-neutral through `IMcpHandler`.

## 1. Requirements & Constraints

- **REQ-001**: Add protocol-neutral MCP abstractions under `src/Common.Abstractions/Mcp`.
- **REQ-002**: Replace the marker-only web `IMcpHandler` with a request/response handler contract that has no dependency on an external MCP SDK.
- **REQ-003**: Implement app-side MCP IPC request dispatch for `mcp.capabilities` and registered handler operations.
- **REQ-004**: Expose `bdk mcp` as a direct STDIO JSON-RPC MCP server in `src/Presentation.Cli` without banners, host summaries, Spectre output, or stdout logs.
- **REQ-005**: Keep the CLI MCP tool catalog stable even when no runtime is selected or a runtime lacks a feature.
- **REQ-006**: Implement runtime tools, capability tools, health diagnostics, project operation tools, and documentation lookup tools.
- **REQ-007**: Implement feature-owned MCP handlers for logs, jobs, messaging, queueing, and orchestrations, returning structured unavailable results when a runtime service is not registered.
- **REQ-008**: Support `diagnostics`, `operations`, and `admin` toolsets, defaulting to `diagnostics`.
- **REQ-009**: Enforce destructive admin calls through explicit confirmation arguments.
- **SEC-001**: Keep host-side MCP registration development-only through the existing `DevKitLocalToolingPolicy`.
- **SEC-002**: Use local IPC nonce validation and never expose an HTTP MCP endpoint.
- **SEC-003**: Bound runtime and documentation responses by default.
- **CON-001**: Do not query application databases or call dashboard HTTP endpoints from the CLI.
- **CON-002**: Prefer BCL implementations and avoid new NuGet dependencies.
- **CON-003**: Run top-level build/test commands sequentially in this worktree.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Establish shared app-side MCP contracts and dispatcher behavior.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Add `src/Common.Abstractions/Mcp/IMcpHandler.cs`, `McpRequest.cs`, `McpResponse.cs`, `McpCapability.cs`, `McpToolset.cs`, and `McpErrorCode.cs`. | ✅ | 2026-07-01 |
| TASK-002 | Update `src/Presentation.Web/Mcp/McpIpcServer.cs` to deserialize MCP IPC requests, validate nonce/protocol version, and call `McpDispatcher`. | ✅ | 2026-07-01 |
| TASK-003 | Add `src/Presentation.Web/Mcp/McpDispatcher.cs` to route `mcp.capabilities` and registered handler operations. | ✅ | 2026-07-01 |
| TASK-004 | Add `src/Presentation.Web/Mcp/RuntimeDiagnosticsMcpHandler.cs` for `health.snapshot` when `HealthCheckService` is available. | ✅ | 2026-07-01 |
| TASK-005 | Add `src/Presentation.Web/Mcp/ServiceCollectionExtensions.cs` with `AddMcpHandler<T>()` and `AddMcpHandlersFromAssembly<T>()`. | ✅ | 2026-07-01 |

### Implementation Phase 2

- GOAL-002: Implement the CLI STDIO MCP server and runtime forwarding.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-006 | Add `src/Presentation.Cli/Mcp/McpCliOptions.cs` to parse `bdk mcp` options: `--runtime-id`, `--workspace`, `--toolset`, and `--verbose`. | ✅ | 2026-07-01 |
| TASK-007 | Add `src/Presentation.Cli/Mcp/StdioMcpServer.cs` to implement newline-delimited JSON-RPC for `initialize`, `tools/list`, and `tools/call`. | ✅ | 2026-07-01 |
| TASK-008 | Add `src/Presentation.Cli/Mcp/McpToolCatalog.cs` and stable tool definitions matching the spec. | ✅ | 2026-07-01 |
| TASK-009 | Add `src/Presentation.Cli/Mcp/McpRuntimeTools.cs` to implement `bdk_mcp_status`, `bdk_mcp_self_test`, `bdk_runtimes_list`, `bdk_runtimes_select`, and `bdk_capabilities_get`. | ✅ | 2026-07-01 |
| TASK-010 | Add `src/Presentation.Cli/Mcp/McpIpcClient.cs` to forward MCP operations to selected runtimes over local IPC. | ✅ | 2026-07-01 |
| TASK-011 | Add `src/Presentation.Cli/Mcp/McpDocumentationTools.cs` for bounded official remote DevKit documentation search and retrieval. | ✅ | 2026-07-01 |
| TASK-012 | Route `bdk mcp` directly in `src/Presentation.Cli/CliHost/CliApplication.cs` before normal CLI banner/help execution. | ✅ | 2026-07-01 |

### Implementation Phase 3

- GOAL-003: Document and validate the feature.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-013 | Add CLI unit tests covering MCP STDIO initialize, tools/list, status without runtimes, capabilities unavailable, toolset enforcement, project calls, and docs lookup. | ✅ | 2026-07-01 |
| TASK-014 | Add web unit tests covering dispatcher capabilities, handler routing, nonce rejection, and health snapshot behavior. | ✅ | 2026-07-01 |
| TASK-015 | Update `docs/specs/spec-presentation-web-mcp-diagnostics.md` implementation notes or status to reflect implemented behavior and feature-handler extension points. | ✅ | 2026-07-01 |
| TASK-016 | Run `dotnet build` for affected projects and `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --nologo`. | ✅ | 2026-07-01 |

### Implementation Phase 4

- GOAL-004: Complete the feature-owned MCP surfaces and cross-platform runtime bridge.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-017 | Expand the stable CLI MCP catalog and forwarding map to the full spec tool list for logs/errors, health/metrics, messaging, queueing, jobs and orchestrations. | ✅ | 2026-07-01 |
| TASK-018 | Add common JSON argument parsing helpers for app-side MCP handlers. | ✅ | 2026-07-01 |
| TASK-019 | Add built-in log/error/correlation, health and metrics MCP handlers in `Presentation.Web`. | ✅ | 2026-07-01 |
| TASK-020 | Add feature-owned MCP handlers for messaging, queueing, job scheduling and orchestrations. | ✅ | 2026-07-01 |
| TASK-021 | Register feature-owned MCP handlers through presentation feature registration extensions. | ✅ | 2026-07-01 |
| TASK-022 | Add Unix domain socket transport for MCP IPC on non-Windows platforms while preserving named pipes on Windows. | ✅ | 2026-07-01 |
| TASK-023 | Add destructive-operation confirmation tests and update catalog coverage tests. | ✅ | 2026-07-01 |
| TASK-024 | Add the MCP architecture appendix to `docs/features-cli.md`. | ✅ | 2026-07-01 |

## 3. Alternatives

- **ALT-001**: Use the official MCP .NET SDK. Not chosen because the spec requires app-side handlers to remain MCP-SDK-independent and the BCL is sufficient for the small STDIO JSON-RPC surface.
- **ALT-002**: Expose feature operations through dynamic MCP tools. Not chosen because the spec requires a stable catalog and project operations through `bdk_project_operations` and `bdk_project_call`.
- **ALT-003**: Implement CLI-side direct reads of logs/jobs persistence. Not chosen because the spec forbids direct database access and requires app-side feature handlers.

## 4. Dependencies

- **DEP-001**: Existing host descriptor contracts in `src/Common.Abstractions/HostDiscovery`.
- **DEP-002**: Existing CLI host discovery and selection in `src/Presentation.Cli/HostDiscovery`.
- **DEP-003**: Existing local web host descriptor lifecycle in `src/Presentation.Web/HostDiscovery`.
- **DEP-004**: ASP.NET Core `HealthCheckService` for the built-in health snapshot handler.
- **DEP-005**: Existing application services for logs, messaging, queueing, job scheduling and orchestrations.

## 5. Files

- **FILE-001**: `src/Common.Abstractions/Mcp/*`
- **FILE-002**: `src/Presentation.Web/Mcp/*`
- **FILE-003**: `src/Presentation.Cli/Mcp/*`
- **FILE-004**: `src/Presentation.Cli/CliHost/CliApplication.cs`
- **FILE-005**: `src/Presentation.Cli/CliHost/CliCommandModules.cs`
- **FILE-006**: `tests/Presentation.UnitTests/Cli/CliFoundationTests.cs`
- **FILE-007**: `tests/Presentation.UnitTests/Web/Hosting/DevKitWebApplicationTests.cs`
- **FILE-008**: `docs/specs/spec-presentation-web-mcp-diagnostics.md`
- **FILE-009**: `src/Presentation.Web.JobScheduling/Mcp/*`
- **FILE-010**: `src/Presentation.Web.Messaging/Mcp/*`
- **FILE-011**: `src/Presentation.Web.Queueing/Mcp/*`
- **FILE-012**: `src/Presentation.Web.Orchestrations/Mcp/*`
- **FILE-013**: `tests/Presentation.UnitTests/Web/Mcp/McpFeatureHandlerTests.cs`
- **FILE-014**: `docs/features-cli.md`

## 6. Testing

- **TEST-001**: `StdioMcpServer_Initialize_ReturnsServerCapabilities`
- **TEST-002**: `StdioMcpServer_ToolsList_ReturnsStableCatalog`
- **TEST-003**: `McpRuntimeTools_StatusWithoutRuntime_ReturnsAvailableDiagnostics`
- **TEST-004**: `McpRuntimeTools_CapabilitiesWithoutRuntime_ReturnsNoRuntimeFound`
- **TEST-005**: `McpRuntimeTools_OperationsToolsetDisabled_ReturnsUnauthorizedToolset`
- **TEST-006**: `McpDispatcher_Capabilities_ReturnsRegisteredHandlerCapabilities`
- **TEST-007**: `McpDispatcher_UnknownOperation_ReturnsFeatureUnavailable`
- **TEST-008**: `McpIpcServer_InvalidNonce_ReturnsUnauthorizedResponse`
- **TEST-009**: `RuntimeDiagnosticsMcpHandler_WithoutHealthChecks_ReturnsFeatureUnavailable`
- **TEST-010**: `McpFeatureHandlers_AdminPurgeWithoutConfirmation_ReturnsConfirmationRequired`
- **TEST-011**: `RuntimeDiagnosticsMcpHandler_WithoutMetrics_ReturnsFeatureUnavailable`
- **TEST-012**: `CliApplication_ToolsList_ReturnsFullStableCatalog`

## 7. Risks & Assumptions

- **RISK-001**: Runtime capability completeness depends on applications registering the matching feature services and presentation feature packages.
- **RISK-002**: MCP clients may evolve beyond the supported JSON-RPC methods. The implementation handles the required initialization and tool call methods from the current official protocol and returns JSON-RPC method-not-found errors for unsupported methods.
- **ASSUMPTION-001**: The selected runtime is the source of truth for all runtime data.
- **ASSUMPTION-002**: `diagnostics` is the only default toolset; `operations` and `admin` require explicit CLI options.

## 8. Related Specifications / Further Reading

- [Design Specification: DevKit STDIO MCP](../docs/specs/spec-presentation-web-mcp-diagnostics.md)
- [Design Specification: DevKit CLI](../docs/specs/spec-presentation-cli.md)
- [Design Specification: DevKit Web Host](../docs/specs/spec-presentation-devkit-web-host.md)
- [MCP Tools Specification](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
- [MCP Transports Specification](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
