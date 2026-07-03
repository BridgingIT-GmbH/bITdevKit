# Presentation Host Feature Documentation

> Configure web and generic host applications through DevKit application builders and package-owned starter extensions.

[TOC]

## Overview

The Presentation Host feature provides the recommended application startup path for hosts that want a DevKit fluent setup experience. It introduces `DevKitWebApplication.CreateBuilder(args)` for ASP.NET Core web hosts and `DevKitApplication.CreateBuilder(args)` for non-web generic hosts such as console applications, workers and daemons.

Use this setup for new web applications that should be discoverable by future DevKit tooling, CLI commands, MCP diagnostics and host descriptors. Existing applications can continue to use `WebApplication.CreateBuilder(args)`; that path remains supported, but it does not opt into DevKit host metadata or local CLI tooling by convention.

Use `DevKitApplication.CreateBuilder(args)` for non-web applications that would otherwise start from `Host.CreateApplicationBuilder(args)` but still want the same DevKit starter extensions.

### Challenges

- Startup code can become repetitive when every host wires configuration, logging and modules manually.
- CLI and diagnostics tooling need a consistent host-side entry point without forcing every application to adopt custom infrastructure.
- Shared builder abstractions must stay clean and avoid leaking ASP.NET Core types into common packages.
- Feature packages need a place to contribute fluent setup extensions without making `Presentation.Web` depend on every feature package.
- Existing applications using `WebApplicationBuilder` must keep working.

### Solution

- `DevKitWebApplication.CreateBuilder(args)` creates the normal ASP.NET Core builder and wraps it in `DevKitWebApplicationBuilder`.
- `DevKitApplication.CreateBuilder(args)` creates the normal generic host builder and wraps it in `DevKitApplicationBuilder`.
- `DevKitWebApplicationBuilder` implements `IDevKitApplicationBuilder`, exposing services, configuration, a DevKit-owned environment abstraction and a shared properties bag.
- `DevKitApplicationBuilder` implements `IDevKitHostApplicationBuilder`, a generic-host marker over `IDevKitApplicationBuilder` used by non-web starter extensions.
- The wrapped `WebApplicationBuilder`, `Host`, `WebHost`, `Logging`, `Environment` and `Build()` APIs remain available for web-specific setup.
- Package-owned starter extensions add fluent setup:
  - `AddConfiguration()` from `Presentation.Configuration`.
  - `AddLogging()` from `Presentation.Serilog`.
  - `AddModules(...)` from `Presentation.Configuration`, using the existing module builder callbacks.
- The builder stores the generic host builder in `DevKitBuilderProperties.HostBuilder`, so starter extensions can call host-level setup without making `Presentation.Web` depend on configuration or logging packages.

## Packages

Use the packages that match the startup features your host needs.

| Package | Responsibility |
| ------- | -------------- |
| `BridgingIT.DevKit.Presentation` | Provides `DevKitApplication`, `DevKitApplicationBuilder` and non-web Console Command starter extensions. |
| `BridgingIT.DevKit.Presentation.Web` | Provides `DevKitWebApplication`, `DevKitWebApplicationBuilder` and web-host local tooling options. |
| `BridgingIT.DevKit.Common.Abstractions` | Provides `IDevKitApplicationBuilder`, `IDevKitHostApplicationBuilder`, `IDevKitHostEnvironment` and shared builder property keys. |
| `BridgingIT.DevKit.Presentation.Configuration` | Provides `AddConfiguration()` and `AddModules(...)` starter extensions. |
| `BridgingIT.DevKit.Presentation.Serilog` | Provides `AddLogging()` starter extension. |
| `BridgingIT.DevKit.Common.Modules` | Provides module registration and `WithModule<T>()`. |

## Core Concepts

### DevKitWebApplication

`DevKitWebApplication` is the web host entry point for new DevKit-aware applications.

```csharp
var builder = DevKitWebApplication.CreateBuilder(args);
```

Internally this still creates a normal ASP.NET Core `WebApplicationBuilder`, so existing web setup remains familiar.

### DevKitApplication

`DevKitApplication` is the generic host entry point for non-web DevKit applications.

```csharp
var builder = DevKitApplication.CreateBuilder(args);
```

Internally this still creates a normal `HostApplicationBuilder`, so console apps, workers and daemons can keep the generic host model while using DevKit starter extensions.

### DevKitWebApplicationBuilder

`DevKitWebApplicationBuilder` is a wrapper around the ASP.NET Core builder. It exposes the common DevKit builder contract and web-specific APIs.

```csharp
var builder = DevKitWebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Host.ConfigureServices((context, services) =>
{
    // Generic host setup remains available.
});

var app = builder.Build();
```

Important members:

- `Services`: the host service collection.
- `Configuration`: the host configuration manager.
- `Environment`: the ASP.NET Core web host environment.
- `Host`: the generic host builder.
- `WebHost`: the web host builder.
- `Logging`: the ASP.NET Core logging builder.
- `WebApplicationBuilder`: the wrapped ASP.NET Core builder for advanced integration points.
- `Properties`: shared extension state used by feature packages.
- `Options`: DevKit web application options.
- `LocalToolingDecision`: the evaluated local tooling decision for the host.

### IDevKitApplicationBuilder

`IDevKitApplicationBuilder` is the common abstraction for feature-owned builder extensions. It deliberately avoids ASP.NET Core concrete types.

```csharp
public interface IDevKitApplicationBuilder
{
    IServiceCollection Services { get; }
    IConfiguration Configuration { get; }
    IDevKitHostEnvironment Environment { get; }
    IDictionary<string, object> Properties { get; }
    IDevKitApplicationBuilder Configure(Action<IDevKitApplicationBuilder> configure);
}
```

Use this abstraction when writing extensions that only need services, configuration, environment metadata or shared builder properties.

### IDevKitHostApplicationBuilder

`IDevKitHostApplicationBuilder` marks DevKit builders backed by the generic host application model. Non-web starter extensions use this marker so they do not collide with web-specific starter extensions when both `BridgingIT.DevKit.Presentation` and `BridgingIT.DevKit.Presentation.Web` are imported.

```csharp
public interface IDevKitHostApplicationBuilder : IDevKitApplicationBuilder
{
}
```

### Shared Builder Properties

Feature packages can coordinate through `IDevKitApplicationBuilder.Properties` using well-known keys from `DevKitBuilderProperties`.

Current keys include:

- `ApplicationName`
- `ContentRootPath`
- `HostBuilder`
- `HostApplicationBuilder`
- `LoggingBuilder`
- `WorkspacePath`

`DevKitWebApplicationBuilder` sets `HostBuilder` to the wrapped ASP.NET Core `ConfigureHostBuilder`. Configuration and logging starter extensions use this property to call the existing host builder extension methods without adding new dependencies to `Presentation.Web`.

`DevKitApplicationBuilder` sets `HostApplicationBuilder` and `LoggingBuilder` for generic-host starter extensions.

## Recommended Setup

For a new web host, start with the DevKit builder and add starter extensions for configuration, logging and modules.

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddConfiguration()
    .AddLogging()
    .AddModules(modules => modules
        .WithModule<CoreModule>());

builder.Services.AddRequester()
    .AddHandlers();

var app = builder.Build();

app.MapEndpoints();
app.Run();
```

This replaces the older explicit setup:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration();
builder.Host.ConfigureLogging(builder.Configuration);

builder.Services.AddModules(builder.Configuration, builder.Environment)
    .WithModule<CoreModule>();
```

Both styles remain valid. Prefer the DevKit builder for new applications that should be prepared for DevKit CLI and host tooling.

### Non-Web Console Application

For a new non-web host, start with `DevKitApplication.CreateBuilder(args)`. This wraps `Host.CreateApplicationBuilder(args)` and gives console applications the same fluent DevKit setup style as web applications.

Reference the packages for the startup features the console application needs:

- `BridgingIT.DevKit.Presentation` for `DevKitApplication` and `AddConsoleCommands(...)`.
- `BridgingIT.DevKit.Presentation.Configuration` for `AddConfiguration()` and `AddModules(...)`.
- `BridgingIT.DevKit.Presentation.Serilog` for `AddLogging()`.
- `BridgingIT.DevKit.Common.Modules` when the console application composes modules.

Use this shape for a single-shot console command application:

```csharp
var builder = DevKitApplication.CreateBuilder(args, builder => builder
    .AddConfiguration()
    .AddLogging()
    .AddConsoleCommands(commands => commands
        .WithCommand<SampleConsoleCommand>()));

using var host = builder.Build();

return await ConsoleCommands.RunAsync(host.Services, args);
```

`DevKitApplication` does not enable descriptor writing or local CLI host advertisement by default. Single-shot command applications can therefore use the fluent builder without explicitly disabling CLI integration.

If the console application uses modules, add them before building the host:

```csharp
var builder = DevKitApplication.CreateBuilder(args, builder => builder
    .AddConfiguration()
    .AddLogging()
    .AddModules(modules => modules
        .WithModule<CoreModule>())
    .AddConsoleCommands(commands => commands
        .WithCommand<SampleConsoleCommand>()));
```

For a long-running worker or daemon, keep the same builder but run the host instead of invoking `ConsoleCommands.RunAsync(...)` directly. Register Console Commands only when the process should expose command implementations through DI for future local forwarding or internal execution.

```csharp
var builder = DevKitApplication.CreateBuilder(args, options => options
    .Cli(cli => cli.Enabled()))
    .AddConfiguration()
    .AddLogging()
    .AddConsoleCommands(commands => commands
        .WithCommand<MaintenanceConsoleCommand>());

builder.Services.AddHostedService<Worker>();

using var host = builder.Build();
await host.RunAsync();
```

Use `Cli(cli => cli.Enabled())` only for long-running generic hosts that should participate in future local host discovery or command forwarding.

The existing raw generic host style remains supported for applications that do not need DevKit fluent setup:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddConsoleCommands(commands => commands
    .WithCommand<SampleConsoleCommand>());
```

## Starter Extensions

### AddConfiguration

`AddConfiguration()` configures the host using the existing `ConfigureAppConfiguration(...)` extension from `Presentation.Configuration`.

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddConfiguration();
```

You can pass an environment override when needed.

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddConfiguration(environment: "Development");
```

### AddLogging

`AddLogging()` configures Serilog using the existing host builder logging extension from `Presentation.Serilog`.

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddLogging();
```

Optional parameters mirror the existing logging setup.

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddLogging(
        exclusionPatterns: ["RequestPath like '/internal%'"],
        selfLogEnabled: false,
        registerLogCommands: true);
```

When `registerLogCommands` is enabled, runtime log level console commands are registered by the logging package.

### AddModules

`AddModules(...)` starts module registration through the existing module builder context.

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddModules(modules => modules
        .WithModule<CoreModule>()
        .WithModule<ReportingModule>());
```

The callback uses the same `ModuleBuilderContext` and `WithModule<T>()` APIs documented in [Modules](./features-modules.md).

### AddConsoleCommands

`AddConsoleCommands(...)` registers non-interactive Console Commands for generic host applications through the existing Console Commands feature.

```csharp
var builder = DevKitApplication.CreateBuilder(args, builder => builder
    .AddConsoleCommands(commands => commands
    .WithCommand<SampleConsoleCommand>()));
```

The method is a builder-level convenience over the service-level registration:

```csharp
builder.Services.AddConsoleCommands(commands => commands
    .WithCommand<SampleConsoleCommand>());
```

Use `ConsoleCommands.RunAsync(host.Services, args)` when the process should execute one command and exit. Use `host.RunAsync()` for long-running workers.

## Local Tooling Policy

The DevKit web builder evaluates local tooling during builder creation. The decision is available through `DevKitWebApplicationBuilder.LocalToolingDecision` and is also stored in the builder properties using `DevKitWebApplicationBuilderProperties.LocalToolingDecision`.

Current web policy:

- Local tooling is enabled only in the `Development` environment.
- Options can disable tooling or individual capabilities.
- Configuration can disable tooling or individual capabilities.
- Configuration cannot enable local tooling outside `Development`.

Example:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args, options => options
    .Cli(cli => cli
        .ConsoleCommands(true)
        .Mcp(false)));
```

When local tooling is eligible, the web builder registers host descriptor writing, descriptor cleanup, endpoint contributors and local named-pipe IPC services. Descriptor files are written to the OS user-local `bdk/hosts/runtimes` registry at host startup and removed during graceful shutdown when possible.

At debug level, DevKit writes startup diagnostics with the host kind, application name, environment, content root, descriptor eligibility and advertised feature flags. The descriptor writer also logs the descriptor path and contributed feature names when it writes or removes a descriptor.

The current host-advertised feature metadata is:

- `features.consoleCommands` when local tooling is eligible, Console Command forwarding is enabled and Console Commands are registered.
- `features.mcp` when local tooling is eligible, MCP is enabled and app-side `IMcpHandler` registrations exist.

Both features advertise local named-pipe endpoints with per-process nonces. The Console Command endpoint accepts `ping` and `run` operations and executes `run` through the existing `ConsoleCommandExecutor`, returning captured Spectre.Console output to the caller. The nonce is for local stale/spoofed descriptor protection only; it is not production authentication.

`DevKitApplication` uses the same option shape, but generic hosts keep local CLI integration disabled by default. This avoids descriptor writing for normal console applications. Long-running workers can opt in explicitly:

```csharp
var builder = DevKitApplication.CreateBuilder(args, options => options
    .Cli(cli => cli.Enabled()));
```

## Migrating an Existing Host

Use this path when moving an application from raw `WebApplicationBuilder` to the DevKit builder.

### Before

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration();
builder.Host.ConfigureLogging(builder.Configuration);

builder.Services.AddModules(builder.Configuration, builder.Environment)
    .WithModule<CoreModule>();
```

### After

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddConfiguration()
    .AddLogging()
    .AddModules(modules => modules
        .WithModule<CoreModule>());
```

Then keep the rest of the service registration and middleware pipeline unchanged unless the host needs additional DevKit-specific options.

### Compatibility Guidance

- Keep using `WebApplication.CreateBuilder(args)` for applications that do not need DevKit host tooling.
- Use `DevKitWebApplication.CreateBuilder(args)` for new applications and apps that should participate in DevKit CLI or diagnostics flows.
- Keep using `Host.CreateApplicationBuilder(args)` for non-web applications that do not need DevKit fluent setup.
- Use `DevKitApplication.CreateBuilder(args)` for new console apps, workers and daemons that should share DevKit builder conventions.
- Do not move package-specific setup into `Presentation.Web`. Feature packages should own their fluent extensions.
- Prefer extensions on `IDevKitApplicationBuilder` when the extension does not need concrete ASP.NET Core APIs.
- Prefer `IDevKitHostApplicationBuilder` for generic-host starter extensions that should not apply to web hosts.
- Use the wrapped `WebApplicationBuilder` only for advanced ASP.NET Core integration points that cannot be expressed through the common builder contract.

## Writing Feature-Owned Builder Extensions

Feature packages can add fluent setup without changing `Presentation.Web`.

```csharp
namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

public static class MyFeatureDevKitWebApplicationBuilderExtensions
{
    public static TBuilder WithMyFeature<TBuilder>(this TBuilder builder)
        where TBuilder : IDevKitApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddMyFeature(builder.Configuration);

        return builder;
    }
}
```

If an extension needs the generic host builder, read it from the shared property bag.

```csharp
private static IHostBuilder GetHostBuilder(IDevKitApplicationBuilder builder)
{
    if (builder.Properties.TryGetValue(DevKitBuilderProperties.HostBuilder, out var value) &&
        value is IHostBuilder hostBuilder)
    {
        return hostBuilder;
    }

    throw new InvalidOperationException("The DevKit builder does not expose a generic host builder.");
}
```

Use the `BridgingIT.DevKit.Presentation.Web` namespace for web-host starter extensions so consuming applications that already import `Presentation.Web` can discover them naturally.

## Example: Commerce Host

A fictional ecommerce host demonstrates the recommended startup shape.

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddConfiguration()
    .AddLogging()
    .AddModules(modules => modules
        .WithModule<CoreModule>());
```

The application still configures requesters, notifiers, storage, jobs, endpoints and middleware through the existing APIs after the builder is created.

## Troubleshooting

### Extension Methods Are Not Found

Check the host references and namespaces:

- `AddConfiguration()` and `AddModules(...)` require `Presentation.Configuration`.
- `AddLogging()` requires `Presentation.Serilog`.
- `AddConsoleCommands(...)` on `DevKitApplication` requires `Presentation`.
- `DevKitApplication` requires `Presentation`.
- `DevKitWebApplication` requires `Presentation.Web`.
- Generic host starter extensions are exposed from the `BridgingIT.DevKit.Presentation` namespace.
- Web starter extensions are exposed from the `BridgingIT.DevKit.Presentation.Web` namespace.

### Local Tooling Is Disabled

Check the evaluated environment and configuration:

- The host must run in `Development`.
- `DevKit:Cli:Enabled` must not be `false`.
- Capability-specific configuration such as `DevKit:Cli:ConsoleCommands` or `DevKit:Cli:Mcp` must not be `false` when that capability is needed.

### Avoid Dependency Churn

Do not add broad feature package references to `Presentation.Web` just to expose starter methods. Put fluent extensions in the package that owns the behavior and target `IDevKitApplicationBuilder` where possible.

## Related Documentation

- [Modules](./features-modules.md)
- [DevKit CLI](./features-cli.md)
- [DevKit MCP](./features-cli-mcp.md)
- [Console Commands](./features-presentation-console-commands.md)
- [Presentation Endpoints](./features-presentation-endpoints.md)
- [DevKit Host Specification](./specs/spec-presentation-devkit-web-host.md)
