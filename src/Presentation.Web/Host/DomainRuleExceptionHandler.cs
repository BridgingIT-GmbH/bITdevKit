// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Host;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

public class DomainRuleExceptionHandler(
    ILogger<DomainRuleExceptionHandler> logger,
    GlobalExceptionHandlerOptions options) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not RuleException ex)
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
            Title = "Bad Request",
            Status = StatusCodes.Status400BadRequest,
            Detail = $"[{ex.GetType().Name}] {ex.Message}",
            Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/400"
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}