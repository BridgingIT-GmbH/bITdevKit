// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Provides operational access to persisted notification email outbox entries.
/// </summary>
/// <example>
/// <code>
/// var messages = await outboxService.GetMessagesAsync(
///     status: EmailMessageStatus.Pending,
///     take: 50,
///     cancellationToken: cancellationToken);
/// </code>
/// </example>
public interface INotificationEmailOutboxService
{
    /// <summary>
    /// Retrieves notification emails with optional operational filters.
    /// </summary>
    /// <param name="status">The optional email status filter.</param>
    /// <param name="subject">The optional subject substring filter.</param>
    /// <param name="lockedBy">The optional lease owner filter.</param>
    /// <param name="isArchived">The optional archive-state filter. Defaults to <c>false</c>.</param>
    /// <param name="createdAfter">The optional lower creation timestamp filter.</param>
    /// <param name="createdBefore">The optional upper creation timestamp filter.</param>
    /// <param name="take">The optional maximum number of entries to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of matching notification emails.</returns>
    Task<IEnumerable<NotificationEmailInfo>> GetMessagesAsync(
        EmailMessageStatus? status = null,
        string subject = null,
        string lockedBy = null,
        bool? isArchived = false,
        DateTimeOffset? createdAfter = null,
        DateTimeOffset? createdBefore = null,
        int? take = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single notification email by its primary key.
    /// </summary>
    /// <param name="id">The notification email primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The notification email details, or <c>null</c> if the row was not found.</returns>
    Task<NotificationEmailInfo> GetMessageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the stored email body content for a single notification email.
    /// </summary>
    /// <param name="id">The notification email primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted body content information, or <c>null</c> if the row was not found.</returns>
    Task<NotificationEmailContentInfo> GetMessageContentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves aggregate statistics for persisted notification emails.
    /// </summary>
    /// <param name="startDate">The optional lower created/sent timestamp filter.</param>
    /// <param name="endDate">The optional upper created/sent timestamp filter.</param>
    /// <param name="isArchived">The optional archive-state filter. Defaults to <c>false</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The aggregated notification email statistics.</returns>
    Task<NotificationEmailStats> GetMessageStatsAsync(
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        bool? isArchived = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a failed notification email so it can be processed again by the outbox worker.
    /// </summary>
    /// <param name="id">The notification email primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RetryMessageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a persisted notification email as archived.
    /// </summary>
    /// <param name="id">The notification email primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ArchiveMessageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives terminal notification emails that have aged beyond the supplied threshold.
    /// </summary>
    /// <param name="olderThan">The age threshold expressed as the latest eligible sent or created timestamp.</param>
    /// <param name="statuses">The terminal statuses eligible for automatic archiving.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of notification emails archived by this operation.</returns>
    /// <example>
    /// <code>
    /// await outboxService.AutoArchiveMessagesAsync(
    ///     DateTimeOffset.UtcNow.AddHours(-1),
    ///     [EmailMessageStatus.Sent, EmailMessageStatus.Failed],
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<int> AutoArchiveMessagesAsync(
        DateTimeOffset olderThan,
        IEnumerable<EmailMessageStatus> statuses = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes persisted notification emails matching the supplied filters.
    /// </summary>
    /// <param name="olderThan">
    /// The optional upper age filter. Archived rows are evaluated by <c>ArchivedDate</c> when available;
    /// otherwise the comparison falls back to the sent or created timestamp.
    /// </param>
    /// <param name="statuses">The optional status filters.</param>
    /// <param name="isArchived">The optional archive-state filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PurgeMessagesAsync(
        DateTimeOffset? olderThan = null,
        IEnumerable<EmailMessageStatus> statuses = null,
        bool? isArchived = null,
        CancellationToken cancellationToken = default);
}
