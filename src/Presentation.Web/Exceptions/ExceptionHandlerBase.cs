// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Presentation.Web.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///     Abstract base class for exception handlers that provides common functionality
///     for logging, problem details creation, response writing, and enrichment.
/// </summary>
/// <typeparam name="TException">The specific exception type this handler processes.</typeparam>
/// <example>
///     <code>
///     public class MyCustomExceptionHandler : ExceptionHandlerBase&lt;MyCustomException&gt;
///     {
///         public MyCustomExceptionHandler(
///             ILogger&lt;MyCustomExceptionHandler&gt; logger,
///             GlobalExceptionHandlerOptions options)
///             : base(logger, options)
///         {
///         }
///
///         protected override int StatusCode => StatusCodes.Status400BadRequest;
///
///         protected override string Title => "Custom Error";
///
///         protected override string GetDetail(MyCustomException exception)
///         {
///             return $"Custom error occurred: {exception.CustomProperty}";
///         }
///     }
///     </code>
/// </example>
/// <remarks>
///     Initializes a new instance of the <see cref="ExceptionHandlerBase{TException}" /> class.
/// </remarks>
/// <param name="logger">The logger instance.</param>
/// <param name="options">The global exception handler options.</param>
public abstract class ExceptionHandlerBase<TException>(
    ILogger logger,
    GlobalExceptionHandlerOptions options) : IExceptionHandler
    where TException : Exception
{
    /// <summary>
    ///     Gets the HTTP status code for the response.
    /// </summary>
    protected abstract int StatusCode { get; }

    /// <summary>
    ///     Gets the title for the problem details response.
    /// </summary>
    protected abstract string Title { get; }

    /// <summary>
    ///     Gets the type URI for the problem details response.
    ///     Defaults to the MDN documentation for the status code.
    /// </summary>
    protected virtual string TypeUri => $"https://httpstatuses.io/{this.StatusCode}";

    /// <summary>
    ///     Gets the logger instance.
    /// </summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    ///     Gets the global exception handler options.
    /// </summary>
    protected GlobalExceptionHandlerOptions Options { get; } = options ?? new GlobalExceptionHandlerOptions();

    /// <summary>
    ///     Attempts to handle the exception.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="exception">The exception to handle.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    ///     <c>true</c> if the exception was handled; otherwise, <c>false</c>.
    /// </returns>
    public async virtual ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not TException typedException)
        {
            return false;
        }

        var combined = httpContext.RequestServices?.GetService<CombinedProblemDetailsRegistration>() is not null;
        if (combined && ResponseProblemDetailsService.HasFailedWrite(httpContext))
        {
            return false;
        }

        if (this.ShouldIgnore(exception))
        {
            return true;
        }

        if (this.ShouldRethrow(exception))
        {
            throw exception;
        }

        if (!combined && this.Options.EnableLogging)
        {
            this.LogException(typedException);
        }

        var problemDetails = combined
            ? this.CreateSharedProblemDetails(httpContext, typedException)
            : this.CreateProblemDetails(httpContext, typedException);

        this.Options.EnrichProblemDetails?.Invoke(httpContext, problemDetails, typedException);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        if (combined)
        {
            var options = httpContext.RequestServices.GetRequiredService<IOptions<Errors.ProblemDetailsOptions>>().Value;
            ResponseProblemDetailsExceptionHandler.LogHandledException(this.Logger, options, exception, httpContext.Response.StatusCode);
            ResponseProblemDetailsHeaderSnapshot.Restore(httpContext);
            var written = await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
                Exception = exception,
                AdditionalMetadata = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint?.Metadata
            });
            if (!written)
            {
                await ResponseProblemDetailsStatusCodeHandler.WriteFallbackAsync(httpContext, problemDetails, cancellationToken);
            }
        }
        else
        {
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        }

        return true;
    }

    /// <summary>
    ///     Gets the detail message for the problem details response.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The detail message, or null if details should not be included.</returns>
    protected virtual string GetDetail(TException exception)
    {
        return this.Options.IncludeExceptionDetails
            ? $"[{exception.GetType().Name}] {exception.Message}"
            : null;
    }

    /// <summary>
    ///     Creates the problem details response for the exception.
    ///     Override this method for complete control over the problem details.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="exception">The exception.</param>
    /// <returns>The problem details response.</returns>
    protected virtual ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        TException exception)
    {
        return new ProblemDetails
        {
            Title = this.Title,
            Status = this.StatusCode,
            Detail = this.GetDetail(exception),
            Type = this.TypeUri,
            Instance = httpContext.Request.Path
        };
    }

    /// <summary>Creates the public problem for the combined DevKit response policy.</summary>
    /// <param name="httpContext">The request that raised the exception.</param>
    /// <param name="exception">The exception handled by this instance.</param>
    /// <returns>A problem with public detail and the handler's selected status, title and type.</returns>
    /// <remarks>Override this method to customize public fields for the combined registration.</remarks>
    protected virtual ProblemDetails CreateSharedProblemDetails(HttpContext httpContext, TException exception)
    {
        var problem = ProblemDetailsPayload.CreateExceptionProblem(exception);
        problem.Status = this.StatusCode;
        problem.Title = this.Title;
        problem.Type = this.TypeUri;
        problem.Instance = httpContext.Request.Path;
        return problem;
    }

    /// <summary>
    ///     Logs the exception. Override to customize logging behavior.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    protected virtual void LogException(TException exception)
    {
        this.Logger?.LogError(
            exception,
            "{ExceptionType} occurred: {ExceptionMessage}",
            exception.GetType().Name,
            exception.Message);
    }

    /// <summary>
    ///     Determines whether the exception should be ignored.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><c>true</c> if the exception should be ignored; otherwise, <c>false</c>.</returns>
    protected virtual bool ShouldIgnore(Exception exception)
    {
        return this.Options.IgnoredExceptions.Contains(exception.GetType());
    }

    /// <summary>
    ///     Determines whether the exception should be rethrown.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><c>true</c> if the exception should be rethrown; otherwise, <c>false</c>.</returns>
    protected virtual bool ShouldRethrow(Exception exception)
    {
        return this.Options.RethrowExceptions.Contains(exception.GetType());
    }
}
