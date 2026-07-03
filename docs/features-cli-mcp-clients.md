# BDK MCP Client Configuration

This page documents how to configure common MCP clients. For day-to-day usage, prompts, runtime selection and troubleshooting, see [DevKit MCP](./features-cli-mcp.md).

This repository is the `bdk` CLI source repository, so the checked-in Visual Studio and VS Code MCP configurations start the server from source:

```json
{
  "servers": {
    "bdk": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/Presentation.Cli/Presentation.Cli.csproj",
        "--",
        "mcp",
        "--toolset",
        "diagnostics,operations,admin"
      ]
    }
  }
}
```

For applications that consume the packaged CLI as a local .NET tool, use this command instead:

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

## Visual Studio

Visual Studio discovers the repository-level `.mcp.json` file.

## VS Code

VS Code discovers `.vscode/mcp.json`.

## Rider

Rider stores MCP server definitions through JetBrains AI Assistant settings rather than a documented source-controlled project file. Add this server in Rider's MCP settings:

```json
{
  "mcpServers": {
    "bdk": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/Presentation.Cli/Presentation.Cli.csproj",
        "--",
        "mcp",
        "--toolset",
        "diagnostics,operations,admin"
      ]
    }
  }
}
```

For consuming applications, replace the Rider `args` with:

```json
["tool", "run", "bdk", "mcp"]
```

## Related Documentation

- [DevKit MCP](./features-cli-mcp.md)
- [DevKit CLI](./features-cli.md)
