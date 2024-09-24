// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;

/// <summary>
///     Was für ein Publishing soll der EventStore durchgeführen
/// </summary>
[Flags]
public enum EventStorePublishingModes
{
    /// <summary>
    ///     Kein Publishing
    /// </summary>
    None = 0,

    /// <summary>
    ///     Ereignis wird in die Outbox geschrieben
    /// </summary>
    AddToOutbox = 1,

    /// <summary>
    ///     Es wird ein Mediator-Request ausgelöst
    /// </summary>
    SendProjectionRequestUsingMediator = 2,

    /// <summary>
    ///     Es wird eine Mediator-Notification ausgelöst
    /// </summary>
    NotifyForProjectionUsingMediator = 4,

    /// <summary>
    ///     Es wird ein Mediator-Request ausgelöst
    /// </summary>
    SendEventOccuredRequestUsingMediator = 8,

    /// <summary>
    ///     Es wird ein Mediator-Notification ausgelöst
    /// </summary>
    NotifyEventOccuredUsingMediator = 16
}