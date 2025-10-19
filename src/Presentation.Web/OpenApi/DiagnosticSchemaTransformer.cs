// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;
using System.Collections.Concurrent;
/// <summary>
/// An OpenAPI schema transformer that logs diagnostic information about each schema processed.
/// </summary>
/// <remarks>
/// This transformer runs as each schema is registered during OpenAPI document generation and logs:
/// - The fully qualified type name of the schema.
/// - The OpenAPI schema type (object, string, integer, array, etc.).
/// - The number of properties defined on the schema.
/// - Whether the schema is nullable or non-nullable.
///
/// System.* types (primitives, collections, generics from the BCL) are filtered out to reduce noise.
/// Each distinct schema type is logged only once, even if the transformer is called multiple times
/// during the build-time document generation process.
///
/// This is useful for understanding what domain and application types are being included
/// in the generated OpenAPI specification and their basic structure.
/// </remarks>
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