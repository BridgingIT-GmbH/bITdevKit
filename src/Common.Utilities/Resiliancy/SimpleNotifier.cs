namespace BridgingIT.DevKit.Common.Utilities;

using BridgingIT.DevKit.Common.Resiliancy;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Threading;

/// <summary>
/// Defines a non-generic base interface for all notifications.
/// </summary>
public interface ISimpleNotification;

/// <summary>
/// Provides a publish-subscribe pattern for loosely coupled event handling.
/// </summary>
/// <remarks>
/// Initializes a new instance of the SimpleNotifier class with the specified settings.
/// </remarks>
/// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
/// <param name="handleErrors">If true, catches and logs exceptions from event handlers; otherwise, throws them. Defaults to false.</param>
/// <param name="pipelineBehaviors">A list of pipeline behaviors to execute before and after event handling, applied in reverse order. Defaults to an empty list.</param>
/// <param name="progress">An optional progress reporter for notification operations. Defaults to null.</param>
/// <example>
/// <code>
/// var notifier = new SimpleNotifier(progress: new Progress<SimpleNotifierProgress>(p => Console.WriteLine($"Progress: {p.Status}, Handlers: {p.HandlersProcessed}/{p.TotalHandlers}")));
/// notifier.Subscribe<MyEvent>(async (e, ct) => Console.WriteLine(e.Message));
/// await notifier.PublishAsync(new MyEvent { Message = "Hello" }, CancellationToken.None);
/// </code>
/// </example>
public class SimpleNotifier(
    ILogger logger = null,
    bool handleErrors = false,
    IEnumerable<ISimpleNotificationPipelineBehavior> pipelineBehaviors = null,
    IProgress<SimpleNotifierProgress> progress = null)
{
    private readonly Dictionary<Type, (ISimpleNotificationHandler Handler, int Order)[]> subscribers = []; // Changed to array for fixed size after subscribe
    private readonly ISimpleNotificationPipelineBehavior[] pipelineBehaviors = pipelineBehaviors?.Reverse()?.ToArray() ?? []; // To array for faster access
    private readonly ReaderWriterLockSlim lockObject = new();
    private readonly IProgress<SimpleNotifierProgress> progress = progress;

    /// <summary>
    /// Subscribes a handler to events of the specified type with an optional execution order.
    /// </summary>
    /// <typeparam name="TNotification">The type of event to subscribe to.</typeparam>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <param name="order">The order in which the handler should be executed (lower values execute first). Defaults to 0.</param>
    /// <example>
    /// <code>
    /// var notifier = new SimpleNotifier();
    /// notifier.Subscribe<MyEvent>(async (e, ct) => Console.WriteLine(e.Message), order: 1);
    /// </code>
    /// </example>
    public void Subscribe<TNotification>(Func<TNotification, CancellationToken, ValueTask> handler, int order = 0)
        where TNotification : ISimpleNotification
    {
        var notificationHandler = new SimpleNotificationHandler<TNotification>(handler);
        this.Subscribe<TNotification>(notificationHandler, order);
    }

    /// <summary>
    /// Subscribes a handler to events of the specified type with an optional execution order.
    /// </summary>
    /// <typeparam name="TNotification">The type of event to subscribe to.</typeparam>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <param name="order">The order in which the handler should be executed (lower values execute first). Defaults to 0.</param>
    /// <example>
    /// <code>
    /// var notifier = new SimpleNotifier();
    /// notifier.Subscribe<MyEvent>(new MyEventHandler(), order: 1);
    /// </code>
    /// </example>
    public void Subscribe<TNotification>(ISimpleNotificationHandler<TNotification> handler, int order = 0)
        where TNotification : ISimpleNotification
    {
        this.lockObject.EnterWriteLock();
        try
        {
            var notificationType = typeof(TNotification);
            List<(ISimpleNotificationHandler, int)> handlers = [.. this.subscribers.GetValueOrDefault(notificationType, [])]; // Copy to list for modification
            handlers.Add((handler, order));
            handlers.Sort(static (a, b) => a.Item2.CompareTo(b.Item2)); // Static lambda to avoid allocation
            this.subscribers[notificationType] = [.. handlers]; // To array
        }
        finally
        {
            this.lockObject.ExitWriteLock();
        }
    }

    /// <summary>
    /// Subscribes a handler to events of the specified type with an optional execution order.
    /// </summary>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <param name="order">The order in which the handler should be executed (lower values execute first). Defaults to 0.</param>
    /// <example>
    /// <code>
    /// var notifier = new SimpleNotifier();
    /// notifier.Subscribe(new MyEventHandler(), order: 1);
    /// </code>
    /// </example>
    public void Subscribe(ISimpleNotificationHandler handler, int order = 0)
    {
        var handlerType = handler.GetType();
        var interfaceType = handlerType.GetInterfaces().FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISimpleNotificationHandler<>));
        if (interfaceType == null)
        {
            throw new InvalidOperationException("The handler does not implement ISimpleNotificationHandler<TNotification>.");
        }

        var tNotification = interfaceType.GetGenericArguments()[0];
        var method = this.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).First(static m => m.Name == nameof(Subscribe) && m.GetParameters().Length == 2 && m.GetGenericArguments().Length == 1 && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(ISimpleNotificationHandler<>));
        var genericMethod = method.MakeGenericMethod(tNotification);
        genericMethod.Invoke(this, [handler, order]);
    }

    /// <summary>
    /// Subscribes a handler to events of the specified type with an optional execution order.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler to instantiate and subscribe.</typeparam>
    /// <param name="order">The order in which the handler should be executed (lower values execute first). Defaults to 0.</param>
    /// <example>
    /// <code>
    /// var notifier = new SimpleNotifier();
    /// notifier.Subscribe<MyEventHandler>(order: 1);
    /// </code>
    /// </example>
    public void Subscribe<THandler>(int order = 0) where THandler : new()
    {
        var instance = new THandler();
        this.Subscribe(instance as ISimpleNotificationHandler, order);
    }

    /// <summary>
    /// Unsubscribes a handler from events of the specified type.
    /// </summary>
    /// <typeparam name="TNotification">The type of event to unsubscribe from.</typeparam>
    /// <param name="handler">The handler to remove.</param>
    /// <example>
    /// <code>
    /// var notifier = new SimpleNotifier();
    /// Func<MyEvent, CancellationToken, Task> handler = async (e, ct) => Console.WriteLine(e.Message);
    /// notifier.Subscribe<MyEvent>(handler);
    /// notifier.Unsubscribe<MyEvent>(handler);
    /// </code>
    /// </example>
    public void Unsubscribe<TNotification>(Func<TNotification, CancellationToken, ValueTask> handler)
        where TNotification : ISimpleNotification
    {
        var notificationHandler = new SimpleNotificationHandler<TNotification>(handler);
        this.Unsubscribe<TNotification>(notificationHandler);
    }

    /// <summary>
    /// Unsubscribes a handler from events of the specified type.
    /// </summary>
    /// <typeparam name="TNotification">The type of event to unsubscribe from.</typeparam>
    /// <param name="handler">The handler to remove.</param>
    /// <example>
    /// <code>
    /// var notifier = new SimpleNotifier();
    /// var handler = new MyEventHandler();
    /// notifier.Subscribe<MyEvent>(handler);
    /// notifier.Unsubscribe<MyEvent>(handler);
    /// </code>
    /// </example>
    public void Unsubscribe<TNotification>(ISimpleNotificationHandler<TNotification> handler)
        where TNotification : ISimpleNotification
    {
        this.lockObject.EnterWriteLock();
        try
        {
            var notificationType = typeof(TNotification);
            if (this.subscribers.TryGetValue(notificationType, out var handlerArray))
            {
                List<(ISimpleNotificationHandler, int)> handlers = [.. handlerArray];
                handlers.RemoveAll(h => h.Item1 == handler);
                this.subscribers[notificationType] = [.. handlers]; // Back to array
                if (handlers.Count == 0)
                {
                    this.subscribers.Remove(notificationType);
                }
            }
        }
        finally
        {
            this.lockObject.ExitWriteLock();
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
    /// var progress = new Progress<SimpleNotifierProgress>(p => Console.WriteLine($"Progress: {p.Status}, Handlers: {p.HandlersProcessed}/{p.TotalHandlers}"));
    /// var notifier = new SimpleNotifier();
    /// notifier.Subscribe<MyEvent>(async (e, ct) => Console.WriteLine(e.Message));
    /// await notifier.PublishAsync(new MyEvent { Message = "Hello" }, cts.Token, progress);
    /// cts.Cancel(); // Cancel the operation if needed
    /// </code>
    /// </example>
    public async Task PublishAsync<TNotification>(TNotification notification, IProgress<SimpleNotifierProgress> progress = null, CancellationToken cancellationToken = default)
        where TNotification : ISimpleNotification
    {
        progress ??= this.progress; // Use instance-level progress if provided
        (ISimpleNotificationHandler Handler, int Order)[] handlers;
        this.lockObject.EnterReadLock();
        try
        {
            if (!this.subscribers.TryGetValue(typeof(TNotification), out handlers))
            {
                return;
            }
            handlers = [.. handlers]; // Snapshot copy (allocation, but necessary for safety)
        }
        finally
        {
            this.lockObject.ExitReadLock();
        }

        if (handlers.Length == 0)
        {
            return;
        }

        var handlersProcessed = 0;
        if (this.pipelineBehaviors.Length == 0)
        {
            foreach (var (handler, _) in handlers)
            {
                try
                {
                    await ((ISimpleNotificationHandler<TNotification>)handler).HandleAsync(notification, cancellationToken);
                    handlersProcessed++;
                    progress?.Report(new SimpleNotifierProgress(handlersProcessed, handlers.Length, $"Processed {handlersProcessed} of {handlers.Length} handlers"));
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (Exception ex) when (handleErrors)
                {
                    logger?.LogError(ex, $"Failed to handle notification of type {typeof(TNotification).Name}.");
                }
            }
        }
        else
        {
            foreach (var (handler, _) in handlers)
            {
                try
                {
                    MessageHandlerDelegate next = () => ((ISimpleNotificationHandler<TNotification>)handler).HandleAsync(notification, cancellationToken);
                    for (var i = this.pipelineBehaviors.Length - 1; i >= 0; i--)
                    {
                        var behavior = this.pipelineBehaviors[i];
                        var currentNext = next;
                        next = () => behavior.HandleAsync(notification, currentNext, cancellationToken);
                    }
                    await next();
                    handlersProcessed++;
                    progress?.Report(new SimpleNotifierProgress(handlersProcessed, handlers.Length, $"Processed {handlersProcessed} of {handlers.Length} handlers"));
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (Exception ex) when (handleErrors)
                {
                    logger?.LogError(ex, $"Failed to handle notification of type {typeof(TNotification).Name}.");
                }
            }
        }
    }
}

/// <summary>
/// Non-generic base interface for notification handlers.
/// </summary>
public interface ISimpleNotificationHandler
{
    ValueTask HandleAsync(object notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a handler for a specific event type.
/// </summary>
/// <typeparam name="TNotification">The type of event to handle.</typeparam>
public interface ISimpleNotificationHandler<in TNotification> : ISimpleNotificationHandler
    where TNotification : ISimpleNotification
{
    ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken = default);

#pragma warning disable CS1066 // The default value specified will have no effect because it applies to a member that is used in contexts that do not allow optional arguments
    ValueTask ISimpleNotificationHandler.HandleAsync(object notification, CancellationToken cancellationToken = default)
#pragma warning restore CS1066 // The default value specified will have no effect because it applies to a member that is used in contexts that do not allow optional arguments
    {
        if (notification is TNotification typedNotification)
        {
            return this.HandleAsync(typedNotification, cancellationToken);
        }

        throw new InvalidOperationException($"Notification type {notification.GetType().Name} does not match expected type {typeof(TNotification).Name}.");
    }
}

/// <summary>
/// Implements a handler for a specific event type using a function.
/// </summary>
/// <typeparam name="TNotification">The type of event to handle.</typeparam>
public class SimpleNotificationHandler<TNotification>(Func<TNotification, CancellationToken, ValueTask> handler) : ISimpleNotificationHandler<TNotification>, IEquatable<SimpleNotificationHandler<TNotification>>
    where TNotification : ISimpleNotification
{
    private readonly Func<TNotification, CancellationToken, ValueTask> handler = handler;

    public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken = default)
    {
        return this.handler(notification, cancellationToken);
    }

    ValueTask ISimpleNotificationHandler.HandleAsync(object notification, CancellationToken cancellationToken)
    {
        if (notification is TNotification typedNotification)
        {
            return this.HandleAsync(typedNotification, cancellationToken);
        }

        throw new InvalidOperationException($"Notification type {notification.GetType().Name} does not match expected type {typeof(TNotification).Name}.");
    }

    public bool Equals(SimpleNotificationHandler<TNotification> other)
    {
        if (other == null)
        {
            return false;
        }

        return ReferenceEquals(this.handler, other.handler);
    }

    public override bool Equals(object obj) => this.Equals(obj as SimpleNotificationHandler<TNotification>);

    public override int GetHashCode() => this.handler.GetHashCode();
}

/// <summary>
/// Defines a pipeline behavior for pre- and post-processing of events.
/// </summary>
public interface ISimpleNotificationPipelineBehavior
{
    ValueTask HandleAsync(ISimpleNotification notification, MessageHandlerDelegate next, CancellationToken cancellationToken = default);
}

/// <summary>
/// Delegate for message handler in pipeline.
/// </summary>
public delegate ValueTask MessageHandlerDelegate();

/// <summary>
/// A sample pipeline behavior that logs event handling.
/// </summary>
public class LoggingNotificationPipelineBehavior(ILogger logger) : ISimpleNotificationPipelineBehavior
{
    public async ValueTask HandleAsync(ISimpleNotification notification, MessageHandlerDelegate next, CancellationToken cancellationToken = default)
    {
        logger?.LogInformation($"Handling notification of type {notification.GetType().Name}");
        try
        {
            await next();
            logger?.LogInformation($"Successfully handled notification of type {notification.GetType().Name}");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, $"Failed to handle notification of type {notification.GetType().Name}");
            throw;
        }
    }
}

/// <summary>
/// A fluent builder for configuring and creating an SimpleNotifier instance.
/// </summary>
public class SimpleNotifierBuilder
{
    private bool handleErrors;
    private ILogger logger;
    private readonly List<ISimpleNotificationPipelineBehavior> pipelineBehaviors = [];
    private IProgress<SimpleNotifierProgress> progress;

    /// <summary>
    /// Initializes a new instance of the SimpleNotifierBuilder.
    /// </summary>
    public SimpleNotifierBuilder()
    {
        this.handleErrors = false;
        this.logger = null;
        this.progress = null;
    }

    /// <summary>
    /// Configures the event aggregator to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The SimpleNotifierBuilder instance for chaining.</returns>
    public SimpleNotifierBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Adds a pipeline behavior to the event aggregator for pre- and post-processing of events.
    /// </summary>
    /// <param name="behavior">The pipeline behavior to add.</param>
    /// <returns>The SimpleNotifierBuilder instance for chaining.</returns>
    public SimpleNotifierBuilder AddPipelineBehavior(ISimpleNotificationPipelineBehavior behavior)
    {
        this.pipelineBehaviors.Add(behavior);
        return this;
    }

    /// <summary>
    /// Configures the notifier to report progress using the specified progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter to use for notification operations.</param>
    /// <returns>The SimpleNotifierBuilder instance for chaining.</returns>
    public SimpleNotifierBuilder WithProgress(IProgress<SimpleNotifierProgress> progress)
    {
        this.progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured SimpleNotifier instance.
    /// </summary>
    /// <returns>A configured SimpleNotifier instance.</returns>
    public SimpleNotifier Build()
    {
        return new SimpleNotifier(
            this.logger,
            this.handleErrors,
            this.pipelineBehaviors,
            this.progress);
    }
}