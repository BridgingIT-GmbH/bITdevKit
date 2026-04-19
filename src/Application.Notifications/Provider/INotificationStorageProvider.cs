namespace BridgingIT.DevKit.Application.Notifications;

using System.Collections.Generic;
using BridgingIT.DevKit.Common;

/// <summary>
/// Persists notification messages for immediate delivery and outbox processing.
/// </summary>
public interface INotificationStorageProvider
{
    /// <summary>
    /// Saves a new notification message.
    /// </summary>
    Task<Result> SaveAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage;

    /// <summary>
    /// Updates an existing notification message.
    /// </summary>
    Task<Result> UpdateAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage;

    /// <summary>
    /// Deletes a persisted notification message.
    /// </summary>
    Task<Result> DeleteAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage;

    /// <summary>
    /// Retrieves a batch of pending notification messages, optionally claiming them for processing.
    /// </summary>
    Task<Result<IEnumerable<TMessage>>> GetPendingAsync<TMessage>(int batchSize/*, int maxRetries*/, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage;
}
