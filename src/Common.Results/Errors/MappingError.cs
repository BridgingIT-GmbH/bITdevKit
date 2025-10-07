// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error result that encapsulates a mapping failure.
/// </summary>
public class MappingError : ExceptionError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MappingError"/> class.
    /// </summary>
    /// <param name="exception">The exception to encapsulate.</param>
    public MappingError(Exception exception) : base(exception)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MappingError"/> class.
    /// </summary>
    /// <param name="exception">The exception to encapsulate.</param>
    /// <param name="message"></param>
    public MappingError(Exception exception, string message) : base(exception, message)
    {
    }
}