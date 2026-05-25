// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>
/// Configures a requester-backed orchestration activity.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TValue">The response value type.</typeparam>
public interface IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> : IOrchestrationActivityBuilder<TData>
    where TData : class, IOrchestrationData
    where TRequest : class, IRequest<TValue>
{
    /// <inheritdoc />
    new IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> Retry(OrchestrationRetryPolicy policy);

    /// <inheritdoc />
    new IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>;

    /// <inheritdoc />
    new IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null);

    IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> Request(Func<OrchestrationContext<TData>, TRequest> requestFactory);

    IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> MapResult(Action<OrchestrationContext<TData>, TValue> mapResult);

    IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> ConfigureOptions(Action<OrchestrationContext<TData>, SendOptions> configure);

    IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> CorrelationId(Func<OrchestrationContext<TData>, string> correlationIdFactory);

    IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> ContextProperty(string key, Func<OrchestrationContext<TData>, string> valueFactory);
}

/// <summary>
/// Configures a notifier-backed orchestration activity.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <typeparam name="TNotification">The notification type.</typeparam>
public interface IOrchestrationNotificationActivityBuilder<TData, TNotification> : IOrchestrationActivityBuilder<TData>
    where TData : class, IOrchestrationData
    where TNotification : class, INotification
{
    /// <inheritdoc />
    new IOrchestrationNotificationActivityBuilder<TData, TNotification> Retry(OrchestrationRetryPolicy policy);

    /// <inheritdoc />
    new IOrchestrationNotificationActivityBuilder<TData, TNotification> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>;

    /// <inheritdoc />
    new IOrchestrationNotificationActivityBuilder<TData, TNotification> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null);

    IOrchestrationNotificationActivityBuilder<TData, TNotification> Notification(Func<OrchestrationContext<TData>, TNotification> notificationFactory);

    IOrchestrationNotificationActivityBuilder<TData, TNotification> ConfigureOptions(Action<OrchestrationContext<TData>, PublishOptions> configure);

    IOrchestrationNotificationActivityBuilder<TData, TNotification> ExecutionMode(ExecutionMode mode);

    IOrchestrationNotificationActivityBuilder<TData, TNotification> CorrelationId(Func<OrchestrationContext<TData>, string> correlationIdFactory);

    IOrchestrationNotificationActivityBuilder<TData, TNotification> ContextProperty(string key, Func<OrchestrationContext<TData>, string> valueFactory);
}

/// <summary>
/// Configures a messaging-backed orchestration activity.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <typeparam name="TMessage">The outbound message type.</typeparam>
public interface IOrchestrationMessageActivityBuilder<TData, TMessage> : IOrchestrationActivityBuilder<TData>
    where TData : class, IOrchestrationData
    where TMessage : class, IMessage
{
    /// <inheritdoc />
    new IOrchestrationMessageActivityBuilder<TData, TMessage> Retry(OrchestrationRetryPolicy policy);

    /// <inheritdoc />
    new IOrchestrationMessageActivityBuilder<TData, TMessage> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>;

    /// <inheritdoc />
    new IOrchestrationMessageActivityBuilder<TData, TMessage> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null);

    IOrchestrationMessageActivityBuilder<TData, TMessage> Message(Func<OrchestrationContext<TData>, TMessage> messageFactory);

    IOrchestrationMessageActivityBuilder<TData, TMessage> ConfigureMessage(Action<OrchestrationContext<TData>, TMessage> configure);

    IOrchestrationMessageActivityBuilder<TData, TMessage> CorrelationId(Func<OrchestrationContext<TData>, string> correlationIdFactory);

    IOrchestrationMessageActivityBuilder<TData, TMessage> FlowId(Func<OrchestrationContext<TData>, string> flowIdFactory);

    IOrchestrationMessageActivityBuilder<TData, TMessage> Property(string key, Func<OrchestrationContext<TData>, object> valueFactory);
}

/// <summary>
/// Configures a queueing-backed orchestration activity.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <typeparam name="TMessage">The outbound queue message type.</typeparam>
public interface IOrchestrationQueueActivityBuilder<TData, TMessage> : IOrchestrationActivityBuilder<TData>
    where TData : class, IOrchestrationData
    where TMessage : class, IQueueMessage
{
    /// <inheritdoc />
    new IOrchestrationQueueActivityBuilder<TData, TMessage> Retry(OrchestrationRetryPolicy policy);

    /// <inheritdoc />
    new IOrchestrationQueueActivityBuilder<TData, TMessage> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>;

    /// <inheritdoc />
    new IOrchestrationQueueActivityBuilder<TData, TMessage> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null);

    IOrchestrationQueueActivityBuilder<TData, TMessage> Message(Func<OrchestrationContext<TData>, TMessage> messageFactory);

    IOrchestrationQueueActivityBuilder<TData, TMessage> ConfigureMessage(Action<OrchestrationContext<TData>, TMessage> configure);

    IOrchestrationQueueActivityBuilder<TData, TMessage> CorrelationId(Func<OrchestrationContext<TData>, string> correlationIdFactory);

    IOrchestrationQueueActivityBuilder<TData, TMessage> FlowId(Func<OrchestrationContext<TData>, string> flowIdFactory);

    IOrchestrationQueueActivityBuilder<TData, TMessage> Property(string key, Func<OrchestrationContext<TData>, object> valueFactory);
}

/// <summary>
/// Configures a pipeline-backed orchestration activity.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <typeparam name="TPipelineContext">The pipeline context type.</typeparam>
public interface IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> : IOrchestrationActivityBuilder<TData>
    where TData : class, IOrchestrationData
    where TPipelineContext : PipelineContextBase
{
    /// <inheritdoc />
    new IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> Retry(OrchestrationRetryPolicy policy);

    /// <inheritdoc />
    new IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>;

    /// <inheritdoc />
    new IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null);

    IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> Pipeline(string pipelineName);

    IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> Context(Func<OrchestrationContext<TData>, TPipelineContext> contextFactory);

    IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> MapToContext(Action<OrchestrationContext<TData>, TPipelineContext> map);

    IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> MapFromContext(Action<OrchestrationContext<TData>, TPipelineContext> map);

    IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> ConfigureOptions(Action<OrchestrationContext<TData>, PipelineExecutionOptions> configure);

    IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> Item(string key, Func<OrchestrationContext<TData>, object> valueFactory);
}

internal abstract class DeferredOrchestrationActivityBuilder<TData> : IOrchestrationActivityBuilder<TData>
    where TData : class, IOrchestrationData
{
    private OrchestrationRetryPolicy retryPolicy;
    private DeferredCompensationDefinition<TData> compensation;

    public IOrchestrationActivityBuilder<TData> Retry(OrchestrationRetryPolicy policy)
    {
        this.retryPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public IOrchestrationActivityBuilder<TData> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        this.compensation = new DeferredCompensationDefinition<TData>(
            (builder, _) => builder.CompensateWith<TActivity>());
        return this;
    }

    public IOrchestrationActivityBuilder<TData> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        this.compensation = new DeferredCompensationDefinition<TData>(
            (builder, activityName) => builder.CompensateWith(executeAsync, string.IsNullOrWhiteSpace(name) ? $"{activityName}Compensation" : name));
        return this;
    }

    public void ApplyTo(IOrchestrationActivityBuilder<TData> builder, string activityName)
    {
        if (this.retryPolicy is not null)
        {
            builder.Retry(this.retryPolicy);
        }

        this.compensation?.Apply(builder, activityName);
    }

    private sealed record DeferredCompensationDefinition<TBuilderData>(
        Action<IOrchestrationActivityBuilder<TBuilderData>, string> Apply)
        where TBuilderData : class, IOrchestrationData;
}

internal sealed class OrchestrationRequestActivityBuilder<TData, TRequest, TValue> : DeferredOrchestrationActivityBuilder<TData>, IOrchestrationRequestActivityBuilder<TData, TRequest, TValue>
    where TData : class, IOrchestrationData
    where TRequest : class, IRequest<TValue>
{
    private readonly List<KeyValuePair<string, Func<OrchestrationContext<TData>, string>>> contextProperties = [];

    public Func<OrchestrationContext<TData>, TRequest> RequestFactory { get; private set; }

    public Action<OrchestrationContext<TData>, TValue> ResultMapper { get; private set; }

    public Action<OrchestrationContext<TData>, SendOptions> OptionsConfigurator { get; private set; }

    public Func<OrchestrationContext<TData>, string> CorrelationIdFactory { get; private set; }

    public IReadOnlyList<KeyValuePair<string, Func<OrchestrationContext<TData>, string>>> ContextProperties => this.contextProperties;

    public new IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> Retry(OrchestrationRetryPolicy policy)
    {
        base.Retry(policy);
        return this;
    }

    public new IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        base.CompensateWith<TActivity>();
        return this;
    }

    public new IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null)
    {
        base.CompensateWith(executeAsync, name);
        return this;
    }

    public IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> Request(Func<OrchestrationContext<TData>, TRequest> requestFactory)
    {
        this.RequestFactory = requestFactory ?? throw new ArgumentNullException(nameof(requestFactory));
        return this;
    }

    public IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> MapResult(Action<OrchestrationContext<TData>, TValue> mapResult)
    {
        this.ResultMapper = mapResult ?? throw new ArgumentNullException(nameof(mapResult));
        return this;
    }

    public IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> ConfigureOptions(Action<OrchestrationContext<TData>, SendOptions> configure)
    {
        this.OptionsConfigurator = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    public IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> CorrelationId(Func<OrchestrationContext<TData>, string> correlationIdFactory)
    {
        this.CorrelationIdFactory = correlationIdFactory ?? throw new ArgumentNullException(nameof(correlationIdFactory));
        return this;
    }

    public IOrchestrationRequestActivityBuilder<TData, TRequest, TValue> ContextProperty(string key, Func<OrchestrationContext<TData>, string> valueFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(valueFactory);

        this.contextProperties.Add(new KeyValuePair<string, Func<OrchestrationContext<TData>, string>>(key, valueFactory));
        return this;
    }
}

internal sealed class OrchestrationNotificationActivityBuilder<TData, TNotification> : DeferredOrchestrationActivityBuilder<TData>, IOrchestrationNotificationActivityBuilder<TData, TNotification>
    where TData : class, IOrchestrationData
    where TNotification : class, INotification
{
    private readonly List<KeyValuePair<string, Func<OrchestrationContext<TData>, string>>> contextProperties = [];

    public Func<OrchestrationContext<TData>, TNotification> NotificationFactory { get; private set; }

    public Action<OrchestrationContext<TData>, PublishOptions> OptionsConfigurator { get; private set; }

    public ExecutionMode? ConfiguredExecutionMode { get; private set; }

    public Func<OrchestrationContext<TData>, string> CorrelationIdFactory { get; private set; }

    public IReadOnlyList<KeyValuePair<string, Func<OrchestrationContext<TData>, string>>> ContextProperties => this.contextProperties;

    public new IOrchestrationNotificationActivityBuilder<TData, TNotification> Retry(OrchestrationRetryPolicy policy)
    {
        base.Retry(policy);
        return this;
    }

    public new IOrchestrationNotificationActivityBuilder<TData, TNotification> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        base.CompensateWith<TActivity>();
        return this;
    }

    public new IOrchestrationNotificationActivityBuilder<TData, TNotification> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null)
    {
        base.CompensateWith(executeAsync, name);
        return this;
    }

    public IOrchestrationNotificationActivityBuilder<TData, TNotification> Notification(Func<OrchestrationContext<TData>, TNotification> notificationFactory)
    {
        this.NotificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));
        return this;
    }

    public IOrchestrationNotificationActivityBuilder<TData, TNotification> ConfigureOptions(Action<OrchestrationContext<TData>, PublishOptions> configure)
    {
        this.OptionsConfigurator = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    public IOrchestrationNotificationActivityBuilder<TData, TNotification> ExecutionMode(ExecutionMode mode)
    {
        this.ConfiguredExecutionMode = mode;
        return this;
    }

    public IOrchestrationNotificationActivityBuilder<TData, TNotification> CorrelationId(Func<OrchestrationContext<TData>, string> correlationIdFactory)
    {
        this.CorrelationIdFactory = correlationIdFactory ?? throw new ArgumentNullException(nameof(correlationIdFactory));
        return this;
    }

    public IOrchestrationNotificationActivityBuilder<TData, TNotification> ContextProperty(string key, Func<OrchestrationContext<TData>, string> valueFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(valueFactory);

        this.contextProperties.Add(new KeyValuePair<string, Func<OrchestrationContext<TData>, string>>(key, valueFactory));
        return this;
    }
}

internal sealed class OrchestrationMessageActivityBuilder<TData, TMessage> : DeferredOrchestrationActivityBuilder<TData>, IOrchestrationMessageActivityBuilder<TData, TMessage>
    where TData : class, IOrchestrationData
    where TMessage : class, IMessage
{
    private readonly List<KeyValuePair<string, Func<OrchestrationContext<TData>, object>>> properties = [];

    public Func<OrchestrationContext<TData>, TMessage> MessageFactory { get; private set; }

    public Action<OrchestrationContext<TData>, TMessage> MessageConfigurator { get; private set; }

    public Func<OrchestrationContext<TData>, string> CorrelationIdFactory { get; private set; }

    public Func<OrchestrationContext<TData>, string> FlowIdFactory { get; private set; }

    public IReadOnlyList<KeyValuePair<string, Func<OrchestrationContext<TData>, object>>> Properties => this.properties;

    public new IOrchestrationMessageActivityBuilder<TData, TMessage> Retry(OrchestrationRetryPolicy policy)
    {
        base.Retry(policy);
        return this;
    }

    public new IOrchestrationMessageActivityBuilder<TData, TMessage> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        base.CompensateWith<TActivity>();
        return this;
    }

    public new IOrchestrationMessageActivityBuilder<TData, TMessage> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null)
    {
        base.CompensateWith(executeAsync, name);
        return this;
    }

    public IOrchestrationMessageActivityBuilder<TData, TMessage> Message(Func<OrchestrationContext<TData>, TMessage> messageFactory)
    {
        this.MessageFactory = messageFactory ?? throw new ArgumentNullException(nameof(messageFactory));
        return this;
    }

    public IOrchestrationMessageActivityBuilder<TData, TMessage> ConfigureMessage(Action<OrchestrationContext<TData>, TMessage> configure)
    {
        this.MessageConfigurator = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    public IOrchestrationMessageActivityBuilder<TData, TMessage> CorrelationId(Func<OrchestrationContext<TData>, string> correlationIdFactory)
    {
        this.CorrelationIdFactory = correlationIdFactory ?? throw new ArgumentNullException(nameof(correlationIdFactory));
        return this;
    }

    public IOrchestrationMessageActivityBuilder<TData, TMessage> FlowId(Func<OrchestrationContext<TData>, string> flowIdFactory)
    {
        this.FlowIdFactory = flowIdFactory ?? throw new ArgumentNullException(nameof(flowIdFactory));
        return this;
    }

    public IOrchestrationMessageActivityBuilder<TData, TMessage> Property(string key, Func<OrchestrationContext<TData>, object> valueFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(valueFactory);

        this.properties.Add(new KeyValuePair<string, Func<OrchestrationContext<TData>, object>>(key, valueFactory));
        return this;
    }
}

internal sealed class OrchestrationQueueActivityBuilder<TData, TMessage> : DeferredOrchestrationActivityBuilder<TData>, IOrchestrationQueueActivityBuilder<TData, TMessage>
    where TData : class, IOrchestrationData
    where TMessage : class, IQueueMessage
{
    private readonly List<KeyValuePair<string, Func<OrchestrationContext<TData>, object>>> properties = [];

    public Func<OrchestrationContext<TData>, TMessage> MessageFactory { get; private set; }

    public Action<OrchestrationContext<TData>, TMessage> MessageConfigurator { get; private set; }

    public Func<OrchestrationContext<TData>, string> CorrelationIdFactory { get; private set; }

    public Func<OrchestrationContext<TData>, string> FlowIdFactory { get; private set; }

    public IReadOnlyList<KeyValuePair<string, Func<OrchestrationContext<TData>, object>>> Properties => this.properties;

    public new IOrchestrationQueueActivityBuilder<TData, TMessage> Retry(OrchestrationRetryPolicy policy)
    {
        base.Retry(policy);
        return this;
    }

    public new IOrchestrationQueueActivityBuilder<TData, TMessage> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        base.CompensateWith<TActivity>();
        return this;
    }

    public new IOrchestrationQueueActivityBuilder<TData, TMessage> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null)
    {
        base.CompensateWith(executeAsync, name);
        return this;
    }

    public IOrchestrationQueueActivityBuilder<TData, TMessage> Message(Func<OrchestrationContext<TData>, TMessage> messageFactory)
    {
        this.MessageFactory = messageFactory ?? throw new ArgumentNullException(nameof(messageFactory));
        return this;
    }

    public IOrchestrationQueueActivityBuilder<TData, TMessage> ConfigureMessage(Action<OrchestrationContext<TData>, TMessage> configure)
    {
        this.MessageConfigurator = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    public IOrchestrationQueueActivityBuilder<TData, TMessage> CorrelationId(Func<OrchestrationContext<TData>, string> correlationIdFactory)
    {
        this.CorrelationIdFactory = correlationIdFactory ?? throw new ArgumentNullException(nameof(correlationIdFactory));
        return this;
    }

    public IOrchestrationQueueActivityBuilder<TData, TMessage> FlowId(Func<OrchestrationContext<TData>, string> flowIdFactory)
    {
        this.FlowIdFactory = flowIdFactory ?? throw new ArgumentNullException(nameof(flowIdFactory));
        return this;
    }

    public IOrchestrationQueueActivityBuilder<TData, TMessage> Property(string key, Func<OrchestrationContext<TData>, object> valueFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(valueFactory);

        this.properties.Add(new KeyValuePair<string, Func<OrchestrationContext<TData>, object>>(key, valueFactory));
        return this;
    }
}

internal sealed class OrchestrationPipelineActivityBuilder<TData, TPipelineContext> : DeferredOrchestrationActivityBuilder<TData>, IOrchestrationPipelineActivityBuilder<TData, TPipelineContext>
    where TData : class, IOrchestrationData
    where TPipelineContext : PipelineContextBase
{
    private readonly List<KeyValuePair<string, Func<OrchestrationContext<TData>, object>>> items = [];

    public string PipelineName { get; private set; }

    public Func<OrchestrationContext<TData>, TPipelineContext> ContextFactory { get; private set; }

    public Action<OrchestrationContext<TData>, TPipelineContext> MapToContextAction { get; private set; }

    public Action<OrchestrationContext<TData>, TPipelineContext> MapFromContextAction { get; private set; }

    public Action<OrchestrationContext<TData>, PipelineExecutionOptions> OptionsConfigurator { get; private set; }

    public IReadOnlyList<KeyValuePair<string, Func<OrchestrationContext<TData>, object>>> Items => this.items;

    public new IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> Retry(OrchestrationRetryPolicy policy)
    {
        base.Retry(policy);
        return this;
    }

    public new IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        base.CompensateWith<TActivity>();
        return this;
    }

    public new IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null)
    {
        base.CompensateWith(executeAsync, name);
        return this;
    }

    public IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> Pipeline(string pipelineName)
    {
        this.PipelineName = pipelineName;
        return this;
    }

    public IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> Context(Func<OrchestrationContext<TData>, TPipelineContext> contextFactory)
    {
        this.ContextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        return this;
    }

    public IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> MapToContext(Action<OrchestrationContext<TData>, TPipelineContext> map)
    {
        this.MapToContextAction = map ?? throw new ArgumentNullException(nameof(map));
        return this;
    }

    public IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> MapFromContext(Action<OrchestrationContext<TData>, TPipelineContext> map)
    {
        this.MapFromContextAction = map ?? throw new ArgumentNullException(nameof(map));
        return this;
    }

    public IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> ConfigureOptions(Action<OrchestrationContext<TData>, PipelineExecutionOptions> configure)
    {
        this.OptionsConfigurator = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    public IOrchestrationPipelineActivityBuilder<TData, TPipelineContext> Item(string key, Func<OrchestrationContext<TData>, object> valueFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(valueFactory);

        this.items.Add(new KeyValuePair<string, Func<OrchestrationContext<TData>, object>>(key, valueFactory));
        return this;
    }
}
