// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using BridgingIT.DevKit.Application.Requester;
using BridgingIT.DevKit.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents an error that occurs during notification processing.
/// </summary>
/// <remarks>
/// This exception is thrown when an error occurs in the Notifier system, such as when no handlers are found for a notification.
/// It supports both a message-only constructor and a constructor with an inner exception for detailed error reporting.
/// </remarks>
/// <example>
/// <code>
/// throw new NotifierException("No handlers found for notification type EmailSentNotification");
/// throw new NotifierException("Notification processing failed", innerException);
/// </code>
/// </example>
public class NotifierException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotifierException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NotifierException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifierException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public NotifierException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Defines a notification that can be dispatched to multiple handlers.
/// </summary>
/// <remarks>
/// This interface is implemented by notification classes to provide metadata for tracking and auditing.
/// It is used by the Notifier to dispatch notifications to their corresponding handlers.
/// </remarks>
public interface INotification
{
    /// <summary>
    /// Gets the unique identifier for the notification.
    /// </summary>
    Guid NotificationId { get; }

    /// <summary>
    /// Gets the UTC timestamp when the notification was created.
    /// </summary>
    DateTimeOffset NotificationTimestamp { get; }
}

/// <summary>
/// Base class for notifications, providing metadata and implementing <see cref="INotification"/>.
/// </summary>
/// <remarks>
/// This abstract class provides a default implementation for notification metadata, including a unique
/// <see cref="NotificationId"/> generated using <see cref="GuidGenerator"/> and a <see cref="NotificationTimestamp"/>
/// set to the current UTC time. Concrete notification classes should inherit from this base class.
/// </remarks>
/// <example>
/// <code>
/// public class EmailSentNotification : NotificationBase
/// {
///     public string EmailAddress { get; set; }
/// }
/// </code>
/// </example>
public abstract class NotificationBase : INotification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationBase"/> class.
    /// </summary>
    protected NotificationBase()
    {
        this.NotificationId = GuidGenerator.CreateSequential();
        this.NotificationTimestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the unique identifier for the notification.
    /// </summary>
    public Guid NotificationId { get; }

    /// <summary>
    /// Gets the UTC timestamp when the notification was created.
    /// </summary>
    public DateTimeOffset NotificationTimestamp { get; }
}

/// <summary>
/// Defines a handler for a specific notification type.
/// </summary>
/// <typeparam name="TNotification">The type of the notification to handle.</typeparam>
/// <remarks>
/// This interface is implemented by handler classes to process notifications dispatched by the Notifier.
/// Handlers return a <see cref="Result"/> to indicate success or failure, and support
/// <see cref="PublishOptions"/> for context and progress reporting.
/// </remarks>
/// <example>
/// <code>
/// public class EmailSentNotificationHandler : NotificationHandlerBase<EmailSentNotification>
/// {
///     protected override async Task<Result> HandleAsync(EmailSentNotification notification, PublishOptions options, CancellationToken cancellationToken)
///     {
///         // Implementation
///         return Result.Success();
///     }
/// }
/// </code>
/// </example>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the specified notification asynchronously.
    /// </summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="options">The options for notification processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the notification handling, returning a <see cref="Result"/>.</returns>
    Task<Result> HandleAsync(TNotification notification, PublishOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for notification handlers, providing a template for handling notifications.
/// </summary>
/// <typeparam name="TNotification">The type of the notification to handle.</typeparam>
/// <remarks>
/// This abstract class implements <see cref="INotificationHandler{TNotification}"/> and requires
/// derived classes to provide the handling logic in the <see cref="HandleAsync"/> method.
/// It serves as a foundation for concrete handlers, ensuring consistent implementation patterns.
/// </remarks>
/// <example>
/// <code>
/// public class EmailSentNotificationHandler : NotificationHandlerBase<EmailSentNotification>
/// {
///     protected override async Task<Result> HandleAsync(EmailSentNotification notification, PublishOptions options, CancellationToken cancellationToken)
///     {
///         // Implementation
///         return Result.Success();
///     }
/// }
/// </code>
/// </example>
public abstract class NotificationHandlerBase<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the specified notification asynchronously.
    /// </summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="options">The options for notification processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the notification handling, returning a <see cref="Result"/>.</returns>
    async Task<Result> INotificationHandler<TNotification>.HandleAsync(TNotification notification, PublishOptions options, CancellationToken cancellationToken)
    {
        return await this.HandleAsync(notification, options, cancellationToken);
    }

    /// <summary>
    /// When implemented in a derived class, handles the specified notification asynchronously.
    /// </summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="options">The options for notification processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the notification handling, returning a <see cref="Result"/>.</returns>
    protected abstract Task<Result> HandleAsync(TNotification notification, PublishOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// Options for configuring notification processing.
/// </summary>
/// <remarks>
/// This class provides configuration options for notification processing, including execution mode,
/// exception handling, a <see cref="RequestContext"/> for contextual data (e.g., UserId, Locale),
/// and a <see cref="Progress"/> reporter for tracking progress.
/// It is used by the Notifier and handlers to access additional context and report progress during notification handling.
/// </remarks>
/// <example>
/// <code>
/// var options = new PublishOptions
/// {
///     ExecutionMode = ExecutionMode.Concurrent,
///     Context = new RequestContext { Properties = { ["UserId"] = "user123", ["Locale"] = "en-US" } },
///     Progress = new Progress<ProgressReport>(report => Console.WriteLine($"Progress: {report.Messages[0]} ({report.PercentageComplete}%)"))
/// };
/// await notifier.PublishAsync(new EmailSentNotification(), options);
/// </code>
/// </example>
public class PublishOptions
{
    /// <summary>
    /// Gets or sets the execution mode for handling notifications.
    /// </summary>
    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.Sequential;

    /// <summary>
    /// Gets or sets the context for the notification, containing properties like UserId or Locale.
    /// </summary>
    public RequestContext Context { get; set; }

    /// <summary>
    /// Gets or sets the progress reporter for tracking notification processing.
    /// </summary>
    public IProgress<ProgressReport> Progress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether exceptions should be caught and returned as a failed result with an error.
    /// If set to <c>false</c>, exceptions will be thrown instead of being converted to a result.
    /// </summary>
    /// <value>
    /// <c>true</c> to handle exceptions as result errors (default); <c>false</c> to throw exceptions.
    /// </value>
    public bool HandleExceptionsAsResultError { get; set; } = true;
}

/// <summary>
/// Defines the execution modes for notification handlers.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Handlers are executed sequentially, one after another.
    /// </summary>
    Sequential,

    /// <summary>
    /// Handlers are executed concurrently, in parallel.
    /// </summary>
    Concurrent,

    /// <summary>
    /// Handlers are executed in a fire-and-forget manner, without awaiting completion.
    /// </summary>
    FireAndForget
}

/// <summary>
/// Defines a provider for resolving notification handlers.
/// </summary>
/// <remarks>
/// This interface is used by the Notifier to resolve handlers for specific notification types.
/// Implementations should retrieve handlers from a cache or dependency injection, ensuring
/// scoped lifetime resolution.
/// </remarks>
/// <example>
/// <code>
/// public class SampleNotificationHandlerProvider : INotificationHandlerProvider
/// {
///     public IReadOnlyList<INotificationHandler<TNotification>> GetHandlers<TNotification>(IServiceProvider serviceProvider)
///         where TNotification : INotification
///     {
///         return serviceProvider.GetServices<INotificationHandler<TNotification>>().ToList();
///     }
/// }
/// </code>
/// </example>
public interface INotificationHandlerProvider
{
    /// <summary>
    /// Resolves the handlers for a specific notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    /// <param name="serviceProvider">The service provider for resolving services.</param>
    /// <returns>A read-only list of handlers for the notification.</returns>
    /// <exception cref="NotifierException">Thrown when no handlers are found for the notification type.</exception>
    IReadOnlyList<INotificationHandler<TNotification>> GetHandlers<TNotification>(IServiceProvider serviceProvider)
        where TNotification : INotification;
}

/// <summary>
/// Provides handlers for notifications using a cached dictionary of handler types.
/// </summary>
/// <remarks>
/// This class implements <see cref="INotificationHandlerProvider"/> to resolve notification handlers from a cached
/// dictionary populated during dependency injection registration. It uses the provided
/// <see cref="IServiceProvider"/> to instantiate handlers, ensuring scoped lifetime resolution.
/// </remarks>
/// <example>
/// <code>
/// var provider = new NotificationHandlerProvider(handlerCache);
/// var handlers = provider.GetHandlers<EmailSentNotification>(serviceProvider);
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="NotificationHandlerProvider"/> class.
/// </remarks>
/// <param name="handlerCache">The cache of handler types, mapping notification handler interfaces to concrete types.</param>
public class NotificationHandlerProvider(IHandlerCache handlerCache) : INotificationHandlerProvider
{
    private readonly IHandlerCache handlerCache = handlerCache ?? throw new ArgumentNullException(nameof(handlerCache));

    /// <summary>
    /// Resolves the handlers for a specific notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    /// <param name="serviceProvider">The service provider for resolving services.</param>
    /// <returns>A read-only list of handlers for the notification.</returns>
    /// <exception cref="NotifierException">Thrown when no handlers are found for the notification type.</exception>
    public IReadOnlyList<INotificationHandler<TNotification>> GetHandlers<TNotification>(IServiceProvider serviceProvider)
        where TNotification : INotification
    {
        var handlerInterface = typeof(INotificationHandler<TNotification>);
        var notifierType = typeof(TNotification);

        // Only use the handlerCache for non-generic request types
        if (!notifierType.IsGenericType && this.handlerCache.TryGetValue(handlerInterface, out var handlerType))
        {
            try
            {
                var handlers = serviceProvider.GetServices(handlerInterface).Cast<INotificationHandler<TNotification>>().ToList();
                if(handlers.Count == 0)
                {
                    throw new NotifierException($"No handlers found for notification type {notifierType.Name}");
                }

                return handlers;
            }
            catch (Exception ex) when (ex is not NotifierException)
            {
                throw new NotifierException($"No handlers found for notification type {notifierType.Name}", ex);
            }
        }

        // Resolve directly from IServiceProvider (for generic handlers or if not found in cache)
        try
        {
            var handlers = serviceProvider.GetServices<INotificationHandler<TNotification>>().ToList().AsReadOnly(); ;
            if (handlers.Count == 0)
            {
                throw new NotifierException($"No handlers found for notification type {notifierType.Name}");
            }

            return handlers;
        }
        catch (Exception ex) when (ex is not NotifierException)
        {
            throw new NotifierException($"No handlers found for notification type {notifierType.Name}", ex);
        }
    }
}

/// <summary>
/// Defines a provider for resolving pipeline behaviors for notifications.
/// </summary>
/// <remarks>
/// This interface is used by the Notifier to resolve a list of pipeline behaviors for specific notification types.
/// Implementations should retrieve behaviors from a registered list, ensuring scoped lifetime resolution
/// via dependency injection.
/// </remarks>
/// <example>
/// <code>
/// public class SampleBehaviorsProvider : INotificationBehaviorsProvider
/// {
///     public IReadOnlyList<IPipelineBehavior<TNotification, Result>> GetBehaviors<TNotification>(IServiceProvider serviceProvider)
///         where TNotification : INotification
///     {
///         return new List<IPipelineBehavior<TNotification, Result>> { serviceProvider.GetService<ValidationBehavior<TNotification, Result>>() };
///     }
/// }
/// </code>
/// </example>
public interface INotificationBehaviorsProvider
{
    IReadOnlyList<IPipelineBehavior<TNotification, IResult>> GetBehaviors<TNotification>(IServiceProvider serviceProvider)
        where TNotification : class, INotification;
}

/// <summary>
/// Provides pipeline behaviors for notifications using registered behavior types.
/// </summary>
/// <remarks>
/// This class implements <see cref="INotificationBehaviorsProvider"/> to resolve pipeline behaviors from a list
/// of registered behavior types, populated during dependency injection registration. It uses the provided
/// <see cref="IServiceProvider"/> to instantiate behaviors, ensuring scoped lifetime resolution.
/// Behaviors are resolved in the order they were registered.
/// </remarks>
/// <example>
/// <code>
/// var provider = new NotificationBehaviorsProvider(pipelineBehaviorTypes);
/// var behaviors = provider.GetBehaviors<EmailSentNotification>(serviceProvider);
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="NotificationBehaviorsProvider"/> class.
/// </remarks>
/// <param name="pipelineBehaviorTypes">The list of registered behavior types.</param>
public class NotificationBehaviorsProvider(IReadOnlyList<Type> pipelineBehaviorTypes) : INotificationBehaviorsProvider
{
    private readonly IReadOnlyList<Type> pipelineBehaviorTypes = pipelineBehaviorTypes ?? throw new ArgumentNullException(nameof(pipelineBehaviorTypes));

    /// <summary>
    /// Resolves the pipeline behaviors for a specific notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    /// <param name="serviceProvider">The service provider for resolving services.</param>
    /// <returns>A read-only list of pipeline behaviors for the notification.</returns>
    public IReadOnlyList<IPipelineBehavior<TNotification, IResult>> GetBehaviors<TNotification>(IServiceProvider serviceProvider)
        where TNotification : class, INotification
    {
        if (serviceProvider == null || this.pipelineBehaviorTypes.Count == 0)
        {
            return Array.Empty<IPipelineBehavior<TNotification, IResult>>().AsReadOnly();
        }

        var behaviorType = typeof(IPipelineBehavior<TNotification, IResult>);
        var allBehaviors = serviceProvider.GetServices(behaviorType)
            .Cast<IPipelineBehavior<TNotification, IResult>>().ToList();

        // If no behaviors are registered but pipelineBehaviorTypes expects some, throw an exception
        if (allBehaviors.Count == 0 && this.pipelineBehaviorTypes.Count > 0)
        {
            throw new InvalidOperationException($"No service for type '{behaviorType}' has been registered.");
        }

        // Order behaviors according to the pipelineBehaviorTypes list
        var orderedBehaviors = new List<IPipelineBehavior<TNotification, IResult>>();
        var remainingBehaviors = new List<IPipelineBehavior<TNotification, IResult>>(allBehaviors);
        foreach (var type in this.pipelineBehaviorTypes)
        {
            var matchingBehavior = remainingBehaviors.FirstOrDefault(b => b.GetType().GetGenericTypeDefinition() == type);
            if (matchingBehavior != null)
            {
                orderedBehaviors.Add(matchingBehavior);
                remainingBehaviors.Remove(matchingBehavior);
            }
            else
            {
                // If a behavior type is in the list but not registered, throw an exception
                throw new InvalidOperationException($"Behavior type '{type}' is not registered in the DI container for '{behaviorType}'.");
            }
        }

        // Validate that all registered behaviors implement the expected interface
        foreach (var behavior in allBehaviors)
        {
            if (!this.pipelineBehaviorTypes.Any(type => type == behavior.GetType().GetGenericTypeDefinition()))
            {
                throw new InvalidOperationException($"Behavior type '{behavior.GetType()}' does not match any registered behavior types for '{behaviorType}'.");
            }
        }

        return orderedBehaviors.AsReadOnly();
    }
}

/// <summary>
/// Defines the interface for dispatching notifications.
/// </summary>
/// <remarks>
/// This interface provides methods for publishing notifications to their handlers through a pipeline of behaviors
/// and retrieving registration information about handlers and behaviors.
/// </remarks>
/// <example>
/// <code>
/// var notifier = serviceProvider.GetRequiredService<INotifier>();
/// var notification = new EmailSentNotification();
/// var result = await notifier.PublishAsync(notification);
/// var info = notifier.GetRegistrationInformation();
/// </code>
/// </example>
public interface INotifier
{
    /// <summary>
    /// Dispatches a notification to its handlers asynchronously.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    /// <param name="notification">The notification to dispatch.</param>
    /// <param name="options">The options for notification processing, including execution mode and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the notification, returning a <see cref="Result"/>.</returns>
    Task<IResult> PublishAsync<TNotification>(
        TNotification notification,
        PublishOptions options = null,
        CancellationToken cancellationToken = default)
        where TNotification : class, INotification;

    /// <summary>
    /// Returns information about registered notification handlers and behaviors.
    /// </summary>
    /// <returns>An object containing handler mappings and behavior types.</returns>
    RegistrationInformation GetRegistrationInformation();
}

/// <summary>
/// Dispatches notifications to their handlers through a pipeline of behaviors.
/// </summary>
/// <remarks>
/// This class implements <see cref="INotifier"/> to dispatch notifications to their handlers, processing them through
/// a pipeline of behaviors resolved via <see cref="INotificationBehaviorsProvider"/>. It uses structured logging
/// with <see cref="TypedLogger"/> to log processing details and supports registration information retrieval
/// through <see cref="GetRegistrationInformation"/>.
/// </remarks>
/// <example>
/// <code>
/// var notifier = serviceProvider.GetRequiredService<INotifier>();
/// var notification = new EmailSentNotification();
/// var options = new PublishOptions { ExecutionMode = ExecutionMode.Concurrent };
/// var result = await notifier.PublishAsync(notification, options);
/// var info = notifier.GetRegistrationInformation();
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="Notifier"/> class.
/// </remarks>
/// <param name="serviceProvider">The service provider for resolving services.</param>
/// <param name="logger">The logger for structured logging.</param>
/// <param name="handlerProvider">The provider for resolving notification handlers.</param>
/// <param name="behaviorsProvider">The provider for resolving pipeline behaviors.</param>
/// <param name="handlerCache">The cache of handler types, mapping notification handler interfaces to concrete types.</param>
/// <param name="pipelineBehaviorTypes">The list of registered behavior types.</param>
public partial class Notifier(
    IServiceProvider serviceProvider,
    ILogger<Notifier> logger,
    INotificationHandlerProvider handlerProvider,
    INotificationBehaviorsProvider behaviorsProvider,
    IHandlerCache handlerCache,
    IReadOnlyList<Type> pipelineBehaviorTypes) : INotifier
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<Notifier> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly INotificationHandlerProvider handlerProvider = handlerProvider ?? throw new ArgumentNullException(nameof(handlerProvider));
    private readonly INotificationBehaviorsProvider behaviorsProvider = behaviorsProvider ?? throw new ArgumentNullException(nameof(behaviorsProvider));
    private readonly IHandlerCache handlerCache = handlerCache ?? throw new ArgumentNullException(nameof(handlerCache));
    private readonly IReadOnlyList<Type> pipelineBehaviorTypes = pipelineBehaviorTypes ?? throw new ArgumentNullException(nameof(pipelineBehaviorTypes));

    /// <summary>
    /// Dispatches a notification to its handlers asynchronously.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    /// <param name="notification">The notification to dispatch.</param>
    /// <param name="options">The options for notification processing, including execution mode and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the notification, returning a <see cref="IResult"/>.</returns>
    public async Task<IResult> PublishAsync<TNotification>(
        TNotification notification,
        PublishOptions options = null,
        CancellationToken cancellationToken = default)
        where TNotification : class, INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        TypedLogger.LogProcessing(this.logger, "NOT", typeof(TNotification).Name, notification.NotificationId.ToString("N"));
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new PublishOptions();
        var watch = ValueStopwatch.StartNew();
        var handlers = this.handlerProvider.GetHandlers<TNotification>(this.serviceProvider);
        var behaviors = this.behaviorsProvider.GetBehaviors<TNotification>(this.serviceProvider);

        // Split behaviors into notification-level and handler-specific
        var notificationLevelBehaviors = behaviors.Where(b => !b.IsHandlerSpecific()).Reverse().ToList();
        var handlerSpecificBehaviors = behaviors.Where(b => b.IsHandlerSpecific()).Reverse().ToList();

        // Build the pipeline for notification-level behaviors (run once)
        Func<Task<IResult>> next = async () =>
        {
            var results = new List<IResult>();
            if (options.ExecutionMode == ExecutionMode.FireAndForget)
            {
                // Fire-and-forget: Dispatch handlers without awaiting
                foreach (var handler in handlers)
                {
                    var handlerType = handler.GetType();
                    Func<Task<IResult>> handlerNext = async () => await handler.HandleAsync(notification, options, cancellationToken);

                    // Build per-handler pipeline for handler-specific behaviors
                    foreach (var behavior in handlerSpecificBehaviors)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var behaviorType = behavior.GetType().Name;
                        TypedLogger.LogProcessing(this.logger, behaviorType, typeof(TNotification).Name, notification.NotificationId.ToString("N"));
                        var behaviorWatch = ValueStopwatch.StartNew();
                        var currentNext = handlerNext;
                        handlerNext = async () =>
                        {
                            var result = await behavior.HandleAsync(notification, options, handlerType, currentNext, cancellationToken);
                            TypedLogger.LogProcessed(this.logger, behaviorType, typeof(TNotification).Name, notification.NotificationId.ToString("N"), behaviorWatch.GetElapsedMilliseconds());
                            return result;
                        };
                    }

                    _ = Task.Run(() => handlerNext(), cancellationToken);
                }
                return Result.Success();
            }
            else if (options.ExecutionMode == ExecutionMode.Concurrent)
            {
                // Concurrent: Run all handlers in parallel
                var tasks = handlers.Select(async handler =>
                {
                    var handlerType = handler.GetType();
                    Func<Task<IResult>> handlerNext = async () => await handler.HandleAsync(notification, options, cancellationToken);

                    // Build per-handler pipeline for handler-specific behaviors
                    foreach (var behavior in handlerSpecificBehaviors)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var behaviorType = behavior.GetType().Name;
                        TypedLogger.LogProcessing(this.logger, behaviorType, typeof(TNotification).Name, notification.NotificationId.ToString("N"));
                        var behaviorWatch = ValueStopwatch.StartNew();
                        var currentNext = handlerNext;
                        handlerNext = async () =>
                        {
                            var result = await behavior.HandleAsync(notification, options, handlerType, currentNext, cancellationToken);
                            TypedLogger.LogProcessed(this.logger, behaviorType, typeof(TNotification).Name, notification.NotificationId.ToString("N"), behaviorWatch.GetElapsedMilliseconds());
                            return result;
                        };
                    }

                    return await handlerNext();
                });

                results.AddRange(await Task.WhenAll(tasks));
            }
            else
            {
                // Sequential: Run handlers one by one, stop on first failure
                foreach (var handler in handlers)
                {
                    var handlerType = handler.GetType();
                    Func<Task<IResult>> handlerNext = async () => await handler.HandleAsync(notification, options, cancellationToken);

                    // Build per-handler pipeline for handler-specific behaviors
                    foreach (var behavior in handlerSpecificBehaviors)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var behaviorType = behavior.GetType().Name;
                        TypedLogger.LogProcessing(this.logger, behaviorType, typeof(TNotification).Name, notification.NotificationId.ToString("N"));
                        var behaviorWatch = ValueStopwatch.StartNew();
                        var currentNext = handlerNext;
                        handlerNext = async () =>
                        {
                            var result = await behavior.HandleAsync(notification, options, handlerType, currentNext, cancellationToken);
                            TypedLogger.LogProcessed(this.logger, behaviorType, typeof(TNotification).Name, notification.NotificationId.ToString("N"), behaviorWatch.GetElapsedMilliseconds());
                            return result;
                        };
                    }

                    var handlerResult = await handlerNext();
                    results.Add((Result)handlerResult);
                    if (handlerResult.IsFailure)
                    {
                        break;
                    }
                }
            }

            // Aggregate results
            if (results.All(r => r.IsSuccess))
            {
                return Result.Success();
            }

            var errors = results.SelectMany(r => r.Errors).ToList();
            var messages = results.SelectMany(r => r.Messages).ToList();

            return Result.Failure(messages, errors);
        };

        try
        {
            // Apply notification-level behaviors in reverse order (outermost to innermost)
            foreach (var behavior in notificationLevelBehaviors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var behaviorType = behavior.GetType().Name;
                TypedLogger.LogProcessing(this.logger, behaviorType, typeof(TNotification).Name, notification.NotificationId.ToString("N"));
                var behaviorWatch = ValueStopwatch.StartNew();
                var currentNext = next;
                next = async () =>
                {
                    var result = await behavior.HandleAsync(notification, options, null, currentNext, cancellationToken);
                    TypedLogger.LogProcessed(this.logger, behaviorType, typeof(TNotification).Name, notification.NotificationId.ToString("N"), behaviorWatch.GetElapsedMilliseconds());
                    return result;
                };
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = await next().ConfigureAwait(false);
            TypedLogger.LogProcessed(this.logger, "NOT", typeof(TNotification).Name, notification.NotificationId.ToString("N"), watch.GetElapsedMilliseconds());
            return result;
        }
        catch (Exception ex) when (options.HandleExceptionsAsResultError)
        {
            TypedLogger.LogError(this.logger, ex, typeof(TNotification).Name, notification.NotificationId.ToString("N"));
            return Result.Failure().WithError(new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Returns information about registered notification handlers and behaviors, logging the details.
    /// </summary>
    /// <returns>An object containing handler mappings and behavior types.</returns>
    public RegistrationInformation GetRegistrationInformation()
    {
        var handlerMappings = this.handlerCache
            .ToDictionary(
                kvp => kvp.Key.GetGenericArguments()[0].PrettyName(), // Notification type
                kvp => new List<string> { kvp.Value.PrettyName() }.AsReadOnly() as IReadOnlyList<string>);

        var behaviorTypes = this.pipelineBehaviorTypes
            .Select(t => t.PrettyName())
            .ToList().AsReadOnly();

        var information = new RegistrationInformation(handlerMappings, behaviorTypes);

        this.logger.LogDebug("Registered Notification Handlers: {HandlerMappings}",
            string.Join("; ", handlerMappings.Select(kvp => $"{kvp.Key}: [{string.Join(", ", kvp.Value)}]")));
        this.logger.LogDebug("Registered Notification Behaviors: {BehaviorTypes}",
            string.Join(", ", behaviorTypes));

        return information;
    }

    /// <summary>
    /// Provides structured logging messages for the <see cref="Notifier"/> class.
    /// </summary>
    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} processing (type={NotificationType}, id={NotificationId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string notificationType, string notificationId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} processed (type={NotificationType}, id={NotificationId}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string notificationType, string notificationId, long timeElapsed);

        [LoggerMessage(2, LogLevel.Error, "Notification processing failed for {NotificationType} ({NotificationId})")]
        public static partial void LogError(ILogger logger, Exception ex, string notificationType, string notificationId);
    }
}

/// <summary>
/// Configures and builds the Notifier service for dependency injection registration.
/// </summary>
/// <remarks>
/// This class provides a fluent API for registering handlers, behaviors, and providers for the Notifier system.
/// It scans assemblies for handlers and validators, caches handler types, and registers services with the DI container.
/// </remarks>
/// <example>
/// <code>
/// var services = new ServiceCollection();
/// services.AddNotifier()
///     .AddHandlers(new[] { "^System\\..*" })
///     .WithBehavior<ValidationBehavior<,>>();
/// var provider = services.BuildServiceProvider();
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="NotifierBuilder"/> class.
/// </remarks>
/// <param name="services">The service collection for dependency injection registration.</param>
public class NotifierBuilder
{
    private readonly IServiceCollection services;
    private readonly List<Type> pipelineBehaviorTypes = [];
    private readonly List<Type> validatorTypes = [];
    private readonly IHandlerCache handlerCache = new HandlerCache();
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = [];

    public NotifierBuilder(IServiceCollection services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));

        this.services.AddSingleton(this.handlerCache);
        this.services.AddSingleton(this.policyCache);
        this.services.AddSingleton<INotificationHandlerProvider, NotificationHandlerProvider>();
        this.services.AddSingleton<INotificationBehaviorsProvider>(sp => new NotificationBehaviorsProvider(this.pipelineBehaviorTypes));
        this.services.AddScoped<INotifier>(sp => new Notifier(
            sp,
            sp.GetRequiredService<ILogger<Notifier>>(),
            sp.GetRequiredService<INotificationHandlerProvider>(),
            sp.GetRequiredService<INotificationBehaviorsProvider>(),
            sp.GetRequiredService<IHandlerCache>(),
            this.pipelineBehaviorTypes));
    }

    public NotifierBuilder AddHandlers(IEnumerable<string> blacklistPatterns = null)
    {
        blacklistPatterns ??= Blacklists.ApplicationDependencies;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.GetName().Name.MatchAny(blacklistPatterns)).ToList();

        foreach (var assembly in assemblies)
        {
            var types = this.SafeGetTypes(assembly);
            foreach (var type in types)
            {
                if (type.IsGenericTypeDefinition)
                {
                    continue;
                }

                var handlerInterfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                    .ToList();

                if (handlerInterfaces.Any())
                {
                    foreach (var handlerInterface in handlerInterfaces)
                    {
                        var notificationType = handlerInterface.GetGenericArguments()[0];

                        this.handlerCache.TryAdd(handlerInterface, type);
                        this.policyCache.TryAdd(type, new PolicyConfig
                        {
                            Retry = type.GetCustomAttribute<HandlerRetryAttribute>(),
                            Timeout = type.GetCustomAttribute<HandlerTimeoutAttribute>(),
                            Chaos = type.GetCustomAttribute<HandlerChaosAttribute>(),
                            CircuitBreaker = type.GetCustomAttribute<HandlerCircuitBreakerAttribute>(),
                            CacheInvalidate = type.GetCustomAttribute<HandlerCacheInvalidateAttribute>(),
                        });

                        if (!type.IsAbstract)
                        {
                            this.services.AddScoped(handlerInterface, type);
                        }

                        var validatorType = notificationType.GetNestedType("Validator");
                        if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(notificationType)) == true)
                        {
                            if (!this.validatorTypes.Contains(validatorType))
                            {
                                this.validatorTypes.Add(validatorType);
                            }
                            this.services.AddScoped(typeof(IValidator<>).MakeGenericType(notificationType), validatorType);
                        }
                    }
                }
            }
        }

        return this;
    }

    public NotifierBuilder AddHandler<TNotification, THandler>()
        where TNotification : INotification
        where THandler : class, INotificationHandler<TNotification>
    {
        var handlerInterface = typeof(INotificationHandler<TNotification>);
        var handlerType = typeof(THandler);

        this.services.AddScoped(handlerInterface, handlerType);

        if (!handlerType.IsGenericTypeDefinition)
        {
            this.handlerCache.TryAdd(handlerInterface, handlerType);
        }

        this.policyCache.TryAdd(handlerType, new PolicyConfig
        {
            Retry = handlerType.GetCustomAttribute<HandlerRetryAttribute>(),
            Timeout = handlerType.GetCustomAttribute<HandlerTimeoutAttribute>(),
            Chaos = handlerType.GetCustomAttribute<HandlerChaosAttribute>(),
            CircuitBreaker = handlerType.GetCustomAttribute<HandlerCircuitBreakerAttribute>(),
            CacheInvalidate = handlerType.GetCustomAttribute<HandlerCacheInvalidateAttribute>(),
        });

        var notificationType = typeof(TNotification);
        var validatorType = notificationType.GetNestedType("Validator");
        if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(notificationType)) == true)
        {
            if (!this.validatorTypes.Contains(validatorType))
            {
                this.validatorTypes.Add(validatorType);
            }
            this.services.AddScoped(typeof(IValidator<>).MakeGenericType(notificationType), validatorType);
        }

        return this;
    }

    public NotifierBuilder AddGenericHandler(Type genericHandlerType, Type genericNotificationType, Type[] typeArguments)
    {
        if (genericHandlerType == null || !genericHandlerType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("Generic handler type must be an open generic type definition.", nameof(genericHandlerType));
        }

        if (genericNotificationType == null || !genericNotificationType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("Generic notification type must be an open generic type definition.", nameof(genericNotificationType));
        }

        if (typeArguments == null || !typeArguments.Any())
        {
            throw new ArgumentException("At least one type argument must be provided.", nameof(typeArguments));
        }

        var notificationTypeParams = genericNotificationType.GetGenericArguments().Length;
        var handlerTypeParams = genericHandlerType.GetGenericArguments().Length;
        if (notificationTypeParams != handlerTypeParams || typeArguments.Length != notificationTypeParams)
        {
            throw new ArgumentException($"The number of type arguments ({typeArguments.Length}) must match the number of generic parameters in the notification type ({notificationTypeParams}) and handler type ({handlerTypeParams}).");
        }

        var handlerInterface = genericHandlerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>));
        if (handlerInterface == null)
        {
            throw new ArgumentException($"Generic handler type {genericHandlerType.Name} does not implement INotificationHandler<>.", nameof(genericHandlerType));
        }

        foreach (var typeArg in typeArguments)
        {
            var closedNotificationType = genericNotificationType.MakeGenericType(typeArg);
            var closedHandlerType = genericHandlerType.MakeGenericType(typeArg);
            var closedHandlerInterface = typeof(INotificationHandler<>).MakeGenericType(closedNotificationType);

            this.services.AddScoped(closedHandlerInterface, closedHandlerType);
            this.handlerCache.TryAdd(closedHandlerInterface, closedHandlerType);

            this.policyCache.TryAdd(closedHandlerType, new PolicyConfig
            {
                Retry = closedHandlerType.GetCustomAttribute<HandlerRetryAttribute>(),
                Timeout = closedHandlerType.GetCustomAttribute<HandlerTimeoutAttribute>(),
                Chaos = closedHandlerType.GetCustomAttribute<HandlerChaosAttribute>(),
                CircuitBreaker = closedHandlerType.GetCustomAttribute<HandlerCircuitBreakerAttribute>(),
                CacheInvalidate = closedHandlerType.GetCustomAttribute<HandlerCacheInvalidateAttribute>(),
            });

            var validatorType = closedNotificationType.GetNestedType("Validator");
            if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(closedNotificationType)) == true)
            {
                if (!this.validatorTypes.Contains(validatorType))
                {
                    this.validatorTypes.Add(validatorType);
                }
                this.services.AddScoped(typeof(IValidator<>).MakeGenericType(closedNotificationType), validatorType);
            }
        }

        return this;
    }

    public NotifierBuilder AddGenericHandlers(IEnumerable<string> blacklistPatterns = null)
    {
        blacklistPatterns ??= Blacklists.ApplicationDependencies;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.GetName().Name.MatchAny(blacklistPatterns)).ToList();

        var genericHandlers = new ConcurrentBag<(Type HandlerType, Type NotificationTypeDefinition)>();
        Parallel.ForEach(assemblies, assembly =>
        {
            var types = this.SafeGetTypes(assembly);
            foreach (var type in types)
            {
                if (type.IsAbstract)
                {
                    continue;
                }

                var handlerInterfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                    .ToList();

                if (handlerInterfaces.Any())
                {
                    foreach (var handlerInterface in handlerInterfaces)
                    {
                        var notificationType = handlerInterface.GetGenericArguments()[0];

                        if (type.IsGenericTypeDefinition)
                        {
                            var notificationTypeDefinition = notificationType.GetGenericTypeDefinition();
                            genericHandlers.Add((type, notificationTypeDefinition));
                        }
                    }
                }
            }
        });

        foreach (var (handlerType, notificationTypeDefinition) in genericHandlers)
        {
            var genericTypeParameters = handlerType.GetGenericArguments();
            if (genericTypeParameters.Length != 1)
            {
                throw new InvalidOperationException($"Handler type {handlerType.Name} must have exactly one generic type parameter for automatic discovery.");
            }

            var typeParameter = genericTypeParameters[0];
            var constraints = typeParameter.GetGenericParameterConstraints();
            var isClassConstraint = (typeParameter.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0;
            var hasDefaultConstructorConstraint = (typeParameter.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0;

            var typeArguments = new ConcurrentBag<Type>();
            Parallel.ForEach(assemblies, assembly =>
            {
                var types = this.SafeGetTypes(assembly);
                foreach (var candidateType in types)
                {
                    if (candidateType.IsAbstract || candidateType.IsInterface || candidateType.IsGenericTypeDefinition)
                    {
                        continue;
                    }

                    if (isClassConstraint && candidateType.IsValueType)
                    {
                        continue;
                    }

                    var satisfiesConstraints = true;

                    if (hasDefaultConstructorConstraint && !candidateType.GetConstructors().Any(c => c.GetParameters().Length == 0))
                    {
                        satisfiesConstraints = false;
                    }

                    if (satisfiesConstraints)
                    {
                        foreach (var constraint in constraints)
                        {
                            if (!constraint.IsAssignableFrom(candidateType))
                            {
                                satisfiesConstraints = false;
                                break;
                            }
                        }
                    }

                    if (satisfiesConstraints)
                    {
                        typeArguments.Add(candidateType);
                    }
                }
            });

            if (!typeArguments.Any())
            {
                throw new InvalidOperationException($"No concrete types found that satisfy the constraints for generic handler {handlerType.Name}.");
            }

            foreach (var typeArg in typeArguments)
            {
                var closedNotificationType = notificationTypeDefinition.MakeGenericType(typeArg);
                var closedHandlerType = handlerType.MakeGenericType(typeArg);
                var closedHandlerInterface = typeof(INotificationHandler<>).MakeGenericType(closedNotificationType);

                this.services.AddScoped(closedHandlerInterface, closedHandlerType);
                this.handlerCache.TryAdd(closedHandlerInterface, closedHandlerType);

                this.policyCache.TryAdd(closedHandlerType, new PolicyConfig
                {
                    Retry = closedHandlerType.GetCustomAttribute<HandlerRetryAttribute>(),
                    Timeout = closedHandlerType.GetCustomAttribute<HandlerTimeoutAttribute>(),
                    Chaos = closedHandlerType.GetCustomAttribute<HandlerChaosAttribute>(),
                    CircuitBreaker = closedHandlerType.GetCustomAttribute<HandlerCircuitBreakerAttribute>(),
                    CacheInvalidate = closedHandlerType.GetCustomAttribute<HandlerCacheInvalidateAttribute>(),
                });

                var validatorType = closedNotificationType.GetNestedType("Validator");
                if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(closedNotificationType)) == true)
                {
                    lock (this.validatorTypes)
                    {
                        if (!this.validatorTypes.Contains(validatorType))
                        {
                            this.validatorTypes.Add(validatorType);
                        }
                    }
                    this.services.AddScoped(typeof(IValidator<>).MakeGenericType(closedNotificationType), validatorType);
                }
            }
        }

        return this;
    }

    /// <summary>
    /// Adds a pipeline behavior to the notification processing pipeline.
    /// </summary>
    /// <param name="behaviorType">The open generic type of the behavior (e.g., typeof(TestBehavior<,>)).</param>
    /// <returns>The <see cref="NotifierBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="behaviorType"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="behaviorType"/> is not an open generic type.</exception>
    public NotifierBuilder WithBehavior(Type behaviorType)
    {
        if (behaviorType == null)
        {
            return this;
        }

        if (!behaviorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException($"Behavior type '{behaviorType}' must be an open generic type.", nameof(behaviorType));
        }

        this.services.AddScoped(typeof(IPipelineBehavior<,>), behaviorType);
        this.pipelineBehaviorTypes.Add(behaviorType);
        return this;
    }

    private IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null);
        }
    }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Notifier service to the specified <see cref="IServiceCollection"/> and returns a <see cref="NotifierBuilder"/> for configuration.
    /// </summary>
    /// <remarks>
    /// This method registers the necessary services for the Notifier system, including the <see cref="INotifier"/> implementation
    /// and its dependencies. It returns a <see cref="NotifierBuilder"/> that can be used to configure handlers and behaviors
    /// using a fluent API. The Notifier system enables dispatching notifications to their corresponding handlers through a pipeline
    /// of behaviors, supporting features like validation, retry, and timeout.
    ///
    /// To use the Notifier system, you must:
    /// 1. Call <see cref="AddNotifier"/> to register the core services.
    /// 2. Use <see cref="NotifierBuilder.AddHandlers"/> to scan for and register notification handlers.
    /// 3. Optionally, use <see cref="NotifierBuilder.WithBehavior{TBehavior}"/> to add pipeline behaviors.
    /// 4. Build the service provider to resolve the <see cref="INotifier"/> service for dispatching notifications.
    ///
    /// The <see cref="INotifier"/> service is registered with a scoped lifetime, meaning a new instance is created for each
    /// scope (e.g., per HTTP request in ASP.NET Core). Ensure that any dependencies (e.g., logging) are also registered in the
    /// service collection before calling this method.
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the Notifier service to.</param>
    /// <returns>A <see cref="NotifierBuilder"/> for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <example>
    /// <code>
    /// // Basic usage with default configuration
    /// var services = new ServiceCollection();
    /// services.AddLogging(); // Required for INotifier logging
    /// services.AddNotifier()
    ///     .AddHandlers(new[] { "^System\\..*" }); // Exclude System assemblies
    /// var provider = services.BuildServiceProvider();
    /// var notifier = provider.GetRequiredService&lt;INotifier&gt;();
    /// var result = await notifier.PublishAsync(new EmailSentNotification());
    ///
    /// // Usage with pipeline behaviors
    /// var services = new ServiceCollection();
    /// services.AddLogging();
    /// services.AddNotifier()
    ///     .AddHandlers(new[] { "^System\\..*" })
    ///     .WithBehavior&lt;ValidationBehavior&lt;,&gt;&gt;() // Add validation behavior
    ///     .WithBehavior&lt;RetryBehavior&lt;,&gt;&gt;();    // Add retry behavior
    /// var provider = services.BuildServiceProvider();
    /// var notifier = provider.GetRequiredService&lt;INotifier&gt;();
    /// var result = await notifier.PublishAsync(new EmailSentNotification());
    /// </code>
    /// </example>
    /// <seealso cref="NotifierBuilder"/>
    /// <seealso cref="INotifier"/>
    public static NotifierBuilder AddNotifier(this IServiceCollection services)
    {
        return services == null ? throw new ArgumentNullException(nameof(services)) : new NotifierBuilder(services);
    }
}