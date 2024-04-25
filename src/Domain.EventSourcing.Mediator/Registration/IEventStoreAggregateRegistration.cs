// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Registration;

using BridgingIT.DevKit.Domain.EventSourcing.Model;

/// <summary>
/// Registratur für EventStore-Aggregates.
/// Bei der Registratur wird eine Name übergeben der während des kompletten
/// Application Life Cylcles der Applikation nicht mehr verändert werden dürfen,
/// um so jederzeit ein Replay zu erlauben.
/// </summary>
public interface IEventStoreAggregateRegistration
{
    /// <summary>
    /// Ordnet einem Aggregate einen Namen zu, der während des kompletten
    /// Application Lifce Cycle nicht mehr verändert werden darf,
    /// so dass jederzeit ein Replay der Ereignisse möglich ist.
    /// Auch bei Umbenennungen der Klasse
    /// darf dieser Name nicht verändert werden.
    /// </summary>
    void Register<TAggregate>(string immutableName)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    /// Gibt den immutable Name eines Aggregates zurück. Auch bei Umbenennungen der Klasse
    /// darf dieser Name nicht verändert werden.
    /// </summary>
    string GetImmutableName<TAggregate>()
        where TAggregate : EventSourcingAggregateRoot;

    string GetTypeOnImmutableName(string immutableName);
}