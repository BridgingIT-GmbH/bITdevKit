# ADR-0022: Host Runtime Descriptor Discovery

## Status

Accepted

## Date

2026-07-01

## Context

The `bdk` CLI and MCP server need to find running local DevKit application processes without assuming a fixed port, HTTP endpoint, container setup, or application-specific configuration.

The discovery mechanism must:

1. Work across projects that consume DevKit
2. Filter runtimes by workspace
3. Support multiple running applications
4. Advertise optional local features such as console commands and MCP
5. Avoid repository-local generated state
6. Avoid HTTP endpoint dependency for CLI/MCP operation

## Decision

Running DevKit web hosts write user-local JSON runtime descriptors under the OS-local `bdk/hosts/runtimes` registry. The CLI reads those descriptors, validates required fields, filters by workspace, and uses workspace-scoped selection files under `bdk/hosts/selections`.

Feature endpoint metadata is advertised under descriptor `features`, for example:

- `consoleCommands`
- `mcp`

Each feature endpoint includes protocol version, local transport, endpoint address, and nonce.

## Rationale

1. **No port coupling**: Discovery does not depend on Kestrel URLs or dashboard routes.
2. **Workspace awareness**: Multiple projects can run at the same time without accidental cross-selection.
3. **Local-only state**: Descriptor files belong in user-local OS storage, not the repository.
4. **Feature extensibility**: New local host capabilities can advertise endpoint metadata without changing discovery contracts.
5. **Agent usability**: `bdk hosts list`, `bdk hosts select`, and MCP runtime tools share the same discovery model.

## Consequences

### Positive

- CLI and MCP commands can discover local runtimes without application HTTP calls.
- Descriptor validation catches stale, invalid, and feature-incomplete hosts.
- Host selection is stable per workspace.
- Feature endpoints can evolve independently through protocol versions.

### Negative

- The host must participate by writing descriptors during local development.
- Stale descriptors can exist after abnormal process termination and must be cleaned.
- Descriptor schema compatibility must be maintained.

### Neutral

- Descriptors contain local endpoint nonces but no application secrets.
- `bdk hosts clean` is responsible for removing invalid or stale descriptors.

## Alternatives Considered

- **Scan running processes**
  - Rejected because process names and command lines are not reliable enough and do not expose feature endpoint metadata.

- **Use HTTP discovery endpoints**
  - Rejected because MCP and CLI forwarding must not depend on HTTP operational endpoints.

- **Use repository-local descriptor files**
  - Rejected because runtime state should not dirty consuming repositories or require `.gitignore` changes.

## Related Decisions

- [ADR-0021](0021-bdk-cli-local-development-tooling.md): BDK CLI local development tooling
- [ADR-0024](0024-local-ipc-runtime-bridge.md): Local IPC runtime bridge

## References

- [DevKit web host specification](../specs/spec-presentation-devkit-web-host.md)
- [CLI specification](../specs/spec-presentation-cli.md)

## Notes

### Implementation Files

- `src/Common.Abstractions/HostDiscovery/*`
- `src/Presentation.Web/HostDiscovery/*`
- `src/Presentation.Cli/HostDiscovery/*`
