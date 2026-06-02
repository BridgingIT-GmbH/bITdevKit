// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides the default event acceptance implementation for provider-neutral event triggers.
/// </summary>
public sealed class JobEventIngress(TimeProvider timeProvider, IJobStoreProvider storeProvider) : IJobEventIngress
{
    /// <inheritdoc />
    public Task<IResult<JobAcceptedEvent>> AcceptAsync(
        string source,
        object data,
        Type dataType,
        JobAcceptedEventOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (dataType is null)
        {
            return Task.FromResult<IResult<JobAcceptedEvent>>(Result<JobAcceptedEvent>.Failure().WithError(new ValidationError("Accepted event data type cannot be null.")));
        }

        return this.AcceptInternalAsync(source, data, dataType, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IResult<JobAcceptedEvent>> AcceptAsync<TEvent>(
        string source,
        TEvent data,
        JobAcceptedEventOptions options = null,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        return await this.AcceptInternalAsync(source, data, typeof(TEvent), options, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IResult<JobAcceptedEvent>> AcceptInternalAsync(
        string source,
        object data,
        Type dataType,
        JobAcceptedEventOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Result<JobAcceptedEvent>.Failure().WithError(new ValidationError("An event source is required."));
        }

        if (data is null)
        {
            return Result<JobAcceptedEvent>.Failure().WithError(new ValidationError("Accepted event data cannot be null."));
        }

        options ??= new JobAcceptedEventOptions();
        using var activity = JobSchedulerInstrumentation.StartEventAcceptanceActivity(source.Trim(), options.CorrelationId);
        var acceptedEvent = new JobAcceptedEvent
        {
            AcceptedEventId = Guid.NewGuid(),
            Source = source.Trim(),
            Data = data,
            DataType = dataType,
            SourceId = options.SourceId,
            CorrelationId = options.CorrelationId,
            IdempotencyKey = string.IsNullOrWhiteSpace(options.IdempotencyKey)
                ? !string.IsNullOrWhiteSpace(options.SourceId)
                    ? options.SourceId.Trim()
                    : Guid.NewGuid().ToString("N")
                : options.IdempotencyKey.Trim(),
            Properties = options.Properties?.Clone() ?? new PropertyBag(),
            AcceptedUtc = (options.AcceptedUtc ?? timeProvider.GetUtcNow()).ToUniversalTime(),
        };

        var created = await storeProvider.AcceptedEvents.TryAcceptAsync(acceptedEvent, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("jobs.event.idempotency_key", acceptedEvent.IdempotencyKey);
        activity?.SetTag("jobs.operation.success", created);
        JobSchedulerInstrumentation.RecordEventAccepted(acceptedEvent.Source, acceptedEvent.CorrelationId, duplicate: !created);
        return created
            ? Result<JobAcceptedEvent>.Success(acceptedEvent)
            : Result<JobAcceptedEvent>.Success(acceptedEvent).WithMessage($"The accepted event '{acceptedEvent.IdempotencyKey}' was already recorded.");
    }
}
