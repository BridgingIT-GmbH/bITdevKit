// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System;

/// <summary>
/// Configuration options for JWT Bearer token authentication.
/// </summary>
/// <remarks>
/// <para>
/// This class encapsulates all settings required to configure JWT Bearer authentication
/// in an ASP.NET Core application. It includes token validation parameters, issuer/audience
/// configuration, and metadata requirements.
/// </para>
/// <para>
/// Typically, these options are populated from application configuration (appsettings.json)
/// under the "Authentication" section and passed to the authentication service collection extensions.
/// </para>
/// <para>
/// Example configuration:
/// <code>
/// {
///   "Authentication": {
///     "Authority": "https://auth.example.com",
///     "ValidIssuer": "https://auth.example.com",
///     "ValidAudience": "my-api",
///     "ValidateIssuer": true,
///     "ValidateAudience": true,
///     "ValidateLifetime": true,
///     "ValidateSigningKey": true,
///     "RequireHttpsMetadata": true,
///     "SaveToken": true,
///     "RequireSignedTokens": true,
///     "ClockSkew": "00:05:00"
///   }
/// }
/// </code>
/// </para>
/// </remarks>
public class AuthenticationOptions
{
    /// <summary>
    /// Gets or sets the authority URL where the authorization server is located.
    /// </summary>
    /// <value>
    /// The base URL of the authorization server (e.g., "https://login.microsoftonline.com/common").
    /// This is required and used to fetch OpenID Connect metadata and signing keys.
    /// </value>
    public string Authority { get; set; }

    /// <summary>
    /// Gets or sets the valid issuer URL that tokens should be issued from.
    /// </summary>
    /// <value>
    /// The expected issuer URL. Tokens with a different issuer will be rejected if <see cref="ValidateIssuer"/> is true.
    /// Example: "https://auth.example.com"
    /// </value>
    public string ValidIssuer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to validate the token issuer.
    /// </summary>
    /// <value>
    /// True to validate that the token issuer matches <see cref="ValidIssuer"/>; false to skip validation.
    /// Defaults to false.
    /// </value>
    public bool ValidateIssuer { get; set; }

    /// <summary>
    /// Gets or sets the valid audience for the API.
    /// </summary>
    /// <value>
    /// The expected audience claim in the token. Tokens with a different audience will be rejected if <see cref="ValidateAudience"/> is true.
    /// Example: "my-api", "https://api.example.com"
    /// </value>
    public string ValidAudience { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to validate the token audience.
    /// </summary>
    /// <value>
    /// True to validate that the token audience matches <see cref="ValidAudience"/>; false to skip validation.
    /// Defaults to false.
    /// </value>
    public bool ValidateAudience { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to validate the token lifetime (expiration).
    /// </summary>
    /// <value>
    /// True to reject expired tokens; false to accept tokens regardless of expiration.
    /// Defaults to false. Should generally be set to true for security.
    /// </value>
    public bool ValidateLifetime { get; set; }

    /// <summary>
    /// Gets or sets the clock skew tolerance for token lifetime validation.
    /// </summary>
    /// <value>
    /// A timespan representing the allowed difference between server times during token validation.
    /// Accounts for minor clock differences between the token issuer and validator.
    /// Defaults to 5 minutes.
    /// </value>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether HTTPS metadata is required.
    /// </summary>
    /// <value>
    /// True to require the authority URL to use HTTPS and reject HTTP metadata endpoints; false to allow HTTP.
    /// Defaults to false. Should be set to true in production environments for security.
    /// </value>
    public bool RequireHttpsMetadata { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the access token should be stored in the HTTP context.
    /// </summary>
    /// <value>
    /// True to save the token in the authentication properties, making it accessible via the HTTP context;
    /// false to discard the token after validation. Useful for accessing the token in application code.
    /// Defaults to false.
    /// </value>
    public bool SaveToken { get; set; }

    /// <summary>
    /// Gets or sets the signing key used for symmetric token validation.
    /// </summary>
    /// <value>
    /// A base64-encoded or plaintext key used to validate token signatures when using symmetric algorithms.
    /// Only used if <see cref="ValidateSigningKey"/> is true and the authority is not used for key retrieval.
    /// Typically used with HS256 (HMAC-SHA256) algorithms.
    /// Defaults to null (keys fetched from authority's metadata endpoint).
    /// </value>
    public string SigningKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to validate the token signing key.
    /// </summary>
    /// <value>
    /// True to validate that the token is signed with a known and trusted key; false to skip signing key validation.
    /// Defaults to false. Should be set to true for security.
    /// </value>
    public bool ValidateSigningKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether tokens must be cryptographically signed.
    /// </summary>
    /// <value>
    /// True to reject unsigned tokens; false to accept unsigned tokens.
    /// Defaults to false. Should be set to true in production for security.
    /// </value>
    public bool RequireSignedTokens { get; set; }
}