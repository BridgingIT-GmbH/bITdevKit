// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler for <see cref="HttpRequestException" />.
///     Returns a 503 Service Unavailable response when an HTTP request fails.
/// </summary>
public class HttpRequestExceptionHandler(
    ILogger<HttpRequestExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<HttpRequestException>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status503ServiceUnavailable;

    /// <inheritdoc />
    protected override string Title => "Service Unavailable";
}