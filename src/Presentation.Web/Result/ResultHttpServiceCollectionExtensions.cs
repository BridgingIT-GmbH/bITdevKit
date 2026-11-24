// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Configures HTTP Problem Details for failed Results.</summary>
public static class ResultHttpServiceCollectionExtensions
{
    /// <summary>Registers request correlation and the optional exception diagnostics for Result responses.</summary>
    /// <param name="services">The service collection for the HTTP host.</param>
    /// <param name="includeExceptionDetails">Whether to expose full exception diagnostics. Enable this only on development hosts you trust.</param>
    /// <returns>The service collection.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddResultHttpProblemDetails(
    ///     includeExceptionDetails: builder.Environment.IsEnvironment("LocalDebug"));
    /// </code>
    /// </example>
    public static IServiceCollection AddResultHttpProblemDetails(
        this IServiceCollection services,
        bool includeExceptionDetails = false)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<ResultHttpProblemDetailsOptions>(options => options.IncludeExceptionDetails = includeExceptionDetails);
        services.AddProblemDetails();
        services.PostConfigure<ProblemDetailsOptions>(options =>
        {
            var customize = options.CustomizeProblemDetails;
            options.CustomizeProblemDetails = context =>
            {
                customize?.Invoke(context);
                if (context.ProblemDetails.Status >= 400)
                {
                    context.ProblemDetails = ResultHttpProblemDetails.PrepareResponse(context.ProblemDetails, context.HttpContext);
                }
            };
        });
        return services;
    }
}

/// <summary>Controls exception disclosure in Result HTTP Problem Details.</summary>
/// <example><code>builder.Services.Configure&lt;ResultHttpProblemDetailsOptions&gt;(options => options.IncludeExceptionDetails = builder.Environment.IsDevelopment());</code></example>
public sealed class ResultHttpProblemDetailsOptions
{
    /// <summary>Gets or sets whether clients receive full exception diagnostics. The default is <see langword="false"/>.</summary>
    public bool IncludeExceptionDetails { get; set; }
}
