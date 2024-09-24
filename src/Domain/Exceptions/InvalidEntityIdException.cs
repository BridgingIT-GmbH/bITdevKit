// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Represents errors that occur when an entity ID is deemed invalid.
/// </summary>
public class InvalidEntityIdException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidEntityIdException" /> class.
    ///     The InvalidEntityIdException is thrown when an entity ID is found to be invalid.
    /// </summary>
    public InvalidEntityIdException() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidEntityIdException" /> class.
    ///     Represents errors that occur when an invalid entity id is encountered.
    /// </summary>
    public InvalidEntityIdException(string message)
        : base(message) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidEntityIdException" /> class.
    ///     Exception thrown when an entity ID is invalid.
    /// </summary>
    public InvalidEntityIdException(string message, Exception innerException)
        : base(message, innerException) { }
}