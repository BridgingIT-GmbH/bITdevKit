// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides a low-friction direct-job test harness for ordinary xUnit tests.
/// </summary>
/// <example>
/// <code>
/// var harness = JobTestHarness.Create()
///     .WithJob&lt;CleanupJob&gt;("cleanup")
///     .Build();
/// </code>
/// </example>
public sealed class JobTestHarness : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly Type jobType;

    internal JobTestHarness(ServiceProvider provider, Type jobType, IJobExecutionContext context)
    {
        this.provider = provider;
        this.jobType = jobType;
        this.Context = context;
    }

    /// <summary>
    /// Gets the service provider backing the harness.
    /// </summary>
    public IServiceProvider Services => this.provider;

    /// <summary>
    /// Gets the synthetic execution context.
    /// </summary>
    public IJobExecutionContext Context { get; }

    /// <summary>
    /// Creates a fluent builder for direct job tests.
    /// </summary>
    public static JobTestHarnessBuilder Create() => new();

    /// <summary>
    /// Executes the configured job.
    /// </summary>
    public async Task<IResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var job = (IJob)this.provider.GetRequiredService(this.jobType);
        return await job.ExecuteAsync(this.Context, cancellationToken == default ? this.Context.CancellationToken : cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the configured job as the requested concrete type.
    /// </summary>
    public async Task<IResult> ExecuteAsync<TJob>(CancellationToken cancellationToken = default)
        where TJob : class, IJob
    {
        if (this.jobType != typeof(TJob))
        {
            throw new InvalidOperationException($"The harness is configured for '{this.jobType.FullName}', not '{typeof(TJob).FullName}'.");
        }

        var job = this.provider.GetRequiredService<TJob>();
        return await ((IJob)job).ExecuteAsync(this.Context, cancellationToken == default ? this.Context.CancellationToken : cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.provider.Dispose();
    }
}

/// <summary>
/// Builds a <see cref="JobTestHarness"/> instance.
/// </summary>
public sealed class JobTestHarnessBuilder
{
    private readonly ServiceCollection services = [];
    private Type jobType;
    private string jobName = "test-job";
    private string triggerName = "manual";
    private Guid occurrenceId = Guid.NewGuid();
    private Guid executionId = Guid.NewGuid();
    private int attemptNumber = 1;
    private string correlationId = Guid.NewGuid().ToString("N");
    private string idempotencyKey = Guid.NewGuid().ToString("N");
    private DateTimeOffset? scheduledUtc;
    private DateTimeOffset dueUtc = new(2026, 05, 26, 09, 00, 00, TimeSpan.Zero);
    private DateTimeOffset startedUtc = new(2026, 05, 26, 09, 00, 00, TimeSpan.Zero);
    private object data = Unit.Value;
    private bool dataConfigured;
    private PropertyBag executionProperties = new();
    private JobExecutionContextSnapshot previousExecution;
    private JobExecutionContextSnapshot previousSuccessfulExecution;
    private CancellationToken cancellationToken;
    private readonly List<string> messages = [];
    private readonly Dictionary<string, object> items = [];

    /// <summary>
    /// Registers the concrete job type under test.
    /// </summary>
    public JobTestHarnessBuilder WithJob<TJob>(string jobName)
        where TJob : class, IJob
    {
        this.jobType = typeof(TJob);
        this.jobName = string.IsNullOrWhiteSpace(jobName) ? this.jobName : jobName.Trim();
        this.services.AddTransient<TJob>();
        return this;
    }

    /// <summary>
    /// Registers a test service instance.
    /// </summary>
    public JobTestHarnessBuilder WithService<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        this.services.AddSingleton(instance);
        return this;
    }

    /// <summary>
    /// Sets the trigger name for the synthetic context.
    /// </summary>
    public JobTestHarnessBuilder WithTriggerName(string value)
    {
        this.triggerName = string.IsNullOrWhiteSpace(value) ? this.triggerName : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the scheduled UTC instant for the synthetic context.
    /// </summary>
    public JobTestHarnessBuilder WithScheduledUtc(DateTimeOffset? value)
    {
        this.scheduledUtc = value;
        return this;
    }

    /// <summary>
    /// Sets the due UTC instant for the synthetic context.
    /// </summary>
    public JobTestHarnessBuilder WithDueUtc(DateTimeOffset value)
    {
        this.dueUtc = value;
        return this;
    }

    /// <summary>
    /// Sets the started UTC instant for the synthetic context.
    /// </summary>
    public JobTestHarnessBuilder WithStartedUtc(DateTimeOffset value)
    {
        this.startedUtc = value;
        return this;
    }

    /// <summary>
    /// Sets the typed or untyped payload.
    /// </summary>
    public JobTestHarnessBuilder WithData<TData>(TData value)
    {
        this.data = value;
        this.dataConfigured = true;
        return this;
    }

    /// <summary>
    /// Sets immutable properties.
    /// </summary>
    public JobTestHarnessBuilder WithProperties(PropertyBag value)
    {
        this.executionProperties = value?.Clone() ?? new PropertyBag();
        return this;
    }

    /// <summary>
    /// Sets the correlation identifier.
    /// </summary>
    public JobTestHarnessBuilder WithCorrelationId(string value)
    {
        this.correlationId = string.IsNullOrWhiteSpace(value) ? this.correlationId : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the idempotency key.
    /// </summary>
    public JobTestHarnessBuilder WithIdempotencyKey(string value)
    {
        this.idempotencyKey = string.IsNullOrWhiteSpace(value) ? this.idempotencyKey : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the cancellation token.
    /// </summary>
    public JobTestHarnessBuilder WithCancellationToken(CancellationToken value)
    {
        this.cancellationToken = value;
        return this;
    }

    /// <summary>
    /// Sets the previous execution snapshot.
    /// </summary>
    public JobTestHarnessBuilder WithPreviousExecution(Action<JobExecutionContextSnapshotBuilder> configure)
    {
        var builder = new JobExecutionContextSnapshotBuilder(this.jobName, this.triggerName);
        configure?.Invoke(builder);
        this.previousExecution = builder.Build();
        return this;
    }

    /// <summary>
    /// Sets the previous successful execution snapshot.
    /// </summary>
    public JobTestHarnessBuilder WithPreviousSuccessfulExecution(Action<JobExecutionContextSnapshotBuilder> configure)
    {
        var builder = new JobExecutionContextSnapshotBuilder(this.jobName, this.triggerName);
        configure?.Invoke(builder);
        this.previousSuccessfulExecution = builder.Build();
        return this;
    }

    /// <summary>
    /// Adds an initial context message.
    /// </summary>
    public JobTestHarnessBuilder WithMessage(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            this.messages.Add(value);
        }

        return this;
    }

    /// <summary>
    /// Adds an initial context item.
    /// </summary>
    public JobTestHarnessBuilder WithItem(string key, object value)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            this.items[key.Trim()] = value;
        }

        return this;
    }

    /// <summary>
    /// Builds the direct job test harness.
    /// </summary>
    public JobTestHarness Build()
    {
        if (this.jobType is null)
        {
            throw new InvalidOperationException("Job test harness requires a configured job type.");
        }

        var dataType = ResolveJobDataType(this.jobType);
        var context = BuildContext(dataType,
            this.jobName,
            this.triggerName,
            this.occurrenceId,
            this.executionId,
            this.attemptNumber,
            this.correlationId,
            this.idempotencyKey,
            this.scheduledUtc,
            this.dueUtc,
            this.startedUtc,
            this.dataConfigured ? this.data : dataType == typeof(Unit) ? Unit.Value : null,
            this.executionProperties,
            this.previousExecution,
            this.previousSuccessfulExecution,
            this.cancellationToken,
            this.messages,
            this.items);

        return new JobTestHarness(this.services.BuildServiceProvider(), this.jobType, context);
    }

    private static Type ResolveJobDataType(Type jobType)
    {
        return jobType.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IJob<>))
            ?.GetGenericArguments()[0] ?? typeof(Unit);
    }

    private static IJobExecutionContext BuildContext(
        Type dataType,
        string jobName,
        string triggerName,
        Guid occurrenceId,
        Guid executionId,
        int attemptNumber,
        string correlationId,
        string idempotencyKey,
        DateTimeOffset? scheduledUtc,
        DateTimeOffset dueUtc,
        DateTimeOffset startedUtc,
        object data,
        PropertyBag executionProperties,
        JobExecutionContextSnapshot previousExecution,
        JobExecutionContextSnapshot previousSuccessfulExecution,
        CancellationToken cancellationToken,
        IReadOnlyList<string> messages,
        IReadOnlyDictionary<string, object> items)
    {
        var builderType = typeof(JobExecutionContextBuilder<>).MakeGenericType(dataType);
        var builder = Activator.CreateInstance(builderType)
            ?? throw new InvalidOperationException($"Unable to create execution context builder for '{dataType.FullName}'.");

        Invoke(builderType, builder, "WithJobName", jobName);
        Invoke(builderType, builder, "WithTriggerName", triggerName);
        Invoke(builderType, builder, "WithOccurrenceId", occurrenceId);
        Invoke(builderType, builder, "WithExecutionId", executionId);
        Invoke(builderType, builder, "WithAttemptNumber", attemptNumber);
        Invoke(builderType, builder, "WithCorrelationId", correlationId);
        Invoke(builderType, builder, "WithIdempotencyKey", idempotencyKey);
        Invoke(builderType, builder, "WithScheduledUtc", scheduledUtc);
        Invoke(builderType, builder, "WithDueUtc", dueUtc);
        Invoke(builderType, builder, "WithStartedUtc", startedUtc);
        Invoke(builderType, builder, "WithProperties", executionProperties);
        Invoke(builderType, builder, "WithPreviousExecution", previousExecution);
        Invoke(builderType, builder, "WithPreviousSuccessfulExecution", previousSuccessfulExecution);
        Invoke(builderType, builder, "WithCancellationToken", cancellationToken);

        if (data is not null)
        {
            if (dataType != typeof(Unit) && !dataType.IsInstanceOfType(data))
            {
                throw new InvalidOperationException($"The configured test data type '{data.GetType().FullName}' does not match job data contract '{dataType.FullName}'.");
            }

            Invoke(builderType, builder, "WithData", dataType == typeof(Unit) && data is not Unit ? Unit.Value : data);
        }

        foreach (var message in messages)
        {
            Invoke(builderType, builder, "WithMessage", message);
        }

        foreach (var (key, value) in items)
        {
            Invoke(builderType, builder, "WithItem", key, value);
        }

        return (IJobExecutionContext)(builderType.GetMethod("Build")?.Invoke(builder, null)
            ?? throw new InvalidOperationException("Unable to build synthetic job execution context."));
    }

    private static void Invoke(Type builderType, object instance, string methodName, params object[] arguments)
    {
        var method = builderType.GetMethod(methodName)
            ?? throw new InvalidOperationException($"Execution context builder method '{methodName}' was not found.");
        method.Invoke(instance, arguments);
    }
}