// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler for <see cref="SecurityException" />.
///     Returns a 401 Unauthorized response when a security violation occurs.
/// </summary>
public class SecurityExceptionHandler(
    ILogger<SecurityExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<SecurityException>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status401Unauthorized;

    /// <inheritdoc />
    protected override string Title => "Unauthorized";
}