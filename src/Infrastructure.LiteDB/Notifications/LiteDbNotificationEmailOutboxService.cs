// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Common;

public sealed partial class LiteDbNotificationStorageProvider
{
    /// <inheritdoc />
    public Task<ResultPaged<NotificationEmailInfo>> SearchMessagesAsync(FilterModel filter, bool? isArchived = false, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        var query = this.messages.Values.Where(row => !isArchived.HasValue || row.IsArchived == isArchived).Select(MapInfo).AsQueryable().ApplyFilter(filter);
        return Task.FromResult(ResultPaged<NotificationEmailInfo>.Success(query.ApplyOrder(filter, row => row.Id).ApplyPage(filter).ToArray(), query.LongCount(), Math.Max(filter?.Page ?? 1, 1), Math.Clamp(filter?.PageSize > 0 ? filter.PageSize : 50, 1, 1000)));
    }, cancellationToken);

    /// <inheritdoc />
    public Task<IEnumerable<NotificationEmailInfo>> GetMessagesAsync(EmailMessageStatus? status = null, string subject = null, string lockedBy = null, bool? isArchived = false,
        DateTimeOffset? createdAfter = null, DateTimeOffset? createdBefore = null, int? take = null, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        var query = this.messages.Values.Where(row => (!status.HasValue || row.Message.Status == status) && (subject is null || row.Message.Subject?.Contains(subject, StringComparison.OrdinalIgnoreCase) == true)
            && (lockedBy is null || row.LeaseOwner == lockedBy) && (!isArchived.HasValue || row.IsArchived == isArchived) && (!createdAfter.HasValue || row.Message.CreatedAt >= createdAfter) && (!createdBefore.HasValue || row.Message.CreatedAt <= createdBefore));
        return Task.FromResult<IEnumerable<NotificationEmailInfo>>(query.OrderByDescending(row => row.Message.CreatedAt).ThenBy(row => row.Message.Id).Take(take ?? database.Options.MaxMaterializedRows).Select(MapInfo).ToArray());
    }, cancellationToken);

    /// <inheritdoc />
    public Task<NotificationEmailInfo> GetMessageAsync(Guid id, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
        Task.FromResult(this.messages.TryGetValue(id, out var row) ? MapInfo(row) : null), cancellationToken);

    /// <inheritdoc />
    public Task<NotificationEmailContentInfo> GetMessageContentAsync(Guid id, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
        Task.FromResult(this.messages.TryGetValue(id, out var row) ? new NotificationEmailContentInfo { Id = id, Subject = row.Message.Subject, Body = row.Message.Body, IsHtml = row.Message.IsHtml } : null), cancellationToken);

    /// <inheritdoc />
    public Task<NotificationEmailStats> GetMessageStatsAsync(DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, bool? isArchived = false, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        var rows = this.messages.Values.Where(row => (!isArchived.HasValue || row.IsArchived == isArchived) && (!startDate.HasValue || row.Message.CreatedAt >= startDate) && (!endDate.HasValue || row.Message.CreatedAt <= endDate)).ToArray();
        return Task.FromResult(new NotificationEmailStats { Total = rows.Length, Pending = rows.Count(row => row.Message.Status == EmailMessageStatus.Pending), Locked = rows.Count(row => row.Message.Status == EmailMessageStatus.Locked),
            Sent = rows.Count(row => row.Message.Status == EmailMessageStatus.Sent), Failed = rows.Count(row => row.Message.Status == EmailMessageStatus.Failed), Archived = rows.Count(row => row.IsArchived), Leased = rows.Count(row => row.LockedUntil > clock.GetUtcNow()) });
    }, cancellationToken);

    /// <inheritdoc />
    public Task RetryMessageAsync(Guid id, CancellationToken cancellationToken = default) => this.MutateAsync(id, row =>
    {
        row.Message.Status = EmailMessageStatus.Pending; row.Message.RetryCount = 0; row.Message.SentAt = null; row.Message.Properties.Remove(LeaseProperty);
        row.IsArchived = false; row.ArchivedDate = null; row.LeaseOwner = null; row.LockedUntil = null;
    }, cancellationToken);

    /// <inheritdoc />
    public Task ArchiveMessageAsync(Guid id, CancellationToken cancellationToken = default) => this.MutateAsync(id, row =>
    {
        row.IsArchived = true; row.ArchivedDate = clock.GetUtcNow(); row.LeaseOwner = null; row.LockedUntil = null;
    }, cancellationToken);

    /// <inheritdoc />
    public Task<int> AutoArchiveMessagesAsync(DateTimeOffset olderThan, IEnumerable<EmailMessageStatus> statuses = null, CancellationToken cancellationToken = default) =>
        this.MaintainAsync(true, olderThan, statuses, false, cancellationToken);

    /// <inheritdoc />
    public async Task PurgeMessagesAsync(DateTimeOffset? olderThan = null, IEnumerable<EmailMessageStatus> statuses = null, bool? isArchived = null, CancellationToken cancellationToken = default) =>
        _ = await this.MaintainAsync(false, olderThan, statuses, isArchived, cancellationToken).ConfigureAwait(false);

    private async Task<int> MaintainAsync(bool archive, DateTimeOffset? olderThan, IEnumerable<EmailMessageStatus> statuses, bool? isArchived, CancellationToken cancellationToken)
    {
        var selectedStatuses = statuses?.ToArray() ?? (archive ? [EmailMessageStatus.Sent, EmailMessageStatus.Failed] : []);
        var total = 0;
        var batchSize = Math.Min(128, database.Options.MaxMaterializedRows);
        while (true)
        {
            var count = await database.ExecuteAsync(() =>
            {
                var expression = "$.LeaseUntilTicks <= @0";
                var parameters = new List<BsonValue> { clock.GetUtcNow().UtcTicks };
                if (olderThan.HasValue) { parameters.Add(olderThan.Value.UtcTicks); expression += $" AND $.CreatedTicks < @{parameters.Count - 1}"; }

                if (isArchived.HasValue) { parameters.Add(isArchived.Value); expression += $" AND $.Archived = @{parameters.Count - 1}"; }

                if (selectedStatuses.Length != 0)
                {
                    var comparisons = new List<string>();
                    foreach (var status in selectedStatuses) { parameters.Add((int)status); comparisons.Add($"$.Status = @{parameters.Count - 1}"); }

                    expression += " AND (" + string.Join(" OR ", comparisons) + ")";
                }

                var rows = database.Query<LiteDbNotificationRecord>("emails", BsonExpression.Create(expression, parameters.ToArray()), batchSize);
                foreach (var row in rows)
                {
                    if (archive) { row.IsArchived = true; row.ArchivedDate = clock.GetUtcNow(); }
                    else { this.messages.Remove(row.Message.Id); }
                }

                return Task.FromResult(rows.Count);
            }, cancellationToken).ConfigureAwait(false);
            total = checked(total + count);
            if (count < batchSize) { return total; }
        }
    }

    private Task MutateAsync(Guid id, Action<LiteDbNotificationRecord> action, CancellationToken cancellationToken) => database.ExecuteAsync(() =>
    {
        if (!this.messages.TryGetValue(id, out var row)) { throw new KeyNotFoundException("The email notification was not found."); }

        action(row);
        return Task.CompletedTask;
    }, cancellationToken);

    private static NotificationEmailInfo MapInfo(LiteDbNotificationRecord row)
    {
        var message = row.Message;
        return new NotificationEmailInfo
        {
            Id = message.Id, To = message.To, Cc = message.CC, Bcc = message.BCC, FromName = message.From?.Name, FromAddress = message.From?.Address,
            ReplyToName = message.ReplyTo?.Name, ReplyToAddress = message.ReplyTo?.Address, Subject = message.Subject, IsHtml = message.IsHtml, Priority = message.Priority,
            Status = message.Status, RetryCount = message.RetryCount, CreatedAt = message.CreatedAt, SentAt = message.SentAt, IsArchived = row.IsArchived, ArchivedDate = row.ArchivedDate,
            LockedBy = row.LeaseOwner, LockedUntil = row.LockedUntil, ProcessMessage = message.Properties.GetValueOrDefault("ProcessMessage")?.ToString(),
            AttachmentCount = message.Attachments.Count, Properties = message.Properties.Where(p => p.Key != LeaseProperty).ToDictionary(p => p.Key, p => p.Value),
            Attachments = message.Attachments.Select(a => new NotificationEmailAttachmentInfo { Id = a.Id, EmailMessageId = message.Id, FileName = a.FileName, ContentType = a.ContentType,
                ContentLength = a.Content?.Length ?? 0, ContentId = a.ContentId, IsEmbedded = a.IsEmbedded }).ToArray()
        };
    }
}
