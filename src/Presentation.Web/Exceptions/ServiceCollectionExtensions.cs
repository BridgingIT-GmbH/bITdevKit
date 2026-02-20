// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Diagnostics;

/// <summary>
///     Extension methods for configuring exception handling services.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the global exception handler and built-in exception handlers
    ///     to the service collection with default options.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection" /> for chaining.</returns>
    /// <example>
    ///     <code>
    ///     // In Program.cs or Startup.cs
    ///     builder.Services.AddExceptionHandler();
    ///
    ///     // In the middleware pipeline
    ///     app.UseExceptionHandler();
    ///     </code>
    /// </example>
    public static IServiceCollection AddExceptionHandler(this IServiceCollection services)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        return services.AddExceptionHandler(new GlobalExceptionHandlerOptions());
    }

    /// <summary>
    ///     Adds the global exception handler and built-in exception handlers
    ///     to the service collection with the specified options configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="configureOptions">
    ///     A delegate to configure the <see cref="GlobalExceptionHandlerOptions" />.
    /// </param>
    /// <returns>The <see cref="IServiceCollection" /> for chaining.</returns>
    /// <example>
    ///     <code>
    ///     // Basic configuration
    ///     builder.Services.AddExceptionHandler(options =>
    ///     {
    ///         options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    ///         options.EnableLogging = true;
    ///     });
    ///
    ///     // With custom handlers and priority
    ///     builder.Services.AddExceptionHandler(options =>
    ///     {
    ///         options.AddHandler&lt;HighPriorityHandler&gt;(priority: 100)
    ///                .AddHandler&lt;DebugHandler&gt;(when: env.IsDevelopment())
    ///                .AddHandler&lt;NormalHandler&gt;();
    ///     });
    ///
    ///     // With fluent exception mapping
    ///     builder.Services.AddExceptionHandler(options =>
    ///     {
    ///         options.Map&lt;NotFoundException&gt;(StatusCodes.Status404NotFound, "Resource Not Found")
    ///                .Map&lt;ConflictException&gt;(StatusCodes.Status409Conflict)
    ///                .Map&lt;BusinessException&gt;((ex, ctx) => new ProblemDetails
    ///                {
    ///                    Title = "Business Rule Violation",
    ///                    Status = StatusCodes.Status422UnprocessableEntity,
    ///                    Detail = ex.Message,
    ///                    Extensions = { ["code"] = ex.ErrorCode }
    ///                });
    ///     });
    ///
    ///     // With exception filtering
    ///     builder.Services.AddExceptionHandler(options =>
    ///     {
    ///         options.Ignore&lt;OperationCanceledException&gt;()
    ///                .Ignore&lt;TaskCanceledException&gt;()
    ///                .Rethrow&lt;CriticalSystemException&gt;();
    ///     });
    ///
    ///     // With problem details enrichment
    ///     builder.Services.AddExceptionHandler(options =>
    ///     {
    ///         options.EnrichProblemDetails = (context, problem, exception) =>
    ///         {
    ///             problem.Extensions["correlationId"] = context.TraceIdentifier;
    ///             problem.Extensions["timestamp"] = DateTimeOffset.UtcNow;
    ///             problem.Extensions["machineName"] = Environment.MachineName;
    ///         };
    ///     });
    ///
    ///     // Complete example with all features
    ///     builder.Services.AddExceptionHandler(options =>
    ///     {
    ///         options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    ///         options.EnableLogging = true;
    ///
    ///         // Custom handlers with priority
    ///         options.AddHandler&lt;SecurityExceptionHandler&gt;(priority: 100)
    ///                .AddHandler&lt;DebugExceptionHandler&gt;(
    ///                    when: builder.Environment.IsDevelopment(),
    ///                    priority: 50);
    ///
    ///         // Fluent mappings
    ///         options.Map&lt;NotFoundException&gt;(StatusCodes.Status404NotFound, "Not Found")
    ///                .Map&lt;ConflictException&gt;(StatusCodes.Status409Conflict);
    ///
    ///         // Exception filtering
    ///         options.Ignore&lt;OperationCanceledException&gt;()
    ///                .Rethrow&lt;OutOfMemoryException&gt;();
    ///
    ///         // Enrichment
    ///         options.EnrichProblemDetails = (ctx, problem, ex) =>
    ///         {
    ///             problem.Extensions["traceId"] = ctx.TraceIdentifier;
    ///         };
    ///     });
    ///     </code>
    /// </example>
    public static IServiceCollection AddExceptionHandler(
        this IServiceCollection services,
        Action<GlobalExceptionHandlerOptions> configureOptions)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var options = new GlobalExceptionHandlerOptions();
        configureOptions?.Invoke(options);

        return services.AddExceptionHandler(options);
    }

    /// <summary>
    ///     Adds the global exception handler and built-in exception handlers
    ///     to the service collection with the specified options instance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="options">The <see cref="GlobalExceptionHandlerOptions" /> instance to use.</param>
    /// <returns>The <see cref="IServiceCollection" /> for chaining.</returns>
    /// <remarks>
    ///     <para>
    ///         Exception handlers are processed in registration order. The first handler
    ///         that returns <c>true</c> from <c>TryHandleAsync</c> stops the chain.
    ///     </para>
    ///     <para>
    ///         Registration order:
    ///         <list type="number">
    ///             <item>
    ///                 Additional handlers sorted by priority (highest first)
    ///             </item>
    ///             <item>
    ///                 MappedExceptionHandler (processes fluent mappings)
    ///             </item>
    ///             <item>
    ///                 Built-in handlers (Validation, DomainPolicy, DomainRule, etc.)
    ///             </item>
    ///             <item>
    ///                 GlobalExceptionHandler (catch-all, always last)
    ///             </item>
    ///         </list>
    ///     </para>
    /// </remarks>
    public static IServiceCollection AddExceptionHandler(
        this IServiceCollection services,
        GlobalExceptionHandlerOptions options)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        options ??= new GlobalExceptionHandlerOptions();

        services.AddSingleton(options);

        // Register additional custom handlers sorted by priority (highest first)
        var sortedHandlers = options.AdditionalHandlers
            .OrderByDescending(h => h.Priority);

        foreach (var registration in sortedHandlers)
        {
            services.AddExceptionHandler(registration.HandlerType);
        }

        // Register mapped exception handler (processes fluent mappings)
        if (options.Mappings.Count > 0)
        {
            services.AddExceptionHandler<MappedExceptionHandler>();
        }

        // Register built-in handlers
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<DomainPolicyExceptionHandler>();
        services.AddExceptionHandler<DomainRuleExceptionHandler>();
        services.AddExceptionHandler<SecurityExceptionHandler>();
        services.AddExceptionHandler<ModuleNotEnabledExceptionHandler>();
        services.AddExceptionHandler<AggregateNotFoundExceptionHandler>();
        services.AddExceptionHandler<EntityNotFoundExceptionHandler>();
        services.AddExceptionHandler<NotImplementedExceptionHandler>();
        services.AddExceptionHandler<HttpRequestExceptionHandler>();

        // Register global catch-all handler last (lowest priority)
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    private static IServiceCollection AddExceptionHandler(
        this IServiceCollection services,
        Type handlerType)
    {
        services.AddTransient(typeof(IExceptionHandler), handlerType);

        return services;
    }
}