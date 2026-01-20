// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler for <see cref="ModuleNotEnabledException" />.
///     Returns a 503 Service Unavailable response when a module is not enabled.
/// </summary>
public class ModuleNotEnabledExceptionHandler(
    ILogger<ModuleNotEnabledExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<ModuleNotEnabledException>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status503ServiceUnavailable;

    /// <inheritdoc />
    protected override string Title => "Module Not Enabled";
}