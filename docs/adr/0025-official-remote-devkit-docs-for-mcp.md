# ADR-0025: Official Remote DevKit Documentation for MCP

## Status

Accepted

## Date

2026-07-01

## Context

The MCP specification requires documentation tools that help coding agents answer DevKit usage questions. The `bdk` CLI is intended to be used from consuming application repositories, not only from the DevKit source repository.

Therefore, documentation lookup must not assume that the current workspace contains DevKit's `docs/` directory.

The documentation tools must:

1. Work from any consuming project workspace
2. Prefer official DevKit documentation sources
3. Return bounded content and source links
4. Avoid requiring a selected runtime
5. Be testable without network access

## Decision

`bdk_docs_search` and `bdk_docs_get` use an `IMcpDocumentationSource` abstraction. The default implementation reads official DevKit documentation from the `bridgingit/bitdevkit` GitHub repository.

Unit tests use an in-memory documentation source so tests remain deterministic and do not depend on network availability.

## Rationale

1. **Works in consuming projects**: Developers can use docs tools from any repository that installs `bdk`.
2. **Official source preference**: Results link to the canonical DevKit documentation repository.
3. **Runtime independence**: Documentation tools do not require a running local application host.
4. **Bounded responses**: Search and get operations enforce limits before returning content to agents.
5. **Testability**: Source abstraction separates lookup behavior from network I/O.

## Consequences

### Positive

- Documentation MCP tools work outside the DevKit source repository.
- Agents receive source URLs they can cite or inspect.
- Tests remain fast and deterministic through a fake source.
- The source abstraction allows future documentation backends.

### Negative

- Default docs lookup requires network access at runtime.
- GitHub availability and API rate limits can affect documentation tools.
- A small cache is needed to avoid repeated remote traversal.

### Neutral

- Runtime diagnostic tools still use local host discovery and IPC.
- Documentation tools are intentionally separate from runtime tools.

## Alternatives Considered

- **Read local workspace docs**
  - Rejected because consuming application repositories do not contain DevKit source documentation.

- **Embed documentation in the CLI package**
  - Rejected initially because embedded docs can become stale unless packaging explicitly updates them.

- **Require a selected runtime to provide docs**
  - Rejected because documentation lookup should work without a running application process.

## Related Decisions

- [ADR-0021](0021-bdk-cli-local-development-tooling.md): BDK CLI local development tooling
- [ADR-0023](0023-stdio-mcp-server-with-sdk-free-app-handlers.md): STDIO MCP server

## References

- [MCP diagnostics specification](../specs/spec-presentation-web-mcp-diagnostics.md)
- [DevKit documentation repository](https://github.com/bridgingit/bitdevkit/tree/main/docs)

## Notes

### Implementation Files

- `src/Presentation.Cli/Mcp/McpDocumentationTools.cs`
- `tests/Presentation.UnitTests/Cli/CliFoundationTests.cs`
