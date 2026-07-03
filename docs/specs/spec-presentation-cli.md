---
status: implemented
---

# Design Specification: DevKit CLI

> This design document specifies `bdk`, the DevKit command-line interface. The CLI is the extensible local-development command surface for DevKit workflows. Command modules such as MCP, code generation, scaffolding, diagnostics export, and local maintenance commands plug into this CLI instead of creating separate command hosts.

[TOC]

## Introduction

DevKit already has reusable local-development features that are useful from a terminal: diagnostics, console commands, project setup helpers, feature scaffolding, and coding-agent integration. The `bdk` CLI provides one discoverable entry point for those workflows.

DevKit applications can also host Console Commands inside their own webapp process. Those commands include built-in DevKit commands and project-specific commands registered by the application. The CLI must provide a standard forwarding feature so a developer can invoke those host commands from `bdk`, pass through all command arguments and options, and see the host command output rendered back in the CLI.

The CLI is distributed as a repository-local .NET tool:

```xml
<PropertyGroup>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>bdk</ToolCommandName>
  <PackageId>BridgingIT.DevKit.Cli</PackageId>
</PropertyGroup>
```

The CLI foundation depends on the DevKit web host foundation specified in [spec-presentation-web-devkit-host.md](spec-presentation-web-devkit-host.md). That host foundation must be implemented first because running web hosts write the descriptors and expose the local IPC endpoint metadata consumed by `bdk`. The MCP command module is specified separately in [spec-presentation-web-mcp-diagnostics.md](spec-presentation-web-mcp-diagnostics.md).

## Positioning

The DevKit CLI is:

* a repository-local .NET tool named `bdk`
* a shared command host for DevKit local-development workflows
* an extensible command-module surface for commands such as `bdk host`, `bdk mcp`, `bdk generate`, and `bdk scaffold`
* a terminal-oriented command environment aligned with the existing [Console Commands feature](../features-presentation-console-commands.md)
* a standard way to forward Console Commands into a selected running webapp host
* a place for developer workflows that should be scriptable, discoverable, and testable

The DevKit CLI is not:

* an MCP-only binary
* an application runtime host
* a replacement for ASP.NET Core application startup
* a production remote administration surface
* a general-purpose OS shell
* a parallel application CRUD API

The core boundary is:

```text
bdk owns CLI command routing and command-module composition.
Console Commands provide the preferred terminal command model.
Running hosts advertise themselves through OS-local runtime descriptors.
Command modules own their domain behavior.
MCP is one command module inside bdk.
Host command forwarding is one command module inside bdk.
Commands that do not need a running app do not require runtime selection.
```

## Goals

## Extensible command host

The CLI shall support independent command modules.

Examples:

```text
bdk hosts list
bdk hosts select <runtimeId>
bdk host run <command> [arguments...]
bdk docs
bdk mcp
bdk generate ...
bdk scaffold ...
bdk version
```

Command modules must be able to register command groups without depending on unrelated command modules.

Command modules are registered explicitly by the CLI package. The initial platform does not support automatic module discovery, dynamic plugin loading, or project-owned CLI command modules. All command modules, including MCP, are implemented in the `Presentation.Cli` package and wired into the CLI host at startup.

Minimal boundary contracts:

```csharp
public interface ICliCommandModule
{
  string Name { get; }
  string Description { get; }

  void RegisterServices(IServiceCollection services, CliCommandModuleContext context);

  void RegisterCommands(ICliCommandRegistry registry);
}

public interface ICliCommandRegistry
{
  void WithCommand<TCommand>() where TCommand : class;

  void WithCommand(Type commandType);

  void AddGroup(string name, string description, Action<ICliCommandRegistry> configure);
}

public sealed class CliCommandModuleContext
{
  public required CliWorkspaceContext Workspace { get; init; }
  public required CliOutputSettings Output { get; init; }
  public required HostRegistryOptions HostRegistry { get; init; }
  public required IDictionary<string, string?> Environment { get; init; }
}

public sealed class CliInvocationContext
{
  public required string[] Arguments { get; init; }
  public required CliWorkspaceContext Workspace { get; init; }
  public required CliOutputSettings Output { get; init; }
  public required CancellationToken CancellationToken { get; init; }
}

public sealed class CliOutputSettings
{
  public required CliOutputFormat Format { get; init; }
  public required bool Quiet { get; init; }
  public required bool Verbose { get; init; }
  public required bool NoColor { get; init; }
  public required bool IsCi { get; init; }
}

public enum CliOutputFormat
{
  Text,
  Json
}
```

The exact implementation may evolve, but the platform contract must preserve these responsibilities and boundary rules:

* modules register explicitly in CLI startup code
* modules register their own services and commands
* modules do not scan application projects for CLI extensions
* modules do not depend on unrelated modules
* shared services are registered by the CLI foundation before modules are registered
* `bdk version` and `bdk help` can list registered modules from the same registry
* command registration records enough metadata for help and version output
* invocation context is created by the CLI adapter and passed to command execution infrastructure
* output settings are resolved once per invocation and shared by commands

`CliCommandModuleContext` should expose only shared platform services and settings needed during registration, such as workspace context, output settings, host registry options, and environment information. It must not expose MCP-specific services, host forwarding clients, application service providers, DbContexts, or project runtime services.

`ICliCommandRegistry` is a registration boundary, not a discovery mechanism. It records commands supplied by registered modules and may adapt those commands into the Console Commands execution model. It must not scan arbitrary assemblies or application projects.

## Console Commands alignment

Human-invoked terminal commands should reuse or align with the [Console Commands feature](../features-presentation-console-commands.md).

The Console Commands feature already provides:

* lightweight command classes through `ConsoleCommandBase`
* command contracts through `IConsoleCommand`
* grouped subcommands through `IGroupedConsoleCommand`
* attribute-driven options and arguments
* validation and generated help through `ConsoleCommandBinder`
* Spectre.Console output for tables, markup, rules, and panels
* non-interactive single-shot invocation patterns

The CLI should avoid introducing a second command definition and binding model when the existing Console Commands model can provide the behavior.

Local human-invoked `bdk` commands should be implemented as Console Commands-compatible command classes unless process-level behavior requires a CLI-native wrapper. Compatible means command name, aliases, description, option binding, argument binding, grouped subcommands, validation errors, help rendering, and Spectre.Console output follow the semantics of the Console Commands feature.

The adapter may provide CLI-native wrappers for concerns that are not normal in-process Console Commands responsibilities, such as global option pre-parsing, exit code mapping, JSON output envelopes, host selection, and `bdk host run` forwarding boundaries. These wrappers should delegate command-specific parsing and validation to Console Commands-compatible metadata where practical.

## CLI parsing and routing

`bdk` uses a thin CLI adapter over the Console Commands command model where practical.

The adapter owns process-level concerns that are outside normal in-process Console Commands execution:

* command module registration
* root command and command group routing
* global options
* exit code mapping
* structured output selection
* quiet and no-color behavior
* workspace resolution
* host selection options
* forwarding boundaries such as `bdk host run -- <host command tokens>`

The Console Commands model remains the preferred command definition and binding model for human-invoked commands. The adapter may wrap or extend it where CLI process behavior requires different handling, but command help, validation, option binding, grouped subcommands, and Spectre.Console rendering should remain compatible.

Global options:

```text
--help
--version
--workspace <path>
--verbose
--quiet
--no-color
--nologo
--banner
--non-interactive
--output text|json
```

Global options must be parsed before command execution. Command modules may add module-specific options, but they should not redefine the semantics of shared global options.

Global option compatibility:

| Option | Applies to | Behavior |
| ------ | ---------- | -------- |
| `--help` | all commands | Shows help for the selected command or command group and exits successfully. |
| `--version` | root command | Shows CLI version information and exits successfully. |
| `--workspace <path>` | all workspace-aware commands | Overrides workspace resolution and therefore host filtering, selected-host storage, and future workspace-aware command modules. |
| `--verbose` | human-readable output | Adds diagnostic detail and may show startup host summaries for global commands. It must not add non-JSON text to JSON output. |
| `--quiet` | human-readable output | Suppresses non-essential text, startup summaries, and prompts where possible. It must not suppress required errors. |
| `--no-color` | human-readable output | Disables ANSI color and Spectre.Console color styling regardless of terminal detection. |
| `--nologo` | human-readable output | Suppresses the startup banner. |
| `--banner` | human-readable output | Forces the startup banner when it would normally be suppressed. Banner output is written to standard error so command stdout remains usable. |
| `--non-interactive` | all commands | Disables interactive behavior, prompts and default banner animation. Commands that require interaction must fail with a clear error instead of prompting. |
| `--output text` | commands with output | Uses human-readable Spectre.Console output. This is the default. |
| `--output json` | commands with structured output | Emits JSON only, suppresses color and startup summaries, and uses the structured output contract. |

`--quiet` and `--verbose` must not both be accepted for the same invocation. If both are supplied, the CLI returns `invalid_arguments` with exit code `2`.

`--nologo` and `--banner` must not both be accepted for the same invocation. If both are supplied, the CLI returns `invalid_arguments` with exit code `2`.

`--output json` implies no color and suppresses human-only startup summaries. It does not imply quiet error handling; structured errors are still emitted.

CI mode is detected through common environment variables such as `CI=true`. CI mode suppresses startup summaries, banner animation and interactive prompts by default, but it does not force JSON output.

Stable exit code categories:

| Exit code | Category | Meaning |
| --------: | -------- | ------- |
| `0` | Success | The command completed successfully. |
| `1` | CommandFailed | The command ran but failed. |
| `2` | InvalidArguments | Command tokens or options failed parsing or validation. |
| `3` | HostNotFound | A host-aware command could not find a compatible running host. |
| `4` | HostSelectionRequired | Multiple compatible hosts are available and no selection was provided. |
| `5` | SelectedHostUnavailable | The selected host is stale, unreachable, or no longer compatible. |
| `6` | ProtocolVersionMismatch | CLI and host protocol versions are incompatible. |
| `7` | InternalError | An unexpected CLI error occurred. |

Human-readable errors should include a concise summary and one actionable hint where possible. Structured errors must include a stable `code`, `summary`, and `exitCode`.

## Workspace resolution

Workspace resolution must be deterministic because host filtering and selected-host storage are scoped by workspace hash.

Resolution order:

```text
1. Explicit --workspace path.
2. Nearest .slnx, .sln, or .git ancestor from the current directory.
3. Current directory fallback.
```

The resolved workspace path is normalized before hashing:

* use the full absolute path
* normalize directory separators
* trim trailing separators
* compare case-insensitively on Windows
* resolve symbolic links where practical without failing if resolution is unavailable

The workspace hash must be stable across shells for the same logical workspace. Commands that do not need workspace context may still resolve it for display and configuration, but they must not fail solely because a solution file is absent.

## Shared host discovery and selection

The CLI shall know about running DevKit webapp hosts through shared host runtime descriptors. The MCP specification uses the same descriptor registry and contributes MCP-specific endpoint metadata to it.

Host discovery is a shared CLI capability for command modules that need a running application process. The MCP command module uses it to connect MCP tools to a selected runtime. The Host Commands module uses it to forward Console Commands to a selected runtime. Future command modules may also use it when they need a running host.

Each running local host writes one descriptor file to an OS user-local location. The descriptor registry is outside the workspace so normal runtime state does not create repository files.

Default registry locations:

```text
Windows:     %LOCALAPPDATA%\bdk\hosts\runtimes
Linux/macOS: $XDG_RUNTIME_DIR/bdk/hosts/runtimes
Fallback:    $TMPDIR/bdk/hosts/runtimes
```

The selected host should be stored in the same OS user-local registry, scoped by workspace hash:

```text
Windows:     %LOCALAPPDATA%\bdk\hosts\selections\<workspace-hash>.json
Linux/macOS: $XDG_RUNTIME_DIR/bdk/hosts/selections/<workspace-hash>.json
Fallback:    $TMPDIR/bdk/hosts/selections/<workspace-hash>.json
```

Descriptors include `workspacePath`, `contentRootPath`, and process metadata so the CLI can filter hosts for the current workspace. They may also include feature endpoint metadata for modules that can connect to the host.

In host descriptors, `features` means host-advertised endpoint capabilities. It does not mean CLI command modules.

Shared descriptor DTOs live in `Common.Abstractions` so the CLI and `Presentation.Web` serialize the same schema without duplicating contracts:

```text
src/Common.Abstractions/HostDiscovery
  HostRuntimeDescriptor
  HostRuntimeAssemblyDescriptor
  HostFeatureEndpointMetadata
  HostFeatureEndpointCollection
  HostRuntimeDescriptorSchema
```

These DTOs are schema contracts and must not depend on `Presentation.Cli`, `Presentation.Web`, MCP SDK types, Console Commands implementation types, ASP.NET Core hosting types, or application services.

Example descriptor:

```json
{
  "schemaVersion": 1,
  "runtimeId": "commerce-api-5001",
  "applicationName": "CommerceApi",
  "environmentName": "Development",
  "workspacePath": "F:/projects/bit/bITdevKit",
  "contentRootPath": "F:/projects/acme/commerce/src/CommerceApi.Presentation.Web.Server",
  "projectPath": "F:/projects/acme/commerce/src/CommerceApi.Presentation.Web.Server/CommerceApi.Presentation.Web.Server.csproj",
  "processId": 23844,
  "startedAt": "2026-06-19T14:20:00Z",
  "assembly": {
    "name": "CommerceApi.Presentation.Web.Server",
    "version": "1.0.0.0",
    "informationalVersion": "1.0.0+abc1234",
    "fileVersion": "1.0.0.0"
  },
  "features": {
    "consoleCommands": {
      "protocolVersion": 1,
      "transport": "named-pipe",
      "endpoint": "bdk-commerce-api-5001-console",
      "nonce": "local-random-token"
    },
    "mcp": {
      "protocolVersion": 1,
      "transport": "named-pipe",
      "endpoint": "bdk-commerce-api-5001-mcp",
      "nonce": "local-random-token"
    }
  }
}
```

Required shared descriptor fields:

| Field | Required | Description |
| ----- | -------: | ----------- |
| `schemaVersion` | yes | Host descriptor schema version. |
| `runtimeId` | yes | Stable id for the current host instance. |
| `applicationName` | yes | Display name of the running application. |
| `environmentName` | yes | ASP.NET Core environment name. |
| `workspacePath` | yes | Solution or workspace root. |
| `contentRootPath` | yes | Application content root. |
| `projectPath` | no | Host project path when known. |
| `processId` | yes | Local process id. |
| `startedAt` | yes | UTC start timestamp. |
| `assembly.name` | yes | Entry assembly name for the running host. |
| `assembly.version` | yes | Entry assembly version for the running host. |
| `assembly.informationalVersion` | no | Informational or product version when available. |
| `assembly.fileVersion` | no | File version when available. |
| `features` | no | Feature endpoint metadata keyed by feature name. |

Feature endpoint metadata is required only for command modules that need to connect to the host. For example, `bdk host run` requires `features.consoleCommands`, while `bdk mcp` requires MCP endpoint metadata as specified by the MCP command module.

A host may write a valid descriptor with no `features` object or with an empty `features` object. Such a descriptor is still useful for host inventory and `bdk hosts versions`, but commands that require a feature endpoint must report `FeatureUnavailable` or the corresponding command-specific unavailable result.

Descriptor schema compatibility rules:

* unknown JSON fields are ignored by readers
* missing required fields mark the descriptor `Invalid`
* unsupported `schemaVersion` values mark the descriptor `VersionMismatch`
* `runtimeId` is stable for the current host process lifetime, not across all future restarts
* descriptor filenames should include runtime id and process id to avoid restart collisions
* `features.*.transport` values are feature-owned but must be documented by the feature that contributes them

Host discovery order:

```text
1. Explicit --host or --runtime-id argument.
2. OS user-local bdk/hosts/runtimes descriptors matching the current workspace.
3. OS user-local bdk/hosts/runtimes descriptors when --all is supplied.
4. No host found.
```

Host validation:

```text
1. Parse descriptor JSON.
2. Check schemaVersion.
3. Check process id when possible.
4. Check requested feature endpoint metadata when a command needs it.
5. Try the command-specific local connection when validation requires it.
6. Send the feature nonce handshake when a feature endpoint has a nonce.
7. Mark host as Ready, Stale, Unreachable, Invalid, VersionMismatch, or FeatureUnavailable.
```

## Host descriptor writer dependency

The CLI reads, validates, selects, and cleans host descriptors. It does not write descriptors.

Running DevKit web applications write descriptors through the host foundation specified in [spec-presentation-web-devkit-host.md](spec-presentation-web-devkit-host.md). That specification owns `DevKitWebApplication.CreateBuilder(args)`, local tooling eligibility, descriptor writer registration, descriptor lifecycle at host start, and host-advertised endpoint capability registration.

The CLI depends on the following host-side guarantees:

* descriptors are written only by eligible local-development DevKit web hosts
* a descriptor can exist without callable endpoint capabilities
* enabled host capabilities contribute endpoint metadata under `features.consoleCommands` and `features.mcp`
* `features` means host-advertised endpoint capabilities, not CLI command modules
* descriptor writing failures do not prevent the application from starting
* stale descriptors are expected after crashes and are handled by CLI validation and cleanup

## Descriptor lifecycle and cleanup

Descriptor lifecycle rules:

* write the descriptor after the host has enough metadata to describe itself and after endpoint contributors have produced their local endpoint metadata
* write descriptor files atomically by writing to a temporary file and replacing the final descriptor path
* use a filename derived from runtime id and process id to reduce collisions between restarts
* refresh heartbeat metadata only when useful for stale detection; refreshes must not rotate endpoint nonces
* remove only the current host's descriptor on graceful shutdown
* treat missing cleanup on crash as normal behavior
* never delete descriptors owned by live processes
* make `bdk hosts clean` responsible for user-initiated cleanup of stale descriptors

Stale detection should use process id checks where available. When process id checks are unavailable or ambiguous, the CLI should validate the advertised endpoint before marking a host `Ready`. If the process id exists but endpoint validation fails, the host should be marked `Unreachable` or `FeatureUnavailable` depending on whether the descriptor itself is valid and whether the requested feature endpoint exists.

Descriptor cleanup rules:

* descriptors with invalid JSON may be removed by `bdk hosts clean` after confirmation
* descriptors whose process no longer exists may be removed by `bdk hosts clean` after confirmation
* endpoint socket files or named-pipe metadata may be removed only when safe and owned by stale descriptors
* cleanup should support `--yes` for non-interactive use
* cleanup must report what was removed and what was skipped

## Local trust model

Host discovery and forwarding are local-development features and use an OS user-local trust boundary.

Rules:

* descriptors are stored in user-local runtime locations, not in the repository
* descriptor presence is not sufficient authorization to execute a host command
* local IPC endpoints must require a nonce handshake when a nonce is advertised
* endpoint nonces are generated randomly per process start or endpoint rebind
* endpoint nonces remain stable for the lifetime of that endpoint
* descriptor refresh writes must not rotate active endpoint nonces
* the CLI refuses incompatible protocol versions before sending command payloads
* non-development hosts must not advertise CLI-connectable endpoints by default
* host command forwarding must be enabled explicitly by the application host
* destructive host commands remain responsible for their own confirmation and environment safeguards

The nonce protects against accidental cross-process use of stale or spoofed descriptors in the same user-local registry. It is not a production authentication mechanism and must not be presented as one.

Host selection behavior:

* if exactly one compatible host is ready, a command may auto-select it
* if multiple compatible hosts are ready and no host is selected, commands return `host_selection_required`
* if the selected host is no longer valid, commands return `selected_host_unavailable`
* commands must not aggregate across hosts by default
* commands that do not need a running host must not perform host discovery

## Startup host summary

For human-readable CLI output, `bdk` should show a concise host summary when host context is relevant.

Startup behavior:

* host-aware commands show the startup host summary by default
* global commands show the startup host summary only with `--verbose` or when directly relevant
* machine-readable output, quiet output, and CI mode suppress the startup host summary
* when shown, discover ready workspace hosts before executing the requested command
* when one or more hosts are found, list them compactly with runtime id, app name, advertised features, and status
* when no hosts are found for a host-aware command, show a friendly warning that no running DevKit hosts were discovered
* do not fail global commands such as `bdk version`, `bdk help`, or `bdk hosts versions` solely because no hosts are running
* commands that require a host still return their command-specific unavailable result, such as `no_host_found`

Example startup output when hosts are available:

```text
Hosts
  commerce-api-5001   CommerceApi    Ready  consoleCommands,mcp
  billing-api-5001    BillingApi     Ready  consoleCommands
```

Example startup output when no hosts are available:

```text
No running DevKit hosts were found for this workspace.
Start a local DevKit webapp to enable host commands, MCP runtime tools, and runtime diagnostics.
```

The CLI provides shared host registry commands so users can inspect and select hosts without using an MCP-specific command group.

Shared host registry command surface:

```text
bdk hosts list
bdk hosts current
bdk hosts select <runtimeId>
bdk hosts refresh
bdk hosts versions
bdk hosts clean
```

The MCP specification may keep MCP-specific protocol details, but the general idea of discovering local webapp hosts through descriptors belongs to the CLI foundation so non-MCP modules can use it too.

## Host Console Command forwarding

The CLI shall provide a standard host command forwarding command module.

The forwarding module lets a developer invoke Console Commands that are registered in a running DevKit webapp host process. The command implementation executes inside the selected host process through the host's DI container. The CLI forwards the command name and all parameters, receives the command output, and displays that output as the `bdk` response.

Example command shape:

```bash
bdk host run status
bdk host run diag perf
bdk host run seed data --count=50
bdk host run --host commerce-api-5001 -- jobs trigger --name=reindex
```

The exact command naming can evolve, but the command module must provide one explicit host-forwarding command group. Forwarded command tokens after the forwarding command are treated as host command tokens and must be preserved, including positional arguments, options, flags, quoted values, and grouped subcommands. When host selector options are supplied, `--` should separate CLI forwarding options from host command tokens.

The host forwarding IPC v1 contract is buffered, single-request/single-response, and non-interactive.

IPC v1 rules:

* the CLI sends one request payload for one host command invocation
* the host returns one response payload containing output, exit code, and optional error metadata
* interactive stdin is not supported
* output is buffered by the host and returned as ANSI or plain terminal text
* cancellation and timeout signals are propagated where practical
* concurrent requests may be rejected by the host if the host command executor is single-flight
* request and response payloads include protocol version metadata
* protocol version mismatch maps to exit code `6`
* default request timeout is 60 seconds unless the command module documents a different timeout
* default maximum buffered output is 1 MiB unless the command module documents a different limit
* output that exceeds the limit is truncated and marked as truncated in the response
* host command stdin is always empty in v1

IPC v1 error categories:

| Code | CLI exit code | Meaning |
| ---- | ------------: | ------- |
| `invalid_arguments` | `2` | CLI forwarding options or host command arguments failed validation before execution. |
| `no_host_found` | `3` | No compatible host with `features.consoleCommands` is available. |
| `host_selection_required` | `4` | Multiple compatible hosts exist and no target host was selected. |
| `selected_host_unavailable` | `5` | The selected host is stale, unreachable, or no longer compatible. |
| `version_mismatch` | `6` | CLI and host forwarding protocol versions are incompatible. |
| `feature_unavailable` | `3` | The selected host does not advertise `features.consoleCommands`. |
| `host_command_not_found` | `1` | The host does not have a matching Console Command. |
| `host_command_validation_failed` | `2` | The host Console Command binder rejected the forwarded tokens. |
| `host_command_failed` | `1` | The command executed but returned a failure or threw an application exception. |
| `timeout` | `1` | The host command did not complete before the request timeout. |
| `output_truncated` | `1` | The host response exceeded the output limit and the command could not be represented completely. |
| `host_busy` | `1` | The host rejected the request because another command is already running. |
| `internal_error` | `7` | The CLI failed unexpectedly while preparing, sending, or rendering the request. |

Validation failures that occur before a host command starts should use `invalid_arguments` or `host_command_validation_failed`. Exceptions thrown while executing a host command must be sanitized to `host_command_failed` unless the host command deliberately returns a more specific failure code.

The host should return terminal output in one buffered `output` field for v1. Separate stdout and stderr streams are out of scope. A host command that wants machine-readable command-specific data may write JSON as its terminal output, but the forwarding protocol itself does not interpret that command-specific JSON.

Forwarded host commands are different from local `bdk` commands:

```text
bdk local command
  -> executes in the CLI process

bdk host run <host command>
  -> selects a running host
  -> forwards command tokens to that host
  -> executes inside the host process
  -> streams or returns host command output to the CLI
```

Host command output should preserve the Console Commands rendering model. Spectre.Console output from the host should be returned in a terminal-safe form, preferably ANSI text, so the CLI can render tables, markup output, rules, and panels without reimplementing host command presentation.

When no compatible host is available, the forwarding command must return a clear message explaining that no running DevKit host was found and that the application must be started with host command forwarding enabled.

When multiple compatible hosts are available, the forwarding command must not guess. It must either:

* accept an explicit host selector argument such as `--host <runtimeId>` or `--runtime-id <runtimeId>`
* use a previously selected host when a shared selection exists
* return a host selection required message listing the available hosts and showing how to rerun the command with a selector

Host discovery and selection use the shared host descriptor registry defined in this specification. MCP protocol tools and host Console Command forwarding remain separate command modules.

## Command module isolation

Each command module owns its dependencies and domain behavior.

Rules:

* host runtime discovery and selection may be shared by modules that need a running host.
* MCP-specific IPC clients are used only by MCP commands.
* host Console Command forwarding clients are used only by host forwarding commands.
* Code generation commands do not require MCP runtime selection.
* Scaffolding commands do not require an application process to be running.
* Shared CLI services remain protocol-neutral and MCP-neutral.
* Command modules define their own parsing, validation, output, and failure tests.

## Repository-local tool installation

The preferred setup is a repository-local .NET tool manifest.

In the solution root:

```bash
dotnet new tool-manifest
dotnet tool install BridgingIT.DevKit.Cli
```

The generated file is committed:

```text
.config/dotnet-tools.json
```

Developers restore tools with:

```bash
dotnet tool restore
```

## Local CLI development invocation

During CLI development, contributors must be able to run the CLI directly from the repository without first packing or installing the tool.

The development invocation should use the CLI project directly:

```bash
dotnet run --project src/Presentation.Cli/Presentation.Cli.csproj -- <bdk arguments>
```

Examples:

```bash
dotnet run --project src/Presentation.Cli/Presentation.Cli.csproj -- version
dotnet run --project src/Presentation.Cli/Presentation.Cli.csproj -- hosts list
dotnet run --project src/Presentation.Cli/Presentation.Cli.csproj -- hosts versions
dotnet run --project src/Presentation.Cli/Presentation.Cli.csproj -- host run status
```

This invocation path must exercise the same command host, command modules, parsing, validation, output formatting, host discovery, and failure behavior as the packaged `bdk` tool. The only difference should be process launch mechanics.

The repository-local tool path remains the preferred consumer setup:

```bash
dotnet tool run bdk version
```

Local `dotnet run --project ... --` execution is for development, debugging, and tests before the tool package is installed.

## Package and Project Placement

Suggested package:

```text
src/Common.Abstractions/HostDiscovery
  HostRuntimeDescriptor
  HostRuntimeAssemblyDescriptor
  HostFeatureEndpointMetadata
  HostFeatureEndpointCollection
  HostRuntimeDescriptorSchema

src/Presentation.Cli
  CliHost
  CliCommandModule
  WorkspaceContext
  Output
  HostDiscovery
    HostRuntimeDiscovery
    HostRuntimeSelection
  ConsoleCommands
    ConsoleCommandAdapter
    ConsoleCommandModuleRegistration
  HostCommands
    HostCommandForwarder
    HostCommandClient
    HostRuntimeSelector
  Mcp
    command module specified separately
  CodeGeneration
    future command module
  Scaffolding
    future command module
```

Shared CLI concerns:

* command routing
* command module registration
* version output
* workspace resolution
* host runtime descriptor discovery and selection
* configuration loading
* Spectre.Console-based human-readable output helpers
* structured output helpers
* logging setup
* Console Commands integration

Command-specific concerns remain inside command modules.

Shared descriptor DTOs live in `Common.Abstractions/HostDiscovery`. CLI-specific discovery, validation, selection, and rendering services live in `Presentation.Cli/HostDiscovery`. App-side descriptor writing lives in `Presentation.Web/HostDiscovery`.

## Console Commands Integration

The CLI should use Console Commands as the preferred command model for human-invoked terminal commands.

Expected behavior:

* command classes can be registered through the same command abstraction used by application console commands
* grouped commands keep the same shape as application console command groups
* options and arguments use the same attribute-driven binding semantics where practical
* generated help and validation errors follow the Console Commands behavior
* Spectre.Console remains the default terminal rendering model
* non-interactive single-shot execution is supported for automation

The CLI may introduce a thin adapter around Console Commands when tool-host concerns require it, but the public command behavior should remain compatible.

## Terminal output design

The CLI shall use Spectre.Console for human-readable terminal output. The visual style should be modern, minimal, and consistent with the Console Commands feature.

Design principles:

* use concise tables for lists and comparisons
* use subtle rules or section headings for major output groups
* use restrained color for status and emphasis only
* avoid noisy banners, oversized panels, and decorative output
* prefer aligned columns over long prose for host, feature, and version summaries
* use clear empty states and next-step hints when no data is available
* keep command output script-friendly by supporting quiet or structured modes where useful

Recommended status styling:

| Status | Style |
| ------ | ----- |
| `Ready` | green text |
| `Stale` | yellow text |
| `Unreachable` | red text |
| `Invalid` | red text |
| `FeatureUnavailable` | yellow text |
| `Unknown` | dim text |

Examples of Spectre.Console output surfaces:

* `bdk hosts list` renders a compact table
* `bdk hosts versions` renders a compact table
* startup host summaries use a minimal rule or heading plus compact rows
* no-host warnings use a short warning line plus one actionable hint
* validation failures use concise error text followed by relevant command help

Command modules should use shared output helpers so command output looks like one CLI, not a collection of unrelated tools.

## Structured output contract

Commands that support automation should support structured output through `--output json`.

Rules:

* structured output must suppress decorative headings, startup host summaries, color, progress spinners, and human-only hints unless those hints are represented as structured fields
* successful responses should include stable property names and avoid terminal markup
* failures should include `available` when relevant, stable `code`, human-readable `summary`, optional `details`, optional `next`, and `exitCode`
* command modules may add module-specific data fields, but shared error fields must keep the same meaning across modules
* JSON output should be deterministic enough for tests and scripts
* commands that do not support structured output must fail with `invalid_arguments` when `--output json` is supplied, or document why structured output is not meaningful

Example structured failure:

```json
{
  "available": false,
  "code": "no_host_found",
  "summary": "No running DevKit host with Console Command forwarding is available.",
  "next": "Start a local DevKit webapp host and try again.",
  "exitCode": 3
}
```

## Host Commands Forwarding Module

The Host Commands module is a standard `bdk` command module that bridges local CLI invocation to Console Commands hosted by a running webapp.

Responsibilities:

* discover compatible running hosts
* select the target host explicitly or through a shared selection
* forward the full host command token stream
* execute the command in the host process
* return command output, exit status, and validation errors to the CLI
* render host output in the CLI without losing terminal formatting

Non-responsibilities:

* do not execute arbitrary OS shell commands
* do not expose host commands from non-development hosts by default
* do not reinterpret host command arguments in the CLI after the forwarding boundary
* do not require MCP to be running
* do not require MCP tools or `IMcpHandler` implementations

Forwarding request shape, conceptually:

```json
{
  "protocolVersion": 1,
  "requestId": "01J...",
  "command": "diag",
  "arguments": ["perf"],
  "rawTokens": ["diag", "perf"],
  "workingDirectory": "F:/projects/bit/bITdevKit",
  "timeoutMs": 60000,
  "maxOutputBytes": 1048576,
  "terminal": {
    "ansi": true,
    "width": 120,
    "outputFormat": "text"
  }
}
```

Forwarding response shape, conceptually:

```json
{
  "protocolVersion": 1,
  "requestId": "01J...",
  "available": true,
  "exitCode": 0,
  "output": "... ANSI or plain terminal output ...",
  "outputTruncated": false,
  "error": null
}
```

Unavailable response shape, conceptually:

```json
{
  "protocolVersion": 1,
  "requestId": "01J...",
  "available": false,
  "code": "no_host_found",
  "summary": "No running DevKit host with Console Command forwarding is available.",
  "next": "Start a local DevKit webapp host and try again.",
  "exitCode": 3,
  "output": "",
  "outputTruncated": false,
  "error": {
    "code": "no_host_found",
    "summary": "No running DevKit host with Console Command forwarding is available.",
    "details": null
  }
}
```

The host-side execution path should use the same Console Commands binder, validation, help, grouped subcommand handling, and `IConsoleCommand.ExecuteAsync` behavior described in [features-presentation-console-commands.md](../features-presentation-console-commands.md).

## Command Surface

Initial global command surface:

```text
bdk version
bdk help
```

Known dependent command modules:

```text
bdk hosts    shared host discovery and selection commands
bdk host     standard host Console Command forwarding module
bdk mcp      specified in spec-presentation-web-mcp-diagnostics.md
```

Command discovery is registration-based. The CLI package registers each command module explicitly at startup. No automatic assembly scanning, dynamic plugin loading, or project-level CLI module extension is implemented for the initial platform.

Discoverability requirements:

* `bdk help` lists registered command groups and concise descriptions
* `bdk help <command>` shows command-specific options, arguments, examples, and failure behavior where practical
* `bdk version` lists registered command modules and CLI version metadata
* command modules document examples and failure behavior in their owning specification
* hidden or experimental commands must be clearly marked if they are exposed at all

Reserved future command groups:

```text
bdk generate ...
bdk scaffold ...
```

The reserved command groups document CLI direction only. Their detailed behavior should be defined in separate specifications before implementation.

Future generation and scaffolding command modules should define their own boundaries before implementation, including template discovery, dry-run behavior, overwrite policy, workspace and project detection, naming conventions, architecture placement rules, and post-generation validation. The base CLI should provide shared workspace, output, confirmation, and structured-result services, but it must not implement generation or scaffolding behavior as part of the initial platform.

## Initial JSON output shapes

Initial commands that support `--output json` should return these minimal shapes. Additional fields may be added later, but existing field meanings must remain stable.

`bdk version --output json`:

```json
{
  "version": "1.0.0",
  "informationalVersion": "1.0.0+abc1234",
  "modules": [
    { "name": "hosts", "description": "Shared host discovery and selection commands" },
    { "name": "host", "description": "Host Console Command forwarding" },
    { "name": "mcp", "description": "STDIO MCP command module" }
  ],
  "exitCode": 0
}
```

`bdk help --output json`:

```json
{
  "commands": [
    { "name": "version", "description": "Shows CLI version information" },
    { "name": "hosts", "description": "Inspects and selects running DevKit hosts" },
    { "name": "host", "description": "Forwards Console Commands to a running host" },
    { "name": "mcp", "description": "Starts the MCP command module" }
  ],
  "exitCode": 0
}
```

`bdk hosts list --output json`:

```json
{
  "workspacePath": "F:/projects/bit/bITdevKit",
  "hosts": [
    {
      "runtimeId": "commerce-api-5001",
      "applicationName": "CommerceApi",
      "environmentName": "Development",
      "status": "Ready",
      "features": ["consoleCommands", "mcp"],
      "processId": 23844,
      "startedAt": "2026-06-19T14:20:00Z"
    }
  ],
  "exitCode": 0
}
```

`bdk hosts current --output json`:

```json
{
  "workspacePath": "F:/projects/bit/bITdevKit",
  "selectedRuntimeId": "commerce-api-5001",
  "host": {
    "runtimeId": "commerce-api-5001",
    "applicationName": "CommerceApi",
    "status": "Ready"
  },
  "exitCode": 0
}
```

When no host is selected, `selectedRuntimeId` and `host` are `null` and `exitCode` remains `0`.

`bdk hosts select <runtimeId> --output json`:

```json
{
  "workspacePath": "F:/projects/bit/bITdevKit",
  "selectedRuntimeId": "commerce-api-5001",
  "selectionPath": "%LOCALAPPDATA%/bdk/hosts/selections/<workspace-hash>.json",
  "exitCode": 0
}
```

`bdk hosts refresh --output json`:

```json
{
  "workspacePath": "F:/projects/bit/bITdevKit",
  "hosts": [
    { "runtimeId": "commerce-api-5001", "status": "Ready" },
    { "runtimeId": "billing-api-5001", "status": "Stale" }
  ],
  "exitCode": 0
}
```

`bdk hosts versions --output json`:

```json
{
  "workspacePath": "F:/projects/bit/bITdevKit",
  "hosts": [
    {
      "runtimeId": "commerce-api-5001",
      "applicationName": "CommerceApi",
      "assembly": {
        "name": "CommerceApi.Presentation.Web.Server",
        "version": "1.0.0.0",
        "informationalVersion": "1.0.0+abc1234",
        "fileVersion": "1.0.0.0"
      }
    }
  ],
  "exitCode": 0
}
```

`bdk hosts clean --yes --output json`:

```json
{
  "removed": [
    { "path": "%LOCALAPPDATA%/bdk/hosts/runtimes/billing-api.1234.json", "reason": "Stale" }
  ],
  "skipped": [
    { "path": "%LOCALAPPDATA%/bdk/hosts/runtimes/commerce-api.23844.json", "reason": "LiveProcess" }
  ],
  "exitCode": 0
}
```

`bdk host run status --output json`:

```json
{
  "runtimeId": "commerce-api-5001",
  "available": true,
  "exitCode": 0,
  "output": "... ANSI-free or plain terminal output ...",
  "outputTruncated": false,
  "error": null
}
```

Forwarded host command JSON output wraps the forwarding protocol response. It does not parse or reshape command-specific JSON emitted by the host command.

## Global Commands

## bdk version

Shows the CLI version and registered command modules. Command modules may add their own capability or protocol details, but `bdk version` must remain safe to run without an application runtime.

## bdk help

Shows command help using the same command discovery, grouping, validation, and help conventions as the Console Commands feature.

## Shared Host Registry Commands

## bdk hosts list

Lists ready host descriptors for the current workspace by default.

Example output:

```text
Runtime ID          App             Features                 Status
commerce-api-5001   CommerceApi     consoleCommands,mcp      Ready
billing-api-5001    BillingApi      consoleCommands          Ready
```

Options:

```text
--all
--feature <featureName>
```

By default, `bdk hosts list` hides stale descriptors and shows only ready descriptors for the current workspace. `--all` includes stale descriptors and descriptors outside the current workspace. `--feature` filters to hosts that advertise a specific feature endpoint, such as `consoleCommands` or `mcp`.

## bdk hosts current

Shows the currently selected host for the workspace, if one exists.

## bdk hosts select

Selects a host for the current workspace.

```bash
bdk hosts select commerce-api-5001
```

The selected host is written to the OS user-local `bdk/hosts/selections/<workspace-hash>.json` registry.

## bdk hosts refresh

Re-reads host descriptors, validates reachable hosts, and reports ready hosts by default. Stale descriptors remain hidden unless `--all` is supplied.

## bdk hosts versions

Shows the host entry assembly version metadata for ready hosts in the current workspace by default.

This is the first proof-of-concept command for the shared host registry. It does not require a selected host and does not connect to a feature endpoint. It reads host descriptor metadata and reports one version per host: the version of the host entry assembly. It must not enumerate or display all assemblies loaded inside the host process.

Example output:

```text
Runtime ID          App             Assembly                              Version  Informational Version
commerce-api-5001   CommerceApi     CommerceApi.Presentation.Web.Server   1.0.0.0  1.0.0+abc1234
billing-api-5001    BillingApi      BillingApi.Presentation.Web.Server    1.0.0.0  1.0.0+def5678
```

Options:

```text
--all
```

`--all` includes stale descriptors and descriptors outside the current workspace.

## bdk hosts clean

Removes stale host descriptors and stale feature IPC metadata where safe.

The command should ask for confirmation unless `--yes` is supplied.

## bdk hosts kill

Terminates ready host processes for the current workspace.

Examples:

```bash
bdk hosts kill commerce-api-5001
bdk hosts kill commerce-api-5001 --yes
bdk hosts kill --all --yes
```

Behavior:

* requires either a runtime id or `--all`
* rejects using a runtime id and `--all` together
* targets only descriptors whose status is `Ready`
* does not kill stale, invalid, unreachable, or out-of-workspace descriptors
* asks for confirmation unless `--yes` is supplied
* does not delete descriptors; `bdk hosts clean` remains responsible for descriptor cleanup

## Standard Host Commands Module

## bdk host run

Forwards a Console Command invocation to a selected running webapp host.

Examples:

```bash
bdk host run status
bdk host run diag perf
bdk host run --host commerce-api-5001 -- seed products --count=50
```

Behavior:

* resolves a compatible running host
* forwards all host command tokens after `bdk host run` or after a `--` forwarding boundary
* executes the command inside the host process
* displays the host command output in the CLI
* returns a non-zero exit code when the host command fails

Host selection options:

```text
--host <runtimeId>
--runtime-id <runtimeId>
```

If no host is available, the command reports `no_host_found` with startup guidance.

If multiple hosts are available and no host can be selected, the command reports `host_selection_required` and lists available host ids.

## Command Module Rules

Command modules should follow these rules:

* register independently with the CLI host
* use Console Commands-compatible terminal behavior where practical
* keep command-specific dependencies inside the module
* avoid requiring a running application unless the command explicitly needs runtime state
* support human-readable terminal output
* support structured output where useful for automation
* document command examples and failure behavior in the owning feature spec

## MCP Module Dependency

The MCP command module depends on the base CLI foundation.

The MCP specification owns:

* `bdk mcp`
* MCP use of shared host discovery and selection
* STDIO MCP hosting
* MCP tool catalog
* MCP endpoint metadata in shared host descriptors
* CLI-side MCP IPC client behavior

The CLI specification owns:

* the `bdk` tool identity
* shared command routing
* command module registration
* shared host discovery and selection commands
* Console Commands integration
* standard host Console Command forwarding
* global command behavior
* future command-module boundaries

## Testing Strategy

CLI tests should use the existing `Presentation.UnitTests` test project. Do not create a new test project for the initial CLI platform.

Use focused test doubles for descriptor registry paths, process checks, endpoint validation, workspace resolution, registered command modules, and output writers.

## Unit tests

Cover:

```text
CLI command module registration
explicit command module registration without auto-discovery
global command discovery
global option parsing for --workspace, --verbose, --quiet, --no-color, --nologo, --banner, --non-interactive, and --output
exit code mapping for success, validation, host, protocol, and internal errors
workspace resolution uses --workspace, nearest .slnx/.sln/.git, then current directory fallback
host runtime descriptor parsing and validation
host descriptor writing is treated as a DevKit web host foundation dependency
descriptor cleanup skips live hosts and reports removed or skipped descriptors
nonce handshake is required for host endpoints that advertise a nonce
host runtime selection behavior
bdk startup lists discovered hosts for human-readable output
host-aware commands show startup host summaries by default
global commands show startup host summaries only when verbose or directly relevant
bdk startup shows a friendly no-host warning when no hosts are discovered for host-aware commands
bdk startup host summary is suppressed for quiet, machine-readable, or CI output
bdk hosts list shows ready workspace-filtered host descriptors by default
bdk hosts list --all includes stale descriptors and descriptors outside the current workspace
bdk hosts current reports the selected workspace host
bdk hosts select writes the workspace-scoped selected host
bdk hosts versions shows the host entry assembly version for each ready workspace host
bdk hosts versions --all includes stale descriptors and descriptors outside the current workspace
bdk hosts clean removes stale descriptors only where safe
bdk hosts kill requires a runtime id or --all and only targets ready workspace hosts
dotnet run --project src/Presentation.Cli/Presentation.Cli.csproj -- version uses the same command host as the packaged tool
bdk version works without command modules requiring a runtime
bdk help uses Console Commands-compatible help behavior
command parsing, validation, and help align with Console Commands behavior
command module command groups register independently
command-specific services do not leak into shared CLI services
commands without runtime dependencies do not require runtime selection
host command forwarding command is available as a standard module
registered commands are discoverable through help output
reserved generation and scaffolding commands are not implemented by the base platform
```

## Host command forwarding tests

Cover:

```text
bdk host run forwards command name and all arguments to the selected host
quoted values, flags, options, and grouped subcommands are preserved across forwarding
host command executes inside the host process using host-registered IConsoleCommand services
host command validation errors are returned and rendered by the CLI
host command Spectre.Console output is displayed by the CLI
host command failure returns a non-zero CLI exit code
no compatible host returns no_host_found with guidance
multiple compatible hosts without selection returns host_selection_required with host ids
explicit --host or --runtime-id selects the target host
selected stale or unreachable host returns selected_host_unavailable
host command forwarding does not require MCP tools or IMcpHandler registrations
host forwarding IPC v1 is buffered and single-request/single-response
host forwarding IPC v1 does not support interactive stdin
protocol version mismatch returns the stable protocol/version mismatch exit code
```

## CLI module tests

Each command module should cover:

```text
command parsing
argument and option validation
help output
human-readable output shape
structured output shape where supported
Spectre.Console output follows the shared minimal CLI style
missing dependency behavior
command-specific failure behavior
```

## Finalized Decisions

* `bdk` is the shared DevKit CLI tool and is not MCP-only.
* the base CLI foundation is implemented before MCP-specific CLI behavior.
* command modules are registered explicitly in the CLI package; no auto-discovery or project-owned CLI modules are planned for the initial platform.
* `bdk` uses a thin CLI adapter over the Console Commands model where practical.
* CLI exit code categories are stable for success, command failure, invalid arguments, host discovery, host selection, selected host availability, protocol version mismatch, and internal errors.
* workspace resolution uses explicit `--workspace`, nearest `.slnx`/`.sln`/`.git`, then current directory fallback.
* host discovery and selection use OS user-local `bdk/hosts` descriptor and selection registries.
* `Presentation.Web` owns shared host descriptor writing through `HostRuntimeDescriptorWriter` and feature endpoint contributors.
* host forwarding IPC v1 is buffered, single-request/single-response, and non-interactive.
* host-aware commands show startup host summaries by default; global commands show them only when verbose or directly relevant.
* human-invoked terminal commands reuse or align with the Console Commands feature.
* MCP is a command module inside `bdk`, specified separately.
* future command modules such as generation and scaffolding get their own specifications.

## Summary

The DevKit CLI provides the shared `bdk` command surface for local-development workflows. It composes independent command modules, aligns terminal command behavior with the existing Console Commands feature, and gives MCP, generation, scaffolding, and future tooling a common host without coupling them to each other.
