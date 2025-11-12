// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics.CodeAnalysis;
using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Extension methods for configuring CORS services.
/// </summary>
[ExcludeFromCodeCoverage]
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure CORS policies from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Reads CORS configuration from the "Cors" section in appsettings.json.
    /// Supports multiple named policies and an optional default policy.
    /// Validates configuration and throws <see cref="InvalidOperationException"/> for invalid setups.
    /// <para>
    /// When Cors:Enabled is false, no CORS services are registered and cross-origin requests will be blocked.
    /// </para>
    /// <para>
    /// Example usage in Program.cs:
    /// <code>
    /// builder.Services.AddCors(builder.Configuration);
    /// </code>
    /// </para>
    /// See: https://learn.microsoft.com/en-us/aspnet/core/security/cors
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when configuration is invalid (e.g., AllowAnyOrigin + AllowCredentials, no policies defined, invalid DefaultPolicy reference).
    /// </exception>
    public static IServiceCollection AddCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsConfig = configuration.GetSection("Cors").Get<CorsConfiguration>();
        if (corsConfig?.Enabled != true)
        {
            return services; // CORS disabled, skip registration
        }

        // Validate configuration
        if (corsConfig.Policies == null || corsConfig.Policies.Count == 0)
        {
            throw new InvalidOperationException(
                "CORS is enabled but no policies are defined. Add at least one policy in Cors:Policies configuration.");
        }

        if (!string.IsNullOrWhiteSpace(corsConfig.DefaultPolicy) &&
            !corsConfig.Policies.ContainsKey(corsConfig.DefaultPolicy))
        {
            throw new InvalidOperationException(
                $"CORS DefaultPolicy '{corsConfig.DefaultPolicy}' is not defined in Cors:Policies configuration.");
        }

        return services.AddCors(options =>
        {
            foreach (var policy in corsConfig.Policies)
            {
                var policyName = policy.Key;
                var policyOptions = policy.Value;

                // Validate policy options
                if (policyOptions.AllowAnyOrigin == true && policyOptions.AllowCredentials == true)
                {
                    throw new InvalidOperationException(
                        $"CORS policy '{policyName}': AllowAnyOrigin and AllowCredentials cannot both be true. " +
                        "This violates the CORS specification. Use specific origins in AllowedOrigins when AllowCredentials is true.");
                }

                options.AddPolicy(policyName, builder =>
                {
                    ConfigurePolicyBuilder(builder, policyOptions);
                });
            }

            // Register default policy if specified using ASP.NET Core's default policy name
            // This allows [EnableCors()] without parameters to use the configured default policy
            if (!string.IsNullOrWhiteSpace(corsConfig.DefaultPolicy))
            {
                var defaultPolicyOptions = corsConfig.Policies[corsConfig.DefaultPolicy];

                options.AddPolicy("__DefaultCorsPolicy", builder =>
                {
                    ConfigurePolicyBuilder(builder, defaultPolicyOptions);
                });

                options.AddDefaultPolicy(builder =>
                {
                    ConfigurePolicyBuilder(builder, defaultPolicyOptions);
                });
            }
        });
    }

    /// <summary>
    /// Configures a CORS policy builder with the specified options.
    /// </summary>
    /// <param name="builder">The CORS policy builder.</param>
    /// <param name="policyOptions">The policy options to apply.</param>
    private static void ConfigurePolicyBuilder(
        Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder builder,
        CorsPolicyOptions policyOptions)
    {
        // Configure origins
        if (policyOptions.AllowAnyOrigin == true)
        {
            builder.AllowAnyOrigin();
        }
        else if (policyOptions.AllowedOrigins?.Length > 0)
        {
            builder.WithOrigins(policyOptions.AllowedOrigins);

            if (policyOptions.AllowWildcardSubdomains == true)
            {
                builder.SetIsOriginAllowedToAllowWildcardSubdomains();
            }
        }

        // Configure methods
        if (policyOptions.AllowAnyMethod == true)
        {
            builder.AllowAnyMethod();
        }
        else if (policyOptions.AllowedMethods?.Length > 0)
        {
            builder.WithMethods(policyOptions.AllowedMethods);
        }

        // Configure headers
        if (policyOptions.AllowAnyHeader == true)
        {
            builder.AllowAnyHeader();
        }
        else if (policyOptions.AllowedHeaders?.Length > 0)
        {
            builder.WithHeaders(policyOptions.AllowedHeaders);
        }

        // Configure credentials
        if (policyOptions.AllowCredentials == true)
        {
            builder.AllowCredentials();
        }

        // Configure exposed headers
        if (policyOptions.ExposeHeaders?.Length > 0)
        {
            builder.WithExposedHeaders(policyOptions.ExposeHeaders);
        }

        // Configure preflight max age
        if (policyOptions.PreflightMaxAgeSeconds.HasValue)
        {
            builder.SetPreflightMaxAge(TimeSpan.FromSeconds(policyOptions.PreflightMaxAgeSeconds.Value));
        }
    }
}
