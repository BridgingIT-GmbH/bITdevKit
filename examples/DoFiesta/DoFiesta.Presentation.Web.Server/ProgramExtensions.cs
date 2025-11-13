// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server;

using BridgingIT.DevKit.Presentation.Web;
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
             .AddDocumentTransformer(
                new DocumentInfoTransformer(new DocumentInfoOptions
                {
                    Title = "DoFiesta API",
                    Description = "API for DoFiesta application.",
                }))
             //.AddOperationTransformer<OperationSummaryDocumentTransformer>()
             //.AddOperationTransformer<DeprecatedOperationTransformer>()
             .AddSchemaTransformer<DiagnosticSchemaTransformer>()
             .AddSchemaTransformer<ResultProblemDetailsSchemaTransformer>()
             .AddSchemaTransformer<FilterModelSchemaTransformer>()
             .AddDocumentTransformer<BearerSecurityRequirementDocumentTransformer>();
        });
    }

    public static IServiceCollection AddAppCors(this IServiceCollection services) // TODO: not needed for pure APIs
    {
        // ===============================================================================================
        // Configure CORS
        return services.AddCors(o => // TODO: use the new configuration based CORS. see: features-presentation-cors.md
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