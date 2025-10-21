// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
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

    /// <summary>
    /// Adds cookie-based authentication to the authentication builder with sensible defaults.
    /// </summary>
    /// <param name="builder">The authentication builder to add cookie authentication to.</param>
    /// <param name="configureOptions">
    /// Optional custom configuration for cookie authentication options.
    /// If not provided, a default configuration is applied.
    /// </param>
    /// <returns>
    /// The authentication builder for method chaining and further configuration.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This extension method adds ASP.NET Core cookie authentication with secure defaults.
    /// It is particularly useful when implementing persistent refresh token functionality,
    /// which requires signing in users with a cookie containing the refresh token.
    /// </para>
    /// <para>
    /// Default configuration applied when no custom options are provided:
    /// <list type="bullet">
    /// <item>Cookie name: ".AspNetCore.Identity"</item>
    /// <item>HttpOnly: true (prevents JavaScript access, protecting against XSS attacks)</item>
    /// <item>SecurePolicy: Always (enforces HTTPS transmission)</item>
    /// <item>Expiration: 30 days (supports "remember me" functionality)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Security considerations:
    /// <list type="bullet">
    /// <item>HttpOnly flag prevents client-side script access to the cookie</item>
    /// <item>SecurePolicy ensures cookies are only transmitted over HTTPS</item>
    /// <item>SameSite policy should be configured at the application level to prevent CSRF attacks</item>
    /// <item>The 30-day expiration assumes a refresh token rotation strategy</item>
    /// </list>
    /// </para>
    /// <para>
    /// Usage examples:
    /// </para>
    /// <para>
    /// Using default configuration:
    /// <code>
    /// services
    ///     .AddJwtAuthentication(/* ... */)
    ///     .AddCookieAuthentication();
    /// </code>
    /// </para>
    /// <para>
    /// Using custom configuration:
    /// <code>
    /// services
    ///     .AddJwtAuthentication(/* ... */)
    ///     .AddCookieAuthentication(options =>
    ///     {
    ///         options.Cookie.Name = ".MyApp.Auth";
    ///         options.ExpireTimeSpan = TimeSpan.FromDays(7);
    ///         options.LoginPath = "/login";
    ///         options.LogoutPath = "/logout";
    ///     });
    /// </code>
    /// </para>
    /// </remarks>
    public static AspNetCore.Authentication.AuthenticationBuilder AddCookieAuthentication(this AspNetCore.Authentication.AuthenticationBuilder builder, Action<CookieAuthenticationOptions> configureOptions = null)
    {
        configureOptions ??= options => // needed for EnablePersistentRefreshTokens which signs in users with a cookie containing the refresh-token
        {
            options.Cookie.Name = ".AspNetCore.Identity"; //.{HashHelper.Compute("authOptions.Authority")}
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(30); // For "remember me"
        };

        return builder.AddCookie(configureOptions);
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