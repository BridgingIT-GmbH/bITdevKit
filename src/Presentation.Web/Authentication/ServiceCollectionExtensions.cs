// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Extension methods for adding JWT Bearer authentication to the dependency injection container.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds JWT Bearer authentication to the service collection using configuration from the application configuration.
    /// </summary>
    /// <param name="services">The service collection to add authentication to.</param>
    /// <param name="configuration">The application configuration containing the "Authentication" section with JWT settings.</param>
    /// <returns>An <see cref="AspNetCore.Authentication.AuthenticationBuilder"/> for further authentication configuration.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the "Authentication" section is missing or when required settings like Authority are not configured.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method reads JWT authentication settings from the application configuration under the "Authentication" section.
    /// It then configures the authentication pipeline with JWT Bearer token validation.
    /// </para>
    /// <para>
    /// Expected configuration structure:
    /// <code>
    /// {
    ///   "Authentication": {
    ///     "Authority": "https://your-auth-server.com",
    ///     "RequireHttpsMetadata": true,
    ///     "SaveToken": true,
    ///     "ValidateIssuer": true,
    ///     "ValidateAudience": true,
    ///     "ValidateLifetime": true,
    ///     "ValidateSigningKey": true,
    ///     "ValidIssuer": "your-issuer",
    ///     "ValidAudience": "your-audience"
    ///   }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static AspNetCore.Authentication.AuthenticationBuilder AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuthenticationOptions>(configuration.GetSection("Authentication"));
        var authOptions = new AuthenticationOptions();
        configuration.GetSection("Authentication")?.Bind(authOptions);

        return services.AddJwtAuthentication(authOptions);
    }

    /// <summary>
    /// Adds JWT Bearer authentication to the service collection using the provided authentication options.
    /// </summary>
    /// <param name="services">The service collection to add authentication to.</param>
    /// <param name="authOptions">The authentication options containing JWT configuration settings.</param>
    /// <returns>An <see cref="AspNetCore.Authentication.AuthenticationBuilder"/> for further authentication configuration.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="authOptions"/> is null or when the Authority property is not set.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method configures the ASP.NET Core authentication pipeline with JWT Bearer token validation.
    /// It sets up all authentication schemes to use JWT Bearer by default and configures token validation parameters.
    /// </para>
    /// <para>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Token issuer validation</item>
    /// <item>Audience validation</item>
    /// <item>Token lifetime validation with 5-minute clock skew tolerance</item>
    /// <item>Signing key validation</item>
    /// <item>Event logging for authentication operations</item>
    /// </list>
    /// </para>
    /// <para>
    /// The method also configures event handlers that log authentication events for debugging and monitoring:
    /// <list type="bullet">
    /// <item>OnAuthenticationFailed - Logs when token validation fails</item>
    /// <item>OnTokenValidated - Logs successful token validation</item>
    /// <item>OnChallenge - Logs authentication challenges (e.g., missing/invalid credentials)</item>
    /// <item>OnMessageReceived - Logs when tokens are received</item>
    /// </list>
    /// </para>
    /// </remarks>
    public static AspNetCore.Authentication.AuthenticationBuilder AddJwtAuthentication(this IServiceCollection services, AuthenticationOptions authOptions)
    {
        if (authOptions == null)
        {
            throw new ArgumentException("Authentication options are required to configure JWT authentication.");
        }

        if (authOptions.Authority.IsNullOrEmpty())
        {
            throw new ArgumentException("Authentication option Authority is required to configure JWT authentication.");
        }

        return services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultSignOutScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
            //options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            //options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            //options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            //options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            //options.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            //options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            SetOptions(options, authOptions);
            SetEventLogging(options);
        });
    }

    private static void SetOptions(JwtBearerOptions options, AuthenticationOptions authOptions)
    {
        options.Authority = authOptions.Authority;
        options.RequireHttpsMetadata = authOptions.RequireHttpsMetadata;
        options.SaveToken = authOptions.SaveToken;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = authOptions.ValidateIssuer,
            ValidIssuer = authOptions.ValidIssuer,
            ValidateAudience = authOptions.ValidateAudience,
            ValidAudience = authOptions.ValidAudience,
            ValidateLifetime = authOptions.ValidateLifetime,
            ClockSkew = TimeSpan.FromMinutes(5), //authOptions.ClockSkew,
            ValidateIssuerSigningKey = authOptions.ValidateSigningKey,
            IssuerSigningKey = !authOptions.SigningKey.IsNullOrEmpty() ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey)) : null,
            RequireSignedTokens = authOptions.RequireSignedTokens,
        };
    }

    private static void SetEventLogging(JwtBearerOptions options)
    {
        options.Events = new JwtBearerEvents // Add event handlers for JWT Bearer authentication
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerHandler>>();

                logger.LogError("JwtAuthentication - Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },

            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerHandler>>();

                //logger.LogInformation("JwtAuthentication - Token validated successfully for user: {Name}", context.Principal?.Identity?.Name);
                logger.LogInformation("JwtAuthentication - Token validated successfully");
                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerHandler>>();

                logger.LogInformation("JwtAuthentication - Authentication challenge issued for request to {Path}", context.Request.Path);
                return Task.CompletedTask;
            },

            OnMessageReceived = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerHandler>>();

                logger.LogInformation("JwtAuthentication - Token received for request to {Path}", context.HttpContext.Request.Path);
                return Task.CompletedTask;
            }
        };
    }
}