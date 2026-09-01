
# Utilities

> Group small but essential application-layer support services and contracts in one lightweight package.

[TOC]

## Overview

`Application.Utilities` is a small application-layer support package rather than one single feature. In the current devkit, it groups three concerns:

- startup task orchestration
- application-facing log-entry contracts
- time-provider registration for runtime code and tests

Two of those already have dedicated feature pages:

- [StartupTasks](./features-startuptasks.md)
- [Log Entries](./features-log-entries.md)

This page gives the package-level picture and documents the remaining utility feature that lives directly here: time-provider integration.

## Challenges

Small cross-cutting application concerns can become inconsistent when each feature registers its own clock, startup orchestration, or log-query contract. Time is especially difficult to test when code mixes injected providers, ambient access, and direct `DateTime.UtcNow` calls.

## Solution

`Application.Utilities` groups the application-facing contracts for startup tasks and persisted log access with `AddTimeProvider(...)`. The time registration replaces the DI `TimeProvider` singleton and sets `TimeProviderAccessor.Current`, allowing DI-aware and ambient callers in the same asynchronous flow to use the same clock.

## Key Features

- startup-task contracts and hosted orchestration
- persisted log-entry query and maintenance contracts
- `TimeProvider.System`, instance, and factory registration overloads
- ambient time access through `TimeProviderAccessor`
- deterministic fake-time support in tests

## Architecture

Startup tasks and log entries have their own feature guides. The time integration is a small bridge: the service-collection extension owns DI registration, while `TimeProviderAccessor` stores the ambient provider in `AsyncLocal<TimeProvider>` and falls back to `TimeProvider.System` when no override exists.

## Use Cases

- share one testable clock across application services
- make expiration, retention, and scheduling tests deterministic
- keep legacy or domain code that uses the ambient accessor aligned with DI
- locate the package boundaries for startup-task and log-entry contracts

## Basic Usage

Register the system provider once and inject `TimeProvider` into DI-managed code. This endpoint returns the current UTC time and shows whether DI and the ambient accessor resolve the same provider instance.

```csharp
using BridgingIT.DevKit.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTimeProvider();

var app = builder.Build();

app.MapGet("/system/time", (TimeProvider timeProvider) =>
{
    var utcNow = timeProvider.GetUtcNow();

    return Results.Ok(new
    {
        UtcNow = utcNow,
        SameProvider = ReferenceEquals(
            timeProvider,
            TimeProviderAccessor.Current)
    });
});

app.Run();
```

`GET /system/time` returns the current timestamp and `SameProvider: true`. `GetUtcNow()` returns a value directly, so there is no `Result` wrapper to unwrap or failure branch to handle.

## Package contents

### Startup tasks

The package contains the hosted-service orchestration for `IStartupTask`, task definitions, and startup-task behaviors.

See [StartupTasks](./features-startuptasks.md) for the full feature documentation.

### Log entries

The package defines `ILogEntryService` plus the DTOs used to query, stream, export, and maintain persisted logs.

See [Log Entries](./features-log-entries.md) for the full feature documentation.

### Time-provider integration

The remaining direct utility feature is the `AddTimeProvider(...)` registration API, which connects .NET's `TimeProvider` with the devkit's ambient `TimeProviderAccessor`.

## Time providers

### Why it exists

Time-dependent code is hard to test when it reaches directly for `DateTime.UtcNow` or `TimeProvider.System`. The utility package solves that by:

- registering a `TimeProvider` in DI
- synchronizing it with `TimeProviderAccessor.Current`
- making the same current time source available to both DI-driven services and code that cannot conveniently receive constructor injection

That gives you one consistent clock for production code, tests, and asynchronous flows.

### Registration

Production setup:

```csharp
builder.Services.AddTimeProvider(); // Uses TimeProvider.System
```

Custom or fake provider setup:

```csharp
var fakeTimeProvider = new FakeTimeProvider();

builder.Services.AddTimeProvider(fakeTimeProvider);

// A factory is also supported. Return a stable instance when ambient and DI
// callers must observe the same provider.
builder.Services.AddTimeProvider(_ => fakeTimeProvider);
```

The overloads support:

- registering `TimeProvider.System`
- registering a concrete provider instance
- registering a provider via factory

All overloads also update `TimeProviderAccessor.Current`. The factory overload invokes the factory immediately through a temporary service provider and DI invokes it again when the final container resolves `TimeProvider`. Do not resolve scoped services or create a different clock on each factory call when ambient and injected callers must stay aligned.

### Using time in DI-driven services

```csharp
public sealed class SubscriptionCleanupService(TimeProvider timeProvider)
{
    public DateTimeOffset GetNow() => timeProvider.GetUtcNow();
}
```

This is the preferred style when the consuming type already participates in dependency injection.

### Using time without DI

Some code paths, especially lower-level domain or utility code, may not naturally receive a `TimeProvider` through the constructor. In those cases the ambient accessor is available:

```csharp
var now = TimeProviderAccessor.Current.GetUtcNow();
```

That keeps time access testable without forcing every method signature to carry a time abstraction.

### Testing

The feature works especially well with fake clocks.

```csharp
var fake = new FakeTimeProvider();
fake.SetUtcNow(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

services.AddTimeProvider(fake);
```

Tests can then advance time deterministically and exercise:

- expiration logic
- delayed work
- date-sensitive validation
- retention and cleanup policies

### When to use it

Use the application utility time-provider setup when:

- application code needs a testable clock
- multiple layers should agree on the same current time source
- existing code uses `TimeProviderAccessor` and should stay aligned with DI registration

Avoid mixing direct `DateTime.UtcNow` calls into the same workflow once you have standardized on this feature.

## Package boundaries

This page intentionally does not document every helper mentioned in older revisions of the utilities docs.

Those concerns belong elsewhere now:

- resiliency helpers such as retries and throttling belong to common infrastructure or feature-specific behaviors
- startup-task execution belongs to [StartupTasks](./features-startuptasks.md)
- operational log access belongs to [Log Entries](./features-log-entries.md)

## Related documentation

- [StartupTasks](./features-startuptasks.md)
- [Log Entries](./features-log-entries.md)
- [Common Utilities](./common-utilities.md)
- [Common Observability Tracing](./common-observability-tracing.md)
