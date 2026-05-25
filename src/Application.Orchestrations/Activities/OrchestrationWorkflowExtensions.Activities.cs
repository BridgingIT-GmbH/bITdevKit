// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides built-in workflow helpers for orchestration activities that integrate with other feature abstractions.
/// </summary>
public static partial class OrchestrationWorkflowExtensions
{
    public static IOrchestrationStateBuilder<TData> QueryActivity<TData, TRequest, TValue>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationRequestActivityBuilder<TData, TRequest, TValue>> configure,
        string name = null)
        where TData : class, IOrchestrationData
        where TRequest : class, IRequest<TValue>
    {
        return AddRequestActivity(
            builder,
            configure,
            name ?? $"Query{typeof(TRequest).Name}",
            $"QueryActivity<{typeof(TRequest).Name}>");
    }

    public static IOrchestrationStateBuilder<TData> CommandActivity<TData, TRequest, TValue>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationRequestActivityBuilder<TData, TRequest, TValue>> configure,
        string name = null)
        where TData : class, IOrchestrationData
        where TRequest : class, IRequest<TValue>
    {
        return AddRequestActivity(
            builder,
            configure,
            name ?? $"Command{typeof(TRequest).Name}",
            $"CommandActivity<{typeof(TRequest).Name}>");
    }

    public static IOrchestrationStateBuilder<TData> SendRequestActivity<TData, TRequest, TValue>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationRequestActivityBuilder<TData, TRequest, TValue>> configure,
        string name = null)
        where TData : class, IOrchestrationData
        where TRequest : class, IRequest<TValue>
    {
        return AddRequestActivity(
            builder,
            configure,
            name ?? $"Send{typeof(TRequest).Name}",
            $"SendRequestActivity<{typeof(TRequest).Name}>");
    }

    public static IOrchestrationStateBuilder<TData> PublishNotificationActivity<TData, TNotification>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationNotificationActivityBuilder<TData, TNotification>> configure,
        string name = null)
        where TData : class, IOrchestrationData
        where TNotification : class, INotification
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var activityName = name ?? $"Publish{typeof(TNotification).Name}";
        var activityLabel = $"PublishNotificationActivity<{typeof(TNotification).Name}>";
        var settings = new OrchestrationNotificationActivityBuilder<TData, TNotification>();
        configure(settings);

        if (settings.NotificationFactory is null)
        {
            throw new InvalidOperationException($"{activityLabel} requires a configured notification factory.");
        }

        return builder.Activity(
            async (context, cancellationToken) =>
            {
                var notifier = context.Services.GetService<INotifier>();
                if (notifier is null)
                {
                    throw new InvalidOperationException($"INotifier is not registered for {activityLabel}.");
                }

                var notification = settings.NotificationFactory(context);
                ArgumentNullException.ThrowIfNull(notification);

                var options = new PublishOptions();
                settings.OptionsConfigurator?.Invoke(context, options);
                if (settings.ConfiguredExecutionMode.HasValue)
                {
                    options.ExecutionMode = settings.ConfiguredExecutionMode.Value;
                }

                ApplyRequestContext(options, context, settings.CorrelationIdFactory, settings.ContextProperties);

                var result = await notifier.PublishAsync(notification, options, cancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    throw new InvalidOperationException($"{activityLabel} failed: {DescribeFailure(result, "Notification publishing failed.")}");
                }

                return OrchestrationOutcome.Continue();
            },
            activity => settings.ApplyTo(activity, activityName),
            activityName);
    }

    public static IOrchestrationStateBuilder<TData> PublishMessageActivity<TData, TMessage>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationMessageActivityBuilder<TData, TMessage>> configure,
        string name = null)
        where TData : class, IOrchestrationData
        where TMessage : class, IMessage
    {
        return AddMessageActivity(
            builder,
            configure,
            name ?? $"Publish{typeof(TMessage).Name}",
            $"PublishMessageActivity<{typeof(TMessage).Name}>");
    }

    public static IOrchestrationStateBuilder<TData> SendQueueMessageActivity<TData, TMessage>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationQueueActivityBuilder<TData, TMessage>> configure,
        string name = null)
        where TData : class, IOrchestrationData
        where TMessage : class, IQueueMessage
    {
        return AddQueueActivity(
            builder,
            configure,
            name ?? $"Enqueue{typeof(TMessage).Name}",
            $"SendQueueMessageActivity<{typeof(TMessage).Name}>");
    }

    public static IOrchestrationStateBuilder<TData> ExecutePipelineActivity<TData, TPipelineContext>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationPipelineActivityBuilder<TData, TPipelineContext>> configure,
        string name = null)
        where TData : class, IOrchestrationData
        where TPipelineContext : PipelineContextBase
    {
        return AddPipelineActivity(
            builder,
            configure,
            name ?? $"Execute{typeof(TPipelineContext).Name}",
            $"ExecutePipelineActivity<{typeof(TPipelineContext).Name}>",
            settings => settings.PipelineName,
            (factory, settings) => factory.Create<TPipelineContext>(settings.PipelineName));
    }

    public static IOrchestrationStateBuilder<TData> ExecutePipelineActivity<TData, TPipelineDefinition, TPipelineContext>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationPipelineActivityBuilder<TData, TPipelineContext>> configure = null,
        string name = null)
        where TData : class, IOrchestrationData
        where TPipelineDefinition : class, IPipelineDefinitionSource<TPipelineContext>
        where TPipelineContext : PipelineContextBase
    {
        return AddPipelineActivity(
            builder,
            configure,
            name ?? $"Execute{typeof(TPipelineDefinition).Name}",
            $"ExecutePipelineActivity<{typeof(TPipelineDefinition).Name}>",
            _ => typeof(TPipelineDefinition).Name,
            static (factory, settings) => factory.Create<TPipelineDefinition, TPipelineContext>());
    }

    private static IOrchestrationStateBuilder<TData> AddRequestActivity<TData, TRequest, TValue>(
        IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationRequestActivityBuilder<TData, TRequest, TValue>> configure,
        string activityName,
        string activityLabel)
        where TData : class, IOrchestrationData
        where TRequest : class, IRequest<TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new OrchestrationRequestActivityBuilder<TData, TRequest, TValue>();
        configure(settings);

        if (settings.RequestFactory is null)
        {
            throw new InvalidOperationException($"{activityLabel} requires a configured request factory.");
        }

        return builder.Activity(
            async (context, cancellationToken) =>
            {
                var requester = context.Services.GetService<IRequester>();
                if (requester is null)
                {
                    throw new InvalidOperationException($"IRequester is not registered for {activityLabel}.");
                }

                var request = settings.RequestFactory(context);
                ArgumentNullException.ThrowIfNull(request);

                var options = new SendOptions();
                settings.OptionsConfigurator?.Invoke(context, options);
                ApplyRequestContext(options, context, settings.CorrelationIdFactory, settings.ContextProperties);

                var result = await requester.SendAsync<TRequest, TValue>(request, options, cancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    throw new InvalidOperationException($"{activityLabel} failed: {DescribeFailure(result, "Request execution failed.")}");
                }

                settings.ResultMapper?.Invoke(context, result.Value);
                return OrchestrationOutcome.Continue();
            },
            activity => settings.ApplyTo(activity, activityName),
            activityName);
    }

    private static IOrchestrationStateBuilder<TData> AddMessageActivity<TData, TMessage>(
        IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationMessageActivityBuilder<TData, TMessage>> configure,
        string activityName,
        string activityLabel)
        where TData : class, IOrchestrationData
        where TMessage : class, IMessage
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new OrchestrationMessageActivityBuilder<TData, TMessage>();
        configure(settings);

        if (settings.MessageFactory is null)
        {
            throw new InvalidOperationException($"{activityLabel} requires a configured message factory.");
        }

        return builder.Activity(
            async (context, cancellationToken) =>
            {
                var messageBroker = context.Services.GetService<IMessageBroker>();
                if (messageBroker is null)
                {
                    throw new InvalidOperationException($"IMessageBroker is not registered for {activityLabel}.");
                }

                var message = settings.MessageFactory(context);
                ArgumentNullException.ThrowIfNull(message);

                settings.MessageConfigurator?.Invoke(context, message);
                ApplyTransportProperties(message.Properties, context, settings.CorrelationIdFactory, settings.FlowIdFactory, settings.Properties);

                await messageBroker.Publish(message, cancellationToken).ConfigureAwait(false);
                return OrchestrationOutcome.Continue();
            },
            activity => settings.ApplyTo(activity, activityName),
            activityName);
    }

    private static IOrchestrationStateBuilder<TData> AddQueueActivity<TData, TMessage>(
        IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationQueueActivityBuilder<TData, TMessage>> configure,
        string activityName,
        string activityLabel)
        where TData : class, IOrchestrationData
        where TMessage : class, IQueueMessage
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new OrchestrationQueueActivityBuilder<TData, TMessage>();
        configure(settings);

        if (settings.MessageFactory is null)
        {
            throw new InvalidOperationException($"{activityLabel} requires a configured message factory.");
        }

        return builder.Activity(
            async (context, cancellationToken) =>
            {
                var queueBroker = context.Services.GetService<IQueueBroker>();
                if (queueBroker is null)
                {
                    throw new InvalidOperationException($"IQueueBroker is not registered for {activityLabel}.");
                }

                var message = settings.MessageFactory(context);
                ArgumentNullException.ThrowIfNull(message);

                settings.MessageConfigurator?.Invoke(context, message);
                ApplyTransportProperties(message.Properties, context, settings.CorrelationIdFactory, settings.FlowIdFactory, settings.Properties);

                await queueBroker.Enqueue(message, cancellationToken).ConfigureAwait(false);
                return OrchestrationOutcome.Continue();
            },
            activity => settings.ApplyTo(activity, activityName),
            activityName);
    }

    private static IOrchestrationStateBuilder<TData> AddPipelineActivity<TData, TPipelineContext>(
        IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationPipelineActivityBuilder<TData, TPipelineContext>> configure,
        string activityName,
        string activityLabel,
        Func<OrchestrationPipelineActivityBuilder<TData, TPipelineContext>, string> descriptorProvider,
        Func<IPipelineFactory, OrchestrationPipelineActivityBuilder<TData, TPipelineContext>, IPipeline<TPipelineContext>> pipelineAccessor)
        where TData : class, IOrchestrationData
        where TPipelineContext : PipelineContextBase
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(descriptorProvider);
        ArgumentNullException.ThrowIfNull(pipelineAccessor);

        var settings = new OrchestrationPipelineActivityBuilder<TData, TPipelineContext>();
        configure?.Invoke(settings);

        var descriptor = descriptorProvider(settings);
        if (string.IsNullOrWhiteSpace(descriptor))
        {
            throw new InvalidOperationException($"{activityLabel} requires a configured pipeline.");
        }

        if (settings.ContextFactory is null &&
            (typeof(TPipelineContext).IsAbstract || typeof(TPipelineContext).GetConstructor(Type.EmptyTypes) is null))
        {
            throw new InvalidOperationException($"{activityLabel} requires either a configured pipeline context factory or a public parameterless constructor on {typeof(TPipelineContext).Name}.");
        }

        return builder.Activity(
            async (context, cancellationToken) =>
            {
                var pipelineFactory = context.Services.GetService<IPipelineFactory>();
                if (pipelineFactory is null)
                {
                    throw new InvalidOperationException($"IPipelineFactory is not registered for {activityLabel}.");
                }

                var pipeline = pipelineAccessor(pipelineFactory, settings);
                var pipelineContext = settings.ContextFactory?.Invoke(context) ?? CreatePipelineContext<TPipelineContext>(activityLabel);
                ArgumentNullException.ThrowIfNull(pipelineContext);

                settings.MapToContextAction?.Invoke(context, pipelineContext);
                ApplyPipelineItems(pipelineContext, context, settings.Items);

                var options = new PipelineExecutionOptions();
                settings.OptionsConfigurator?.Invoke(context, options);

                var result = await pipeline.ExecuteAsync(pipelineContext, options, cancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    throw new InvalidOperationException($"{activityLabel} failed: {DescribeFailure(result, "Pipeline execution failed.")}");
                }

                settings.MapFromContextAction?.Invoke(context, pipelineContext);
                return OrchestrationOutcome.Continue();
            },
            activity => settings.ApplyTo(activity, activityName),
            activityName);
    }

    private static void ApplyRequestContext<TData>(
        SendOptions options,
        OrchestrationContext<TData> context,
        Func<OrchestrationContext<TData>, string> correlationIdFactory,
        IReadOnlyList<KeyValuePair<string, Func<OrchestrationContext<TData>, string>>> contextProperties)
        where TData : class, IOrchestrationData
    {
        var requestContext = EnsureRequestContext(options);
        ApplyRequestContext(requestContext, context, correlationIdFactory, contextProperties);
    }

    private static void ApplyRequestContext<TData>(
        PublishOptions options,
        OrchestrationContext<TData> context,
        Func<OrchestrationContext<TData>, string> correlationIdFactory,
        IReadOnlyList<KeyValuePair<string, Func<OrchestrationContext<TData>, string>>> contextProperties)
        where TData : class, IOrchestrationData
    {
        var requestContext = EnsureRequestContext(options);
        ApplyRequestContext(requestContext, context, correlationIdFactory, contextProperties);
    }

    private static void ApplyRequestContext<TData>(
        RequestContext requestContext,
        OrchestrationContext<TData> context,
        Func<OrchestrationContext<TData>, string> correlationIdFactory,
        IReadOnlyList<KeyValuePair<string, Func<OrchestrationContext<TData>, string>>> contextProperties)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(requestContext);

        if (correlationIdFactory is not null)
        {
            var correlationId = correlationIdFactory(context);
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                requestContext.Properties["CorrelationId"] = correlationId;
            }
        }

        foreach (var property in contextProperties)
        {
            var value = property.Value(context);
            if (!string.IsNullOrWhiteSpace(value))
            {
                requestContext.Properties[property.Key] = value;
            }
        }
    }

    private static RequestContext EnsureRequestContext(SendOptions options)
    {
        options.Context ??= new RequestContext();
        return options.Context;
    }

    private static RequestContext EnsureRequestContext(PublishOptions options)
    {
        options.Context ??= new RequestContext();
        return options.Context;
    }

    private static void ApplyTransportProperties<TData>(
        IDictionary<string, object> properties,
        OrchestrationContext<TData> context,
        Func<OrchestrationContext<TData>, string> correlationIdFactory,
        Func<OrchestrationContext<TData>, string> flowIdFactory,
        IReadOnlyList<KeyValuePair<string, Func<OrchestrationContext<TData>, object>>> mappedProperties)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (correlationIdFactory is not null)
        {
            var correlationId = correlationIdFactory(context);
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                properties[Constants.CorrelationIdKey] = correlationId;
            }
        }

        if (flowIdFactory is not null)
        {
            var flowId = flowIdFactory(context);
            if (!string.IsNullOrWhiteSpace(flowId))
            {
                properties[Constants.FlowIdKey] = flowId;
            }
        }

        foreach (var property in mappedProperties)
        {
            var value = property.Value(context);
            if (value is not null)
            {
                properties[property.Key] = value;
            }
        }
    }

    private static void ApplyPipelineItems<TData, TPipelineContext>(
        TPipelineContext pipelineContext,
        OrchestrationContext<TData> orchestrationContext,
        IReadOnlyList<KeyValuePair<string, Func<OrchestrationContext<TData>, object>>> items)
        where TData : class, IOrchestrationData
        where TPipelineContext : PipelineContextBase
    {
        ArgumentNullException.ThrowIfNull(pipelineContext);

        foreach (var item in items)
        {
            var value = item.Value(orchestrationContext);
            if (value is not null)
            {
                pipelineContext.Pipeline.Items[item.Key] = value;
            }
        }
    }

    private static TPipelineContext CreatePipelineContext<TPipelineContext>(string activityLabel)
        where TPipelineContext : PipelineContextBase
    {
        try
        {
            return Activator.CreateInstance<TPipelineContext>()
                ?? throw new InvalidOperationException($"{activityLabel} could not create a pipeline context of type {typeof(TPipelineContext).Name}.");
        }
        catch (MissingMethodException exception)
        {
            throw new InvalidOperationException(
                $"{activityLabel} requires either a configured pipeline context factory or a public parameterless constructor on {typeof(TPipelineContext).Name}.",
                exception);
        }
    }

    private static string DescribeFailure(IResult result, string fallbackMessage)
    {
        var errors = result.Errors?
            .Where(error => !string.IsNullOrWhiteSpace(error?.Message))
            .Select(error => error.Message)
            .ToArray();
        if (errors?.Length > 0)
        {
            return string.Join("; ", errors);
        }

        var messages = result.Messages?
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();
        if (messages?.Length > 0)
        {
            return string.Join("; ", messages);
        }

        return fallbackMessage;
    }
}
