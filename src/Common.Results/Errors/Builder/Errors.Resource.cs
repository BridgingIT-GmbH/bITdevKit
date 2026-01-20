// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Errors
{
    public static partial class Resource
    {
        /// <summary>Creates a <see cref="ResourceUnavailableError"/> for temporarily unavailable resources.</summary>
        public static ResourceUnavailableError ResourceUnavailable(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="QuotaExceededError"/> for quota or limit exceeded scenarios.</summary>
        public static QuotaExceededError QuotaExceeded(string message = null, long? currentValue = null, long? maxAllowed = null)
            => new(message, currentValue, maxAllowed);

        /// <summary>Creates a <see cref="PartialOperationError"/> for partially completed operations.</summary>
        public static PartialOperationError PartialOperation(string message = null, IEnumerable<string> failedPaths = null, Exception innerException = null)
            => new(message, failedPaths, innerException);

        /// <summary>Creates a <see cref="ResourceError"/> for general resource availability and quota errors.</summary>
        public static ResourceError Error(string message = null)
            => new(message);
    }
}

/// <summary>
/// Represents a general resource availability or quota error.
/// </summary>
/// <param name="message">The error message that describes the resource error. If null, a default message is used.</param>
public class ResourceError(string message = null) : ResultErrorBase(message ?? "Resource error")
{
    public ResourceError() : this(null)
    {
    }
}
