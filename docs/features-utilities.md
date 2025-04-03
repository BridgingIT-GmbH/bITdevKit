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

### 1. Retryer
- **Purpose**: Retries an operation on transient failures with configurable delays and exponential backoff.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var retryer = new RetryerBuilder(3, TimeSpan.FromSeconds(1))
      .UseExponentialBackoff()
      .HandleErrors(new ConsoleLogger())
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
- **Best Practices**:
  - Use exponential backoff for network operations to handle backpressure.
  - Set a reasonable `maxRetries` to avoid infinite loops.

### 2. Debouncer
- **Purpose**: Delays or throttles an action until a specified interval has passed since the last call.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var debouncer = new DebouncerBuilder(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"))
      .ExecuteImmediatelyOnFirstCall()
      .UseThrottling() // Optional: Switch to throttling mode
      .HandleErrors(new ConsoleLogger())
      .Build();
  await debouncer.DebounceAsync(cts.Token);
  ```
- **Configuration Options**:
  - `delay`: Delay interval for debouncing or minimum interval for throttling.
  - `executeImmediatelyOnFirstCall`: Executes immediately on the first call in debounce mode.
  - `useThrottling`: Switches to throttling mode (immediate execution with fixed intervals).
  - `handleErrors`: Log errors instead of throwing (with optional logger).
- **Best Practices**:
  - Use debouncing for UI input handling (e.g., search queries).
  - Use throttling for rate-limiting frequent updates.

### 3. Throttler
- **Purpose**: Rate-limits an action, executing it immediately and then at fixed intervals during rapid calls.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var throttler = new ThrottlerBuilder(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"))
      .HandleErrors(new ConsoleLogger())
      .Build();
  await throttler.ThrottleAsync(cts.Token);
  await Task.Delay(1100);
  await throttler.ThrottleAsync(cts.Token);
  ```
- **Configuration Options**:
  - `interval`: Minimum interval between executions.
  - `handleErrors`: Log errors instead of throwing (with optional logger).
- **Best Practices**:
  - Use for API rate limiting or resource-intensive operations.
  - Adjust `interval` based on system load.

### 4. CircuitBreaker
- **Purpose**: Prevents repeated calls to a failing operation, allowing recovery with states (Closed, Open, Half-Open).
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var circuitBreaker = new CircuitBreakerBuilder(3, TimeSpan.FromSeconds(30))
      .HandleErrors(new ConsoleLogger())
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
- **Best Practices**:
  - Set a low `failureThreshold` for critical dependencies.
  - Use with `Retryer` for combined resilience.

### 5. RateLimiter
- **Purpose**: Enforces a maximum rate of operations within a time window using a sliding window algorithm.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var rateLimiter = new RateLimiterBuilder(5, TimeSpan.FromSeconds(10))
      .HandleErrors(new ConsoleLogger())
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
- **Best Practices**:
  - Use for API rate limiting to comply with external service constraints.
  - Monitor `window` to match usage patterns.

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
  var notifier = new NotifierBuilder()
      .HandleErrors(new ConsoleLogger())
      .AddPipelineBehavior(new LoggingPipelineBehavior(new ConsoleLogger()))
      .Build();
  notifier.Subscribe(new MyEventHandler());
  await notifier.PublishAsync(new MyEvent { Message = "Hello" }, cts.Token);
  ```
- **Configuration Options**:
  - `handleErrors`: Log errors instead of throwing (with optional logger).
  - `pipelineBehaviors`: Add pre- and post-processing behaviors.
- **Best Practices**:
  - Use pipeline behaviors for logging or validation.
  - Order handlers for sequential processing.

### 7. BackgroundWorker
- **Purpose**: Manages long-running background tasks with cancellation and progress reporting.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var worker = new BackgroundWorkerBuilder(async (ct, progress) =>
  {
      for (int i = 0; i <= 100; i += 10) 
      { 
          await Task.Delay(100, ct); 
          progress.Report(i); 
      }
  })
      .HandleErrors(new ConsoleLogger())
      .Build();
  worker.ProgressChanged += (s, e) => Console.WriteLine($"Progress: {e.ProgressPercentage}%");
  await worker.StartAsync(cts.Token);
  ```
- **Configuration Options**:
  - `handleErrors`: Log errors instead of throwing (with optional logger).
- **Best Practices**:
  - Use for file processing or data imports.
  - Handle `ProgressChanged` for UI updates.

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
  var requester = new RequesterBuilder()
      .HandleErrors(new ConsoleLogger())
      .AddPipelineBehavior(new LoggingRequestPipelineBehavior(new ConsoleLogger()))
      .Build();
  requester.RegisterHandler(new MyRequestHandler());
  var response = await requester.SendAsync(new MyRequest { Data = "Test" }, cts.Token);
  Console.WriteLine(response.Result);
  ```
- **Configuration Options**:
  - `handleErrors`: Log errors instead of throwing (with optional logger).
  - `pipelineBehaviors`: Add pre- and post-processing behaviors.
- **Best Practices**:
  - Use for commands or queries in a CQRS pattern.
  - Validate requests in pipeline behaviors.

### 9. TimeoutHandler
- **Purpose**: Enforces a timeout on operations, canceling them if they exceed the duration.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var timeoutHandler = new TimeoutHandlerBuilder(TimeSpan.FromSeconds(2))
      .HandleErrors(new ConsoleLogger())
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
- **Best Practices**:
  - Use for time-sensitive operations like API calls.
  - Combine with `Retryer` for robust retries.

### 10. Bulkhead
- **Purpose**: Isolates failures by limiting concurrent operations with a semaphore.
- **Usage Example**:
  ```csharp
  var cts = new CancellationTokenSource();
  var bulkhead = new BulkheadBuilder(2)
      .HandleErrors(new ConsoleLogger())
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
- **Best Practices**:
  - Use to isolate resource-intensive tasks.
  - Monitor queued tasks for performance tuning.

---

## Best Practices

- **Combine Utilities**: Use `Retryer` with `CircuitBreaker` for robust failure handling, or `TimeoutHandler` with `RateLimiter` for time-sensitive rate-limited operations.
- **Logging**: Always provide a logger when `handleErrors` is true to capture diagnostic information.
- **Cancellation**: Pass `CancellationToken` instances to support graceful shutdowns and user-initiated cancellations.
- **Testing**: Test utilities with edge cases (e.g., timeouts, high concurrency) to ensure resilience.
- **Configuration**: Adjust parameters (e.g., `maxRetries`, `timeout`) based on application needs and performance metrics.