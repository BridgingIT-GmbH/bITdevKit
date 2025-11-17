// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Configurable OpenAPI document transformer for adding security requirements.
/// </summary>
/// <remarks>
/// <para>
/// This transformer provides a flexible way to add any type of security scheme to the OpenAPI specification.
/// It allows configuration of different authentication methods including OAuth 2.0, API Key, HTTP Basic, etc.,
/// and can be customized through dependency injection.
/// </para>
/// <para>
/// Key responsibilities:
/// <list type="bullet">
/// <item>Adds configurable security schemes to the OpenAPI components</item>
/// <item>Applies security requirements to all operations with customizable scopes</item>
/// <item>Supports multiple authentication scheme types (OAuth2, ApiKey, Http, OpenIdConnect, etc.)</item>
/// <item>Enables flexible configuration through <see cref="SecurityRequirementOptions"/></item>
/// </list>
/// </para>
/// <para>
/// Usage examples in <c>Program.cs</c>:
/// </para>
/// <para>
/// Bearer Token (JWT):
/// <code>
/// builder.Services.AddOpenApi(options =>
/// {
///     options.AddDocumentTransformer(new SecurityRequirementDocumentTransformer(
///         new SecurityRequirementOptions
///         {
///             SchemeName = "Bearer",
///             SchemeType = SecuritySchemeType.Http,
///             HttpScheme = "Bearer",
///             BearerFormat = "JWT",
///             Description = "JWT Bearer token authentication"
///         }));
/// });
/// </code>
/// </para>
/// <para>
/// OAuth 2.0:
/// <code>
/// builder.Services.AddOpenApi(options =>
/// {
///     options.AddDocumentTransformer(new SecurityRequirementDocumentTransformer(
///         new SecurityRequirementOptions
///         {
///             SchemeName = "OAuth2",
///             SchemeType = SecuritySchemeType.OAuth2,
///             AuthorizationUrl = new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/authorize"),
///             TokenUrl = new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/token"),
///             Scopes = ["api://client-id/data.read", "api://client-id/data.write"],
///             ScopeDescriptions = new Dictionary&lt;string, string&gt;
///             {
///                 ["api://client-id/data.read"] = "Read access to data",
///                 ["api://client-id/data.write"] = "Write access to data"
///             }
///         }));
/// });
/// </code>
/// </para>
/// <para>
/// API Key:
/// <code>
/// builder.Services.AddOpenApi(options =>
/// {
///     options.AddDocumentTransformer(new SecurityRequirementDocumentTransformer(
///         new SecurityRequirementOptions
///         {
///             SchemeName = "ApiKey",
///             SchemeType = SecuritySchemeType.ApiKey,
///             ParameterLocation = ParameterLocation.Header,
///             ParameterName = "X-API-Key",
///             Description = "API Key authentication"
///         }));
/// });
/// </code>
/// </para>
/// <para>
/// References:
/// <list type="bullet">
/// <item>https://stackoverflow.com/questions/79443341/how-do-i-configure-scalar-to-authenticate-through-entra</item>
/// <item>https://github.com/scalar/scalar/blob/main/documentation/integrations/aspnetcore/integration.md#authentication</item>
/// </list>
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="SecurityRequirementDocumentTransformer"/> class.
/// </remarks>
/// <param name="options">Configuration options for the security requirement.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
public class SecurityRequirementDocumentTransformer(SecurityRequirementOptions options) : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Options controlling the security scheme configuration.
    /// </summary>
    private readonly SecurityRequirementOptions options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Transforms the OpenAPI document to add configured security requirements.
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
    /// <item>Creates and adds a security scheme based on the configured options</item>
    /// <item>Applies the security requirement to all operations</item>
    /// <item>Logs the security scheme configuration</item>
    /// </list>
    /// </para>
    /// <para>
    /// The security scheme is configured according to the <see cref="SecurityRequirementOptions"/>,
    /// allowing different authentication methods to be used without code changes.
    /// </para>
    /// </remarks>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        //Console.WriteLine($"[OpenAPI] Adding Security Scheme: {this.options.SchemeName}");

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

        // Create the security scheme based on configuration
        var securityScheme = this.CreateSecurityScheme();

        // Add the security scheme to the components
        document.Components.SecuritySchemes.Add(this.options.SchemeName, securityScheme);

        // Apply the security requirement to all operations
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(this.options.SchemeName, document)] = []
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a configured security scheme based on the provided options.
    /// </summary>
    /// <returns>A configured <see cref="OpenApiSecurityScheme"/> instance.</returns>
    /// <remarks>
    /// The security scheme type and properties are determined by the <see cref="SecurityRequirementOptions.SchemeType"/>.
    /// </remarks>
    private OpenApiSecurityScheme CreateSecurityScheme()
    {
        var scheme = new OpenApiSecurityScheme
        {
            Type = this.options.SchemeType,
            Description = this.options.Description
        };

        // Configure scheme-specific properties
        switch (this.options.SchemeType)
        {
            case SecuritySchemeType.OAuth2:
                this.ConfigureOAuth2Scheme(scheme);
                break;

            case SecuritySchemeType.ApiKey:
                this.ConfigureApiKeyScheme(scheme);
                break;

            case SecuritySchemeType.Http:
                this.ConfigureHttpScheme(scheme);
                break;

            case SecuritySchemeType.OpenIdConnect:
                this.ConfigureOpenIdConnectScheme(scheme);
                break;
        }

        return scheme;
    }

    /// <summary>
    /// Configures an OAuth 2.0 security scheme.
    /// </summary>
    /// <param name="scheme">The security scheme to configure.</param>
    /// <remarks>
    /// Configures OAuth 2.0 flows based on the provided options.
    /// </remarks>
    private void ConfigureOAuth2Scheme(OpenApiSecurityScheme scheme)
    {
        scheme.Flows = new OpenApiOAuthFlows();

        if (this.options.AuthorizationUrl != null || this.options.TokenUrl != null)
        {
            scheme.Flows.Implicit = new OpenApiOAuthFlow
            {
                AuthorizationUrl = this.options.AuthorizationUrl,
                Scopes = this.options.ScopeDescriptions ?? []
            };

            scheme.Flows.ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = this.options.TokenUrl,
                Scopes = this.options.ScopeDescriptions ?? []
            };
        }
    }

    /// <summary>
    /// Configures an API Key security scheme.
    /// </summary>
    /// <param name="scheme">The security scheme to configure.</param>
    /// <remarks>
    /// Sets the location and parameter name for the API key based on options.
    /// </remarks>
    private void ConfigureApiKeyScheme(OpenApiSecurityScheme scheme)
    {
        scheme.In = this.options.ParameterLocation;
        scheme.Name = this.options.ParameterName ?? "X-API-Key";
    }

    /// <summary>
    /// Configures an HTTP security scheme (Basic, Bearer, etc.).
    /// </summary>
    /// <param name="scheme">The security scheme to configure.</param>
    /// <remarks>
    /// Sets the HTTP scheme (e.g., "Bearer", "Basic") based on options.
    /// </remarks>
    private void ConfigureHttpScheme(OpenApiSecurityScheme scheme)
    {
        scheme.Scheme = this.options.HttpScheme ?? "Bearer";
        scheme.BearerFormat = this.options.BearerFormat;
    }

    /// <summary>
    /// Configures an OpenID Connect security scheme.
    /// </summary>
    /// <param name="scheme">The security scheme to configure.</param>
    /// <remarks>
    /// Sets the OpenID Connect configuration URL based on options.
    /// </remarks>
    private void ConfigureOpenIdConnectScheme(OpenApiSecurityScheme scheme)
    {
        scheme.OpenIdConnectUrl = this.options.OpenIdConnectUrl;
    }
}

/// <summary>
/// Configuration options for the <see cref="SecurityRequirementDocumentTransformer"/>.
/// </summary>
/// <remarks>
/// This class defines all configurable aspects of a security scheme in the OpenAPI specification.
/// </remarks>
public class SecurityRequirementOptions
{
    /// <summary>
    /// Gets or sets the name of the security scheme (e.g., "Bearer", "ApiKey", "OAuth2").
    /// </summary>
    /// <value>
    /// The security scheme name. Defaults to "Bearer".
    /// </value>
    public string SchemeName { get; set; } = "Bearer";

    /// <summary>
    /// Gets or sets the type of security scheme.
    /// </summary>
    /// <value>
    /// One of: OAuth2, ApiKey, Http, OpenIdConnect, or Mutual.
    /// Defaults to <see cref="SecuritySchemeType.Http"/>.
    /// </value>
    public SecuritySchemeType SchemeType { get; set; } = SecuritySchemeType.Http;

    /// <summary>
    /// Gets or sets the description of the security scheme displayed in API documentation.
    /// </summary>
    /// <value>
    /// A human-readable description. Defaults to null.
    /// </value>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the scopes required for this security scheme.
    /// </summary>
    /// <value>
    /// A list of scope names (e.g., ["api://client-id/data.read", "api://client-id/data.write"]).
    /// Defaults to an empty list.
    /// </value>
    public List<string> Scopes { get; set; }

    /// <summary>
    /// Gets or sets the scope descriptions for OAuth 2.0 flows.
    /// </summary>
    /// <value>
    /// A dictionary mapping scope names to their descriptions.
    /// Defaults to null.
    /// </value>
    public Dictionary<string, string> ScopeDescriptions { get; set; }

    /// <summary>
    /// Gets or sets the authorization URL for OAuth 2.0 flows.
    /// </summary>
    /// <value>
    /// The authorization endpoint URL. Defaults to null.
    /// </value>
    public Uri AuthorizationUrl { get; set; }

    /// <summary>
    /// Gets or sets the token URL for OAuth 2.0 flows.
    /// </summary>
    /// <value>
    /// The token endpoint URL. Defaults to null.
    /// </value>
    public Uri TokenUrl { get; set; }

    /// <summary>
    /// Gets or sets the HTTP scheme (for HTTP security scheme type).
    /// </summary>
    /// <value>
    /// The HTTP scheme name (e.g., "Bearer", "Basic"). Defaults to "Bearer".
    /// </value>
    public string HttpScheme { get; set; }

    /// <summary>
    /// Gets or sets the Bearer format (for HTTP Bearer security scheme).
    /// </summary>
    /// <value>
    /// The bearer format (e.g., "JWT", "jti"). Defaults to null.
    /// </value>
    public string BearerFormat { get; set; }

    /// <summary>
    /// Gets or sets the parameter location (for API Key security scheme).
    /// </summary>
    /// <value>
    /// One of: Query, Header, or Cookie. Defaults to <see cref="ParameterLocation.Header"/>.
    /// </value>
    public ParameterLocation ParameterLocation { get; set; } = ParameterLocation.Header;

    /// <summary>
    /// Gets or sets the parameter name (for API Key security scheme).
    /// </summary>
    /// <value>
    /// The name of the header, query, or cookie parameter. Defaults to "X-API-Key".
    /// </value>
    public string ParameterName { get; set; }

    /// <summary>
    /// Gets or sets the OpenID Connect configuration URL.
    /// </summary>
    /// <value>
    /// The OpenID Connect discovery endpoint URL. Defaults to null.
    /// </value>
    public Uri OpenIdConnectUrl { get; set; }
}