// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

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
            OneOf = new List<OpenApiSchema>
            {
                new()
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["name"] = new() { Type = "string" },
                        ["arguments"] = new() { Type = "array", Items = new OpenApiSchema { Type = "object" } }
                    }
                },
                new()
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["logic"] = new()
                        {
                            Type = "string",
                            Enum = Enum.GetNames<FilterLogicOperator>()
                                .Select(x => new OpenApiString(x.ToLowerInvariant()))
                                .Cast<IOpenApiAny>()
                                .ToList()
                        },
                        ["nodes"] = new()
                        {
                            Type = "array",
                            Items = new OpenApiSchema { Type = "object" }
                        }
                    }
                }
            }
        };

        var filterCriteriaSchema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["field"] = new() { Type = "string" },
                ["operator"] = new()
                {
                    Type = "string",
                    Enum = Enum.GetNames<FilterOperator>()
                        .Select(x => new OpenApiString(x.ToLowerInvariant()))
                        .Cast<IOpenApiAny>()
                        .ToList()
                },
                ["value"] = new() { Type = "object" },
                ["logic"] = new()
                {
                    Type = "string",
                    Enum = Enum.GetNames<FilterLogicOperator>()
                        .Select(x => new OpenApiString(x.ToLowerInvariant()))
                        .Cast<IOpenApiAny>()
                        .ToList()
                },
                ["filters"] = new()
                {
                    Type = "array",
                    Items = new OpenApiSchema { Type = "object" }
                },
                ["customType"] = new()
                {
                    Type = "string",
                    Enum = Enum.GetNames<FilterCustomType>()
                        .Select(x => new OpenApiString(x.ToLowerInvariant()))
                        .Cast<IOpenApiAny>()
                        .ToList()
                },
                ["customParameters"] = new() { Type = "object", AdditionalProperties = new OpenApiSchema { Type = "object" } },
                ["specificationName"] = new() { Type = "string" },
                ["specificationArguments"] = new() { Type = "array", Items = new OpenApiSchema { Type = "object" } },
                ["compositeSpecification"] = new()
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["nodes"] = new() { Type = "array", Items = specificationNodeSchema }
                    }
                }
            }
        };

        var filterModelSchema = new OpenApiSchema
        {
            Type = isRequestBody ? "object" : "string",
            Description = isRequestBody ? "FilterModel" : "URL-encoded JSON FilterModel",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["page"] = new() { Type = "integer", Default = new OpenApiInteger(1) },
                ["pageSize"] = new() { Type = "integer", Default = new OpenApiInteger(10) },
                ["orderings"] = new()
                {
                    Type = "array",
                    Items = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["field"] = new() { Type = "string" },
                            ["direction"] = new()
                            {
                                Type = "string",
                                Enum = Enum.GetNames<OrderDirection>()
                                    .Select(x => new OpenApiString(x.ToLowerInvariant()))
                                    .Cast<IOpenApiAny>()
                                    .ToList()
                            }
                        }
                    }
                },
                ["filters"] = new() { Type = "array", Items = filterCriteriaSchema },
                ["includes"] = new() { Type = "array", Items = new OpenApiSchema { Type = "string" } },
                ["hierarchy"] = new() { Type = "string", Nullable = true },
                ["hierarchyMaxDepth"] = new() { Type = "integer", Default = new OpenApiInteger(5) }
            }
        };

        return builder.WithOpenApi(operation =>
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
            return operation;
        });
    }
}