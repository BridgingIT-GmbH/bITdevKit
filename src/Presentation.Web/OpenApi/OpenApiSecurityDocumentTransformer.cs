// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

/// <summary>
/// Adds configurable OAuth2 and HTTP bearer security schemes to an OpenAPI document.
/// </summary>
/// <remarks>
/// This transformer is OpenAPI-focused rather than Scalar-specific. It updates the generated
/// document components and global security requirements so any OpenAPI consumer can understand
/// the configured authentication model.
/// </remarks>
public sealed class OpenApiSecurityDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Applies the configured OpenAPI security schemes and global requirements to the document.
    /// </summary>
    /// <param name="document">The OpenAPI document being transformed.</param>
    /// <param name="context">The transformation context, including access to application services.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>A completed task after the document has been updated.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="OpenApiSecurityOptions"/> has not been registered in the DI container.
    /// </exception>
    /// <remarks>
    /// The transformer can add:
    /// <list type="bullet">
    /// <item>A reusable OAuth2 authorization code security scheme.</item>
    /// <item>A reusable HTTP bearer security scheme for JWT tokens.</item>
    /// <item>Global OpenAPI security requirements that reference one or both schemes.</item>
    /// </list>
    /// </remarks>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var optionsAccessor = context.ApplicationServices
            .GetService<Microsoft.Extensions.Options.IOptions<OpenApiSecurityOptions>>();

        if (optionsAccessor is null)
        {
            throw new InvalidOperationException(
                $"{nameof(OpenApiSecurityDocumentTransformer)} requires {nameof(OpenApiSecurityOptions)} to be registered in the DI container. ");
        }

        var options = optionsAccessor.Value;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.OrdinalIgnoreCase);
        document.Components.SecuritySchemes.Remove(options.OAuth2SchemeName);
        document.Components.SecuritySchemes.Remove(options.BearerSchemeName);

        if (options.AddOAuth2Scheme &&
            !string.IsNullOrWhiteSpace(options.AuthorizationUrl) &&
            !string.IsNullOrWhiteSpace(options.TokenUrl))
        {
            document.Components.SecuritySchemes[options.OAuth2SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Description = "Authenticate using the OAuth2 authorization code flow.",
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri(options.AuthorizationUrl),
                        TokenUrl = new Uri(options.TokenUrl),
                        Scopes = (options.Scopes ?? []).ToDictionary(
                            scope => scope,
                            scope => $"Request the {scope} scope.",
                            StringComparer.OrdinalIgnoreCase),
                    },
                },
            };
        }

        if (options.AddBearerScheme)
        {
            document.Components.SecuritySchemes[options.BearerSchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "The JWT token in the format: Bearer {token}",
            };
        }

        if (!options.AddGlobalSecurityRequirements)
        {
            return Task.CompletedTask;
        }

        document.Security =
            [
                .. (document.Security ?? [])
                    .Where(requirement => !requirement.Keys.Any(scheme =>
                        string.Equals(scheme.Reference?.Id, options.OAuth2SchemeName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(scheme.Reference?.Id, options.BearerSchemeName, StringComparison.OrdinalIgnoreCase))),
            ];

        if (options.AddOAuth2Scheme &&
            !string.IsNullOrWhiteSpace(options.AuthorizationUrl) &&
            !string.IsNullOrWhiteSpace(options.TokenUrl))
        {
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(options.OAuth2SchemeName, document)] = [.. (options.Scopes ?? [])],
            });
        }

        if (options.AddBearerScheme)
        {
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(options.BearerSchemeName, document)] = [],
            });
        }

        return Task.CompletedTask;
    }
}
