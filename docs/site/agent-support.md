---
title: AI Agent Support
---

# AI Agent Support

`bITdevKit` supports coding agents through the `bdk mcp` command. MCP-capable IDEs and agents can use it to read official DevKit documentation, inspect a running application, and follow DevKit patterns in the current workspace.

This integration is for local development. It does not expose a public HTTP endpoint or a production administration API.

## Why it matters

Source code shows what an application can do. Runtime diagnostics show what it is doing now. DevKit MCP gives an agent both, together with official documentation and implementation guidance.

Common workflows include:

- check whether the selected local runtime is ready
- inspect application health and runtime metrics
- query retained logs and recent errors
- follow correlation IDs across logs and operational features
- inspect messaging and queueing state
- inspect and operate durable jobs
- inspect orchestration instances, history, signals, and timers
- call project-owned diagnostics exposed by the application
- search official DevKit documentation from the consuming project
- search official DevKit API reference symbols from the consuming project
- request curated implementation guidance for common DevKit feature work
- summarize the selected runtime, registered modules, and advertised MCP capabilities
- use DevKit docs and API reference while implementing jobs, queues, handlers, modules, endpoints, or project-owned MCP tools

## Agent context

DevKit MCP gives agents four kinds of context:

| Context | Tools | Use |
| --- | --- | --- |
| Guidance | `bdk_guidance_list`, `bdk_guidance_get` | Get a concise implementation checklist for common DevKit work. |
| Documentation | `bdk_docs_search`, `bdk_docs_get` | Read official DevKit feature docs while coding in a consuming project. |
| API reference | `bdk_api_search`, `bdk_api_get` | Find exact DevKit types, members, overloads, extension methods, and signatures. |
| Runtime | `bdk_project_summary`, `bdk_capabilities_get`, feature tools | Inspect the selected app, its modules, capabilities, and live operational state. |

For feature work, prefer this flow:

```text
Guidance -> Docs -> API reference -> Code -> Runtime verification
```

The guidance API uses one generic operation. For a request to add, create, or build DevKit code, call `bdk_guidance_get` with the natural-language request in `query`. The tool selects the relevant topics and can combine them, such as guidance for a job that triggers an orchestration.

Example guidance call:

```json
{
  "query": "how to implement a new job that triggers an orchestration"
}
```

Guidance covers the following DevKit areas:

- application patterns such as commands, queries, events, jobs, messaging, queueing, orchestration, pipelines, and startup tasks
- domain patterns such as ActiveEntity, domain events, repositories, specifications, domain modeling, results, and rules
- shared capabilities such as caching, mapping, serialization, utilities, filtering, modules, and requester or notifier
- storage and presentation features such as document, blob, and file storage, storage monitoring, and dashboard pages

## Use docs while coding

`bdk mcp` exposes documentation and API reference tools directly to the agent:

| Tool | Purpose |
| --- | --- |
| `bdk_docs_search` | Search official DevKit documentation by topic. |
| `bdk_docs_get` | Load a bounded markdown source returned by search. |
| `bdk_api_search` | Search official DevKit API reference symbols by type, member, namespace, topic, or keyword. |
| `bdk_api_get` | Load bounded API reference details for a symbol `uid` returned by search. |

The docs tools read official DevKit documentation from GitHub. The API tools read generated reference metadata from GitHub Pages. Neither tool depends on the consuming project's local `docs` folder. A project can therefore use `bdk` without copying the DevKit source repository.

A useful agent workflow is:

1. Search the DevKit docs for the feature being changed.
2. Summarize the expected pattern.
3. Search the API reference for the concrete types and members involved.
4. Inspect the existing project code for matching conventions.
5. Make the implementation change.
6. Use runtime MCP tools to verify that the app advertises or executes the feature.

Example:

```text
Use the bdk MCP docs tools to read the DevKit Jobs guidance first.
Then implement a nightly customer cleanup job following the documented pattern.
After the change, use bdk_jobs_list to verify the running app advertises the job.
```

```text
Use bdk_guidance_get for results, read the linked docs, then use bdk_api_search and bdk_api_get for Result before changing error handling code.
```

## How it works

```mermaid
flowchart LR
    Developer["Developer"]
    Agent["IDE or coding agent"]
    Mcp["bdk mcp"]
    Registry["Local host registry"]
    App["Running DevKit web host"]
    Handler["MCP handler"]
    Service["Application service"]

    Developer --> Agent
    Agent -->|"MCP over stdio"| Mcp
    Mcp -->|"discover ready runtimes"| Registry
    Mcp -->|"local IPC"| App
    App --> Handler
    Handler --> Service
```

The CLI discovers ready DevKit hosts for the current workspace and selects the current runtime. It forwards runtime tool calls over local IPC. MCP selection ignores stale runtime descriptors.

## Install and enable MCP

MCP requires two processes:

- the `bdk mcp` STDIO server that your IDE or agent starts
- a running DevKit web host with local MCP tooling enabled

### 1. Install the `bdk` .NET tool

Install the DevKit CLI as a local .NET tool in the project repository. Local tools pin the CLI version in `.config/dotnet-tools.json`, so the MCP setup is repeatable for the team.

```powershell
dotnet new tool-manifest
dotnet tool install BridgingIT.DevKit.Cli
```

```powershell
dotnet tool run bdk --version
```

Start the MCP server with:

```powershell
dotnet tool run bdk mcp --toolset diagnostics,operations,admin
```

### 2. Enable MCP in the web host

Use the DevKit web application builder and register MCP handlers. For local development, MCP follows the DevKit local tooling policy by default.

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddConfiguration()
    .AddLogging()
    .AddModules(c => c
        .WithModule<CoreModule>())
    .AddMcp(c => c
        .WithHandlersFromAssembly<CoreModule>());
```

DevKit feature packages can register their built-in handlers. Add project-owned handlers with `.WithHandler<THandler>()` or `.WithHandlersFromAssembly<TMarker>()`.

### 3. Start the application

Run the DevKit web host in local development. When MCP is enabled, the host writes a local runtime descriptor and starts a local IPC endpoint.

The startup log should include a BDK line similar to:

```text
[BDK] mcp handlers registered (...)
```

The MCP dashboard page can also show whether MCP is enabled and whether a `bdk mcp` server is connected to the runtime.

### 4. Configure the MCP client

For VS Code, add a repo-local `.vscode/mcp.json` entry that starts the `bdk mcp` server from the local .NET tool:

```json
{
  "servers": {
    "bdk": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["tool", "run", "bdk", "mcp"]
    }
  }
}
```

Some clients use `.mcp.json`, `.codex/config.toml`, or IDE-specific settings instead of `.vscode/mcp.json`. Keep the command and arguments equivalent.

For client-specific setup, see [MCP Client Configuration](reference/features-cli-mcp-clients.md).

### 5. Verify the setup

Ask the agent to run a self-test:

```text
Use the bdk MCP self-test and tell me whether the selected runtime is healthy.
```

Or call these tools from the MCP client:

- `bdk_mcp_status`
- `bdk_mcp_self_test`
- `bdk_runtimes_list`
- `bdk_capabilities_get`

The expected result is one selected runtime in the ready state and a capabilities response that lists the host operations.

## What agents can access

The stable MCP tool catalog is owned by the CLI. The selected runtime advertises the app-side operations it supports.

Built-in areas include:

| Area | Examples |
| --- | --- |
| Runtime | MCP status, self-test, capabilities, health, and metrics |
| Logs and errors | query logs, tail logs, recent errors, and inspect correlation IDs |
| Messaging | summaries, subscriptions, retained messages, retry, archive, pause, and resume |
| Queueing | queue summaries, retained queue messages, retry, archive, and queue or type pause and resume |
| Jobs | job definitions, run history, run statistics, trigger, pause, resume, and interrupt |
| Orchestrations | instances, details, history, timers, signals, and runtime control |
| Project tools | application-owned diagnostics through `bdk_project_operations` and `bdk_project_call` |
| Project summary | selected runtime, registered modules, MCP capability groups, and project-owned operations |
| Guidance | curated implementation checklists for jobs, messaging, queueing, orchestration, pipelines, and dashboard pages |
| Documentation | official DevKit docs search and retrieval for implementation guidance |
| API reference | official DevKit API symbols, signatures, summaries and DocFX links |

Operations are grouped into toolsets:

- `diagnostics`: default read-oriented tools
- `operations`: runtime control such as retry, pause, resume, trigger, or signal
- `admin`: destructive maintenance operations, always requiring explicit confirmation

## Useful development prompts

State what the agent must inspect first, what it can change, and when it must wait for approval. These examples assume that the application is running locally and the MCP client has started `bdk mcp`.

### Runtime orientation

```text
Use the bdk MCP tools to verify the selected runtime. Run the MCP self-test, inspect the available capabilities, and summarize which diagnostics are available before changing code.
```

```text
Use bdk MCP to check whether this application exposes logs, jobs, messaging, queueing, orchestrations, and project-owned operations. Tell me which areas are available and which are not.
```

### Debugging a failing local feature

```text
Use bdk MCP to inspect the latest errors from the running app. For the newest error, follow the correlation ID, summarize the related logs, and point me to the most likely code area.
```

```text
I just reproduced a bug locally. Use bdk MCP to tail recent warning and error logs from the last 10 minutes, then suggest the smallest code change to investigate first.
```

### Jobs, messaging and queueing

```text
Use bdk MCP to list recent job runs and failed executions. If a job failed, inspect its run details and related logs, then summarize the failure path.
```

```text
Use bdk MCP to inspect waiting queue messages and retained broker messages. Identify messages that look stuck, leased, failed, or ready for retry, but do not perform operations yet.
```

```text
Use bdk MCP operations to retry the failed queue message I identify. Before calling any operation, show me the exact MCP tool arguments you plan to use.
```

### Orchestrations

```text
Use bdk MCP to list active orchestration instances. For any failed or stuck instance, inspect details, history, signals, and timers, then summarize what happened.
```

```text
Use bdk MCP to investigate orchestration instance <instance-id>. Include history, signals, timers, and related correlation logs if available.
```

### Project-owned diagnostics

```text
Use bdk MCP to list project-owned operations, choose the operation that best inspects a customer or product issue, and ask me for any required identifiers before calling it.
```

```text
Use bdk_project_operations to discover application-specific diagnostics. Then call the safest read-only project operation that helps explain why product <product-id> is not visible.
```

### Documentation-aware coding

```text
Use the bdk MCP docs tools to find the DevKit guidance for queueing retries. Compare that guidance with the current code before proposing changes.
```

```text
Before implementing this feature, use bdk MCP docs search for the relevant DevKit feature docs, summarize the expected pattern, then inspect the codebase for existing matching conventions.
```

```text
I need a new DevKit job. Use bdk_docs_search and bdk_docs_get to read the Jobs docs first, then implement the job and verify it with bdk_jobs_list.
```

```text
Use bdk_guidance_get for jobs, then use the linked docs and bdk_project_summary before editing. After implementation, verify with bdk_jobs_list.
```

```text
Use bdk_project_summary to orient on this app's modules and MCP capabilities. Then choose the right DevKit guidance topic before proposing code changes.
```

```text
Use bdk_api_search for IRepository and Specification, then call bdk_api_get for the exact symbols you plan to use before editing repository code.
```

### Admin and destructive operations

```text
Use bdk MCP to inspect retained local test data older than yesterday. Do not purge anything. If cleanup is appropriate, show the exact admin call and wait for my approval.
```

For a destructive action, first ask the agent to inspect and propose the operation. Approve the operation in a second prompt with the explicit confirmation arguments.

## MCP client references

For VS Code, Visual Studio, Rider, and repo-local client examples, see:

- [MCP Client Configuration](reference/features-cli-mcp-clients.md)
- [DevKit MCP Reference](reference/features-cli-mcp.md)
- [DevKit CLI Reference](reference/features-cli.md)

## Add project-owned tools

Applications can expose their own diagnostics by implementing `IMcpHandler` and registering the handler through the DevKit web host builder:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddConfiguration()
    .AddLogging()
    .AddModules(c => c
        .WithModule<CoreModule>())
    .AddMcp(c => c
        .WithHandlersFromAssembly<CoreModule>());
```

Use client-safe project operation names such as `catalog_inspect_product` or `orders_find_customer_context`. Agents discover these operations with `bdk_project_operations` and call them through `bdk_project_call`.

## Safety model

MCP support runs locally:

- no public MCP HTTP endpoint is exposed
- host communication uses local IPC with nonce validation
- runtime selection is workspace-aware and only targets ready runtimes
- tool responses enforce output bounds
- operations and admin tools must be enabled explicitly
- destructive admin operations require confirmation arguments

These constraints separate local diagnostics from production administration.
