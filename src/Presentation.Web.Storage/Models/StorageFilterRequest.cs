// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

using System.Text.Json;
using System.Text.Json.Nodes;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;

/// <summary>Requests a metadata page using key and Dimension filters.</summary>
/// <example><code>var request = new StorageFilterRequest { PageSize = 25 };</code></example>
public sealed class StorageFilterRequest : FilterModel
{
    private static readonly JsonSerializerOptions FilterJson = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new EnumMemberConverter<FilterOperator>(),
            new EnumMemberConverter<FilterLogicOperator>(),
            new EnumMemberConverter<FilterCustomType>(),
            new EnumMemberConverter<OrderDirection>()
        }
    };

    /// <summary>Gets the token returned by the preceding page of the same query.</summary>
    public string ContinuationToken { get; init; }

    /// <summary>Reads a filter model with the shared enum aliases and uncoerced JSON operands.</summary>
    public static async Task<StorageFilterRequest> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await request.ReadFromJsonAsync<StorageFilterRequest>(FilterJson, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Describes the storage-specific additions to the shared filter schema.</summary>
public static class StorageFilterSchema
{
    /// <summary>Adds cursor paging and typed Dimension operands to an endpoint's request schema.</summary>
    public static RouteHandlerBuilder WithStorageFilterSchema(this RouteHandlerBuilder endpoint, string keyFields) =>
        endpoint.WithFilterSchema(isRequestBody: true)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                var root = (OpenApiSchema)operation.RequestBody.Content["application/json"].Schema;
                root.Properties.Remove("includes");
                root.Properties.Remove("hierarchy");
                root.Properties.Remove("hierarchyMaxDepth");
                root.Properties["page"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Enum = [JsonValue.Create(0), JsonValue.Create(1)] };
                root.Properties["pageSize"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Integer,
                    Description = "Omit or use 0 for the client default. Explicit sizes are limited by the client's maximum and 1000."
                };
                root.Properties["continuationToken"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String | JsonSchemaType.Null,
                    Description = "Repeat the same query with the token from the previous page."
                };
                var criteria = (OpenApiSchema)((OpenApiSchema)root.Properties["filters"]).Items;
                criteria.Description = "Use a field for a comparison or nested filters for a group. Pick eq, neq, or startswith for supported keys. Dimensions support typed comparisons and isnull or isnotnull.";
                foreach (var property in new[] { "customType", "customParameters", "specificationName", "specificationArguments", "compositeSpecification" })
                    criteria.Properties.Remove(property);
                criteria.Properties["field"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = keyFields + " and Dimensions.<name>." };
                criteria.Properties["operator"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. new[] { "eq", "neq", "startswith", "gt", "gte", "lt", "lte", "isnull", "isnotnull" }.Select(value => JsonValue.Create(value))]
                };
                criteria.Properties["value"] = new OpenApiSchema
                {
                    OneOf =
                    [
                        new OpenApiSchema { Type = JsonSchemaType.String, Description = "Logical storage key." },
                        new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Description = "Typed Dimension operand. Omit the operand for null checks.",
                            Required = new HashSet<string> { "type", "value" },
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["type"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "integer, decimal, boolean, string, or datetime" },
                                ["value"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Number | JsonSchemaType.Boolean }
                            }
                        }
                    ]
                };
                return Task.CompletedTask;
            });
}
