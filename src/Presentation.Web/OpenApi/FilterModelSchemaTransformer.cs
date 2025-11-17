// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

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
            this.CompleteFilterModelSchema(schema);
        }
        else if (type == typeof(FilterCriteria))
        {
            this.CompleteFilterCriteriaSchema(schema);
        }
        else if (type == typeof(OrderDirection))
        {
            this.CompleteOrderDirectionSchema(schema);
        }
        else if (type == typeof(CompositeSpecification))
        {
            this.CompleteCompositeSpecificationSchema(schema);
        }
        else if (type == typeof(SpecificationNode))
        {
            this.CompleteSpecificationNodeSchema(schema);
        }
        else if (type == typeof(SpecificationLeaf))
        {
            this.CompleteSpecificationLeafSchema(schema);
        }
        else if (type == typeof(SpecificationGroup))
        {
            this.CompleteSpecificationGroupSchema(schema);
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
        schema.Type = JsonSchemaType.Object;
        schema.Description = "Represents a complete filter model for querying data with pagination, sorting, and filtering";
        schema.Example = new JsonObject
        {
            ["page"] = 1,
            ["pageSize"] = 150,

            //["noTracking"] = true,  // uncomment if needed

            ["orderings"] = new JsonArray
            {
                new JsonObject
                {
                    ["field"] = "CreatedDate",
                    ["direction"] = "desc"
                }
            },

            ["filters"] = new JsonArray
            {
                new JsonObject
                {
                    ["field"] = "Status",
                    ["operator"] = "eq",
                    ["value"] = "active"
                }
            },

            ["includes"] = new JsonArray
            {
                "relatedEntity"
            }

            //["hierarchy"] = "children",
            //["hierarchyMaxDepth"] = 3
        };

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();

        // Remove the dummy schema discovery properties
        //schema.Properties.Remove("_FilterCriteriaSchema");
        //schema.Properties.Remove("_CompositeSpecificationSchema");
        //schema.Properties.Remove("_SpecificationNodeSchema");

        // Fix the Filters property to properly reference FilterCriteria
        schema.Properties["filters"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Array | JsonSchemaType.Null,
            Items = new OpenApiSchema()
            {
                OneOf =
                [
                    new OpenApiSchemaRef("FilterCriteria"),
                ]
            },

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
        schema.Type = JsonSchemaType.Object;
        schema.Description = "Represents a single filter criterion for filtering query results";

        schema.Example = new JsonObject
        {
            ["field"] = "status",
            ["operator"] = "eq",
            ["value"] = "active",
            ["logic"] = "and"
        };

        schema.Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["field"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Description = "The field to filter on"
            },

            ["operator"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum =
                [
                    "eq", "neq", "isnull", "isnotnull", "isempty", "isnotempty",
                    "gt", "gte", "lt", "lte",
                    "contains", "doesnotcontain",
                    "startswith", "doesnotstartwith",
                    "endswith", "doesnotendwith",
                    "any", "all", "none"
                ],
                Description = "The operator to use for filtering"
            },

            ["value"] = new OpenApiSchema
            {
                // Any type: string, number, bool, array, object, or null
                OneOf =
                [
                    //new OpenApiSchema() { Type = JsonSchemaType.String },
                    //new OpenApiSchema() { Type = JsonSchemaType.Number },
                    //new OpenApiSchema() { Type = JsonSchemaType.Integer },
                    //new OpenApiSchema() { Type = JsonSchemaType.Boolean },
                    new OpenApiSchema() { Type = JsonSchemaType.Null },
                    new OpenApiSchema() { Type = JsonSchemaType.Object }
                    //new OpenApiSchema() { Type = JsonSchemaType.Array, Items = new OpenApiSchema() },
                    //new OpenApiSchema() { Type = JsonSchemaType.Object, AdditionalPropertiesAllowed = true }
                ],
                Description = "The value to compare against (any type)"
            },

            ["logic"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = ["and", "or"],
                Default = JsonValue.Create("and"),
                Description = "Logic operator for combining nested lofty filters"
            },

            ["filters"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array | JsonSchemaType.Null,
                Items = new OpenApiSchemaRef("FilterCriteria"),
                Description = "Nested filter criteria"
            },

            ["customType"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum =
            [
                "none", "fulltextsearch", "daterange", "daterelative",
                "timerange", "timerelative", "numericrange",
                "isnull", "isnotnull", "enumvalues",
                "textin", "textnotin", "numericin", "numericnotin",
                "namedspecification", "compositespecification"
            ],
                Default = JsonValue.Create("none"),
                Description = "Custom filter type"
            },

            ["customParameters"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object | JsonSchemaType.Null,
                AdditionalPropertiesAllowed = true,
                Description = "Custom parameters for the filter"
            },

            ["specificationName"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Description = "Name of a registered specification"
            },

            ["specificationArguments"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array | JsonSchemaType.Null,
                Items = new OpenApiSchema(),
                Description = "Arguments for the specification"
            },

            ["compositeSpecification"] = new OpenApiSchemaRef("CompositeSpecification")
        };

        // Required fields
        //schema.Required = new HashSet<string> { "operator" };
    }

    /// <summary>
    /// Completes the OrderDirection enum schema.
    /// </summary>
    /// <param name="schema">The OrderDirection schema to complete.</param>
    private void CompleteOrderDirectionSchema(OpenApiSchema schema)
    {
        schema.Type = JsonSchemaType.String;
        schema.Description = "Order direction";
        schema.Example = JsonValue.Create("asc");
        schema.Enum =
        [
            "asc",
            "desc"
        ];
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
        schema.Type = JsonSchemaType.Object;
        schema.Description = "A tree structure containing specification nodes for complex filtering logic";

        schema.Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["nodes"] = new OpenApiSchema()
            {
                Type = JsonSchemaType.Array | JsonSchemaType.Null,
                Items = new OpenApiSchemaRef("SpecificationNode"),
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
        schema.OneOf = [];
        schema.Description = "Polymorphic specification node: either a leaf (named specification) or group (combining multiple nodes)";
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
        schema.Type = JsonSchemaType.Object;
        schema.Description = "A terminal specification node that references a named specification";
        schema.Example = new JsonObject
        {
            ["type"] = "SpecificationLeaf",
            ["name"] = "ActiveUserSpecification",
            ["arguments"] = new JsonArray()
        };
        schema.Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["type"] = new OpenApiSchema()
            {
                Type = JsonSchemaType.String,
                Enum = [JsonValue.Create("SpecificationLeaf")!],
                Description = "Discriminator for specification leaf"
            },
            ["name"] = new OpenApiSchema()
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Description = "Name of the registered specification"
            },
            ["arguments"] = new OpenApiSchema()
            {
                Type = JsonSchemaType.Array | JsonSchemaType.Null,
                Items = new OpenApiSchema(),
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
        schema.Type = JsonSchemaType.Object;
        schema.Description = "A composite specification group combining multiple child nodes with a logical operator";

        schema.Example = new JsonObject
        {
            ["type"] = "SpecificationGroup",
            ["logic"] = "and",
            ["nodes"] = new JsonArray()
        };

        schema.Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["type"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = [JsonValue.Create("SpecificationGroup")!],
                Description = "Discriminator for specification group"
            },
            ["logic"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum =
                [
                    JsonValue.Create("and")!,
                JsonValue.Create("or")!
                ],
                Description = "Logic operator for combining nodes (serialized as string)"
            },
            ["nodes"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array | JsonSchemaType.Null,
                Items = new OpenApiSchemaRef("SpecificationNode"),
                Description = "Child specification nodes"
            }
        };

        schema.Required = new HashSet<string> { "logic" };
    }
}
