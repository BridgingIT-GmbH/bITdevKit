# Common Observability Tracing

> Add lightweight `Activity`-based tracing around services without pulling in a full observability framework.

[TOC]

`Common.Utilities.Tracing` provides a small tracing utility for wrapping services in `Activity` instrumentation. It is a low-level building block rather than a complete observability stack.

The package is built around `TraceActivityDecorator<TDecorated>`, which creates a proxy around a service and starts activities for method calls.

## What it provides

- `TraceActivityDecorator<TDecorated>`
- `[TraceActivity]`
- `[NoTraceActivity]`
- `[ActivityAttributes]`
- `IActivityNamingSchema`
- built-in naming strategies such as `MethodFullNameSchema` and `ClassAndMethodNameSchema`
- `TraceActivityHelper` for method and attribute tags

## How the decorator works

Create a traced wrapper around an existing service instance:

```csharp
var traced = TraceActivityDecorator<IMyService>.Create(
    innerService,
    new ClassAndMethodNameSchema(),
    decorateAllMethods: true);
```

The decorator:

- creates an `ActivitySource` using the wrapped instance's concrete type name
- intercepts method calls through `DispatchProxy`
- starts an activity for each traced method
- adds standard method tags
- adds extra tags from attributes
- optionally records synchronous invocation exceptions

This makes it useful for lightweight tracing around service abstractions without adding tracing code to every method body.

## Attributes

### `[TraceActivity]`

Use this to opt a method into tracing explicitly or override the generated activity name.

The attribute also lets you control whether invocation exceptions are added to the activity.

### `[NoTraceActivity]`

Use this to exclude a method from tracing even when the decorator is configured to trace all methods.

### `[ActivityAttributes]`

Use this to attach extra tags to a class or method.

Example:

```csharp
public interface ICustomerService
{
    [TraceActivity("customers.find")]
    Task<CustomerModel> FindAsync(CustomerId id);
}

[ActivityAttributes("module:customers", "layer:application")]
public sealed class CustomerService : ICustomerService
{
    public Task<CustomerModel> FindAsync(CustomerId id) =>
        throw new NotImplementedException();
}
```

Place method attributes on the decorated interface. Place class-level `[ActivityAttributes]` on the concrete service because the decorator reads class attributes from the wrapped instance's runtime type.

## Naming schemas

`IActivityNamingSchema` controls how activity names are produced.

Built-in strategies include:

- `MethodFullNameSchema`
- `ClassAndMethodNameSchema`

Use a custom schema when your observability stack expects a different naming convention.

## Tags added to activities

`TraceActivityHelper` adds standard code-related tags such as:

- `code.namespace`
- `code.function`
- `code.function.parameters`

If `[ActivityAttributes]` is present, those attribute-defined tags are added as well.

## OpenTelemetry expectations

This package only creates `Activity` instances. It does not configure OpenTelemetry exporters, resource metadata, or sampling on its own.

To make the traces observable, the hosting application still needs to:

- register OpenTelemetry
- subscribe to the relevant activity source names
- configure exporters such as OTLP, console, or Jaeger

In other words, this package emits tracing data, but the application still owns the tracing pipeline.

## Limits and caveats

- This is best suited to service abstractions and boundary components, not every class in the system.
- The decorator adds runtime indirection, so it should be used intentionally rather than everywhere.
- Exception recording happens around method invocation. For methods that return `Task` or `ValueTask`, exceptions raised later in the asynchronous flow are not automatically wrapped by this helper.
- The tracing package is low-level. Higher-level observability concerns such as request tracing, correlation IDs, and pipeline behaviors still belong to the surrounding application infrastructure.

## When to use it

Use this package when you want targeted tracing around reusable service abstractions and you want that instrumentation to stay out of the business logic itself.

Prefer higher-level built-in tracing or logging when:

- the host already gives you enough request-level telemetry
- a pipeline behavior is a better fit
- correlation and logging are sufficient without span-level instrumentation

## Related docs

- [Presentation Correlation IDs](./features-presentation-correlationid.md)
- [Presentation Endpoints](./features-presentation-endpoints.md)
- [Messaging](./features-messaging.md)
- [Requester and Notifier](./features-requester-notifier.md)
- [Testing Common XUnit](./testing-common-xunit.md)
