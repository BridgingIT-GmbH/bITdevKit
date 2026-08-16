// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error that occurs during a HTTP operations.
/// </summary>
public class HttpError(string message = null, int? statusCode = null, string statusText = null, string url = null, string method = null, Exception innerException = null)
    : ResultErrorBase(message ?? "HTTP request failed")
{
    /// <summary>Gets the HTTP status code returned by the remote endpoint, when available.</summary>
    public int? StatusCode { get; } = statusCode;

    /// <summary>Gets the HTTP reason phrase or status text, when available.</summary>
    public string StatusText { get; } = statusText;

    /// <summary>Gets the request URL associated with the failure, when supplied.</summary>
    public string Url { get; } = url;

    /// <summary>Gets the HTTP method associated with the failed request, when supplied.</summary>
    public string Method { get; } = method;

    /// <summary>Gets the exception that caused or describes the HTTP failure, when available.</summary>
    public Exception InnerException { get; } = innerException;
}
