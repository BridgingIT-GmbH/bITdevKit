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
    public string ServiceName { get; } = serviceName;

    public Exception InnerException { get; } = innerException;

    public ExternalServiceError() : this(null, null, null)
    {
    }

    public ExternalServiceError(string serviceName, Exception innerException) : this(null, serviceName, innerException)
    {
    }
}