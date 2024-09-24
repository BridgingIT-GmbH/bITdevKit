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
    public AggregateIsNotRegisteredException() { }

    public AggregateIsNotRegisteredException(string message)
        : base(message) { }

    public AggregateIsNotRegisteredException(string message, Exception innerException)
        : base(message, innerException) { }
}