// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Errors
{
    public static partial class External
    {
        /// <summary>Creates an <see cref="ExternalServiceError"/> for external service call failures.</summary>
        public static ExternalServiceError ExternalService(string message = null, string serviceName = null, Exception innerException = null)
            => new(message, serviceName, innerException);

        /// <summary>Creates a <see cref="ServiceUnavailableError"/> when external service is unavailable.</summary>
        public static ServiceUnavailableError ServiceUnavailable(string message = null, Exception innerException = null)
            => new(message, innerException);

        /// <summary>Creates an <see cref="HttpError"/> for HTTP request failures.</summary>
        public static HttpError Http(string message = null, int? statusCode = null, string statusText = null, string url = null, string method = null, Exception innerException = null)
            => new(message, statusCode, statusText, url, method, innerException);

        /// <summary>Creates a <see cref="NotificationError"/> for notification sending failures.</summary>
        public static NotificationError Notification(string message = null, string channel = null, Exception innerException = null)
            => new(message, channel, innerException);

        /// <summary>Creates a <see cref="TimeoutError"/> when operation times out.</summary>
        public static TimeoutError Timeout(string message = null)
            => new(message);

        /// <summary>Creates an <see cref="ExternalError"/> for general external service and API errors.</summary>
        public static ExternalError Error(string message = null)
            => new(message);
    }
}

/// <summary>
/// Represents a general external service or API error.
/// </summary>
/// <param name="message">The error message that describes the external error. If null, a default message is used.</param>
public class ExternalError(string message = null) : ResultErrorBase(message ?? "External service error")
{
    public ExternalError() : this(null)
    {
    }
}