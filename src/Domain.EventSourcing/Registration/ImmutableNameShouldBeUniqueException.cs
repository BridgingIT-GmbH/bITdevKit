// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Registration;

/// <summary>
/// Represents immutable name should be unique exception.
/// </summary>
public class ImmutableNameShouldBeUniqueException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <c>ImmutableNameShouldBeUniqueException</c> class.
    /// </summary>
    public ImmutableNameShouldBeUniqueException() { }

    /// <summary>
    /// Initializes a new instance of the <c>ImmutableNameShouldBeUniqueException</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    public ImmutableNameShouldBeUniqueException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <c>ImmutableNameShouldBeUniqueException</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="innerException">The inner exception used by the operation.</param>
    public ImmutableNameShouldBeUniqueException(string message, Exception innerException)
        : base(message, innerException) { }
}
