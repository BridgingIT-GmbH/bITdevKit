// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     The <c>AggregateNotFoundException</c> is thrown when an expected aggregate is not found.
/// </summary>
public class AggregateNotFoundException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AggregateNotFoundException" /> class.
    ///     Represents an exception that is thrown when an attempt to retrieve an aggregate fails because the aggregate cannot
    ///     be found.
    /// </summary>
    public AggregateNotFoundException() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AggregateNotFoundException" /> class.
    ///     Represents an exception that is thrown when an aggregate is not found.
    /// </summary>
    public AggregateNotFoundException(string message)
        : base(message) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AggregateNotFoundException" /> class.
    ///     Represents an exception that is thrown when an aggregate is not found.
    /// </summary>
    public AggregateNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }
}