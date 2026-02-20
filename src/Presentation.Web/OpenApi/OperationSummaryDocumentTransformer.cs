// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.RegularExpressions;

/// <summary>
/// Enhances operation documentation with automatically generated summaries and descriptions.
/// </summary>
/// <remarks>
/// <para>
/// This transformer automatically generates clear, user-friendly summaries for API operations
/// based on their operation IDs and path information. It respects existing summaries set via XML documentation
/// but fills in gaps for endpoints that lack documentation.
/// </para>
/// <para>
/// Key responsibilities:
/// <list type="bullet">
/// <item>Generates summaries from operation IDs if not already documented</item>
/// <item>Converts HTTP method names (Get, Post, Put, Delete, Patch) to readable descriptions</item>
/// <item>Preserves existing XML documentation summaries</item>
/// <item>Improves API usability and self-discovery in documentation tools</item>
/// </list>
/// </para>
/// <para>
/// Example transformations:
/// <list type="bullet">
/// <item>GetById → "Get a single item by its identifier"</item>
/// <item>CreateUser → "Create a new user"</item>
/// <item>UpdateProfile → "Update an existing profile"</item>
/// <item>DeleteItem → "Delete the specified item"</item>
/// <item>PatchSettings → "Partially update settings"</item>
/// </list>
/// </para>
/// </remarks>
public class OperationSummaryDocumentTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Transforms the OpenAPI operation to add or enhance its summary.
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
    /// <item>If no summary exists, extracts the operation ID or path</item>
    /// <item>Generates a human-readable summary based on the operation details</item>
    /// <item>Applies the generated summary to the operation</item>
    /// </list>
    /// </para>
    /// <para>
    /// Priority order:
    /// <list type="number">
    /// <item>Existing summary from XML documentation (highest priority)</item>
    /// <item>Generated summary from operation ID or path (fallback)</item>
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

        // Get the method info from the operation context
        var method = context.Description.ActionDescriptor as ControllerActionDescriptor;
        if (method == null)
        {
            // Try to generate from operation ID as fallback
            if (!string.IsNullOrWhiteSpace(operation.OperationId))
            {
                operation.Summary = this.GenerateSummaryFromOperationId(operation.OperationId);
            }
            return Task.CompletedTask;
        }

        // Generate summary from method name
        var methodName = method.MethodInfo.Name;
        operation.Summary = this.GenerateSummaryFromMethodName(methodName);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates a human-readable summary from an operation ID.
    /// </summary>
    /// <param name="operationId">The operation ID (e.g., "GetById", "CreateUser").</param>
    /// <returns>A formatted summary string.</returns>
    private string GenerateSummaryFromOperationId(string operationId)
    {
        // Remove module/namespace prefix if present (e.g., "CoreModule.TodoItems.GetById" → "GetById")
        var parts = operationId.Split('.');
        var methodName = parts.Last();

        return this.GenerateSummaryFromMethodName(methodName);
    }

    /// <summary>
    /// Generates a human-readable summary from a method name.
    /// </summary>
    /// <param name="methodName">The name of the endpoint method (e.g., "GetById", "CreateUser").</param>
    /// <returns>A formatted summary string.</returns>
    /// <remarks>
    /// Extracts action verbs from method names and combines them with context to create
    /// descriptive summaries. For example:
    /// - "GetById" → "Get a single entity by its id"
    /// - "ListUsers" → "List all users"
    /// - "CreateUser" → "Create a new user"
    /// </remarks>
    private string GenerateSummaryFromMethodName(string methodName)
    {
        // Remove common suffixes
        methodName = methodName
            .Replace("Async", "")
            .Replace("Handle", "");

        // Map method names to descriptions
        if (methodName.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
        {
            if (methodName.Equals("Get", StringComparison.OrdinalIgnoreCase))
            {
                return "Retrieve all entities";
            }

            if (methodName.Equals("GetAll", StringComparison.OrdinalIgnoreCase))
            {
                return "Retrieve all entities";
            }

            if (methodName.Contains("ById", StringComparison.OrdinalIgnoreCase))
            {
                return "Get a single entity by its id";
            }

            if (methodName.Contains("By", StringComparison.OrdinalIgnoreCase))
            {
                return "Retrieve entities by criteria";
            }

            var resource = this.ExtractResourceName(methodName.Substring(3));
            return $"Retrieve {resource}";
        }

        if (methodName.StartsWith("Find", StringComparison.OrdinalIgnoreCase))
        {
            if (methodName.Equals("Find", StringComparison.OrdinalIgnoreCase))
            {
                return "Retrieve all entities";
            }

            if (methodName.Equals("FindAll", StringComparison.OrdinalIgnoreCase))
            {
                return "Retrieve all entities";
            }

            if (methodName.Contains("ById", StringComparison.OrdinalIgnoreCase))
            {
                return "Get a single entity by its id";
            }

            if (methodName.Contains("By", StringComparison.OrdinalIgnoreCase))
            {
                return "Retrieve entities by criteria";
            }

            var resource = this.ExtractResourceName(methodName.Substring(3));
            return $"Retrieve {resource}";
        }

        if (methodName.StartsWith("List", StringComparison.OrdinalIgnoreCase))
        {
            var resource = this.ExtractResourceName(methodName.Substring(4));
            return $"List all {resource}";
        }

        if (methodName.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
        {
            var resource = this.ExtractResourceName(methodName.Substring(6));
            return $"Create a new {resource}";
        }

        if (methodName.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
        {
            var resource = this.ExtractResourceName(methodName.Substring(6));
            return $"Update an existing {resource}";
        }

        if (methodName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase))
        {
            var resource = this.ExtractResourceName(methodName.Substring(6));
            return $"Delete the specified {resource}";
        }

        if (methodName.StartsWith("Patch", StringComparison.OrdinalIgnoreCase))
        {
            var resource = this.ExtractResourceName(methodName.Substring(5));
            return $"Partially update {resource}";
        }

        if (methodName.StartsWith("Search", StringComparison.OrdinalIgnoreCase))
        {
            var resource = this.ExtractResourceName(methodName.Substring(6));
            return $"Search for {resource}";
        }

        // Default fallback
        return this.ConvertCamelCaseToWords(methodName);
    }

    /// <summary>
    /// Extracts and formats the resource name from a method name.
    /// </summary>
    /// <param name="resourcePart">The part of the method name after the verb (e.g., "UsersById").</param>
    /// <returns>A formatted resource name (e.g., "users").</returns>
    /// <remarks>
    /// Converts PascalCase resource names to lowercase plural or singular form.
    /// For example: "Users" → "users", "UserProfile" → "user profile".
    /// </remarks>
    private string ExtractResourceName(string resourcePart)
    {
        if (string.IsNullOrEmpty(resourcePart))
        {
            return "entities";
        }

        // Remove common suffixes
        var cleaned = resourcePart
            .Replace("ById", "")
            .Replace("By", "")
            .Replace("Async", "");

        // Convert to lowercase and add spaces between words
        return this.ConvertCamelCaseToWords(cleaned).ToLowerInvariant();
    }

    /// <summary>
    /// Converts a PascalCase string to words separated by spaces.
    /// </summary>
    /// <param name="text">The PascalCase text to convert.</param>
    /// <returns>The text with spaces between words.</returns>
    /// <remarks>
    /// Example: "GetUserProfile" → "Get User Profile"
    /// </remarks>
    private string ConvertCamelCaseToWords(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Insert space before uppercase letters that follow lowercase letters
        return Regex.Replace(text, "([a-z])([A-Z])", "$1 $2");
    }
}
