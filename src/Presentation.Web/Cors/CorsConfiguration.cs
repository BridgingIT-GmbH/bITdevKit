// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Collections.Generic;

/// <summary>
/// Configuration for Cross-Origin Resource Sharing (CORS) policies.
/// </summary>
/// <remarks>
/// CORS allows controlled access to resources from different origins (domains).
/// Configure policies per environment to balance security with development flexibility.
/// <para>
/// Example configuration in appsettings.json:
/// <code>
/// {
///   "Cors": {
///     "Enabled": true,
///     "DefaultPolicy": "DefaultPolicy",
///     "Policies": {
///       "DefaultPolicy": {
///         "AllowedOrigins": ["https://example.com"],
///         "AllowAnyMethod": true,
///         "AllowAnyHeader": true,
///         "AllowCredentials": true
///       }
///     }
///   }
/// }
/// </code>
/// </para>
/// See: https://learn.microsoft.com/en-us/aspnet/core/security/cors
/// </remarks>
public class CorsConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether CORS is enabled.
    /// </summary>
    /// <remarks>
    /// When false, CORS services and middleware are not registered, and all cross-origin requests will be blocked by the browser.
    /// Default: false
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the name of the default policy to apply globally to all endpoints.
    /// </summary>
    /// <remarks>
    /// If specified, this policy will be applied to all endpoints unless overridden by [EnableCors] attribute.
    /// The policy name must exist in the <see cref="Policies"/> dictionary.
    /// Leave null to only use endpoint-level policy application via [EnableCors] attributes.
    /// <para>
    /// Example: "DefaultPolicy"
    /// </para>
    /// </remarks>
    public string DefaultPolicy { get; set; }

    /// <summary>
    /// Gets or sets the dictionary of named CORS policies.
    /// </summary>
    /// <remarks>
    /// Key: Policy name (used in configuration and [EnableCors] attributes)
    /// Value: Policy options defining allowed origins, methods, headers, etc.
    /// At least one policy must be defined when <see cref="Enabled"/> is true.
    /// <para>
    /// Policies can be applied globally via <see cref="DefaultPolicy"/> or per-endpoint using [EnableCors("PolicyName")] attributes.
    /// </para>
    /// </remarks>
    public Dictionary<string, CorsPolicyOptions> Policies { get; set; } = [];
}