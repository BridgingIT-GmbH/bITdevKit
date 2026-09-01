---
title: AI Agent Support
---

# AI Agent Support

Connect an MCP-capable coding agent to official DevKit documentation and a local DevKit application with `bdk mcp`.

This integration is for local development. It does not expose a public HTTP endpoint or a production administration API.

## What an agent can do

DevKit MCP gives an agent both implementation guidance and evidence from the running application. An agent can:

- search official DevKit documentation and API symbols
- get implementation guidance for common DevKit features
- inspect the selected runtime, registered modules, health, metrics, logs, and errors
- inspect messaging, queueing, jobs, and orchestrations when the host exposes them
- call project-owned diagnostics registered by the application
- run explicitly enabled operations after you approve them

The available runtime tools depend on the features registered by the selected application.

## How the connection works

Your MCP client starts `bdk mcp` as a local process. The CLI finds a ready DevKit host in the current workspace and forwards runtime calls over local IPC.

Documentation and API search do not require a running host. Runtime diagnostics do.

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

For VS Code, add `.vscode/mcp.json` to the project repository:

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

Other clients use different configuration files. Keep the command and arguments equivalent. See [MCP Client Configuration](reference/features-cli-mcp-clients.md) for VS Code, Visual Studio, Rider, and other supported clients.

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
