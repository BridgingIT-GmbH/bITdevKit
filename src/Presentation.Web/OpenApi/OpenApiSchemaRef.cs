// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.OpenApi;

/// <summary>
/// Represents an OpenAPI schema that serializes exclusively as a JSON Reference
/// to a component schema in the document (i.e., <c>"$ref": "#/components/schemas/{name}"</c>).
/// </summary>
/// <remarks>
/// This helper is useful in ASP.NET Core (.NET 10) with OpenAPI 3.1 where the older
/// <c>OpenApiReference</c> model is no longer used for schemas. Instead of constructing
/// a reference object, you can return an instance of this class wherever an
/// <see cref="OpenApiSchema"/> is expected to cause the serializer to emit a <c>$ref</c>.
/// <para>
/// Example usage in a schema transformer:
/// <code language="csharp">
/// schema.Properties["nodes"] = new OpenApiSchema
/// {
///     Type = JsonSchemaType.Array | JsonSchemaType.Null,
///     Items = new OpenApiSchemaRef("SpecificationNode"),
///     Description = "List of specification nodes"
/// };
/// </code>
/// </para>
/// </remarks>
/// <param name="name">
/// The component schema name to reference. This should match the key under
/// <c>#/components/schemas</c> in the generated document.
/// </param>
public class OpenApiSchemaRef(string name) : OpenApiSchema
{
    /// <summary>
    /// Serializes this instance as an OpenAPI 3.1 schema object containing only a
    /// <c>$ref</c> to the specified component schema.
    /// </summary>
    /// <param name="writer">The OpenAPI writer.</param>
    public override void SerializeAsV31(IOpenApiWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("$ref");
        writer.WriteValue($"#/components/schemas/{name}");
        writer.WriteEndObject();
    }

    /// <summary>
    /// Serializes this instance as an OpenAPI 3.0 schema object containing only a
    /// <c>$ref</c> to the specified component schema.
    /// </summary>
    /// <param name="writer">The OpenAPI writer.</param>
    public override void SerializeAsV3(IOpenApiWriter writer) => SerializeAsV31(writer);

    /// <summary>
    /// Serializes this instance as an OpenAPI 2.0 (Swagger) schema object containing only a
    /// <c>$ref</c> to the specified component schema. While component paths differ in 2.0,
    /// this implementation mirrors 3.x for convenience; adjust if targeting 2.0 output.
    /// </summary>
    /// <param name="writer">The OpenAPI writer.</param>
    public override void SerializeAsV2(IOpenApiWriter writer) => SerializeAsV31(writer);
}