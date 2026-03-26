# Utilities Feature Documentation

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

## What Belongs Here

### Startup Tasks

The package contains the hosted-service orchestration for `IStartupTask`, task definitions, and startup-task behaviors.

See [StartupTasks](./features-startuptasks.md) for the full feature documentation.

### Log Entries

The package defines `ILogEntryService` plus the DTOs used to query, stream, export, and maintain persisted logs.

See [Log Entries](./features-log-entries.md) for the full feature documentation.

### Time Provider Integration

The remaining direct utility feature is the `AddTimeProvider(...)` registration API, which connects .NET's `TimeProvider` with the devkit's ambient `TimeProviderAccessor`.

## Time Providers

### Why It Exists

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
builder.Services.AddTimeProvider(TimeProvider.System);

builder.Services.AddTimeProvider(sp => new FakeTimeProvider());
```

The overloads support:

- registering `TimeProvider.System`
- registering a concrete provider instance
- registering a provider via factory

All overloads also update `TimeProviderAccessor.Current`.

### Using Time In DI-Driven Services

```csharp
public sealed class SubscriptionCleanupService(TimeProvider timeProvider)
{
    public DateTimeOffset GetNow() => timeProvider.GetUtcNow();
}
```

This is the preferred style when the consuming type already participates in dependency injection.

### Using Time In Places Without DI

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

### When To Use It

Use the application utility time-provider setup when:

- application code needs a testable clock
- multiple layers should agree on the same current time source
- existing code uses `TimeProviderAccessor` and should stay aligned with DI registration

Avoid mixing direct `DateTime.UtcNow` calls into the same workflow once you have standardized on this feature.

## Package Boundaries

This page intentionally does not document every helper mentioned in older revisions of the utilities docs.

Those concerns belong elsewhere now:

- resiliency helpers such as retries and throttling belong to common infrastructure or feature-specific behaviors
- startup-task execution belongs to [StartupTasks](./features-startuptasks.md)
- operational log access belongs to [Log Entries](./features-log-entries.md)

## Related Documentation

- [StartupTasks](./features-startuptasks.md)
- [Log Entries](./features-log-entries.md)
- [Common Observability Tracing](./common-observability-tracing.md)
