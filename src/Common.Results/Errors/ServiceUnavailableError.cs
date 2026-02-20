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
    public Exception InnerException { get; } = innerException;

    public ServiceUnavailableError() : this(null, null)
    {
    }

    public ServiceUnavailableError(Exception innerException) : this(null, innerException)
    {
    }
}