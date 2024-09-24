// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Host;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

public class NotImplementedExceptionHandler(
    ILogger<NotImplementedExceptionHandler> logger,
    GlobalExceptionHandlerOptions options) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not NotImplementedException ex)
        {
            return false;
        }

        options ??= new GlobalExceptionHandlerOptions();

        if (options.EnableLogging)
        {
            logger.LogError(ex, "{ExceptionType} occurred: {ExceptionMessage}", ex.GetType().Name, ex.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Title = "Not Implemented",
            Status = StatusCodes.Status501NotImplemented,
            Detail = options.IncludeExceptionDetails ? $"[{ex.GetType().Name}] {ex.Message}" : null,
            Type = "https://httpstatuses.com/501"
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}