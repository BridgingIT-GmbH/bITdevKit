# Common Utilities Documentation

[TOC]

## Overview

The devkit includes a broad set of shared utilities for cross-cutting runtime concerns. It is not one single feature. Instead, it groups several lower-level building blocks that support application, domain, infrastructure, and presentation code.

This includes:

- resiliency and concurrency helpers such as `Retryer`, `Debouncer`, `Throttler`, `CircuitBreaker`, `RateLimiter`, `Bulkhead`, and `TimeoutHandler`
- lightweight background and in-process messaging helpers such as `BackgroundWorker`, `SimpleNotifier`, and `SimpleRequester`
- dynamic predicate and reflection helpers
- content-type, compression, hashing, and cloning utilities
- id and key generation helpers
- low-level activity and tracing helpers
- startup-task primitives and behaviors
- validation helpers for FluentValidation

Some of those areas also have higher-level feature docs elsewhere in `docs/`. This page focuses on the shared utilities available across the devkit and gives a short usage example for each main utility family.

## Resiliency Helpers

The strongest concentration of reusable behavior here is the resiliency set.

### Retryer

`Retryer` reruns an asynchronous operation a configured number of times with a fixed or exponential delay.

Use it when:

- an operation can fail transiently
- the caller should stay in-process instead of using an external queue
- retry state and progress should remain explicit in code

Key capabilities:

- fixed-delay retries
- optional exponential backoff
- `Task` and `Task<T>` overloads
- optional `ILogger`-based error handling
- optional `IProgress<RetryProgress>` reporting

Example:

```csharp
var progress = new Progress<RetryProgress>(p =>
    Console.WriteLine($"retry {p.CurrentAttempt}/{p.MaxAttempts}: {p.Status}"));

var retryer = new RetryerBuilder(3, TimeSpan.FromSeconds(1))
    .UseExponentialBackoff()
    .WithProgress(progress)
    .Build();

await retryer.ExecuteAsync(
    async cancellationToken => await ImportAsync(cancellationToken),
    cancellationToken);
```

### Debouncer And SimpleDebouncer

`Debouncer` delays execution until no new call arrived during the configured interval. It is useful for noisy inputs such as UI typing, file-change bursts, or repeated refresh triggers.

`SimpleDebouncer` is the lighter-weight sibling when you only need basic delayed coalescing behavior without the richer progress shape.

Use them when:

- repeated calls should collapse into one execution
- the latest trigger matters more than the earlier ones
- you want cancellation-aware delayed execution

Example:

```csharp
var progress = new Progress<DebouncerProgress>(p => Console.WriteLine(p.Status));

var debouncer = new DebouncerBuilder(
        TimeSpan.FromMilliseconds(500),
        async ct => await SearchAsync(ct))
    .WithProgress(progress)
    .Build();

await debouncer.DebounceAsync(cancellationToken);

using var simpleDebouncer = new SimpleDebouncer(
    TimeSpan.FromMilliseconds(250),
    async () => await SaveDraftAsync());

simpleDebouncer.Debounce();
```

### Throttler

`Throttler` lets calls happen immediately and then suppresses repeated execution until the throttle interval expires.

Use it when:

- work should happen at most once per interval
- first-call responsiveness matters
- repeated triggers during the interval should not queue up unlimited work

Example:

```csharp
var progress = new Progress<ThrottlerProgress>(p =>
    Console.WriteLine($"remaining: {p.RemainingInterval.TotalMilliseconds} ms"));

using var throttler = new ThrottlerBuilder(
        TimeSpan.FromSeconds(1),
        async ct => await RefreshCacheAsync(ct))
    .WithProgress(progress)
    .Build();

await throttler.ThrottleAsync(cancellationToken);
```

### CircuitBreaker

`CircuitBreaker` protects callers from repeatedly invoking a failing dependency.

Key capabilities:

- `Closed`, `Open`, and `HalfOpen` states
- configurable failure threshold
- configurable reset timeout
- optional handled-error mode
- optional `IProgress<CircuitBreakerProgress>` reporting

Use it when:

- a downstream dependency is unstable
- fast failure is better than repeatedly waiting for the same failing call
- you want the dependency to get a recovery window before traffic resumes

Example:

```csharp
var progress = new Progress<CircuitBreakerProgress>(p =>
    Console.WriteLine($"{p.State}: {p.Status}"));

var circuitBreaker = new CircuitBreakerBuilder(3, TimeSpan.FromSeconds(30))
    .WithProgress(progress)
    .Build();

await circuitBreaker.ExecuteAsync(
    async ct => await CallRemoteServiceAsync(ct),
    cancellationToken);
```

### TimeoutHandler

`TimeoutHandler` wraps an async operation with a maximum allowed duration.

Use it when:

- a call should not outlive a known SLA
- you need explicit timeout behavior even when the underlying code lacks one
- callers need remaining-time progress information

Example:

```csharp
var progress = new Progress<TimeoutHandlerProgress>(p =>
    Console.WriteLine($"{p.RemainingTime.TotalSeconds:n1}s remaining"));

var timeout = new TimeoutHandlerBuilder(TimeSpan.FromSeconds(5))
    .WithProgress(progress)
    .Build();

await timeout.ExecuteAsync(
    async ct => await GenerateReportAsync(ct),
    cancellationToken);
```

### Bulkhead

`Bulkhead` limits concurrency using a semaphore and isolates pressure from one workload from starving the rest of the process.

Use it when:

- only a fixed number of expensive operations should run in parallel
- you want queued work rather than unrestricted concurrency
- a resource such as CPU, network, or a fragile dependency needs protection

Example:

```csharp
var progress = new Progress<BulkheadProgress>(p =>
    Console.WriteLine($"{p.CurrentConcurrency}/{p.MaxConcurrency} active"));

var bulkhead = new BulkheadBuilder(4)
    .WithProgress(progress)
    .Build();

await bulkhead.ExecuteAsync(
    async ct => await ProcessFileAsync(ct),
    cancellationToken);
```

### RateLimiter

`RateLimiter` enforces a maximum number of operations inside a time window.

Use it when:

- a dependency has rate limits
- local work should be smoothed over time
- excess requests should fail or be skipped explicitly

Example:

```csharp
var progress = new Progress<RateLimiterProgress>(p =>
    Console.WriteLine($"{p.CurrentOperations}/{p.MaxOperations} in window"));

var rateLimiter = new RateLimiterBuilder(10, TimeSpan.FromMinutes(1))
    .WithProgress(progress)
    .Build();

await rateLimiter.ExecuteAsync(
    async ct => await SendWebhookAsync(ct),
    cancellationToken);
```

### BackgroundWorker

`BackgroundWorker` is a lightweight helper for running cancellable background work with progress reporting.

Use it when:

- you want an in-process long-running task with cooperative cancellation
- the work should expose progress updates
- a full hosted-service or scheduler abstraction would be excessive

Example:

```csharp
var progress = new Progress<BackgroundWorkerProgress>(p =>
    Console.WriteLine($"{p.ProgressPercentage}%"));

var worker = new BackgroundWorkerBuilder(async (ct, p) =>
    {
        for (var i = 0; i <= 100; i += 10)
        {
            await Task.Delay(100, ct);
            p.Report(i);
        }
    })
    .WithProgress(progress)
    .Build();

await worker.StartAsync(cancellationToken);
```

### SimpleNotifier And SimpleRequester

These two types are lightweight in-process messaging helpers:

- `SimpleNotifier`: publish/subscribe notification fan-out
- `SimpleRequester`: single-handler request/response dispatch

They support progress reporting and pipeline-style extensibility, but they are the lighter-weight option. For the richer devkit-level guidance around in-process request/notification handling, see [Requester and Notifier](./features-requester-notifier.md).

Example:

```csharp
public sealed record UserImported(string Email) : ISimpleNotification;
public sealed record Ping(string Text) : ISimpleRequest<string>;

var notifier = new SimpleNotifierBuilder()
    .WithProgress(new Progress<SimpleNotifierProgress>(p => Console.WriteLine(p.Status)))
    .Build();

notifier.Subscribe<UserImported>((message, ct) =>
{
    Console.WriteLine(message.Email);
    return ValueTask.CompletedTask;
});

await notifier.PublishAsync(new UserImported("alice@example.com"), cancellationToken: cancellationToken);

var requester = new SimpleRequesterBuilder()
    .WithProgress(new Progress<SimpleRequesterProgress>(p => Console.WriteLine(p.Status)))
    .Build();

requester.RegisterHandler<Ping, string>((request, ct) => new ValueTask<string>($"pong: {request.Text}"));

var response = await requester.SendAsync<Ping, string>(new Ping("hello"), cancellationToken: cancellationToken);
```

### Progress Types

The resiliency family also defines typed progress models such as:

- `RetryProgress`
- `DebouncerProgress`
- `ThrottlerProgress`
- `CircuitBreakerProgress`
- `RateLimiterProgress`
- `BackgroundWorkerProgress`
- `TimeoutHandlerProgress`
- `BulkheadProgress`
- `SimpleNotifierProgress`
- `SimpleRequesterProgress`

That lets callers observe utility-specific state without reducing everything to plain log messages.

Example:

```csharp
var progress = new Progress<RetryProgress>(p =>
    logger.LogInformation("attempt {Attempt}/{Max}: {Status}", p.CurrentAttempt, p.MaxAttempts, p.Status));
```

## Requester Utilities

The devkit also includes a fuller in-process request/notification stack than the simple resiliency helpers.

It includes:

- `Requester` and `RequesterBuilder`
- `Notifier` and `NotifierBuilder`
- DI registration helpers
- handler discovery and caching
- pipeline behaviors
- policy attributes for retry, timeout, chaos, cache invalidation, and authorization
- no-op implementations for optional wiring scenarios

This area overlaps conceptually with the higher-level feature documentation in [Requester and Notifier](./features-requester-notifier.md). That feature page should be the main conceptual guide. This page just gives a short orientation and example.

Example:

```csharp
services.AddRequester()
    .AddHandlers()
    .WithBehavior(typeof(ValidationPipelineBehavior<,>))
    .WithRetryOptions(3, 250);

services.AddNotifier()
    .AddHandlers();
```

## Startup Task Utilities

The devkit includes shared startup-task primitives and behaviors, including:

- `StartupTaskOptions`
- `StartupTaskOptionsBuilder`
- `StartupTasksBuilderContext`
- retry, timeout, circuit-breaker, and chaos behaviors for startup work

These are the lower-level building blocks behind the startup-task concept. For the feature-level usage story, see [StartupTasks](./features-startuptasks.md).

Example:

```csharp
var options = new StartupTaskOptionsBuilder()
    .Enabled()
    .Order(100)
    .StartupDelay(TimeSpan.FromSeconds(5))
    .HaltOnFailure()
    .Build();
```

## Activity And Tracing Helpers

The devkit provides lower-level helpers around `System.Diagnostics.Activity` and `ActivitySource`.

This includes:

- `ActivityHelper`
- `ActivitySourceExtensions`
- `ActivityConstants`

Use these helpers when you need explicit activity creation, tagging, baggage propagation, or exception/status recording in low-level code.

This is closely related to [Common Observability Tracing](./common-observability-tracing.md), which documents the higher-level tracing conventions and decorators used elsewhere in the devkit.

Example:

```csharp
var source = new ActivitySource("MyModule");

await source.StartActvity(
    "import-users",
    async (activity, ct) =>
    {
        activity?.SetTag("tenant.id", tenantId);
        await ImportUsersAsync(ct);
    },
    cancellationToken: cancellationToken);
```

## Reflection And Expression Helpers

### PredicateBuilder

`PredicateBuilder<T>` is a fluent builder for dynamic LINQ predicates.

It is useful when:

- filters are assembled conditionally
- the final predicate must stay EF Core compatible
- nested `if` trees would otherwise make query construction noisy

It supports:

- `Add(...)` and `Or(...)`
- conditional additions like `AddIf(...)` and `OrIf(...)`
- grouped conditions
- custom combinators

Example:

```csharp
var predicate = new PredicateBuilder<Customer>()
    .Add(c => c.IsActive)
    .AddIf(minAge.HasValue, c => c.Age >= minAge.Value)
    .BeginGroup(useOr: true)
    .Add(c => c.City == "Berlin")
    .Or(c => c.City == "Hamburg")
    .EndGroup()
    .BuildExpression();

var customers = dbContext.Customers.Where(predicate);
```

### ReflectionHelper And PrivateReflection

`ReflectionHelper` provides cached reflection access and helpers for:

- reading and writing properties dynamically
- discovering methods and properties with caching
- creating low-level getter delegates
- scanning assemblies for matching types

Private-reflection helpers complement that with more ergonomic access to non-public members.

These helpers are mainly useful in infrastructure, testing, diagnostics, and framework-style code where dynamic access is justified.

Example:

```csharp
var customer = new Customer();

ReflectionHelper.SetProperty(customer, "Name", "Alice");
var name = ReflectionHelper.GetProperty<string>(customer, "Name");

var handlers = ReflectionHelper.FindTypes(
    t => t.Name.EndsWith("Handler"),
    typeof(Customer).Assembly);
```

## Shared State And Value Helpers

### TimeProviderAccessor

`TimeProviderAccessor` gives ambient access to the current `TimeProvider`. It is useful when code needs the current time without threading a `TimeProvider` through every constructor or method.

Use it when:

- domain or helper code needs the current time
- tests need to replace time deterministically
- you want one consistent time source within an async flow

Example:

```csharp
var now = TimeProviderAccessor.Current.GetUtcNow();

TimeProviderAccessor.Current = fakeTimeProvider;
var later = TimeProviderAccessor.Current.GetUtcNow();

TimeProviderAccessor.Reset();
```

### Version

`Version` is the devkit's semantic-version helper. It can parse version strings, compare versions, and render short or full version text.

Use it when:

- you need SemVer parsing and comparison
- prerelease or build metadata values matter
- version values should stay richer than plain strings

Example:

```csharp
var current = Version.Parse("2.4.0-beta.1+build45");
var released = Version.Parse("2.3.9");

var isNewer = current > released;
var shortText = current.ToString(VersionFormat.Short);
```

### ValueList

`ValueList<T>` is a tiny immutable list optimized for very small collections. It works well when a value object or helper only needs to carry a handful of items.

Use it when:

- most cases contain zero, one, or two items
- you want simple immutable append-style usage
- a full `List<T>` would be unnecessary overhead

Example:

```csharp
var tags = default(ValueList<string>)
    .Add("important")
    .Add("internal");

foreach (var tag in tags.AsEnumerable())
{
    Console.WriteLine(tag);
}
```

### PropertyBag

`PropertyBag` stores flexible named values with typed reads and optional typed keys. It works well for metadata, context, ad-hoc attributes, and extension points.

Use it when:

- you need named values without creating a dedicated class
- callers should read values back in a typed way
- metadata needs to travel alongside a request, event, or object

Example:

```csharp
var bag = new PropertyBag();
bag.Set("tenantId", "acme");
bag.Set("retryCount", 3);

var tenantId = bag.Get<string>("tenantId");
var retryCount = bag.Get<int>("retryCount");
```

### SafeDictionary

`SafeDictionary<TKey, TValue>` behaves like a normal mutable dictionary, but missing keys return the default value instead of throwing. For string keys it is case-insensitive by default.

Use it when:

- missing keys are expected and should be harmless
- callers prefer simple indexer access
- string-key lookups should be case-insensitive

Example:

```csharp
var values = new SafeDictionary<string, int>();
values["Retries"] = 3;

var retries = values["retries"];
var missing = values["unknown"]; // returns 0
```

### Enumeration And Smart Enumeration

`Enumeration` is the devkit's smart-enum base type. It lets you model fixed values as rich types instead of plain enums, while still supporting lookup by id or value.

Use it when:

- a fixed set of options needs behavior or metadata
- ids and display values both matter
- you want stronger domain semantics than a plain `enum`

Example:

```csharp
public sealed class OrderStatus : Enumeration
{
    public static readonly OrderStatus Draft = new(1, "Draft");
    public static readonly OrderStatus Submitted = new(2, "Submitted");

    private OrderStatus(int id, string value) : base(id, value) { }
}

var status = Enumeration.FromValue<OrderStatus>("Submitted");
var allStatuses = Enumeration.GetAll<OrderStatus>();
```

## Data And Content Helpers

### Content Types

The content-type helpers define a `ContentType` model plus extension methods for:

- resolving from MIME type
- resolving from file name
- resolving from file extension
- reading metadata such as `MimeType()`, `FileExtension()`, `IsText()`, and `IsBinary()`

This is a small but practical utility family for file, document, and HTTP-oriented scenarios.

Example:

```csharp
var contentType = ContentTypeExtensions.FromFileName("report.pdf");
var mimeType = contentType.MimeType();
var isBinary = contentType.IsBinary();
```

### CompressionHelper

`CompressionHelper` compresses and decompresses:

- strings
- byte arrays
- streams

It uses GZip and supports async workflows, making it useful for payload compression, export/import scenarios, and storage pipelines.

Example:

```csharp
var compressed = await CompressionHelper.CompressAsync("hello world");
var original = await CompressionHelper.DecompressAsync(compressed);
```

### HashHelper

`HashHelper` computes hashes for:

- strings
- byte arrays
- streams
- arbitrary objects serialized to JSON

It is handy for fingerprints, change detection, cache keys, and duplicate detection.

Example:

```csharp
var hash1 = HashHelper.Compute("hello world");
var hash2 = HashHelper.Compute(new { Id = 42, Name = "Alice" });
```

### CloneHelper And CloneHelperNew

`CloneHelper` performs deep cloning through serialization-based copying. `CloneHelperNew` appears to be the newer alternative that exists alongside the original helper.

These helpers are useful when:

- a defensive deep copy is needed
- mutable graph state should be duplicated for comparison or sandboxed modification

Because cloning is serialization-based, it is best treated as a utility of convenience rather than a universal object-copy strategy.

Example:

```csharp
var snapshot = CloneHelper.Clone(order);
var snapshot2 = CloneHelperNew.Clone(order);
```

## Id And Key Helpers

The devkit also includes several lightweight generation helpers:

- `GuidGenerator`
- `IdGenerator`
- `KeyGenerator`

These are useful for creating opaque identifiers, random keys, and various short-lived generated values in code that should not hand-roll randomness or string construction repeatedly.

Example:

```csharp
var id = IdGenerator.Create();
var apiKey = KeyGenerator.Create(32);
```

## Factory Helpers

`Factory<T>` and the non-generic `Factory` provide dynamic construction helpers. These are useful in framework-style code, plugin scenarios, or places where types are resolved dynamically and you want the call site to stay terse.

Example:

```csharp
var customer = Factory<Customer>.Create(new Dictionary<string, object>
{
    ["Name"] = "Alice",
    ["Age"] = 42
});

var handler = Factory.Create(typeof(MyHandler), serviceProvider);
```

## Validation Helpers

The devkit includes `FluentValidatorExtensions`, including `AddRangeRule<T>(...)`.

This helper is designed for dynamic validator construction, especially when validation rules are assembled from reflected metadata instead of hard-coded property expressions.

Example:

```csharp
var validator = new InlineValidator<Product>();
var property = typeof(Product).GetProperty(nameof(Product.Price));

validator.AddRangeRule(property, 0m, 9999m, "Price must stay within the allowed range.");
```

## Other Helpers

Several smaller low-level helpers round out this utility set:

- `ValueStopwatch` for lightweight elapsed-time measurement
- `Retry` as a compact retry utility alongside the richer `Retryer`
- smaller clone and guid validation helpers

These are small but useful support pieces that round out the shared utility set.

Example:

```csharp
var stopwatch = ValueStopwatch.StartNew();
await Retry.On<TimeoutException>(
    () => SendAsync(),
    delays: [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250)]);

Console.WriteLine(stopwatch.GetElapsedMilliseconds());
```

## Related Documentation

- [Requester and Notifier](./features-requester-notifier.md)
- [StartupTasks](./features-startuptasks.md)
- [Common Observability Tracing](./common-observability-tracing.md)
- [Common Extensions](./common-extensions.md)
