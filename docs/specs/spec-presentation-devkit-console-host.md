---
status: implemented
---

# Design Specification: DevKit Application Host

> This design document specifies `DevKitApplication`, the DevKit-aware generic host entry point for non-web applications such as console apps, workers, daemons and local tooling hosts. It is the non-web sibling of `DevKitWebApplication` and must be specified before implementation is extended, so web and non-web builders stay symmetrical without forcing one hosting model into the other.

[TOC]

## Introduction

DevKit web applications use `DevKitWebApplication.CreateBuilder(args)` to get a fluent DevKit startup experience while still wrapping ASP.NET Core's `WebApplication.CreateBuilder(args)`.

Non-web applications currently use the standard generic host entry point:

```csharp
var builder = Host.CreateApplicationBuilder(args);
```

That is the correct Microsoft hosting primitive for console applications, workers and local command hosts. DevKit should not make those applications adopt `DevKitWebApplication` only to get the same fluent setup conventions.

`DevKitApplication.CreateBuilder(args)` is the DevKit-aware generic host entry point. It wraps `Host.CreateApplicationBuilder(args)`, returns a DevKit builder wrapper over `HostApplicationBuilder`, and implements the same common `IDevKitApplicationBuilder` abstraction used by the web builder.

Target shape:

```csharp
var builder = DevKitApplication.CreateBuilder(args)
  .AddConfiguration()
  .AddLogging()
  .AddModules(modules => modules
    .WithModule<CoreModule>())
  .AddConsoleCommands(commands => commands
    .AddCommand<SampleConsoleCommand>());

using var host = builder.Build();

return await ConsoleCommands.RunAsync(host.Services, args);
```

The older raw generic host startup remains supported:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddConsoleCommands(commands => commands
  .AddCommand<SampleConsoleCommand>());
```

Using the raw builder means the application keeps normal .NET hosting behavior, but it does not opt into DevKit host metadata, DevKit local tooling policy, or future DevKit builder extension hooks by convention.

## Positioning

`DevKitApplication` is:

* a DevKit-aware wrapper around `Host.CreateApplicationBuilder(args)`
* the generic host sibling of `DevKitWebApplication`
* the recommended entry point for new DevKit console apps, workers, daemons and local command hosts
* a fluent composition surface for non-web DevKit setup
* a concrete adapter that implements the shared `IDevKitApplicationBuilder` contract
* compatible with the normal `HostApplicationBuilder` service, configuration, logging and environment model

`DevKitApplication` is not:

* a web application builder
* a replacement for `DevKitWebApplication`
* an ASP.NET Core dependency hidden behind another name
* a CLI binary by itself
* an MCP server by itself
* a production remote administration surface
* a replacement for raw `Host.CreateApplicationBuilder(args)` in existing applications

The core boundary is:

```text
DevKitApplication owns DevKit-aware generic host startup.
DevKitWebApplication owns DevKit-aware ASP.NET Core web startup.
Feature packages own fluent extensions over the shared builder contract.
Console Commands own command definitions and command execution.
bdk owns CLI command routing and host descriptor consumption.
```

## Relationship to DevKitWebApplication

`DevKitApplication` and `DevKitWebApplication` are siblings.

| Host kind | DevKit entry point | Wrapped Microsoft entry point | Build result |
| --------- | ------------------ | ----------------------------- | ------------ |
| Console, worker, daemon, local command host | `DevKitApplication.CreateBuilder(args)` | `Host.CreateApplicationBuilder(args)` | `IHost` |
| ASP.NET Core web host | `DevKitWebApplication.CreateBuilder(args)` | `WebApplication.CreateBuilder(args)` | `WebApplication` |

Both concrete builders must implement `IDevKitApplicationBuilder`. `DevKitApplicationBuilder` must also implement `IDevKitHostApplicationBuilder`, a marker for generic-host builders. Shared feature-owned extensions should target `IDevKitApplicationBuilder` when they truly apply to both host models. Generic-host starter extensions should target `IDevKitHostApplicationBuilder` to avoid colliding with web-specific starter extensions in applications that import both presentation namespaces.

Web-specific extensions must stay on `DevKitWebApplicationBuilder` or a web-specific abstraction. Generic-host extensions must stay on `IDevKitHostApplicationBuilder` or `DevKitApplicationBuilder` when they should not apply to web hosts.

The web builder must not inherit from the generic builder if doing so would leak `HostApplicationBuilder` assumptions into ASP.NET Core startup. The two builders should instead adapt their native hosting model into the same shared DevKit abstraction.

## Implementation Order

This specification should be agreed before extending the current implementation with a non-web builder.

Implementation order:

1. Keep the shared builder abstractions in `Common.Abstractions` hosting-neutral and free of ASP.NET Core types.
2. Add `DevKitApplication` and `DevKitApplicationBuilder` as the generic host adapter.
3. Update package-owned starter extensions so shared extensions work for both generic and web builders where possible.
4. Keep `DevKitWebApplication` as the web adapter and avoid moving non-web behavior into `Presentation.Web`.
5. Add Console Commands builder-level convenience extensions after the generic host adapter exists.

The DevKit CLI and MCP specifications may consume host descriptors produced by eligible running hosts, but this specification does not implement CLI routing, MCP protocol behavior, or local IPC transports.

## DevKitApplication CreateBuilder

Target API:

```csharp
var builder = DevKitApplication.CreateBuilder(args);
```

The returned type must wrap a `HostApplicationBuilder` and expose the common generic host surface:

```csharp
builder.Services
builder.Configuration
builder.Environment
builder.Logging
builder.HostApplicationBuilder
```

The builder must implement `IDevKitApplicationBuilder`:

```csharp
public interface IDevKitApplicationBuilder
{
  IServiceCollection Services { get; }

  IConfiguration Configuration { get; }

  IDevKitHostEnvironment Environment { get; }

  IDictionary<string, object?> Properties { get; }

  IDevKitApplicationBuilder Configure(Action<IDevKitApplicationBuilder> configure);
}

public interface IDevKitHostApplicationBuilder : IDevKitApplicationBuilder
{
}
```

`Build()` returns `IHost`:

```csharp
using var host = builder.Build();
```

The wrapper should remain thin. Existing generic host setup should remain possible after switching only the first line:

```csharp
var builder = DevKitApplication.CreateBuilder(args);

builder.Services.AddHostedService<Worker>();
builder.Logging.AddConsole();

using var host = builder.Build();
await host.RunAsync();
```

## Fluent Starter Extensions

Feature packages should expose DevKit builder extensions from the package that owns the behavior.

Recommended generic host setup:

```csharp
var builder = DevKitApplication.CreateBuilder(args)
  .AddConfiguration()
  .AddLogging()
  .AddModules(modules => modules
    .WithModule<CoreModule>());
```

Console Commands can add a builder-level convenience extension after `DevKitApplicationBuilder` exists:

```csharp
var builder = DevKitApplication.CreateBuilder(args)
  .AddConsoleCommands(commands => commands
    .AddCommand<SampleConsoleCommand>());
```

The builder-level extension is sugar over the existing service registration:

```csharp
builder.Services.AddConsoleCommands(commands => commands
  .AddCommand<SampleConsoleCommand>());
```

The existing service-level API remains the compatibility baseline. Console applications that do not adopt `DevKitApplication` continue to use `Host.CreateApplicationBuilder(args)` and `builder.Services.AddConsoleCommands(...)`.

## Host Builder Compatibility

`DevKitWebApplicationBuilder` currently exposes the ASP.NET Core generic host builder through `DevKitBuilderProperties.HostBuilder`. That works for `WebApplicationBuilder`, but `HostApplicationBuilder` is not an `IHostBuilder`.

Shared starter extensions must therefore not assume `DevKitBuilderProperties.HostBuilder` exists for every `IDevKitApplicationBuilder`.

The generic host adapter should expose native host state through shared property keys without adding concrete hosting types to the common abstraction contract:

```text
DevKitBuilderProperties.HostApplicationBuilder
DevKitBuilderProperties.LoggingBuilder
```

The property keys are strings owned by `Common.Abstractions`; concrete packages decide whether they understand the stored objects.

Starter extensions such as `AddConfiguration()` and `AddLogging()` should support both hosting models by either:

* branching on `HostBuilder` versus `HostApplicationBuilder` properties, or
* moving reusable configuration and logging setup into helper services that can apply to both builder shapes.

If an extension truly requires `IHostBuilder`, it must fail with a clear message on `DevKitApplicationBuilder` rather than silently doing partial setup.

## Local Tooling and Host Descriptors

`DevKitApplication` should use the same local-development policy model as `DevKitWebApplication`, but generic hosts have different capability expectations.

Default rules:

* local tooling and descriptor writing are disabled by default for generic hosts
* local tooling remains disabled outside `Development`
* configuration may disable tooling or individual capabilities
* configuration must not enable tooling outside `Development`
* explicit test-only options may allow non-Development registration for tests
* descriptor writing is useful only for long-running generic hosts, workers and daemons that explicitly opt in
* single-shot console command programs do not need to opt out because they are not advertised by default

Generic hosts may advertise capabilities such as:

| Descriptor metadata | Enabled by | Purpose |
| ------------------- | ---------- | ------- |
| `features.consoleCommands` | Console Commands forwarding capability | Allows `bdk host run ...` to connect over local IPC when the generic host is long-running. |
| `features.mcp` | MCP host capability | Allows `bdk mcp` to connect to app-side MCP handlers when the generic host exposes them. |

This specification does not require the first implementation to support every local IPC capability. It requires the builder shape and options to avoid preventing those capabilities later.

## Options Contract

`DevKitApplication` should have its own options type rather than reusing web-named options.

Conceptual shape:

```csharp
public sealed class DevKitApplicationOptionsBuilder
{
  public DevKitApplicationOptionsBuilder Cli(Action<DevKitCliHostOptionsBuilder> configure);
}
```

The CLI options may share lower-level option objects with `DevKitWebApplication` when those objects are hosting-neutral. Web-specific options must remain in the web package.

Example:

```csharp
var builder = DevKitApplication.CreateBuilder(args, options => options
  .Cli(cli => cli
    .Enabled()));
```

Single-shot command programs use the default disabled local tooling state:

```csharp
var builder = DevKitApplication.CreateBuilder(args, builder => builder
  .AddConfiguration()
  .AddLogging()
  .AddConsoleCommands(commands => commands
    .AddCommand<SampleConsoleCommand>()));
```

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

src/Presentation/Hosting
  DevKitApplication
  DevKitApplicationBuilder
  DevKitApplicationOptions
  DevKitApplicationOptionsBuilder
  DevKitHostEnvironment

src/Presentation.Web/Hosting
  DevKitWebApplication
  DevKitWebApplicationBuilder
  DevKitWebApplicationOptions
  DevKitWebApplicationOptionsBuilder
```

Shared descriptor contracts should stay in `Common.Abstractions/HostDiscovery` when implemented. Shared descriptor writer services should be hosting-neutral if they are usable by both generic and web hosts; web-only endpoint contributors remain in `Presentation.Web`.

Feature-owned fluent extensions should live with their feature package:

```text
src/Presentation.Configuration
  DevKitApplicationBuilderExtensions

src/Presentation.Serilog
  DevKitApplicationBuilderExtensions

src/Presentation
  ConsoleCommands
    DevKitApplicationBuilderExtensions
```

This keeps the generic host foundation open for feature integration while avoiding a central builder that references every DevKit feature package.

## Console Commands Migration Path

Current non-interactive console command setup:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddConsoleCommands(commands =>
{
  commands.AddCommand<SampleConsoleCommand>();
});

using var host = builder.Build();

return await ConsoleCommands.RunAsync(host.Services, args);
```

Future DevKit-aware setup:

```csharp
var builder = DevKitApplication.CreateBuilder(args, builder => builder
  .AddConfiguration()
  .AddLogging()
  .AddConsoleCommands(commands => commands
    .AddCommand<SampleConsoleCommand>()));

using var host = builder.Build();

return await ConsoleCommands.RunAsync(host.Services, args);
```

Interactive web console commands remain web-specific:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
  .AddConsoleCommandsInteractive(commands => commands
    .AddCommand<SeedDataConsoleCommand>());

var app = builder.Build();
app.UseConsoleCommandsInteractive();
```

The interactive Kestrel loop and dashboard-oriented console command surface must not be moved into `DevKitApplication`.

## Testing Strategy

Tests should use existing Presentation test projects. Do not create a new test project only for this host foundation.

Cover:

* `DevKitApplication.CreateBuilder(args)` wraps `Host.CreateApplicationBuilder(args)`
* returned builder implements `IDevKitApplicationBuilder` and `IDevKitHostApplicationBuilder`
* services, configuration, environment and logging remain accessible
* `Build()` returns `IHost`
* `Configure(...)` callbacks run and return the original builder
* shared properties include generic-host-specific state without requiring ASP.NET Core types
* starter extensions either work on both web and generic builders or fail with clear messages
* raw `Host.CreateApplicationBuilder(args)` plus service-level registrations remains supported

## Acceptance Criteria

The specification is satisfied when:

* non-web DevKit apps have a named `DevKitApplication.CreateBuilder(args)` design
* web and non-web builders are documented as siblings over different Microsoft hosting primitives
* both builders share `IDevKitApplicationBuilder` without exposing ASP.NET Core types from common abstractions
* package-owned extensions can target the shared abstraction where possible
* Console Commands have a clear migration path from service-level registration to builder-level fluent setup
* raw `Host.CreateApplicationBuilder(args)` remains supported
* no implementation forces generic host applications through `Presentation.Web`

## Related Specifications

* [DevKit Web Application Host](spec-presentation-devkit-web-host.md)
* [DevKit CLI](spec-presentation-cli.md)
* [DevKit STDIO MCP](spec-presentation-web-mcp-diagnostics.md)
