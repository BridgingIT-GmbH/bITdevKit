// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server;

using Microsoft.OpenApi.Models;

public static class ProgramExtensions
{
    public static IServiceCollection AddSwagger(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer(); // not needed for AddOpenApi(), only needed for SwaggerGen
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("api", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Version = "v1",
                Title = "Backend API",
            });

            // Define OAuth2 with Authorization Code flow (not Implicit)
            options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"{configuration["Authentication:Authority"]}/api/_system/identity/connect/authorize"),
                        TokenUrl = new Uri($"{configuration["Authentication:Authority"]}/api/_system/identity/connect/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            { "openid", "OpenID Connect scope" },
                            { "profile", "Profile information" },
                            { "email", "Email information" }
                        }
                    }
                }
            });

            // Add the security requirement to all operations
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "oauth2"
                        }
                    },
                    new[] { "openid", "profile", "email" }
                }
            });
        });

        return services;
    }

    public static IApplicationBuilder UseSwagger(this IApplicationBuilder app, string clientId = null)
    {
        app.UseSwagger(options => // https://localhost:5001/openapi/api.json
        {
            options.RouteTemplate = "openapi/{documentName}.json";
        });

        app.UseSwaggerUI(c => // https://localhost:5001/openapi
        {
            c.SwaggerEndpoint("api.json", "Backend API");
            c.RoutePrefix = "openapi";

            c.OAuthClientId(clientId);
            c.OAuthAppName("Backend API - Swagger UI");
            c.OAuthUsePkce();
            c.OAuthScopeSeparator(" ");
        });

        return app;
    }
}
