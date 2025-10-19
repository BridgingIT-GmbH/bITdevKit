// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;
using System.Collections.Concurrent;

/// <summary>
/// An OpenAPI schema transformer that configures additional properties for Result problem detail schemas.
/// </summary>
/// <remarks>
/// This transformer modifies the OpenAPI schema for <see cref="ResultProblemDetails"/>,
/// <see cref="ResultProblemData"/>, <see cref="ResultProblemError"/>, and <see cref="ProblemError"/>
/// to explicitly allow arbitrary additional properties at the same level as known properties.
///
/// For each matching schema, this transformer:
/// - Sets the schema type to "object".
/// - Sets <c>additionalPropertiesAllowed</c> to <c>true</c>.
/// - Generates <c>"additionalProperties": {}</c> in the OpenAPI JSON, which allows any additional
///   properties of any type to be included alongside defined properties.
///
/// This is essential for RFC 7807 problem details that include extension data and custom error information.
/// Each distinct schema type is logged only once, even if the transformer is called multiple times
/// during the build-time document generation process.
/// </remarks>
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