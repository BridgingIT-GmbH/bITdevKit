namespace BridgingIT.DevKit.Application.Queueing;

using System.Threading.Channels;
using BridgingIT.DevKit.Common;

/// <summary>
/// Stores in-process queue runtime state shared by the broker and the operational service.
/// </summary>
public class InProcessQueueBrokerRuntime
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, InProcessQueueTrackedItem> trackedItems = [];
    private readonly HashSet<Guid> waitingItemIds = [];
    private readonly HashSet<Guid> pausedItemIds = [];
    private readonly Channel<InProcessQueueTrackedItem> channel;

    /// <summary>
    /// Initializes a new runtime store for the in-process broker.
    /// </summary>
    public InProcessQueueBrokerRuntime(InProcessQueueBrokerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var channelOptions = new UnboundedChannelOptions
        {
            SingleReader = options.MaxDegreeOfParallelism == 1,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        this.channel = Channel.CreateUnbounded<InProcessQueueTrackedItem>(channelOptions);
    }

    /// <summary>
    /// Tracks and enqueues a new item for processing.
    /// </summary>
    public async ValueTask EnqueueAsync(InProcessQueueTrackedItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (this.syncRoot)
        {
            this.trackedItems[item.Id] = item;
        }

        await this.channel.Writer.WriteAsync(item, cancellationToken);
    }

    /// <summary>
    /// Completes the internal channel writer.
    /// </summary>
    public void Complete()
    {
        this.channel.Writer.TryComplete();
    }

    /// <summary>
    /// Reads all queued items until cancellation or completion.
    /// </summary>
    public IAsyncEnumerable<InProcessQueueTrackedItem> ReadAllAsync(CancellationToken cancellationToken)
    {
        return this.channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a runtime summary of tracked items.
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
                Expired = this.trackedItems.Values.Count(item => item.Status == QueueMessageStatus.Expired),
                PausedQueues = controlState.GetPausedQueues(),
                PausedTypes = controlState.GetPausedTypes(),
                Capabilities = new QueueBrokerCapabilities
                {
                    SupportsDurableStorage = false,
                    SupportsRetry = true,
                    SupportsArchive = true,
                    SupportsLeaseManagement = false,
                    SupportsCircuitManagement = false,
                    SupportsPauseResume = true,
                    SupportsWaitingMessageInspection = true
                }
            };
        }
    }

    /// <summary>
    /// Gets tracked messages using the supplied filters.
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
            IEnumerable<InProcessQueueTrackedItem> query = this.trackedItems.Values
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
                query = query.Where(item => item.Message.MessageId.Contains(messageId, StringComparison.OrdinalIgnoreCase));
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

            return query.Select(MapInfo).ToArray();
        }
    }

    /// <summary>
    /// Gets a single tracked message by primary key.
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
    /// Gets the stored content for a single tracked message.
    /// </summary>
    public QueueMessageContentInfo GetMessageContent(Guid id, ISerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        lock (this.syncRoot)
        {
            return this.trackedItems.TryGetValue(id, out var item)
                ? new QueueMessageContentInfo
                {
                    Id = item.Id,
                    MessageId = item.Message.MessageId,
                    QueueName = item.QueueName,
                    Type = item.Type,
                    Content = serializer.SerializeToString(item.Message),
                    ContentHash = item.ContentHash ?? HashHelper.Compute(item.Message),
                    CreatedDate = item.CreatedDate,
                    IsArchived = item.IsArchived,
                    ArchivedDate = item.ArchivedDate
                }
                : null;
        }
    }

    /// <summary>
    /// Gets aggregate runtime statistics for tracked messages.
    /// </summary>
    public QueueMessageStats GetMessageStats(QueueBrokerControlState controlState, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, bool? isArchived = false)
    {
        ArgumentNullException.ThrowIfNull(controlState);

        lock (this.syncRoot)
        {
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
                Archived = materialized.Count(item => item.IsArchived),
                Leased = materialized.Count(item => !string.IsNullOrWhiteSpace(item.LockedBy) && item.LockedUntil > DateTimeOffset.UtcNow),
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
            IEnumerable<InProcessQueueTrackedItem> query = this.trackedItems.Values
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
    /// Marks an item as paused and pending.
    /// </summary>
    public void MarkPaused(InProcessQueueTrackedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (this.syncRoot)
        {
            item.Status = QueueMessageStatus.Pending;
            item.LockedBy = null;
            item.LockedUntil = null;
            item.ProcessingStartedDate = null;
            this.pausedItemIds.Add(item.Id);
        }
    }

    /// <summary>
    /// Marks an item as being processed.
    /// </summary>
    public void MarkProcessing(InProcessQueueTrackedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (this.syncRoot)
        {
            item.Status = QueueMessageStatus.Processing;
            item.LastError = null;
            item.AttemptCount++;
            item.LockedBy = Environment.MachineName;
            item.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(1);
            item.ProcessingStartedDate = DateTimeOffset.UtcNow;
            this.pausedItemIds.Remove(item.Id);
            this.waitingItemIds.Remove(item.Id);
        }
    }

    /// <summary>
    /// Marks an item as expired.
    /// </summary>
    public void MarkExpired(InProcessQueueTrackedItem item, string error)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (this.syncRoot)
        {
            item.Status = QueueMessageStatus.Expired;
            item.LastError = error;
            item.ProcessedDate = DateTimeOffset.UtcNow;
            item.LockedBy = null;
            item.LockedUntil = null;
            item.ProcessingStartedDate = null;
            this.waitingItemIds.Remove(item.Id);
            this.pausedItemIds.Remove(item.Id);
        }
    }

    /// <summary>
    /// Marks an item as successfully processed.
    /// </summary>
    public void MarkSucceeded(InProcessQueueTrackedItem item, string registeredHandlerType)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (this.syncRoot)
        {
            item.Status = QueueMessageStatus.Succeeded;
            item.ProcessedDate = DateTimeOffset.UtcNow;
            item.LastError = null;
            item.RegisteredHandlerType = registeredHandlerType;
            item.LockedBy = null;
            item.LockedUntil = null;
            item.ProcessingStartedDate = null;
        }
    }

    /// <summary>
    /// Marks an item as waiting for a handler.
    /// </summary>
    public void MarkWaitingForHandler(InProcessQueueTrackedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (this.syncRoot)
        {
            item.Status = QueueMessageStatus.WaitingForHandler;
            item.ProcessedDate = null;
            item.LastError = null;
            item.LockedBy = null;
            item.LockedUntil = null;
            item.ProcessingStartedDate = null;
            this.waitingItemIds.Add(item.Id);
        }
    }

    /// <summary>
    /// Marks an item as failed.
    /// </summary>
    public void MarkFailed(InProcessQueueTrackedItem item, string error, string registeredHandlerType)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (this.syncRoot)
        {
            item.Status = QueueMessageStatus.Failed;
            item.ProcessedDate = DateTimeOffset.UtcNow;
            item.LastError = error;
            item.RegisteredHandlerType = registeredHandlerType;
            item.LockedBy = null;
            item.LockedUntil = null;
            item.ProcessingStartedDate = null;
        }
    }

    /// <summary>
    /// Retries a tracked message by requeueing it.
    /// </summary>
    public async Task<bool> RetryMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        InProcessQueueTrackedItem item;

        lock (this.syncRoot)
        {
            if (!this.trackedItems.TryGetValue(id, out item))
            {
                return false;
            }

            item.Status = QueueMessageStatus.Pending;
            item.ProcessedDate = null;
            item.ProcessingStartedDate = null;
            item.LastError = null;
            item.IsArchived = false;
            item.ArchivedDate = null;
            item.LockedBy = null;
            item.LockedUntil = null;
            this.waitingItemIds.Remove(item.Id);
            this.pausedItemIds.Remove(item.Id);
        }

        await this.channel.Writer.WriteAsync(item, cancellationToken);
        return true;
    }

    /// <summary>
    /// Releases the lease of a tracked message if one exists.
    /// </summary>
    public bool ReleaseLease(Guid id)
    {
        lock (this.syncRoot)
        {
            if (!this.trackedItems.TryGetValue(id, out var item))
            {
                return false;
            }

            item.LockedBy = null;
            item.LockedUntil = null;
            if (item.Status == QueueMessageStatus.Processing)
            {
                item.Status = QueueMessageStatus.Pending;
                item.ProcessingStartedDate = null;
            }

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
                this.waitingItemIds.Remove(id);
                this.pausedItemIds.Remove(id);
            }

            return ids.Count;
        }
    }

    /// <summary>
    /// Requeues items that were waiting for a handler and now match the supplied type.
    /// </summary>
    public async Task RequeueWaitingItemsAsync(string type, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        List<InProcessQueueTrackedItem> items;
        lock (this.syncRoot)
        {
            items = this.trackedItems.Values
                .Where(item => item.Type.Equals(type, StringComparison.OrdinalIgnoreCase) && this.waitingItemIds.Contains(item.Id))
                .ToList();

            foreach (var item in items)
            {
                this.waitingItemIds.Remove(item.Id);
                item.Status = QueueMessageStatus.Pending;
            }
        }

        foreach (var item in items)
        {
            await this.channel.Writer.WriteAsync(item, cancellationToken);
        }
    }

    /// <summary>
    /// Resumes all paused items for the specified queue.
    /// </summary>
    public async Task ResumeQueueAsync(string queueName, QueueBrokerControlState controlState, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(controlState);

        List<InProcessQueueTrackedItem> items;
        lock (this.syncRoot)
        {
            controlState.ResumeQueue(queueName);
            items = this.trackedItems.Values
                .Where(item => item.QueueName.Equals(queueName, StringComparison.OrdinalIgnoreCase) && this.pausedItemIds.Contains(item.Id))
                .ToList();

            foreach (var item in items)
            {
                this.pausedItemIds.Remove(item.Id);
                item.Status = QueueMessageStatus.Pending;
            }
        }

        foreach (var item in items)
        {
            await this.channel.Writer.WriteAsync(item, cancellationToken);
        }
    }

    /// <summary>
    /// Resumes all paused items for the specified queue message type.
    /// </summary>
    public async Task ResumeMessageTypeAsync(string type, QueueBrokerControlState controlState, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(controlState);

        List<InProcessQueueTrackedItem> items;
        lock (this.syncRoot)
        {
            controlState.ResumeMessageType(type);
            items = this.trackedItems.Values
                .Where(item => item.Type.Equals(type, StringComparison.OrdinalIgnoreCase) && this.pausedItemIds.Contains(item.Id))
                .ToList();

            foreach (var item in items)
            {
                this.pausedItemIds.Remove(item.Id);
                item.Status = QueueMessageStatus.Pending;
            }
        }

        foreach (var item in items)
        {
            await this.channel.Writer.WriteAsync(item, cancellationToken);
        }
    }

    private static QueueMessageInfo MapInfo(InProcessQueueTrackedItem item)
    {
        return new QueueMessageInfo
        {
            Id = item.Id,
            MessageId = item.Message.MessageId,
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
            Properties = item.Message.Properties?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? []
        };
    }
}

/// <summary>
/// Represents a tracked item inside the in-process broker runtime.
/// </summary>
public sealed class InProcessQueueTrackedItem
{
    public Guid Id { get; init; }

    public IQueueMessage Message { get; init; }

    public string QueueName { get; init; }

    public string Type { get; init; }

    public string RegisteredHandlerType { get; set; }

    public bool IsArchived { get; set; }

    public DateTimeOffset? ArchivedDate { get; set; }

    public QueueMessageStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedDate { get; init; }

    public DateTimeOffset? ExpiresOn { get; init; }

    public string LockedBy { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset? ProcessingStartedDate { get; set; }

    public DateTimeOffset? ProcessedDate { get; set; }

    public string LastError { get; set; }

    public string ContentHash { get; init; }
}