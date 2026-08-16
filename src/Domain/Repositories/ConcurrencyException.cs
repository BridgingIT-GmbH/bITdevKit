// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Represents concurrency exception.
/// </summary>
public class ConcurrencyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <c>ConcurrencyException</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    public ConcurrencyException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <c>ConcurrencyException</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="innerException">The inner exception used by the operation.</param>
    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Gets or sets the entity id.
    /// </summary>
    public string EntityId { get; init; }

    /// <summary>
    /// Gets or sets the expected version.
    /// </summary>
    public Guid ExpectedVersion { get; init; }

    /// <summary>
    /// Gets or sets the actual version.
    /// </summary>
    public Guid ActualVersion { get; init; }
}
