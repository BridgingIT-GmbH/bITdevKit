// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Executes a Notifier-backed outbound job registered through <see cref="JobSchedulerIntegrationExtensions.WithNotifierJob{TData, TNotification}(Microsoft.Extensions.DependencyInjection.JobSchedulerBuilderContext, string, Action{JobNotifierDefinitionBuilder{TData, TNotification}})"/>.
/// </summary>
/// <typeparam name="TData">The typed job data contract.</typeparam>
/// <typeparam name="TNotification">The published notification type.</typeparam>
public sealed class NotifierJob<TData, TNotification> : JobBase<TData>
    where TNotification : class, INotification
{
    private readonly IServiceProvider serviceProvider;
    private readonly NotifierJobRegistrationStore registrations;

    internal NotifierJob(
        IServiceProvider serviceProvider,
        NotifierJobRegistrationStore registrations)
    {
        this.serviceProvider = serviceProvider;
        this.registrations = registrations;
    }

    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<TData> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = this.registrations.Get<TData, TNotification>(context.JobName);
        if (settings is null)
        {
            return Result.Failure().WithError(new ValidationError($"No Notifier integration is registered for job '{context.JobName}'."));
        }

        var notifier = this.serviceProvider.GetService<INotifier>();
        if (notifier is null)
        {
            return Result.Failure().WithError(new ValidationError($"INotifier is not registered for job '{context.JobName}'."));
        }

        var notification = settings.NotificationFactory(context);
        if (notification is null)
        {
            return Result.Failure().WithError(new ValidationError($"The Notifier job '{context.JobName}' produced a null notification payload."));
        }

        var options = new PublishOptions();
        settings.OptionsConfigurator?.Invoke(context, options);
        ApplyRequestContext(options, context, settings.ContextProperties);

        var result = await notifier.PublishAsync(notification, options, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Result.Failure().WithErrors(result.Errors).WithMessages(result.Messages);
        }

        context.Messages.Add($"Notifier integration published '{typeof(TNotification).Name}'.");
        return Result.Success().WithMessages(result.Messages);
    }

    private static void ApplyRequestContext(
        PublishOptions options,
        IJobExecutionContext<TData> context,
        IReadOnlyList<KeyValuePair<string, Func<IJobExecutionContext<TData>, string>>> contextProperties)
    {
        if (contextProperties.Count == 0)
        {
            return;
        }

        options.Context ??= new RequestContext();
        foreach (var property in contextProperties)
        {
            var value = property.Value(context);
            if (!string.IsNullOrWhiteSpace(value))
            {
                options.Context.Properties[property.Key] = value;
            }
        }
    }
}

internal sealed class NotifierJobSettings<TData, TNotification>
    where TNotification : class, INotification
{
    public required Func<IJobExecutionContext<TData>, TNotification> NotificationFactory { get; init; }

    public Action<IJobExecutionContext<TData>, PublishOptions> OptionsConfigurator { get; init; }

    public IReadOnlyList<KeyValuePair<string, Func<IJobExecutionContext<TData>, string>>> ContextProperties { get; init; } = [];
}

internal sealed class NotifierJobRegistrationStore
{
    private readonly Dictionary<string, object> registrations = new(StringComparer.OrdinalIgnoreCase);

    public void Add<TData, TNotification>(string jobName, NotifierJobSettings<TData, TNotification> settings)
        where TNotification : class, INotification
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (this.registrations.ContainsKey(jobName))
        {
            throw new InvalidOperationException($"A Notifier integration is already registered for job '{jobName}'.");
        }

        this.registrations[jobName] = settings;
    }

    public NotifierJobSettings<TData, TNotification> Get<TData, TNotification>(string jobName)
        where TNotification : class, INotification
    {
        return this.registrations.TryGetValue(jobName, out var settings)
            ? settings as NotifierJobSettings<TData, TNotification>
            : null;
    }
}

/// <summary>
/// Builds a Notifier-backed outbound job definition.
/// </summary>
/// <typeparam name="TData">The typed job data contract.</typeparam>
/// <typeparam name="TNotification">The published notification type.</typeparam>
/// <example>
/// <code>
/// services.AddJobScheduler()
///     .WithNotifierJob&lt;UserExportCompletedData, UserExportCompletedNotification&gt;("notify-export-complete", job =&gt; job
///         .WithDescription("Publishes a completion notification.")
///         .WithNotification(context =&gt; new UserExportCompletedNotification(context.Data.ExportId))
///         .MapCorrelationId()
///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
/// </code>
/// </example>
public sealed class JobNotifierDefinitionBuilder<TData, TNotification>
    : JobOutboundIntegrationDefinitionBuilderBase<JobNotifierDefinitionBuilder<TData, TNotification>, NotifierJob<TData, TNotification>, TData>
    where TNotification : class, INotification
{
    private readonly List<KeyValuePair<string, Func<IJobExecutionContext<TData>, string>>> contextProperties = [];
    private Func<IJobExecutionContext<TData>, TNotification> notificationFactory;
    private Action<IJobExecutionContext<TData>, PublishOptions> optionsConfigurator;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobNotifierDefinitionBuilder{TData, TNotification}"/> class.
    /// </summary>
    /// <param name="jobName">The stable job name.</param>
    public JobNotifierDefinitionBuilder(string jobName)
        : base(jobName)
    {
    }

    /// <summary>
    /// Configures the notification payload factory.
    /// </summary>
    public JobNotifierDefinitionBuilder<TData, TNotification> WithNotification(Func<IJobExecutionContext<TData>, TNotification> factory)
    {
        this.notificationFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    /// <summary>
    /// Configures the publish options for each execution.
    /// </summary>
    public JobNotifierDefinitionBuilder<TData, TNotification> ConfigurePublishOptions(Action<IJobExecutionContext<TData>, PublishOptions> configure)
    {
        this.optionsConfigurator = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    /// <summary>
    /// Maps a computed execution value into the Notifier context properties.
    /// </summary>
    public JobNotifierDefinitionBuilder<TData, TNotification> MapContextProperty(
        string key,
        Func<IJobExecutionContext<TData>, string> valueFactory)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"The Notifier job '{this.JobName}' requires a non-empty context property key.");
        }

        this.contextProperties.Add(new KeyValuePair<string, Func<IJobExecutionContext<TData>, string>>(key.Trim(), valueFactory ?? throw new ArgumentNullException(nameof(valueFactory))));
        return this;
    }

    /// <summary>
    /// Maps a job property value into the Notifier context properties.
    /// </summary>
    public JobNotifierDefinitionBuilder<TData, TNotification> MapProperty(string propertyKey, string contextPropertyKey = null)
    {
        if (string.IsNullOrWhiteSpace(propertyKey))
        {
            throw new InvalidOperationException($"The Notifier job '{this.JobName}' requires a non-empty property key.");
        }

        var resolvedContextPropertyKey = string.IsNullOrWhiteSpace(contextPropertyKey) ? propertyKey.Trim() : contextPropertyKey.Trim();
        return this.MapContextProperty(
            resolvedContextPropertyKey,
            context => context.Properties.Get<string>(propertyKey.Trim()));
    }

    /// <summary>
    /// Maps a correlation identifier into the Notifier context properties.
    /// </summary>
    public JobNotifierDefinitionBuilder<TData, TNotification> MapCorrelationId(
        string contextPropertyKey = "CorrelationId",
        Func<IJobExecutionContext<TData>, string> valueFactory = null)
    {
        return this.MapContextProperty(contextPropertyKey, valueFactory ?? (context => context.CorrelationId));
    }

    internal NotifierJobSettings<TData, TNotification> BuildSettings()
    {
        if (this.notificationFactory is null)
        {
            throw new InvalidOperationException($"The Notifier job '{this.JobName}' requires a configured notification factory.");
        }

        return new NotifierJobSettings<TData, TNotification>
        {
            NotificationFactory = this.notificationFactory,
            OptionsConfigurator = this.optionsConfigurator,
            ContextProperties = this.contextProperties.ToArray(),
        };
    }
}