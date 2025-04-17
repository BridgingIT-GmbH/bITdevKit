namespace BridgingIT.DevKit.Common.Utilities;

using BridgingIT.DevKit.Common.Resiliancy;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides a publish-subscribe pattern for loosely coupled event handling.
/// </summary>
/// <remarks>
/// Initializes a new instance of the Notifier class with the specified settings.
/// </remarks>
/// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
/// <param name="handleErrors">If true, catches and logs exceptions from event handlers; otherwise, throws them. Defaults to false.</param>
/// <param name="pipelineBehaviors">A list of pipeline behaviors to execute before and after event handling, applied in reverse order. Defaults to an empty list.</param>
/// <param name="progress">An optional progress reporter for notification operations. Defaults to null.</param>
/// <example>
/// <code>
/// var notifier = new Notifier(progress: new Progress<NotifierProgress>(p => Console.WriteLine($"Progress: {p.Status}, Handlers: {p.HandlersProcessed}/{p.TotalHandlers}")));
/// notifier.Subscribe<MyEvent>(async (e, ct) => Console.WriteLine(e.Message));
/// await notifier.PublishAsync(new MyEvent { Message = "Hello" }, CancellationToken.None);
/// </code>
/// </example>
public class Notifier(
    ILogger logger = null,
    bool handleErrors = false,
    IEnumerable<INotifcationPipelineBehavior> pipelineBehaviors = null,
    IProgress<NotifierProgress> progress = null)
{
    private readonly Dictionary<Type, List<(INotificationHandler Handler, int Order)>> subscribers = [];
    private readonly List<INotifcationPipelineBehavior> pipelineBehaviors = pipelineBehaviors?.Reverse()?.ToList() ?? [];
    private readonly Lock lockObject = new();
    private readonly IProgress<NotifierProgress> progress = progress;

    /// <summary>
    /// Subscribes a handler to events of the specified type with an optional execution order.
    /// </summary>
    /// <typeparam name="TNotification">The type of event to subscribe to.</typeparam>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <param name="order">The order in which the handler should be executed (lower values execute first). Defaults to 0.</param>
    /// <example>
    /// <code>
    /// var notifier = new Notifier();
    /// notifier.Subscribe<MyEvent>(async (e, ct) => Console.WriteLine(e.Message), order: 1);
    /// </code>
    /// </example>
    public void Subscribe<TNotification>(Func<TNotification, CancellationToken, Task> handler, int order = 0)
    {
        var notificationHandler = new NotificationHandler<TNotification>(handler);
        lock (this.lockObject)
        {
            var notificationType = typeof(TNotification);
            if (!this.subscribers.TryGetValue(notificationType, out var handlers))
                handlers = [];
            {
                this.subscribers[notificationType] = handlers;
            }

            handlers.Add((notificationHandler, order));
            handlers.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
    }

    /// <summary>
    /// Unsubscribes a handler from events of the specified type.
    /// </summary>
    /// <typeparam name="TNotification">The type of event to unsubscribe from.</typeparam>
    /// <param name="handler">The handler to remove.</param>
    /// <example>
    /// <code>
    /// var notifier = new Notifier();
    /// Func<MyEvent, CancellationToken, Task> handler = async (e, ct) => Console.WriteLine(e.Message);
    /// notifier.Subscribe<MyEvent>(handler);
    /// notifier.Unsubscribe<MyEvent>(handler);
    /// </code>
    /// </example>
    public void Unsubscribe<TNotification>(Func<TNotification, CancellationToken, Task> handler)
    {
        lock (this.lockObject)
        {
            var notificationType = typeof(TNotification);
            if (this.subscribers.TryGetValue(notificationType, out var handlers))
            {
                var notificationHandler = new NotificationHandler<TNotification>(handler);
                handlers.RemoveAll(h => h.Handler.Equals(notificationHandler));
                if (handlers.Count == 0)
                {
                    this.subscribers.Remove(notificationType);
                }
            }
        }
    }

    /// <summary>
    /// Publishes an event to all subscribed handlers, applying pipeline behaviors before and after handling.
    /// </summary>
    /// <typeparam name="TNotification">The type of event to publish.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="progress">An optional progress reporter for notification operations. Defaults to null.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<NotifierProgress>(p => Console.WriteLine($"Progress: {p.Status}, Handlers: {p.HandlersProcessed}/{p.TotalHandlers}"));
    /// var notifier = new Notifier();
    /// notifier.Subscribe<MyEvent>(async (e, ct) => Console.WriteLine(e.Message));
    /// await notifier.PublishAsync(new MyEvent { Message = "Hello" }, cts.Token, progress);
    /// cts.Cancel(); // Cancel the operation if needed
    /// </code>
    /// </example>
    public async Task PublishAsync<TNotification>(TNotification notification, IProgress<NotifierProgress> progress = null, CancellationToken cancellationToken = default)
    {
        progress ??= this.progress; // Use instance-level progress if provided
        List<(INotificationHandler Handler, int Order)> handlers;
        lock (this.lockObject)
        {
            if (!this.subscribers.TryGetValue(typeof(TNotification), out var handlerList))
            {
                return;
            }
            handlers = [.. handlerList];
        }

        var handlersProcessed = 0;
        foreach (var (handler, _) in handlers)
        {
            try
            {
                // Apply pipeline behaviors (already reversed during initialization)
                Func<Task> next = async () => await handler.HandleAsync(notification, cancellationToken);
                foreach (var behavior in this.pipelineBehaviors)
                {
                    var currentNext = next;
                    next = () => behavior.HandleAsync(notification, currentNext, cancellationToken);
                }
                await next();
                handlersProcessed++;
                progress?.Report(new NotifierProgress(handlersProcessed, handlers.Count, $"Processed {handlersProcessed} of {handlers.Count} handlers"));
            }
            catch (OperationCanceledException)
            {
                // If the handler was canceled, continue with the next handler
                continue;
            }
            catch (Exception ex) when (handleErrors)
            {
                logger?.LogError(ex, $"Failed to handle notification of type {typeof(TNotification).Name}.");
            }
        }
    }
}

/// <summary>
/// Defines a handler for a specific event type.
/// </summary>
public interface INotificationHandler
{
    Task HandleAsync(object notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements a handler for a specific event type.
/// </summary>
/// <typeparam name="TNotification">The type of event to handle.</typeparam>
public class NotificationHandler<TNotification>(Func<TNotification, CancellationToken, Task> handler) : INotificationHandler, IEquatable<NotificationHandler<TNotification>>
{
    private readonly Func<TNotification, CancellationToken, Task> handler = handler;

    public async Task HandleAsync(object notification, CancellationToken cancellationToken = default)
    {
        if (notification is TNotification typedNotification)
        {
            await this.handler(typedNotification, cancellationToken);
        }
    }

    public bool Equals(NotificationHandler<TNotification> other)
    {
        if (other == null)
        {
            return false;
        }

        return ReferenceEquals(this.handler, other.handler);
    }

    public override bool Equals(object obj) => this.Equals(obj as NotificationHandler<TNotification>);

    public override int GetHashCode() => this.handler.GetHashCode();
}

/// <summary>
/// Defines a pipeline behavior for pre- and post-processing of events.
/// </summary>
public interface INotifcationPipelineBehavior
{
    Task HandleAsync<TNotification>(TNotification notification, Func<Task> next, CancellationToken cancellationToken = default);
}

/// <summary>
/// A sample pipeline behavior that logs event handling.
/// </summary>
public class LoggingPipelineBehavior(ILogger logger) : INotifcationPipelineBehavior
{
    public async Task HandleAsync<TNotification>(TNotification notification, Func<Task> next, CancellationToken cancellationToken = default)
    {
        logger?.LogInformation($"Handling notification of type {typeof(TNotification).Name}");
        try
        {
            await next();
            logger?.LogInformation($"Successfully handled notification of type {typeof(TNotification).Name}");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, $"Failed to notification notification of type {typeof(TNotification).Name}");
            throw;
        }
    }
}

/// <summary>
/// A fluent builder for configuring and creating an Notifier instance.
/// </summary>
public class NotifierBuilder
{
    private bool handleErrors;
    private ILogger logger;
    private readonly List<INotifcationPipelineBehavior> pipelineBehaviors;
    private IProgress<NotifierProgress> progress;

    /// <summary>
    /// Initializes a new instance of the NotifierBuilder.
    /// </summary>
    public NotifierBuilder()
    {
        this.handleErrors = false;
        this.logger = null;
        this.pipelineBehaviors = [];
        this.progress = null;
    }

    /// <summary>
    /// Configures the event aggregator to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The NotifierBuilder instance for chaining.</returns>
    public NotifierBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Adds a pipeline behavior to the event aggregator for pre- and post-processing of events.
    /// </summary>
    /// <param name="behavior">The pipeline behavior to add.</param>
    /// <returns>The NotifierBuilder instance for chaining.</returns>
    public NotifierBuilder AddPipelineBehavior(INotifcationPipelineBehavior behavior)
    {
        this.pipelineBehaviors.Add(behavior);
        return this;
    }

    /// <summary>
    /// Configures the notifier to report progress using the specified progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter to use for notification operations.</param>
    /// <returns>The NotifierBuilder instance for chaining.</returns>
    public NotifierBuilder WithProgress(IProgress<NotifierProgress> progress)
    {
        this.progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured Notifier instance.
    /// </summary>
    /// <returns>A configured Notifier instance.</returns>
    public Notifier Build()
    {
        return new Notifier(
            this.logger,
            this.handleErrors,
            this.pipelineBehaviors,
            this.progress);
    }
}