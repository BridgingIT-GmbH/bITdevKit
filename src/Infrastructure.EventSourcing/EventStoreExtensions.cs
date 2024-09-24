// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Common;
using Domain.EventSourcing.Model;
using Domain.EventSourcing.Store;

public static class EventStoreExtensions
{
    public static EventBlob ConvertToBlob(this IAggregateEvent @event, ISerializer serializer)
    {
        EnsureArg.IsNotNull(serializer, nameof(serializer));

        return new EventBlob(@event.GetType(), serializer.SerializeToBytes(@event));
    }

    public static IAggregateEvent ConvertFromBlob(
        this byte[] data,
        string typename,
        ISerializer serializer,
        IEventTypeSelector typeSelector)
    {
        EnsureArg.IsNotNull(serializer, nameof(serializer));
        EnsureArg.IsNotNull(typeSelector, nameof(typeSelector));

        var type = typeSelector.FindType(typename);
        using var stream = new MemoryStream(data);
        var @event = serializer.Deserialize(stream, type);
        return @event as IAggregateEvent;
    }

    public static EventSourcingAggregateRoot ConvertFromBlob(
        this byte[] data,
        string typename,
        ISerializer serializer,
        IAggregateTypeSelector typeSelector)
    {
        EnsureArg.IsNotNull(serializer, nameof(serializer));
        EnsureArg.IsNotNull(typeSelector, nameof(typeSelector));

        var type = typeSelector.Find(typename);
        using var stream = new MemoryStream(data);
        var @event = serializer.Deserialize(stream, type);
        return @event as EventSourcingAggregateRoot;
    }

    public static EventBlob ConvertToBlob(this EventSourcingAggregateRoot aggregate, ISerializer serializer)
    {
        EnsureArg.IsNotNull(serializer, nameof(serializer));

        return new EventBlob(aggregate.GetType(), serializer.SerializeToBytes(aggregate));
    }
}