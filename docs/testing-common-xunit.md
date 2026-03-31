# Testing Common XUnit

> Reuse shared xUnit test helpers for setup, web hosts, fake time, traits, and Result assertions.

[TOC]

## Overview

`Common.Utilities.Xunit` is contributor-facing test infrastructure. It is not runtime application plumbing. The package exists to make unit, integration, and end-to-end tests easier to write by standardizing test context, logging, traits, web host factories, and a few common assertions.

## Test Base

### `TestsBase`

`TestsBase` provides a reusable base class for tests that need dependency injection and logging.

- it creates an `IServiceCollection`
- it wires xUnit logging into the test output
- it builds a `ServiceProvider`
- it exposes `CreateLogger()` and `CreateLogger<T>()`
- it supports `RegisterServices(...)` and `RegisterServicesAsync(...)`
- it supports `ResetServices(...)` when a test needs a clean container
- it provides `CreateScope()` for scoped service resolution
- it includes `Benchmark(...)` and `BenchmarkAsync(...)` helpers for simple performance checks
- it calls `SetUp()` and `TearDown()` hooks around the test lifecycle

`TestContext` is the small metadata object attached to the base class. It currently tracks the test name and a string dictionary for additional metadata.

## Web App Factories

### `CustomWebApplicationFactory<TEntryPoint>`

This factory is meant for integration tests that run the app through `WebApplicationFactory`.

- it loads `appsettings.json` and environment variables
- it sets the host environment, defaulting to `Development`
- it plugs in xUnit logging
- it configures Serilog for test output
- it can register extra services through a callback
- it can enable fake authentication automatically for test runs

### `KestrelWebApplicationFactoryFixture<TEntryPoint>`

This fixture is for end-to-end scenarios that need a real Kestrel listener instead of only `TestServer`.

- it starts a real host
- it exposes `ServerAddress`
- it resets module registration state before bootstrapping the test server
- it can attach xUnit output logging

## Fake Time

`Common.Utilities.Xunit` includes a test helper for `TimeProvider`.

- `AddTimeProvider(DateTimeOffset start)` registers a `FakeTimeProvider`
- `AddTimeProvider(DateTime start)` does the same for UTC `DateTime`
- both overloads update `TimeProviderAccessor.Current`

That makes time-dependent code deterministic in tests without special setup in the application code.

## Result Assertions

`ShouldBeResultExtensions` adds focused assertions for `Result`-style values.

- `ShouldBeSuccess()` and `ShouldBeFailure()`
- `ShouldContainMessage()` and `ShouldNotContainMessage()`
- `ShouldContainMessages()` and `ShouldNotContainMessages()`
- `ShouldContainError<TError>()` and `ShouldNotContainError<TError>()`
- `ShouldBeValue<T>()` and `ShouldNotBeValue<T>()`

These helpers are intentionally small and map cleanly to the devkit result model instead of introducing a separate assertion vocabulary.

## Traits

The package defines xUnit trait attributes so test discovery can be filtered by category.

- `UnitTestAttribute`
- `IntegrationTestAttribute`
- `ModuleAttribute`
- `FeatureAttribute`
- `SystemTestAttribute`
- `CategoryAttribute`

The discoverers emit traits such as `Category`, `UnitTest`, `IntegrationTest`, `Module`, `Feature`, and `SystemTest`. Several attributes accept an optional string or numeric identifier so teams can slice test runs more precisely.

## Practical Use

Use this package when you are writing tests inside the devkit or against a devkit-based application and want consistent infrastructure without repeating the same host setup in every test project.

It is a good fit for:

- unit tests that need DI and logging
- integration tests that exercise HTTP endpoints
- end-to-end tests that need a real server address
- tests that assert devkit `Result` types directly
- test suites that want stable trait names across projects
