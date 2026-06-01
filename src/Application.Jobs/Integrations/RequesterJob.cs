// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Executes a Requester-backed outbound job registered through <see cref="JobSchedulerIntegrationExtensions.WithRequesterJob{TData, TRequest, TValue}(Microsoft.Extensions.DependencyInjection.JobSchedulerBuilderContext, string, Action{JobRequesterDefinitionBuilder{TData, TRequest, TValue}})"/>.
/// </summary>
/// <typeparam name="TData">The typed job data contract.</typeparam>
/// <typeparam name="TRequest">The dispatched request type.</typeparam>
/// <typeparam name="TValue">The request result value type.</typeparam>
public sealed class RequesterJob<TData, TRequest, TValue> : JobBase<TData>
    where TRequest : class, IRequest<TValue>
{
    private readonly IServiceProvider serviceProvider;
    private readonly RequesterJobRegistrationStore registrations;

    internal RequesterJob(
        IServiceProvider serviceProvider,
        RequesterJobRegistrationStore registrations)
    {
        this.serviceProvider = serviceProvider;
        this.registrations = registrations;
    }

    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<TData> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = this.registrations.Get<TData, TRequest, TValue>(context.JobName);
        if (settings is null)
        {
            return Result.Failure().WithError(new ValidationError($"No Requester integration is registered for job '{context.JobName}'."));
        }

        var requester = this.serviceProvider.GetService<IRequester>();
        if (requester is null)
        {
            return Result.Failure().WithError(new ValidationError($"IRequester is not registered for job '{context.JobName}'."));
        }

        var request = settings.RequestFactory(context);
        if (request is null)
        {
            return Result.Failure().WithError(new ValidationError($"The Requester job '{context.JobName}' produced a null request payload."));
        }

        var options = new SendOptions();
        settings.OptionsConfigurator?.Invoke(context, options);
        ApplyRequestContext(options, context, settings.ContextProperties);

        var result = await requester.SendAsync<TRequest, TValue>(request, options, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Result.Failure().WithErrors(result.Errors).WithMessages(result.Messages);
        }

        context.Messages.Add($"Requester integration dispatched '{typeof(TRequest).Name}'.");
        return Result.Success().WithMessages(result.Messages);
    }

    private static void ApplyRequestContext(
        SendOptions options,
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

internal sealed class RequesterJobSettings<TData, TRequest, TValue>
    where TRequest : class, IRequest<TValue>
{
    public required Func<IJobExecutionContext<TData>, TRequest> RequestFactory { get; init; }

    public Action<IJobExecutionContext<TData>, SendOptions> OptionsConfigurator { get; init; }

    public IReadOnlyList<KeyValuePair<string, Func<IJobExecutionContext<TData>, string>>> ContextProperties { get; init; } = [];
}

internal sealed class RequesterJobRegistrationStore
{
    private readonly Dictionary<string, object> registrations = new(StringComparer.OrdinalIgnoreCase);

    public void Add<TData, TRequest, TValue>(string jobName, RequesterJobSettings<TData, TRequest, TValue> settings)
        where TRequest : class, IRequest<TValue>
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (this.registrations.ContainsKey(jobName))
        {
            throw new InvalidOperationException($"A Requester integration is already registered for job '{jobName}'.");
        }

        this.registrations[jobName] = settings;
    }

    public RequesterJobSettings<TData, TRequest, TValue> Get<TData, TRequest, TValue>(string jobName)
        where TRequest : class, IRequest<TValue>
    {
        return this.registrations.TryGetValue(jobName, out var settings)
            ? settings as RequesterJobSettings<TData, TRequest, TValue>
            : null;
    }
}

/// <summary>
/// Builds a Requester-backed outbound job definition.
/// </summary>
/// <typeparam name="TData">The typed job data contract.</typeparam>
/// <typeparam name="TRequest">The dispatched request type.</typeparam>
/// <typeparam name="TValue">The request result value type.</typeparam>
/// <example>
/// <code>
/// services.AddJobScheduler()
///     .WithRequesterJob&lt;ExportCustomersRequest, ExportCustomersCommand, int&gt;("export-customers", job =&gt; job
///         .WithDescription("Dispatches the export command.")
///         .WithRequest(context =&gt; new ExportCustomersCommand(context.Data.Profile))
///         .MapCorrelationId()
///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
/// </code>
/// </example>
public sealed class JobRequesterDefinitionBuilder<TData, TRequest, TValue>
    : JobOutboundIntegrationDefinitionBuilderBase<JobRequesterDefinitionBuilder<TData, TRequest, TValue>, RequesterJob<TData, TRequest, TValue>, TData>
    where TRequest : class, IRequest<TValue>
{
    private readonly List<KeyValuePair<string, Func<IJobExecutionContext<TData>, string>>> contextProperties = [];
    private Func<IJobExecutionContext<TData>, TRequest> requestFactory;
    private Action<IJobExecutionContext<TData>, SendOptions> optionsConfigurator;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobRequesterDefinitionBuilder{TData, TRequest, TValue}"/> class.
    /// </summary>
    /// <param name="jobName">The stable job name.</param>
    public JobRequesterDefinitionBuilder(string jobName)
        : base(jobName)
    {
    }

    /// <summary>
    /// Configures the request payload factory.
    /// </summary>
    public JobRequesterDefinitionBuilder<TData, TRequest, TValue> WithRequest(Func<IJobExecutionContext<TData>, TRequest> factory)
    {
        this.requestFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    /// <summary>
    /// Configures the send options for each execution.
    /// </summary>
    public JobRequesterDefinitionBuilder<TData, TRequest, TValue> ConfigureSendOptions(Action<IJobExecutionContext<TData>, SendOptions> configure)
    {
        this.optionsConfigurator = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    /// <summary>
    /// Maps a computed execution value into the Requester context properties.
    /// </summary>
    public JobRequesterDefinitionBuilder<TData, TRequest, TValue> MapContextProperty(
        string key,
        Func<IJobExecutionContext<TData>, string> valueFactory)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"The Requester job '{this.JobName}' requires a non-empty context property key.");
        }

        this.contextProperties.Add(new KeyValuePair<string, Func<IJobExecutionContext<TData>, string>>(key.Trim(), valueFactory ?? throw new ArgumentNullException(nameof(valueFactory))));
        return this;
    }

    /// <summary>
    /// Maps a job property value into the Requester context properties.
    /// </summary>
    public JobRequesterDefinitionBuilder<TData, TRequest, TValue> MapProperty(string propertyKey, string contextPropertyKey = null)
    {
        if (string.IsNullOrWhiteSpace(propertyKey))
        {
            throw new InvalidOperationException($"The Requester job '{this.JobName}' requires a non-empty property key.");
        }

        var resolvedContextPropertyKey = string.IsNullOrWhiteSpace(contextPropertyKey) ? propertyKey.Trim() : contextPropertyKey.Trim();
        return this.MapContextProperty(
            resolvedContextPropertyKey,
            context => context.Properties.Get<string>(propertyKey.Trim()));
    }

    /// <summary>
    /// Maps a correlation identifier into the Requester context properties.
    /// </summary>
    public JobRequesterDefinitionBuilder<TData, TRequest, TValue> MapCorrelationId(
        string contextPropertyKey = "CorrelationId",
        Func<IJobExecutionContext<TData>, string> valueFactory = null)
    {
        return this.MapContextProperty(contextPropertyKey, valueFactory ?? (context => context.CorrelationId));
    }

    internal RequesterJobSettings<TData, TRequest, TValue> BuildSettings()
    {
        if (this.requestFactory is null)
        {
            throw new InvalidOperationException($"The Requester job '{this.JobName}' requires a configured request factory.");
        }

        return new RequesterJobSettings<TData, TRequest, TValue>
        {
            RequestFactory = this.requestFactory,
            OptionsConfigurator = this.optionsConfigurator,
            ContextProperties = this.contextProperties.ToArray(),
        };
    }
}