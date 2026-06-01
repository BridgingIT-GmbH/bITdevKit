// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

internal sealed class RuntimeJobExecutionContext<TData> : IJobExecutionContext<TData>
{
    public RuntimeJobExecutionContext(
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
    {
        this.JobName = jobName;
        this.TriggerName = triggerName;
        this.OccurrenceId = occurrenceId;
        this.ExecutionId = executionId;
        this.AttemptNumber = attemptNumber;
        this.CorrelationId = correlationId;
        this.IdempotencyKey = idempotencyKey;
        this.ScheduledUtc = scheduledUtc;
        this.DueUtc = dueUtc;
        this.StartedUtc = startedUtc;
        this.Data = data;
        this.DataType = dataType;
        this.Properties = properties?.Clone() ?? new PropertyBag();
        this.PreviousExecution = previousExecution;
        this.PreviousSuccessfulExecution = previousSuccessfulExecution;
        this.CancellationToken = cancellationToken;
    }

    public string JobName { get; }

    public string TriggerName { get; }

    public Guid OccurrenceId { get; }

    public Guid ExecutionId { get; }

    public int AttemptNumber { get; }

    public string CorrelationId { get; }

    public string IdempotencyKey { get; }

    public DateTimeOffset? ScheduledUtc { get; }

    public DateTimeOffset DueUtc { get; }

    public DateTimeOffset StartedUtc { get; }

    public TData Data { get; }

    object IJobExecutionContext.Data => this.Data;

    public Type DataType { get; }

    public PropertyBag Properties { get; }

    public ICollection<string> Messages { get; } = [];

    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

    public JobExecutionContextSnapshot PreviousExecution { get; }

    public JobExecutionContextSnapshot PreviousSuccessfulExecution { get; }

    public CancellationToken CancellationToken { get; }
}