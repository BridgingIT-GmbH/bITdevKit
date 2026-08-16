// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error indicating that an external service is unavailable.
/// </summary>
public class ServiceUnavailableError(string message = null, Exception innerException = null)
    : ResultErrorBase(message ?? "Service is unavailable")
{
    /// <summary>Gets the exception that caused or describes the service outage, when available.</summary>
    public Exception InnerException { get; } = innerException;

    /// <summary>Initializes a service-unavailable error with the default message and no exception.</summary>
    public ServiceUnavailableError() : this(null, null)
    {
    }

    /// <summary>Initializes a service-unavailable error from an exception and uses the default message.</summary>
    /// <param name="innerException">The exception that caused or describes the service outage.</param>
    public ServiceUnavailableError(Exception innerException) : this(null, innerException)
    {
    }
}
