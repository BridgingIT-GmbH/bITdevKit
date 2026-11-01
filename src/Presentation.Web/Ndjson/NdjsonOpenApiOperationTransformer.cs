// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Text.Json.Nodes;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>Describes NDJSON as streamed text with separate per-record schema metadata.</summary>
/// <example><code>options.AddOperationTransformer&lt;NdjsonOpenApiOperationTransformer&gt;();</code></example>
public sealed class NdjsonOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>Applies the endpoint's immutable declared mode and record model.</summary>
    /// <example><code>await transformer.TransformAsync(operation, context, ct);</code></example>
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var descriptor = context.Description.ActionDescriptor.EndpointMetadata.OfType<NdjsonEndpointDescriptor>().LastOrDefault();
        if (descriptor is null) { return; }

        var dataSchema = await context.GetOrCreateSchemaAsync(descriptor.RecordType, cancellationToken: cancellationToken).ConfigureAwait(false);
        IOpenApiSchema schema = descriptor.Mode == NdjsonMode.Envelope ? EnvelopeSchema(dataSchema) : dataSchema;
        var schemaText = await schema.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1, cancellationToken).ConfigureAwait(false);
        operation.Responses ??= new OpenApiResponses();
        var response = new OpenApiResponse
        {
            Description = "Incremental LF-delimited UTF-8 JSON values. HTTP success means admission. Raw failure aborts; envelope failure uses a safe terminal error or aborts. Generated JSON-array clients do not parse this stream.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [NdjsonDefaults.CONTENT_TYPE] = new()
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Description = "NDJSON text sequence; see x-ndjson-record-schema for one record." },
                    Example = JsonValue.Create(descriptor.Mode == NdjsonMode.Envelope
                        ? "{\"version\":1,\"kind\":\"heartbeat\"}\n{\"version\":1,\"kind\":\"completed\",\"recordCount\":0}\n"
                        : "{\"example\":1}\n{\"example\":2}\n"),
                    Extensions = new Dictionary<string, IOpenApiExtension> { ["x-ndjson-record-schema"] = new JsonNodeExtension(JsonNode.Parse(schemaText)) }
                }
            },
            Headers = new Dictionary<string, IOpenApiHeader>
            {
                ["X-Ndjson-Mode"] = new OpenApiHeader { Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String }, Example = JsonValue.Create(descriptor.Mode == NdjsonMode.Raw ? "raw" : "envelope") }
            }
        };
        if (descriptor.Mode == NdjsonMode.Envelope)
        {
            response.Headers["X-Ndjson-Envelope-Version"] = new OpenApiHeader { Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String }, Example = JsonValue.Create("1") };
        }

        operation.Responses["200"] = response;
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions["x-ndjson-mode"] = new JsonNodeExtension(JsonValue.Create(descriptor.Mode == NdjsonMode.Raw ? "raw" : "envelope"));
    }

    private static OpenApiSchema EnvelopeSchema(IOpenApiSchema dataSchema)
    {
        var error = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string> { "code", "message" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["code"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["message"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };
        return new OpenApiSchema
        {
            OneOf =
            [
                Kind("heartbeat"),
                Kind("data", "data", new OpenApiSchema { AnyOf = [dataSchema, new OpenApiSchema { Type = JsonSchemaType.Null }] }),
                Kind("completed", "recordCount", new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64", Description = "Non-negative count of data frames in this response." }),
                Kind("error", "error", error)
            ]
        };

        static OpenApiSchema Kind(string name, string field = null, IOpenApiSchema fieldSchema = null)
        {
            var shape = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Required = new HashSet<string> { "version", "kind" },
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["version"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Enum = [JsonValue.Create(1)] },
                    ["kind"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [JsonValue.Create(name)] }
                }
            };
            if (field is not null)
            {
                shape.Required.Add(field);
                shape.Properties[field] = fieldSchema;
            }

            return shape;
        }
    }
}
