// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers Messaging-backed outbound jobs.
/// </summary>
public static class JobSchedulerMessagingIntegrationExtensions
{
    /// <summary>
    /// Registers a Messaging-backed outbound publish job.
    /// </summary>
    public static JobBuilderContext WithMessagingPublishJob<TData, TMessage>(
        this JobBuilderContext context,
        string jobName,
        Action<JobMessagePublishDefinitionBuilder<TData, TMessage>> configure)
        where TMessage : class, IMessage
        => context.WithMessagePublishJob<TData, TMessage>(jobName, configure);

    public static JobBuilderContext WithMessagePublishJob<TData, TMessage>(
        this JobBuilderContext context,
        string jobName,
        Action<JobMessagePublishDefinitionBuilder<TData, TMessage>> configure)
        where TMessage : class, IMessage
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new JobMessagePublishDefinitionBuilder<TData, TMessage>(jobName);
        configure?.Invoke(builder);

        EnsureRegistrations(context.Services).Add(jobName, builder.BuildSettings());
        context.Services.AddTransient<MessagePublishJob<TData, TMessage>>(sp =>
            new MessagePublishJob<TData, TMessage>(sp, sp.GetRequiredService<MessagePublishJobRegistrationStore>()));
        context.Registrations.Add(builder.BuildDefinition());
        return context;
    }

    private static MessagePublishJobRegistrationStore EnsureRegistrations(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(MessagePublishJobRegistrationStore));
        if (descriptor?.ImplementationInstance is MessagePublishJobRegistrationStore registrations)
        {
            return registrations;
        }

        registrations = new MessagePublishJobRegistrationStore();
        services.AddSingleton(registrations);
        return registrations;
    }
}

internal sealed class MessagePublishJobSettings<TData, TMessage>
    where TMessage : class, IMessage
{
    public required Func<IJobExecutionContext<TData>, TMessage> MessageFactory { get; init; }

    public Action<IJobExecutionContext<TData>, TMessage> MessageConfigurator { get; init; }

    public IReadOnlyList<KeyValuePair<string, Func<IJobExecutionContext<TData>, object>>> Properties { get; init; } = [];
}

internal sealed class MessagePublishJobRegistrationStore
{
    private readonly Dictionary<string, object> registrations = new(StringComparer.OrdinalIgnoreCase);

    public void Add<TData, TMessage>(string jobName, MessagePublishJobSettings<TData, TMessage> settings)
        where TMessage : class, IMessage
    {
        this.registrations[jobName] = settings;
    }

    public MessagePublishJobSettings<TData, TMessage> Get<TData, TMessage>(string jobName)
        where TMessage : class, IMessage
    {
        return this.registrations.TryGetValue(jobName, out var settings)
            ? settings as MessagePublishJobSettings<TData, TMessage>
            : null;
    }
}

/// <summary>
/// Builds a Messaging publish job definition.
/// </summary>
public sealed class JobMessagePublishDefinitionBuilder<TData, TMessage>
    : JobOutboundIntegrationDefinitionBuilderBase<JobMessagePublishDefinitionBuilder<TData, TMessage>, MessagePublishJob<TData, TMessage>, TData>
    where TMessage : class, IMessage
{
    private readonly List<KeyValuePair<string, Func<IJobExecutionContext<TData>, object>>> properties = [];
    private Func<IJobExecutionContext<TData>, TMessage> messageFactory;
    private Action<IJobExecutionContext<TData>, TMessage> messageConfigurator;

    public JobMessagePublishDefinitionBuilder(string jobName)
        : base(jobName)
    {
    }

    public JobMessagePublishDefinitionBuilder<TData, TMessage> WithMessage(Func<IJobExecutionContext<TData>, TMessage> factory)
    {
        this.messageFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    public JobMessagePublishDefinitionBuilder<TData, TMessage> ConfigureMessage(Action<IJobExecutionContext<TData>, TMessage> configure)
    {
        this.messageConfigurator = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    public JobMessagePublishDefinitionBuilder<TData, TMessage> MapProperty(string key, Func<IJobExecutionContext<TData>, object> valueFactory)
    {
        this.properties.Add(new KeyValuePair<string, Func<IJobExecutionContext<TData>, object>>(key, valueFactory));
        return this;
    }

    public JobMessagePublishDefinitionBuilder<TData, TMessage> MapContextProperty(string propertyKey, string propertyName = null)
        => this.MapProperty(string.IsNullOrWhiteSpace(propertyName) ? propertyKey : propertyName, (Func<IJobExecutionContext<TData>, object>)(context => (object)(context.Properties.TryGetValue(propertyKey, out var value) ? value : null)));

    public JobMessagePublishDefinitionBuilder<TData, TMessage> MapCorrelationId(string propertyName = "CorrelationId", Func<IJobExecutionContext<TData>, string> valueFactory = null)
        => this.MapProperty(propertyName, context => valueFactory is null ? context.CorrelationId : valueFactory(context));

    internal MessagePublishJobSettings<TData, TMessage> BuildSettings()
    {
        if (this.messageFactory is null)
        {
            throw new InvalidOperationException($"The Messaging job '{this.JobName}' requires a configured message factory.");
        }

        return new MessagePublishJobSettings<TData, TMessage>
        {
            MessageFactory = this.messageFactory,
            MessageConfigurator = this.messageConfigurator,
            Properties = this.properties.ToArray(),
        };
    }
}
