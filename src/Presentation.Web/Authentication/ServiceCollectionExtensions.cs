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

public static partial class ServiceCollectionExtensions
{
    public static AspNetCore.Authentication.AuthenticationBuilder AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuthenticationOptions>(configuration.GetSection("Authentication"));
        var authOptions = new AuthenticationOptions();
        configuration.GetSection("Authentication")?.Bind(authOptions);

        return services.AddJwtAuthentication(authOptions);
    }

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

                logger.LogInformation("JwtAuthentication - Token validated successfully for user: {Name}", context.Principal?.Identity?.Name);
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