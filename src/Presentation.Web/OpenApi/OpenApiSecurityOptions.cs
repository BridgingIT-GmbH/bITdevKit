// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.OpenApi;

using Microsoft.AspNetCore.Authentication.JwtBearer;

/// <summary>
/// Configures generated OpenAPI security schemes and related OAuth2 endpoint metadata.
/// </summary>
public class OpenApiSecurityOptions
{
    /// <summary>
    /// Gets or sets the name of the HTTP bearer security scheme in the OpenAPI document.
    /// </summary>
    public string BearerSchemeName { get; set; } = JwtBearerDefaults.AuthenticationScheme;

    /// <summary>
    /// Gets or sets the name of the OAuth2 security scheme in the OpenAPI document.
    /// </summary>
    public string OAuth2SchemeName { get; set; } = "OAuth2";

    /// <summary>
    /// Gets or sets a value indicating whether a HTTP bearer security scheme should be added.
    /// </summary>
    public bool AddBearerScheme { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether an OAuth2 authorization code security scheme should be added.
    /// </summary>
    public bool AddOAuth2Scheme { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether global OpenAPI security requirements should be added.
    /// </summary>
    public bool AddGlobalSecurityRequirements { get; set; } = true;

    /// <summary>
    /// Gets or sets the authority base URL used to build OAuth2 authorization and token endpoint URLs.
    /// </summary>
    public string Authority { get; set; }

    /// <summary>
    /// Gets or sets the relative authorization endpoint path appended to <see cref="Authority"/>.
    /// </summary>
    public string AuthorizationPath { get; set; } = "/api/_system/identity/connect/authorize";

    /// <summary>
    /// Gets or sets the relative token endpoint path appended to <see cref="Authority"/>.
    /// </summary>
    public string TokenPath { get; set; } = "/api/_system/identity/connect/token";

    /// <summary>
    /// Gets or sets the scopes exposed by the OAuth2 authorization code flow.
    /// </summary>
    public string[] Scopes { get; set; } = ["openid", "profile", "email", "roles"];

    /// <summary>
    /// Gets the full OAuth2 authorization endpoint URL derived from <see cref="Authority"/> and <see cref="AuthorizationPath"/>.
    /// </summary>
    public string AuthorizationUrl
    {
        get
        {
            var authority = this.Authority?.TrimEnd('/');
            return string.IsNullOrWhiteSpace(authority)
                ? null
                : $"{authority}{this.AuthorizationPath}";
        }
    }

    /// <summary>
    /// Gets the full OAuth2 token endpoint URL derived from <see cref="Authority"/> and <see cref="TokenPath"/>.
    /// </summary>
    public string TokenUrl
    {
        get
        {
            var authority = this.Authority?.TrimEnd('/');
            return string.IsNullOrWhiteSpace(authority)
                ? null
                : $"{authority}{this.TokenPath}";
        }
    }
}
