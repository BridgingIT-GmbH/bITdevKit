// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
///     Global catch-all exception handler that processes any unhandled exceptions.
///     This handler is always registered last and handles exceptions that were not
///     caught by more specific handlers.
/// </summary>
public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    GlobalExceptionHandlerOptions options) : IExceptionHandler
{
    /// <summary>
    ///     Attempts to handle the exception.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="exception">The exception to handle.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Always returns <c>true</c> as this is the catch-all handler.</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not Exception ex)
        {
            return false;
        }

        options ??= new GlobalExceptionHandlerOptions();

        if (options.IgnoredExceptions.Contains(ex.GetType()))
        {
            return true;
        }

        if (options.RethrowExceptions.Contains(ex.GetType()))
        {
            throw exception;
        }

        if (options.EnableLogging)
        {
            logger?.LogError(
                ex,
                "{ExceptionType} occurred: {ExceptionMessage}",
                ex.GetType().Name,
                ex.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = options.IncludeExceptionDetails
                ? $"[{ex.GetType().Name}] {ex.Message}"
                : null,
            Type = "https://httpstatuses.io/500",
            Instance = httpContext.Request.Path
        };

        options.EnrichProblemDetails?.Invoke(httpContext, problemDetails, ex);

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}