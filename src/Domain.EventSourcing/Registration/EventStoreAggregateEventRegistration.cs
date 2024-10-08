﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Registration;

using Model;

/// <summary>
///     Registratur für EventStore-AggregateEvents.
///     Bei der Registratur wird eine Name übergeben der während des kompletten
///     Application Life Cylcles der Applikation nicht mehr verändert werden dürfen,
///     um so jederzeit ein Replay zu erlauben.
/// </summary>
public class EventStoreAggregateEventRegistration : IEventStoreAggregateEventRegistration
{
    private readonly Dictionary<Type, string> registration = [];

    /// <summary>
    ///     Ordnet einem AggregateEvent einen Namen zu, der während des kompletten
    ///     Application Lifce Cycle nicht mehr verändert werden darf,
    ///     so dass jederzeit ein Replay der Ereignisse möglich ist.
    ///     Auch bei Umbenennungen der Klasse
    ///     darf dieser Name nicht verändert werden.
    /// </summary>
    public void Register<TAggregateEvent>(string immutableName)
        where TAggregateEvent : AggregateEvent
    {
        var type = typeof(TAggregateEvent);
        if (this.registration.Values.Contains(immutableName))
        {
            throw new ImmutableNameShouldBeUniqueException(immutableName);
        }

        this.registration.Add(type, immutableName);
    }

    /// <summary>
    ///     Gibt den immutable Name eines AggregateEvents zurück. Auch bei Umbenennungen der Klasse
    ///     darf dieser Name nicht verändert werden.
    /// </summary>
    public string GetImmutableName(IAggregateEvent aggregateEvent)
    {
        EnsureArg.IsNotNull(aggregateEvent, nameof(aggregateEvent));
        var type = aggregateEvent.GetType();
        if (this.registration.TryGetValue(type, out var immutableName))
        {
            return immutableName;
        }

        throw new AggregateIsNotRegisteredException();
    }

    /// <summary>
    ///     Gibt den Type als String anhand des übergebenen 'immutableNames" zurück. Der immutable Name
    ///     muss mit Register für den Type definiert worden sein.
    /// </summary>
    public string GetTypeOnImmutableName(string immutableName)
    {
        var found = this.registration.FirstOrDefault(pair => pair.Value == immutableName);

        return found.Key.FullName;
    }
}