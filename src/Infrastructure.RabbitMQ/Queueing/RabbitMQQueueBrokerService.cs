// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.RabbitMQ;

using System.Collections.Concurrent;
using Application.Queueing;
using Common;

/// <summary>
///     Provides operational access to the RabbitMQ queue broker runtime state.
/// </summary>
/// <remarks>
///     Because RabbitMQ does not retain processed message history, this service tracks
///     a bounded set of recent messages in memory. Older messages are evicted when
///     the capacity limit is reached.
/// </remarks>
public class RabbitMQQueueBrokerService : IQueueBrokerService
{
    private const int MaxTrackedMessages = 10000;
    private readonly ConcurrentDictionary<Guid, RabbitMQQueueTrackedItem> trackedItems = new();
    private readonly QueueBrokerControlState controlState;
    private readonly QueueingRegistrationStore registrationStore;
    private readonly object evictionLock = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="RabbitMQQueueBrokerService" /> class.
    /// </summary>
    /// <param name="controlState">The runtime control state.</param>
    /// <param name="registrationStore">The subscription registration store.</param>
    public RabbitMQQueueBrokerService(
        QueueBrokerControlState controlState = null,
        QueueingRegistrationStore registrationStore = null)
    {
        this.controlState = controlState ?? new QueueBrokerControlState();
        this.registrationStore = registrationStore ?? new QueueingRegistrationStore();
    }

    /// <summary>
    ///     Gets the runtime control state.
    /// </summary>
    public QueueBrokerControlState ControlState => this.controlState;

    /// <summary>
    ///     Tracks a message that has been enqueued.
    /// </summary>
    /// <param name="message">The queue message.</param>
    /// <param name="queueName">The queue name.</param>
    /// <param name="type">The message type.</param>
    public void TrackEnqueued(IQueueMessage message, string queueName, string type)
    {
        var id = Guid.NewGuid();
        var item = new RabbitMQQueueTrackedItem
        {
            Id = id,
            MessageId = message.MessageId,
            QueueName = queueName,
            Type = type,
            Status = QueueMessageStatus.Pending,
            CreatedDate = DateTimeOffset.UtcNow,
            Properties = message.Properties?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? []
        };

        this.trackedItems[id] = item;
        this.EvictIfNeeded();
    }

    /// <summary>
    ///     Tracks a message that has been consumed from RabbitMQ.
    /// </summary>
    /// <param name="message">The queue message.</param>
    /// <param name="queueName">The queue name.</param>
    /// <param name="type">The message type.</param>
    public void TrackConsumed(IQueueMessage message, string queueName, string type)
    {
        // Try to find existing by MessageId, otherwise create new
        var existing = this.trackedItems.Values.FirstOrDefault(i => i.MessageId == message.MessageId);
        if (existing is not null)
        {
            existing.Status = QueueMessageStatus.Processing;
            existing.LockedBy = Environment.MachineName;
            existing.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(1);
            existing.ProcessingStartedDate = DateTimeOffset.UtcNow;
        }
        else
        {
            var id = Guid.NewGuid();
            this.trackedItems[id] = new RabbitMQQueueTrackedItem
            {
                Id = id,
                MessageId = message.MessageId,
                QueueName = queueName,
                Type = type,
                Status = QueueMessageStatus.Processing,
                CreatedDate = message.Timestamp,
                LockedBy = Environment.MachineName,
                LockedUntil = DateTimeOffset.UtcNow.AddMinutes(1),
                ProcessingStartedDate = DateTimeOffset.UtcNow,
                Properties = message.Properties?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? []
            };
            this.EvictIfNeeded();
        }
    }

    /// <summary>
    ///     Tracks the processing result for a message.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="status">The resulting status.</param>
    /// <param name="attemptCount">The attempt count, if applicable.</param>
    public void TrackResult(string messageId, QueueMessageStatus status, int attemptCount = 0)
    {
        var existing = this.trackedItems.Values.FirstOrDefault(i => i.MessageId == messageId);
        if (existing is not null)
        {
            existing.Status = status;
            existing.AttemptCount = attemptCount;
            existing.ProcessedDate = DateTimeOffset.UtcNow;
            existing.LockedBy = null;
            existing.LockedUntil = null;

            if (status == QueueMessageStatus.Failed)
            {
                existing.LastError = $"Processing failed (attempt {attemptCount})";
            }
        }
    }

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

        IEnumerable<RabbitMQQueueTrackedItem> query = this.trackedItems.Values
            .OrderByDescending(item => item.CreatedDate);

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(item => item.Type.Contains(type, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(queueName))
        {
            query = query.Where(item => item.QueueName.Contains(queueName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(messageId))
        {
            query = query.Where(item => item.MessageId.Contains(messageId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(lockedBy))
        {
            query = query.Where(item => string.Equals(item.LockedBy, lockedBy, StringComparison.OrdinalIgnoreCase));
        }

        if (isArchived.HasValue)
        {
            query = query.Where(item => item.IsArchived == isArchived.Value);
        }

        if (createdAfter.HasValue)
        {
            query = query.Where(item => item.CreatedDate >= createdAfter.Value);
        }

        if (createdBefore.HasValue)
        {
            query = query.Where(item => item.CreatedDate <= createdBefore.Value);
        }

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        return Task.FromResult(query.Select(MapInfo).ToArray().AsEnumerable());
    }

    /// <inheritdoc />
    public Task<QueueMessageInfo> GetMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            this.trackedItems.TryGetValue(id, out var item) ? MapInfo(item) : null);
    }

    /// <inheritdoc />
    public Task<QueueMessageContentInfo> GetMessageContentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!this.trackedItems.TryGetValue(id, out var item))
        {
            return Task.FromResult<QueueMessageContentInfo>(null);
        }

        return Task.FromResult(new QueueMessageContentInfo
        {
            Id = item.Id,
            MessageId = item.MessageId,
            QueueName = item.QueueName,
            Type = item.Type,
            Content = item.Content,
            ContentHash = item.ContentHash,
            CreatedDate = item.CreatedDate,
            IsArchived = item.IsArchived
        });
    }

    /// <inheritdoc />
    public Task<QueueMessageStats> GetMessageStatsAsync(
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        bool? isArchived = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var messages = this.trackedItems.Values.AsEnumerable();

        if (isArchived.HasValue)
        {
            messages = messages.Where(item => item.IsArchived == isArchived.Value);
        }

        if (startDate.HasValue)
        {
            messages = messages.Where(item => (item.ProcessedDate ?? item.CreatedDate) >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            messages = messages.Where(item => (item.ProcessedDate ?? item.CreatedDate) <= endDate.Value);
        }

        var materialized = messages.ToList();

        return Task.FromResult(new QueueMessageStats
        {
            Total = materialized.Count,
            Pending = materialized.Count(item => item.Status == QueueMessageStatus.Pending),
            WaitingForHandler = materialized.Count(item => item.Status == QueueMessageStatus.WaitingForHandler),
            Processing = materialized.Count(item => item.Status == QueueMessageStatus.Processing),
            Succeeded = materialized.Count(item => item.Status == QueueMessageStatus.Succeeded),
            Failed = materialized.Count(item => item.Status == QueueMessageStatus.Failed),
            DeadLettered = materialized.Count(item => item.Status == QueueMessageStatus.DeadLettered),
            Expired = materialized.Count(item => item.Status == QueueMessageStatus.Expired),
            Archived = materialized.Count(item => item.IsArchived),
            Leased = materialized.Count(item => !string.IsNullOrWhiteSpace(item.LockedBy) && item.LockedUntil > DateTimeOffset.UtcNow),
            PausedQueues = this.controlState.GetPausedQueues(),
            PausedTypes = this.controlState.GetPausedTypes(),
            OpenCircuits = []
        });
    }

    /// <inheritdoc />
    public Task<QueueBrokerSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var values = this.trackedItems.Values.ToList();

        return Task.FromResult(new QueueBrokerSummary
        {
            Total = values.Count,
            Pending = values.Count(item => item.Status == QueueMessageStatus.Pending),
            WaitingForHandler = values.Count(item => item.Status == QueueMessageStatus.WaitingForHandler),
            Processing = values.Count(item => item.Status == QueueMessageStatus.Processing),
            Succeeded = values.Count(item => item.Status == QueueMessageStatus.Succeeded),
            Failed = values.Count(item => item.Status == QueueMessageStatus.Failed),
            Expired = values.Count(item => item.Status == QueueMessageStatus.Expired),
            PausedQueues = this.controlState.GetPausedQueues(),
            PausedTypes = this.controlState.GetPausedTypes(),
            Capabilities = new QueueBrokerCapabilities
            {
                SupportsDurableStorage = false,
                SupportsRetry = false,
                SupportsArchive = true,
                SupportsLeaseManagement = false,
                SupportsCircuitManagement = false,
                SupportsPauseResume = true,
                SupportsWaitingMessageInspection = true
            }
        });
    }

    /// <inheritdoc />
    public Task<IEnumerable<QueueSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<QueueSubscriptionInfo> result = this.registrationStore.Subscriptions
            .Select(item => new QueueSubscriptionInfo
            {
                QueueName = this.GetQueueName(item.MessageType),
                MessageType = item.MessageType.PrettyName(false),
                HandlerType = item.HandlerType.FullName,
                IsQueuePaused = this.controlState.IsQueuePaused(this.GetQueueName(item.MessageType)),
                IsMessageTypePaused = this.controlState.IsMessageTypePaused(item.MessageType.PrettyName(false))
            })
            .OrderBy(item => item.MessageType)
            .ToArray();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IEnumerable<QueueMessageInfo>> GetWaitingMessagesAsync(int? take = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<RabbitMQQueueTrackedItem> query = this.trackedItems.Values
            .Where(item => item.Status == QueueMessageStatus.WaitingForHandler)
            .OrderBy(item => item.CreatedDate);

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        return Task.FromResult(query.Select(MapInfo).ToArray().AsEnumerable());
    }

    /// <inheritdoc />
    public Task RetryMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (this.trackedItems.TryGetValue(id, out var item))
        {
            item.Status = QueueMessageStatus.Pending;
            item.ProcessedDate = null;
            item.ProcessingStartedDate = null;
            item.LastError = null;
            item.IsArchived = false;
            item.ArchivedDate = null;
            item.LockedBy = null;
            item.LockedUntil = null;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseLeaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (this.trackedItems.TryGetValue(id, out var item))
        {
            item.LockedBy = null;
            item.LockedUntil = null;
            if (item.Status == QueueMessageStatus.Processing)
            {
                item.Status = QueueMessageStatus.Pending;
                item.ProcessingStartedDate = null;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PauseQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.controlState.PauseQueue(queueName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.controlState.ResumeQueue(queueName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PauseMessageTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.controlState.PauseMessageType(type);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeMessageTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.controlState.ResumeMessageType(type);
        return Task.CompletedTask;
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

        if (this.trackedItems.TryGetValue(id, out var item))
        {
            item.IsArchived = true;
            item.ArchivedDate ??= DateTimeOffset.UtcNow;
        }

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

        var statusSet = statuses?.ToHashSet();
        var ids = this.trackedItems.Values
            .Where(item => !olderThan.HasValue || item.CreatedDate <= olderThan.Value)
            .Where(item => statusSet is null || statusSet.Count == 0 || statusSet.Contains(item.Status))
            .Where(item => !isArchived.HasValue || item.IsArchived == isArchived.Value)
            .Select(item => item.Id)
            .ToList();

        foreach (var id in ids)
        {
            this.trackedItems.TryRemove(id, out _);
        }

        return Task.CompletedTask;
    }

    private void EvictIfNeeded()
    {
        if (this.trackedItems.Count <= MaxTrackedMessages)
        {
            return;
        }

        lock (this.evictionLock)
        {
            if (this.trackedItems.Count <= MaxTrackedMessages)
            {
                return;
            }

            var toEvict = this.trackedItems.Values
                .OrderBy(item => item.LastAccessedDate)
                .Take(this.trackedItems.Count - MaxTrackedMessages + 1000) // Evict a batch to reduce churn
                .Select(item => item.Id)
                .ToList();

            foreach (var id in toEvict)
            {
                this.trackedItems.TryRemove(id, out _);
            }
        }
    }

    private string GetQueueName(Type messageType)
    {
        var typeName = messageType.PrettyName(false);
        return typeName;
    }

    private static QueueMessageInfo MapInfo(RabbitMQQueueTrackedItem item)
    {
        return new QueueMessageInfo
        {
            Id = item.Id,
            MessageId = item.MessageId,
            QueueName = item.QueueName,
            Type = item.Type,
            RegisteredHandlerType = item.RegisteredHandlerType,
            IsArchived = item.IsArchived,
            ArchivedDate = item.ArchivedDate,
            Status = item.Status,
            AttemptCount = item.AttemptCount,
            CreatedDate = item.CreatedDate,
            ExpiresOn = item.ExpiresOn,
            LockedBy = item.LockedBy,
            LockedUntil = item.LockedUntil,
            ProcessingStartedDate = item.ProcessingStartedDate,
            ProcessedDate = item.ProcessedDate,
            LastError = item.LastError,
            Properties = item.Properties?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? []
        };
    }
}

/// <summary>
/// Represents a tracked item inside the RabbitMQ queue broker runtime.
/// </summary>
public sealed class RabbitMQQueueTrackedItem
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    public string MessageId { get; init; }

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public string QueueName { get; init; }

    /// <summary>
    /// Gets or sets the message type.
    /// </summary>
    public string Type { get; init; }

    /// <summary>
    /// Gets or sets the registered handler type.
    /// </summary>
    public string RegisteredHandlerType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item is archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the archived date.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public QueueMessageStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the attempt count.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets or sets the expiration date.
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the locked by value.
    /// </summary>
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the locked until value.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the processing started date.
    /// </summary>
    public DateTimeOffset? ProcessingStartedDate { get; set; }

    /// <summary>
    /// Gets or sets the processed date.
    /// </summary>
    public DateTimeOffset? ProcessedDate { get; set; }

    /// <summary>
    /// Gets or sets the last error.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the content hash.
    /// </summary>
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the last accessed date.
    /// </summary>
    public DateTimeOffset LastAccessedDate { get; set; } = DateTimeOffset.UtcNow;
}
