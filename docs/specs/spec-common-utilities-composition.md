---
status: implemented
---

# Design Specification: Composition Building Blocks (Common.Utilities.Composition)

> This design document specifies a reusable set of developer-facing composition building blocks in `Common.Utilities`. The feature helps developers compose services through adapters, decorators, interception, strategies, composites, and chains without repeatedly writing low-level dependency-injection plumbing or relying on external helper packages such as Scrutor for common decorator scenarios.

[TOC]

## Introduction

`Common.Utilities.Composition` provides general-purpose building blocks for composing application services and integration abstractions in a consistent, discoverable, and low-friction way.

The feature is intended for developers who need to structure their own services, wrap third-party APIs, add cross-cutting behavior around contracts, select implementations at runtime, or combine multiple implementations behind one contract.

A key motivation is to reduce the need for developers to reach for external DI helper libraries such as Scrutor when they only need common composition capabilities like ordered decorators. The devkit should provide these capabilities natively, with predictable behavior, good diagnostics, and fluent APIs aligned with the rest of the devkit.

This feature is not a business framework and does not replace existing devkit-specific behavior systems such as Requester/Notifier pipelines, Repository behaviors, ActiveEntity behaviors, Pipelines, Messaging, Queueing, or Orchestrations. Instead, it provides generic `Common.Utilities` building blocks that can be consumed by applications and, where useful, by other devkit features.

The implementation must avoid source generation and attribute-heavy configuration. The public model must be fluent, declarative, IntelliSense-friendly, and explicit enough that developers can understand the resulting service composition from the composition root.

## Goals

* Provide reusable composition helpers in the `Common.Utilities` namespace.
* Reduce developer reliance on external decorator/composition helper libraries such as Scrutor for common cases.
* Support common composition patterns through developer-friendly APIs.
* Keep registration fluent, declarative, and discoverable through IntelliSense.
* Avoid attribute-heavy configuration.
* Avoid source generation.
* Prefer explicit DI composition where possible.
* Support runtime interface interception without external proxy dependencies.
* Support custom interception implementations in addition to built-in interception behaviors.
* Use existing `Common.Utilities` resiliency helpers where applicable, especially for retry and timeout behavior.
* Support devkit `Result` and `Result<T>` return types in interception behavior handling.
* Preserve service lifetimes when wrapping or adapting services.
* Avoid open generic composition complexities in the first implementation.
* Keep the implementation small, testable, and independent from application-layer features.
* Ship all listed building blocks as part of the first version.

## Non-Goals

* Do not build a full AOP framework.
* Do not require Castle DynamicProxy or other third-party proxy engines.
* Do not require Scrutor.
* Do not support class proxying initially.
* Do not add source generation.
* Do not use attributes as the primary configuration model.
* Do not replace Requester, Notifier, Pipelines, Repository behaviors, ActiveEntity behaviors, Messaging, Queueing, or Orchestration behavior systems.
* Do not hide business logic inside generic proxy behaviors.
* Do not try to automatically infer complex adapters from unrelated contracts.
* Do not include caching proxy behavior in the first version.

## Package and Namespace

The feature should live under the existing utilities area.

Suggested project/package placement:

```text
BridgingIT.DevKit.Common.Utilities
```

Suggested namespace root:

```csharp
BridgingIT.DevKit.Common.Utilities.Composition
```

The feature should not introduce additional nested public namespaces beyond:

```csharp
BridgingIT.DevKit.Common.Utilities.Composition
```

All public contracts, builders, behaviors, extensions, abstractions, and helpers should remain directly inside the single `BridgingIT.DevKit.Common.Utilities.Composition` namespace.

Internal folder structure inside the project may still be organized physically by concern, but public API namespace fragmentation should be avoided.

The public entry point should be:

```csharp
services.AddComposition()
```

## Pattern Family

The feature should expose building blocks for these patterns.

| Pattern                 | Purpose                                                              | Support                                 |
| ----------------------- | -------------------------------------------------------------------- | --------------------------------------- |
| Adapter                 | Convert one contract into another                                    | Required                                |
| Decorator               | Add behavior while keeping the same contract                         | Required                                |
| Interception            | Control access to or intercept calls while keeping the same contract | Required                                |
| Strategy                | Register and resolve named/keyed implementations                     | Required                                |
| Composite               | Treat multiple implementations as one implementation                 | Required                                |
| Chain of Responsibility | Pass a request through ordered handlers until one handles it         | Required                                |

## Design Principles

### Fluent Over Attribute-Heavy

The primary user experience must be fluent registration.

Good:

```csharp
services.AddComposition()
    .For<IWeatherClient>()
        .Use<WeatherClient>()
        .Intercept(interception => interception
            .WithLogging()
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithRetry(3)
            .With<MyCustomWeatherInterceptor>())
        .RegisterScoped();
```

Avoid this as the primary model:

```csharp
[ProxyFor(typeof(IWeatherClient))]
[WithLogging]
[WithTimeout("00:00:05")]
public partial class WeatherClientProxy;
```

### Explicit Composition Root

Developers should be able to understand service composition by reading startup/module registration code.

### Native Decorator Support

Decorators are one of the central reasons for this feature. Developers should not need Scrutor for normal ordered decoration scenarios.

The devkit should support:

* ordered decorator chains
* explicit lifetime registration
* validation that decorators implement the decorated contract
* open generic decorator registration where practical
* clear diagnostics when the chain cannot be built

### Small Runtime Core

The runtime implementation should be simple and avoid heavy magic. Dynamic interface proxies may use `DispatchProxy`, but explicit decorators and adapters should use normal DI composition.

### Extensible Convenience

Built-in methods such as `WithLogging()`, `WithTimeout(...)`, and `WithRetry(...)` are convenience extensions only. The underlying model must allow custom implementations.

```csharp
proxy.WithBehavior<MyProxyBehavior<IWeatherClient>>();
proxy.With<MyCustomWeatherInterceptor>();
```

### Same Contract vs Different Contract

The API should make the distinction clear:

* Decorator: same contract, explicit wrapper class.
* Interception: same contract, runtime interception or custom interception wrapper.
* Adapter: different contract, explicit conversion class.

The public API should use `Intercept(...)` naming rather than `AsProxy(...)`. Proxying is an implementation technique; interception is the developer-facing intent.

### Use Existing Resiliency Helpers

Retry and timeout behavior must use existing devkit/Common.Utilities resiliency helpers where applicable instead of creating duplicate retry/timeout engines inside composition.

Composition-specific retry/timeout proxy behaviors should be thin adapters around the existing helpers.

## High-Level API

### Root Registration

```csharp
services.AddComposition();
```

The call should return a composition builder that can register multiple composition elements.

```csharp
services.AddComposition()
    .For<IWeatherClient>()
        .Use<WeatherClient>()
        .Intercept(interception => interception.WithLogging())
        .RegisterScoped()
    .For<INotificationSender>()
        .Use<SmtpNotificationSender>()
        .Decorate(decorators => decorators
            .With<LoggingNotificationSender>()
            .With<RetryNotificationSender>())
        .RegisterScoped();
```

The builder should be additive and safe to call from multiple modules.

### Registration Finalization Model

`services.AddComposition()` must be callable multiple times from different modules or startup areas. Calls should accumulate composition registrations and finalize them later before service resolution.

The implementation should register internal composition metadata during `AddComposition()` calls and materialize final service descriptors through a predictable finalization step. Developers should not need to call a separate manual `BuildComposition()` method.

The finalization model must satisfy these requirements:

* `services.AddComposition()` can be called multiple times.
* Multiple modules can contribute composition registrations independently.
* Composition definitions are accumulated deterministically in registration order.
* Final service factories are produced from the accumulated model.
* Duplicate or conflicting registrations are reported with clear diagnostics.
* Normal DI resolution works after service provider creation.

### Official Composition Order

When multiple composition modes are used together for the same service contract, the official call chain is:

```text
Decorators
→ Explicit interception wrappers (.With<TInterceptor>())
→ Runtime interception behaviors (.WithLogging(), .WithRetry(), .WithBehavior<T>())
→ Actual implementation
```

Example:

```csharp
services.AddComposition()
    .For<IWeatherClient>()
        .Use<WeatherClient>()
        .Decorate(d => d.With<LoggingWeatherClient>())
        .Intercept(i => i
            .With<AuthorizationWeatherInterceptor>()
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithRetry(3))
        .RegisterScoped();
```

Execution flow:

```text
Caller
  ↓
LoggingWeatherClient              (decorator)
  ↓
AuthorizationWeatherInterceptor   (explicit interception wrapper)
  ↓
RuntimeInterceptionHost            (runtime behavior host, which may be backed by an internal generated proxy)
  ↓
TimeoutInterceptionBehavior
  ↓
RetryInterceptionBehavior
  ↓
WeatherClient                     (actual implementation)
```

Compact form:

```text
Caller -> Decorator(s) -> Explicit Interceptor(s) -> Runtime Behavior Interceptor -> Implementation
```

## Decorators

### Purpose

Decorators add behavior around a service while keeping the same service contract. They are explicit classes and should be preferred when the wrapper is meaningful, reusable, or needs direct constructor injection.

This feature should cover the common scenarios where developers would otherwise use Scrutor's decoration helpers.

### Public API

```csharp
services.AddComposition()
    .For<IWeatherClient>()
        .Use<WeatherClient>()
        .Decorate(decorators => decorators
            .With<LoggingWeatherClient>()
            .With<RetryWeatherClient>())
        .RegisterScoped();
```

Alternative shorthand:

```csharp
services.AddComposition()
    .Decorate<IWeatherClient, LoggingWeatherClient>()
    .Decorate<IWeatherClient, RetryWeatherClient>();
```

### Example Usage

```csharp
public interface IWeatherClient
{
    Task<Result<Forecast>> GetForecastAsync(
        string city,
        CancellationToken cancellationToken);
}

public class WeatherClient : IWeatherClient
{
    public Task<Result<Forecast>> GetForecastAsync(
        string city,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<Forecast>.Success(new Forecast(city, 21)));
    }
}

public class LoggingWeatherClient(
    IWeatherClient inner,
    ILogger<LoggingWeatherClient> logger) : IWeatherClient
{
    public async Task<Result<Forecast>> GetForecastAsync(
        string city,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading forecast for {City}", city);

        var result = await inner.GetForecastAsync(city, cancellationToken);

        logger.LogInformation(
            "Loaded forecast for {City} with success={Success}",
            city,
            result.IsSuccess);

        return result;
    }
}

services.AddComposition()
    .For<IWeatherClient>()
        .Use<WeatherClient>()
        .Decorate(d => d.With<LoggingWeatherClient>())
        .RegisterScoped();
```

### Decorator Requirements

A decorator must implement the decorated service contract.

The decorator should receive the next service in the chain through a constructor parameter of the decorated interface type.

### Execution Order

Decoration order must be deterministic.

```csharp
.Decorate(decorators => decorators
    .With<A>()
    .With<B>()
    .With<C>())
```

The resulting call chain should be:

```text
A -> B -> C -> RealImplementation
```

The first registered decorator is the outermost decorator.

### Lifetime

The final exposed service lifetime must match the selected registration method.

```csharp
.RegisterSingleton()
.RegisterScoped()
.RegisterTransient()
```

The feature should not silently change lifetimes.

### Validation

At registration or build time, validate:

* Decorator implements the service interface.
* Concrete implementation can be constructed by DI.
* Decorator can be constructed by DI.
* Decorator has a usable constructor path that accepts the decorated service contract.

If validation cannot happen early, fail with clear exception messages at resolution time.

## Adapters

### Purpose

Adapters convert an incompatible source API into a target contract expected by the application. Adapters should be explicit classes.

### Public API

```csharp
services.AddComposition()
    .Adapt<ThirdPartyWeatherClient>()
        .To<IWeatherClient>()
        .Using<ThirdPartyWeatherClientAdapter>()
        .RegisterScoped();
```

Alternative shorthand:

```csharp
services.AddComposition()
    .AddAdapter<ThirdPartyWeatherClient, IWeatherClient, ThirdPartyWeatherClientAdapter>();
```

### Example Usage

```csharp
public class ThirdPartyWeatherClient
{
    public Task<ThirdPartyForecastResponse> LoadForecastAsync(
        string location,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new ThirdPartyForecastResponse(location, 21, "Sunny"));
    }
}

public class ThirdPartyWeatherClientAdapter(
    ThirdPartyWeatherClient client) : IWeatherClient
{
    public async Task<Result<Forecast>> GetForecastAsync(
        string city,
        CancellationToken cancellationToken)
    {
        var response = await client.LoadForecastAsync(city, cancellationToken);

        return Result<Forecast>.Success(
            new Forecast(response.Location, response.TemperatureCelsius));
    }
}

services.AddComposition()
    .Adapt<ThirdPartyWeatherClient>()
        .To<IWeatherClient>()
        .Using<ThirdPartyWeatherClientAdapter>()
        .RegisterScoped();
```

### Adapter Requirements

An adapter must implement the target contract.

The source service may be registered separately or through the adapter registration.

### Factory-Based Adapter Usage

The feature should expose an adapter factory for explicit runtime adaptation.

```csharp
public interface IAdapterFactory
{
    TTarget Adapt<TSource, TTarget>(TSource source)
        where TSource : class
        where TTarget : class;
}
```

Usage:

```csharp
var sender = adapterFactory.Adapt<ThirdPartyEmailClient, INotificationSender>(client);
```

This is useful when source instances are not created by DI.

### Validation

Validate:

* Adapter implements target contract.
* Adapter can receive the source service or source instance.
* Target registration does not accidentally override an existing service unless explicitly allowed.

## Interception

### Purpose

Interception adds cross-cutting behavior around a service while preserving the same service contract. It can be used for logging, metrics, timeout, retry, authorization, lazy access, and other access-control or invocation-control scenarios.

The feature should support two interception styles:

* explicit interceptor wrappers that implement the service contract and receive the next service in the chain
* runtime behavior-based interception hosted by a generated interface proxy

Proxying is an implementation technique for the runtime behavior host. Interception is the developer-facing concept.

Runtime-generated interception should support interface service contracts only.

### Public API

```csharp
services.AddComposition()
    .For<IWeatherClient>()
        .Use<WeatherClient>()
        .Intercept(interception => interception
            .WithLogging()
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithRetry(3))
        .RegisterScoped();
```

### Example Usage

```csharp
services.AddComposition()
    .For<IWeatherClient>()
        .Use<WeatherClient>()
        .Intercept(interception => interception
            .WithLogging()
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithRetry(3))
        .RegisterScoped();

public class ForecastService(IWeatherClient weatherClient)
{
    public Task<Result<Forecast>> GetAsync(
        string city,
        CancellationToken cancellationToken)
    {
        return weatherClient.GetForecastAsync(city, cancellationToken);
    }
}
```

### Explicit Interceptor Wrapper

Developers must be able to plug in an explicit interceptor wrapper when they want full control over the interception logic.

```csharp
services.AddComposition()
    .For<IWeatherClient>()
        .Use<WeatherClient>()
        .Intercept(interception => interception
            .WithLogging()
            .WithTimeout(TimeSpan.FromSeconds(5))
            .With<MyCustomWeatherInterceptor>())
        .RegisterScoped();
```

An explicit interceptor wrapper must implement the intercepted service contract and accept the next service in the chain.

```csharp
public class MyCustomWeatherInterceptor(
    IWeatherClient inner,
    ILogger<MyCustomWeatherInterceptor> logger) : IWeatherClient
{
    public async Task<Result<Forecast>> GetForecastAsync(
        string city,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Custom interceptor before call");

        var result = await inner.GetForecastAsync(city, cancellationToken);

        logger.LogInformation("Custom interceptor after call");

        return result;
    }
}
```

Explicit interceptor wrappers should be preferred when the wrapper itself is meaningful, reusable, or needs normal constructor injection and strongly typed code.

### Interception Behaviors

Runtime interception should also support behavior-based interception through a generated host that applies ordered behaviors around the real implementation. When runtime behaviors are configured, the interception layer may internally create an interface proxy host for that chain. Proxy creation is an implementation technique; interception remains the public concept.

```csharp
public interface IInterceptionBehavior<TService>
    where TService : class
{
    ValueTask<object> InvokeAsync(
        InterceptionInvocationContext<TService> context,
        CancellationToken cancellationToken);
}
```

```csharp
public class InterceptionInvocationContext<TService>
    where TService : class
{
    public required TService Inner { get; init; }

    public required MethodInfo Method { get; init; }

    public required object[] Arguments { get; init; }

    public required IServiceProvider Services { get; init; }

    public required CancellationToken CancellationToken { get; init; }

    public required Func<ValueTask<object>> Next { get; init; }
}
```

The builder should expose both generic behavior registration and convenience methods.

```csharp
.Intercept(interception => interception
    .WithBehavior<LoggingInterceptionBehavior<IWeatherClient>>()
    .WithBehavior<TimeoutInterceptionBehavior<IWeatherClient>>()
    .WithBehavior<MyCustomInterceptionBehavior<IWeatherClient>>())
```

Convenience methods should be implemented as extension methods:

```csharp
interception.WithLogging();
interception.WithTimeout(TimeSpan.FromSeconds(5));
interception.WithRetry(3);
```

There should be no caching behavior in this version.

### Runtime Interception Model

The runtime interception host should:

* receive the real implementation as its inner target
* translate method invocations into a behavior pipeline
* preserve the original method contract at the call site
* avoid exposing proxy-specific concerns to application code

The public mental model should remain:

```text
Caller -> Interception chain -> Implementation
```

When runtime behaviors are used, the generated proxy is only the internal host for that chain.

### Resiliency Behavior Requirements

`WithTimeout(...)` and `WithRetry(...)` must use the existing `Common.Utilities` resiliency helpers where applicable.

The composition feature should not create a separate retry engine or timeout implementation if the devkit already has reusable helpers for these concerns.

The interception behaviors should adapt the invocation model to the resiliency helpers:

```text
InterceptionInvocationContext -> Resiliency helper -> context.Next()
```

### Interception Execution Order

Explicit interceptor wrappers and runtime interception behaviors must compose deterministically.

```csharp
.Intercept(interception => interception
    .With<AInterceptor>()
    .With<BInterceptor>()
    .WithLogging()
    .WithTimeout(TimeSpan.FromSeconds(5)))
```

The intended call chain should be:

```text
AInterceptor -> BInterceptor -> RuntimeInterceptionHost -> RealImplementation
```

Inside the runtime interception host, behavior order should be registration order, with the first behavior outermost.

```text
Logging -> Timeout -> RealImplementation
```

This means the outermost concern is the first one registered, both for explicit interceptor wrappers and for runtime behaviors.

### Built-In Interception Behaviors

The required built-ins are:

| Behavior      | Purpose                                              | Notes                                                                         |
| ------------- | ---------------------------------------------------- | ----------------------------------------------------------------------------- |
| Logging       | Log before/after/failure of method calls             | Use `ILogger`                                                                 |
| Timeout       | Cancel or fail calls exceeding configured duration   | Use existing Common.Utilities resiliency helpers where applicable             |
| Retry         | Retry transient failures                             | Use existing Common.Utilities resiliency helpers                              |
| Metrics       | Record call duration and outcome                     | Keep lightweight                                                              |
| Authorization | Check access before invoking                         | Generic hook only; application-specific authorization can add custom behavior |
| Lazy          | Delay expensive inner service creation when possible | Optional behavior implementation, not class proxying                          |

Caching is intentionally excluded.

### Return Type Support

The runtime interception host must support common .NET service method return types and devkit Result return types:

```text
void
T
Task
Task<T>
ValueTask
ValueTask<T>
Result
Result<T>
Task<Result>
Task<Result<T>>
ValueTask<Result>
ValueTask<Result<T>>
```

`Result` and `Result<T>` are important because many devkit services expose explicit success/failure outcomes.

The implementation should include an internal return adapter, for example:

```csharp
internal static class InterceptionReturnValueAdapter
{
    // Converts invoked return values into ValueTask<object> for behavior processing.
    // Converts ValueTask<object> back into the original method return type.
    // Detects Result and Result<T> return types for Result-aware behavior handling.
}
```

### Result-Aware Behavior Handling

Interception behaviors should not require all methods to return `Result`, but when the return value is `Result` or `Result<T>`, built-in behaviors should preserve result semantics.

Examples:

* Logging should log `IsSuccess` / `IsFailure` when the result is an `Result`.
* Retry behavior may retry on thrown exceptions and, if configured, on failed `Result` values.
* Timeout behavior should return or throw according to existing resiliency helper conventions. If the existing helper supports Result-based timeout failures, use that path.

The default should be conservative:

* Exceptions can be retried by retry behavior.
* Failed business `Result` values should not be retried unless explicitly configured.

### CancellationToken Detection

The runtime interception host should detect a `CancellationToken` argument when present. If no token is present, use `CancellationToken.None`.

Timeout behavior must link its timeout token with the detected method token.

### Interface-Only Constraint

Runtime-generated interception should support interface services only.

Explicit interceptor wrappers still use normal DI composition and are not separate class-proxy support.

If `.Intercept(...)` is used for a non-interface service contract, fail clearly:

```text
Interception composition requires an interface service contract in this version.
```

## Strategies

### Purpose

Strategies allow multiple implementations of the same contract to be registered and selected by string key at runtime.

String keys are sufficient.

### Public API

```csharp
services.AddComposition()
    .Strategies<IPaymentProvider>()
        .Add<StripePaymentProvider>("stripe")
        .Add<PaypalPaymentProvider>("paypal")
        .Add<InvoicePaymentProvider>("invoice")
        .WithDefault("stripe");
```

### Example Usage

```csharp
public interface IPaymentProvider
{
    Task<Result<PaymentReceipt>> PayAsync(
        PaymentRequest request,
        CancellationToken cancellationToken);
}

public class PaymentService(
    IStrategyResolver<IPaymentProvider> providers)
{
    public Task<Result<PaymentReceipt>> PayAsync(
        string provider,
        PaymentRequest request,
        CancellationToken cancellationToken)
    {
        var strategy = providers.Resolve(provider);
        return strategy.PayAsync(request, cancellationToken);
    }
}

services.AddComposition()
    .Strategies<IPaymentProvider>()
        .Add<StripePaymentProvider>("stripe")
        .Add<PaypalPaymentProvider>("paypal")
        .WithDefault("stripe");
```

### Runtime API

```csharp
public interface IStrategyResolver<TStrategy>
    where TStrategy : class
{
    TStrategy Resolve(string key);

    bool TryResolve(string key, out TStrategy? strategy);

    TStrategy ResolveDefault();

    IReadOnlyCollection<string> Keys { get; }
}
```

### Validation

Validate:

* Duplicate string keys are not allowed unless explicitly replaced.
* Default key must exist.
* Strategy implementation must implement the strategy contract.

## Composites

### Purpose

Composites expose multiple implementations as one implementation. This is useful for fan-out behavior such as sending notifications to several targets or running multiple exporters.

### Public API

```csharp
services.AddComposition()
    .Composite<INotificationSender, CompositeNotificationSender>(composite => composite
        .With<EmailNotificationSender>()
        .With<TeamsNotificationSender>()
        .With<AuditNotificationSender>())
    .RegisterScoped();
```

### Example Usage

```csharp
public interface INotificationSender
{
    Task<Result> SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken);
}

public class CompositeNotificationSender(
    IEnumerable<INotificationSender> senders) : INotificationSender
{
    public async Task<Result> SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        var result = Result.Success();

        foreach (var sender in senders)
        {
            var sendResult = await sender.SendAsync(message, cancellationToken);

            if (sendResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(sendResult.Errors)
                    .WithMessages(sendResult.Messages);
            }

            result = result.WithMessages(sendResult.Messages);
        }

        return result;
    }
}

services.AddComposition()
    .Composite<INotificationSender, CompositeNotificationSender>(c => c
        .With<EmailNotificationSender>()
        .With<TeamsNotificationSender>())
    .RegisterScoped();
```

### Composite Requirements

A composite implementation must implement the contract and receive the configured child implementations.

The composition system must avoid injecting the composite into itself. It should pass only the configured child implementations.

### Execution Policy

The generic composition helper should not force sequential/concurrent/error policy. That belongs to the composite implementation.

## Chain of Responsibility

### Purpose

A chain passes a request through ordered handlers until one handles it or all decline. This is useful for provider selection, fallback handling, and small processing flows that do not need the full Pipelines feature.

### Public API

```csharp
services.AddComposition()
    .Chain<IFileImportHandler, FileImportContext>(chain => chain
        .With<CsvImportHandler>()
        .With<ExcelImportHandler>()
        .With<JsonImportHandler>());
```

### Contracts

```csharp
public interface IChainHandler<TContext>
{
    ValueTask<ChainResult> HandleAsync(
        TContext context,
        ChainExecutionDelegate<TContext> next,
        CancellationToken cancellationToken);
}

public delegate ValueTask<ChainResult> ChainExecutionDelegate<TContext>(
    TContext context,
    CancellationToken cancellationToken);
```

```csharp
public class ChainResult
{
    public bool Handled { get; init; }

    public Result Result { get; init; } = Result.Success();
}
```

### Example Usage

```csharp
public class FileImportContext
{
    public required string FileName { get; init; }

    public required Stream Content { get; init; }
}

public interface IFileImportHandler : IChainHandler<FileImportContext>
{
}

public class CsvImportHandler : IFileImportHandler
{
    public async ValueTask<ChainResult> HandleAsync(
        FileImportContext context,
        ChainExecutionDelegate<FileImportContext> next,
        CancellationToken cancellationToken)
    {
        if (!context.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return await next(context, cancellationToken);
        }

        // Import CSV here.
        return new ChainResult
        {
            Handled = true,
            Result = Result.Success("CSV file imported.")
        };
    }
}

services.AddComposition()
    .Chain<IFileImportHandler, FileImportContext>(chain => chain
        .With<CsvImportHandler>()
        .With<ExcelImportHandler>()
        .With<JsonImportHandler>());
```

### Chain Runtime

A chain resolver/executor should be available:

```csharp
public interface IChainExecutor<TContext>
{
    ValueTask<ChainResult> ExecuteAsync(
        TContext context,
        CancellationToken cancellationToken = default);
}
```

Handlers should be invoked in registration order.

## Fluent Builder Shape

The fluent API should stay discoverable and predictable.

Suggested shape:

```csharp
public static class ServiceCollectionExtensions
{
    public static ICompositionBuilder AddComposition(this IServiceCollection services);
}
```

```csharp
public interface ICompositionBuilder
{
    IServiceCollection Services { get; }

    IServiceCompositionBuilder<TService> For<TService>()
        where TService : class;

    IAdapterSourceBuilder<TSource> Adapt<TSource>()
        where TSource : class;

    IStrategyBuilder<TStrategy> Strategies<TStrategy>()
        where TStrategy : class;

    ICompositeBuilder<TService, TComposite> Composite<TService, TComposite>(
        Action<ICompositeChildrenBuilder<TService>> configure)
        where TService : class
        where TComposite : class, TService;

    IChainBuilder<THandler, TContext> Chain<THandler, TContext>(
        Action<IChainBuilder<THandler, TContext>> configure)
        where THandler : class, IChainHandler<TContext>;
}
```

```csharp
public interface IServiceCompositionBuilder<TService>
    where TService : class
{
    IServiceCompositionBuilder<TService, TImplementation> Use<TImplementation>()
        where TImplementation : class, TService;
}
```

```csharp
public interface IServiceCompositionBuilder<TService, TImplementation>
    where TService : class
    where TImplementation : class, TService
{
    IServiceCompositionBuilder<TService, TImplementation> Decorate(
        Action<IDecoratorBuilder<TService>> configure);

    IServiceCompositionBuilder<TService, TImplementation> Intercept(
        Action<IInterceptionBuilder<TService>> configure);

    ICompositionBuilder RegisterSingleton();

    ICompositionBuilder RegisterScoped();

    ICompositionBuilder RegisterTransient();
}
```

```csharp
public interface IDecoratorBuilder<TService>
    where TService : class
{
    IDecoratorBuilder<TService> With<TDecorator>()
        where TDecorator : class, TService;
}
```

```csharp
public interface IInterceptionBuilder<TService>
    where TService : class
{
    IInterceptionBuilder<TService> With<TInterceptor>()
        where TInterceptor : class, TService;

    IInterceptionBuilder<TService> WithBehavior<TBehavior>()
        where TBehavior : class, IInterceptionBehavior<TService>;
}
```

Built-in extension methods:

```csharp
public static class InterceptionBuilderExtensions
{
    public static IInterceptionBuilder<TService> WithLogging<TService>(
        this IInterceptionBuilder<TService> builder)
        where TService : class;

    public static IInterceptionBuilder<TService> WithTimeout<TService>(
        this IInterceptionBuilder<TService> builder,
        TimeSpan timeout)
        where TService : class;

    public static IInterceptionBuilder<TService> WithRetry<TService>(
        this IInterceptionBuilder<TService> builder,
        int attempts)
        where TService : class;

    public static IInterceptionBuilder<TService> WithMetrics<TService>(
        this IInterceptionBuilder<TService> builder)
        where TService : class;
}
```

No `WithCaching()` extension should be included.

## Dependency Injection Implementation Notes

### Service Chain Construction

The implementation needs a safe way to build a chain without resolving the exposed service recursively.

For a service registration:

```csharp
.For<IWeatherClient>()
    .Use<WeatherClient>()
    .Decorate(d => d.With<A>().With<B>())
    .Intercept(i => i.With<CInterceptor>().WithLogging())
    .RegisterScoped();
```

The intended chain is:

```text
A -> B -> CInterceptor -> RuntimeInterceptionHost -> WeatherClient
```

Implementation options:

1. Build the chain manually from descriptors using `ActivatorUtilities.CreateInstance` and pass the current inner instance as an explicit constructor argument.
2. Use internal wrapper factories instead of registering every intermediate as the public service contract.
3. Store composition registrations and register one final factory for `TService`.

Preferred approach:

* Register concrete implementation types separately where needed.
* Register the public service contract with a factory that builds the chain.
* Use `ActivatorUtilities.CreateInstance(serviceProvider, wrapperType, inner)` to construct decorators/interceptors.

### Lifetime Preservation

The public service registration lifetime is selected explicitly by `.RegisterScoped()`, `.RegisterSingleton()`, or `.RegisterTransient()`.

For singleton chains, all wrapper and implementation dependencies must be singleton-safe. The composition feature should not try to fully validate captive dependencies, but it should not introduce additional lifetime changes.

### Multiple Registrations

By default, registering the same service contract multiple times through `.For<TService>()` should append service descriptors like normal Microsoft DI behavior only when explicitly intended.

Suggested options:

```csharp
.ReplaceExisting()
.TryRegister()
.AddAdditional()
```

## Error Handling

Errors should be explicit and developer-friendly.

Examples:

```text
Composition registration for IWeatherClient failed: LoggingWeatherClient does not implement IWeatherClient.
```

```text
Interception composition for WeatherClient failed: IWeatherClient must be an interface when using runtime interception hosting.
```

```text
Decorator CachedWeatherClient could not be constructed. Ensure it has a constructor accepting IWeatherClient or a compatible service chain parameter.
```

```text
Strategy key 'stripe' is already registered for IPaymentProvider.
```

## Testing Strategy

### Unit Tests

Cover builder behavior:

* registers simple service implementation
* registers decorator chain in order
* rejects decorator not implementing service contract
* registers adapter target contract
* rejects adapter not implementing target contract
* registers runtime interception host with built-in behaviors
* registers service with custom explicit interceptor wrapper
* verifies interception behavior order
* verifies strategy resolver by key
* rejects duplicate strategy key
* verifies composite receives only configured child implementations
* verifies chain execution order

### Integration Tests With DI

Create test services and verify resolution through `ServiceProvider`.

Important scenarios:

* scoped composition returns same scoped instance within scope
* transient composition returns new instances
* singleton composition returns one instance
* decorators receive inner chain correctly
* custom interceptor receives inner chain correctly
* runtime interception host supports `void`, `T`, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`, `Result`, `Result<T>`, `Task<Result>`, `Task<Result<T>>`, `ValueTask<Result>`, and `ValueTask<Result<T>>`
* cancellation token is passed to timeout/retry behaviors
* retry and timeout use existing Common.Utilities resiliency helpers

### Runtime Interception Host Tests

Test all supported return types:

```text
void
string
Task
Task<string>
ValueTask
ValueTask<string>
Result
Result<string>
Task<Result>
Task<Result<string>>
ValueTask<Result>
ValueTask<Result<string>>
```

Test exception behavior:

* inner exception bubbles by default
* logging behavior logs failure
* retry behavior retries configured number of times through existing resiliency helpers
* timeout behavior cancels long-running call through existing resiliency helpers
* failed Result values are logged as failures
* failed Result values are not retried by default
* failed Result values can be retried only when explicitly configured

### Composite Tests

Ensure the composite does not receive itself as a child implementation.

## Documentation Requirements

The feature documentation should include:

* Overview and motivation
* Scrutor replacement/reduction note for decorator scenarios
* Pattern comparison table
* Decorator examples
* Adapter examples
* Interception examples
* Custom interceptor example
* Strategy examples
* Composite examples
* Chain examples
* Factory examples
* Null Object examples
* Result and Result<T> return type behavior
* Resiliency helper reuse for retry/timeout
* Guidance on when to use existing devkit features instead
* Limitations

### Pattern Comparison Table

| Need                                        | Use                     |
| ------------------------------------------- | ----------------------- |
| Same interface, explicit wrapper            | Decorator               |
| Same interface, access control/interception | Interception            |
| Different interface shape                   | Adapter                 |
| Select implementation by key                | Strategy                |
| Combine many implementations as one         | Composite               |
| Ordered fallback/handling                   | Chain of Responsibility |

## Summary

`Common.Utilities.Composition` should provide a practical, fluent, developer-facing composition toolkit. The implementation should focus on explicit and discoverable registration rather than source generation or attributes.

The key value is not inventing new patterns, but making common composition patterns easy, consistent, and safe to use across applications and devkit features.

The feature should also reduce external dependency pressure for developers. Common decorator scenarios should no longer require Scrutor.

The preferred developer experience is:

```csharp
services.AddComposition()
    .For<IWeatherClient>()
        .Use<WeatherClient>()
        .Decorate(d => d
            .With<LoggingWeatherClient>())
        .Intercept(p => p
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithRetry(3)
            .With<MyCustomWeatherInterceptor>())
        .RegisterScoped();
```

This keeps the service contract stable, the composition explicit, and the implementation extensible without forcing dynamic proxy magic, Scrutor dependency, source generation, or attribute-heavy programming models.
