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
///     Exception handler that processes exceptions based on fluent mappings
///     configured in <see cref="GlobalExceptionHandlerOptions.Mappings" />.
///     This handler is automatically registered and processes mapped exceptions
///     before the built-in handlers.
/// </summary>
public class MappedExceptionHandler(
    ILogger<MappedExceptionHandler> logger,
    GlobalExceptionHandlerOptions options) : IExceptionHandler
{
    /// <summary>
    ///     Attempts to handle the exception using configured mappings.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="exception">The exception to handle.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    ///     <c>true</c> if a mapping was found and the exception was handled;
    ///     otherwise, <c>false</c>.
    /// </returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        options ??= new GlobalExceptionHandlerOptions();

        var mapping = options.Mappings
            .FirstOrDefault(m => m.ExceptionType.IsInstanceOfType(exception));

        if (mapping is null)
        {
            return false;
        }

        if (options.IgnoredExceptions.Contains(exception.GetType()))
        {
            return true;
        }

        if (options.RethrowExceptions.Contains(exception.GetType()))
        {
            throw exception;
        }

        if (options.EnableLogging)
        {
            logger?.LogError(
                exception,
                "{ExceptionType} occurred: {ExceptionMessage}",
                exception.GetType().Name,
                exception.Message);
        }

        var problemDetails = this.CreateProblemDetails(httpContext, exception, mapping);

        options.EnrichProblemDetails?.Invoke(httpContext, problemDetails, exception);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        Exception exception,
        ExceptionMapping mapping)
    {
        if (mapping.ProblemDetailsFactory is not null)
        {
            return mapping.ProblemDetailsFactory(exception, httpContext);
        }

        return new ProblemDetails
        {
            Title = mapping.Title ?? "An error occurred",
            Status = mapping.StatusCode,
            Detail = options.IncludeExceptionDetails
                ? $"[{exception.GetType().Name}] {exception.Message}"
                : null,
            Type = mapping.TypeUri ?? $"https://httpstatuses.io/{mapping.StatusCode}",
            Instance = httpContext.Request.Path
        };
    }
}