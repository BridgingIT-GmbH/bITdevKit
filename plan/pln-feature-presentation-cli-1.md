---
goal: Implement the DevKit CLI foundation
version: 1.0
date_created: 2026-06-30
last_updated: 2026-06-30
owner: bITdevKit
status: 'Completed'
tags: [feature, cli, presentation, local-development]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-green)

This plan implements the `bdk` CLI foundation before the MCP command module. The first working vertical slice creates a packable .NET tool with global commands, workspace resolution, host descriptor discovery, shared host registry commands, and Console Command forwarding over the host-advertised local IPC endpoint.

## 1. Requirements & Constraints

- **REQ-001**: Implement `src/Presentation.Cli/Presentation.Cli.csproj` as a packable .NET tool with `ToolCommandName` set to `bdk` and `PackageId` set to `BridgingIT.DevKit.Cli`.
- **REQ-002**: Implement `bdk version` and `bdk help` without requiring a running application host.
- **REQ-003**: Implement `bdk docs` to open the official bITdevKit documentation from the CLI.
- **REQ-004**: Implement global options `--workspace`, `--verbose`, `--quiet`, `--no-color`, `--nologo`, `--banner`, `--non-interactive`, and `--output text|json`.
- **REQ-005**: Implement deterministic workspace resolution using explicit workspace, nearest `.slnx`/`.sln`/`.git`, then current directory fallback.
- **REQ-006**: Implement descriptor discovery from OS user-local `bdk/hosts/runtimes` locations using shared DTOs in `Common.Abstractions/HostDiscovery`.
- **REQ-007**: Implement `bdk hosts list`, `bdk hosts current`, `bdk hosts select`, `bdk hosts refresh`, `bdk hosts versions`, and `bdk hosts clean`.
- **REQ-008**: Implement `bdk host run` as a buffered single-request/single-response Console Command forwarding command over `features.consoleCommands`.
- **REQ-009**: Keep MCP-specific STDIO hosting, MCP tools, MCP dispatcher, MCP IPC client, and MCP handler behavior out of this implementation phase.
- **CON-001**: Do not add dynamic plugin loading or project-owned CLI command modules in the initial platform.
- **CON-002**: Do not make CLI commands load application configuration, rebuild application DI, or query application databases.
- **CON-003**: Preserve existing `WithModule<T>()` wording in docs and do not change unrelated specs.
- **PAT-001**: Use the existing Console Commands/Spectre.Console terminal style where practical.
- **PAT-002**: Use shared descriptor DTOs from `Common.Abstractions` instead of duplicating descriptor contracts.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Create the CLI tool foundation and global command surface.

| Task | Description | Completed | Date |
| ---- | ----------- | --------- | ---- |
| TASK-001 | Update `src/Presentation.Cli/Presentation.Cli.csproj` to use `Microsoft.NET.Sdk`, correct `AssemblyName`, reference `Common.Abstractions`, and reference `Spectre.Console`. | ✅ | 2026-06-30 |
| TASK-002 | Add `src/Presentation.Cli/Program.cs` and `src/Presentation.Cli/CliHost/CliApplication.cs` with argument parsing, exit code mapping, and global command dispatch. | ✅ | 2026-06-30 |
| TASK-003 | Add CLI contracts and settings types under `src/Presentation.Cli/CliHost`. | ✅ | 2026-06-30 |
| TASK-004 | Implement `bdk version` and `bdk help` with text and JSON output. | ✅ | 2026-06-30 |
| TASK-004A | Implement `bdk docs` to open or print the official documentation URL. | ✅ | 2026-06-30 |
| TASK-004B | Implement the animated CLI banner and global `--nologo`, `--banner`, and `--non-interactive` options. | ✅ | 2026-06-30 |

### Implementation Phase 2

- GOAL-002: Implement shared workspace and host descriptor registry commands.

| Task | Description | Completed | Date |
| ---- | ----------- | --------- | ---- |
| TASK-005 | Add `Workspace/WorkspaceResolver.cs` and `Workspace/CliWorkspaceContext.cs`. | ✅ | 2026-06-30 |
| TASK-006 | Add `HostDiscovery/HostRegistryPath.cs`, `HostDiscovery/HostRuntimeDiscovery.cs`, `HostDiscovery/HostRuntimeStatus.cs`, and host selection storage. | ✅ | 2026-06-30 |
| TASK-007 | Implement `bdk hosts list`, `current`, `select`, `refresh`, `versions`, and `clean`. | ✅ | 2026-06-30 |
| TASK-008 | Support `--all`, `--feature`, and `--yes` where specified. | ✅ | 2026-06-30 |

### Implementation Phase 3

- GOAL-003: Implement standard host Console Command forwarding.

| Task | Description | Completed | Date |
| ---- | ----------- | --------- | ---- |
| TASK-009 | Add `HostCommands/HostCommandClient.cs` with named-pipe request/response support for `features.consoleCommands`. | ✅ | 2026-06-30 |
| TASK-010 | Implement `bdk host run` dispatch to resolve a compatible host and forward command tokens after `bdk host run`. | ✅ | 2026-06-30 |
| TASK-011 | Map no host, selection required, unreachable, protocol mismatch, and command failure categories to stable exit codes. | ✅ | 2026-06-30 |

### Implementation Phase 4

- GOAL-004: Add focused tests and validation.

| Task | Description | Completed | Date |
| ---- | ----------- | --------- | ---- |
| TASK-012 | Add `tests/Presentation.UnitTests/Cli` tests for workspace resolution, descriptor discovery, host selection, command routing, JSON output, and host forwarding request serialization. | ✅ | 2026-06-30 |
| TASK-013 | Run `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --filter FullyQualifiedName~Cli`. | ✅ | 2026-06-30 |
| TASK-014 | Run `dotnet run --project src/Presentation.Cli/Presentation.Cli.csproj -- version`. | ✅ | 2026-06-30 |

## 3. Alternatives

- **ALT-001**: Use `System.CommandLine`; not chosen because the spec prefers alignment with the existing Console Commands model and no package is already present.
- **ALT-002**: Implement MCP first; not chosen because MCP depends on the base CLI command host and shared host discovery.
- **ALT-003**: Put CLI discovery services in `Presentation.Web`; not chosen because the CLI must consume descriptors without depending on the web host package.

## 4. Dependencies

- **DEP-001**: `src/Common.Abstractions/HostDiscovery` descriptor DTOs.
- **DEP-002**: `Spectre.Console` for human-readable output.
- **DEP-003**: Host-side `features.consoleCommands` endpoint metadata produced by `DevKitWebApplication`.
- **DEP-004**: Existing `Presentation.UnitTests` test project for focused tests.

## 5. Files

- **FILE-001**: `src/Presentation.Cli/Presentation.Cli.csproj` for package/tool setup.
- **FILE-002**: `src/Presentation.Cli/Program.cs` for process entry.
- **FILE-003**: `src/Presentation.Cli/CliHost/**` for command host, parsing, settings, and output.
- **FILE-004**: `src/Presentation.Cli/Workspace/**` for workspace resolution.
- **FILE-005**: `src/Presentation.Cli/HostDiscovery/**` for descriptor registry discovery and selection.
- **FILE-006**: `src/Presentation.Cli/HostCommands/**` for Console Command forwarding.
- **FILE-007**: `tests/Presentation.UnitTests/Cli/**` for focused CLI tests.
- **FILE-008**: `docs/specs/spec-presentation-cli.md` status remains `draft` until the initial CLI acceptance criteria pass.

## 6. Testing

- **TEST-001**: `bdk version` returns success without hosts.
- **TEST-002**: `bdk help` returns success without hosts.
- **TEST-003**: workspace resolver honors explicit path, solution ancestors, git ancestors, and current directory fallback.
- **TEST-004**: host discovery reads descriptors, filters by workspace, validates schema and live process status, and reports invalid descriptors.
- **TEST-005**: host selection writes and reads the workspace-scoped selection file.
- **TEST-006**: host forwarding preserves command tokens and sends a nonce-protected `run` request to the host endpoint.

## 7. Risks & Assumptions

- **RISK-001**: Host-side IPC response currently exposes a minimal `Ok`, `Output`, and `Error` shape; the CLI must tolerate this while the richer protocol evolves.
- **RISK-002**: Unix socket support is specified but host-side endpoint metadata currently advertises named pipes only; first CLI forwarding implementation supports named pipes and reports unsupported transports clearly.
- **ASSUMPTION-001**: `src/Presentation.Cli` is the intended CLI package because it already exists in the solution.
- **ASSUMPTION-002**: The first MCP implementation will reuse shared host discovery services from this CLI package.

## 8. Related Specifications / Further Reading

- [DevKit CLI](../docs/specs/spec-presentation-cli.md)
- [DevKit Web Application Host](../docs/specs/spec-presentation-devkit-web-host.md)
- [DevKit STDIO MCP](../docs/specs/spec-presentation-web-mcp-diagnostics.md)
- [Presentation Console Commands](../docs/features-presentation-console-commands.md)
