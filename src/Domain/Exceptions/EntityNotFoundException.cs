// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     EntityNotFoundException is thrown when a requested entity cannot be found.
/// </summary>
public class EntityNotFoundException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="EntityNotFoundException" /> class.
    ///     Exception thrown when a requested entity is not found in the repository.
    /// </summary>
    public EntityNotFoundException() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="EntityNotFoundException" /> class.
    ///     Represents an exception that is thrown when an entity is not found.
    /// </summary>
    public EntityNotFoundException(string message)
        : base(message) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="EntityNotFoundException" /> class.
    ///     Represents an exception that is thrown when an entity is not found.
    /// </summary>
    public EntityNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }
}