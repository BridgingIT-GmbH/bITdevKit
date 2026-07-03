# ADR-0024: Local IPC Runtime Bridge

## Status

Accepted

## Date

2026-07-01

## Context

The CLI and MCP server need to execute runtime operations inside a running DevKit application process. The CLI must not load application configuration, build the application service provider, query application databases, or call dashboard HTTP endpoints for normal MCP operation.

The bridge must:

1. Connect only to local development runtimes
2. Hide transport differences behind endpoint metadata
3. Use the running app's dependency injection container
4. Validate that the caller discovered the current endpoint
5. Return bounded, structured results
6. Let the application continue starting if the IPC server cannot bind

## Decision

Use local IPC endpoints advertised through runtime descriptors to connect `bdk` to running DevKit hosts.

For Windows, the current implementation uses named pipes. Endpoint metadata contains:

- feature name
- protocol version
- transport
- endpoint address
- nonce

The MCP bridge sends `McpIpcRequest` envelopes to the app-side `McpIpcServer`, which validates nonce and protocol version, then dispatches through `McpDispatcher` to registered `IMcpHandler` instances.

## Rationale

1. **No HTTP dependency**: MCP operations do not depend on dashboard or API routes.
2. **Runtime ownership**: Handlers execute in the real application process with normal services and persistence.
3. **Local trust model**: Named pipes plus nonce validation are appropriate for local development.
4. **Feature extensibility**: Each feature can register its own `IMcpHandler`.
5. **Failure isolation**: IPC bind or connection failures produce unavailable results instead of preventing application startup.

## Consequences

### Positive

- The CLI never needs direct access to application databases or configuration.
- Feature handlers use the same services as the running app.
- Runtime feature availability is discoverable through `mcp.capabilities`.
- Local IPC avoids exposing a network listener for MCP.

### Negative

- The app process must be running before runtime-bound MCP tools can work.
- Transport implementations must be maintained per operating system.
- Named pipe/socket failures must be handled carefully to avoid hanging MCP calls.

### Neutral

- IPC is a local development bridge, not a remote security boundary.
- Descriptor nonces reduce accidental connections but are not a substitute for production authorization.

## Alternatives Considered

- **Direct database access from the CLI**
  - Rejected because it violates feature ownership and couples the CLI to application persistence schemas.

- **HTTP operational endpoints**
  - Rejected because MCP should not depend on dashboard/API endpoint availability or authentication setup.

- **Application-hosted MCP endpoint**
  - Rejected because the MCP protocol server is owned by `bdk mcp` over STDIO.

## Related Decisions

- [ADR-0022](0022-host-runtime-descriptor-discovery.md): Host runtime descriptor discovery
- [ADR-0023](0023-stdio-mcp-server-with-sdk-free-app-handlers.md): STDIO MCP server and handler boundary

## References

- [MCP diagnostics specification](../specs/spec-presentation-web-mcp-diagnostics.md)

## Notes

### Implementation Files

- `src/Common.Abstractions/Mcp/McpIpcRequest.cs`
- `src/Common.Abstractions/Mcp/McpIpcResponse.cs`
- `src/Presentation.Cli/Mcp/McpIpcClient.cs`
- `src/Presentation.Web/Mcp/McpIpcServer.cs`
- `src/Presentation.Web/Mcp/McpDispatcher.cs`
