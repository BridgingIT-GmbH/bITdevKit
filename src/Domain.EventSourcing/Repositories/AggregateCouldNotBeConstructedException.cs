// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Repositories;

/// <summary>
///     Ein Aggregate benötigt zwingend einen Konstruktor mit dem Parameter Guid, an zweiter Stelle vom Typ IEnumerable&lt;
///     IAggregateEvent&gt; savedEvents.
/// </summary>
public class AggregateCouldNotBeConstructedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <c>AggregateCouldNotBeConstructedException</c> class.
    /// </summary>
    public AggregateCouldNotBeConstructedException() { }

    /// <summary>
    /// Initializes a new instance of the <c>AggregateCouldNotBeConstructedException</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    public AggregateCouldNotBeConstructedException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <c>AggregateCouldNotBeConstructedException</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="innerException">The inner exception used by the operation.</param>
    public AggregateCouldNotBeConstructedException(string message, Exception innerException)
        : base(message, innerException) { }
}
