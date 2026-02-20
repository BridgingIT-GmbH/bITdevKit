// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Sets the operation summary directly from the operation name/ID.
/// </summary>
/// <remarks>
/// <para>
/// This transformer provides a simple summary generation strategy that uses the operation ID
/// directly as the operation summary. This is useful for APIs that already have descriptive
/// operation ID naming conventions and don't need additional text generation.
/// </para>
/// <para>
/// Key responsibilities:
/// <list type="bullet">
/// <item>Extracts the operation ID</item>
/// <item>Uses it directly as the summary without modification</item>
/// <item>Respects existing summaries from XML documentation</item>
/// <item>Provides a clean, minimal approach to summary generation</item>
/// </list>
/// </para>
/// <para>
/// Example:
/// <list type="bullet">
/// <item>Operation ID: "CoreModule.TodoItems.GetAll" → Summary: "CoreModule.TodoItems.GetAll"</item>
/// <item>Operation ID: "TodoItems.GetById" → Summary: "TodoItems.GetById"</item>
/// </list>
/// </para>
/// </remarks>
public class OperationNameToSummaryTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Transforms the OpenAPI operation to set the summary from the operation name.
    /// </summary>
    /// <param name="operation">The OpenAPI operation being transformed.</param>
    /// <param name="context">Context information about the operation.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A completed task representing the transformation operation.</returns>
    /// <remarks>
    /// <para>
    /// This method:
    /// <list type="number">
    /// <item>Checks if the operation already has a summary from XML documentation</item>
    /// <item>If no summary exists, uses the operation ID as the summary</item>
    /// <item>Skips processing if no operation ID is available</item>
    /// </list>
    /// </para>
    /// </remarks>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Skip if summary already exists from XML documentation
        if (!string.IsNullOrWhiteSpace(operation.Summary))
        {
            return Task.CompletedTask;
        }

        // Set summary from operation ID if available
        if (!string.IsNullOrWhiteSpace(operation.OperationId))
        {
            operation.Summary = operation.OperationId;
        }

        return Task.CompletedTask;
    }
}