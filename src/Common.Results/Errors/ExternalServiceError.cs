// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error that occurs when calling external services or APIs.
/// </summary>
public class ExternalServiceError(string message = null, string serviceName = null, Exception innerException = null)
    : ResultErrorBase(message ?? "External service error")
{
    /// <summary>Gets the name of the external service that failed, when supplied.</summary>
    public string ServiceName { get; } = serviceName;

    /// <summary>Gets the exception produced by the external service call, when available.</summary>
    public Exception InnerException { get; } = innerException;

    /// <summary>Initializes an external-service error with the default message and no service details.</summary>
    public ExternalServiceError() : this(null, null, null)
    {
    }

    /// <summary>Initializes an external-service error for a named service and exception.</summary>
    /// <param name="serviceName">The name of the external service that failed.</param>
    /// <param name="innerException">The exception produced by the service call.</param>
    public ExternalServiceError(string serviceName, Exception innerException) : this(null, serviceName, innerException)
    {
    }
}
