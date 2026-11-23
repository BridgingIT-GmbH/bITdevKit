// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;

public sealed partial class LiteDbMessageBroker
{
    /// <inheritdoc />
    public Task<ResultPaged<BrokerMessageInfo>> SearchMessagesAsync(FilterModel filter, bool? isArchived = false, bool includeHandlers = false, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
    {
        var query = this.messages.Values.Where(m => !isArchived.HasValue || m.IsArchived == isArchived).Select(m => MapInfo(m, includeHandlers)).AsQueryable().ApplyFilter(filter);
        var total = query.LongCount();
        var rows = query.ApplyOrder(filter, m => m.Id).ApplyPage(filter).ToArray();
        return Task.FromResult(ResultPaged<BrokerMessageInfo>.Success(rows, total, Math.Max(1, filter?.Page ?? 1), Math.Clamp(filter?.PageSize > 0 ? filter.PageSize : 50, 1, 1000)));
    }, cancellationToken);

    /// <inheritdoc />
    public Task<IEnumerable<BrokerMessageInfo>> GetMessagesAsync(BrokerMessageStatus? status = null, string type = null, string messageId = null, string lockedBy = null,
        bool? isArchived = false, DateTimeOffset? createdAfter = null, DateTimeOffset? createdBefore = null, bool includeHandlers = false, int? take = null, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
    {
        var rows = this.messages.Values.Where(m => (!status.HasValue || m.Status == status) && (type is null || m.Type == type) && (messageId is null || m.MessageId == messageId)
            && (lockedBy is null || m.LockedBy == lockedBy) && (!isArchived.HasValue || m.IsArchived == isArchived) && (!createdAfter.HasValue || m.CreatedDate >= createdAfter)
            && (!createdBefore.HasValue || m.CreatedDate <= createdBefore)).OrderByDescending(m => m.CreatedDate).ThenBy(m => m.Id).Take(take ?? this.options.MaxMaterializedRows);
        return Task.FromResult<IEnumerable<BrokerMessageInfo>>(rows.Select(m => MapInfo(m, includeHandlers)).ToArray());
    }, cancellationToken);

    /// <inheritdoc />
    public Task<BrokerMessageInfo> GetMessageAsync(Guid id, bool includeHandlers = true, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
        Task.FromResult(this.messages.TryGetValue(id, out var row) ? MapInfo(row, includeHandlers) : null), cancellationToken);

    /// <inheritdoc />
    public Task<BrokerMessageContentInfo> GetMessageContentAsync(Guid id, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
        Task.FromResult(this.messages.TryGetValue(id, out var row) ? new BrokerMessageContentInfo
        {
            Id = row.Id, MessageId = row.MessageId, Type = row.Type, Content = row.Content, ContentHash = row.ContentHash, CreatedDate = row.CreatedDate, IsArchived = row.IsArchived
        } : null), cancellationToken);

    /// <inheritdoc />
    public Task<BrokerMessageStats> GetMessageStatsAsync(DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, bool? isArchived = false, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
    {
        var rows = this.messages.Values.Where(m => (!isArchived.HasValue || m.IsArchived == isArchived) && (!startDate.HasValue || m.CreatedDate >= startDate) && (!endDate.HasValue || m.CreatedDate <= endDate)).ToArray();
        return Task.FromResult(new BrokerMessageStats
        {
            Total = rows.Length, Pending = rows.Count(m => m.Status == BrokerMessageStatus.Pending), Processing = rows.Count(m => m.Status == BrokerMessageStatus.Processing),
            Succeeded = rows.Count(m => m.Status == BrokerMessageStatus.Succeeded), Failed = rows.Count(m => m.Status == BrokerMessageStatus.Failed), DeadLettered = rows.Count(m => m.Status == BrokerMessageStatus.DeadLettered),
            Expired = rows.Count(m => m.Status == BrokerMessageStatus.Expired), Archived = rows.Count(m => m.IsArchived), Leased = rows.Count(m => m.LockedUntil > this.clock.GetUtcNow())
        });
    }, cancellationToken);

    /// <inheritdoc />
    public Task RetryMessageAsync(Guid id, CancellationToken cancellationToken = default) => this.MutateAsync(id, row =>
    {
        foreach (var handler in row.HandlerStates.Where(h => h.Status != BrokerMessageHandlerStatus.Succeeded)) { handler.Status = BrokerMessageHandlerStatus.Pending; handler.AttemptCount = 0; handler.ProcessedDate = null; handler.LastError = null; }

        Reset(row);
    }, cancellationToken);

    /// <inheritdoc />
    public Task RetryMessageHandlerAsync(Guid id, string handlerType, CancellationToken cancellationToken = default) => this.MutateAsync(id, row =>
    {
        var handler = row.HandlerStates.FirstOrDefault(h => h.HandlerType == handlerType) ?? throw new KeyNotFoundException("The message handler was not found.");
        handler.Status = BrokerMessageHandlerStatus.Pending; handler.AttemptCount = 0; handler.ProcessedDate = null; handler.LastError = null;
        Reset(row);
    }, cancellationToken);

    /// <inheritdoc />
    public Task ReleaseLeaseAsync(Guid id, CancellationToken cancellationToken = default) => this.MutateAsync(id, row =>
    {
        row.LeaseToken = null; row.LockedBy = null; row.LockedUntil = null;
        if (row.Status == BrokerMessageStatus.Processing) { row.Status = BrokerMessageStatus.Pending; }
    }, cancellationToken);

    /// <inheritdoc />
    public Task ArchiveMessageAsync(Guid id, CancellationToken cancellationToken = default) => this.MutateAsync(id, row =>
    {
        row.IsArchived = true; row.ArchivedDate = this.clock.GetUtcNow(); row.LeaseToken = null; row.LockedBy = null; row.LockedUntil = null;
    }, cancellationToken);

    /// <inheritdoc />
    public async Task PurgeMessagesAsync(DateTimeOffset? olderThan = null, IEnumerable<BrokerMessageStatus> statuses = null, bool? isArchived = null, CancellationToken cancellationToken = default)
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

                var rows = this.database.Query<LiteDbBrokerMessage>("messages", BsonExpression.Create(expression, parameters.ToArray()), batchSize);
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
    public async Task<BrokerMessageBrokerSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var stats = await this.GetMessageStatsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var pausedTypes = await this.database.ExecuteAsync(() => Task.FromResult(this.paused.Keys.ToArray()), cancellationToken).ConfigureAwait(false);
        return new() { Total = stats.Total, Pending = stats.Pending, Processing = stats.Processing, Succeeded = stats.Succeeded, Failed = stats.Failed, DeadLettered = stats.DeadLettered, Expired = stats.Expired, PausedTypes = pausedTypes,
            Capabilities = new() { SupportsDurableStorage = true, SupportsRetry = true, SupportsArchive = true, SupportsLeaseManagement = true, SupportsPauseResume = true, SupportsWaitingMessageInspection = true } };
    }

    /// <inheritdoc />
    public Task<IEnumerable<BrokerMessageSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
        Task.FromResult<IEnumerable<BrokerMessageSubscriptionInfo>>(this.subscribedTypes.Values.SelectMany(type => this.Subscriptions.GetAll(type.PrettyName(false)))
            .Select(subscription => new BrokerMessageSubscriptionInfo { MessageType = subscription.MessageType.PrettyName(false), HandlerType = subscription.HandlerType.FullName,
                IsMessageTypePaused = this.paused.ContainsKey(subscription.MessageType.AssemblyQualifiedNameShort()) }).ToArray()), cancellationToken);

    /// <inheritdoc />
    public Task<IEnumerable<BrokerMessageInfo>> GetWaitingMessagesAsync(int? take = null, CancellationToken cancellationToken = default) => this.database.ExecuteAsync(() =>
        Task.FromResult<IEnumerable<BrokerMessageInfo>>(this.database.Query<LiteDbBrokerMessage>("messages", BsonExpression.Create("$.Waiting = true AND $.Archived = false"), Math.Min(take ?? this.options.BatchSize, this.options.MaxMaterializedRows)).Select(m => MapInfo(m, false)).ToArray()), cancellationToken);

    private Task MutateAsync(Guid id, Action<LiteDbBrokerMessage> mutation, CancellationToken cancellationToken) => this.database.ExecuteAsync(() =>
    {
        if (!this.messages.TryGetValue(id, out var row)) { throw new KeyNotFoundException("The stored broker message was not found."); }

        mutation(row);
        return Task.CompletedTask;
    }, cancellationToken);

    private string CanonicalType(string type) => LiteDbMessageTypeResolver.Resolve(type, this.subscribedTypes.Values);

    private static void Reset(LiteDbBrokerMessage row)
    {
        row.ProcessingFailureCount = 0;
        row.Status = BrokerMessageStatus.Pending; row.ProcessedDate = null; row.IsArchived = false; row.ArchivedDate = null; row.LeaseToken = null;
        row.LockedBy = null; row.LockedUntil = null; row.NextAttemptUtc = null; row.LastError = null;
    }

    private static BrokerMessageInfo MapInfo(LiteDbBrokerMessage row, bool handlers) => new()
    {
        Id = row.Id, MessageId = row.MessageId, Type = row.Type, Status = row.Status, IsArchived = row.IsArchived, ArchivedDate = row.ArchivedDate,
        CreatedDate = row.CreatedDate, ExpiresOn = row.ExpiresOn, LockedBy = row.LockedBy, LockedUntil = row.LockedUntil, ProcessedDate = row.ProcessedDate,
        AttemptCountSummary = row.HandlerStates.Sum(h => h.AttemptCount), LastError = row.LastError, Properties = row.Properties,
        Handlers = handlers ? row.HandlerStates.Select(h => new BrokerMessageHandlerInfo { SubscriptionKey = h.SubscriptionKey, HandlerType = h.HandlerType, Status = h.Status, AttemptCount = h.AttemptCount, LastError = h.LastError, ProcessedDate = h.ProcessedDate }).ToArray() : []
    };
}
