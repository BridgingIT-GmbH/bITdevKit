// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;
using System.Reflection;

/// <summary>
/// Marks deprecated API operations and adds migration guidance to the OpenAPI documentation.
/// </summary>
/// <remarks>
/// <para>
/// This transformer detects deprecated endpoints using the <see cref="ObsoleteAttribute"/>
/// and enhances their OpenAPI documentation with deprecation warnings and migration guidance.
/// This helps API consumers understand which endpoints are being phased out and what alternatives
/// to use.
/// </para>
/// <para>
/// Key responsibilities:
/// <list type="bullet">
/// <item>Identifies deprecated operations via <see cref="ObsoleteAttribute"/></item>
/// <item>Sets the deprecated flag in the OpenAPI operation</item>
/// <item>Adds deprecation message and migration guidance to the description</item>
/// <item>Preserves existing descriptions and appends deprecation info</item>
/// <item>Provides a consistent format for deprecation notices across all endpoints</item>
/// </list>
/// </para>
/// <para>
/// Usage example:
/// <code>
/// [Obsolete("Use GetUserV2 instead. This endpoint will be removed on 2025-12-31.")]
/// [HttpGet("{id}")]
/// public IResult GetUser(string id) { ... }
/// </code>
/// </para>
/// <para>
/// This will result in:
/// - Operation marked as deprecated in OpenAPI spec
/// - Visual indicator in API documentation tools
/// - Clear migration path provided to consumers
/// </para>
/// </remarks>
public class DeprecatedOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Transforms the OpenAPI operation to mark it as deprecated if applicable.
    /// </summary>
    /// <param name="operation">The OpenAPI operation being transformed.</param>
    /// <param name="context">Context information about the operation.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A completed task representing the transformation operation.</returns>
    /// <remarks>
    /// <para>
    /// This method:
    /// <list type="number">
    /// <item>Checks if the method has an <see cref="ObsoleteAttribute"/></item>
    /// <item>If found, sets the operation's Deprecated flag to true</item>
    /// <item>Extracts the deprecation message from the attribute</item>
    /// <item>Prepends the deprecation notice to the operation description</item>
    /// <item>Formats the notice for clear visibility in documentation</item>
    /// </list>
    /// </para>
    /// <para>
    /// The deprecation notice is formatted as a prominent header:
    /// <code>
    /// **DEPRECATED** - [Original Message]
    ///
    /// [Original Description]
    /// </code>
    /// </para>
    /// </remarks>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Get the method info from the operation context
        var method = context.Description.ActionDescriptor as ControllerActionDescriptor;
        if (method == null)
        {
            return Task.CompletedTask;
        }

        // Check for ObsoleteAttribute on the method
        var obsoleteAttribute = method.MethodInfo
            .GetCustomAttribute<ObsoleteAttribute>();

        if (obsoleteAttribute == null)
        {
            return Task.CompletedTask;
        }

        // Mark operation as deprecated in OpenAPI
        operation.Deprecated = true;

        // Build deprecation notice
        var deprecationNotice = this.FormatDeprecationNotice(obsoleteAttribute);

        // Prepend deprecation notice to description
        operation.Description = deprecationNotice + (operation.Description ?? "");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Formats a deprecation notice from an <see cref="ObsoleteAttribute"/>.
    /// </summary>
    /// <param name="obsoleteAttribute">The obsolete attribute to format.</param>
    /// <returns>A formatted deprecation notice string.</returns>
    /// <remarks>
    /// Creates a visually distinctive deprecation warning with the message from the attribute.
    /// </remarks>
    private string FormatDeprecationNotice(ObsoleteAttribute obsoleteAttribute)
    {
        var message = obsoleteAttribute.Message ?? "This endpoint is deprecated.";

        return $"""
            **DEPRECATED** - {message}

            """;
    }
}