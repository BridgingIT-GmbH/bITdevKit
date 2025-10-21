// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

/// <summary>
/// Completes incomplete FilterModel-related schemas in the OpenAPI document.
/// </summary>
/// <remarks>
/// <para>
/// The .NET 9 OpenAPI generator creates incomplete schemas for types that are only referenced
/// indirectly (e.g., <see cref="FilterCriteria"/> is only used inside <c>List&lt;FilterCriteria&gt;</c>).
/// This transformer enhances these incomplete schemas with their full definitions during schema generation.
/// </para>
/// <para>
/// Key responsibilities:
/// <list type="bullet">
/// <item>Completes schemas for all FilterModel-related types with their full property definitions</item>
/// <item>Defines enum values inline where they are used (not as separate schema references)</item>
/// <item>Handles polymorphic types like <see cref="SpecificationNode"/></item>
/// <item>Removes temporary dummy properties used for schema discovery</item>
/// <item>Adds proper descriptions and references to all properties</item>
/// </list>
/// </para>
/// </remarks>
public sealed class FilterModelSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Transforms OpenAPI schemas for FilterModel and related types.
    /// </summary>
    /// <param name="schema">The schema being transformed.</param>
    /// <param name="context">Context information about the schema transformation, including the type being processed.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A completed task representing the transformation operation.</returns>
    /// <remarks>
    /// This method is called during the OpenAPI schema generation phase for each type discovered.
    /// It dispatches to specific completion methods based on the type being transformed.
    /// </remarks>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if (type == typeof(FilterModel))
        {
            CompleteFilterModelSchema(schema);
        }
        else if (type == typeof(FilterCriteria))
        {
            CompleteFilterCriteriaSchema(schema);
        }
        else if (type == typeof(OrderDirection))
        {
            CompleteOrderDirectionSchema(schema);
        }
        else if (type == typeof(CompositeSpecification))
        {
            CompleteCompositeSpecificationSchema(schema);
        }
        else if (type == typeof(SpecificationNode))
        {
            CompleteSpecificationNodeSchema(schema);
        }
        else if (type == typeof(SpecificationLeaf))
        {
            CompleteSpecificationLeafSchema(schema);
        }
        else if (type == typeof(SpecificationGroup))
        {
            CompleteSpecificationGroupSchema(schema);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Completes the FilterModel schema, ensuring Filters property properly references FilterCriteria.
    /// </summary>
    /// <param name="schema">The FilterModel schema to complete.</param>
    /// <remarks>
    /// Removes temporary dummy properties used for schema discovery and ensures all properties
    /// have proper descriptions and references.
    /// </remarks>
    private void CompleteFilterModelSchema(OpenApiSchema schema)
    {
        schema.Type = "object";

        if (schema.Properties == null)
        {
            schema.Properties = new Dictionary<string, OpenApiSchema>();
        }

        // Remove the dummy schema discovery properties
        //schema.Properties.Remove("_FilterCriteriaSchema");
        //schema.Properties.Remove("_CompositeSpecificationSchema");
        //schema.Properties.Remove("_SpecificationNodeSchema");

        // Fix the Filters property to properly reference FilterCriteria
        schema.Properties["filters"] = new()
        {
            Type = "array",
            Items = new OpenApiSchema
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.Schema,
                    Id = "FilterCriteria"
                }
            },
            Nullable = true,
            Description = "Filter criteria to apply to the query"
        };

        // Ensure other properties have proper descriptions
        if (schema.Properties.TryGetValue("page", out var pageSchema))
        {
            pageSchema.Description = "The page number for pagination (1-based)";
        }

        if (schema.Properties.TryGetValue("pageSize", out var pageSizeSchema))
        {
            pageSizeSchema.Description = "The number of items per page";
        }

        if (schema.Properties.TryGetValue("noTracking", out var noTrackingSchema))
        {
            noTrackingSchema.Description = "Disable change tracking for improved query performance";
        }

        if (schema.Properties.TryGetValue("orderings", out var orderingsSchema))
        {
            orderingsSchema.Description = "Sort criteria to apply to the results";
        }

        if (schema.Properties.TryGetValue("includes", out var includesSchema))
        {
            includesSchema.Description = "Related entities to include via eager loading";
        }

        if (schema.Properties.TryGetValue("hierarchy", out var hierarchySchema))
        {
            hierarchySchema.Description = "Navigation property for hierarchical queries";
        }

        if (schema.Properties.TryGetValue("hierarchyMaxDepth", out var depthSchema))
        {
            depthSchema.Description = "Maximum depth for hierarchical data loading";
        }
    }

    /// <summary>
    /// Completes the FilterCriteria schema with all properties.
    /// </summary>
    /// <param name="schema">The FilterCriteria schema to complete.</param>
    /// <remarks>
    /// Defines inline enum values for operator, logic, and custom type instead of using schema references.
    /// Handles the polymorphic Value property which can be any type.
    /// </remarks>
    private void CompleteFilterCriteriaSchema(OpenApiSchema schema)
    {
        schema.Type = "object";
        schema.Properties = new Dictionary<string, OpenApiSchema>
        {
            ["field"] = new()
            {
                Type = "string",
                Nullable = true,
                Description = "The field to filter on"
            },
            ["operator"] = new()
            {
                Type = "string",
                Enum = new List<Microsoft.OpenApi.Any.IOpenApiAny>
                {
                    new Microsoft.OpenApi.Any.OpenApiString("eq"),
                    new Microsoft.OpenApi.Any.OpenApiString("neq"),
                    new Microsoft.OpenApi.Any.OpenApiString("isnull"),
                    new Microsoft.OpenApi.Any.OpenApiString("isnotnull"),
                    new Microsoft.OpenApi.Any.OpenApiString("isempty"),
                    new Microsoft.OpenApi.Any.OpenApiString("isnotempty"),
                    new Microsoft.OpenApi.Any.OpenApiString("gt"),
                    new Microsoft.OpenApi.Any.OpenApiString("gte"),
                    new Microsoft.OpenApi.Any.OpenApiString("lt"),
                    new Microsoft.OpenApi.Any.OpenApiString("lte"),
                    new Microsoft.OpenApi.Any.OpenApiString("contains"),
                    new Microsoft.OpenApi.Any.OpenApiString("doesnotcontain"),
                    new Microsoft.OpenApi.Any.OpenApiString("startswith"),
                    new Microsoft.OpenApi.Any.OpenApiString("doesnotstartwith"),
                    new Microsoft.OpenApi.Any.OpenApiString("endswith"),
                    new Microsoft.OpenApi.Any.OpenApiString("doesnotendwith"),
                    new Microsoft.OpenApi.Any.OpenApiString("any"),
                    new Microsoft.OpenApi.Any.OpenApiString("all"),
                    new Microsoft.OpenApi.Any.OpenApiString("none")
                },
                Description = "The operator to use for filtering"
            },
            ["value"] = new()
            {
                Nullable = true,
                Description = "The value to compare against (any type)",
                Type = "object"
                //OneOf = new List<OpenApiSchema>
                //{
                //    new() { Type = "string" },
                //    new() { Type = "number" },
                //    new() { Type = "integer" },
                //    new() { Type = "boolean" },
                //    //new() { Type = "null" },
                //    new() { Type = "array", Items = new OpenApiSchema() },
                //    new() { Type = "object", AdditionalPropertiesAllowed = true }
                //}
            },
            ["logic"] = new()
            {
                Type = "string",
                Enum = new List<Microsoft.OpenApi.Any.IOpenApiAny>
                {
                    new Microsoft.OpenApi.Any.OpenApiString("and"),
                    new Microsoft.OpenApi.Any.OpenApiString("or")
                },
                Default = new Microsoft.OpenApi.Any.OpenApiString("and"),
                Description = "Logic operator for combining nested filters"
            },
            ["filters"] = new()
            {
                Type = "array",
                Items = new OpenApiSchema
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.Schema,
                        Id = "FilterCriteria"
                    }
                },
                Nullable = true,
                Description = "Nested filter criteria"
            },
            ["customType"] = new()
            {
                Type = "string",
                Enum = new List<Microsoft.OpenApi.Any.IOpenApiAny>
                {
                    new Microsoft.OpenApi.Any.OpenApiString("none"),
                    new Microsoft.OpenApi.Any.OpenApiString("fulltextsearch"),
                    new Microsoft.OpenApi.Any.OpenApiString("daterange"),
                    new Microsoft.OpenApi.Any.OpenApiString("daterelative"),
                    new Microsoft.OpenApi.Any.OpenApiString("timerange"),
                    new Microsoft.OpenApi.Any.OpenApiString("timerelative"),
                    new Microsoft.OpenApi.Any.OpenApiString("numericrange"),
                    new Microsoft.OpenApi.Any.OpenApiString("isnull"),
                    new Microsoft.OpenApi.Any.OpenApiString("isnotnull"),
                    new Microsoft.OpenApi.Any.OpenApiString("enumvalues"),
                    new Microsoft.OpenApi.Any.OpenApiString("textin"),
                    new Microsoft.OpenApi.Any.OpenApiString("textnotin"),
                    new Microsoft.OpenApi.Any.OpenApiString("numericin"),
                    new Microsoft.OpenApi.Any.OpenApiString("numericnotin"),
                    new Microsoft.OpenApi.Any.OpenApiString("namedspecification"),
                    new Microsoft.OpenApi.Any.OpenApiString("compositespecification")
                },
                Default = new Microsoft.OpenApi.Any.OpenApiString("none"),
                Description = "Custom filter type"
            },
            ["customParameters"] = new()
            {
                Type = "object",
                AdditionalPropertiesAllowed = true,
                Nullable = true,
                Description = "Custom parameters for the filter"
            },
            ["specificationName"] = new()
            {
                Type = "string",
                Nullable = true,
                Description = "Name of a registered specification"
            },
            ["specificationArguments"] = new()
            {
                Type = "array",
                Items = new OpenApiSchema(),
                Nullable = true,
                Description = "Arguments for the specification"
            },
            ["compositeSpecification"] = new()
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.Schema,
                    Id = "CompositeSpecification"
                },
                Nullable = true,
                Description = "Composite specification combining multiple nodes"
            }
        };
    }

    /// <summary>
    /// Completes the OrderDirection enum schema.
    /// </summary>
    /// <param name="schema">The OrderDirection schema to complete.</param>
    private void CompleteOrderDirectionSchema(OpenApiSchema schema)
    {
        schema.Type = "string";
        schema.Enum = new List<Microsoft.OpenApi.Any.IOpenApiAny>
        {
            new Microsoft.OpenApi.Any.OpenApiString("asc"),
            new Microsoft.OpenApi.Any.OpenApiString("desc")
        };
        schema.Description = "Order direction";
    }

    /// <summary>
    /// Completes the CompositeSpecification schema.
    /// </summary>
    /// <param name="schema">The CompositeSpecification schema to complete.</param>
    /// <remarks>
    /// A composite specification is a tree structure containing specification nodes
    /// (either leaf or group nodes) that can be combined with logical operators.
    /// </remarks>
    private void CompleteCompositeSpecificationSchema(OpenApiSchema schema)
    {
        schema.Type = "object";
        schema.Properties = new Dictionary<string, OpenApiSchema>
        {
            ["nodes"] = new()
            {
                Type = "array",
                Items = new OpenApiSchema
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.Schema,
                        Id = "SpecificationNode"
                    }
                },
                Nullable = true,
                Description = "List of specification nodes"
            }
        };
    }

    /// <summary>
    /// Completes the SpecificationNode polymorphic schema.
    /// </summary>
    /// <param name="schema">The SpecificationNode schema to complete.</param>
    /// <remarks>
    /// SpecificationNode is a polymorphic type that can be either a SpecificationLeaf
    /// (referencing a named specification) or a SpecificationGroup (combining nodes with logic).
    /// The actual type is determined by which properties are present during deserialization.
    /// </remarks>
    private void CompleteSpecificationNodeSchema(OpenApiSchema schema)
    {
        schema.OneOf = new List<OpenApiSchema>
        {
            //new()
            //{
            //    Reference = new OpenApiReference
            //    {
            //        Type = ReferenceType.Schema,
            //        Id = "SpecificationLeaf"
            //    }
            //},
            //new()
            //{
            //    Reference = new OpenApiReference
            //    {
            //        Type = ReferenceType.Schema,
            //        Id = "SpecificationGroup"
            //    }
            //}
        };
        schema.Description = "Base specification node (polymorphic): SpecificationLeaf or SpecificationGroup";
    }

    /// <summary>
    /// Completes the SpecificationLeaf schema.
    /// </summary>
    /// <param name="schema">The SpecificationLeaf schema to complete.</param>
    /// <remarks>
    /// A specification leaf is a terminal node that references a named specification
    /// registered in the system and provides arguments for that specification.
    /// </remarks>
    private void CompleteSpecificationLeafSchema(OpenApiSchema schema)
    {
        schema.Type = "object";
        schema.Properties = new Dictionary<string, OpenApiSchema>
        {
            ["type"] = new()
            {
                Type = "string",
                Enum = new List<Microsoft.OpenApi.Any.IOpenApiAny>
                {
                    new Microsoft.OpenApi.Any.OpenApiString("SpecificationLeaf")
                },
                Description = "Discriminator for specification leaf"
            },
            ["name"] = new()
            {
                Type = "string",
                Nullable = true,
                Description = "Name of the registered specification"
            },
            ["arguments"] = new()
            {
                Type = "array",
                Items = new OpenApiSchema(),
                Nullable = true,
                Description = "Arguments for the specification"
            }
        };
        schema.Required = new HashSet<string> { "name" };
    }

    /// <summary>
    /// Completes the SpecificationGroup schema.
    /// </summary>
    /// <param name="schema">The SpecificationGroup schema to complete.</param>
    /// <remarks>
    /// A specification group combines multiple child nodes (leaves or other groups)
    /// using a logical operator (AND/OR) to create complex specification trees.
    /// </remarks>
    private void CompleteSpecificationGroupSchema(OpenApiSchema schema)
    {
        schema.Type = "object";
        schema.Properties = new Dictionary<string, OpenApiSchema>
        {
            ["type"] = new()
            {
                Type = "string",
                Enum = new List<Microsoft.OpenApi.Any.IOpenApiAny>
                {
                    new Microsoft.OpenApi.Any.OpenApiString("SpecificationGroup")
                }
            },
            ["logic"] = new()
            {
                Type = "string",
                Enum = new List<Microsoft.OpenApi.Any.IOpenApiAny>
                {
                    new Microsoft.OpenApi.Any.OpenApiString("and"),
                    new Microsoft.OpenApi.Any.OpenApiString("or")
                },
                Description = "Logic operator for combining nodes (serialized as string)"
            },
            ["nodes"] = new()
            {
                Type = "array",
                Items = new OpenApiSchema
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.Schema,
                        Id = "SpecificationNode"
                    }
                },
                Nullable = true,
                Description = "Child specification nodes"
            }
        };
        schema.Required = new HashSet<string> { "logic" };
    }
}