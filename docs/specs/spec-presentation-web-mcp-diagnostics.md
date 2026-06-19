---
status: draft
---

# Design Specification: DevKit STDIO MCP Diagnostics

> This design document specifies a STDIO-based Model Context Protocol diagnostics adapter for DevKit-based applications. The feature allows IDE agents and coding agents to inspect one or more running DevKit hosts through their authenticated `/_bdk/api/*` operational HTTP endpoints.

[TOC]

## Introduction

DevKit applications expose operational and diagnostic information through feature-specific HTTP endpoints. These endpoints make runtime data available for diagnostics, support, and local development workflows.

This feature adds a local STDIO MCP adapter that allows IDE agents and coding agents to inspect running DevKit applications in a structured way.

The MCP server is not hosted inside the ASP.NET Core application. Instead, the DevKit CLI runs the MCP server as a local STDIO process. The MCP process discovers running DevKit hosts through runtime descriptor files, authenticates to the selected host, calls its `/_bdk/api/*` endpoints, and translates responses into MCP tool results.

```text id="f3iq90"
VS Code / coding agent
  -> MCP over STDIO
  -> dotnet tool run bdk mcp
  -> runtime descriptor discovery
  -> selected running DevKit host
  -> authenticated /_bdk/api/* endpoints
  -> existing feature services and persistence
```

The running application remains the source of truth for diagnostics. The MCP process is a local adapter.

## Positioning

This feature is an agent-readable diagnostics surface for DevKit applications.

It is not:

* an application host
* a service discovery system
* a distributed application runtime
* a dashboard replacement
* a deployment orchestrator
* a cross-service topology model
* a database reader
* an authorization bypass
* a general production operations plane

The core boundary is:

```text id="swq7yn"
Running DevKit host owns diagnostics.
DevKit operational endpoints expose diagnostics.
bdk mcp adapts those endpoints to MCP.
```

## Goals

## Make DevKit hosts agent-readable

The feature shall allow IDE agents and coding agents to inspect a running DevKit host without requiring the developer to manually copy logs, metrics, messages, endpoint output, or database rows.

## Use STDIO as the MCP transport

The primary MCP transport shall be STDIO.

The MCP server shall be started by the agent as a local process:

```text id="weln99"
dotnet tool run bdk mcp
```

STDIO keeps the IDE integration local, predictable, and independent from application-hosted MCP endpoints.

## Distribute the CLI as a local .NET tool

The DevKit CLI shall be distributed as a .NET tool.

The recommended installation mode is a repository-local tool manifest so each solution can pin the CLI version that matches its DevKit package version.

Global tool installation may be supported as a convenience, but the documented team setup shall prefer a local tool.

## Support multiple running DevKit hosts

A workspace may contain a modular monolith or multiple microservice hosts.

Each running DevKit host owns its own:

* process
* runtime descriptor
* `/_bdk/api/*` operational endpoint surface
* authentication configuration
* database
* persisted logs
* metrics
* messages
* queueing state
* jobs
* orchestrations
* notifications

The MCP server shall discover all running hosts in the workspace and allow one runtime to be selected for diagnostic calls.

## Reuse operational endpoints

The MCP process shall call DevKit operational HTTP endpoints.

The MCP process shall not call dashboard pages, Razor components, JavaScript, or internal application DI services.

If a feature lacks the required operational endpoint, the feature endpoint surface should be extended. The MCP process must not add feature-specific persistence access.

## Preserve application authorization

The MCP process shall authenticate like any other HTTP client.

The MCP process shall not bypass `RequireAuthorization`, role requirements, policies, or endpoint-specific authorization rules.

The MCP process shall obtain a JWT access token and send it as a bearer token when calling protected `/_bdk/api/*` endpoints.

## Keep diagnostics bounded

MCP tools shall return bounded, structured responses by default.

Tools must avoid unbounded logs, metrics, message lists, orchestration histories, or large raw payloads unless explicit limits are supplied and accepted by the server.

## Non-goals

## No application-hosted MCP endpoint

The ASP.NET Core application shall not host an MCP server.

The following are not part of this feature:

```text id="k6gyuu"
/_bdk/mcp
app.MapDevKitMcp(...)
AddDiagnosticsMcp(...)
application-hosted Streamable HTTP MCP
```

## No direct database access from MCP

The MCP process shall not:

* load application configuration
* rebuild the application DI container
* resolve application DbContexts
* query application databases directly
* read EF Core provider-specific tables directly
* read log databases directly
* inspect feature persistence schemas directly

All diagnostic data must come through the running host’s HTTP endpoints.

## No inter-service discovery

Multiple runtimes may be discovered, listed, and selected.

The feature shall not infer relationships between microservices. It shall not build a topology model, resolve dependencies between hosts, or aggregate diagnostics across services by default.

## No anonymous operational access by default

DevKit operational endpoints under `/_bdk/api/*` are not assumed to be anonymous.

The MCP process must authenticate when the selected runtime requires authentication.

## No production exposure by default

The feature is primarily for local development and controlled Docker development scenarios.

Non-development usage must be explicitly enabled and secured by the application.

## Terminology

| Term                  | Meaning                                                                                                             |
| --------------------- | ------------------------------------------------------------------------------------------------------------------- |
| DevKit host           | A running ASP.NET Core application using DevKit features.                                                           |
| Runtime               | One discovered running DevKit host instance.                                                                        |
| Runtime descriptor    | A local JSON file written by a running host so tooling can discover it.                                             |
| Operational endpoint  | A DevKit HTTP endpoint under `/_bdk/api/*` that exposes diagnostics or operational state.                           |
| Capabilities endpoint | The endpoint under `/_bdk/api/capabilities` that reports available operational features and endpoints for one host. |
| MCP process           | The `bdk mcp` process running as a local STDIO MCP server.                                                          |
| Selected runtime      | The runtime currently used for diagnostic MCP tool calls.                                                           |
| Local tool            | A .NET tool installed through a repository-local `.config/dotnet-tools.json` manifest.                              |

## High-Level Architecture

```text id="b2lg1s"
Developer workspace
  .config/dotnet-tools.json
  .bdk/runtimes/*.json
  .bdk/runtime-selection.json

Running host A
  https://localhost:5001/_bdk/api/capabilities
  https://localhost:5001/_bdk/api/logentries
  https://localhost:5001/_bdk/api/metrics
  https://localhost:5001/_bdk/api/queueing

Running host B
  https://localhost:5101/_bdk/api/capabilities
  https://localhost:5101/_bdk/api/messaging
  https://localhost:5101/_bdk/api/orchestrations

bdk mcp
  discovers descriptors
  authenticates to selected runtime
  calls selected runtime /_bdk/api/*
  exposes MCP tools over STDIO
```

The DevKit CLI and the running applications are separate processes.

The CLI owns:

* STDIO MCP transport
* MCP tool definitions
* runtime descriptor discovery
* selected runtime state
* token acquisition
* HTTP calls to operational endpoints
* mapping HTTP responses to MCP results

Each running application owns:

* feature registration
* endpoint registration
* authorization rules
* feature service implementations
* persistence and provider details
* capabilities metadata
* runtime descriptor writing

## Package and Project Placement

## DevKit CLI package

A CLI package should be introduced:

```text id="wwgozy"
src/Tooling.Cli
  BridgingIT.DevKit.Cli
```

The package is distributed as a .NET tool:

```xml id="c87ymj"
<PropertyGroup>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>bdk</ToolCommandName>
  <PackageId>BridgingIT.DevKit.Cli</PackageId>
</PropertyGroup>
```

Initial commands:

```text id="z2sodd"
bdk mcp
bdk runtimes list
bdk runtimes current
bdk runtimes select <runtimeId>
bdk runtimes clean
bdk version
```

## Presentation.Web additions

`Presentation.Web` should provide:

```text id="iumqmc"
runtime descriptor writer
runtime descriptor options
capabilities endpoint
capability provider abstractions
endpoint capability registration helpers
optional local development auth helpers
```

Feature-specific presentation packages should contribute capabilities when their operational endpoints are registered.

Examples:

```text id="zfkrpm"
Presentation.Web.Logging
Presentation.Web.Metrics
Presentation.Web.Messaging
Presentation.Web.Queueing
Presentation.Web.Jobs
Presentation.Web.Orchestrations
Presentation.Web.Notifications
```

## Local Tool Installation

The preferred setup is a local .NET tool manifest.

In the solution root:

```bash id="o3kqnd"
dotnet new tool-manifest
dotnet tool install BridgingIT.DevKit.Cli
```

The generated file is committed:

```text id="zrxlnb"
.config/dotnet-tools.json
```

Every developer restores tools with:

```bash id="m4nfnb"
dotnet tool restore
```

The MCP server is started with:

```bash id="mip19w"
dotnet tool run bdk mcp
```

or:

```bash id="vb8518"
dotnet bdk mcp
```

depending on .NET tool invocation support and command aliasing.

## VS Code MCP Configuration

The default VS Code MCP configuration should use the local tool:

```json id="rpa798"
{
  "servers": {
    "bdk": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["tool", "run", "bdk", "mcp"],
      "cwd": "${workspaceFolder}"
    }
  }
}
```

Global tool installation may be documented as a convenience:

```bash id="kkkvdz"
dotnet tool install --global BridgingIT.DevKit.Cli
```

A global tool can be configured as:

```json id="smc7vl"
{
  "servers": {
    "bdk": {
      "type": "stdio",
      "command": "bdk",
      "args": ["mcp"],
      "cwd": "${workspaceFolder}"
    }
  }
}
```

The local tool remains the recommended setup because it pins the CLI version to the repository.

## Runtime Descriptor Registry

## Descriptor directory

Each running host writes its own descriptor file.

The descriptor registry lives under:

```text id="nj8e5m"
.bdk/runtimes/
```

Examples:

```text id="iowxrq"
.bdk/runtimes/weatherfiesta-api.5001.json
.bdk/runtimes/billing-api.5101.json
.bdk/runtimes/inventory-api.5201.json
.bdk/runtimes/worker-host.5301.json
```

A single `current.json` is not the primary model because workspaces may run several hosts at the same time.

## Descriptor ownership

Each host writes and owns only its own descriptor.

The descriptor file name should be deterministic enough to avoid collisions and readable enough for support:

```text id="axurhl"
{normalized-application-name}.{port-or-processId}.json
```

The descriptor should be written on host startup and refreshed when important runtime metadata changes.

The descriptor should be removed on graceful shutdown where possible. Stale descriptors must still be handled because processes may crash.

## Descriptor schema

Example descriptor:

```json id="w1okx5"
{
  "schemaVersion": 1,
  "runtimeId": "billing-api-5101",
  "applicationName": "Billing.Api",
  "environmentName": "Development",
  "workspacePath": "D:/src/my-solution",
  "contentRootPath": "D:/src/my-solution/src/Billing.Api",
  "projectPath": "D:/src/my-solution/src/Billing.Api/Billing.Api.csproj",
  "processId": 23844,
  "startedAt": "2026-06-18T14:20:00Z",
  "urls": {
    "hostBaseUrl": "https://localhost:5101",
    "containerBaseUrl": "http://billing-api:8080"
  },
  "preferredUrl": "hostBaseUrl",
  "operationalBasePath": "/_bdk/api",
  "capabilitiesPath": "/_bdk/api/capabilities",
  "auth": {
    "type": "jwt-password",
    "scheme": "Bearer",
    "authority": "https://localhost:5101",
    "tokenEndpoint": "https://localhost:5101/_bdk/api/identity/connect/token",
    "clientId": "bdk-mcp",
    "clientSecret": "local-dev-secret",
    "username": "admin@local",
    "password": "admin",
    "scope": "openid profile email roles",
    "audience": "api"
  }
}
```

## Required descriptor fields

| Field                 | Required | Description                                                          |
| --------------------- | -------: | -------------------------------------------------------------------- |
| `schemaVersion`       |      yes | Runtime descriptor schema version.                                   |
| `runtimeId`           |      yes | Stable id for the current host instance.                             |
| `applicationName`     |      yes | Display name of the running application.                             |
| `environmentName`     |      yes | ASP.NET Core environment name.                                       |
| `workspacePath`       |      yes | Solution or workspace root used for discovery.                       |
| `contentRootPath`     |      yes | Application content root.                                            |
| `processId`           |      yes | Local process id when known.                                         |
| `startedAt`           |      yes | UTC start timestamp.                                                 |
| `urls`                |      yes | Reachable base URLs for host and container scenarios.                |
| `operationalBasePath` |      yes | Base path for DevKit operational endpoints. Defaults to `/_bdk/api`. |
| `capabilitiesPath`    |      yes | Capabilities endpoint path. Defaults to `/_bdk/api/capabilities`.    |
| `auth`                |       no | Authentication metadata used by the MCP process.                     |

## URL model

The default local host URL is:

```text id="mbsmu5"
https://localhost:5001
```

The default operational base path is:

```text id="dsu4wp"
/_bdk/api
```

The default capabilities URL is:

```text id="a1lcg8"
https://localhost:5001/_bdk/api/capabilities
```

The descriptor may contain multiple URLs to support host and Docker scenarios:

```json id="rirr7w"
{
  "urls": {
    "hostBaseUrl": "https://localhost:5001",
    "containerBaseUrl": "http://weatherfiesta-api:8080"
  },
  "preferredUrl": "hostBaseUrl"
}
```

The MCP process chooses the best URL based on:

```text id="q99rb2"
explicit command-line override
descriptor preferredUrl
host/devcontainer detection
connectivity check
```

## Runtime Discovery

`bdk mcp` and `bdk runtimes list` discover runtimes in this order:

```text id="l3mo5h"
1. Explicit --runtime-url
2. Explicit --runtime-id
3. BDK_RUNTIME_URL environment variable
4. Workspace .bdk/runtimes/*.json
5. Parent-folder .bdk/runtimes/*.json
6. Optional user-level runtime registry
7. No runtime found
```

A user-level registry may be added later for cross-workspace tooling, but the workspace registry is the primary model.

## Descriptor validation

Before a descriptor is treated as usable, the CLI validates it:

```text id="wulffq"
1. Parse JSON.
2. Check schemaVersion.
3. Check process id if the process is local.
4. Build capabilities URL from selected base URL and capabilitiesPath.
5. Acquire a token if auth is configured.
6. Call capabilities endpoint.
7. Mark runtime as Ready, Unauthorized, Forbidden, Stale, Unreachable, or Invalid.
```

## Runtime statuses

| Status         | Meaning                                                           |
| -------------- | ----------------------------------------------------------------- |
| `Ready`        | Descriptor is valid, host is reachable, capabilities can be read. |
| `Unauthorized` | Token is missing, invalid, or expired.                            |
| `Forbidden`    | Token is valid but lacks required role or policy.                 |
| `Unreachable`  | Base URL or capabilities endpoint cannot be reached.              |
| `Stale`        | Process is gone or descriptor no longer matches a live host.      |
| `Invalid`      | Descriptor cannot be parsed or required fields are missing.       |
| `Unknown`      | Runtime has not been validated yet.                               |

## Runtime Selection

The MCP server supports one selected runtime for normal diagnostic tool calls.

Selection tools:

```text id="w7dkdn"
bdk.runtimes.list
bdk.runtimes.select
bdk.runtimes.current
bdk.runtimes.refresh
```

CLI commands:

```bash id="eoq4t7"
dotnet tool run bdk runtimes list
dotnet tool run bdk runtimes current
dotnet tool run bdk runtimes select billing-api-5101
dotnet tool run bdk runtimes clean
```

## Selection behavior

When a diagnostic MCP tool is called:

* if exactly one runtime is ready, the MCP server may auto-select it
* if multiple runtimes are ready and no runtime is selected, the tool returns `runtime_selection_required`
* if the selected runtime is no longer valid, the tool returns `selected_runtime_unavailable`
* the MCP server never aggregates across runtimes by default

Example response:

```json id="znyofh"
{
  "available": false,
  "reason": "Multiple DevKit runtimes are available. Select one runtime first.",
  "code": "runtime_selection_required",
  "runtimes": [
    {
      "runtimeId": "billing-api-5101",
      "applicationName": "Billing.Api",
      "baseUrl": "https://localhost:5101",
      "status": "Ready"
    },
    {
      "runtimeId": "inventory-api-5201",
      "applicationName": "Inventory.Api",
      "baseUrl": "https://localhost:5201",
      "status": "Ready"
    }
  ]
}
```

## Persisted selection

The selected runtime may be stored in:

```text id="zhwgo3"
.bdk/runtime-selection.json
```

Example:

```json id="rmcfz0"
{
  "selectedRuntimeId": "billing-api-5101",
  "selectedAt": "2026-06-18T14:30:00Z"
}
```

The persisted selection is a convenience only. The CLI must still validate that the runtime exists and is reachable.

## Capabilities Endpoint

## Purpose

The MCP process must not guess which features are configured.

A running host exposes an authenticated capabilities endpoint:

```text id="bkunsk"
/_bdk/api/capabilities
```

The runtime descriptor points to this endpoint.

The capabilities endpoint returns the features and operations that are available through the selected host’s operational HTTP endpoints.

The descriptor answers:

```text id="cyonyl"
Where is this host?
How can the MCP authenticate?
Where can capabilities be read?
```

The capabilities endpoint answers:

```text id="zo37ux"
Which features are exposed?
Which endpoints are mapped?
Which operations are available?
Which operations are read-only?
Which operations require authentication, roles, or policies?
```

## Feature availability rule

A feature is MCP-available only when:

```text id="g2euz8"
the feature is configured
AND its operational HTTP endpoints are registered
AND the MCP token can access them
```

Internal service registration alone is not enough.

A feature may be configured internally but unavailable to MCP if its `/_bdk/api/*` endpoint surface is not registered.

## Capabilities response

Example response:

```json id="dcgrh6"
{
  "schemaVersion": 1,
  "runtimeId": "billing-api-5101",
  "applicationName": "Billing.Api",
  "environmentName": "Development",
  "generatedAt": "2026-06-18T14:33:00Z",
  "operationalBasePath": "/_bdk/api",
  "features": {
    "logs": {
      "available": true,
      "configured": true,
      "endpointsRegistered": true,
      "basePath": "/_bdk/api/logentries",
      "authorization": {
        "requiresAuthentication": true,
        "roles": ["Administrator"],
        "policy": null
      },
      "operations": {
        "query": {
          "method": "GET",
          "path": "/_bdk/api/logentries",
          "readOnly": true,
          "description": "Query persisted log entries."
        },
        "stream": {
          "method": "GET",
          "path": "/_bdk/api/logentries/stream",
          "readOnly": true,
          "description": "Stream recent persisted log entries."
        }
      }
    },
    "metrics": {
      "available": true,
      "configured": true,
      "endpointsRegistered": true,
      "basePath": "/_bdk/api/metrics",
      "authorization": {
        "requiresAuthentication": true,
        "roles": ["Administrator"],
        "policy": null
      },
      "operations": {
        "snapshot": {
          "method": "GET",
          "path": "/_bdk/api/metrics/snapshot",
          "readOnly": true,
          "description": "Return the current metric snapshot."
        },
        "query": {
          "method": "POST",
          "path": "/_bdk/api/metrics/query",
          "readOnly": true,
          "description": "Query persisted or aggregated metrics."
        }
      }
    },
    "queueing": {
      "available": true,
      "configured": true,
      "endpointsRegistered": true,
      "basePath": "/_bdk/api/queueing",
      "authorization": {
        "requiresAuthentication": true,
        "roles": ["Administrator"],
        "policy": null
      },
      "operations": {
        "summary": {
          "method": "GET",
          "path": "/_bdk/api/queueing/stats",
          "readOnly": true
        },
        "messages": {
          "method": "GET",
          "path": "/_bdk/api/queueing/messages",
          "readOnly": true
        },
        "retryMessage": {
          "method": "POST",
          "path": "/_bdk/api/queueing/messages/{id}/retry",
          "readOnly": false
        }
      }
    },
    "orchestrations": {
      "available": false,
      "configured": false,
      "endpointsRegistered": false,
      "reason": "Orchestration endpoints are not registered."
    }
  }
}
```

## Capability provider model

Feature endpoint packages contribute capability metadata when their operational endpoints are registered.

Conceptual contract:

```csharp id="fu7xsh"
public interface IDevKitOperationalCapabilityProvider
{
    DevKitOperationalCapability GetCapability();
}
```

Feature endpoint registration should register a provider:

```csharp id="ehukn4"
services.AddDevKitOperationalCapability("queueing", options.GroupPath, operations =>
{
    operations.Get("summary", "stats", readOnly: true);
    operations.Get("messages", "messages", readOnly: true);
    operations.Post("retryMessage", "messages/{id}/retry", readOnly: false);
});
```

The capabilities endpoint aggregates all registered providers.

## Do not infer from DI alone

The capabilities endpoint may include `configured` and `endpointsRegistered`, but MCP availability is based on endpoint availability.

The following is not enough:

```text id="ymyxtf"
ILogEntryService is registered
IMetricsService is registered
IQueueBrokerService is registered
IMessageBrokerService is registered
```

The endpoint surface must also be mapped and accessible.

## Do not probe known routes

The MCP process should not discover features by probing route guesses such as:

```text id="ow7j88"
GET /_bdk/api/logentries
GET /_bdk/api/metrics/snapshot
GET /_bdk/api/queueing/stats
GET /_bdk/api/orchestrations
```

Route probing is ambiguous because:

* `404` may mean disabled or custom path
* `401` may mean registered but unauthenticated
* `403` may mean registered but forbidden
* routes can be customized
* probing creates noisy logs

The capabilities endpoint is the authoritative contract.

## Authentication and Authorization

## Principle

The MCP process authenticates to the selected running DevKit application through normal JWT bearer authentication.

It does not bypass ASP.NET Core authentication or authorization.

All DevKit operational endpoints under `/_bdk/api/*` may require:

* authenticated user
* specific roles
* specific policies

The token used by the MCP process must satisfy those requirements.

The feature does not introduce separate diagnostic roles. Applications decide which roles or policies are required for their DevKit endpoints. In development this will often be an existing role such as `Administrator`, or only an authenticated user.

## Supported authentication types

The runtime descriptor supports:

```text id="rqej9i"
jwt-password
jwt-client-credentials
none
```

`jwt-password` uses OAuth2 password grant. This is intended for local development with a local identity provider and a configured development user.

`jwt-client-credentials` uses OAuth2 client credentials. This is intended for tool/client identities where the identity provider can issue the required claims.

`none` is only valid when the selected host intentionally exposes the relevant operational endpoints without authentication. This should not be the default.

## Password grant

Password grant allows the MCP process to authenticate as a configured local development user.

This is useful when `/_bdk/api/*` endpoints require a normal user role such as `Administrator`.

Descriptor example:

```json id="ds1agi"
{
  "auth": {
    "type": "jwt-password",
    "scheme": "Bearer",
    "authority": "https://localhost:5001",
    "tokenEndpoint": "https://localhost:5001/_bdk/api/identity/connect/token",
    "clientId": "bdk-mcp",
    "clientSecret": "local-dev-secret",
    "username": "admin@local",
    "password": "admin",
    "scope": "openid profile email roles",
    "audience": "api"
  }
}
```

Token request:

```http id="ckgrtr"
POST https://localhost:5001/_bdk/api/identity/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password
&client_id=bdk-mcp
&client_secret=local-dev-secret
&username=admin%40local
&password=admin
&scope=openid profile email roles
```

## Client credentials grant

Client credentials allows the MCP process to authenticate as a tool/client identity.

Descriptor example:

```json id="a9t66b"
{
  "auth": {
    "type": "jwt-client-credentials",
    "scheme": "Bearer",
    "authority": "https://localhost:5001",
    "tokenEndpoint": "https://localhost:5001/_bdk/api/identity/connect/token",
    "clientId": "bdk-mcp",
    "clientSecret": "local-dev-secret",
    "scope": "openid profile email roles",
    "audience": "api"
  }
}
```

Token request:

```http id="ydxuud"
POST https://localhost:5001/_bdk/api/identity/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id=bdk-mcp
&client_secret=local-dev-secret
&scope=openid profile email roles
```

## Inline secrets and secret files

For frictionless local development, the runtime descriptor may contain inline secrets.

Supported fields:

```text id="u49jea"
clientSecret
clientSecretFile
password
passwordFile
```

If both inline and file-based values are supplied, the file-based value takes precedence.

Recommended precedence:

```text id="sa23gr"
clientSecretFile > clientSecret
passwordFile > password
```

Example using files:

```json id="e0jtld"
{
  "auth": {
    "type": "jwt-password",
    "tokenEndpoint": "https://localhost:5001/_bdk/api/identity/connect/token",
    "clientId": "bdk-mcp",
    "clientSecretFile": ".bdk/runtime/bdk-mcp.secret",
    "username": "admin@local",
    "passwordFile": ".bdk/runtime/bdk-mcp.password",
    "scope": "openid profile email roles"
  }
}
```

Inline secrets are acceptable for local development and local identity providers when teams intentionally choose friction over stronger local secret handling.

Secret files are preferred when the host generates temporary secrets or when descriptors are shared more broadly.

## Token use

After token acquisition, the MCP process sends:

```http id="vfy4uk"
Authorization: Bearer <access-token>
```

to all protected `/_bdk/api/*` calls.

The capabilities endpoint may also require authentication. Therefore the token must be acquired before reading capabilities when `auth.type` is not `none`.

## Token caching

The MCP process may cache tokens in memory.

Recommended behavior:

```text id="bwiwxk"
cache access token in memory only
refresh shortly before expiry
never log access tokens
never log passwords
never log client secrets
on 401, clear token cache and retry once
on 403, report missing role or policy
```

## Endpoint authorization

Endpoint authorization remains application-owned.

Authenticated-only endpoint:

```csharp id="e70dd9"
builder.Services.AddEndpoints<LogEntryEndpoints>(options => options
    .GroupPath("/_bdk/api/logentries")
    .RequireAuthorization());
```

Role-protected endpoint:

```csharp id="p5ehen"
builder.Services.AddEndpoints<LogEntryEndpoints>(options => options
    .GroupPath("/_bdk/api/logentries")
    .RequireAuthorization()
    .RequireRoles("Administrator"));
```

Policy-protected endpoint:

```csharp id="h0x5ew"
builder.Services.AddEndpoints<QueueingEndpoints>(options => options
    .GroupPath("/_bdk/api/queueing")
    .RequireAuthorization()
    .RequirePolicy("OperationsAccess"));
```

The MCP process does not need to know the role model ahead of time. It obtains the configured token and lets the host enforce authorization.

## Authorization failures

A `401 Unauthorized` response means:

```text id="ii1jtx"
no token
expired token
invalid token
invalid issuer
invalid audience
invalid signature
```

A `403 Forbidden` response means:

```text id="wdrb5s"
token is valid
but endpoint role or policy requirements are not satisfied
```

MCP tools should return structured auth failures instead of raw exception output.

Example:

```json id="dh5axs"
{
  "available": false,
  "code": "forbidden",
  "statusCode": 403,
  "runtimeId": "billing-api-5101",
  "feature": "logs",
  "message": "The token is valid, but it does not satisfy the endpoint authorization requirements."
}
```

## Localhost and Docker Scope

This feature is intended for local development and controlled Docker development.

Supported scenarios:

```text id="h8bfkd"
bdk mcp on host -> app on https://localhost:5001
bdk mcp on host -> app in Docker through published localhost port
bdk mcp in devcontainer -> app in sibling Docker container over configured Docker network
```

A valid JWT is still required. Localhost or Docker network access does not bypass authentication.

Applications may choose to restrict `/_bdk/api/*` endpoints to local development or local network ranges, but that is an application-side security decision.

## Operational Endpoint Base

All DevKit operational endpoints used by this feature are under:

```text id="eagddk"
/_bdk/api
```

Examples:

```text id="ux0qvv"
/_bdk/api/capabilities
/_bdk/api/logentries
/_bdk/api/metrics
/_bdk/api/messaging
/_bdk/api/queueing
/_bdk/api/jobs
/_bdk/api/orchestrations
/_bdk/api/health
/_bdk/api/notifications
```

## Existing Feature Endpoint Mapping

The MCP process maps tools to feature endpoint operations reported by capabilities.

Expected endpoint areas:

| Feature        | Default base path          | Purpose                                                                               |
| -------------- | -------------------------- | ------------------------------------------------------------------------------------- |
| capabilities   | `/_bdk/api/capabilities`   | Runtime feature and operation metadata.                                               |
| logs           | `/_bdk/api/logentries`     | Persisted log query, stream, export, statistics.                                      |
| metrics        | `/_bdk/api/metrics`        | Runtime and persisted metric snapshots, counters, gauges, timers, and metric queries. |
| health         | `/_bdk/api/health`         | Health snapshot and readiness information.                                            |
| messaging      | `/_bdk/api/messaging`      | Broker messages, subscriptions, handler states, stats.                                |
| queueing       | `/_bdk/api/queueing`       | Queue messages, waiting items, queue state, stats.                                    |
| jobs           | `/_bdk/api/jobs`           | Job definitions, runs, stats, trigger/control operations.                             |
| orchestrations | `/_bdk/api/orchestrations` | Orchestration instances, state, history, signals, timers.                             |
| notifications  | `/_bdk/api/notifications`  | Notification outbox and delivery state where exposed.                                 |

The capabilities endpoint must be used to determine the actual path and available operations.

## MCP Tool Catalog

## Runtime tools

These tools do not require a selected runtime unless explicitly stated.

```text id="ds97bq"
bdk.runtimes.list
bdk.runtimes.select
bdk.runtimes.current
bdk.runtimes.refresh
```

## Capabilities tools

```text id="ow44gl"
bdk.diagnostics.capabilities
```

Returns capabilities for the selected runtime.

If no runtime is selected and exactly one ready runtime exists, the MCP server may auto-select that runtime.

## Logs and errors

```text id="ovvx57"
bdk.logs.query
bdk.logs.tail
bdk.errors.recent
bdk.errors.details
```

`bdk.logs.query` supports filters such as:

```text id="h34gam"
from
to
age
level
traceId
spanId
correlationId
logKey
moduleName
shortTypeName
searchText
limit
continuationToken
```

`bdk.logs.tail` returns the newest matching entries. It must be bounded.

`bdk.errors.recent` is a convenience wrapper around log query filters for error and fatal levels.

`bdk.errors.details` returns one error and related logs when correlation, trace, or span metadata is available.

## Metrics

```text id="wudzui"
bdk.metrics.snapshot
bdk.metrics.query
bdk.metrics.series
bdk.metrics.summary
```

`bdk.metrics.snapshot` returns the current metric values available from the selected runtime.

`bdk.metrics.query` queries persisted or aggregated metrics when the selected runtime exposes that operation.

`bdk.metrics.series` returns time-based metric values where supported.

`bdk.metrics.summary` returns grouped metric statistics such as count, min, max, average, percentiles, or feature-specific aggregates where supported.

Possible filters:

```text id="c57i82"
from
to
metricName
category
moduleName
tags
aggregation
interval
limit
```

Metrics tools must be bounded and should avoid returning high-cardinality raw measurements unless explicitly requested and accepted by the endpoint.

## Correlation inspection

```text id="uv5yr8"
bdk.correlation.inspect
```

Inputs:

```text id="eq3i8i"
correlationId
traceId
spanId
from
to
limit
```

Returns:

```text id="zt9vuo"
matching logs
matching errors
related metrics where discoverable
related messages where discoverable
related queue messages where discoverable
related job runs where discoverable
related orchestration instances where discoverable
summary counts
time range
```

This tool is expected to become one of the highest-value diagnostic workflows.

## Health

```text id="w0dqu5"
bdk.health.snapshot
```

Returns a health snapshot for the selected runtime.

The exact data depends on the selected host’s health endpoint and registered health checks.

## Messaging

```text id="m99a2b"
bdk.messages.summary
bdk.messages.subscriptions
bdk.messages.waiting
bdk.messages.list
bdk.messages.details
bdk.messages.content
```

Read-only by default.

Future operations may include:

```text id="sydz91"
bdk.messages.retry
bdk.messages.releaseLease
bdk.messages.archive
bdk.messages.pauseType
bdk.messages.resumeType
bdk.messages.purge
```

These must require explicit operation mode and normal endpoint authorization.

## Queueing

```text id="s9xgqb"
bdk.queueing.summary
bdk.queueing.subscriptions
bdk.queueing.waiting
bdk.queueing.messages
bdk.queueing.messageDetails
```

Read-only by default.

Future operations may include:

```text id="nu4dk8"
bdk.queueing.retry
bdk.queueing.releaseLease
bdk.queueing.archive
bdk.queueing.pauseQueue
bdk.queueing.resumeQueue
bdk.queueing.pauseType
bdk.queueing.resumeType
bdk.queueing.purge
```

## Jobs

```text id="fj6zn4"
bdk.jobs.list
bdk.jobs.details
bdk.jobs.runs
bdk.jobs.runStats
```

Read-only by default.

Future operations may include:

```text id="ffs3u2"
bdk.jobs.trigger
bdk.jobs.pause
bdk.jobs.resume
bdk.jobs.interrupt
bdk.jobs.purgeRuns
```

## Orchestrations

```text id="t4t96r"
bdk.orchestrations.list
bdk.orchestrations.instances
bdk.orchestrations.instanceDetails
bdk.orchestrations.history
bdk.orchestrations.signals
bdk.orchestrations.timers
```

Read-only by default.

Future operations may include:

```text id="pb3nmd"
bdk.orchestrations.signal
bdk.orchestrations.pause
bdk.orchestrations.resume
bdk.orchestrations.cancel
bdk.orchestrations.terminate
bdk.orchestrations.repair
```

## Tool behavior for unavailable features

Tools should return structured unavailable results when a feature is not available.

Example:

```json id="s0rzpd"
{
  "available": false,
  "code": "feature_unavailable",
  "runtimeId": "billing-api-5101",
  "feature": "orchestrations",
  "reason": "Orchestration endpoints are not registered in the selected runtime."
}
```

The MCP server should not crash because one feature is missing.

## Read-Only and Operations Toolsets

The first version exposes read-only tools by default.

Read-only tools may still expose sensitive information and must still honor authentication and authorization.

Mutating tools are not part of the first implementation unless explicitly enabled by a future operations toolset.

Potential future CLI option:

```bash id="g2zqm9"
dotnet tool run bdk mcp --toolset diagnostics
dotnet tool run bdk mcp --toolset diagnostics,operations
```

Potential toolsets:

| Toolset       | Purpose                                                |
| ------------- | ------------------------------------------------------ |
| `diagnostics` | Read-only inspection tools. Default.                   |
| `operations`  | Retry, pause, resume, trigger, release lease. Future.  |
| `admin`       | Purge, destructive cleanup, broad maintenance. Future. |

Even when a toolset is enabled, the selected host’s authorization remains the final authority.

## MCP Response Model

MCP tools should return compact structured DTOs.

Common envelope shape:

```json id="utvjpb"
{
  "available": true,
  "runtimeId": "billing-api-5101",
  "feature": "logs",
  "data": {}
}
```

Unavailable shape:

```json id="lh3ow1"
{
  "available": false,
  "runtimeId": "billing-api-5101",
  "feature": "logs",
  "code": "feature_unavailable",
  "reason": "Log entry endpoints are not registered."
}
```

Error shape:

```json id="d5ejby"
{
  "available": false,
  "runtimeId": "billing-api-5101",
  "feature": "logs",
  "code": "http_error",
  "statusCode": 500,
  "reason": "The selected runtime returned an error while querying logs."
}
```

Authorization failure shape:

```json id="gj5k2j"
{
  "available": false,
  "runtimeId": "billing-api-5101",
  "feature": "logs",
  "code": "forbidden",
  "statusCode": 403,
  "reason": "The token is valid but does not satisfy the endpoint authorization requirements."
}
```

## Limits and Defaults

Default limits:

| Tool                        |       Default limit |
| --------------------------- | ------------------: |
| log query                   |            100 rows |
| log tail                    |            100 rows |
| recent errors               |             50 rows |
| metric query                | 100 rows or buckets |
| metric series               |         100 buckets |
| correlation inspection logs |            200 rows |
| message list                |            100 rows |
| queue message list          |            100 rows |
| job runs                    |            100 rows |
| orchestration instances     |            100 rows |
| orchestration history       |            200 rows |

The MCP process should pass explicit limits to HTTP endpoints when supported.

The server remains responsible for enforcing maximum limits.

Recommended maximums:

| Tool                        |        Maximum limit |
| --------------------------- | -------------------: |
| log query                   |            1000 rows |
| metric query                | 1000 rows or buckets |
| metric series               |         1000 buckets |
| correlation inspection logs |            2000 rows |
| message list                |            1000 rows |
| queue message list          |            1000 rows |
| job runs                    |            1000 rows |
| orchestration history       |            2000 rows |

## Time Defaults

When no date range is supplied:

* logs default to recent entries or current day, depending on endpoint support
* metrics default to recent entries, current process snapshot, or current day depending on endpoint support
* errors default to recent entries or current day
* correlation inspection may search recent/current day first
* jobs default to recent runs
* messages default to active/recent retained messages
* queueing defaults to active/recent retained messages
* orchestrations default to active/recent instances

All timestamps exchanged by MCP tools and operational endpoints should be UTC.

## Dashboard Relationship

The dashboard and MCP are sibling presentation surfaces.

Allowed:

```text id="yxqceu"
Dashboard page -> internal DI service -> feature service
MCP tool -> HTTP endpoint -> feature service
```

Not allowed:

```text id="cwfit2"
MCP tool -> dashboard page
MCP tool -> Razor page
MCP tool -> dashboard JavaScript
Dashboard page -> MCP tool
MCP process -> application DbContext
```

Dashboard pages may continue to use internal DI services.

The MCP process runs outside the application and therefore uses the operational HTTP endpoint contract.

## OpenAPI Relationship

The MCP server itself is a STDIO process and does not expose OpenAPI endpoints.

Existing `/_bdk/api/*` endpoints may be included or excluded from OpenAPI based on endpoint options.

The runtime descriptor writer and capabilities endpoint should follow the DevKit operational endpoint conventions.

Applications may choose to hide `/_bdk/api/*` endpoints from public OpenAPI output when those endpoints are intended for operational tooling only.

## Local and Docker Scenarios

## Host process on developer machine

```text id="ompwph"
bdk mcp runs on host
application runs on host
base URL: https://localhost:5001
descriptor path: .bdk/runtimes/*.json
```

## App in Docker, MCP on host

```text id="hxp9ag"
bdk mcp runs on host
application runs in Docker
application exposes published port 5001
descriptor is written into mounted workspace volume
hostBaseUrl: https://localhost:5001
```

## MCP in devcontainer, app in sibling container

```text id="b8agsd"
bdk mcp runs inside devcontainer
application runs in sibling container
descriptor is available through mounted workspace volume
containerBaseUrl: http://billing-api:8080
```

The MCP process chooses the reachable URL based on descriptor metadata and connectivity.

## Failure Handling

The MCP server should start even when:

```text id="orgoyb"
no runtime is running
multiple runtimes are running
selected runtime is stale
selected runtime is unreachable
auth metadata is missing
token acquisition fails
capabilities endpoint is unavailable
a feature endpoint is not registered
a feature endpoint returns 401 or 403
```

The MCP process should return structured results that help the agent or developer recover.

## Common failures

| Failure                   | Tool behavior                                                 |
| ------------------------- | ------------------------------------------------------------- |
| No runtimes found         | Return `no_runtime_found` with hints.                         |
| Multiple runtimes found   | Return `runtime_selection_required`.                          |
| Descriptor stale          | Mark runtime `Stale`; suggest runtime cleanup or app restart. |
| Token request failed      | Return `auth_token_request_failed`.                           |
| Capabilities unauthorized | Return `unauthorized`.                                        |
| Capabilities forbidden    | Return `forbidden`.                                           |
| Feature unavailable       | Return `feature_unavailable`.                                 |
| Endpoint 500              | Return `http_error` with status and sanitized message.        |
| Timeout                   | Return `runtime_timeout`.                                     |

## CLI Command Details

## bdk mcp

Starts the STDIO MCP server.

Options:

```text id="ieqgd0"
--runtime-id <id>
--runtime-url <url>
--workspace <path>
--toolset <toolsets>
--verbose
```

Default:

```bash id="omhrbu"
dotnet tool run bdk mcp
```

## bdk runtimes list

Lists discovered runtimes.

Example output:

```text id="qr5qno"
Runtime ID          App             URL                       Status
billing-api-5101    Billing.Api     https://localhost:5101     Ready
inventory-api-5201  Inventory.Api   https://localhost:5201     Forbidden
worker-5301         Worker.Host     https://localhost:5301     Stale
```

## bdk runtimes current

Shows the selected runtime.

## bdk runtimes select

Selects a runtime.

```bash id="huxfzy"
dotnet tool run bdk runtimes select billing-api-5101
```

Writes:

```text id="sjw574"
.bdk/runtime-selection.json
```

## bdk runtimes clean

Removes stale runtime descriptors.

A descriptor is stale when:

```text id="ygwrrh"
process id no longer exists
capabilities endpoint is unreachable
startedAt is too old and liveness cannot be confirmed
descriptor is invalid
```

The command should ask for confirmation unless `--yes` is supplied.

## bdk version

Shows CLI version and optionally detected DevKit runtime versions.

## Web Host Registration

## Runtime descriptor registration

Target API:

```csharp id="wh538j"
builder.Services.AddDevKitRuntimeDescriptors(options =>
{
    options.Enabled = builder.Environment.IsDevelopment();
    options.WorkspacePath = builder.Environment.ContentRootPath;
    options.OperationalBasePath = "/_bdk/api";
    options.CapabilitiesPath = "/_bdk/api/capabilities";
});
```

At application startup, the descriptor writer should write the runtime descriptor.

At graceful shutdown, it should delete or mark the descriptor stale where possible.

## Capabilities endpoint registration

Target API:

```csharp id="k9pu95"
builder.Services.AddDevKitOperationalCapabilities();

builder.Services.AddEndpoints<DevKitCapabilitiesEndpoints>(options => options
    .GroupPath("/_bdk/api")
    .RequireAuthorization());
```

or:

```csharp id="zdyjcz"
builder.Services.AddDevKitOperationalEndpoints(options => options
    .GroupPath("/_bdk/api")
    .RequireAuthorization());
```

The exact API shape can evolve, but the concepts must remain:

```text id="glb3ck"
capabilities are exposed under /_bdk/api/capabilities
capabilities are protected like other BDK endpoints
feature endpoint registrations contribute capability metadata
```

## Feature endpoint registration examples

Logs:

```csharp id="rm58lg"
builder.Services.AddEndpoints<LogEntryEndpoints>(options => options
    .GroupPath("/_bdk/api/logentries")
    .RequireAuthorization()
    .RequireRoles("Administrator"));
```

Metrics:

```csharp id="fctyzg"
builder.Services.AddEndpoints<MetricsEndpoints>(options => options
    .GroupPath("/_bdk/api/metrics")
    .RequireAuthorization()
    .RequireRoles("Administrator"));
```

Queueing:

```csharp id="lvjnnf"
builder.Services.AddQueueing(builder.Configuration)
    .WithEntityFrameworkBroker<AppDbContext>()
    .AddEndpoints(options => options
        .GroupPath("/_bdk/api/queueing")
        .RequireAuthorization()
        .RequireRoles("Administrator"));
```

Messaging:

```csharp id="jr0dsj"
builder.Services.AddMessaging(builder.Configuration)
    .WithEntityFrameworkBroker<AppDbContext>()
    .AddEndpoints(options => options
        .GroupPath("/_bdk/api/messaging")
        .RequireAuthorization()
        .RequireRoles("Administrator"));
```

Orchestrations:

```csharp id="a8ob71"
builder.Services.AddOrchestrations()
    .WithEntityFramework<AppDbContext>()
    .AddEndpoints(options => options
        .GroupPath("/_bdk/api/orchestrations")
        .RequireAuthorization()
        .RequireRoles("Administrator"));
```

## Local Identity Provider Setup

For local development, hosts can use a local identity provider.

Recommended local password grant setup:

```json id="x2g921"
{
  "auth": {
    "type": "jwt-password",
    "tokenEndpoint": "https://localhost:5001/_bdk/api/identity/connect/token",
    "clientId": "bdk-mcp",
    "clientSecret": "local-dev-secret",
    "username": "admin@local",
    "password": "admin",
    "scope": "openid profile email roles",
    "audience": "api"
  }
}
```

The local identity provider must issue a token with whatever role or policy the `/_bdk/api/*` endpoints require.

If endpoints require `Administrator`, then the local user or local client must receive `Administrator`.

No special DevKit-only diagnostic role is required.

## Security Considerations

## Secrets

Inline secrets are supported for frictionless local development.

Secret files are supported when stronger local handling is desired.

Rules:

```text id="wpyozd"
never log client secrets
never log passwords
never log access tokens
exclude .bdk/runtime/*.secret and .bdk/runtime/*.password from source control
prefer generated temporary secrets when practical
```

## Source control

Recommended `.gitignore` additions:

```gitignore id="eeos9j"
.bdk/runtimes/
.bdk/runtime-selection.json
.bdk/runtime/*.secret
.bdk/runtime/*.password
```

If teams want to commit sample descriptors, they should use:

```text id="rqtcdu"
.bdk/runtimes/*.sample.json
```

without real secrets.

## Network exposure

The feature is intended for local and Docker development.

Applications should avoid exposing `/_bdk/api/*` publicly unless intentionally secured.

## Least privilege

For development, many teams may use `Administrator`.

For production-like environments, prefer a narrower role or policy, but the DevKit MCP feature does not mandate a separate role model.

## Auditing

Operational endpoints should log normal authenticated access where existing endpoint logging supports it.

Future mutating MCP tools should include clear audit metadata such as:

```text id="hmaine"
tool name
runtime id
caller identity
correlation id
operation id
```

## Testing Strategy

## Unit tests

Cover:

```text id="b02fxe"
runtime descriptor serialization
runtime descriptor validation
runtime discovery
selection behavior
stale descriptor cleanup
auth descriptor parsing
clientSecretFile/clientSecret precedence
passwordFile/password precedence
token acquisition request construction
capabilities response parsing
tool-to-operation mapping
structured unavailable results
```

## Integration tests

Cover:

```text id="dlvypn"
bdk mcp starts over STDIO
runtime tools work without a running host
multiple descriptors require runtime selection
single runtime auto-selection
capabilities endpoint call with password grant
capabilities endpoint call with client credentials
401 retry behavior
403 reporting behavior
log query tool calls /_bdk/api/logentries
metrics snapshot tool calls /_bdk/api/metrics
queueing summary tool calls /_bdk/api/queueing
messaging summary tool calls /_bdk/api/messaging
orchestration list tool calls /_bdk/api/orchestrations
```

## Web host tests

Cover:

```text id="udwmb9"
descriptor written on startup
descriptor contains correct paths
descriptor contains expected auth metadata
capabilities endpoint aggregates registered providers
unregistered feature appears unavailable
endpoint authorization is respected
capabilities endpoint can be protected
metrics capabilities appear when metrics endpoints are registered
```

## Example app verification

Example applications should demonstrate:

```text id="daoq8p"
local tool manifest
VS Code MCP config
one-host monolith scenario
multi-host microservice scenario
jwt-password auth with local IDP
client credentials auth where useful
logs query
metrics snapshot
recent errors
queueing summary
messaging summary
orchestration list where registered
runtime selection
```

## Implementation Phases

## Phase 1: CLI skeleton and local tool

Create `BridgingIT.DevKit.Cli`.

Implement:

```text id="ejbhdk"
bdk version
bdk runtimes list
bdk runtimes current
bdk runtimes select
bdk runtimes clean
```

Package as a .NET tool.

Document local tool manifest setup.

## Phase 2: Runtime descriptors

Add runtime descriptor writing to `Presentation.Web`.

Support:

```text id="q7f7ey"
.bdk/runtimes/*.json
schemaVersion 1
runtime id
application metadata
URLs
/_bdk/api paths
auth metadata
descriptor cleanup on shutdown
```

## Phase 3: Capabilities endpoint

Add:

```text id="inecok"
/_bdk/api/capabilities
IDevKitOperationalCapabilityProvider
feature capability registration helpers
authorization metadata where known
```

Update feature endpoint packages to contribute capabilities when endpoints are registered.

## Phase 4: Authentication

Implement MCP-side token acquisition for:

```text id="k1af71"
jwt-password
jwt-client-credentials
none
```

Support:

```text id="f49ic5"
clientSecret
clientSecretFile
password
passwordFile
in-memory token cache
401 refresh retry
403 structured failure
```

## Phase 5: STDIO MCP server

Implement:

```text id="hcv9c8"
bdk mcp
runtime MCP tools
capabilities MCP tool
runtime selection behavior
structured error handling
```

## Phase 6: Read-only diagnostics tools

Implement read-only wrappers for:

```text id="y3v7tt"
logs
errors
metrics
health
messaging
queueing
jobs
orchestrations
```

Tool implementation depends on capabilities metadata and selected runtime.

## Phase 7: Examples and docs

Add documentation for:

```text id="mnp36j"
local tool install
VS Code config
runtime descriptors
multi-host selection
local IDP password grant
client credentials grant
capabilities endpoint
feature endpoint registration
metrics endpoint usage
```

Update example apps.

## Phase 8: Future operations toolset

Add mutating tools only after the read-only diagnostics flow is stable.

Candidate future operations:

```text id="pza9qi"
retry message
release lease
archive terminal message
pause queue
resume queue
trigger job
pause job
resume job
send orchestration signal
cancel orchestration
terminate orchestration
```

All mutating tools must be explicitly enabled and authorized by the selected host.

## Open Questions

* Should the CLI generate `.vscode/mcp.json` automatically?
* Should the selected runtime be persisted per workspace, per user, or both?
* Should capabilities include exact authorization metadata, or only report it where endpoint options expose it reliably?
* Should the descriptor writer support custom descriptor directories?
* Should Docker URL selection be automatic or explicitly configured?
* Should token secrets be generated by the host, configured by the user, or both?
* Should operation tools be a separate CLI option, separate MCP server name, or separate capability namespace?
* Should cross-runtime correlation inspection be added later as an explicit multi-runtime tool?
* Which metrics operations are part of the MVP: snapshot only, query only, series, or all three?

## Summary

The DevKit STDIO MCP Diagnostics feature provides a local agent adapter for running DevKit applications.

The important boundaries are:

```text id="bnkow6"
MCP is STDIO-only.
The CLI hosts MCP.
The app does not host MCP.
The app exposes /_bdk/api/* operational endpoints.
Each running host writes a descriptor.
Multiple hosts are supported.
One runtime is selected for normal diagnostic tools.
Capabilities are discovered from the selected host.
Auth uses normal JWT bearer tokens.
Password grant and client credentials are supported.
No direct database access happens from the MCP process.
No inter-service discovery is included.
Metrics are a first-class diagnostics feature.
```

This keeps the feature useful for local coding agents while preserving DevKit architecture, endpoint security, and feature ownership.
