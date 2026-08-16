// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Registration;

/// <summary>
///     EventStore-Aggregates müssen mit einem ImmutableName bei der AggregateRegistration registriert werden.
/// </summary>
public class AggregateIsNotRegisteredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <c>AggregateIsNotRegisteredException</c> class.
    /// </summary>
    public AggregateIsNotRegisteredException() { }

    /// <summary>
    /// Initializes a new instance of the <c>AggregateIsNotRegisteredException</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    public AggregateIsNotRegisteredException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <c>AggregateIsNotRegisteredException</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="innerException">The inner exception used by the operation.</param>
    public AggregateIsNotRegisteredException(string message, Exception innerException)
        : base(message, innerException) { }
}
