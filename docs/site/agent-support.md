---
title: AI Agent Support
---

<img class="bdk-doc-icon" src="assets/features/mcp.svg" width="32" height="32" alt="" align="right">

# AI Agent Support

Connect an MCP-capable coding agent to official DevKit documentation and a local DevKit application with `bdk mcp`.

This integration is for local development. It does not expose a public HTTP endpoint or a production administration API.

## What an agent can do

DevKit MCP gives an agent both implementation guidance and evidence from the running application. An agent can:

- search official DevKit documentation and API symbols
- get implementation guidance for common DevKit features
- inspect the selected runtime, registered modules, health, metrics, logs, and errors
- inspect recorded operations and HTTP requests, find performance hotspots, and compare recordings
- inspect messaging, queueing, jobs, and orchestrations when the host exposes them
- call project-owned diagnostics registered by the application
- run explicitly enabled operations after you approve them

The available runtime tools depend on the features registered by the selected application.

## How the connection works

Your MCP client starts `bdk mcp` as a local process. The CLI finds a ready DevKit host in the current workspace and forwards runtime calls over local IPC.

Documentation and API search do not require a running host. Runtime diagnostics do.

## Inspect operations and requests

When operation profiling is enabled, the agent reads retained recordings through the same runtime services used by the profiling dashboard. It can filter by time range, operation kind, key, node, dimensions, and correlation ID.

| Tool | What the agent can inspect |
| --- | --- |
| `bdk_profiling_query` | Recent or slow executions, grouped operation counts, outcomes, and execution IDs |
| `bdk_profiling_details` | An execution's duration, dimensions, measurements, HTTP metadata, failures, and aggregate segment costs |
| `bdk_profiling_insights` | Hotspots ranked by p95 latency, Total duration, execution count, or failures, with retained sample counts and segment bottlenecks |
| `bdk_profiling_compare` | The same operation across time ranges, nodes, or application versions, including latency and segment cost changes |

Use `bdk_investigate_correlation` to connect operation recordings with related logs and errors. HTTP request recordings require the optional request profiling adapter. These tools read captured work; profiling must have been enabled when that work ran.

For example, ask the agent:

```text
Use bdk MCP to find slow HTTP requests in the last 15 minutes. Inspect a recorded execution and its segment costs, then follow its correlation ID to related logs and errors. Report the retained sample counts.
```

For coding help, use `bdk_guidance_get` with `topic="profiling"` to learn how to record operations, add segments, choose stable dimensions, and handle outcomes. This guidance works without a running application.

See [Profiling](reference/features-profiling.md) for capture setup and [MCP profiling tools](reference/features-cli-mcp.md#inspect-operation-and-request-recordings) for filters, comparison, and correlation investigations.

## Get started

MCP requires the `bdk` CLI, a DevKit web host with MCP enabled, and an MCP client configuration.

### 1. Install the `bdk` CLI

Install the CLI as a local .NET tool in the project repository:

```powershell
dotnet new tool-manifest
dotnet tool install BridgingIT.DevKit.Cli
```

Confirm the installation:

```powershell
dotnet tool run bdk --version
```

### 2. Enable MCP in the web host

Register MCP with the DevKit web application builder:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddConfiguration()
    .AddLogging()
    .AddModules(c => c
        .WithModule<CoreModule>())
    .AddMcp(c => c
        .WithHandlersFromAssembly<CoreModule>());
```

DevKit feature packages can register built-in handlers. Add project-owned handlers with `.WithHandler<THandler>()` or `.WithHandlersFromAssembly<TMarker>()`.

### 3. Start the application

Run the DevKit web host in local development. The startup log includes a BDK line similar to:

```text
[BDK] mcp handlers registered (...)
```

The host now has a local runtime descriptor and IPC endpoint.

### 4. Configure the MCP client

**GitHub Copilot (VS Code):**
Add a workspace-root `.mcp.json` to the project repository. It uses the portable `mcpServers` format, which VS Code and the Copilot harness both read:

```json
{
  "mcpServers": {
    "bdk": {
      "command": "dotnet",
      "args": ["tool", "run", "bdk", "mcp"]
    }
  }
}
```

Enable the experimental `chat.mcp.workspaceRootConfig.enabled` setting and run **Developer: Reload Window**. Then open **Chat: Open Customizations**, select **MCP Servers** under the Copilot harness, and approve the `bdk` server if prompted. The default **Configure Tools** picker in the chat input does not list workspace-root servers.

The previous `.vscode/mcp.json` file with a top-level `servers` object still works but is deprecated in VS Code. Keep it only if you need VS Code input variables, `cwd`, or per-server sandboxing.

**Codex:**
Add `.codex/config.toml` to the project repository:

```text
[mcp_servers.bdk]
command = "dotnet"
args = [
  "tool", "run", "bdk", "mcp"
]
cwd = ".."
startup_timeout_sec = 60
tool_timeout_sec = 120
enabled = true
```

Other clients use different configuration files. Keep the command and arguments equivalent. To enable the operations and admin tools, append `--toolset diagnostics,operations,admin` to the arguments (`["tool", "run", "bdk", "mcp", "--toolset", "diagnostics,operations,admin"]`). See [MCP Client Configuration](reference/features-cli-mcp-clients.md#full-toolset) for the full-toolset variants for VS Code, Visual Studio, Rider, and other supported clients, including the differences between the portable and VS Code formats.

### 5. Verify the connection

Ask the agent:

```text
Use the bdk MCP self-test and tell me whether the selected runtime is healthy.
```

The result should show one selected runtime in the ready state and list the operations advertised by the host.

Now ask the agent to use the documentation:

```text
Use the bdk MCP docs tools to find the DevKit guidance for jobs. Summarize the recommended implementation pattern and list the relevant API types.
```

This request works without a running host because the CLI reads the official DevKit documentation and API index.

## Go further

Use the [AI Agent Support Guide](agent-support-guide.md) for the architecture, tool catalog, coding workflow, prompt library, project-owned diagnostics, toolsets, and safety model.

Use the [DevKit MCP Reference](reference/features-cli-mcp.md) for the command and protocol contract.
