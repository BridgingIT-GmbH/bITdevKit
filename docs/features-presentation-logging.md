# Presentation Logging

> Configure Serilog once for a devkit host and change its global minimum level at runtime.

[TOC]

## Overview

Presentation logging configures Serilog as the host logging provider. It reads sinks, enrichers, and level settings from application configuration, applies devkit noise filters, and creates a `LoggingLevelSwitch` for runtime level changes.

The feature can also forward log events to an OTLP endpoint and register Console Commands for inspecting or changing the active minimum level.

This feature configures live log emission. [Log Entries](./features-log-entries.md) is the separate persisted-log query and maintenance feature.

## Challenges

ASP.NET Core and worker hosts need the same structured logging setup. If each host configures providers independently, filters, sinks, minimum levels, and OpenTelemetry forwarding drift.

Developers also need to raise or lower logging for a running process without restarting it. The change must affect the same minimum-level control used by the configured Serilog pipeline.

Framework health requests and telemetry transport messages can overwhelm local logs. These known sources need stable filters before host-specific exclusions are added.

## Solution

`ConfigureLogging(...)` builds a Serilog `LoggerConfiguration`, reads the `Serilog` configuration section, and replaces the existing Microsoft logging providers with Serilog.

The extension creates one `LoggingLevelSwitch`. `LogLevelManager` reads and changes that switch, while the `loglevel` Console Commands expose list, get, and set operations.

`AddLogging()` and `WithLogging()` apply the same setup to `DevKitWebApplication` and `DevKitApplication` builders.

## Key Features

- Serilog configuration through `appsettings.json` and other `IConfiguration` providers
- Replacement of the default Microsoft logging providers
- Global minimum level controlled by `LoggingLevelSwitch`
- Runtime `loglevel list`, `loglevel get`, and `loglevel set` commands
- Default filters for health and telemetry noise
- Additional Serilog expression filters supplied by the host
- Optional Serilog self-log output to `Console.Error`
- Optional OTLP log forwarding
- Configuration-based and code-based logger overloads
- Guard against replacing an already configured global Serilog logger

## Architecture

The logging path has four parts:

1. A host or devkit builder calls `ConfigureLogging(...)` or `AddLogging()`.
2. `LoggerConfiguration` reads the host configuration and creates the Serilog logger.
3. `LoggingLevelSwitch` controls the global minimum level and is stored in `LogLevelSwitchProvider`.
4. `LogLevelManager` and the optional Console Commands use that same switch.

The setup runs only while `Serilog.Log.Logger` is the Serilog `SilentLogger`. This prevents a later devkit call from replacing a logger that application bootstrap code already configured.

The configuration-based overloads skip logger setup during build-time OpenAPI generation. This avoids starting normal logging infrastructure for that tool path.

## Use Cases

Use Presentation logging when a web or generic host wants the repository's Serilog defaults, runtime log-level commands, and optional OTLP forwarding.

Use the configuration overload for normal applications. Use the custom action overload when the host must define every sink and enricher in code.

Use [Log Entries](./features-log-entries.md) when the application must persist logs and query them through an application service, endpoints, or the dashboard. Serilog configuration alone does not add persisted log storage.

## Basic Usage

Add a Serilog section to the host configuration:

```json
{
	"Serilog": {
		"MinimumLevel": {
			"Default": "Information",
			"Override": {
				"Microsoft": "Warning"
			}
		},
		"WriteTo": [
			{
				"Name": "Console"
			}
		]
	}
}
```

Configure a devkit web host and write a structured log event:

```csharp
using BridgingIT.DevKit.Presentation.Web;

var builder = DevKitWebApplication.CreateBuilder(args)
	.AddConfiguration()
	.AddLogging();

var app = builder.Build();

app.MapGet("/inventory/{id}", (string id, ILogger<Program> logger) =>
{
	logger.LogInformation("Inventory item {InventoryItemId} requested", id);
	return Results.Ok(new { id });
});

app.Run();
```

A request to `/inventory/42` returns `{ "id": "42" }` and writes an Information event with `InventoryItemId` set to `42`.

## Standard host registration

For a standard `WebApplicationBuilder`, configure the generic host:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration();
builder.Host.ConfigureLogging(builder.Configuration);
```

Pass extra Serilog expression filters when the host has noisy routes or sources:

```csharp
builder.Host.ConfigureLogging(
	builder.Configuration,
	exclusionPatterns:
	[
		"RequestPath like '/internal/poll%'",
		"SourceContext = 'Inventory.NoisyClient'"
	]);
```

The devkit always adds these filters before host filters:

- `RequestPath like '/health%'`;
- `RequestPath like '/api/events/raw'`;
- `StartsWith(@Message, 'Execution attempt. Source')`.

The filters use Serilog.Expressions syntax. An invalid expression fails logger creation.

## Custom logger configuration

Use the action overload when configuration should not define the Serilog pipeline:

```csharp
builder.Host.ConfigureLogging(
	configuration => configuration
		.Enrich.FromLogContext()
		.WriteTo.Console(),
	logEventLevel: LogEventLevel.Information);
```

This overload does not call `ReadFrom.Configuration(...)` and does not add the default devkit exclusion filters. The callback must configure the required sinks and enrichers.

## Runtime log levels

The configuration overload reads the initial level from `Serilog:MinimumLevel:Default`. If the value is missing or does not parse as `LogEventLevel`, the switch starts at `Debug`.

When `registerLogCommands` is `true`, logging registers these commands:

- `loglevel list` lists the Serilog levels;
- `loglevel get` shows the current level;
- `loglevel set <name>` changes the level.

`log` is an alias for the `loglevel` group. The commands become reachable through a configured [Console Commands](./features-presentation-console-commands.md) host.

The change is process-local and lasts until the process stops or another command changes it. The feature does not write the new level back to configuration.

Application code can also use `LogLevelManager`:

```csharp
var manager = new LogLevelManager(LogLevelSwitchProvider.GetControlSwitch());
manager.SetLevel(LogEventLevel.Warning);
```

`LogLevelManager.SetLevel(string)` parses names without case sensitivity and throws `ArgumentException` for an invalid name.

## OpenTelemetry forwarding

When `OTEL_EXPORTER_OTLP_ENDPOINT` has a value, the configuration-based setup adds the Serilog OpenTelemetry sink:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=https://collector.example.com:4317
OTEL_EXPORTER_OTLP_HEADERS=api-key=secret,tenant=inventory
OTEL_RESOURCE_ATTRIBUTES=service.name=inventory-api,deployment.environment=production
```

The sink starts with `service.name=presentation-web-server`. A `service.name` entry in `OTEL_RESOURCE_ATTRIBUTES` replaces that value.

Both header and resource-attribute settings accept comma-separated `key=value` pairs. Empty settings are allowed. A pair without a non-empty key and value throws during logger setup.

The `IHostBuilder.ConfigureLogging(...)` overload adds the OTLP sink only when its optional `configuration` argument is supplied. `AddLogging()` supplies the devkit builder configuration.

Do not put secrets directly in committed settings. Supply OTLP headers through a protected deployment source.

## Self-log diagnostics

Set `selfLogEnabled` to write Serilog internal diagnostic messages to `Console.Error`:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
	.AddLogging(selfLogEnabled: true);
```

Use this while diagnosing sink or configuration problems. Serilog self-log output bypasses the configured logger and may be noisy.

## Existing logger behavior

The feature checks the global `Log.Logger` type before configuration. If another bootstrap path already replaced `SilentLogger`, the devkit does not replace that logger, clear providers, create a level switch, or register the log-level commands.

Configure one owner for the global logger. If the application creates a bootstrap logger before `AddLogging()`, keep the complete Serilog setup in that bootstrap path.

## Related documentation

- [Presentation Host](./features-presentation.md) covers devkit builder composition.
- [Presentation configuration](./features-presentation-configuration.md) covers the provider sequence used to load Serilog settings.
- [Console Commands](./features-presentation-console-commands.md) covers command registration and invocation.
- [Log Entries](./features-log-entries.md) covers persisted log access and maintenance.
- [Presentation Correlation IDs](./features-presentation-correlationid.md) covers correlation values in structured logs.
