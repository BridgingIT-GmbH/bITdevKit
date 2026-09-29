// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

internal sealed class RuntimeJobExecutionContext<TData>(
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
    TData data,
    Type dataType,
    PropertyBag properties,
    JobExecutionContextSnapshot previousExecution,
    JobExecutionContextSnapshot previousSuccessfulExecution,
    CancellationToken cancellationToken)
    : IJobExecutionContext<TData>
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

    public TData Data { get; } = data;

    object IJobExecutionContext.Data => this.Data;

    public Type DataType { get; } = dataType;

    public PropertyBag Properties { get; } = properties?.Clone() ?? new PropertyBag();

    public ICollection<string> Messages { get; } = [];

    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

    public JobExecutionContextSnapshot PreviousExecution { get; } = previousExecution;

    public JobExecutionContextSnapshot PreviousSuccessfulExecution { get; } = previousSuccessfulExecution;

    public CancellationToken CancellationToken { get; } = cancellationToken;
}