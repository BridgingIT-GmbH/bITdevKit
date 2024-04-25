// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Registration;

using System;
using System.Collections.Generic;
using System.Linq;
using BridgingIT.DevKit.Domain.EventSourcing.Model;

/// <summary>
/// Registratur für EventStore-Aggregates.
/// Bei der Registratur wird eine Name übergeben der während des kompletten
/// Application Life Cylcles der Applikation nicht mehr verändert werden dürfen,
/// um so jederzeit ein Replay zu erlauben.
/// </summary>
public class EventStoreAggregateRegistration : IEventStoreAggregateRegistration
{
    private readonly Dictionary<Type, string> registration = new();

    /// <summary>
    /// Ordnet einem Aggregate einen Namen zu, der während des kompletten
    /// Application Lifce Cycle nicht mehr verändert werden darf,
    /// so dass jederzeit ein Replay der Ereignisse möglich ist.
    /// Auch bei Umbenennungen der Klasse
    /// darf dieser Name nicht verändert werden.
    /// </summary>
    public void Register<TAggregate>(string immutableName)
        where TAggregate : EventSourcingAggregateRoot
    {
        var type = typeof(TAggregate);
        if (this.registration.Values.Contains(immutableName))
        {
            throw new ImmutableNameShouldBeUniqueException(immutableName);
        }

        this.registration.Add(type, immutableName);
    }

    /// <summary>
    /// Gibt den immutable Name eines Aggregates zurück. Auch bei Umbenennungen der Klasse
    /// darf dieser Name nicht verändert werden.
    /// </summary>
    public string GetImmutableName<TAggregate>()
        where TAggregate : EventSourcingAggregateRoot
    {
        var type = typeof(TAggregate);
        if (this.registration.TryGetValue(type, out var immutableName))
        {
            return immutableName;
        }

        throw new AggregateIsNotRegisteredException();
    }

    public string GetTypeOnImmutableName(string immutableName)
    {
        var found = this.registration.FirstOrDefault(pair => pair.Value == immutableName);
        return found.Key.FullName;
    }
}