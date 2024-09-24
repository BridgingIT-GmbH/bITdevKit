// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     The InvalidAggregateIdException is thrown when an invalid aggregate ID is encountered in the domain layer.
/// </summary>
/// <remarks>
///     This exception indicates that the provided aggregate ID does not adhere to the required format or constraints.
/// </remarks>
public class InvalidAggregateIdException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidAggregateIdException" /> class.
    ///     Represents errors that occur when an invalid aggregate ID is encountered.
    /// </summary>
    public InvalidAggregateIdException() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidAggregateIdException" /> class.
    ///     Exception thrown when an invalid aggregate identifier is encountered.
    /// </summary>
    public InvalidAggregateIdException(string message)
        : base(message) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidAggregateIdException" /> class.
    ///     Represents errors that occur when an aggregate ID is invalid.
    /// </summary>
    public InvalidAggregateIdException(string message, Exception innerException)
        : base(message, innerException) { }
}