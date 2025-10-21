// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server;

using BridgingIT.DevKit.Presentation.Web;
//using BridgingIT.DevKit.Presentation.Web.OpenApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

public static class ProgramExtensions
{
    public static IServiceCollection AddAppOpenApi(this IServiceCollection services)
    {
        // ===============================================================================================
        // Configure OpenAPI generation (openapi.json)
        //builder.Services.AddEndpointsApiExplorer();
        return services.AddOpenApi(o =>
        {
            o.AddDocumentTransformer<DiagnosticDocumentTransformer>()
             .AddOperationTransformer<OperationNameToSummaryTransformer>()
             //.AddOperationTransformer<OperationSummaryDocumentTransformer>()
             //.AddOperationTransformer<DeprecatedOperationTransformer>()
             .AddSchemaTransformer<DiagnosticSchemaTransformer>()
             .AddSchemaTransformer<ResultProblemDetailsSchemaTransformer>()
             .AddSchemaTransformer<FilterModelSchemaTransformer>()
             .AddDocumentTransformer<FakeIdentityProviderDocumentTransformer>();
        });
    }

    public static IServiceCollection AddAppCors(this IServiceCollection services) // TODO: not needed for pure APIs
    {
        // ===============================================================================================
        // Configure CORS
        return services.AddCors(o =>
        {
            o.AddDefaultPolicy(p =>
            {
                p.WithOrigins()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }
}

public class FakeIdentityProviderDocumentTransformer : IOpenApiDocumentTransformer
{
    // https://stackoverflow.com/questions/79443341/how-do-i-configure-scalar-to-authenticate-through-entra
    // https://github.com/scalar/scalar/blob/main/documentation/integrations/aspnetcore/integration.md#authentication
    // https://vitorafgomes.medium.com/how-to-build-a-minimal-api-with-scalar-and-keycloak-authentication-301fde490e40

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[OpenAPI] Adding Security Scheme");
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes.Add(JwtBearerDefaults.AuthenticationScheme,
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
            });

        // Provide a security requirement for all operations (preselected default security scheme)
        document.SecurityRequirements.Add(
            new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = JwtBearerDefaults.AuthenticationScheme
                        }
                    },
                    [] //"api://<client-id>/data.read"
                }
            });

        return Task.CompletedTask;
    }
}