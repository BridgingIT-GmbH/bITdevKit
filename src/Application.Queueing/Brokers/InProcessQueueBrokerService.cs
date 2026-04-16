namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Provides operational access to the in-process queue broker runtime state.
/// </summary>
public class InProcessQueueBrokerService(
    InProcessQueueBrokerRuntime runtime,
    InProcessQueueBrokerOptions options,
    QueueingRegistrationStore registrationStore,
    QueueBrokerControlState controlState) : IQueueBrokerService
{
    /// <inheritdoc />
    public Task<IEnumerable<QueueMessageInfo>> GetMessagesAsync(
        QueueMessageStatus? status = null,
        string type = null,
        string queueName = null,
        string messageId = null,
        string lockedBy = null,
        bool? isArchived = false,
        DateTimeOffset? createdAfter = null,
        DateTimeOffset? createdBefore = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(runtime.GetMessages(status, type, queueName, messageId, lockedBy, isArchived, createdAfter, createdBefore, take));
    }

    /// <inheritdoc />
    public Task<QueueMessageInfo> GetMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(runtime.GetMessage(id));
    }

    /// <inheritdoc />
    public Task<QueueMessageContentInfo> GetMessageContentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(runtime.GetMessageContent(id, options.Serializer ?? new SystemTextJsonSerializer()));
    }

    /// <inheritdoc />
    public Task<QueueMessageStats> GetMessageStatsAsync(
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        bool? isArchived = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(runtime.GetMessageStats(controlState, startDate, endDate, isArchived));
    }

    /// <inheritdoc />
    public Task<QueueBrokerSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(runtime.GetSummary(controlState));
    }

    /// <inheritdoc />
    public Task<IEnumerable<QueueSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<QueueSubscriptionInfo> result = registrationStore.Subscriptions
            .Select(item => new QueueSubscriptionInfo
            {
                QueueName = this.GetQueueName(item.MessageType),
                MessageType = item.MessageType.PrettyName(false),
                HandlerType = item.HandlerType.FullName,
                IsQueuePaused = controlState.IsQueuePaused(this.GetQueueName(item.MessageType)),
                IsMessageTypePaused = controlState.IsMessageTypePaused(item.MessageType.PrettyName(false))
            })
            .OrderBy(item => item.MessageType)
            .ToArray();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IEnumerable<QueueMessageInfo>> GetWaitingMessagesAsync(int? take = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(runtime.GetWaitingMessages(take));
    }

    /// <inheritdoc />
    public async Task RetryMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await runtime.RetryMessageAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public Task ReleaseLeaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        runtime.ReleaseLease(id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PauseQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        controlState.PauseQueue(queueName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return runtime.ResumeQueueAsync(queueName, controlState, cancellationToken);
    }

    /// <inheritdoc />
    public Task PauseMessageTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        controlState.PauseMessageType(type);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeMessageTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return runtime.ResumeMessageTypeAsync(type, controlState, cancellationToken);
    }

    /// <inheritdoc />
    public Task ResetMessageTypeCircuitAsync(string type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ArchiveMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        runtime.ArchiveMessage(id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PurgeMessagesAsync(
        DateTimeOffset? olderThan = null,
        IEnumerable<QueueMessageStatus> statuses = null,
        bool? isArchived = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        runtime.PurgeMessages(olderThan, statuses, isArchived);
        return Task.CompletedTask;
    }

    private string GetQueueName(Type messageType)
    {
        var typeName = messageType.PrettyName(false);
        return string.Concat(options.QueueNamePrefix, typeName, options.QueueNameSuffix);
    }
}