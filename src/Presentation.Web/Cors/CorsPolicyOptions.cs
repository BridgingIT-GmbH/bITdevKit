// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Options for configuring a CORS policy.
/// </summary>
/// <remarks>
/// Defines what origins, methods, headers, and credentials are allowed for cross-origin requests.
/// Null values for boolean properties are treated as false (feature not enabled).
/// <para>
/// Configure policies based on your security requirements:
/// - Production: Use specific <see cref="AllowedOrigins"/> with <see cref="AllowCredentials"/> = true
/// - Development: Use <see cref="AllowAnyOrigin"/> = true (without credentials) or specific localhost origins
/// - Public APIs: Use <see cref="AllowAnyOrigin"/> = true (without credentials)
/// </para>
/// </remarks>
public class CorsPolicyOptions
{
    /// <summary>
    /// Gets or sets the array of allowed origins (e.g., "https://example.com").
    /// </summary>
    /// <remarks>
    /// Specifies which origins are allowed to make cross-origin requests.
    /// Origins must include scheme, host, and optionally port (e.g., "https://localhost:5001").
    /// Do not include trailing slashes.
    /// Cannot be used when <see cref="AllowAnyOrigin"/> is true.
    /// When <see cref="AllowWildcardSubdomains"/> is true, specify base domain (e.g., "https://example.com" allows https://*.example.com).
    /// <para>
    /// Example: ["https://example.com", "https://app.example.com", "https://localhost:3000"]
    /// </para>
    /// </remarks>
    public string[] AllowedOrigins { get; set; }

    /// <summary>
    /// Gets or sets the array of allowed HTTP methods (e.g., "GET", "POST", "PUT").
    /// </summary>
    /// <remarks>
    /// Specifies which HTTP methods are allowed for cross-origin requests.
    /// Cannot be used when <see cref="AllowAnyMethod"/> is true.
    /// If not specified and <see cref="AllowAnyMethod"/> is false, defaults to GET only.
    /// <para>
    /// Common values: "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS"
    /// </para>
    /// Example: ["GET", "POST", "PUT", "DELETE"]
    /// </remarks>
    public string[] AllowedMethods { get; set; }

    /// <summary>
    /// Gets or sets the array of allowed request headers (e.g., "Content-Type", "Authorization").
    /// </summary>
    /// <remarks>
    /// Specifies which headers can be sent in cross-origin requests.
    /// Cannot be used when <see cref="AllowAnyHeader"/> is true.
    /// Simple headers (Accept, Accept-Language, Content-Language, Content-Type) are always allowed.
    /// <para>
    /// Example: ["Content-Type", "Authorization", "X-Custom-Header"]
    /// </para>
    /// </remarks>
    public string[] AllowedHeaders { get; set; }

    /// <summary>
    /// Gets or sets the array of headers exposed to the browser (e.g., "X-Total-Count").
    /// </summary>
    /// <remarks>
    /// Specifies which response headers the browser should expose to JavaScript.
    /// By default, only simple response headers are exposed (Cache-Control, Content-Language, Content-Type, Expires, Last-Modified, Pragma).
    /// Use this to expose custom headers like pagination info or request IDs.
    /// <para>
    /// Example: ["X-Total-Count", "X-Page-Number", "X-Request-Id"]
    /// </para>
    /// </remarks>
    public string[] ExposeHeaders { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether credentials (cookies, authorization headers) are allowed.
    /// </summary>
    /// <remarks>
    /// When true, allows credentials to be sent in cross-origin requests.
    /// <para>
    /// WARNING: Cannot be used with <see cref="AllowAnyOrigin"/> = true (violates CORS specification).
    /// When using credentials, <see cref="AllowedOrigins"/> must specify exact origins (no wildcards except subdomains).
    /// </para>
    /// <para>
    /// Use Cases:
    /// - Frontend applications that send authentication tokens or cookies
    /// - APIs that require user authentication for cross-origin requests
    /// </para>
    /// Default: false (null treated as false)
    /// </remarks>
    public bool? AllowCredentials { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether any origin is allowed.
    /// </summary>
    /// <remarks>
    /// When true, allows cross-origin requests from any origin (*).
    /// <para>
    /// WARNING: Cannot be used with <see cref="AllowCredentials"/> = true (violates CORS specification).
    /// </para>
    /// <para>
    /// SECURITY: Only use in development or for public APIs that don't require authentication.
    /// For production applications with user data, always specify exact origins in <see cref="AllowedOrigins"/>.
    /// </para>
    /// Overrides <see cref="AllowedOrigins"/> when true.
    /// Default: false (null treated as false)
    /// </remarks>
    public bool? AllowAnyOrigin { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether any HTTP method is allowed.
    /// </summary>
    /// <remarks>
    /// When true, allows all HTTP methods (GET, POST, PUT, DELETE, PATCH, etc.).
    /// Overrides <see cref="AllowedMethods"/> when true.
    /// Simplifies configuration for APIs that support multiple methods.
    /// Default: false (null treated as false)
    /// </remarks>
    public bool? AllowAnyMethod { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether any header is allowed.
    /// </summary>
    /// <remarks>
    /// When true, allows any header in cross-origin requests.
    /// Overrides <see cref="AllowedHeaders"/> when true.
    /// Simplifies configuration but reduces control over what headers clients can send.
    /// Default: false (null treated as false)
    /// </remarks>
    public bool? AllowAnyHeader { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether wildcard subdomain matching is enabled.
    /// </summary>
    /// <remarks>
    /// When true, origins in <see cref="AllowedOrigins"/> are treated as base domains allowing any subdomain.
    /// <para>
    /// Example: "https://example.com" allows:
    /// - "https://api.example.com"
    /// - "https://app.example.com"
    /// - "https://admin.example.com"
    /// </para>
    /// <para>
    /// Requires <see cref="AllowedOrigins"/> to be specified (does not work with <see cref="AllowAnyOrigin"/>).
    /// Compatible with <see cref="AllowCredentials"/> = true.
    /// </para>
    /// Default: false (null treated as false)
    /// </remarks>
    public bool? AllowWildcardSubdomains { get; set; }

    /// <summary>
    /// Gets or sets the preflight cache duration in seconds.
    /// </summary>
    /// <remarks>
    /// Specifies how long (in seconds) the browser can cache the preflight OPTIONS request result.
    /// Reduces preflight requests for subsequent cross-origin requests from the same origin.
    /// <para>
    /// If not specified, browser default is used (typically 5 seconds).
    /// Recommended: 600-3600 seconds for production to reduce overhead.
    /// </para>
    /// <para>
    /// Example: 3600 (1 hour)
    /// </para>
    /// </remarks>
    public int? PreflightMaxAgeSeconds { get; set; }
}
