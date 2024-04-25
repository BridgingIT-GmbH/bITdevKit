// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using EnsureThat;
using Microsoft.Extensions.Logging;

public abstract partial class DomainEventHandlerBase<TEvent> : IDomainEventHandler<TEvent>
    where TEvent : class, IDomainEvent
{
    protected DomainEventHandlerBase(ILoggerFactory loggerFactory)
    {
        EnsureArg.IsNotNull(loggerFactory, nameof(loggerFactory));

        this.Logger = loggerFactory.CreateLogger(this.GetType());
    }

    protected ILogger Logger { get; }

    public abstract bool CanHandle(TEvent notification);

    public virtual async Task Handle(
        TEvent @event,
        CancellationToken cancellationToken)
    {
        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            ["DomainEventId"] = @event.EventId,
            ["DomainEventType"] = @event.GetType().Name
        }))
        {
            try
            {
                EnsureArg.IsNotNull(@event, nameof(@event));

                if (!this.CanHandle(@event))
                {
                    // TODO: log when not handled?
                    return;
                }

                TypedLogger.LogProcessing(this.Logger, Constants.LogKey, @event.GetType().Name, @event.EventId);
                var watch = ValueStopwatch.StartNew();

                await this.Process(@event, cancellationToken).AnyContext();

                TypedLogger.LogProcessed(this.Logger, Constants.LogKey, @event.GetType().Name, @event.EventId, watch.GetElapsedMilliseconds());
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "{LogKey} event processing error (type={DomainEventType}, id={DomainEventId}): {ErrorMessage}", Constants.LogKey, @event.GetType().Name, @event.EventId, ex.Message);
                throw;
            }
        }
    }

    public abstract Task Process(
        TEvent notification,
        CancellationToken cancellationToken);

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} event processing (type={DomainEventType}, id={DomainEventId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string domainEventType, Guid domainEventId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} event processed (type={DomainEventType}, id={DomainEventId}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string domainEventType, Guid domainEventId, long timeElapsed);
    }
}