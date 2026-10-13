---
title: AI Agent Support Guide
---

# AI Agent Support Guide

This guide describes the DevKit MCP context model, runtime connection, tool catalog, prompt patterns, project-owned diagnostics, and safety controls.

For installation and the first self-test, start with [AI Agent Support](agent-support.md).

## Agent context

DevKit MCP gives agents four kinds of context:

| Context | Tools | Use |
| --- | --- | --- |
| Guidance | `bdk_guidance_list`, `bdk_guidance_get` | Get an implementation checklist for common DevKit work. |
| Documentation | `bdk_docs_search`, `bdk_docs_get` | Read official DevKit feature docs while coding in a consuming project. |
| API reference | `bdk_api_search`, `bdk_api_get` | Find DevKit types, members, overloads, extension methods, and signatures. |
| Runtime | `bdk_project_summary`, `bdk_capabilities_get`, feature tools | Inspect the selected application, its modules, capabilities, operational state, and retained operation and request recordings. |

For feature work, use this flow:

```text
Guidance -> Docs -> API reference -> Code -> Runtime verification
```

The guidance API uses one generic operation. For a request to add, create, or build DevKit code, call `bdk_guidance_get` with the request in `query`. The tool selects one or more relevant topics.

Example guidance call:

```json
{
  "query": "how to implement a new job that triggers an orchestration"
}
```

Guidance covers these DevKit areas:

- application patterns such as commands, queries, events, jobs, messaging, queueing, orchestration, pipelines, and startup tasks
- domain patterns such as ActiveEntity, domain events, repositories, specifications, domain modeling, results, and rules
- shared capabilities such as caching, mapping, serialization, utilities, filtering, modules, and Requester or Notifier
- storage and presentation features such as document, blob, and file storage, storage monitoring, and dashboard pages
- operation and request profiling, including recording ownership, segments, outcomes, dimensions, and capture configuration

For profiling implementation guidance, request the exact topic:

```json
{
  "topic": "profiling"
}
```

The guide explains how to start or join an operation, record nested or repeated segments, preserve results and cancellation, and choose stable grouping dimensions. Use `bdk_api_search` and `bdk_api_get` to confirm the signatures of `IOperationProfiler` and `OperationProfilingHelpers` before changing code. Guidance is available without a running application.

## Use docs while coding

`bdk mcp` exposes documentation and API reference tools directly to the agent:

| Tool | Purpose |
| --- | --- |
| `bdk_docs_search` | Search official DevKit documentation by topic. |
| `bdk_docs_get` | Load a bounded Markdown source returned by search. |
| `bdk_api_search` | Search official DevKit API symbols by type, member, namespace, topic, or keyword. |
| `bdk_api_get` | Load bounded API reference details for a symbol `uid` returned by search. |

The documentation tools read official DevKit documentation from GitHub. The API tools read generated reference metadata from GitHub Pages. Neither tool depends on a local `docs` folder in the consuming project.

A typical coding workflow is:

1. Search the DevKit docs for the feature being changed.
2. Summarize the expected pattern.
3. Search the API reference for the types and members involved.
4. Inspect the project for matching conventions.
5. Make the implementation change.
6. Use runtime tools to verify that the application advertises or executes the feature.

Example:

```text
Use the bdk MCP docs tools to read the DevKit Jobs guidance first.
Then implement a nightly customer cleanup job following the documented pattern.
After the change, use bdk_jobs_list to verify the running app advertises the job.
```

```text
Use bdk_guidance_get for results, read the linked docs, then use bdk_api_search and bdk_api_get for Result before changing error handling code.
```

## Runtime connection

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

The CLI discovers ready DevKit hosts for the current workspace and selects the current runtime. It forwards runtime calls over local IPC and ignores stale runtime descriptors.

## Tool catalog

The CLI owns the stable MCP tool catalog. The selected runtime advertises the application operations it supports.

| Area | Examples |
| --- | --- |
| Runtime | `bdk_mcp_status`, `bdk_mcp_self_test`, `bdk_mcp_explain_setup`, `bdk_runtimes_list`, `bdk_runtimes_current`, `bdk_runtimes_select`, `bdk_runtimes_refresh`, and `bdk_capabilities_get` |
| Health and metrics | `bdk_health_snapshot`, `bdk_metrics_snapshot`, and `bdk_metrics_query` |
| Logs and errors | `bdk_logs_query`, `bdk_logs_tail`, `bdk_errors_recent`, `bdk_errors_details`, and `bdk_correlation_inspect` |
| Operation and request profiling | `bdk_profiling_query`, `bdk_profiling_details`, `bdk_profiling_insights`, and `bdk_profiling_compare` |
| Investigations | `bdk_investigate_recent_errors`, `bdk_investigate_correlation`, `bdk_investigate_job_run`, and `bdk_investigate_orchestration_instance` |
| Messaging | summaries, subscriptions, retained messages, retry, archive, pause, and resume |
| Queueing | queue summaries, retained queue messages, retry, archive, and queue or type pause and resume |
| Blob Storage | `bdk_blobs_summary`, `bdk_blobs_clients`, and `bdk_blobs_probe` inspect registered clients and storage health without changing stored blobs |
| Jobs | job definitions, run history, run statistics, trigger, pause, resume, and interrupt |
| Orchestrations | instances, details, history, timers, signals, and runtime control |
| Project tools | application-owned diagnostics through `bdk_project_operations` and `bdk_project_call` |
| Project summary | selected runtime, registered modules, MCP capability groups, and project-owned operations |
| Guidance | `bdk_guidance_list` and `bdk_guidance_get` provide implementation checklists for application, domain, storage, presentation, and profiling features |
| Documentation | official DevKit documentation search and retrieval |
| API reference | official DevKit API symbols, signatures, summaries, and DocFX links |

Tools return structured unavailable responses when the selected host does not expose the required feature. Use `bdk_capabilities_get` to check availability. See the [MCP Reference](reference/features-cli-mcp.md#tool-areas) for the broader catalog and argument contracts.

Operations are grouped into toolsets:

- `diagnostics` contains the default read-oriented tools
- `operations` contains runtime controls such as retry, pause, resume, trigger, and signal
- `admin` contains destructive maintenance operations and requires explicit confirmation

Start the MCP server with explicit toolsets when you need more than diagnostics:

```powershell
dotnet tool run bdk mcp --toolset diagnostics,operations,admin
```

## Operation and request profiling

The four profiling tools belong to the default `diagnostics` toolset. They read retained recordings through `IOperationProfilingQueryService`, the same runtime service used by the dashboard. The dashboard does not need to be open, and operation recordings do not require a Runtime Profiling session.

Enable `.WithOperationProfiling()` when registering profiling in the application; add `.WithRequestProfiling()` for HTTP request capture. Recordings must have been captured before they can be queried.

| Tool | Use |
| --- | --- |
| `bdk_profiling_query` | Find recent or slow executions, or group them by operation count. Filter by time, kind, key, node, application version, correlation ID, outcomes, or dimensions. |
| `bdk_profiling_details` | Inspect an execution ID returned by query: duration, outcomes, HTTP metadata, dimensions, measurements, failures, and aggregate segment costs. |
| `bdk_profiling_insights` | Rank hotspots by `p95`, `total`, `count`, or `failures` (failure rate); inspect latency samples, failure rates, and segment bottlenecks. |
| `bdk_profiling_compare` | Compare the same exact operation `key` and `kind` under separate baseline and candidate conditions, such as time ranges, nodes, or application versions. |

For example, find slow retained HTTP requests:

```json
{
  "kind": "HttpRequest",
  "view": "Slow",
  "take": 10
}
```

Pass this object to `bdk_profiling_query`, then use a returned execution ID with `bdk_profiling_details`. Without date bounds, query and insights select the last 15 minutes. Date bounds select execution start times, with an inclusive `fromUtc` and exclusive `toUtc`.

Comparison defaults to completed outcomes. To compare failure rates across all outcomes, set `outcomes: []` in both the baseline and candidate selections.

Inspect sample counts and capture quality when interpreting results. Insights calculates latency, Total duration, and segment costs from completed executions; failure rates use all selected outcomes. Segment costs describe aggregated paths, rather than exact invocation order. If an analysis exceeds the runtime's configured bound, narrow the filters instead of treating a partial sample as a full ranking.

Both `bdk_correlation_inspect` and `bdk_investigate_correlation` return related logs, errors, and a separate `data.profiling` result. Use execution IDs from that result to inspect details. Logs and profiling can be available independently; profiling pagination uses `profilingCursor`, while logs use `continuationToken`.

See [MCP profiling tools](reference/features-cli-mcp.md#inspect-operation-and-request-recordings) for schemas, comparison examples, paging, and bounds, and [Profiling](reference/features-profiling.md) for recording and segment semantics.

## Prompt library

State what the agent must inspect first, what it can change, and when it must wait for approval. Runtime prompts require the application and `bdk mcp` to be running. Documentation, API reference, and guidance prompts work without a running application.

### Runtime orientation

```text
Use the bdk MCP tools to verify the selected runtime. Run the MCP self-test, inspect the available capabilities, and summarize which diagnostics are available before changing code.
```

```text
Use bdk MCP to check whether this application exposes logs, operation and request profiling, Blob Storage diagnostics, jobs, messaging, queueing, orchestrations, and project-owned operations. Tell me which areas are available and which are not.
```

### Debug a local feature

```text
Use bdk MCP to inspect the latest errors from the running app. For the newest error, use bdk_investigate_correlation to inspect related logs and operation recordings. Summarize the evidence and point me to the most likely code area.
```

```text
I just reproduced a bug locally. Use bdk MCP to tail recent warning and error logs from the last 10 minutes, then suggest the smallest code change to investigate first.
```

### Profiling and performance

```text
Use bdk_profiling_insights to find HTTP request hotspots in the last 15 minutes, ranked by p95. Query a matching execution and inspect its details and aggregate segment costs. Report retained sample counts and capture limitations before proposing a change.
```

```text
Use bdk_profiling_compare to compare operation <key> of kind <kind> before and after my change. Use the baseline and candidate time ranges I provide, include all outcomes on both sides to compare failure rates, and keep the other filters equivalent. Summarize latency, failure rates, and segment cost changes.
```

```text
Use bdk_investigate_correlation for <correlation-id>. Inspect both logs and profiling evidence, then use bdk_profiling_details for the relevant execution IDs. State which evidence sources are available.
```

### Blob Storage diagnostics

```text
Use bdk_blobs_summary and bdk_blobs_clients to inspect registered Blob Storage clients. Probe an unhealthy client with bdk_blobs_probe and summarize the result without changing stored blobs.
```

### Jobs, messaging, and queueing

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
Use bdk MCP to list project-owned operations. Choose the operation that best inspects a customer or product issue, and ask me for any required identifiers before calling it.
```

```text
Use bdk_project_operations to discover application-specific diagnostics. Then call the safest read-only project operation that helps explain why product <product-id> is not visible.
```

### Documentation-aware coding

```text
Use the bdk MCP docs tools to find the DevKit guidance for queueing retries. Compare that guidance with the current code before proposing changes.
```

```text
Before implementing this feature, use bdk MCP docs search for the relevant DevKit feature docs. Summarize the expected pattern, then inspect the codebase for matching conventions.
```

```text
I need a new DevKit job. Use bdk_docs_search and bdk_docs_get to read the Jobs docs first, then implement the job and verify it with bdk_jobs_list.
```

```text
Use bdk_guidance_get for jobs, then use the linked docs and bdk_project_summary before editing. After implementation, verify with bdk_jobs_list.
```

```text
Use bdk_guidance_get with topic profiling, read the linked docs, and confirm IOperationProfiler and OperationProfilingHelpers with the API tools. Add operation recordings and aggregate segments with stable dimensions to this feature. When the app is running, verify the captured work with bdk_profiling_query and bdk_profiling_details.
```

```text
Use bdk_project_summary to orient on this app's modules and MCP capabilities. Then choose the right DevKit guidance topic before proposing code changes.
```

```text
Use bdk_api_search for IRepository and Specification. Call bdk_api_get for the exact symbols you plan to use before editing repository code.
```

### Admin operations

```text
Use bdk MCP to inspect retained local test data older than yesterday. Do not purge anything. If cleanup is appropriate, show the exact admin call and wait for my approval.
```

For a destructive action, ask the agent to inspect and propose the operation first. Approve the operation in a second prompt with the explicit confirmation arguments.

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

Use client-safe operation names such as `catalog_inspect_product` or `orders_find_customer_context`. Agents discover these operations with `bdk_project_operations` and call them through `bdk_project_call`.

## Safety model

MCP support runs locally:

- no public MCP HTTP endpoint is exposed
- host communication uses local IPC with nonce validation
- runtime selection is workspace-aware and targets only ready runtimes
- tool responses enforce output bounds
- operations and admin tools require explicit enablement
- destructive admin operations require confirmation arguments

These controls separate local diagnostics from production administration.

## Related reference

- [MCP Client Configuration](reference/features-cli-mcp-clients.md)
- [DevKit MCP Reference](reference/features-cli-mcp.md)
- [Profiling](reference/features-profiling.md)
- [DevKit CLI Reference](reference/features-cli.md)
