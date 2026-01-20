// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Host;

/// <summary>
///     Extension methods for configuring Entity Framework Core exception handlers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds Entity Framework Core exception handlers to the
    ///     <see cref="GlobalExceptionHandlerOptions" />.
    ///     Handlers are registered with appropriate priorities to ensure correct
    ///     exception handling order.
    /// </summary>
    /// <param name="options">The <see cref="GlobalExceptionHandlerOptions" /> to configure.</param>
    /// <returns>The <see cref="GlobalExceptionHandlerOptions" /> for chaining.</returns>
    /// <remarks>
    ///     <para>
    ///         This method registers the following handlers with descending priority:
    ///         <list type="bullet">
    ///             <item>
    ///                 <see cref="DbUpdateConcurrencyExceptionHandler" /> (priority: -100)
    ///             </item>
    ///             <item>
    ///                 <see cref="DbUpdateExceptionHandler" /> (priority: -101)
    ///             </item>
    ///             <item>
    ///                 <see cref="DbExceptionHandler" /> (priority: -102)
    ///             </item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         The negative priorities ensure these handlers run after application-level
    ///         handlers but before the global catch-all handler.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///     builder.Services.AddExceptionHandler(options =>
    ///     {
    ///         options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    ///         options.EnableLogging = true;
    ///         options.UseEntityFramework(); // Add EF Core handlers
    ///     });
    ///     </code>
    /// </example>
    public static GlobalExceptionHandlerOptions AddEntityFrameworkHandlers(
        this GlobalExceptionHandlerOptions options)
    {
        EnsureArg.IsNotNull(options, nameof(options));

        // Register with negative priorities to run after custom handlers
        // but before the global catch-all. Order matters for inheritance chain.
        options.AddHandler<DbUpdateConcurrencyExceptionHandler>(priority: -100);
        options.AddHandler<DbUpdateExceptionHandler>(priority: -101);
        options.AddHandler<DbExceptionHandler>(priority: -102);

        return options;
    }

    /// <summary>
    ///     Adds Entity Framework Core exception handlers to the
    ///     <see cref="GlobalExceptionHandlerOptions" /> conditionally.
    /// </summary>
    /// <param name="options">The <see cref="GlobalExceptionHandlerOptions" /> to configure.</param>
    /// <param name="when">The condition that must be true for the handlers to be registered.</param>
    /// <returns>The <see cref="GlobalExceptionHandlerOptions" /> for chaining.</returns>
    /// <example>
    ///     <code>
    ///     builder.Services.AddExceptionHandler(options =>
    ///     {
    ///         options.UseEntityFramework(when: useDatabase);
    ///     });
    ///     </code>
    /// </example>
    public static GlobalExceptionHandlerOptions AddEntityFrameworkHandlers(this GlobalExceptionHandlerOptions options, bool when)
    {
        if (when)
        {
            options.AddEntityFrameworkHandlers();
        }

        return options;
    }
}