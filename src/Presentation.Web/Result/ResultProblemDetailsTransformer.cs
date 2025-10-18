// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using BridgingIT.DevKit.Presentation.Web; // adjust if your types live elsewhere
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;
using System.Collections.Concurrent;

public class ResultProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    // Thread-safe collection for distinct type names logged
    private static readonly ConcurrentDictionary<string, bool> AdjustedSchemas = new();

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(ProblemError) ||
            type == typeof(ResultProblemError) ||
            type == typeof(ResultProblemData) ||
            type == typeof(ResultProblemDetails))
        {
            var typeName = type.FullName ?? "(unknown)";

            // Your logic to force additionalProperties: {}
            schema.Type = "object"; // set as object (optional, as in your base code)
            schema.AdditionalPropertiesAllowed = true; // set the flag
            schema.AdditionalProperties = new OpenApiSchema { }; // force "additionalProperties": {} in JSON

            // Log post-transformation
            //Console.WriteLine($"[OpenAPI] Transformed '{typeName}' (post): type={schema.Type ?? "(none)"}, additionalPropertiesAllowed={schema.AdditionalPropertiesAllowed}");

            // Log "adjusted" only if this type is new (distinct)
            if (AdjustedSchemas.TryAdd(typeName, true))
            {
                Console.WriteLine($"[OpenAPI] Schema {typeName} adjusted with additionalProperties: {{}}");
            }
        }

        return Task.CompletedTask;
    }
}

public class DiagnosticDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[OpenAPI] Processing document '{context.DocumentName}'");
        Console.WriteLine($"[OpenAPI] Title: {document.Info?.Title}");
        Console.WriteLine($"[OpenAPI] Version: {document.Info?.Version}");

        // Log paths
        if (document.Paths?.Count > 0)
        {
            Console.WriteLine($"[OpenAPI] Paths ({document.Paths.Count}):");
            foreach (var path in document.Paths)
            {
                var pathKey = path.Key;
                var pathItem = path.Value;

                var operations = new List<string>();

                if (pathItem.Operations != null)
                {
                    foreach (var operation in pathItem.Operations)
                    {
                        operations.Add(operation.Key.ToString().ToUpper());
                    }
                }

                var operationList = operations.Count > 0 ? string.Join(", ", operations) : "NONE";
                Console.WriteLine($"[OpenAPI] - {pathKey} [{operationList}]");
            }
        }
        else
        {
            Console.WriteLine("[OpenAPI] Paths: (none)");
        }

        // Log schemas
        if (document.Components?.Schemas?.Count > 0)
        {
            Console.WriteLine($"[OpenAPI] Schemas ({document.Components.Schemas.Count}):");
            foreach (var schemaKey in document.Components.Schemas.Keys)
            {
                Console.WriteLine($"[OpenAPI]   - {schemaKey}");
            }
        }
        //else
        //{
        //    Console.WriteLine("[OpenAPI] Schemas: (none)");
        //}

        Console.WriteLine("[OpenAPI] Document processing complete");

        return Task.CompletedTask;
    }
}

public class DiagnosticSchemaTransformer : IOpenApiSchemaTransformer
{
    // Thread-safe collection for distinct schema types logged
    private static readonly ConcurrentDictionary<string, bool> LoggedSchemas = new();

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var typeName = context.JsonTypeInfo.Type?.FullName ?? "(unknown)";

        // Skip System.* types
        if (typeName.StartsWith("System."))
        {
            return Task.CompletedTask;
        }

        // Log schema processing only once per type (distinct)
        if (LoggedSchemas.TryAdd(typeName, true))
        {
            var schemaType = schema.Type ?? "(none)";
            var hasProperties = schema.Properties?.Count > 0 ? schema.Properties.Count : 0;
            var isNullable = schema.Nullable ? "nullable" : "non-nullable";

            Console.WriteLine($"[OpenAPI] Schema {typeName}, Type: {schemaType}, Properties: {hasProperties}, {isNullable}");
        }

        return Task.CompletedTask;
    }
}