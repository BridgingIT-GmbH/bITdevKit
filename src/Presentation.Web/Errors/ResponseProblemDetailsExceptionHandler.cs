// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Errors;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Problem = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>Maps unhandled exceptions to DevKit Problem Details within the configured request scope.</summary>
/// <example><code>builder.Services.AddResponseProblemDetails();</code></example>
public sealed class ResponseProblemDetailsExceptionHandler(
    IOptions<ProblemDetailsOptions> options,
    IProblemDetailsService service,
    ILogger<ResponseProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    private readonly IProblemDetailsService service = service;
    private readonly ILogger<ResponseProblemDetailsExceptionHandler> logger = logger;
    private readonly ProblemDetailsOptions options = options.Value;
    private readonly ProblemDetailsOptions.Registration[] mappings = [.. options.Value.HostMappings, .. options.Value.ModuleMappings];

    /// <inheritdoc />
    /// <example><code>await handler.TryHandleAsync(httpContext, exception, httpContext.RequestAborted);</code></example>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (ResponseProblemDetailsService.HasFailedWrite(httpContext) || !this.ShouldHandleException(httpContext))
        {
            httpContext.Features.Set(new DeclinedException(exception));
            return false;
        }

        httpContext.Features.Set<DeclinedException>(null);
        var problem = this.Map(exception, httpContext);
        httpContext.Response.StatusCode = problem.Status.Value;
        LogHandledException(this.logger, this.options, exception, httpContext.Response.StatusCode);

        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
            AdditionalMetadata = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint?.Metadata
        };
        if (!await this.service.TryWriteAsync(context))
        {
            await ResponseProblemDetailsStatusCodeHandler.WriteFallbackAsync(httpContext, problem, cancellationToken);
        }

        return true;
    }

    /// <summary>Logs a server exception at the selected status when the combined policy enables logging.</summary>
    /// <param name="logger">The handler's logger.</param>
    /// <param name="options">The combined Problem Details options.</param>
    /// <param name="exception">The handled exception.</param>
    /// <param name="statusCode">The selected HTTP response status.</param>
    public static void LogHandledException(ILogger logger, ProblemDetailsOptions options, Exception exception, int statusCode)
    {
        if (options.ShouldLogUnhandledException && statusCode is >= 500 and <= 599)
        {
            logger?.LogError(exception, "Unhandled exception mapped to HTTP {StatusCode}.", statusCode);
        }
    }

    /// <summary>Identifies an exception outside automatic DevKit scope for native fallback.</summary>
    /// <param name="Exception">The exception declined by the DevKit handler.</param>
    public sealed record DeclinedException(Exception Exception);

    private bool ShouldHandleException(HttpContext context) =>
        this.options.ShouldHandleRequest?.Invoke(context) ??
        (context.Features.Get<IExceptionHandlerPathFeature>() is not { } feature ||
            string.Equals(feature.Path, context.Request.Path.Value, StringComparison.Ordinal));

    private Problem Map(Exception exception, HttpContext context)
    {
        foreach (var registration in this.mappings)
        {
            if (registration.Mapping.ExceptionType.IsInstanceOfType(exception) &&
                (registration.When?.Invoke(exception, context) ?? true))
            {
                var problem = registration.Mapping.ProblemDetailsFactory(exception, context);
                return problem?.Status is >= 400 and <= 599
                    ? problem
                    : throw new InvalidOperationException("An exception mapping must return Problem Details with an HTTP status between 400 and 599.");
            }
        }

        return ProblemDetailsPayload.CreateExceptionProblem(exception);
    }
}
