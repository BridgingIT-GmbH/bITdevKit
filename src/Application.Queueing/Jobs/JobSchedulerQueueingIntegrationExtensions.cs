// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers Queueing-backed outbound jobs.
/// </summary>
public static class JobSchedulerQueueingIntegrationExtensions
{
    /// <summary>
    /// Registers a Queueing-backed outbound send job.
    /// </summary>
    public static JobBuilderContext WithQueueingSendJob<TData, TMessage>(
        this JobBuilderContext context,
        string jobName,
        Action<JobQueueSendDefinitionBuilder<TData, TMessage>> configure)
        where TMessage : class, IQueueMessage
        => context.WithQueueSendJob<TData, TMessage>(jobName, configure);

    public static JobBuilderContext WithQueueSendJob<TData, TMessage>(
        this JobBuilderContext context,
        string jobName,
        Action<JobQueueSendDefinitionBuilder<TData, TMessage>> configure)
        where TMessage : class, IQueueMessage
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new JobQueueSendDefinitionBuilder<TData, TMessage>(jobName);
        configure?.Invoke(builder);

        EnsureRegistrations(context.Services).Add(jobName, builder.BuildSettings());
        context.Services.AddTransient<QueueSendJob<TData, TMessage>>(sp =>
            new QueueSendJob<TData, TMessage>(sp, sp.GetRequiredService<QueueSendJobRegistrationStore>()));
        context.Registrations.Add(builder.BuildDefinition());
        return context;
    }

    private static QueueSendJobRegistrationStore EnsureRegistrations(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(QueueSendJobRegistrationStore));
        if (descriptor?.ImplementationInstance is QueueSendJobRegistrationStore registrations)
        {
            return registrations;
        }

        registrations = new QueueSendJobRegistrationStore();
        services.AddSingleton(registrations);
        return registrations;
    }
}

internal sealed class QueueSendJobRegistrationStore
{
    private readonly Dictionary<string, object> registrations = new(StringComparer.OrdinalIgnoreCase);

    public void Add<TData, TMessage>(string jobName, QueueSendJobSettings<TData, TMessage> settings)
        where TMessage : class, IQueueMessage
    {
        this.registrations[jobName] = settings;
    }

    public QueueSendJobSettings<TData, TMessage> Get<TData, TMessage>(string jobName)
        where TMessage : class, IQueueMessage
    {
        return this.registrations.TryGetValue(jobName, out var settings)
            ? settings as QueueSendJobSettings<TData, TMessage>
            : null;
    }
}

internal sealed class QueueSendJobSettings<TData, TMessage>
    where TMessage : class, IQueueMessage
{
    public required Func<IJobExecutionContext<TData>, TMessage> MessageFactory { get; init; }

    public Action<IJobExecutionContext<TData>, TMessage> MessageConfigurator { get; init; }

    public bool WaitForPersistence { get; init; }

    public IReadOnlyList<KeyValuePair<string, Func<IJobExecutionContext<TData>, object>>> Properties { get; init; } = [];
}

/// <summary>
/// Builds a Queueing send job definition.
/// </summary>
public sealed class JobQueueSendDefinitionBuilder<TData, TMessage>
    : JobOutboundIntegrationDefinitionBuilderBase<JobQueueSendDefinitionBuilder<TData, TMessage>, QueueSendJob<TData, TMessage>, TData>
    where TMessage : class, IQueueMessage
{
    private readonly List<KeyValuePair<string, Func<IJobExecutionContext<TData>, object>>> properties = [];
    private Func<IJobExecutionContext<TData>, TMessage> messageFactory;
    private Action<IJobExecutionContext<TData>, TMessage> messageConfigurator;
    private bool waitForPersistence;

    public JobQueueSendDefinitionBuilder(string jobName)
        : base(jobName)
    {
    }

    public JobQueueSendDefinitionBuilder<TData, TMessage> WithMessage(Func<IJobExecutionContext<TData>, TMessage> factory)
    {
        this.messageFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    public JobQueueSendDefinitionBuilder<TData, TMessage> ConfigureMessage(Action<IJobExecutionContext<TData>, TMessage> configure)
    {
        this.messageConfigurator = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    public JobQueueSendDefinitionBuilder<TData, TMessage> MapProperty(string key, Func<IJobExecutionContext<TData>, object> valueFactory)
    {
        this.properties.Add(new KeyValuePair<string, Func<IJobExecutionContext<TData>, object>>(key, valueFactory));
        return this;
    }

    public JobQueueSendDefinitionBuilder<TData, TMessage> MapContextProperty(string propertyKey, string propertyName = null)
        => this.MapProperty(string.IsNullOrWhiteSpace(propertyName) ? propertyKey : propertyName, (Func<IJobExecutionContext<TData>, object>)(context => (object)(context.Properties.TryGetValue(propertyKey, out var value) ? value : null)));

    public JobQueueSendDefinitionBuilder<TData, TMessage> MapCorrelationId(string propertyName = "CorrelationId", Func<IJobExecutionContext<TData>, string> valueFactory = null)
        => this.MapProperty(propertyName, context => valueFactory is null ? context.CorrelationId : valueFactory(context));

    public JobQueueSendDefinitionBuilder<TData, TMessage> WaitForPersistence(bool value = true)
    {
        this.waitForPersistence = value;
        return this;
    }

    internal QueueSendJobSettings<TData, TMessage> BuildSettings()
    {
        if (this.messageFactory is null)
        {
            throw new InvalidOperationException($"The Queueing job '{this.JobName}' requires a configured queue message factory.");
        }

        return new QueueSendJobSettings<TData, TMessage>
        {
            MessageFactory = this.messageFactory,
            MessageConfigurator = this.messageConfigurator,
            WaitForPersistence = this.waitForPersistence,
            Properties = this.properties.ToArray(),
        };
    }
}
