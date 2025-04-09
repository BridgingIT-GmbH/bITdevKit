# Utilities Feature Documentation

[TOC]

## Startup Tasks

[see ./features-startuptasks.md](./features-startuptasks.md)

## Resiliency

### Overview

The **Resiliency** utilities are a collection of robust, reusable components designed to enhance the reliability and fault tolerance of .NET applications. These utilities address common challenges in asynchronous programming, resource management, and system stability, making them suitable for both frontend (e.g., Blazor) and backend scenarios. The suite includes tools for retrying failed operations, managing timing, isolating failures, handling notifications, processing background tasks, managing request/response patterns, enforcing timeouts, and limiting concurrency.

#### Key Features
- **Cancellation Support**: All utilities support `CancellationToken` for async operations.
- **Error Handling**: Configurable error handling with optional logging.
- **Thread Safety**: Ensures safe concurrent usage with locks and semaphores.
- **Fluent Builders**: Provides a flexible configuration API.
- **Extensibility**: Includes pipeline behaviors for pre- and post-processing.
- **Progress Reporting**: Each utility supports specific progress reporting via `IProgress<T>` with a custom progress type (e.g., `RetryProgress`, `DebouncerProgress`), inheriting from the abstract `ResiliencyProgress` base class, configurable through the fluent builder.

### 1. Retryer
- **Purpose**: Retries an operation on transient failures with configurable delays and exponential backoff.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var progress = new Progress<RetryProgress>(p => Console.WriteLine($"Retry Attempt: {p.CurrentAttempt}/{p.MaxAttempts}, Delay: {p.Delay.TotalSeconds}s"));
  var retryer = new RetryerBuilder(3, TimeSpan.FromSeconds(1))
      .UseExponentialBackoff()
      .HandleErrors(new ConsoleLogger())
      .WithProgress(progress)
      .Build();
  await retryer.ExecuteAsync(async ct =>
  {
      await Task.Delay(100, ct);
      if (new Random().Next(2) == 0) throw new Exception("Transient failure");
      Console.WriteLine("Success");
  }, cts.Token);
  ```
- **Configuration Options**:
  - `maxRetries`: Number of retry attempts (minimum 1).
  - `delay`: Initial delay between retries.
  - `useExponentialBackoff`: Increases delay exponentially (e.g., 1s, 2s, 4s).
  - `handleErrors`: Log errors instead of throwing (with optional logger).
  - `progress`: An optional progress reporter of type `IProgress<RetryProgress>` for retry operations (configured via `WithProgress`).
- **Best Practices**:
  - Use exponential backoff for network operations to handle backpressure.
  - Set a reasonable `maxRetries` to avoid infinite loops.
  - Use `IProgress<RetryProgress>` to monitor specific retry details (e.g., `CurrentAttempt`) in real-time.

### 2. Debouncer
- **Purpose**: Delays or throttles an action until a specified interval has passed since the last call.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var progress = new Progress<DebouncerProgress>(p => Console.WriteLine($"Progress: {p.Status}, Remaining: {p.RemainingDelay.TotalSeconds}s, Throttling: {p.IsThrottling}"));
  var debouncer = new DebouncerBuilder(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"))
      .ExecuteImmediatelyOnFirstCall()
      .WithProgress(progress)
      .Build();
  await debouncer.DebounceAsync(cts.Token);
  ```
- **Configuration Options**:
  - `delay`: Delay interval for debouncing or minimum interval for throttling.
  - `executeImmediatelyOnFirstCall`: Executes immediately on the first call in debounce mode.
  - `progress`: An optional progress reporter of type `IProgress<DebouncerProgress>` for debouncing/throttling operations (configured via `WithProgress`).
- **Best Practices**:
  - Use debouncing for UI input handling (e.g., search queries).
  - Use `IProgress<DebouncerProgress>` to monitor delay or IsThrottling status in real-time.

### 3. Throttler
- **Purpose**: Rate-limits an action, executing it immediately and then at fixed intervals during rapid calls.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var progress = new Progress<ThrottlerProgress>(p => Console.WriteLine($"Progress: {p.Status}, Remaining: {p.RemainingInterval.TotalSeconds}s"));
  var throttler = new ThrottlerBuilder(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"))
      .HandleErrors(new ConsoleLogger())
      .WithProgress(progress)
      .Build();
  await throttler.ThrottleAsync(cts.Token);
  await Task.Delay(1100);
  await throttler.ThrottleAsync(cts.Token);
  ```
- **Configuration Options**:
  - `interval`: Minimum interval between executions.
  - `handleErrors`: Log errors instead of throwing (with optional logger).
  - `progress`: An optional progress reporter of type `IProgress<ThrottlerProgress>` for throttling operations (configured via `WithProgress`).
- **Best Practices**:
  - Use for API rate limiting or resource-intensive operations.
  - Adjust `interval` based on system load.
  - Use `IProgress<ThrottlerProgress>` to monitor throttling delays in real-time.

### 4. CircuitBreaker
- **Purpose**: Prevents repeated calls to a failing operation, allowing recovery with states (Closed, Open, Half-Open).
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var progress = new Progress<CircuitBreakerProgress>(p => Console.WriteLine($"Progress: {p.Status}, State: {p.State}, Failures: {p.FailureCount}, ResetTimeout: {p.ResetTimeout.TotalSeconds}s"));
  var circuitBreaker = new CircuitBreakerBuilder(3, TimeSpan.FromSeconds(30))
      .HandleErrors(new ConsoleLogger())
      .WithProgress(progress)
      .Build();
  await circuitBreaker.ExecuteAsync(async ct =>
  {
      await Task.Delay(100, ct);
      if (new Random().Next(2) == 0) throw new Exception("Operation failed");
      Console.WriteLine("Success");
  }, cts.Token);
  ```
- **Configuration Options**:
  - `failureThreshold`: Number of failures to open the circuit.
  - `resetTimeout`: Duration to wait before transitioning to Half-Open.
  - `handleErrors`: Log errors instead of throwing (with optional logger).
  - `progress`: An optional progress reporter of type `IProgress<CircuitBreakerProgress>` for circuit breaker operations (configured via `WithProgress`).
- **Best Practices**:
  - Set a low `failureThreshold` for critical dependencies.
  - Use with `Retryer` for combined resilience.
  - Use `IProgress<CircuitBreakerProgress>` to monitor circuit state changes in real-time.

### 5. RateLimiter
- **Purpose**: Enforces a maximum rate of operations within a time window using a sliding window algorithm.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var progress = new Progress<RateLimiterProgress>(p => Console.WriteLine($"Progress: {p.Status}, Operations: {p.CurrentOperations}/{p.MaxOperations}, Window: {p.Window.TotalSeconds}s"));
  var rateLimiter = new RateLimiterBuilder(5, TimeSpan.FromSeconds(10))
      .HandleErrors(new ConsoleLogger())
      .WithProgress(progress)
      .Build();
  await rateLimiter.ExecuteAsync(async ct =>
  {
      await Task.Delay(100, ct);
      Console.WriteLine("Operation executed");
  }, cts.Token);
  ```
- **Configuration Options**:
  - `maxOperations`: Maximum operations allowed in the window.
  - `window`: Time window for rate limiting.
  - `handleErrors`: Log errors instead of throwing (with optional logger).
  - `progress`: An optional progress reporter of type `IProgress<RateLimiterProgress>` for rate-limiting operations (configured via `WithProgress`).
- **Best Practices**:
  - Use for API rate limiting to comply with external service constraints.
  - Monitor `window` to match usage patterns.
  - Use `IProgress<RateLimiterProgress>` to track operation counts and rate limit status in real-time.

### 6. Notifier
- **Purpose**: Provides a feature-rich notification system with handler ordering and pipeline behaviors.
- **Supporting Classes**:
  ```csharp
  /// <summary>
  /// A sample notification event.
  /// </summary>
  public class MyEvent : INotification
  {
      public string Message { get; set; }
  }

  /// <summary>
  /// A sample handler for MyEvent notifications.
  /// </summary>
  public class MyEventHandler : INotificationHandler<MyEvent>
  {
      public async Task HandleAsync(MyEvent notification, CancellationToken cancellationToken)
      {
          await Task.Delay(100, cancellationToken); // Simulate processing
          Console.WriteLine($"Handler processed: {notification.Message}");
      }
  }
  ```
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var progress = new Progress<NotifierProgress>(p => Console.WriteLine($"Progress: {p.Status}, Handlers: {p.HandlersProcessed}/{p.TotalHandlers}"));
  var notifier = new NotifierBuilder()
      .HandleErrors(new ConsoleLogger())
      .AddPipelineBehavior(new LoggingPipelineBehavior(new ConsoleLogger()))
      .WithProgress(progress)
      .Build();
  notifier.Subscribe(new MyEventHandler());
  await notifier.PublishAsync(new MyEvent { Message = "Hello" }, cts.Token);
  ```
- **Configuration Options**:
  - `handleErrors`: Log errors instead of throwing (with optional logger).
  - `pipelineBehaviors`: Add pre- and post-processing behaviors.
  - `progress`: An optional progress reporter of type `IProgress<NotifierProgress>` for notification operations (configured via `WithProgress`).
- **Best Practices**:
  - Use pipeline behaviors for logging or validation.
  - Order handlers for sequential processing.
  - Use `IProgress<NotifierProgress>` to monitor the number of handlers processed in real-time.

### 7. BackgroundWorker
- **Purpose**: Manages long-running background tasks with cancellation and progress reporting.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var progress = new Progress<BackgroundWorkerProgress>(p => Console.WriteLine($"Progress: {p.Status}, Percentage: {p.ProgressPercentage}%"));
  var worker = new BackgroundWorkerBuilder(async (ct, p) =>
  {
      for (int i = 0; i <= 100; i += 10) 
      { 
          await Task.Delay(100, ct); 
          p.Report(i); 
      }
  })
      .HandleErrors(new ConsoleLogger())
      .WithProgress(progress)
      .Build();
  worker.ProgressChanged += (s, e) => Console.WriteLine($"Legacy Progress: {e.ProgressPercentage}%");
  await worker.StartAsync(cts.Token);
  ```
- **Configuration Options**:
  - `handleErrors`: Log errors instead of throwing (with optional logger).
  - `progress`: An optional progress reporter of type `IProgress<BackgroundWorkerProgress>` for background operations (configured via `WithProgress`).
- **Best Practices**:
  - Use for file processing or data imports.
  - Handle `ProgressChanged` for UI updates using the legacy event, or use `IProgress<BackgroundWorkerProgress>` for custom updates.
  - Use `IProgress<BackgroundWorkerProgress>` to monitor task execution in real-time.

### 8. Requester
- **Purpose**: Handles request/response patterns with a single handler per request type.
- **Supporting Classes**:
  ```csharp
  /// <summary>
  /// A sample request class.
  /// </summary>
  public class MyRequest : IRequest<MyResponse>
  {
      public string Data { get; set; }
  }

  /// <summary>
  /// A sample response class.
  /// </summary>
  public class MyResponse
  {
      public string Result { get; set; }
  }

  /// <summary>
  /// A sample handler for MyRequest.
  /// </summary>
  public class MyRequestHandler : IRequestHandler<MyRequest, MyResponse>
  {
      public async Task<MyResponse> HandleAsync(MyRequest request, CancellationToken cancellationToken)
      {
          await Task.Delay(100, cancellationToken); // Simulate work
          return new MyResponse { Result = $"Processed: {request.Data}" };
      }
  }
  ```
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var progress = new Progress<RequesterProgress>(p => Console.WriteLine($"Progress: {p.Status}, Request Type: {p.RequestType}"));
  var requester = new RequesterBuilder()
      .HandleErrors(new ConsoleLogger())
      .AddPipelineBehavior(new LoggingRequestPipelineBehavior(new ConsoleLogger()))
      .WithProgress(progress)
      .Build();
  requester.RegisterHandler(new MyRequestHandler());
  var response = await requester.SendAsync(new MyRequest { Data = "Test" }, cts.Token);
  Console.WriteLine(response.Result);
  ```
- **Configuration Options**:
  - `handleErrors`: Log errors instead of throwing (with optional logger).
  - `pipelineBehaviors`: Add pre- and post-processing behaviors.
  - `progress`: An optional progress reporter of type `IProgress<RequesterProgress>` for request operations (configured via `WithProgress`).
- **Best Practices**:
  - Use for commands or queries in a CQRS pattern.
  - Validate requests in pipeline behaviors.
  - Use `IProgress<RequesterProgress>` to monitor request processing in real-time.

### 9. TimeoutHandler
- **Purpose**: Enforces a timeout on operations, canceling them if they exceed the duration.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var progress = new Progress<TimeoutHandlerProgress>(p => Console.WriteLine($"Progress: {p.Status}, Remaining: {p.RemainingTime.TotalSeconds}s"));
  var timeoutHandler = new TimeoutHandlerBuilder(TimeSpan.FromSeconds(2))
      .HandleErrors(new ConsoleLogger())
      .WithProgress(progress)
      .Build();
  await timeoutHandler.ExecuteAsync(async ct =>
  {
      await Task.Delay(3000, ct); // This will timeout
      Console.WriteLine("Operation completed");
  }, cts.Token);
  ```
- **Configuration Options**:
  - `timeout`: Maximum duration before cancellation.
  - `handleErrors`: Log errors instead of throwing (with optional logger).
  - `progress`: An optional progress reporter of type `IProgress<TimeoutHandlerProgress>` for timeout operations (configured via `WithProgress`).
- **Best Practices**:
  - Use for time-sensitive operations like API calls.
  - Combine with `Retryer` for robust retries.
  - Use `IProgress<TimeoutHandlerProgress>` to monitor remaining time in real-time.

### 10. Bulkhead
- **Purpose**: Isolates failures by limiting concurrent operations with a semaphore.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var progress = new Progress<BulkheadProgress>(p => Console.WriteLine($"Progress: {p.Status}, Concurrency: {p.CurrentConcurrency}/{p.MaxConcurrency}, Queued: {p.QueuedTasks}"));
  var bulkhead = new BulkheadBuilder(2)
      .HandleErrors(new ConsoleLogger())
      .WithProgress(progress)
      .Build();
  await bulkhead.ExecuteAsync(async ct =>
  {
      await Task.Delay(1000, ct);
      Console.WriteLine("Operation completed");
  }, cts.Token);
  ```
- **Configuration Options**:
  - `maxConcurrency`: Maximum number of concurrent operations.
  - `handleErrors`: Log errors instead of throwing (with optional logger).
  - `progress`: An optional progress reporter of type `IProgress<BulkheadProgress>` for bulkhead operations (configured via `WithProgress`).
- **Best Practices**:
  - Use to isolate resource-intensive tasks.
  - Monitor queued tasks for performance tuning.
  - Use `IProgress<BulkheadProgress>` to track concurrency and queue status in real-time.

---

## Best Practices

- **Combine Utilities**: Use `Retryer` with `CircuitBreaker` for robust failure handling, or `TimeoutHandler` with `RateLimiter` for time-sensitive rate-limited operations.
- **Logging**: Always provide a logger when `handleErrors` is true to capture diagnostic information.
- **Cancellation**: Pass `CancellationToken` instances to support graceful shutdowns and user-initiated cancellations.
- **Testing**: Test utilities with edge cases (e.g., timeouts, high concurrency) to ensure resilience.
- **Configuration**: Adjust parameters (e.g., `maxRetries`, `timeout`) based on application needs and performance metrics.
- **Progress Monitoring**: Leverage the specific progress reporting feature for each utility (e.g., `IProgress<RetryProgress>` for `Retryer`) to monitor operation status in real-time. Use the `WithProgress` method in the fluent builder to configure progress reporting, and ensure progress handlers are lightweight to avoid performance overhead.

---

## Appendix: Handling Progress Reporting

### Overview

The **Resiliency** utilities provide progress reporting through specific `IProgress<T>` implementations (e.g., `IProgress<RetryProgress>`, `IProgress<DebouncerProgress>`), allowing clients to monitor operation status in real-time. A console application serves as a simple host to demonstrate how to integrate and utilize these progress updates. This appendix outlines a general approach to handling progress from a client perspective, adaptable to various hosting environments.

### Implementing Progress Handling

To handle progress updates as a client:
1. Create an instance of the appropriate `Progress<T>` class, where `T` is the specific progress type for the utility (e.g., `Progress<RetryProgress>`).
2. Provide a callback to process the progress data, which can display, log, or otherwise react to the updates.
3. Configure the utility with the progress instance using the fluent builder’s `WithProgress` method.

### Example: Demonstrating Progress Handling

Here’s a minimal example using a console app to illustrate progress handling across utilities:

```csharp
using BridgingIT.DevKit.Common.Utilities;
using System;
using System.Threading;

class Program
{
    static async Task Main(string[] args)
    {
        // Example with Retryer
        var retryProgress = new Progress<RetryProgress>(p => Console.WriteLine($"Progress: Attempt {p.CurrentAttempt}/{p.MaxAttempts}, Delay: {p.Delay.TotalSeconds}s"));
        var retryer = new RetryerBuilder(3, TimeSpan.FromSeconds(1))
            .WithProgress(retryProgress)
            .Build();
        await retryer.ExecuteAsync(ct => ThrowRandom(ct), CancellationToken.None);

        // Example with TimeoutHandler
        var timeoutProgress = new Progress<TimeoutHandlerProgress>(p => Console.WriteLine($"Progress: Remaining {p.RemainingTime.TotalSeconds}s"));
        var timeoutHandler = new TimeoutHandlerBuilder(TimeSpan.FromSeconds(2))
            .WithProgress(timeoutProgress)
            .Build();
        await timeoutHandler.ExecuteAsync(ct => LongTask(ct), CancellationToken.None);
    }

    static async Task ThrowRandom(CancellationToken ct)
    {
        await Task.Delay(100, ct);
        if (new Random().Next(2) == 0) throw new Exception("Transient failure");
    }

    static async Task LongTask(CancellationToken ct)
    {
        await Task.Delay(3000, ct); // Simulates a long task
    }
}
```
- **Explanation**: This example shows how to attach a `Progress<T>` instance to different utilities (`Retryer` and `TimeoutHandler`). The callback logs progress updates to the console, demonstrating how clients can react to progress data. The specific properties (e.g., `CurrentAttempt`, `RemainingTime`) are accessed directly via the custom progress type.

### General Guidelines
- **Flexibility**: The `Progress<T>` callback can be adapted to any hosting environment (console, UI, logging framework) by adjusting the callback logic.
- **Lightweight Callbacks**: Keep the callback logic minimal to avoid performance overhead, such as using simple console output or asynchronous logging.
- **Context Awareness**: Use the specific progress type’s properties (e.g., `RemainingInterval` for `ThrottlerProgress`) to provide meaningful feedback tailored to the utility.
- **Cancellation Support**: Ensure the callback handles cancellation scenarios gracefully, especially for long-running operations.

### Notes
- Each utility’s progress type (e.g., `RetryProgress`, `TimeoutHandlerProgress`) inherits from `ResiliencyProgress`, enabling type-safe access to utility-specific data.
- Clients can extend this approach to other environments (e.g., WPF, ASP.NET) by replacing console output with UI updates or API responses.
- 