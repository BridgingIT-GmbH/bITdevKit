// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System; // for StringComparison

/// <summary>
/// Configures Bearer token security requirements for the OpenAPI document.
/// </summary>
/// <remarks>
/// <para>
/// This transformer adds OAuth 2.0 Bearer token authentication to the OpenAPI specification,
/// enabling API clients and documentation tools (such as Scalar) to support authentication
/// through JWT bearer tokens.
/// </para>
/// <para>
/// Key responsibilities:
/// <list type="bullet">
/// <item>Adds an OAuth 2.0 security scheme to the OpenAPI components</item>
/// <item>Applies the security requirement to all operations as the default authentication method</item>
/// <item>Enables API documentation tools to provide authentication UI and token management</item>
/// </list>
/// </para>
/// <para>
/// References:
/// <list type="bullet">
/// <item>https://stackoverflow.com/questions/79443341/how-do-i-configure-scalar-to-authenticate-through-entra</item>
/// <item>https://github.com/scalar/scalar/blob/main/documentation/integrations/aspnetcore/integration.md#authentication</item>
/// <item>https://vitorafgomes.medium.com/how-to-build-a-minimal-api-with-scalar-and-keycloak-authentication-301fde490e40</item>
/// </list>
/// </para>
/// </remarks>
public class BearerSecurityRequirementDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Transforms the OpenAPI document to add Bearer token security configuration.
    /// </summary>
    /// <param name="document">The OpenAPI document being transformed.</param>
    /// <param name="context">Context information about the document transformation.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A completed task representing the transformation operation.</returns>
    /// <remarks>
    /// <para>
    /// This method:
    /// <list type="number">
    /// <item>Ensures the document has a components section</item>
    /// <item>Adds an OAuth 2.0 Bearer security scheme using the default JWT bearer authentication scheme name</item>
    /// <item>Applies the security requirement to all operations, making Bearer authentication the default</item>
    /// </list>
    /// </para>
    /// <para>
    /// The Bearer token is configured as an OAuth 2.0 scheme, which allows API documentation
    /// tools and clients to properly handle authentication flows and display authentication UI.
    /// </para>
    /// </remarks>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("[OpenAPI] Adding Bearer Security Scheme");

        // Ensure the document has a components section for security schemes
        document.Components ??= new OpenApiComponents();
        if (document.Components.SecuritySchemes == null)
        {
            document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.OrdinalIgnoreCase);
        }

        if (document.Security == null)
        {
            document.Security = new List<OpenApiSecurityRequirement>();
        }

        // Add the OAuth 2.0 Bearer security scheme to the components
        document.Components.SecuritySchemes.Add(
            JwtBearerDefaults.AuthenticationScheme,
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT",
                Description = "The JWT token in the format: Bearer {token}"
            });

        // Apply the security requirement to all operations as the default authentication method
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = [],
        });

        return Task.CompletedTask;
    }
}