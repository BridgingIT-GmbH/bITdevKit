---
status: implemented
---

# Design Specification: DevKit Web Application Host

> This design document specifies `DevKitWebApplication`, the DevKit-aware ASP.NET Core web host entry point. It owns host-side local-development integration for the `bdk` CLI, including host runtime descriptor writing, local tooling eligibility, and registration of host-advertised endpoint capabilities such as Console Command forwarding and MCP. This host foundation must be implemented before the DevKit CLI and MCP command module that depend on its descriptors and local IPC registrations.

[TOC]

## Introduction

DevKit web applications currently start with the standard ASP.NET Core entry point:

```csharp
var builder = WebApplication.CreateBuilder(args);
```

That gives applications full ASP.NET Core flexibility, but DevKit has cross-cutting host concerns that belong at application creation time: configuration defaults, local-development policy, host identity, runtime descriptor writing, and later broader fluent DevKit setup conventions.

`DevKitWebApplication.CreateBuilder(args)` is the DevKit-aware host entry point. It wraps `WebApplication.CreateBuilder(args)`, keeps the returned builder compatible with normal ASP.NET Core startup, and provides one early place to register local `bdk` host integration safely.

The first required use case is local `bdk` support:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args);
```

In Development, this host entry point registers descriptor writer infrastructure and local host capabilities by convention. Outside Development, it fails closed and does not register descriptor writing, local IPC servers, MCP endpoint metadata, or Console Command forwarding services unless explicitly overridden for tests.

Existing applications that continue to use `WebApplication.CreateBuilder(args)` remain supported. They keep the current ASP.NET Core startup and existing DevKit feature registration behavior, but they do not get local `bdk` host discovery, descriptor writing, host command forwarding, MCP endpoint advertising, or future DevKit builder extension hooks by convention.

## Positioning

`DevKitWebApplication` is:

* a DevKit-aware wrapper around ASP.NET Core `WebApplication.CreateBuilder(args)`
* the host-side prerequisite for local `bdk` discovery
* the owner of local tooling eligibility policy
* the owner of host runtime descriptor writer registration
* the composition point for host-advertised local endpoint capabilities
* a future foundation for broader fluent DevKit web application setup

`DevKitWebApplication` is not:

* a replacement for ASP.NET Core hosting
* a production remote administration surface
* a CLI command host
* an MCP server by itself
* an application module system replacement
* a place to query application databases or rebuild application DI
* a breaking replacement for existing `WebApplicationBuilder` startup code

The core boundary is:

```text
DevKitWebApplication owns host-side local tooling registration.
bdk owns CLI command routing and descriptor consumption.
MCP owns MCP protocol behavior and app-side handlers.
Console Commands own command definitions and in-process execution.
```

## Implementation Order

This host foundation must be implemented before:

* [spec-presentation-cli.md](spec-presentation-cli.md)
* [spec-presentation-web-mcp-diagnostics.md](spec-presentation-web-mcp-diagnostics.md)

The generic non-web host sibling is specified in [spec-presentation-devkit-console-host.md](spec-presentation-devkit-console-host.md). `DevKitWebApplication` must remain web-specific and must not own the generic `DevKitApplication` builder.

The CLI depends on host descriptors being written by running DevKit web applications. The MCP command module depends on the host descriptor and app-side MCP IPC endpoint metadata contributed by this host foundation.

## DevKitWebApplication CreateBuilder

Target API:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args);
```

Opt-out and tuning use fluent options:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args, options => options
  .Cli(cli => cli.Enabled(false)));
```

```csharp
var builder = DevKitWebApplication.CreateBuilder(args, options => options
  .Cli(cli => cli
    .ConsoleCommands(false)
    .Mcp(false)));
```

```csharp
var builder = DevKitWebApplication.CreateBuilder(args, options => options
  .Cli(cli => cli.Mcp(false)));
```

`CreateBuilder` must remain a thin wrapper around `WebApplication.CreateBuilder(args)`. It should return a DevKit builder wrapper that is compatible with normal ASP.NET Core startup and exposes the common `WebApplicationBuilder` surface so existing DevKit applications can adopt it by changing the first startup line and keeping existing `builder.Services`, `builder.Configuration`, `builder.Environment`, `builder.Host`, and `builder.Logging` usage.

The returned type must also implement the common DevKit builder abstractions described below so feature packages can add future fluent hooks such as `WithMessaging(...)`, `WithQueueing(...)`, or `WithJobs(...)` without depending on `Presentation.Web` internals.

The existing startup path remains valid:

```csharp
var builder = WebApplication.CreateBuilder(args);
```

Using the raw ASP.NET Core builder means the application has not opted into DevKit host discovery. The application should not write a host runtime descriptor, should not register local CLI IPC capabilities by convention, and should not be visible to `bdk hosts ...` unless lower-level descriptor services are explicitly registered for tests or migration scenarios.

## Local Tooling Policy

`DevKitWebApplication.CreateBuilder(args)` evaluates local CLI integration eligibility immediately after creating the underlying `WebApplicationBuilder`.

The policy may use only:

* `builder.Environment`
* `builder.Configuration`
* explicit `DevKitWebApplication` options

The policy must not depend on application services, modules, databases, endpoints, dashboard setup, middleware, hosted services registered later by the application, or built application state.

Default policy:

```text
1. Explicit code opt-out disables all local CLI integration.
2. Host environment must be Development.
3. Explicit test override may permit non-Development registration for tests only.
4. Configuration may disable local CLI integration, but must not enable it outside Development.
5. Capability options decide whether Console Command forwarding and MCP are enabled.
```

Configuration can disable local tooling:

```json
{
  "DevKit": {
    "Cli": {
      "Enabled": false,
      "ConsoleCommands": false,
      "Mcp": false
    }
  }
}
```

Configuration must not bypass the Development environment gate. Enabling local CLI integration outside Development requires an explicit code-level test override with a clearly named option.

Conceptual policy shape:

```csharp
public static DevKitLocalToolingDecision Evaluate(
  IHostEnvironment environment,
  IConfiguration configuration,
  DevKitCliHostOptions options)
{
  if (!options.Enabled)
  {
    return DevKitLocalToolingDecision.Disabled("Disabled by options.");
  }

  if (!environment.IsDevelopment() && !options.AllowOutsideDevelopmentForTests)
  {
    return DevKitLocalToolingDecision.Disabled("Host environment is not Development.");
  }

  if (configuration.GetValue<bool?>("DevKit:Cli:Enabled") == false)
  {
    return DevKitLocalToolingDecision.Disabled("Disabled by configuration.");
  }

  return DevKitLocalToolingDecision.Enabled(
    consoleCommands: options.ConsoleCommandsEnabled && configuration.GetValue<bool?>("DevKit:Cli:ConsoleCommands") != false,
    mcp: options.McpEnabled && configuration.GetValue<bool?>("DevKit:Cli:Mcp") != false);
}
```

When eligible, `CreateBuilder` registers descriptor writer, cleanup, endpoint contributor, and local IPC hosted services. The descriptor is not written during builder creation. It is written when the host starts, after the application is built and after enabled endpoint contributors have produced metadata.

When not eligible, descriptor writer services, cleanup services, local IPC servers, endpoint contributors, MCP endpoint metadata, and Console Command forwarding services must not be registered. The implementation must fail closed rather than registering services that later decide not to run.

The implementation must not infer local tooling eligibility from machine name, bound URLs, debugger state, loopback addresses, or operating system checks. The default gate is `IHostEnvironment.IsDevelopment()` plus explicit options.

## Options Contract

The options API should be fluent, but it should configure defaults rather than introduce separate feature setup chains.

Conceptual shape:

```csharp
public sealed class DevKitWebApplicationOptionsBuilder
{
  public DevKitWebApplicationOptionsBuilder Cli(Action<DevKitCliHostOptionsBuilder> configure);
}

public sealed class DevKitCliHostOptionsBuilder
{
  public DevKitCliHostOptionsBuilder Enabled(bool enabled = true);

  public DevKitCliHostOptionsBuilder ConsoleCommands(bool enabled = true);

  public DevKitCliHostOptionsBuilder ConsoleCommands(Action<DevKitConsoleCommandForwardingOptionsBuilder> configure);

  public DevKitCliHostOptionsBuilder Mcp(bool enabled = true);

  public DevKitCliHostOptionsBuilder Mcp(Action<DevKitMcpHostOptionsBuilder> configure);
}

public sealed class DevKitConsoleCommandForwardingOptionsBuilder
{
  public DevKitConsoleCommandForwardingOptionsBuilder Enabled(bool enabled = true);
}

public sealed class DevKitMcpHostOptionsBuilder
{
  public DevKitMcpHostOptionsBuilder Enabled(bool enabled = true);

  public DevKitMcpHostOptionsBuilder DisableFeature(string featureName);

  public DevKitMcpHostOptionsBuilder WorkspacePathFromContentRoot();
}
```

The exact implementation can evolve, but it must preserve these behaviors:

* `Cli(cli => cli.Enabled(false))` disables descriptor writing and all local CLI integration
* `ConsoleCommands(false)` disables `features.consoleCommands` and its IPC server
* `Mcp(false)` disables `features.mcp` and its local IPC endpoint registration
* configuration may disable but cannot bypass the Development gate
* non-Development enablement requires an explicitly named test-only override

## DevKit Builder Abstractions

The first host foundation implementation must include the minimal abstraction contracts needed for future feature-owned fluent setup. These contracts make the future fluent shape possible without implementing every feature integration in this specification.

The common contracts should live in `Common.Abstractions` because Application and Presentation feature packages already reference it and because the contracts should not depend on `Presentation.Web` internals.

`Common.Abstractions` must stay clean of ASP.NET Core hosting dependencies for these contracts. It may use existing dependency injection and configuration abstractions, but host environment information should flow through a DevKit-owned abstraction instead of `WebApplicationBuilder`, `WebApplication`, `IWebHostEnvironment`, endpoint route builders, or MVC types.

Suggested placement:

```text
src/Common.Abstractions/Hosting
  IDevKitApplicationBuilder
  IDevKitHostApplicationBuilder
  IDevKitBuilderArea
  IDevKitHostEnvironment
  IDevKitFeatureHook
  DevKitFeatureHookContext
  DevKitFeatureHookStage
  DevKitBuilderProperties
```

The contracts must expose only stable Microsoft.Extensions abstractions and DevKit-owned abstractions. They must be clean of ASP.NET Core dependencies and must not expose `WebApplicationBuilder`, `WebApplication`, endpoint route builders, MVC types, EF Core types, messaging brokers, queue brokers, job scheduler types, or feature implementation types.

Conceptual shape:

```csharp
public interface IDevKitApplicationBuilder
{
  IServiceCollection Services { get; }

  IConfiguration Configuration { get; }

  IDevKitHostEnvironment Environment { get; }

  IDictionary<string, object?> Properties { get; }

  IDevKitApplicationBuilder Configure(Action<IDevKitApplicationBuilder> configure);
}

public interface IDevKitBuilderArea
{
  IDevKitApplicationBuilder Application { get; }
}

public interface IDevKitHostEnvironment
{
  string ApplicationName { get; }

  string EnvironmentName { get; }

  string ContentRootPath { get; }
}

public interface IDevKitFeatureHook
{
  string Name { get; }

  DevKitFeatureHookStage Stage { get; }

  void Apply(DevKitFeatureHookContext context);
}

public sealed class DevKitFeatureHookContext
{
  public required IDevKitApplicationBuilder Builder { get; init; }

  public required IServiceCollection Services { get; init; }

  public required IConfiguration Configuration { get; init; }

  public required IDevKitHostEnvironment Environment { get; init; }
}

public enum DevKitFeatureHookStage
{
  ConfigureServices,
  BeforeBuild,
  AfterBuild
}
```

`Presentation.Web` owns the concrete adapter from `WebApplicationBuilder` to these abstractions:

```text
src/Presentation.Web/Hosting
  DevKitWebApplicationBuilder : IDevKitApplicationBuilder
  DevKitPresentationBuilder
```

The concrete wrapper may expose the underlying ASP.NET Core builder for web-specific packages, but that property must not be part of the common abstraction contract.

Feature packages add extension methods against the abstraction they need:

```csharp
public static class MessagingDevKitBuilderExtensions
{
  public static TBuilder WithMessaging<TBuilder>(
    this TBuilder builder,
    Action<MessagingDevKitBuilder> configure)
    where TBuilder : IDevKitApplicationBuilder;
}
```

Web-only packages may add extension methods against a web/presentation-specific builder abstraction owned by `Presentation.Web` or `Presentation` if they need ASP.NET Core concepts:

```csharp
public static class DashboardDevKitBuilderExtensions
{
  public static IDevKitPresentationBuilder WithDashboard(
    this IDevKitPresentationBuilder builder,
    Action<DashboardOptionsBuilder>? configure = null);
}
```

The central host foundation must not know about every feature integration. It only provides the shared builder contract, property bag, hook execution points, and concrete web adapter. Messaging, queueing, jobs, orchestrations, modules, dashboard, OpenAPI, metrics, and observability remain responsible for their own extension methods and options builders.

Feature hooks should be explicit and package-owned. The initial host foundation must not auto-discover feature hooks by scanning assemblies. Future specifications may add discovery, but the first version should prefer extension methods that register exactly the services and hooks requested by the application startup code.

## Host Runtime Descriptor

Each eligible running DevKit web application writes one shared host descriptor file to the OS user-local registry specified by the CLI foundation.

The shared descriptor DTOs live in `Common.Abstractions`:

```text
src/Common.Abstractions/HostDiscovery
  HostRuntimeDescriptor
  HostRuntimeAssemblyDescriptor
  HostFeatureEndpointMetadata
  HostFeatureEndpointCollection
  HostRuntimeDescriptorSchema
```

These DTOs are schema contracts and must not depend on `Presentation.Cli`, `Presentation.Web`, MCP SDK types, Console Commands implementation types, ASP.NET Core hosting types, or application services.

The descriptor can be written without endpoint capabilities. Such a descriptor is enough for `bdk hosts list` and `bdk hosts versions`.

In host descriptors, `features` means host-advertised endpoint capabilities. It does not mean CLI command modules.

Host-advertised capabilities:

| Descriptor metadata | Enabled by | Purpose |
| ------------------- | ---------- | ------- |
| `features.consoleCommands` | local Console Command forwarding capability | Allows `bdk host run ...` to connect over local IPC. |
| `features.mcp` | local MCP host capability | Allows `bdk mcp` to connect to app-side MCP handlers over local IPC. |

## Descriptor Writer Ownership

`Presentation.Web` owns the shared host descriptor writer infrastructure:

```text
src/Presentation.Web/HostDiscovery
  HostRuntimeDescriptorWriter
  IHostFeatureEndpointContributor
  HostDescriptorOptions
  HostDescriptorCleanupService
  HostDescriptorLifecycleService
  LocalIpcEndpointState
  LocalIpcHandshakeRequest
  LocalIpcHandshakeResponse
```

Responsibilities:

* write one descriptor for the current running host
* include shared host metadata such as workspace, content root, process id, start time, and entry assembly version
* collect optional endpoint metadata from registered `HostFeatureEndpointContributor` implementations
* write descriptors only for eligible local-development hosts
* remove the host's own descriptor during graceful shutdown when possible
* tolerate stale descriptors left by crashes or forced process termination
* avoid preventing application startup when descriptor writing fails
* write debug-level startup, descriptor and feature endpoint diagnostics for visibility

Feature endpoint contributors are owned by the feature that exposes the endpoint. Console Command forwarding contributes `features.consoleCommands`. MCP contributes `features.mcp`. The shared descriptor writer remains protocol-neutral and does not know MCP or Console Command forwarding details beyond the contributed metadata shape.

## Descriptor Lifecycle

Descriptor lifecycle rules:

* write the descriptor after the host has enough metadata to describe itself and after endpoint contributors have produced their local endpoint metadata
* write descriptor files atomically by writing to a temporary file and replacing the final descriptor path
* use a filename derived from runtime id and process id to reduce collisions between restarts
* refresh heartbeat metadata only when useful for stale detection; refreshes must not rotate endpoint nonces
* remove only the current host's descriptor on graceful shutdown
* treat missing cleanup on crash as normal behavior
* never delete descriptors owned by live processes

Host-start lifecycle:

```text
DevKitWebApplication.CreateBuilder(args)
  -> creates WebApplicationBuilder
  -> evaluates local tooling policy
  -> registers local tooling services only when eligible

builder.Build()
  -> builds the normal ASP.NET Core app

app.Run()
  -> host starts
  -> local IPC endpoints bind when enabled
  -> feature endpoint contributors produce metadata
  -> HostRuntimeDescriptorWriter writes descriptor

host shutdown
  -> descriptor writer removes only this host's descriptor where possible
```

## Local IPC Capability Registration

`DevKitWebApplication` registers host-side local IPC capabilities by convention in Development.

Console Command forwarding:

* enabled when local CLI integration is eligible, Console Commands are registered, and `ConsoleCommands` is not disabled
* starts a local IPC server for forwarded Console Commands
* supports nonce-protected `ping` and `run` operations over the advertised named-pipe endpoint
* executes `run` through the existing `ConsoleCommandExecutor` and returns captured Spectre.Console output
* contributes `features.consoleCommands` endpoint metadata
* must not expose host commands outside Development
* must not require MCP

MCP:

* enabled when local CLI integration is eligible, MCP support is available, and `Mcp` is not disabled
* starts a local MCP IPC endpoint shell for the MCP command module
* contributes `features.mcp` endpoint metadata
* leaves MCP protocol dispatch, capabilities, toolsets and `IMcpHandler` activation to [spec-presentation-web-mcp-diagnostics.md](spec-presentation-web-mcp-diagnostics.md)
* must not require Console Command forwarding

## Local Trust Model

Host discovery and local IPC are local-development features and use an OS user-local trust boundary.

Rules:

* descriptors are stored in user-local runtime locations, not in the repository
* descriptor presence is not sufficient authorization to execute a host command or MCP operation
* local IPC endpoints must require a nonce handshake when a nonce is advertised
* endpoint nonces are generated randomly per process start or endpoint rebind
* endpoint nonces remain stable for the lifetime of that endpoint
* descriptor refresh writes must not rotate active endpoint nonces
* non-development hosts must not advertise CLI-connectable endpoints by default
* destructive operations remain responsible for their own confirmation and environment safeguards

The nonce protects against accidental cross-process use of stale or spoofed descriptors in the same user-local registry. It is not a production authentication mechanism and must not be presented as one.

## Broader DevKit Setup Direction

`DevKitWebApplication` is intentionally more than a descriptor hook. It is a future foundation for a more fluent DevKit web application setup experience.

The initial implementation should stay narrow and focus on host identity, local tooling policy, descriptor writing, the common builder abstraction surface, and local IPC capability registration. Future specifications may extend the returned builder with broader DevKit conventions for modules, dashboards, OpenAPI, observability, metrics, fake identity provider, endpoints, and operational features.

The initial host foundation does not require a full application composition API like this:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
  .AddConfiguration()
  .AddLogging()
  .AddModules(modules => modules
    .WithModule<CoreModule>())
  .WithApplication(application => application
    .WithRequester()
    .WithNotifier())
  .WithMessaging(messaging => messaging
    .StartupDelay("00:00:30")
    .WithEntityFrameworkBroker<CoreDbContext>())
  .WithQueueing(queueing => queueing
    .StartupDelay("00:00:30")
    .WithEntityFrameworkBroker<CoreDbContext>())
  .WithOrchestrations(orchestrations => orchestrations
    .StartupDelay("00:00:30")
    .WithEntityFramework<CoreDbContext>())
  .WithPresentation(web => web
    .WithDashboard()
    .WithOpenApi()
    .WithMetrics()
    .WithConsoleCommands()
    .WithDevKitCli())
  .WithObservability();
```

That shape must remain possible through the common builder abstractions and feature-owned extension methods. It belongs to a separate fluent DevKit application composition specification or a later phase of this specification, but this host foundation must not close the door on it by returning only a raw `WebApplicationBuilder` or by hiding `Services`, `Configuration`, `Environment`, `Host`, or `Logging`.

Messaging, queueing, orchestrations, modules, requester/notifier, dashboard, OpenAPI, metrics, and observability integrations should remain owned by their existing feature packages until that broader composition layer is explicitly designed. Those feature packages can later hook into the DevKit builder by referencing the common builder abstractions and adding extension methods in their own packages.

For the initial host foundation, `WithDevKitCli()` is represented by the `DevKitWebApplication.CreateBuilder(args)` local tooling defaults and its `Cli(...)` options. The `WithConsoleCommands()` registration remains the Console Commands feature registration; when present and local CLI integration is eligible, the host foundation can advertise `features.consoleCommands` and enable host command forwarding.

The API must keep escape hatches by exposing the underlying ASP.NET Core builder surface:

```csharp
builder.Services
builder.Configuration
builder.Environment
builder.Host
builder.Logging
```

This allows incremental adoption:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args);

builder.Services.AddModules(builder.Configuration, builder.Environment)
  .WithModule<CoreModule>();

// Existing startup code remains valid.
```

Applications can also choose not to adopt `DevKitWebApplication` yet:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddModules(builder.Configuration, builder.Environment)
  .WithModule<CoreModule>();
```

This remains a supported startup style, but it is intentionally outside the new local `bdk` discovery path.

## Package and Project Placement

Suggested placement:

```text
src/Common.Abstractions/Hosting
  IDevKitApplicationBuilder
  IDevKitHostApplicationBuilder
  IDevKitBuilderArea
  IDevKitHostEnvironment
  IDevKitFeatureHook
  DevKitFeatureHookContext
  DevKitFeatureHookStage
  DevKitBuilderProperties

src/Presentation.Web
  DevKitWebApplication
  DevKitWebApplicationBuilder
  DevKitPresentationBuilder
  DevKitWebApplicationOptionsBuilder
  DevKitCliHostOptionsBuilder
  DevKitLocalToolingPolicy
  DevKitLocalToolingDecision
  HostDiscovery
    HostRuntimeDescriptorWriter
    HostFeatureEndpointContributor
    HostDescriptorOptions
    HostDescriptorCleanupService
  ConsoleCommands
    HostConsoleCommandEndpointContributor
    HostConsoleCommandIpcServer
  Mcp
    McpHostFeatureEndpointContributor
    McpIpcServer
```

Shared descriptor contracts live in `src/Common.Abstractions/HostDiscovery`. Shared builder extension contracts live in `src/Common.Abstractions/Hosting`.

Future feature-owned fluent extensions should live with the owning feature package, for example:

```text
src/Application.Messaging
  Hosting
    MessagingDevKitBuilderExtensions
    MessagingDevKitBuilder

src/Application.Queueing
  Hosting
    QueueingDevKitBuilderExtensions
    QueueingDevKitBuilder

src/Application.Jobs
  Hosting
    JobsDevKitBuilderExtensions
    JobsDevKitBuilder

src/Application.Orchestrations
  Hosting
    OrchestrationsDevKitBuilderExtensions
    OrchestrationsDevKitBuilder

src/Presentation.Web
  Hosting
    PresentationDevKitBuilderExtensions
    DevKitPresentationBuilder
```

This keeps the host foundation open for feature integration while avoiding a central `DevKitWebApplicationBuilder` that must reference every DevKit feature package.

## Testing Strategy

Tests should use existing Presentation test projects. Do not create a new test project only for this host foundation.

Cover:

```text
DevKitWebApplication.CreateBuilder wraps WebApplication.CreateBuilder compatibility
DevKitWebApplication.CreateBuilder returns a builder wrapper implementing IDevKitApplicationBuilder
common builder abstractions do not reference Presentation.Web or ASP.NET Core concrete hosting types
feature-owned extensions can be written against IDevKitApplicationBuilder without referencing Presentation.Web
raw WebApplication.CreateBuilder startup remains supported without descriptor writing or local CLI support by convention
local tooling policy enables only in Development by default
code opt-out disables all descriptor and IPC registration
configuration can disable local tooling but cannot enable it outside Development
test-only override can enable registration outside Development
non-eligible hosts do not register descriptor writer, cleanup, IPC servers, endpoint contributors, MCP endpoint metadata, or Console Command forwarding services
descriptor writer runs at host start, not during builder creation
descriptor writer can write descriptor without feature endpoints
Console Command forwarding contributes features.consoleCommands only when enabled
MCP contributes features.mcp only when enabled
descriptor cleanup removes only current host descriptor on graceful shutdown
startup continues when descriptor writing or IPC binding fails
```

## Finalized Decisions

* `DevKitWebApplication.CreateBuilder(args)` is the host-side prerequisite for local `bdk` integration.
* `DevKitWebApplication.CreateBuilder(args)` returns a DevKit builder wrapper, not only a raw `WebApplicationBuilder`, so future feature-owned fluent setup remains possible.
* common builder extension contracts live in `Common.Abstractions/Hosting` and do not expose `Presentation.Web` internals.
* existing `WebApplication.CreateBuilder(args)` startup remains supported, but does not opt into descriptor writing, local CLI support, or future DevKit builder hooks by convention.
* local CLI integration is Development-only by default and fails closed outside Development.
* configuration may disable local CLI integration but must not enable it outside Development.
* descriptor writing happens at host start, not during builder creation.
* `Presentation.Web` owns descriptor writing and local host capability registration.
* `Common.Abstractions/HostDiscovery` owns shared descriptor DTOs.
* Console Command forwarding and MCP are host-advertised endpoint capabilities, not CLI command modules.
* broader fluent DevKit web application setup is a future direction, not required for the first host foundation implementation.

## Summary

`DevKitWebApplication` gives DevKit a single web host entry point for local-development host identity and `bdk` integration. It writes host descriptors, registers local IPC capabilities only when eligible, fails closed outside Development, and creates a foundation for future fluent DevKit web application setup without replacing ASP.NET Core hosting.
