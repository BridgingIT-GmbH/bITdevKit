# ADR-0023: STDIO MCP Server with SDK-Free App Handlers

## Status

Accepted

## Date

2026-07-01

## Context

The DevKit MCP feature must expose a coding-agent protocol surface while preserving feature ownership inside the running application process.

The system has two distinct boundaries:

1. The outer MCP protocol boundary between an MCP client and `bdk mcp`
2. The internal DevKit operation boundary between `bdk mcp` and app-side feature handlers

The app-side handlers must be reusable DevKit abstractions and should not depend on any MCP transport SDK. At the same time, the CLI must speak enough MCP over STDIO for current MCP clients.

## Decision

Implement `bdk mcp` as a STDIO JSON-RPC MCP server in `src/Presentation.Cli`, while keeping app-side handler contracts SDK-free in `src/Common.Abstractions/Mcp`.

The initial CLI MCP server implements:

- `initialize`
- `tools/list`
- `tools/call`
- stable `bdk_*` tool catalog
- structured tool responses
- protocol-clean stdout

App-side handlers implement DevKit-owned contracts:

- `IMcpHandler`
- `McpRequest`
- `McpResponse`
- `McpCapability`

The official MCP .NET SDK is not used in the initial implementation. It remains a viable future replacement for the CLI STDIO transport only. It must not replace the DevKit app-side handler abstraction.

## Rationale

1. **Boundary clarity**: MCP protocol concerns stay in the CLI adapter; feature handlers stay protocol-neutral.
2. **Small initial slice**: The first implementation covers the required MCP methods without changing package dependency shape.
3. **Stable internal contract**: Feature packages and projects can implement `IMcpHandler` without taking an MCP SDK dependency.
4. **Future compatibility path**: The CLI transport can later be refactored to the official SDK while keeping `McpToolExecutor`, IPC, and handlers mostly unchanged.
5. **Protocol-clean output**: The CLI can bypass normal Console Commands rendering for `bdk mcp`.

## Consequences

### Positive

- App-side DevKit features remain independent from MCP SDK versions.
- The stable MCP tool catalog can be tested without starting a web app.
- The CLI owns protocol framing and can keep stdout free of banners or logs.
- A future SDK migration is localized to the CLI transport adapter.

### Negative

- DevKit currently owns MCP protocol compatibility for the implemented methods.
- New MCP protocol capabilities must be added manually unless the SDK is adopted later.
- The hand-rolled transport must be tested against real MCP clients.

### Neutral

- The CLI currently targets the implemented MCP JSON-RPC surface, not every optional MCP protocol feature.
- The official MCP .NET SDK is still appropriate for a future outer transport refactor.

## Alternatives Considered

- **Use the official MCP .NET SDK immediately**
  - Not chosen for the initial slice to keep the implementation small and avoid coupling app-side handler design to SDK APIs.
  - Still considered a good future option for the CLI STDIO transport.

- **Expose app-side handlers directly through the MCP SDK**
  - Rejected because feature handlers must remain DevKit abstractions and must not depend on MCP transport packages.

- **Host MCP inside the ASP.NET Core application**
  - Rejected because the specification requires the only MCP server to be the `bdk mcp` STDIO process.

## Related Decisions

- [ADR-0021](0021-bdk-cli-local-development-tooling.md): BDK CLI local development tooling
- [ADR-0024](0024-local-ipc-runtime-bridge.md): Local IPC runtime bridge

## References

- [MCP diagnostics specification](../specs/spec-presentation-web-mcp-diagnostics.md)
- [Official MCP C# SDK v1.0 announcement](https://devblogs.microsoft.com/dotnet/release-v10-of-the-official-mcp-csharp-sdk/)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [MCP tools specification](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
- [MCP transports specification](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)

## Notes

### Implementation Files

- `src/Presentation.Cli/Mcp/StdioMcpServer.cs`
- `src/Presentation.Cli/Mcp/McpToolCatalog.cs`
- `src/Presentation.Cli/Mcp/McpToolExecutor.cs`
- `src/Common.Abstractions/Mcp/*`
