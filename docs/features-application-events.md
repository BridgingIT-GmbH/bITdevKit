# Application Events Feature Documentation

> Publish and handle application-layer events through `INotifier` with explicit `Result`-based outcomes.

[TOC]

## Overview

### Background

Application events represent things that happened in the application layer and should trigger one or more in-process reactions. Typical examples are `UserRegisteredEvent`, `InvoiceApprovedEvent`, or `CacheWarmupRequestedEvent`. They help coordinate side effects and follow-up work without forcing the publisher to know which handlers react.

In bITdevKit, application events are built on the `Notifier` infrastructure. That gives them:

- pub/sub fan-out to multiple handlers
- shared pipeline behaviors such as validation, retry, timeout, and authorization
- consistent `Result`-based success and failure handling
- a low-boilerplate source-generated authoring model through `[Event]`

This page focuses on application-layer events published intentionally from services, handlers, or endpoints. These are not the same as domain events raised from aggregates. For domain-originated events, see [Domain Events](./features-domain-events.md).

### When To Use Application Events

Application events work well when:

- a command or endpoint needs to trigger several in-process follow-up actions
- the publisher should not depend on concrete subscribers
- the work belongs to the application layer rather than the domain model
- handlers should use the same validation and resiliency pipeline as other app-layer interactions

Application events are a good fit for orchestration and side effects inside one application process. If you need durable cross-process delivery, see [Messaging](./features-messaging.md). If you need aggregate-originated business events, see [Domain Events](./features-domain-events.md).

## Challenges

- Publishers should not need to know every subscriber.
- Multiple handlers need a consistent execution model.
- Follow-up work should return explicit success or failure information instead of hiding everything behind exceptions.
- Validation and retry rules should stay out of the core business logic.
- Boilerplate grows quickly when each event needs separate event and handler classes.

## Solution

The application-events approach uses the existing notifier building blocks:

- `INotifier` publishes an event to all discovered handlers.
- `NotificationBase` provides event metadata such as `NotificationId` and `NotificationTimestamp`.
- `NotificationHandlerBase<TEvent>` provides the handler abstraction.
- `ValidationPipelineBehavior<,>`, `RetryPipelineBehavior<,>`, and related behaviors can run for events just like requests.
- `[Event]` source generation can emit one generated handler per `[Handle]` method on a partial event type.

Handlers return `Result`, which keeps success, failure, messages, and typed errors explicit. See [Results](./features-results.md) for the underlying outcome model and [Requester and Notifier](./features-requester-notifier.md) for the runtime infrastructure.

## Setup

Register the notifier in the dependency injection container:

```csharp
services.AddNotifier()
    .AddHandlers()
    .WithBehavior<ValidationPipelineBehavior<,>>()
    .WithBehavior<RetryPipelineBehavior<,>>();
```

If you want to use the source-generated authoring model, add the code generation package to the project that contains the events:

```xml
<PackageReference Include="BridgingIT.DevKit.Common.Utilities.CodeGen"
                  Version="x.y.z"
                  PrivateAssets="all" />
```

## Basic Usage

### Defining An Application Event

The lowest-boilerplate authoring model uses `[Event]` and one or more `[Handle]` methods:

```csharp
[Event]
public partial class UserRegisteredEvent
{
    [ValidateNotEmptyGuid("UserId is required.")]
    public string UserId { get; init; }

    [ValidateNotEmpty("Email is required.")]
    [ValidateEmail("Email must be valid.")]
    public string Email { get; init; }

    [Handle]
    private Result Audit()
    {
        Console.WriteLine($"Audit registration for {UserId}");
        return Success();
    }

    [Handle]
    private async Task<Result> SendWelcomeEmailAsync(
        INotificationService<EmailMessage> notifications,
        CancellationToken cancellationToken)
    {
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            Subject = "Welcome",
            Body = "Your account is ready.",
            To = [Email]
        };

        var sendResult = await notifications.SendAsync(message, cancellationToken: cancellationToken);

        return sendResult.IsSuccess
            ? Success()
            : Failure("Welcome email could not be sent.");
    }
}
```

Each `[Handle]` method becomes its own generated `NotificationHandlerBase<UserRegisteredEvent>` implementation. That means one event can fan out to multiple generated subscribers without additional ceremony.

### Manual Handlers Still Work

Source-generated and manual handlers can be mixed freely:

```csharp
public class UserRegisteredMetricsHandler : NotificationHandlerBase<UserRegisteredEvent>
{
    protected override Task<Result> HandleAsync(
        UserRegisteredEvent notification,
        PublishOptions options,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Metrics tracked for {notification.UserId}");
        return Task.FromResult(Result.Success());
    }
}
```

When `INotifier.PublishAsync(...)` is called, the generated handlers and the manual handler above all run under the normal notifier execution rules.

### Publishing An Event

Inject `INotifier` anywhere in the application layer that needs to publish the event:

```csharp
public class RegistrationService(INotifier notifier)
{
    public async Task<Result> RegisterAsync(string userId, string email, CancellationToken cancellationToken)
    {
        // application logic omitted

        return (Result)await notifier.PublishAsync(
            new UserRegisteredEvent
            {
                UserId = userId,
                Email = email
            },
            cancellationToken: cancellationToken);
    }
}
```

The returned `Result` aggregates the notifier outcome, which makes failure handling explicit at the call site.

## Validation

Simple validation rules can be added directly to event properties:

```csharp
[Event]
public partial class InvoiceApprovedEvent
{
    [ValidateNotEmptyGuid("InvoiceId is required.")]
    public string InvoiceId { get; init; }

    [ValidateNotEmpty("ApprovedBy is required.")]
    public string ApprovedBy { get; init; }

    [Handle]
    private Result Handle()
    {
        return Success();
    }
}
```

For more complex validation, use `[Validate]` with `InlineValidator<TEvent>`:

```csharp
[Event]
public partial class CustomerImportCompletedEvent
{
    [ValidateNotEmpty]
    public List<string> ImportedCustomerIds { get; init; }

    [Validate]
    private static void Validate(InlineValidator<CustomerImportCompletedEvent> validator)
    {
        validator.RuleFor(x => x.ImportedCustomerIds)
            .Must(ids => ids.Count <= 1000)
            .WithMessage("A maximum of 1000 imported customers is allowed.");
    }

    [Handle]
    private Result Handle()
    {
        return Success();
    }
}
```

The generated validator is picked up by the normal notifier validation pipeline, so the event authoring model stays aligned with commands and queries.

## Handler Parameters And Behaviors

`[Handle]` methods can declare:

- application event properties directly through `this`
- `CancellationToken`
- `PublishOptions`
- DI services as additional parameters

Class-level handler policy attributes such as retry, timeout, authorization, chaos, circuit breaker, and cache invalidation can be applied to the event type. For source-generated events, those attributes are copied to each generated handler.

Example:

```csharp
[Event]
[HandlerRetry(2, 100)]
[HandlerTimeout(500)]
public partial class ReportGeneratedEvent
{
    public string ReportId { get; init; }

    [Handle]
    private async Task<Result> InvalidateCacheAsync(
        ICache cache,
        CancellationToken cancellationToken)
    {
        await cache.RemoveAsync($"report:{ReportId}", cancellationToken);
        return Success();
    }
}
```

For runtime execution modes such as sequential, concurrent, or fire-and-forget publication, see [Requester and Notifier](./features-requester-notifier.md).

## Application Events vs Domain Events

Use application events when the application layer decides to publish a follow-up signal.

Use domain events when an aggregate records a business-significant state transition inside the domain model.

Practical rule of thumb:

- `CustomerCreatedDomainEvent`: raised by the aggregate itself
- `WelcomeEmailRequestedEvent`: published by the application layer after orchestration logic completes

The distinction keeps domain logic and application orchestration separate. For the domain side, see [Domain Events](./features-domain-events.md).

## Notes

- `[Event]` requires a top-level, non-generic, `partial` class.
- One event can declare one or more `[Handle]` methods.
- Additional manual `INotificationHandler<TEvent>` subscribers remain fully supported.
- `[Handle]` methods must return `Result` or `Task<Result>`.
- If the event does not inherit `NotificationBase`, the generator adds it automatically.
- Publishing returns `Result`, which is useful when handlers need to surface failure information to the caller.

## Relationship To Other Features

- [Requester and Notifier](./features-requester-notifier.md) covers the underlying runtime dispatching model, behaviors, and source-generated event appendix.
- [Results](./features-results.md) explains the `Result` abstraction used by event handlers and publishers.
- [Application Commands and Queries](./features-application-commands-queries.md) covers the request/response side of application orchestration.
- [Domain Events](./features-domain-events.md) covers aggregate-originated events in the domain layer.
- [Notifications](./features-notifications.md) covers user-facing notification delivery such as email and queued notification sending.
