# ADR-0021: BDK CLI for Local Development Tooling

## Status

Accepted

## Date

2026-07-01

## Context

DevKit applications need a consistent local tooling entry point for developers and coding agents. Before the `bdk` CLI, operational workflows were spread across terminal console commands, web dashboards, application endpoints, and ad hoc local scripts.

The CLI must support:

1. Discovering running local DevKit web hosts
2. Selecting a workspace-scoped runtime
3. Forwarding host-owned console commands
4. Hosting future command modules such as MCP
5. Running from a .NET local tool manifest in consuming projects

The CLI must not become a shadow application runtime. It should not rebuild application dependency injection containers, read application configuration, query application databases, or duplicate feature-specific business logic.

## Decision

Create a dedicated .NET tool named `bdk` in `src/Presentation.Cli`.

The CLI is organized as command modules:

- `core`: help and version
- `docs`: DevKit documentation commands
- `hosts`: shared host discovery and selection
- `host`: console command forwarding into a selected runtime
- `mcp`: STDIO MCP server and runtime diagnostics tools

The CLI is a local development adapter. Runtime data and feature behavior remain owned by the running application process.

## Rationale

1. **Single developer entry point**: Developers and agents can use one command surface for DevKit local workflows.
2. **Host ownership**: The running application remains the source of truth for runtime data and operations.
3. **Module growth**: CLI features can grow independently without overloading application startup code.
4. **Tool manifest compatibility**: Consuming projects can install `bdk` as a repo-local .NET tool.
5. **Clean architecture alignment**: The CLI lives in Presentation and does not pull infrastructure/application internals into the command process.

## Consequences

### Positive

- Provides one consistent command surface for local DevKit workflows.
- Keeps runtime feature logic inside the application process.
- Supports both human CLI usage and coding-agent MCP usage.
- Allows new command modules without changing application feature packages.

### Negative

- Adds another executable to build, package, document, and test.
- Requires careful stdout behavior for protocol-oriented commands such as `bdk mcp`.
- Some workflows require a running local DevKit host before the CLI can provide useful runtime data.

### Neutral

- The CLI uses existing Console Commands infrastructure for ordinary CLI commands.
- Protocol-specific command modules can bypass the normal console renderer when needed.

## Alternatives Considered

- **Use only application-hosted dashboards**
  - Rejected because dashboards are human-facing and do not provide a stable CLI or MCP protocol surface.

- **Use only in-process terminal console commands**
  - Rejected because agents and external tools need process-independent access through `bdk`.

- **Let each project create its own scripts**
  - Rejected because it fragments local workflows and prevents a consistent DevKit tool experience.

## Related Decisions

- [ADR-0022](0022-host-runtime-descriptor-discovery.md): Host runtime descriptor discovery
- [ADR-0023](0023-stdio-mcp-server-with-sdk-free-app-handlers.md): STDIO MCP server and app handler boundary
- [ADR-0024](0024-local-ipc-runtime-bridge.md): Local IPC bridge into running runtimes

## References

- [CLI specification](../specs/spec-presentation-cli.md)
- [MCP diagnostics specification](../specs/spec-presentation-web-mcp-diagnostics.md)

## Notes

### Implementation Files

- `src/Presentation.Cli/Program.cs`
- `src/Presentation.Cli/CliHost/CliApplication.cs`
- `src/Presentation.Cli/CliHost/CliCommandModules.cs`
- `src/Presentation.Cli/Commands/*`
