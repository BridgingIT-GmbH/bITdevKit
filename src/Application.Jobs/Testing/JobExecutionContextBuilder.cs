// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Builds a synthetic <see cref="IJobExecutionContext{TData}"/> for direct job tests.
/// </summary>
/// <typeparam name="TData">The typed job data contract.</typeparam>
/// <example>
/// <code>
/// var context = new JobExecutionContextBuilder&lt;Unit&gt;()
///     .WithJobName("cleanup")
///     .WithTriggerName("manual")
///     .Build();
/// </code>
/// </example>
public sealed class JobExecutionContextBuilder<TData>
{
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
    private TData data = default;
    private PropertyBag executionProperties = new();
    private JobExecutionContextSnapshot previousExecution;
    private JobExecutionContextSnapshot previousSuccessfulExecution;
    private CancellationToken cancellationToken;
    private readonly List<string> messages = [];
    private readonly Dictionary<string, object> items = [];

    /// <summary>
    /// Sets the stable job name.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithJobName(string value)
    {
        this.jobName = string.IsNullOrWhiteSpace(value) ? this.jobName : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the trigger name.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithTriggerName(string value)
    {
        this.triggerName = string.IsNullOrWhiteSpace(value) ? this.triggerName : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the occurrence identifier.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithOccurrenceId(Guid value)
    {
        this.occurrenceId = value;
        return this;
    }

    /// <summary>
    /// Sets the execution identifier.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithExecutionId(Guid value)
    {
        this.executionId = value;
        return this;
    }

    /// <summary>
    /// Sets the attempt number.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithAttemptNumber(int value)
    {
        this.attemptNumber = value;
        return this;
    }

    /// <summary>
    /// Sets the correlation identifier.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithCorrelationId(string value)
    {
        this.correlationId = string.IsNullOrWhiteSpace(value) ? this.correlationId : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the idempotency key.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithIdempotencyKey(string value)
    {
        this.idempotencyKey = string.IsNullOrWhiteSpace(value) ? this.idempotencyKey : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the scheduled UTC instant.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithScheduledUtc(DateTimeOffset? value)
    {
        this.scheduledUtc = value;
        return this;
    }

    /// <summary>
    /// Sets the due UTC instant.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithDueUtc(DateTimeOffset value)
    {
        this.dueUtc = value;
        return this;
    }

    /// <summary>
    /// Sets the started UTC instant.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithStartedUtc(DateTimeOffset value)
    {
        this.startedUtc = value;
        return this;
    }

    /// <summary>
    /// Sets the typed data payload.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithData(TData value)
    {
        this.data = value;
        return this;
    }

    /// <summary>
    /// Sets the immutable properties.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithProperties(PropertyBag value)
    {
        this.executionProperties = value?.Clone() ?? new PropertyBag();
        return this;
    }

    /// <summary>
    /// Sets the previous execution snapshot.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithPreviousExecution(JobExecutionContextSnapshot value)
    {
        this.previousExecution = value;
        return this;
    }

    /// <summary>
    /// Sets the previous successful execution snapshot.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithPreviousSuccessfulExecution(JobExecutionContextSnapshot value)
    {
        this.previousSuccessfulExecution = value;
        return this;
    }

    /// <summary>
    /// Sets the execution cancellation token.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithCancellationToken(CancellationToken value)
    {
        this.cancellationToken = value;
        return this;
    }

    /// <summary>
    /// Adds a pre-seeded execution message.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithMessage(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            this.messages.Add(value);
        }

        return this;
    }

    /// <summary>
    /// Adds a pre-seeded execution item.
    /// </summary>
    public JobExecutionContextBuilder<TData> WithItem(string key, object value)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            this.items[key.Trim()] = value;
        }

        return this;
    }

    /// <summary>
    /// Builds the synthetic execution context.
    /// </summary>
    public IJobExecutionContext<TData> Build()
    {
        var context = new SyntheticJobExecutionContext<TData>(
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
            this.data,
            typeof(TData),
            this.executionProperties,
            this.previousExecution,
            this.previousSuccessfulExecution,
            this.cancellationToken);

        foreach (var message in this.messages)
        {
            context.Messages.Add(message);
        }

        foreach (var pair in this.items)
        {
            context.Items[pair.Key] = pair.Value;
        }

        return context;
    }

    private sealed class SyntheticJobExecutionContext<TValue>(
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
        TValue data,
        Type dataType,
        PropertyBag properties,
        JobExecutionContextSnapshot previousExecution,
        JobExecutionContextSnapshot previousSuccessfulExecution,
        CancellationToken cancellationToken) : IJobExecutionContext<TValue>
    {
        public string JobName { get; } = jobName;

        public string TriggerName { get; } = triggerName;

        public Guid OccurrenceId { get; } = occurrenceId;

        public Guid ExecutionId { get; } = executionId;

        public int AttemptNumber { get; } = attemptNumber;

        public string CorrelationId { get; } = correlationId;

        public string IdempotencyKey { get; } = idempotencyKey;

        public DateTimeOffset? ScheduledUtc { get; } = scheduledUtc;

        public DateTimeOffset DueUtc { get; } = dueUtc;

        public DateTimeOffset StartedUtc { get; } = startedUtc;

        public TValue Data { get; } = data;

        object IJobExecutionContext.Data => this.Data;

        public Type DataType { get; } = dataType;

        public PropertyBag Properties { get; } = properties?.Clone() ?? new PropertyBag();

        public ICollection<string> Messages { get; } = [];

        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

        public JobExecutionContextSnapshot PreviousExecution { get; } = previousExecution;

        public JobExecutionContextSnapshot PreviousSuccessfulExecution { get; } = previousSuccessfulExecution;

        public CancellationToken CancellationToken { get; } = cancellationToken;
    }
}

/// <summary>
/// Builds a previous-execution snapshot for synthetic job contexts and harnesses.
/// </summary>
public sealed class JobExecutionContextSnapshotBuilder
{
    private Guid occurrenceId = Guid.NewGuid();
    private Guid executionId = Guid.NewGuid();
    private string jobName = "test-job";
    private string triggerName = "manual";
    private int attemptNumber = 1;
    private JobExecutionStatus status = JobExecutionStatus.Completed;
    private DateTimeOffset startedUtc = new(2026, 05, 26, 09, 00, 00, TimeSpan.Zero);
    private DateTimeOffset? completedUtc = new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero);
    private readonly List<string> messages = [];
    private string errorMessage;

    internal JobExecutionContextSnapshotBuilder(string jobName, string triggerName)
    {
        this.jobName = string.IsNullOrWhiteSpace(jobName) ? this.jobName : jobName.Trim();
        this.triggerName = string.IsNullOrWhiteSpace(triggerName) ? this.triggerName : triggerName.Trim();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExecutionContextSnapshotBuilder"/> class.
    /// </summary>
    public JobExecutionContextSnapshotBuilder()
    {
    }

    /// <summary>
    /// Sets the occurrence identifier.
    /// </summary>
    public JobExecutionContextSnapshotBuilder OccurrenceId(Guid value)
    {
        this.occurrenceId = value;
        return this;
    }

    /// <summary>
    /// Sets the execution identifier.
    /// </summary>
    public JobExecutionContextSnapshotBuilder ExecutionId(Guid value)
    {
        this.executionId = value;
        return this;
    }

    /// <summary>
    /// Sets the job name.
    /// </summary>
    public JobExecutionContextSnapshotBuilder JobName(string value)
    {
        this.jobName = string.IsNullOrWhiteSpace(value) ? this.jobName : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the trigger name.
    /// </summary>
    public JobExecutionContextSnapshotBuilder TriggerName(string value)
    {
        this.triggerName = string.IsNullOrWhiteSpace(value) ? this.triggerName : value.Trim();
        return this;
    }

    /// <summary>
    /// Sets the attempt number.
    /// </summary>
    public JobExecutionContextSnapshotBuilder AttemptNumber(int value)
    {
        this.attemptNumber = value;
        return this;
    }

    /// <summary>
    /// Sets the execution status.
    /// </summary>
    public JobExecutionContextSnapshotBuilder Status(JobExecutionStatus value)
    {
        this.status = value;
        return this;
    }

    /// <summary>
    /// Sets the started UTC instant.
    /// </summary>
    public JobExecutionContextSnapshotBuilder StartedUtc(DateTimeOffset value)
    {
        this.startedUtc = value;
        return this;
    }

    /// <summary>
    /// Sets the completed UTC instant.
    /// </summary>
    public JobExecutionContextSnapshotBuilder CompletedUtc(DateTimeOffset? value)
    {
        this.completedUtc = value;
        return this;
    }

    /// <summary>
    /// Adds a snapshot message.
    /// </summary>
    public JobExecutionContextSnapshotBuilder Message(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            this.messages.Add(value);
        }

        return this;
    }

    /// <summary>
    /// Sets the error message.
    /// </summary>
    public JobExecutionContextSnapshotBuilder ErrorMessage(string value)
    {
        this.errorMessage = value;
        return this;
    }

    /// <summary>
    /// Builds the immutable snapshot.
    /// </summary>
    public JobExecutionContextSnapshot Build()
    {
        return new JobExecutionContextSnapshot(
            this.occurrenceId,
            this.executionId,
            this.jobName,
            this.triggerName,
            this.attemptNumber,
            this.status,
            this.startedUtc,
            this.completedUtc,
            this.messages.ToArray(),
            this.errorMessage);
    }
}

/// <summary>
/// Provides simple assertion-style helpers for synthetic execution contexts.
/// </summary>
public static class JobExecutionContextAssertionExtensions
{
    /// <summary>
    /// Ensures that all expected messages are present.
    /// </summary>
    public static void ShouldHaveMessages(this IJobExecutionContext context, params string[] expectedMessages)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var expectedMessage in expectedMessages ?? [])
        {
            if (!context.Messages.Contains(expectedMessage))
            {
                throw new InvalidOperationException($"The execution context does not contain expected message '{expectedMessage}'.");
            }
        }
    }

    /// <summary>
    /// Ensures that a property exists with the expected value.
    /// </summary>
    public static void ShouldHaveItem(this IJobExecutionContext context, string key, object expectedValue)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Items.TryGetValue(key, out var value))
        {
            throw new InvalidOperationException($"The execution context does not contain property '{key}'.");
        }

        if (!Equals(value, expectedValue))
        {
            throw new InvalidOperationException($"The execution context property '{key}' has value '{value}', expected '{expectedValue}'.");
        }
    }
}