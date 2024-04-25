// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Store;

using System;

public class EventBlob
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventBlob"/> class.
    /// The EventBlob is used for the in memory implementation of the EventStore. It is a helper class to know the related type of an event.
    /// </summary>
    /// <param name="eventType">The type of the event.</param>
    /// <param name="blob">The byte buffer which holds the content of the event.</param>
    public EventBlob(Type eventType, byte[] blob)
    {
        this.EventType = eventType;
        this.Blob = blob;
    }

    public Type EventType { get; private set; }

    public byte[] Blob { get; private set; } =[];

    public string InfinityImmutableTypeIdentifier { get; private set; }
}