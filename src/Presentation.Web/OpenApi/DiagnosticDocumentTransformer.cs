// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

/// <summary>
/// An OpenAPI document transformer that logs diagnostic information about the generated OpenAPI document.
/// </summary>
/// <remarks>
/// This transformer runs after the OpenAPI document is fully built and logs:
/// - The document name, title, and version.
/// - All paths (routes) with their supported HTTP methods (GET, POST, PUT, DELETE, etc.).
/// - All component schemas registered in the document.
///
/// This is useful for debugging and understanding what endpoints and types are included
/// in the generated OpenAPI specification during the build-time document generation process.
/// </remarks>
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
