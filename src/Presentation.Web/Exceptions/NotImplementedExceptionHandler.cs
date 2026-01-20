// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler for <see cref="NotImplementedException" />.
///     Returns a 501 Not Implemented response.
/// </summary>
public class NotImplementedExceptionHandler(
    ILogger<NotImplementedExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<NotImplementedException>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status501NotImplemented;

    /// <inheritdoc />
    protected override string Title => "Not Implemented";
}