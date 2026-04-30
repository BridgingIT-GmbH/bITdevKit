// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Provides operational access to persisted broker messages.
/// </summary>
/// <example>
/// <code>
/// var messages = await messageBrokerService.GetMessagesAsync(
///     status: BrokerMessageStatus.Pending,
///     isArchived: false,
///     take: 100,
///     cancellationToken: cancellationToken);
/// </code>
/// </example>
public interface IMessageBrokerService
{
    /// <summary>
    /// Retrieves broker messages with optional filters.
    /// </summary>
    /// <param name="status">Optional aggregate status filter.</param>
    /// <param name="type">Optional message type filter.</param>
    /// <param name="messageId">Optional logical message identifier filter.</param>
    /// <param name="lockedBy">Optional lease owner filter.</param>
    /// <param name="isArchived">Optional archive-state filter. Defaults to <c>false</c>.</param>
    /// <param name="createdAfter">Optional lower creation-date filter.</param>
    /// <param name="createdBefore">Optional upper creation-date filter.</param>
    /// <param name="includeHandlers">Whether handler details should be included.</param>
    /// <param name="take">Optional limit for the number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of broker message summaries.</returns>
    Task<IEnumerable<BrokerMessageInfo>> GetMessagesAsync(
        BrokerMessageStatus? status = null,
        string type = null,
        string messageId = null,
        string lockedBy = null,
        bool? isArchived = false,
        DateTimeOffset? createdAfter = null,
        DateTimeOffset? createdBefore = null,
        bool includeHandlers = false,
        int? take = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single broker message by its primary key.
    /// </summary>
    /// <param name="id">The broker message primary key.</param>
    /// <param name="includeHandlers">Whether handler details should be included.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The broker message details, or <c>null</c> if not found.</returns>
    Task<BrokerMessageInfo> GetMessageAsync(
        Guid id,
        bool includeHandlers = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the stored serialized content for a single broker message.
    /// </summary>
    /// <param name="id">The broker message primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored message content details, or <c>null</c> if not found.</returns>
    Task<BrokerMessageContentInfo> GetMessageContentAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves aggregate broker message statistics.
    /// </summary>
    /// <param name="startDate">Optional lower processed/created date filter.</param>
    /// <param name="endDate">Optional upper processed/created date filter.</param>
    /// <param name="isArchived">Optional archive-state filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The aggregated broker message statistics.</returns>
    Task<BrokerMessageStats> GetMessageStatsAsync(
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        bool? isArchived = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a broker message so retryable handler work can be processed again.
    /// </summary>
    /// <param name="id">The broker message primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RetryMessageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a single handler entry for retry.
    /// </summary>
    /// <param name="id">The broker message primary key.</param>
    /// <param name="handlerType">The fully qualified handler type to reset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RetryMessageHandlerAsync(Guid id, string handlerType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the current lease for a broker message.
    /// </summary>
    /// <param name="id">The broker message primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ReleaseLeaseAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a terminal broker message as archived.
    /// </summary>
    /// <param name="id">The broker message primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ArchiveMessageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes persisted broker messages matching the supplied filters.
    /// </summary>
    /// <param name="olderThan">Optional upper age filter.</param>
    /// <param name="statuses">Optional status filter.</param>
    /// <param name="isArchived">Optional archive-state filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PurgeMessagesAsync(
        DateTimeOffset? olderThan = null,
        IEnumerable<BrokerMessageStatus> statuses = null,
        bool? isArchived = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses processing for the specified message type.
    /// </summary>
    /// <param name="type">The message type identifier (assembly-qualified short name as stored in BrokerMessage.Type).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PauseMessageTypeAsync(string type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes processing for the specified message type.
    /// </summary>
    /// <param name="type">The message type identifier (assembly-qualified short name as stored in BrokerMessage.Type).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ResumeMessageTypeAsync(string type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a summary of broker runtime state.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The broker summary including counts, paused types, and capabilities.</returns>
    Task<BrokerMessageBrokerSummary> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently active message subscriptions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of registered message subscription info.</returns>
    Task<IEnumerable<BrokerMessageSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages that are waiting because no compatible handler was registered at publish time.
    /// </summary>
    /// <param name="take">Optional limit for the number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of waiting broker messages.</returns>
    Task<IEnumerable<BrokerMessageInfo>> GetWaitingMessagesAsync(int? take = null, CancellationToken cancellationToken = default);

}