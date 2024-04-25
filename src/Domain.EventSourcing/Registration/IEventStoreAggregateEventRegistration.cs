// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Registration;

using BridgingIT.DevKit.Domain.EventSourcing.Model;

/// <summary>
/// Registratur für EventStore-AggregateEvents.
/// Bei der Registratur wird eine Name übergeben der während des kompletten
/// Application Life Cylcles der Applikation nicht mehr verändert werden dürfen,
/// um so jederzeit ein Replay zu erlauben.
/// </summary>
public interface IEventStoreAggregateEventRegistration
{
    /// <summary>
    /// Ordnet einem AggregateEvent einen Namen zu, der während des kompletten
    /// Application Lifce Cycle nicht mehr verändert werden darf,
    /// so dass jederzeit ein Replay der Ereignisse möglich ist.
    /// Auch bei Umbenennungen der Klasse
    /// darf dieser Name nicht verändert werden.
    /// </summary>
    void Register<TAggregateEvent>(string immutableName)
        where TAggregateEvent : AggregateEvent;

    /// <summary>
    /// Gibt den immutable Name eines AggregateEvents zurück. Auch bei Umbenennungen der Klasse
    /// darf dieser Name nicht verändert werden.
    /// </summary>
    string GetImmutableName(IAggregateEvent aggregateEventType);

    /// <summary>
    /// Gibt den Type als String anhand des übergebenen 'immutableNames" zurück. Der immutable Name
    /// muss mit Register für den Type definiert worden sein.
    /// </summary>
    string GetTypeOnImmutableName(string immutableName);
}