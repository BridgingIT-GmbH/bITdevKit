// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;
using System.Collections.Concurrent;

/// <summary>
/// An OpenAPI schema transformer that configures Result problem detail schemas.
/// </summary>
/// <remarks>
/// <para>
/// This transformer modifies the OpenAPI schema for <see cref="ResultProblemDetails"/>,
/// <see cref="ResultProblemData"/>, <see cref="ResultProblemError"/>, and <see cref="ProblemError"/>
/// to enhance their documentation and explicitly allow arbitrary additional properties.
/// </para>
/// <para>
/// For each matching schema, this transformer:
/// <list type="bullet">
/// <item>Adds comprehensive descriptions to all objects and their properties</item>
/// <item>Sets the schema type to "object"</item>
/// <item>Sets <c>additionalPropertiesAllowed</c> to <c>true</c></item>
/// <item>Generates <c>"additionalProperties": {}</c> in the OpenAPI JSON for RFC 7807 compliance</item>
/// </list>
/// </para>
/// <para>
/// This is essential for RFC 7807 problem details that include extension data and custom error information.
/// Each distinct schema type is logged only once during the build-time document generation process.
/// </para>
/// </remarks>
public class ResultProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Thread-safe collection for tracking distinct schema types that have been adjusted.
    /// </summary>
    private static readonly ConcurrentDictionary<string, bool> AdjustedSchemas = new();

    /// <summary>
    /// Transforms OpenAPI schemas for Result problem detail types.
    /// </summary>
    /// <param name="schema">The schema being transformed.</param>
    /// <param name="context">Context information about the schema transformation, including the type being processed.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A completed task representing the transformation operation.</returns>
    /// <remarks>
    /// Dispatches to specific transformation methods based on the type being transformed.
    /// </remarks>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if (type == typeof(ResultProblemDetails))
        {
            TransformResultProblemDetailsSchema(schema);
        }
        else if (type == typeof(ResultProblemData))
        {
            TransformResultProblemDataSchema(schema);
        }
        else if (type == typeof(ResultProblemResult))
        {
            TransformResultProblemResultSchema(schema);
        }
        else if (type == typeof(ResultProblemError))
        {
            TransformResultProblemErrorSchema(schema);
        }
        else if (type == typeof(ProblemError))
        {
            TransformProblemErrorSchema(schema);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Transforms the ResultProblemDetails schema.
    /// </summary>
    /// <param name="schema">The ResultProblemDetails schema to transform.</param>
    /// <remarks>
    /// Adds description to the schema and configures the data property reference.
    /// </remarks>
    private void TransformResultProblemDetailsSchema(OpenApiSchema schema)
    {
        schema.Type = "object";
        schema.AdditionalPropertiesAllowed = true;
        schema.AdditionalProperties = new OpenApiSchema();
        schema.Description = "A Problem Details response containing result information and contextual data (RFC 7807)";

        if (schema.Properties != null)
        {
            // Add descriptions to inherited ProblemDetails properties
            if (schema.Properties.TryGetValue("type", out var typeSchema))
            {
                typeSchema.Description = "A URI reference that identifies the problem type";
            }

            if (schema.Properties.TryGetValue("title", out var titleSchema))
            {
                titleSchema.Description = "A short, human-readable summary of the problem";
            }

            if (schema.Properties.TryGetValue("status", out var statusSchema))
            {
                statusSchema.Description = "The HTTP status code";
            }

            if (schema.Properties.TryGetValue("detail", out var detailSchema))
            {
                detailSchema.Description = "A human-readable explanation specific to this occurrence";
            }

            if (schema.Properties.TryGetValue("instance", out var instanceSchema))
            {
                instanceSchema.Description = "A URI reference that identifies the specific occurrence of the problem";
            }

            // Add description to the custom data property
            if (schema.Properties.TryGetValue("data", out var dataSchema))
            {
                dataSchema.Description = "Additional contextual data attached to this problem";
            }
        }

        LogAdjustment(nameof(ResultProblemDetails));
    }

    /// <summary>
    /// Transforms the ResultProblemData schema.
    /// </summary>
    /// <param name="schema">The ResultProblemData schema to transform.</param>
    /// <remarks>
    /// Adds descriptions to the result and errors properties.
    /// </remarks>
    private void TransformResultProblemDataSchema(OpenApiSchema schema)
    {
        schema.Type = "object";
        schema.AdditionalPropertiesAllowed = true;
        schema.AdditionalProperties = new OpenApiSchema();
        schema.Description = "Data section included within a problem details response";

        if (schema.Properties != null)
        {
            if (schema.Properties.TryGetValue("result", out var resultSchema))
            {
                resultSchema.Description = "The serialized operation result containing outcome and diagnostic information";
            }

            if (schema.Properties.TryGetValue("errors", out var errorsSchema))
            {
                errorsSchema.Description = "Collection of additional errors outside the result object";
            }
        }

        LogAdjustment(nameof(ResultProblemData));
    }

    /// <summary>
    /// Transforms the ResultProblemResult schema.
    /// </summary>
    /// <param name="schema">The ResultProblemResult schema to transform.</param>
    /// <remarks>
    /// Adds descriptions to the operation outcome and diagnostic properties.
    /// </remarks>
    private void TransformResultProblemResultSchema(OpenApiSchema schema)
    {
        schema.Type = "object";
        schema.AdditionalPropertiesAllowed = true;
        schema.AdditionalProperties = new OpenApiSchema();
        schema.Description = "Represents the operation outcome with success status, messages, and errors";

        if (schema.Properties != null)
        {
            if (schema.Properties.TryGetValue("isSuccess", out var isSuccessSchema))
            {
                isSuccessSchema.Description = "Indicates whether the operation completed successfully";
            }

            if (schema.Properties.TryGetValue("messages", out var messagesSchema))
            {
                messagesSchema.Description = "Collection of informational or diagnostic messages from the operation";
            }

            if (schema.Properties.TryGetValue("errors", out var errorsSchema))
            {
                errorsSchema.Description = "Collection of domain or technical errors encountered during the operation";
            }
        }

        LogAdjustment(nameof(ResultProblemResult));
    }

    /// <summary>
    /// Transforms the ResultProblemError schema.
    /// </summary>
    /// <param name="schema">The ResultProblemError schema to transform.</param>
    /// <remarks>
    /// Adds description to individual error entries within a result.
    /// </remarks>
    private void TransformResultProblemErrorSchema(OpenApiSchema schema)
    {
        schema.Type = "object";
        schema.AdditionalPropertiesAllowed = true;
        schema.AdditionalProperties = new OpenApiSchema();
        schema.Description = "Represents an individual error entry with a message and optional metadata";

        if (schema.Properties != null)
        {
            if (schema.Properties.TryGetValue("message", out var messageSchema))
            {
                messageSchema.Description = "Human-readable error message";
            }
        }

        LogAdjustment(nameof(ResultProblemError));
    }

    /// <summary>
    /// Transforms the ProblemError schema.
    /// </summary>
    /// <param name="schema">The ProblemError schema to transform.</param>
    /// <remarks>
    /// Adds description for additional error entries outside the result object.
    /// </remarks>
    private void TransformProblemErrorSchema(OpenApiSchema schema)
    {
        schema.Type = "object";
        schema.AdditionalPropertiesAllowed = true;
        schema.AdditionalProperties = new OpenApiSchema();
        schema.Description = "Represents an individual error entry with flexible structure for additional data";

        LogAdjustment(nameof(ProblemError));
    }

    /// <summary>
    /// Logs that a schema has been adjusted, but only logs each distinct type once.
    /// </summary>
    /// <param name="typeName">The name of the type that was adjusted.</param>
    private void LogAdjustment(string typeName)
    {
        if (AdjustedSchemas.TryAdd(typeName, true))
        {
            Console.WriteLine($"[OpenAPI] Schema {typeName} adjusted with descriptions and additionalProperties");
        }
    }
}