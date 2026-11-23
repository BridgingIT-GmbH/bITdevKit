// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;

public sealed partial class LiteDbQueueBroker
{
    /// <inheritdoc />
    public Task<ResultPaged<QueueMessageInfo>> SearchMessagesAsync(FilterModel filter, bool? isArchived = false, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
    {
        var query = this.messages.Values.Where(m => !isArchived.HasValue || m.IsArchived == isArchived).Select(m => MapInfo(m)).AsQueryable().ApplyFilter(filter);
        var total = query.LongCount();
        var rows = query.ApplyOrder(filter, m => m.Id).ApplyPage(filter).ToArray();
        return Task.FromResult(ResultPaged<QueueMessageInfo>.Success(rows, total, Math.Max(1, filter?.Page ?? 1), Math.Clamp(filter?.PageSize > 0 ? filter.PageSize : 50, 1, 1000)));
    }, cancellationToken);

    /// <inheritdoc />
    public Task<IEnumerable<QueueMessageInfo>> GetMessagesAsync(QueueMessageStatus? status = null, string type = null, string queueName = null, string messageId = null, string lockedBy = null,
        bool? isArchived = false, DateTimeOffset? createdAfter = null, DateTimeOffset? createdBefore = null, int? take = null, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
    {
        var rows = this.messages.Values.Where(m => (!status.HasValue || m.Status == status) && (type is null || m.Type == type) && (queueName is null || m.QueueName == queueName) && (messageId is null || m.MessageId == messageId)
            && (lockedBy is null || m.LockedBy == lockedBy) && (!isArchived.HasValue || m.IsArchived == isArchived) && (!createdAfter.HasValue || m.CreatedDate >= createdAfter)
            && (!createdBefore.HasValue || m.CreatedDate <= createdBefore)).OrderByDescending(m => m.CreatedDate).ThenBy(m => m.Id).Take(take ?? this.options.MaxMaterializedRows);
        return Task.FromResult<IEnumerable<QueueMessageInfo>>(rows.Select(m => MapInfo(m)).ToArray());
    }, cancellationToken);

    /// <inheritdoc />
    public Task<QueueMessageInfo> GetMessageAsync(Guid id, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
        Task.FromResult(this.messages.TryGetValue(id, out var row) ? MapInfo(row) : null), cancellationToken);

    /// <inheritdoc />
    public Task<QueueMessageContentInfo> GetMessageContentAsync(Guid id, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
        Task.FromResult(this.messages.TryGetValue(id, out var row) ? new QueueMessageContentInfo
        {
            Id = row.Id, MessageId = row.MessageId, QueueName = row.QueueName, Type = row.Type, Content = row.Content, ContentHash = row.ContentHash, CreatedDate = row.CreatedDate, IsArchived = row.IsArchived
        } : null), cancellationToken);

    /// <inheritdoc />
    public Task<QueueMessageStats> GetMessageStatsAsync(DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, bool? isArchived = false, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
    {
        var rows = this.messages.Values.Where(m => (!isArchived.HasValue || m.IsArchived == isArchived) && (!startDate.HasValue || m.CreatedDate >= startDate) && (!endDate.HasValue || m.CreatedDate <= endDate)).ToArray();
        return Task.FromResult(new QueueMessageStats
        {
            Total = rows.Length, WaitingForHandler = rows.Count(m => m.Status == QueueMessageStatus.WaitingForHandler), Pending = rows.Count(m => m.Status == QueueMessageStatus.Pending), Processing = rows.Count(m => m.Status == QueueMessageStatus.Processing),
            Succeeded = rows.Count(m => m.Status == QueueMessageStatus.Succeeded), Failed = rows.Count(m => m.Status == QueueMessageStatus.Failed), DeadLettered = rows.Count(m => m.Status == QueueMessageStatus.DeadLettered),
            Expired = rows.Count(m => m.Status == QueueMessageStatus.Expired), Archived = rows.Count(m => m.IsArchived), Leased = rows.Count(m => m.LockedUntil > this.clock.GetUtcNow())
        });
    }, cancellationToken);

    /// <inheritdoc />
    public Task RetryMessageAsync(Guid id, CancellationToken cancellationToken = default) => this.MutateAsync(id, row =>
    {
        row.AttemptCount = 0;
        Reset(row);
    }, cancellationToken);

    /// <inheritdoc />
    public Task ReleaseLeaseAsync(Guid id, CancellationToken cancellationToken = default) => this.MutateAsync(id, row =>
    {
        row.LeaseToken = null; row.LockedBy = null; row.LockedUntil = null;
        if (row.Status == QueueMessageStatus.Processing) { row.Status = QueueMessageStatus.Pending; }
    }, cancellationToken);

    /// <inheritdoc />
    public Task ArchiveMessageAsync(Guid id, CancellationToken cancellationToken = default) => this.MutateAsync(id, row =>
    {
        row.IsArchived = true; row.ArchivedDate = this.clock.GetUtcNow(); row.LeaseToken = null; row.LockedBy = null; row.LockedUntil = null;
    }, cancellationToken);

    /// <inheritdoc />
    public async Task PurgeMessagesAsync(DateTimeOffset? olderThan = null, IEnumerable<QueueMessageStatus> statuses = null, bool? isArchived = null, CancellationToken cancellationToken = default)
    {
        var selected = statuses?.ToArray() ?? [];
        var batchSize = Math.Min(128, this.options.MaxMaterializedRows);
        while (true)
        {
            var count = await this.database.ExecuteAsync(() =>
            {
                var expression = "$.LeaseUntilTicks <= @0";
                var parameters = new List<BsonValue> { this.clock.GetUtcNow().UtcTicks };
                if (olderThan.HasValue) { parameters.Add(olderThan.Value.UtcTicks); expression += $" AND $.CreatedTicks < @{parameters.Count - 1}"; }

                if (isArchived.HasValue) { parameters.Add(isArchived.Value); expression += $" AND $.Archived = @{parameters.Count - 1}"; }

                if (selected.Length != 0)
                {
                    var clauses = new List<string>();
                    foreach (var status in selected) { parameters.Add((int)status); clauses.Add($"$.Status = @{parameters.Count - 1}"); }

                    expression += " AND (" + string.Join(" OR ", clauses) + ")";
                }

                var rows = this.database.Query<LiteDbQueueMessage>("messages", BsonExpression.Create(expression, parameters.ToArray()), batchSize);
                foreach (var row in rows) { this.messages.Remove(row.Id); this.identities.Remove(row.MessageId); }

                return Task.FromResult(rows.Count);
            }, cancellationToken).ConfigureAwait(false);
            if (count < batchSize) { return; }
        }
    }

    /// <inheritdoc />
    public Task PauseMessageTypeAsync(string type, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() => { this.paused[CanonicalType(type)] = true; return Task.CompletedTask; }, cancellationToken);

    /// <inheritdoc />
    public Task ResumeMessageTypeAsync(string type, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() => { this.paused.Remove(CanonicalType(type)); return Task.CompletedTask; }, cancellationToken);

    /// <inheritdoc />
    public async Task<QueueBrokerSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var stats = await this.GetMessageStatsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var pausedTypes = await this.database.ExecuteAsync(() => Task.FromResult(this.paused.Keys.ToArray()), cancellationToken).ConfigureAwait(false);
        return new() { Total = stats.Total, WaitingForHandler = stats.WaitingForHandler, Pending = stats.Pending, Processing = stats.Processing, Succeeded = stats.Succeeded, Failed = stats.Failed, DeadLettered = stats.DeadLettered, Expired = stats.Expired, PausedTypes = pausedTypes.Where(t => !t.StartsWith("queue:", StringComparison.Ordinal)).ToArray(),
            PausedQueues = pausedTypes.Where(t => t.StartsWith("queue:", StringComparison.Ordinal)).Select(t => t[6..]).ToArray(),
            Capabilities = new() { SupportsDurableStorage = true, SupportsRetry = true, SupportsArchive = true, SupportsLeaseManagement = true, SupportsPauseResume = true, SupportsWaitingMessageInspection = true } };
    }

    /// <inheritdoc />
    public Task<IEnumerable<QueueSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
        Task.FromResult<IEnumerable<QueueSubscriptionInfo>>(this.GetSubscriptions().Values.Select(subscription => new QueueSubscriptionInfo
        {
            QueueName = this.GetQueueName(subscription.MessageType), MessageType = subscription.MessageType.PrettyName(false), HandlerType = subscription.HandlerType.FullName,
            IsQueuePaused = this.paused.ContainsKey("queue:" + this.GetQueueName(subscription.MessageType)), IsMessageTypePaused = this.paused.ContainsKey(subscription.MessageType.AssemblyQualifiedNameShort())
        }).ToArray()), cancellationToken);

    /// <inheritdoc />
    public Task<IEnumerable<QueueMessageInfo>> GetWaitingMessagesAsync(int? take = null, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
        Task.FromResult<IEnumerable<QueueMessageInfo>>(this.database.Query<LiteDbQueueMessage>("messages", BsonExpression.Create("$.Waiting = true AND $.Archived = false"), Math.Min(take ?? this.options.BatchSize, this.options.MaxMaterializedRows)).Select(m => MapInfo(m)).ToArray()), cancellationToken);

    private Task MutateAsync(Guid id, Action<LiteDbQueueMessage> mutation, CancellationToken cancellationToken) => this.database.ExecuteAsync(() =>
    {
        if (!this.messages.TryGetValue(id, out var row)) { throw new KeyNotFoundException("The stored broker message was not found."); }

        mutation(row);
        return Task.CompletedTask;
    }, cancellationToken);

    private string CanonicalType(string type) => LiteDbMessageTypeResolver.Resolve(type, this.subscribedTypes.Values);

    private static void Reset(LiteDbQueueMessage row)
    {
        row.ProcessingFailureCount = 0;
        row.Status = QueueMessageStatus.Pending; row.ProcessedDate = null; row.IsArchived = false; row.ArchivedDate = null; row.LeaseToken = null;
        row.LockedBy = null; row.LockedUntil = null; row.NextAttemptUtc = null; row.LastError = null;
    }

    private static QueueMessageInfo MapInfo(LiteDbQueueMessage row) => new()
    {
        Id = row.Id, MessageId = row.MessageId, QueueName = row.QueueName, Type = row.Type, RegisteredHandlerType = row.RegisteredHandlerType,
        Status = row.Status, AttemptCount = row.AttemptCount, IsArchived = row.IsArchived, ArchivedDate = row.ArchivedDate,
        CreatedDate = row.CreatedDate, ExpiresOn = row.ExpiresOn, LockedBy = row.LockedBy, LockedUntil = row.LockedUntil,
        ProcessingStartedDate = row.ProcessingStartedDate, ProcessedDate = row.ProcessedDate, LastError = row.LastError, Properties = row.Properties
    };

    /// <inheritdoc />
    public Task PauseQueueAsync(string queueName, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() => { this.paused["queue:" + queueName] = true; return Task.CompletedTask; }, cancellationToken);

    /// <inheritdoc />
    public Task ResumeQueueAsync(string queueName, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() => { this.paused.Remove("queue:" + queueName); return Task.CompletedTask; }, cancellationToken);

    /// <inheritdoc />
    public Task ResetMessageTypeCircuitAsync(string type, CancellationToken cancellationToken = default) => throw new NotSupportedException("The LiteDB queue transport does not own a circuit breaker. Configure circuit behavior in the handler pipeline.");
}
