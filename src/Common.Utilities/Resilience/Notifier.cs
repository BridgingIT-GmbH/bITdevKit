// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Utilities;

using Microsoft.Extensions.Logging;

/// <summary>
/// Provides a publish-subscribe pattern for loosely coupled event handling.
/// </summary>
/// <remarks>
/// Initializes a new instance of the EventAggregator class with the specified settings.
/// </remarks>
/// <param name="handleErrors">If true, catches and logs exceptions from event handlers; otherwise, throws them. Defaults to false.</param>
/// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
/// <param name="pipelineBehaviors">A list of pipeline behaviors to execute before and after event handling, applied in reverse order. Defaults to an empty list.</param>
/// <example>
/// <code>
/// var eventAggregator = new EventAggregator();
/// eventAggregator.Subscribe&lt;MyEvent&gt;(async (e, ct) => Console.WriteLine(e.Message));
/// await eventAggregator.PublishAsync(new MyEvent { Message = "Hello" }, CancellationToken.None);
/// </code>
/// </example>
public class Notifier(
    bool handleErrors = false,
    ILogger logger = null,
    IEnumerable<INotifcationPipelineBehavior> pipelineBehaviors = null)
{
    private readonly Dictionary<Type, List<(INotificationHandler Handler, int Order)>> subscribers = [];
    private readonly List<INotifcationPipelineBehavior> pipelineBehaviors = pipelineBehaviors?.Reverse()?.ToList() ?? [];
    private readonly Lock lockObject = new();

    /// <summary>
    /// Subscribes a handler to events of the specified type with an optional execution order.
    /// </summary>
    /// <typeparam name="TNotification">The type of event to subscribe to.</typeparam>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <param name="order">The order in which the handler should be executed (lower values execute first). Defaults to 0.</param>
    /// <example>
    /// <code>
    /// var eventAggregator = new EventAggregator();
    /// eventAggregator.Subscribe&lt;MyEvent&gt;(async (e, ct) => Console.WriteLine(e.Message), order: 1);
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
    /// var eventAggregator = new EventAggregator();
    /// Func&lt;MyEvent, CancellationToken, Task&gt; handler = async (e, ct) => Console.WriteLine(e.Message);
    /// eventAggregator.Subscribe&lt;MyEvent&gt;(handler);
    /// eventAggregator.Unsubscribe&lt;MyEvent&gt;(handler);
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
                handlers.RemoveAll(h => h.Handler.Equals(handler));
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
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var eventAggregator = new EventAggregator();
    /// eventAggregator.Subscribe&lt;MyEvent&gt;(async (e, ct) => Console.WriteLine(e.Message));
    /// await eventAggregator.PublishAsync(new MyEvent { Message = "Hello" }, cts.Token);
    /// cts.Cancel(); // Cancel the operation if needed
    /// </code>
    /// </example>
    public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    {
        List<(INotificationHandler Handler, int Order)> handlers;
        lock (this.lockObject)
        {
            if (!this.subscribers.TryGetValue(typeof(TNotification), out var handlerList))
            {
                return;
            }
            handlers = [.. handlerList];
        }

        foreach (var (handler, _) in handlers)
        {
            try
            {
                // Apply pipeline behaviors (already reversed during initialization)
                Func<Task> next = async () => await handler.HandleAsync(notification, cancellationToken);
                foreach (var behavior in this.pipelineBehaviors)
                {
                    var currentNext = next;
                    next = () => behavior.HandleAsync(notification, cancellationToken, currentNext);
                }
                await next();
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
    Task HandleAsync(object notification, CancellationToken cancellationToken);
}

/// <summary>
/// Implements a handler for a specific event type.
/// </summary>
/// <typeparam name="TNotification">The type of event to handle.</typeparam>
public class NotificationHandler<TNotification>(Func<TNotification, CancellationToken, Task> handler) : INotificationHandler, IEquatable<NotificationHandler<TNotification>>
{
    private readonly Func<TNotification, CancellationToken, Task> handler = handler;

    public async Task HandleAsync(object notification, CancellationToken cancellationToken)
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
    Task HandleAsync<TNotification>(TNotification notification, CancellationToken cancellationToken, Func<Task> next);
}

/// <summary>
/// A sample pipeline behavior that logs event handling.
/// </summary>
public class LoggingPipelineBehavior(ILogger logger) : INotifcationPipelineBehavior
{
    public async Task HandleAsync<TNotification>(TNotification notification, CancellationToken cancellationToken, Func<Task> next)
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
/// A fluent builder for configuring and creating an EventAggregator instance.
/// </summary>
public class NotifierBuilder
{
    private bool handleErrors;
    private ILogger logger;
    private readonly List<INotifcationPipelineBehavior> pipelineBehaviors;

    /// <summary>
    /// Initializes a new instance of the EventAggregatorBuilder.
    /// </summary>
    public NotifierBuilder()
    {
        this.handleErrors = false;
        this.logger = null;
        this.pipelineBehaviors = [];
    }

    /// <summary>
    /// Configures the event aggregator to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The EventAggregatorBuilder instance for chaining.</returns>
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
    /// <returns>The EventAggregatorBuilder instance for chaining.</returns>
    public NotifierBuilder AddPipelineBehavior(INotifcationPipelineBehavior behavior)
    {
        this.pipelineBehaviors.Add(behavior);
        return this;
    }

    /// <summary>
    /// Builds and returns a configured EventAggregator instance.
    /// </summary>
    /// <returns>A configured EventAggregator instance.</returns>
    public Notifier Build()
    {
        return new Notifier(
            this.handleErrors,
            this.logger,
            this.pipelineBehaviors);
    }
}