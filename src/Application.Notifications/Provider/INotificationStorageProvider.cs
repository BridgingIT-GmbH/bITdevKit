namespace BridgingIT.DevKit.Application.Notifications;

using System.Collections.Generic;
using BridgingIT.DevKit.Common;

public interface INotificationStorageProvider
{
    Task<Result> SaveAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage;

    Task<Result> UpdateAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage;

    Task<Result> DeleteAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage;

    Task<Result<IEnumerable<TMessage>>> GetPendingAsync<TMessage>(int batchSize/*, int maxRetries*/, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage;
}