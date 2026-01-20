// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
///     Options for configuring global exception handling behavior.
/// </summary>
public class GlobalExceptionHandlerOptions : ExceptionHandlerOptions
{
    /// <summary>
    ///     Gets or sets a value indicating whether exception details
    ///     are included in the problem details response.
    /// </summary>
    public bool IncludeExceptionDetails { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether exceptions should be logged.
    /// </summary>
    public bool EnableLogging { get; set; } = false;

    /// <summary>
    ///     Gets the collection of additional exception handler registrations.
    ///     These handlers are registered before the built-in handlers.
    /// </summary>
    public List<ExceptionHandlerRegistration> AdditionalHandlers { get; } = [];

    /// <summary>
    ///     Gets the collection of exception mappings for fluent exception-to-response configuration.
    /// </summary>
    public List<ExceptionMapping> Mappings { get; } = [];

    /// <summary>
    ///     Gets the collection of exception types that should be silently ignored.
    ///     Ignored exceptions will not produce a response and will not be logged.
    /// </summary>
    public HashSet<Type> IgnoredExceptions { get; } = [];

    /// <summary>
    ///     Gets the collection of exception types that should be rethrown.
    ///     Rethrown exceptions bypass the exception handler and propagate up the middleware pipeline.
    /// </summary>
    public HashSet<Type> RethrowExceptions { get; } = [];

    /// <summary>
    ///     Gets or sets a delegate to enrich problem details with additional information.
    ///     This is called for all exception responses after the problem details are created.
    /// </summary>
    /// <example>
    ///     <code>
    ///     options.EnrichProblemDetails = (context, problem, exception) =>
    ///     {
    ///         problem.Extensions["correlationId"] = context.GetCorrelationId();
    ///         problem.Extensions["timestamp"] = DateTimeOffset.UtcNow;
    ///         problem.Extensions["path"] = context.Request.Path;
    ///     };
    ///     </code>
    /// </example>
    public Action<HttpContext, ProblemDetails, Exception> EnrichProblemDetails { get; set; }

    /// <summary>
    ///     Adds an additional exception handler type to the collection with optional priority.
    ///     The handler must implement <see cref="IExceptionHandler" />.
    /// </summary>
    /// <typeparam name="THandler">The type of the exception handler to add.</typeparam>
    /// <param name="priority">
    ///     The priority of the handler. Higher values are executed first. Default is 0.
    /// </param>
    /// <returns>The current <see cref="GlobalExceptionHandlerOptions" /> instance for chaining.</returns>
    /// <example>
    ///     <code>
    ///     options.AddHandler&lt;HighPriorityHandler&gt;(priority: 100)
    ///            .AddHandler&lt;NormalHandler&gt;()
    ///            .AddHandler&lt;LowPriorityHandler&gt;(priority: -100);
    ///     </code>
    /// </example>
    public GlobalExceptionHandlerOptions AddHandler<THandler>(int priority = 0)
        where THandler : IExceptionHandler
    {
        this.AdditionalHandlers.Add(new ExceptionHandlerRegistration
        {
            HandlerType = typeof(THandler),
            Priority = priority
        });

        return this;
    }

    /// <summary>
    ///     Adds an additional exception handler type conditionally with optional priority.
    ///     The handler is only registered if the condition is true.
    /// </summary>
    /// <typeparam name="THandler">The type of the exception handler to add.</typeparam>
    /// <param name="when">The condition that must be true for the handler to be registered.</param>
    /// <param name="priority">
    ///     The priority of the handler. Higher values are executed first. Default is 0.
    /// </param>
    /// <returns>The current <see cref="GlobalExceptionHandlerOptions" /> instance for chaining.</returns>
    /// <example>
    ///     <code>
    ///     options.AddHandler&lt;DebugExceptionHandler&gt;(
    ///                when: environment.IsDevelopment(),
    ///                priority: 100)
    ///            .AddHandler&lt;ProductionExceptionHandler&gt;(
    ///                when: environment.IsProduction());
    ///     </code>
    /// </example>
    public GlobalExceptionHandlerOptions AddHandler<THandler>(bool when, int priority = 0)
        where THandler : IExceptionHandler
    {
        if (when)
        {
            this.AdditionalHandlers.Add(new ExceptionHandlerRegistration
            {
                HandlerType = typeof(THandler),
                Priority = priority
            });
        }

        return this;
    }

    /// <summary>
    ///     Maps an exception type to a specific HTTP status code and optional title.
    /// </summary>
    /// <typeparam name="TException">The exception type to map.</typeparam>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="title">The optional title for the problem details.</param>
    /// <param name="typeUri">The optional type URI for the problem details.</param>
    /// <returns>The current <see cref="GlobalExceptionHandlerOptions" /> instance for chaining.</returns>
    /// <example>
    ///     <code>
    ///     options.Map&lt;NotFoundException&gt;(StatusCodes.Status404NotFound, "Resource Not Found")
    ///            .Map&lt;ConflictException&gt;(StatusCodes.Status409Conflict, "Resource Conflict")
    ///            .Map&lt;UnauthorizedException&gt;(StatusCodes.Status401Unauthorized);
    ///     </code>
    /// </example>
    public GlobalExceptionHandlerOptions Map<TException>(
        int statusCode,
        string title = null,
        string typeUri = null)
        where TException : Exception
    {
        this.Mappings.Add(new ExceptionMapping
        {
            ExceptionType = typeof(TException),
            StatusCode = statusCode,
            Title = title,
            TypeUri = typeUri
        });

        return this;
    }

    /// <summary>
    ///     Maps an exception type using a custom factory function to create problem details.
    /// </summary>
    /// <typeparam name="TException">The exception type to map.</typeparam>
    /// <param name="factory">
    ///     A factory function that creates problem details from the exception and HTTP context.
    /// </param>
    /// <returns>The current <see cref="GlobalExceptionHandlerOptions" /> instance for chaining.</returns>
    /// <example>
    ///     <code>
    ///     options.Map&lt;ValidationException&gt;((ex, context) => new ProblemDetails
    ///     {
    ///         Title = "Validation Failed",
    ///         Status = StatusCodes.Status422UnprocessableEntity,
    ///         Detail = ex.Message,
    ///         Extensions = { ["errors"] = ex.Errors }
    ///     });
    ///     </code>
    /// </example>
    public GlobalExceptionHandlerOptions Map<TException>(
        Func<TException, HttpContext, ProblemDetails> factory)
        where TException : Exception
    {
        EnsureArg.IsNotNull(factory, nameof(factory));

        this.Mappings.Add(new ExceptionMapping
        {
            ExceptionType = typeof(TException),
            ProblemDetailsFactory = (ex, ctx) => factory((TException)ex, ctx)
        });

        return this;
    }

    /// <summary>
    ///     Configures an exception type to be silently ignored.
    ///     Ignored exceptions will not produce a response and will not be logged.
    /// </summary>
    /// <typeparam name="TException">The exception type to ignore.</typeparam>
    /// <returns>The current <see cref="GlobalExceptionHandlerOptions" /> instance for chaining.</returns>
    /// <example>
    ///     <code>
    ///     options.Ignore&lt;OperationCanceledException&gt;()
    ///            .Ignore&lt;TaskCanceledException&gt;();
    ///     </code>
    /// </example>
    public GlobalExceptionHandlerOptions Ignore<TException>()
        where TException : Exception
    {
        this.IgnoredExceptions.Add(typeof(TException));

        return this;
    }

    /// <summary>
    ///     Configures an exception type to be rethrown, bypassing the exception handler.
    ///     The exception will propagate up the middleware pipeline.
    /// </summary>
    /// <typeparam name="TException">The exception type to rethrow.</typeparam>
    /// <returns>The current <see cref="GlobalExceptionHandlerOptions" /> instance for chaining.</returns>
    /// <example>
    ///     <code>
    ///     options.Rethrow&lt;CriticalSystemException&gt;()
    ///            .Rethrow&lt;FatalException&gt;();
    ///     </code>
    /// </example>
    public GlobalExceptionHandlerOptions Rethrow<TException>()
        where TException : Exception
    {
        this.RethrowExceptions.Add(typeof(TException));

        return this;
    }
}