// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

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
        if (!notifierType.IsGenericType && this.handlerCache.TryGetValue(handlerInterface, out _))
        {
            try
            {
                return [.. serviceProvider.GetServices(handlerInterface).Cast<INotificationHandler<TNotification>>()];
            }
            catch (InvalidOperationException)
            {
                return [];
            }
            catch (Exception ex)
            {
                throw new NotifierException($"No handlers found for notification type {notifierType.Name}", ex);
            }
        }

        // Resolve directly from IServiceProvider (for generic handlers or if not found in cache)
        try
        {
            return [.. serviceProvider.GetServices<INotificationHandler<TNotification>>()];
        }
        catch (InvalidOperationException)
        {
            return [];
        }
        catch (Exception ex)
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
    private readonly Dictionary<Type, object> behaviorCache = []; // Scoped cache per provider instance

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
            return [];
        }

        var behaviorType = typeof(IPipelineBehavior<TNotification, IResult>);
        if (this.behaviorCache.TryGetValue(behaviorType, out var cachedBehaviors))
        {
            return (IReadOnlyList<IPipelineBehavior<TNotification, IResult>>)cachedBehaviors;
        }

        var allBehaviors = serviceProvider.GetServices(behaviorType).Cast<IPipelineBehavior<TNotification, IResult>>().ToArray();
        if (allBehaviors.Length == 0 && this.pipelineBehaviorTypes.Count > 0)
        {
            throw new InvalidOperationException($"No service for type '{behaviorType}' has been registered.");
        }

        var orderedBehaviors = new IPipelineBehavior<TNotification, IResult>[this.pipelineBehaviorTypes.Count];
        var index = 0;
        for (var i = 0; i < this.pipelineBehaviorTypes.Count; i++)
        {
            var type = this.pipelineBehaviorTypes[i];
            for (var j = 0; j < allBehaviors.Length; j++)
            {
                if (allBehaviors[j].GetType().GetGenericTypeDefinition() == type)
                {
                    orderedBehaviors[index++] = allBehaviors[j];
                    break;
                }
            }
        }

        if (index == 0 && this.pipelineBehaviorTypes.Count > 0)
        {
            throw new InvalidOperationException($"No service for type '{behaviorType}' has been registered.");
        }

        var finalBehaviors = index < orderedBehaviors.Length ? orderedBehaviors.AsSpan(0, index).ToArray() : orderedBehaviors;
        this.behaviorCache[behaviorType] = finalBehaviors; // Cache for scope lifetime
        return finalBehaviors;
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

    private const string RequestLogKey = "NOT";
    private const string NotificationIdLogKey = "NotificationId";
    private const string NotificationTypeLogKey = "NotificationType";
    private static readonly ConcurrentDictionary<Type, string> TypeNameCache = [];
    private static readonly PublishOptions DefaultOptions = new();

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

        var notificationTypeName = TypeNameCache.GetOrAdd(typeof(TNotification), static t => t.Name);
        var notificationIdString = notification.NotificationId.ToString("N");

        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            [NotificationIdLogKey] = notificationIdString,
            [NotificationTypeLogKey] = notificationTypeName
        }))
        {
            TypedLogger.LogProcessing(this.logger, RequestLogKey, notificationTypeName, notificationIdString);
            cancellationToken.ThrowIfCancellationRequested();
            options ??= DefaultOptions;
            var startTicks = Environment.TickCount64;
            var handlers = this.handlerProvider.GetHandlers<TNotification>(this.serviceProvider);
            var behaviors = this.behaviorsProvider.GetBehaviors<TNotification>(this.serviceProvider);

            var notificationLevelBehaviors = behaviors.Where(b => !b.IsHandlerSpecific()).ToArray();
            var handlerSpecificBehaviors = behaviors.Where(b => b.IsHandlerSpecific()).ToArray();

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

                        for (var i = handlerSpecificBehaviors.Length - 1; i >= 0; i--)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var behavior = handlerSpecificBehaviors[i];
                            var behaviorTypeName = TypeNameCache.GetOrAdd(behavior.GetType(), static t => t.Name);
                            //TypedLogger.LogProcessing(this.logger, behaviorTypeName, notificationTypeName, notificationIdString);
                            var behaviorStartTicks = Environment.TickCount64;
                            var currentNext = handlerNext;
                            handlerNext = async () =>
                            {
                                var result = await behavior.HandleAsync(notification, options, handlerType, currentNext, cancellationToken);
                                //TypedLogger.LogProcessed(this.logger, behaviorTypeName, notificationTypeName, notificationIdString, Environment.TickCount64 - behaviorStartTicks);
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
                    var tasks = new Task<IResult>[handlers.Count];
                    for (var i = 0; i < handlers.Count; i++)
                    {
                        var handler = handlers[i];
                        var handlerType = handler.GetType();
                        Func<Task<IResult>> handlerNext = async () => await handler.HandleAsync(notification, options, cancellationToken);

                        for (var j = handlerSpecificBehaviors.Length - 1; j >= 0; j--)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var behavior = handlerSpecificBehaviors[j];
                            var behaviorTypeName = TypeNameCache.GetOrAdd(behavior.GetType(), static t => t.Name);
                            //TypedLogger.LogProcessing(this.logger, behaviorTypeName, notificationTypeName, notificationIdString);
                            var behaviorStartTicks = Environment.TickCount64;
                            var currentNext = handlerNext;
                            handlerNext = async () =>
                            {
                                var result = await behavior.HandleAsync(notification, options, handlerType, currentNext, cancellationToken);
                                //TypedLogger.LogProcessed(this.logger, behaviorTypeName, notificationTypeName, notificationIdString, Environment.TickCount64 - behaviorStartTicks);
                                return result;
                            };
                        }

                        tasks[i] = handlerNext();
                    }

                    results.AddRange(await Task.WhenAll(tasks));
                }
                else
                {
                    // Sequential: Run handlers one by one, stop on first failure
                    foreach (var handler in handlers)
                    {
                        var handlerType = handler.GetType();
                        Func<Task<IResult>> handlerNext = async () => await handler.HandleAsync(notification, options, cancellationToken);

                        for (var i = handlerSpecificBehaviors.Length - 1; i >= 0; i--)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var behavior = handlerSpecificBehaviors[i];
                            var behaviorTypeName = TypeNameCache.GetOrAdd(behavior.GetType(), static t => t.Name);
                            //TypedLogger.LogProcessing(this.logger, behaviorTypeName, notificationTypeName, notificationIdString);
                            var behaviorStartTicks = Environment.TickCount64;
                            var currentNext = handlerNext;
                            handlerNext = async () =>
                            {
                                var result = await behavior.HandleAsync(notification, options, handlerType, currentNext, cancellationToken);
                                //TypedLogger.LogProcessed(this.logger, behaviorTypeName, notificationTypeName, notificationIdString, Environment.TickCount64 - behaviorStartTicks);
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
                for (var i = notificationLevelBehaviors.Length - 1; i >= 0; i--)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var behavior = notificationLevelBehaviors[i];
                    var behaviorTypeName = TypeNameCache.GetOrAdd(behavior.GetType(), static t => t.Name);
                    //TypedLogger.LogProcessing(this.logger, behaviorTypeName, notificationTypeName, notificationIdString);
                    var behaviorStartTicks = Environment.TickCount64;
                    var currentNext = next;
                    next = async () =>
                    {
                        var result = await behavior.HandleAsync(notification, options, null, currentNext, cancellationToken);
                        //TypedLogger.LogProcessed(this.logger, behaviorTypeName, notificationTypeName, notificationIdString, Environment.TickCount64 - behaviorStartTicks);
                        return result;
                    };
                }

                cancellationToken.ThrowIfCancellationRequested();
                var result = await next().ConfigureAwait(false);
                TypedLogger.LogProcessed(this.logger, RequestLogKey, notificationTypeName, notificationIdString, Environment.TickCount64 - startTicks);
                return result;
            }
            catch (Exception ex) when (options.HandleExceptionsAsResultError)
            {
                TypedLogger.LogError(this.logger, ex, notificationTypeName, notificationIdString);
                return Result.Failure().WithError(new ExceptionError(ex));
            }
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

        [LoggerMessage(1, LogLevel.Information, "{LogKey} processed (type={NotificationType}, id={NotificationId}) -> took {TimeElapsed} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string notificationType, string notificationId, long timeElapsed);

        [LoggerMessage(2, LogLevel.Error, "Notification processing failed for {NotificationType} ({NotificationId})")]
        public static partial void LogError(ILogger logger, Exception ex, string notificationType, string notificationId);
    }
}
