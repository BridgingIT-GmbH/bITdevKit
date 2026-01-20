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
    public int? StatusCode { get; } = statusCode;

    public string StatusText { get; } = statusText;

    public string Url { get; } = url;

    public string Method { get; } = method;

    public Exception InnerException { get; } = innerException;
}