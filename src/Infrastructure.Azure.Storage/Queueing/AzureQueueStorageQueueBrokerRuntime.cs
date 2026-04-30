// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;

/// <summary>
/// Tracks lightweight runtime state for the Azure Queue Storage broker to support operational queries.
/// </summary>
public class AzureQueueStorageQueueBrokerRuntime
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, AzureQueueStorageQueueTrackedItem> trackedItems = [];
    private readonly AzureQueueStorageQueueBrokerOptions options;

    /// <summary>
    /// Initializes a new runtime for the Azure Queue Storage broker.
    /// </summary>
    /// <param name="options">The broker options.</param>
    public AzureQueueStorageQueueBrokerRuntime(AzureQueueStorageQueueBrokerOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Tracks a message that was successfully enqueued.
    /// </summary>
    public void TrackEnqueued(IQueueMessage message, string queueName, string type)
    {
        ArgumentNullException.ThrowIfNull(message);

        var item = new AzureQueueStorageQueueTrackedItem
        {
            Id = Guid.NewGuid(),
            MessageId = message.MessageId,
            QueueName = queueName,
            Type = type,
            Status = QueueMessageStatus.Pending,
            CreatedDate = DateTimeOffset.UtcNow,
            ExpiresOn = this.options.MessageExpiration.HasValue
                ? DateTimeOffset.UtcNow.Add(this.options.MessageExpiration.Value)
                : null
        };

        lock (this.syncRoot)
        {
            this.trackedItems[item.Id] = item;
            this.TrimIfNeeded();
        }
    }

    /// <summary>
    /// Tracks a message that has been consumed from the queue.
    /// </summary>
    public void TrackConsumed(IQueueMessage message, string queueName, string type)
    {
        lock (this.syncRoot)
        {
            var existing = this.trackedItems.Values
                .FirstOrDefault(item =>
                    item.MessageId == message.MessageId &&
                    item.QueueName == queueName);

            if (existing is not null)
            {
                existing.Status = QueueMessageStatus.Processing;
                existing.LockedBy = Environment.MachineName;
                existing.LockedUntil = DateTimeOffset.UtcNow.Add(this.options.VisibilityTimeout);
                existing.ProcessingStartedDate = DateTimeOffset.UtcNow;
            }
            else
            {
                var id = Guid.NewGuid();
                this.trackedItems[id] = new AzureQueueStorageQueueTrackedItem
                {
                    Id = id,
                    MessageId = message.MessageId,
                    QueueName = queueName,
                    Type = type,
                    Status = QueueMessageStatus.Processing,
                    CreatedDate = message.Timestamp,
                    LockedBy = Environment.MachineName,
                    LockedUntil = DateTimeOffset.UtcNow.Add(this.options.VisibilityTimeout),
                    ProcessingStartedDate = DateTimeOffset.UtcNow
                };
                this.TrimIfNeeded();
            }
        }
    }

    /// <summary>
    /// Tracks a message that was successfully processed.
    /// </summary>
    public void TrackSucceeded(IQueueMessage message, string queueName, string type)
    {
        lock (this.syncRoot)
        {
            var existing = this.trackedItems.Values
                .FirstOrDefault(item =>
                    item.MessageId == message.MessageId &&
                    item.QueueName == queueName);

            if (existing is not null)
            {
                existing.Status = QueueMessageStatus.Succeeded;
                existing.ProcessedDate = DateTimeOffset.UtcNow;
                existing.LockedBy = null;
                existing.LockedUntil = null;
            }
        }
    }

    /// <summary>
    /// Tracks a message that failed processing.
    /// </summary>
    public void TrackFailed(IQueueMessage message, string queueName, string type, int attemptCount)
    {
        lock (this.syncRoot)
        {
            var existing = this.trackedItems.Values
                .FirstOrDefault(item =>
                    item.MessageId == message.MessageId &&
                    item.QueueName == queueName);

            if (existing is not null)
            {
                existing.Status = QueueMessageStatus.Failed;
                existing.AttemptCount = attemptCount;
                existing.ProcessedDate = DateTimeOffset.UtcNow;
                existing.LockedBy = null;
                existing.LockedUntil = null;
                existing.LastError = $"Processing failed (attempt {attemptCount})";
            }
        }
    }

    /// <summary>
    /// Tracks a message that is waiting for a handler.
    /// </summary>
    public void TrackWaitingForHandler(IQueueMessage message, string queueName, string type)
    {
        lock (this.syncRoot)
        {
            var existing = this.trackedItems.Values
                .FirstOrDefault(item =>
                    item.MessageId == message.MessageId &&
                    item.QueueName == queueName);

            if (existing is not null)
            {
                existing.Status = QueueMessageStatus.WaitingForHandler;
                existing.LockedBy = null;
                existing.LockedUntil = null;
            }
        }
    }

    /// <summary>
    /// Tracks a message that expired before processing.
    /// </summary>
    public void TrackExpired(IQueueMessage message, string queueName, string type)
    {
        lock (this.syncRoot)
        {
            var existing = this.trackedItems.Values
                .FirstOrDefault(item =>
                    item.MessageId == message.MessageId &&
                    item.QueueName == queueName);

            if (existing is not null)
            {
                existing.Status = QueueMessageStatus.Expired;
                existing.ProcessedDate = DateTimeOffset.UtcNow;
                existing.LockedBy = null;
                existing.LockedUntil = null;
            }
        }
    }

    /// <summary>
    /// Tracks a message that was dead-lettered after max delivery attempts.
    /// </summary>
    public void TrackDeadLettered(IQueueMessage message, string queueName, string type, int attemptCount)
    {
        lock (this.syncRoot)
        {
            var existing = this.trackedItems.Values
                .FirstOrDefault(item =>
                    item.MessageId == message.MessageId &&
                    item.QueueName == queueName);

            if (existing is not null)
            {
                existing.Status = QueueMessageStatus.DeadLettered;
                existing.AttemptCount = attemptCount;
                existing.ProcessedDate = DateTimeOffset.UtcNow;
                existing.LockedBy = null;
                existing.LockedUntil = null;
                existing.LastError = $"Dead-lettered after {attemptCount} attempts";
            }
        }
    }

    /// <summary>
    /// Gets a summary of tracked runtime state.
    /// </summary>
    public QueueBrokerSummary GetSummary(QueueBrokerControlState controlState)
    {
        ArgumentNullException.ThrowIfNull(controlState);

        lock (this.syncRoot)
        {
            return new QueueBrokerSummary
            {
                Total = this.trackedItems.Count,
                Pending = this.trackedItems.Values.Count(item => item.Status == QueueMessageStatus.Pending),
                WaitingForHandler = this.trackedItems.Values.Count(item => item.Status == QueueMessageStatus.WaitingForHandler),
                Processing = this.trackedItems.Values.Count(item => item.Status == QueueMessageStatus.Processing),
                Succeeded = this.trackedItems.Values.Count(item => item.Status == QueueMessageStatus.Succeeded),
                Failed = this.trackedItems.Values.Count(item => item.Status == QueueMessageStatus.Failed),
                DeadLettered = this.trackedItems.Values.Count(item => item.Status == QueueMessageStatus.DeadLettered),
                Expired = this.trackedItems.Values.Count(item => item.Status == QueueMessageStatus.Expired),
                PausedQueues = controlState.GetPausedQueues(),
                PausedTypes = controlState.GetPausedTypes(),
                Capabilities = new QueueBrokerCapabilities
                {
                    SupportsDurableStorage = true,
                    SupportsRetry = false,
                    SupportsArchive = false,
                    SupportsLeaseManagement = false,
                    SupportsCircuitManagement = false,
                    SupportsPauseResume = true,
                    SupportsWaitingMessageInspection = true
                }
            };
        }
    }

    /// <summary>
    /// Gets tracked messages matching the supplied filters.
    /// </summary>
    public IEnumerable<QueueMessageInfo> GetMessages(
        QueueMessageStatus? status = null,
        string type = null,
        string queueName = null,
        string messageId = null,
        string lockedBy = null,
        bool? isArchived = false,
        DateTimeOffset? createdAfter = null,
        DateTimeOffset? createdBefore = null,
        int? take = null)
    {
        lock (this.syncRoot)
        {
            IEnumerable<AzureQueueStorageQueueTrackedItem> query = this.trackedItems.Values
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

            return query.Select(MapInfo).ToArray();
        }
    }

    /// <summary>
    /// Gets a single tracked message by its primary key.
    /// </summary>
    public QueueMessageInfo GetMessage(Guid id)
    {
        lock (this.syncRoot)
        {
            return this.trackedItems.TryGetValue(id, out var item)
                ? MapInfo(item)
                : null;
        }
    }

    /// <summary>
    /// Gets aggregate runtime statistics.
    /// </summary>
    public QueueMessageStats GetMessageStats(QueueBrokerControlState controlState, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, bool? isArchived = false)
    {
        ArgumentNullException.ThrowIfNull(controlState);

        lock (this.syncRoot)
        {
            var messages = this.trackedItems.Values.AsEnumerable();

            if (startDate.HasValue)
            {
                messages = messages.Where(item => (item.ProcessedDate ?? item.CreatedDate) >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                messages = messages.Where(item => (item.ProcessedDate ?? item.CreatedDate) <= endDate.Value);
            }

            var materialized = messages.ToList();

            return new QueueMessageStats
            {
                Total = materialized.Count,
                Pending = materialized.Count(item => item.Status == QueueMessageStatus.Pending),
                WaitingForHandler = materialized.Count(item => item.Status == QueueMessageStatus.WaitingForHandler),
                Processing = materialized.Count(item => item.Status == QueueMessageStatus.Processing),
                Succeeded = materialized.Count(item => item.Status == QueueMessageStatus.Succeeded),
                Failed = materialized.Count(item => item.Status == QueueMessageStatus.Failed),
                DeadLettered = materialized.Count(item => item.Status == QueueMessageStatus.DeadLettered),
                Expired = materialized.Count(item => item.Status == QueueMessageStatus.Expired),
                PausedQueues = controlState.GetPausedQueues(),
                PausedTypes = controlState.GetPausedTypes(),
                OpenCircuits = []
            };
        }
    }

    /// <summary>
    /// Gets tracked messages that are waiting for a handler registration.
    /// </summary>
    public IEnumerable<QueueMessageInfo> GetWaitingMessages(int? take = null)
    {
        lock (this.syncRoot)
        {
            IEnumerable<AzureQueueStorageQueueTrackedItem> query = this.trackedItems.Values
                .Where(item => item.Status == QueueMessageStatus.WaitingForHandler)
                .OrderBy(item => item.CreatedDate);

            if (take.HasValue)
            {
                query = query.Take(take.Value);
            }

            return query.Select(MapInfo).ToArray();
        }
    }

    /// <summary>
    /// Retries a tracked message by resetting its status to pending.
    /// </summary>
    public bool RetryMessage(Guid id)
    {
        lock (this.syncRoot)
        {
            if (!this.trackedItems.TryGetValue(id, out var item))
            {
                return false;
            }

            item.Status = QueueMessageStatus.Pending;
            item.ProcessedDate = null;
            item.ProcessingStartedDate = null;
            item.LastError = null;
            item.AttemptCount = 0;
            item.LockedBy = null;
            item.LockedUntil = null;
            return true;
        }
    }

    /// <summary>
    /// Archives a tracked terminal message.
    /// </summary>
    public bool ArchiveMessage(Guid id)
    {
        lock (this.syncRoot)
        {
            if (!this.trackedItems.TryGetValue(id, out var item))
            {
                return false;
            }

            item.IsArchived = true;
            item.ArchivedDate ??= DateTimeOffset.UtcNow;
            return true;
        }
    }

    /// <summary>
    /// Removes tracked messages matching the supplied filters.
    /// </summary>
    public int PurgeMessages(DateTimeOffset? olderThan = null, IEnumerable<QueueMessageStatus> statuses = null, bool? isArchived = null)
    {
        lock (this.syncRoot)
        {
            var statusSet = statuses?.ToHashSet();
            var ids = this.trackedItems.Values
                .Where(item => !olderThan.HasValue || item.CreatedDate <= olderThan.Value)
                .Where(item => statusSet is null || statusSet.Count == 0 || statusSet.Contains(item.Status))
                .Where(item => !isArchived.HasValue || item.IsArchived == isArchived.Value)
                .Select(item => item.Id)
                .ToList();

            foreach (var id in ids)
            {
                this.trackedItems.Remove(id);
            }

            return ids.Count;
        }
    }

    private void TrimIfNeeded()
    {
        const int MaxTrackedItems = 10000;

        if (this.trackedItems.Count > MaxTrackedItems)
        {
            var oldest = this.trackedItems.Values
                .OrderBy(item => item.CreatedDate)
                .Take(this.trackedItems.Count - MaxTrackedItems)
                .Select(item => item.Id)
                .ToList();

            foreach (var id in oldest)
            {
                this.trackedItems.Remove(id);
            }
        }
    }

    private static QueueMessageInfo MapInfo(AzureQueueStorageQueueTrackedItem item)
    {
        return new QueueMessageInfo
        {
            Id = item.Id,
            MessageId = item.MessageId,
            QueueName = item.QueueName,
            Type = item.Type,
            Status = item.Status,
            AttemptCount = item.AttemptCount,
            IsArchived = item.IsArchived,
            ArchivedDate = item.ArchivedDate,
            CreatedDate = item.CreatedDate,
            ExpiresOn = item.ExpiresOn,
            LockedBy = item.LockedBy,
            LockedUntil = item.LockedUntil,
            ProcessingStartedDate = item.ProcessingStartedDate,
            ProcessedDate = item.ProcessedDate,
            LastError = item.LastError
        };
    }
}

/// <summary>
/// Represents a tracked item inside the Azure Queue Storage broker runtime.
/// </summary>
public sealed class AzureQueueStorageQueueTrackedItem
{
    /// <summary>
    /// Gets or sets the internal identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets or sets the logical message identifier.
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
    /// Gets or sets the current status.
    /// </summary>
    public QueueMessageStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the attempt count.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item is archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the archive timestamp.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets or sets the expiration timestamp.
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
    /// Gets or sets the processed timestamp.
    /// </summary>
    public DateTimeOffset? ProcessedDate { get; set; }

    /// <summary>
    /// Gets or sets the latest error.
    /// </summary>
    public string LastError { get; set; }
}
