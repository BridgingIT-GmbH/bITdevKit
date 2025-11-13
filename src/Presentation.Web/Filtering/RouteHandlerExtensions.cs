// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

public static class RouteHandlerExtensions
{
    /// <summary>
    /// Adds OpenAPI documentation for the FilterModel to the endpoint.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="isRequestBody">If true, documents the filter as a request body. If false, documents it as a query parameter.</param>
    /// <returns>The route handler builder for method chaining.</returns>
    /// <remarks>
    /// Usage examples:
    ///
    /// 1. For GET endpoint with query parameter: filter={"page":1,"pageSize":10,"filters":[{"field":"name","operator":"eq","value":"John"}],"orderings":[{"field":"createdAt","direction":"desc"}]}
    /// <code>
    /// app.MapGet("/api/users/search", async (HttpContext context, ...) =>
    /// {
    ///     var filter = await context.FromQueryFilterAsync();
    ///     // ...
    /// })
    /// .WithName("SearchUsers")
    /// .WithOpenApi(operation =>
    /// {
    ///     operation.Summary = "Search users";
    ///     return operation;
    /// })
    /// .WithFilterSchema();
    /// </code>
    ///
    /// 2. For POST endpoint with request body: content={"page":1,"pageSize":10,"filters":[{"field":"name","operator":"eq","value":"John"}],"orderings":[{"field":"createdAt","direction":"desc"}]}
    /// <code>
    /// app.MapPost("/api/users/search", async (HttpContext context, ...) =>
    /// {
    ///     var filter = await context.FromBodyFilterAsync();
    ///     // ...
    /// })
    /// .WithName("SearchUsersPost")
    /// .WithOpenApi(operation =>
    /// {
    ///     operation.Summary = "Search users";
    ///     return operation;
    /// })
    /// .WithFilterSchema(isRequestBody: true);
    /// </code>
    /// </remarks>
    public static RouteHandlerBuilder WithFilterSchema(this RouteHandlerBuilder builder, bool isRequestBody = false)
    {
        var specificationNodeSchema = new OpenApiSchema
        {
            OneOf =
            [
                new OpenApiSchema()
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["name"] = new OpenApiSchema() { Type = JsonSchemaType.String },
                        ["arguments"] = new OpenApiSchema() { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.Object } }
                    }
                },
                new OpenApiSchema()
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["logic"] = new OpenApiSchema()
                        {
                            Type = JsonSchemaType.String,
                            Enum = [.. Common.EnumExtensions.GetEnumMemberValues<FilterLogicOperator>().Select(v => JsonValue.Create(v))]
                        },
                        ["nodes"] = new OpenApiSchema()
                        {
                            Type = JsonSchemaType.Array,
                            Items = new OpenApiSchema { Type = JsonSchemaType.Object }
                        }
                    }
                }
            ]
        };

        var filterCriteriaSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["field"] = new OpenApiSchema() { Type = JsonSchemaType.String },
                ["operator"] = new OpenApiSchema()
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. Common.EnumExtensions.GetEnumMemberValues<FilterOperator>().Select(v => JsonValue.Create(v))]
                },
                ["value"] = new OpenApiSchema() { Type = JsonSchemaType.Object },
                ["logic"] = new OpenApiSchema()
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. Common.EnumExtensions.GetEnumMemberValues<FilterLogicOperator>().Select(v => JsonValue.Create(v))]
                },
                ["filters"] = new OpenApiSchema()
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.Object }
                },
                ["customType"] = new OpenApiSchema()
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. Common.EnumExtensions.GetEnumMemberValues<FilterCustomType>().Select(v => JsonValue.Create(v))]
                },
                ["customParameters"] = new OpenApiSchema() { Type = JsonSchemaType.Object, AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Object } },
                ["specificationName"] = new OpenApiSchema() { Type = JsonSchemaType.String },
                ["specificationArguments"] = new OpenApiSchema() { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.Object } },
                ["compositeSpecification"] = new OpenApiSchema()
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["nodes"] = new OpenApiSchema() { Type = JsonSchemaType.Array, Items = specificationNodeSchema }
                    }
                }
            }
        };

        var filterModelSchema = new OpenApiSchema
        {
            Type = isRequestBody ? JsonSchemaType.Object : JsonSchemaType.String,
            Description = isRequestBody ? "FilterModel" : "URL-encoded JSON FilterModel",
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["page"] = new OpenApiSchema() { Type = JsonSchemaType.Integer, Default = JsonValue.Create(0) },
                ["pageSize"] = new OpenApiSchema() { Type = JsonSchemaType.Integer, Default = JsonValue.Create(0) },
                ["orderings"] = new OpenApiSchema()
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["field"] = new OpenApiSchema() { Type = JsonSchemaType.String },
                            ["direction"] = new OpenApiSchema()
                            {
                                Type = JsonSchemaType.String,
                                Enum = [.. Common.EnumExtensions.GetEnumMemberValues<OrderDirection>().Select(v => JsonValue.Create(v))]
                            }
                        }
                    }
                },
                ["filters"] = new OpenApiSchema() { Type = JsonSchemaType.Array, Items = filterCriteriaSchema },
                ["includes"] = new OpenApiSchema() { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
                ["hierarchy"] = new OpenApiSchema() { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["hierarchyMaxDepth"] = new OpenApiSchema() { Type = JsonSchemaType.Integer, Default = JsonValue.Create(5) }
            }
        };

        return builder
            //.WithOpenApi(operation => // TODO: WithOpenApi is obsolete in dotnet 10 https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/10/withopenapi-deprecated
            .AddOpenApiOperationTransformer((operation, context, ct) =>
            {
                if (isRequestBody)
                {
                    operation.RequestBody = new OpenApiRequestBody
                    {
                        Required = true,
                        Content =
                        {
                            ["application/json"] = new OpenApiMediaType { Schema = filterModelSchema }
                        }
                    };
                }
                else
                {
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = "filter",
                        In = ParameterLocation.Query,
                        Required = false,
                        Schema = filterModelSchema
                    });
                }

                return Task.CompletedTask;
            });
    }
}