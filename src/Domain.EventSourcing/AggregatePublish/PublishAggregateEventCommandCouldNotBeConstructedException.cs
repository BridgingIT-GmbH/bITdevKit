// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;

using System;

/// <summary>
/// Ein Aggregate benötigt zwingend einen Konstruktor mit dem Parameter Guid, an zweiter Stelle vom Typ IEnumerable&lt;IAggregateEvent&gt; savedEvents und an
/// dritter Stelle IMediator.
/// </summary>
public class PublishAggregateEventCommandCouldNotBeConstructedException : Exception
{
    public PublishAggregateEventCommandCouldNotBeConstructedException()
    {
    }

    public PublishAggregateEventCommandCouldNotBeConstructedException(string message)
        : base(message)
    {
    }

    public PublishAggregateEventCommandCouldNotBeConstructedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}