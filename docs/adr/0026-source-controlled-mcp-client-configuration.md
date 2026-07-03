# ADR-0026: Source-Controlled MCP Client Configuration

## Status

Accepted

## Date

2026-07-01

## Context

Developers use different IDEs and MCP clients. The new `bdk mcp` server should be easy to start from common local development tools without requiring each developer to rediscover the command.

The repository must support:

1. Visual Studio
2. VS Code
3. Rider
4. The DevKit source repository scenario
5. Consuming application repository scenarios

IDE configuration should avoid committing user-specific state such as `.vs/` or `.idea/`.

## Decision

Add source-controlled MCP client configuration where the IDE supports stable repo-level files:

- `.mcp.json` for Visual Studio
- `.vscode/mcp.json` for VS Code

For Rider, document the MCP server JSON in `docs/features-cli-mcp-clients.md` instead of committing `.idea` files, because Rider stores MCP server definitions through JetBrains AI Assistant settings.

In the DevKit source repository, the checked-in server command runs the CLI from source:

```text
dotnet run --project src/Presentation.Cli/Presentation.Cli.csproj -- mcp
```

For consuming projects, documentation shows the packaged local-tool command:

```text
dotnet tool run bdk mcp
```

## Rationale

1. **Low setup friction**: Visual Studio and VS Code users get an MCP server definition from source control.
2. **Avoid user state**: `.vs/` and `.idea/` remain ignored.
3. **Source repo correctness**: Running from source lets contributors test current CLI changes.
4. **Consumer guidance**: Documentation shows the command applications should use after installing the packaged CLI.

## Consequences

### Positive

- Common MCP-capable IDEs have a documented setup path.
- Contributors can test `bdk mcp` without installing the package globally.
- Consuming projects have a clear copyable configuration.

### Negative

- The source repository config differs from consuming project config.
- IDE MCP configuration formats may evolve and require updates.
- Rider setup remains a documented manual step rather than a checked-in file.

### Neutral

- MCP clients still require a running DevKit application process for runtime-bound tools.
- Documentation tools work even when no runtime is running.

## Alternatives Considered

- **Only document MCP setup**
  - Rejected because Visual Studio and VS Code support source-controlled MCP configuration files.

- **Commit Rider `.idea` configuration**
  - Rejected because `.idea/` is user-specific and ignored by repository policy.

- **Use packaged `dotnet tool run bdk mcp` in this source repository**
  - Rejected because contributors need to test the current source version of the CLI.

## Related Decisions

- [ADR-0021](0021-bdk-cli-local-development-tooling.md): BDK CLI local development tooling
- [ADR-0023](0023-stdio-mcp-server-with-sdk-free-app-handlers.md): STDIO MCP server

## References

- [MCP client setup](../features-cli-mcp-clients.md)
- [VS Code MCP configuration](https://code.visualstudio.com/docs/agents/reference/mcp-configuration)
- [Visual Studio MCP servers](https://learn.microsoft.com/en-us/visualstudio/ide/mcp-servers)
- [JetBrains MCP setup](https://www.jetbrains.com/help/ai-assistant/mcp.html)

## Notes

### Implementation Files

- `.mcp.json`
- `.vscode/mcp.json`
- `docs/features-cli-mcp-clients.md`
