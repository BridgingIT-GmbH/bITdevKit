# Metrics

> Record devkit and application measurements through the standard .NET metrics runtime.

[TOC]

## Overview

Metrics provides one optional `IMetricsService` for counters, current values, histograms, gauges, and timed operations. The implementation writes instruments to the `bdk` .NET `Meter`, so the host can collect them with `System.Diagnostics.Metrics` listeners or OpenTelemetry.

The web integration can also track ASP.NET Core requests and expose in-process JSON snapshots. These endpoints support local diagnostics and the devkit dashboard. They are not an OTLP or Prometheus endpoint.

## Challenges

Application features often need the same operational measurements but use different naming, timing, and failure-handling code. Direct use of `Meter` also makes each caller responsible for reusing instruments safely.

Instrumentation must not interrupt the operation that it measures. A missing listener, a failed meter factory, or a conflicting instrument definition must not fail a message handler or storage request.

Developers also need a small local view of current measurements without configuring an external collector for every development host.

## Solution

`AddMetrics(...)` registers a singleton `IMetricsService`. `MetricsService` owns or obtains the `bdk` meter, normalizes series names, reuses instruments across callers, and isolates non-fatal recording errors.

Higher-level devkit features resolve the service optionally. When metrics is not registered, those features keep working without measurements.

For web hosts, `UseRequestMetrics()` records request totals, active requests, status groups, latency, and route-level values. `AddMetricsEndpoints(...)` adds JSON snapshots for devkit, .NET runtime, and ASP.NET Core measurements.

## Key Features

- Standard .NET `Meter`, counter, up/down counter, histogram, and observable gauge instruments
- Shared meter name `bdk`
- Convenience methods for totals, failures, current values, durations, and tracked scopes
- Direct instrument methods with `MetricTag` dimensions
- Optional built-in metrics for requester, notifier, messaging, queueing, jobs, orchestrations, repositories, storage, and composition
- ASP.NET Core request and route measurements
- JSON discovery and snapshot endpoints
- Dashboard projection when the devkit dashboard is enabled
- No-op behavior after disposal or when the meter is unavailable

## Architecture

The metrics feature has three layers:

1. `Metrics` defines the meter name and the series naming rules.
2. `IMetricsService` and `MetricsService` create and record instruments.
3. The Presentation.Web integration listens to measurements, tracks HTTP requests, and maps snapshot endpoints.

`AddMetrics(...)` registers the shared service only when `MetricsOptions.Enabled` is `true`. The registration also adds web snapshot services when the `BridgingIT.DevKit.Presentation.Web` assembly is available.

`AddMetricsEndpoints(...)` registers `MetricsEndpoints`. The normal `MapEndpoints()` call maps those endpoints. `UseRequestMetrics()` adds `RequestMetricsMiddleware` to collect ASP.NET Core measurements.

The snapshot services hold measurements in the current process. An application restart clears the snapshot state. External retention and aggregation belong to the configured metrics backend.

## Use Cases

Use Metrics when you need to:

- compare success, failure, concurrency, and latency across devkit features;
- add a small set of application-specific measurements;
- inspect local runtime and request measurements through JSON or the dashboard;
- export the `bdk` meter through an existing OpenTelemetry pipeline.

Use raw `System.Diagnostics.Metrics` APIs when the application needs instrument types or callbacks that `IMetricsService` does not expose. Use an external metrics backend for alerts, cross-node queries, and long-term retention.

Do not put entity IDs, message IDs, file names, email addresses, or other unbounded values in metric names or tags. Each distinct value can create another time series in the collector.

## Basic Usage

The following web host records one application operation and exposes local snapshot endpoints:

```csharp
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMetricsEndpoints(options => options
	.RequireAuthorization(false)); // Local development only.

var app = builder.Build();

app.UseRequestMetrics();

app.MapGet("/inventory/refresh", (IMetricsService metrics) =>
{
	using var operation = metrics.Track("jobs", "inventory_refresh");
	return Results.Ok(new { refreshed = true });
});

app.MapEndpoints();
app.Run();
```

Call the application endpoint, then read the devkit snapshot:

```powershell
Invoke-RestMethod http://localhost:5000/inventory/refresh
Invoke-RestMethod http://localhost:5000/_bdk/api/metrics/bdk
```

The first response contains `refreshed = true`. The second response contains a `jobs` feature with the normalized `jobs_inventory_refresh` series.

Keep metrics endpoints authorized outside a local development environment. `MetricsEndpointsOptions` requires authorization by default.

## Registration

Register only the shared service when the host exports measurements through another pipeline:

```csharp
builder.Services.AddMetrics(options => options.Enabled());
```

Register the service and the default web endpoints together:

```csharp
builder.Services.AddMetrics(options => options
	.Enabled()
	.AddEndpoints());
```

The second form uses the default endpoint options. Call `AddMetricsEndpoints(...)` separately when you need to configure the endpoint group, authorization, or individual snapshots.

Disable all devkit metric registration with `.Enabled(false)`. This leaves `IMetricsService` unregistered. Devkit components that treat metrics as optional continue without emitting measurements.

## Recording measurements

`Track(...)` combines a total counter, a current-value up/down counter, and a duration histogram:

```csharp
public sealed class InventoryImportService(IMetricsService metrics)
{
	public async Task ImportAsync(CancellationToken cancellationToken)
	{
		using var operation = metrics.Track("inventory_import", "warehouse_a");

		try
		{
			await ImportCoreAsync(cancellationToken);
		}
		catch
		{
			metrics.IncrementFailure("inventory_import", "warehouse_a");
			throw;
		}
	}
}
```

The convenience methods use these suffixes:

- `Increment(...)` records the base series.
- `IncrementFailure(...)` adds `_failure`.
- `ChangeCurrent(...)` adds `_current`.
- `RecordDuration(...)` adds `_duration` and records milliseconds.

Use the direct methods when a stable instrument name needs bounded dimensions:

```csharp
MetricTag[] tags =
[
	new("operation", "import"),
	new("outcome", "success")
];

metrics.AddCounter("inventory_import_outcomes", tags: tags);
metrics.RecordHistogram("inventory_import_items", 25, "{item}", tags);
metrics.SetGauge("inventory_pending_items", 4);
```

The direct API also provides `AddUpDownCounter(...)`, `StartTimestamp()`, and `RecordHistogramDuration(...)`.

## Built-in feature metrics

Several devkit packages provide metrics behaviors. Register `IMetricsService`, then add the behavior through the owning feature builder:

```csharp
builder.Services.AddMetrics(options => options.Enabled());

builder.Services.AddMessaging(builder.Configuration)
	.WithBehavior<MetricsMessagePublisherBehavior>()
	.WithBehavior<MetricsMessageHandlerBehavior>();

builder.Services.AddQueueing(builder.Configuration)
	.WithBehavior<MetricsQueueEnqueuerBehavior>()
	.WithBehavior<MetricsQueueHandlerBehavior>();
```

The exact behavior registration belongs to each feature. See [Messaging](./features-messaging.md), [Queueing](./features-queueing.md), [Jobs](./features-jobs.md), [Orchestrations](./features-orchestrations.md), and [Domain Repositories](./features-domain-repositories.md).

## HTTP endpoints

The default group path is `/_bdk/api/metrics`. It requires authorization and maps these routes:

- `GET /_bdk/api/metrics` returns links to the enabled snapshots.
- `GET /_bdk/api/metrics/bdk` returns measurements from the `bdk` meter.
- `GET /_bdk/api/metrics/overview` returns the dashboard-oriented summary.
- `GET /_bdk/api/metrics/dotnet` returns process, GC, memory, and thread-pool values.
- `GET /_bdk/api/metrics/aspnet` returns aggregate HTTP request values.
- `GET /_bdk/api/metrics/aspnet/routes` returns route-level HTTP values.

The devkit snapshot groups known devkit series prefixes such as `requester_`, `messaging_`, `jobs_`, `broadcasting_`, and `blobstorage_`. A custom series with another prefix still reaches .NET listeners and OpenTelemetry, but the devkit JSON snapshot does not include it.

Configure the endpoint set with `MetricsEndpointsOptionsBuilder`:

```csharp
builder.Services.AddMetricsEndpoints(options => options
	.EnableApp()
	.EnableOverview()
	.EnableDotNet()
	.EnableAspNet()
	.RequireAuthorization());
```

Call `UseRequestMetrics()` before the endpoints that you want to measure. Set `RouteMetricsEnabled` to `false`, or omit the middleware, when the host must expose snapshots without collecting request measurements.

## OpenTelemetry export

Subscribe to the `bdk` meter in the host's OpenTelemetry configuration:

```csharp
builder.Services.AddOpenTelemetry()
	.WithMetrics(metrics => metrics
		.AddMeter("bdk")
		.AddRuntimeInstrumentation()
		.AddAspNetCoreInstrumentation());
```

Add the required exporter in the host. The devkit does not select an exporter or collector endpoint.

## Related documentation

- [Common Utilities](./common-utilities.md) contains the low-level metrics API reference.
- [Presentation Endpoints](./features-presentation-endpoints.md) explains endpoint discovery and mapping.
- [Presentation Dashboard](./features-presentation-dashboard.md) explains dashboard registration and authorization.
- [Profiling](./features-profiling.md) covers bounded diagnostic snapshots rather than continuous operational measurements.
