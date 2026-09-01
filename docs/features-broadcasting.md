# Broadcasting

> Send a short-lived typed notification to every active application node in one or more scopes.

[TOC]

## Overview

Broadcasting distributes immediate control notifications across the currently registered nodes of an application. Each node registers the scopes that it serves. A publisher selects active nodes in the target scopes and sends the same typed payload to each node.

The feature is best effort. It does not store broadcasts for later delivery, replay missed broadcasts, or provide queue semantics. Use it for events such as cache invalidation, configuration refresh, and local operational probes where stale nodes can recover through their normal read path.

## Challenges

Multi-node applications sometimes need every live process to react to the same small notification. A normal competing-consumer queue sends a message to one consumer, not every node.

Static node lists become stale as processes start, stop, or move. Direct HTTP calls also leave each application to implement discovery, bounded fan-out, timeouts, duplicate protection, payload validation, and receiver authentication.

A reusable module may need to contribute a handler or a scope without taking ownership of the complete host configuration.

## Solution

`AddBroadcasting(...)` creates one shared runtime for the host. Repeated calls reopen the same registration and combine scopes and handlers.

`IBroadcastService.PublishAsync(...)` reads an immutable target snapshot from `IBroadcastRegistryStore`, creates one envelope, and delivers it to each selected node. The local dispatcher gives each payload type a bounded handler queue. The receiver checks the envelope lifetime, payload size, target scope, payload type, and duplicate ID before admission.

The default in-memory registry and local-only transport support one process. Multi-node hosts can select the Entity Framework registry and the HTTP transport.

## Key Features

- Typed payloads and one `IBroadcastHandler<TBroadcast>` per payload type
- Case-insensitive node identities and scopes
- A `default` scope when registration and publication omit scopes
- Re-entrant host configuration across modules
- Bounded concurrent delivery and per-node timeouts
- Per-handler bounded queues
- Payload lifetime and size limits
- Duplicate ID retention on each receiving node
- In-memory and Entity Framework registry providers
- Local-only and HTTP transports
- Built-in shared-secret or custom HTTP receiver authentication
- Correlation ID propagation
- Metrics, diagnostics, dashboard integration, and console commands

## Architecture

Broadcasting separates discovery, transport, admission, and handler execution:

1. `BroadcastNodeLifecycleService` registers the current process and its scopes.
2. `IBroadcastRegistryStore` returns the active target nodes for a publication.
3. `IBroadcastService` serializes one envelope and calls `IBroadcastTransport` for each target.
4. `IBroadcastReceiver` validates and deserializes the envelope.
5. `IBroadcastLocalDispatcher` admits the payload to the bounded queue for its registered handler.
6. The handler runs in a dependency injection scope created for that delivery.

The in-memory registry is process-local and does not require an advertised HTTP address. `LocalOnlyBroadcastTransport` sends only to the current process.

`WithEntityFrameworkRegistry<TContext>()` stores node and scope registrations in an application-owned `DbContext`. `WithHttpTransport(...)` maps the receiver and sends directly to each registered node address. These providers are the multi-node pair.

## Use Cases

Use Broadcasting for short-lived notifications such as:

- clear a named in-memory cache on every active API node;
- reload a local configuration projection after an administrative change;
- tell every live node to refresh a routing or feature registry;
- run an operational delivery probe.

Use [Messaging](./features-messaging.md) when delivery must survive downtime or be retried from durable storage. Use [Queueing](./features-queueing.md) when one consumer should claim each work item. Use [Application Events](./features-application-events.md) for in-process notification handlers that do not need node discovery.

Do not broadcast large data sets. Publish a small identifier or version and let each node load the current state from its authoritative store.

## Basic Usage

The in-memory setup works in a worker, console application, test host, or a single web process:

```csharp
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
	.AddBroadcasting(options => options
		.Scopes("catalog")
		.NodeIdentity("catalog-local"))
	.AddHandler<RefreshCatalog, RefreshCatalogHandler>();

using var host = builder.Build();
await host.StartAsync();

var broadcaster = host.Services.GetRequiredService<IBroadcastService>();
var result = await broadcaster.PublishAsync(
	new RefreshCatalog("products"),
	["catalog"]);

if (result.IsFailure)
{
	Console.Error.WriteLine(result.ToString());
	return;
}

Console.WriteLine($"Accepted by {result.Value.AcceptedCount} node(s).");
await host.StopAsync();

public sealed record RefreshCatalog(string CacheName);

public sealed class RefreshCatalogHandler : IBroadcastHandler<RefreshCatalog>
{
	public Task HandleAsync(
		RefreshCatalog payload,
		BroadcastContext context,
		CancellationToken cancellationToken)
	{
		Console.WriteLine($"Refresh requested for {payload.CacheName}.");
		return Task.CompletedTask;
	}
}
```

The publication reports one accepted node. The handler then prints `Refresh requested for products.` The publication result confirms admission, not completion of the handler.

## Registration model

Every `AddBroadcasting(...)` call contributes to one host-wide registration:

```csharp
builder.Services.AddBroadcasting(options => options
	.Scopes("catalog"))
	.AddHandler<RefreshCatalog, RefreshCatalogHandler>();

builder.Services.AddBroadcasting(options => options
	.Scopes("pricing"))
	.AddHandler<RefreshPrices, RefreshPricesHandler>();
```

The process registers both scopes and both handlers. Calls without a scope use `BroadcastingOptions.DefaultScope`. A later explicit scope replaces an implicit default scope, but an explicitly configured `default` scope remains.

Only one handler can be registered for each payload type. Conflicting registry providers, transports, handler types, address-resolver orders, or HTTP authentication selections fail during configuration.

## Publication results

`PublishAsync(...)` returns `Result<BroadcastResult>`. A successful result contains:

- `BroadcastId` for the envelope;
- normalized `TargetScopes`;
- `StartedUtc` and `CompletedUtc`;
- one `BroadcastNodeDeliveryResult` per selected node;
- aggregate target, response, accepted, failure, unreachable, and expired counts.

A successful `Result` does not mean that every node accepted the payload. Check `Nodes`, `AcceptedCount`, and `FailureCount` when the caller needs per-node evidence.

The method returns a failed `Result` for invalid input or runtime conditions such as disabled Broadcasting, no targets, an unavailable registry, an unregistered sender, a forbidden scope, or serialization failure.

## Multi-node setup

Use a shared registry and a transport for more than one process. The Entity Framework provider requires the application `DbContext` to implement `IBroadcastingContext`:

```csharp
using BridgingIT.DevKit.Infrastructure.EntityFramework.Broadcasting;
using Microsoft.EntityFrameworkCore;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
	: DbContext(options), IBroadcastingContext
{
	public DbSet<BroadcastNodeRegistrationEntity> BroadcastNodeRegistrations { get; set; }
	public DbSet<BroadcastNodeScopeEntity> BroadcastNodeScopes { get; set; }
}
```

Configure the shared registry and HTTP transport:

```csharp
builder.Services
	.AddBroadcasting(options => options
		.Scopes("catalog")
		.RegistrationLease(
			renewalInterval: TimeSpan.FromMinutes(1),
			duration: TimeSpan.FromMinutes(3)))
	.WithEntityFrameworkRegistry<AppDbContext>()
	.WithHttpTransport(options => options
		.AdvertisedAddress(builder.Configuration["Broadcasting:AdvertisedAddress"])
		.SharedSecret(builder.Configuration["Broadcasting:SharedSecret"]))
	.AddHandler<RefreshCatalog, RefreshCatalogHandler>();
```

Map devkit endpoints in the web application:

```csharp
app.MapEndpoints();
```

The Entity Framework provider uses `__Broadcasting_NodeRegistrations` and `__Broadcasting_NodeScopes`. Include those entities in the application's migrations.

`WithEntityFrameworkRegistry<TContext>()` enables database-readiness coordination for the selected context. If no `IDatabaseReadyService` is registered, startup continues without the wait.

## HTTP transport security

The receiver route defaults to `/_bdk/api/broadcasting`. Broadcasting applies its own `IBroadcastHttpAuthentication` before reading the request body. The route does not use the host's normal endpoint authorization settings.

`WithHttpTransport()` uses `AllowAllBroadcastHttpAuthentication` unless the host selects another implementation. Configure `.SharedSecret(...)` or `WithHttpAuthentication<TAuthentication>()` when another process can reach the receiver.

The shared-secret implementation sends the value in `X-Bdk-Broadcast-Key`. Use HTTPS and store the secret outside source control. An empty string is still an explicitly selected shared secret, so validate production configuration before building the host.

The advertised address must identify one concrete HTTP or HTTPS process. Wildcard hosts such as `0.0.0.0`, `*`, and `+` are rejected. When configuration does not supply an address, the resolver chain can use a reachable Kestrel address.

## Delivery limits

The defaults are:

- maximum payload size: 64 KiB;
- delivery timeout: 2 seconds per node;
- maximum concurrent remote deliveries: 16;
- broadcast lifetime: 5 seconds;
- duplicate retention: 10 minutes with a capacity of 1,024 IDs;
- handler queue capacity: 32 per payload type;
- unreachable threshold: 3 consecutive failed deliveries.

`BroadcastingOptions.Validate()` requires positive limits. Duplicate retention must be longer than the default lifetime. When registration leases are enabled, the lease duration must be longer than the renewal interval.

The publisher marks a remote registration inactive after it reaches the configured unreachable threshold. Registration leases provide a second cleanup path for processes that stop without graceful deregistration.

## Diagnostics and operations

`IBroadcastingDiagnostics.GetAsync()` returns registrations grouped by scope. `RemoveAsync(...)` requires an `IBroadcastOperationalAuthorizer`; the default authorizer denies removals.

Add the console commands through the shared builder:

```csharp
builder.Services.AddBroadcasting()
	.AddConsoleCommands();
```

The `broadcasting list` command shows registrations. `broadcasting probe` publishes the built-in `BroadcastProbe` to the default scope. Use `broadcasting probe --scope <name>` for a named scope.

When the dashboard is registered, its Broadcasting page shows the provider-neutral diagnostic snapshot. Register [Metrics](./features-metrics.md) to emit publication, delivery, and handler measurements through the `bdk` meter.

## Correlation behavior

Broadcasting copies the current application correlation ID into the envelope. The receiving dispatcher restores that value while it runs the handler. It does not transport the current distributed tracing `TraceId`.

See [Presentation Correlation IDs](./features-presentation-correlationid.md) for the application-wide correlation model.

## Related documentation

- [Common Utilities](./common-utilities.md) contains the low-level Broadcasting reference.
- [Presentation Endpoints](./features-presentation-endpoints.md) explains `MapEndpoints()`.
- [Presentation Dashboard](./features-presentation-dashboard.md) explains dashboard registration.
- [Results](./features-results.md) explains `Result<BroadcastResult>` handling.
