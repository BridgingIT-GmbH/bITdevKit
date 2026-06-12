// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
///     Provides shared configuration for endpoint classes that map routes through <see cref="EndpointsBase.MapGroup" />.
/// </summary>
/// <remarks>
///     The options describe how an endpoint group is created and which metadata is applied to it. The group configuration
///     is additive and optional: unset values are ignored, while configured values are applied to the generated
///     <c>RouteGroupBuilder</c>. Authorization is applied only when <see cref="RequireAuthorization" /> is enabled, unless
///     <see cref="AllowAnonymous" /> is set, in which case anonymous metadata is added and authorization metadata is skipped.
///
///     Example:
///     <code>
///     public sealed class OrdersEndpointsOptions : EndpointsOptionsBase
///     {
///         public OrdersEndpointsOptions()
///         {
///             GroupPath = "/api/orders";
///             GroupTag = "Orders";
///             RequireAuthorization = true;
///             RequirePolicy = "Orders.Read";
///         }
///     }
///     </code>
/// </remarks>
public abstract class EndpointsOptionsBase
{
    /// <summary>
    ///     Gets or sets whether endpoint registration for the owning endpoint class is enabled.
    /// </summary>
    /// <remarks>
    ///     Endpoint implementations commonly read this value before mapping their routes. The default value is
    ///     <c>true</c>.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the base route path used when creating the endpoint group.
    /// </summary>
    /// <remarks>
    ///     The value is passed to <c>MapGroup</c>. When <see cref="NormalizeGroupPath" /> is disabled, the value is used as
    ///     supplied. When normalization is enabled, duplicate separators, backslashes, and leading or trailing separators
    ///     are normalized before mapping.
    /// </remarks>
    public string GroupPath { get; set; } = "/api";

    /// <summary>
    ///     Gets or sets whether <see cref="GroupPath" /> should be normalized before the route group is created.
    /// </summary>
    /// <remarks>
    ///     Normalization converts backslashes to forward slashes, removes empty path segments, and ensures the mapped group
    ///     path starts with a leading slash. Empty or whitespace paths normalize to <c>/</c>.
    /// </remarks>
    public bool NormalizeGroupPath { get; set; }

    /// <summary>
    ///     Gets or sets the primary OpenAPI tag applied to the endpoint group.
    /// </summary>
    /// <remarks>
    ///     The value is combined with <see cref="GroupTags" /> and passed to <c>WithTags</c>. Existing behavior is preserved
    ///     by keeping this singular tag property as the first configured tag.
    /// </remarks>
    public string GroupTag { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets additional OpenAPI tags applied to the endpoint group.
    /// </summary>
    /// <remarks>
    ///     These tags are appended after <see cref="GroupTag" /> when the group is configured. Empty or whitespace values are
    ///     not filtered by the option itself.
    /// </remarks>
    public string[] GroupTags { get; set; } = [];

    /// <summary>
    ///     Gets or sets the OpenAPI group name applied to all endpoints in the group.
    /// </summary>
    /// <remarks>
    ///     When set, the value is applied through <c>WithGroupName</c>. Empty values are ignored.
    /// </remarks>
    public string GroupName { get; set; }

    /// <summary>
    ///     Gets or sets the OpenAPI summary applied to the group.
    /// </summary>
    /// <remarks>
    ///     When set, the value is applied through <c>WithSummary</c>. Empty values are ignored.
    /// </remarks>
    public string Summary { get; set; }

    /// <summary>
    ///     Gets or sets the OpenAPI description applied to the group.
    /// </summary>
    /// <remarks>
    ///     When set, the value is applied through <c>WithDescription</c>. Empty values are ignored.
    /// </remarks>
    public string Description { get; set; }

    /// <summary>
    ///     Gets or sets whether the group should be marked as deprecated.
    /// </summary>
    /// <remarks>
    ///     When enabled, <see cref="ObsoleteAttribute" /> metadata is added to the group. Consumers of endpoint metadata and
    ///     OpenAPI generators can use this marker to surface deprecation information.
    /// </remarks>
    public bool Deprecated { get; set; }

    /// <summary>
    ///     Gets or sets the optional prefix used by endpoint implementations when building endpoint names.
    /// </summary>
    /// <remarks>
    ///     The prefix is exposed as <see cref="EndpointRouteNamePrefixMetadata" /> on the group. Endpoint implementations
    ///     should call <see cref="EndpointsBase.BuildRouteName" /> when assigning route names to include the prefix.
    /// </remarks>
    public string RouteNamePrefix { get; set; }

    /// <summary>
    ///     Gets or sets whether authorization metadata should be applied to the group.
    /// </summary>
    /// <remarks>
    ///     When enabled, <see cref="EndpointsBase.MapGroup" /> applies role, policy, authentication scheme, or default
    ///     authorization metadata based on the remaining authorization options. This value is ignored when
    ///     <see cref="AllowAnonymous" /> is enabled.
    /// </remarks>
    public bool RequireAuthorization { get; set; }

    /// <summary>
    ///     Gets or sets whether anonymous access metadata should be applied to the group.
    /// </summary>
    /// <remarks>
    ///     When enabled, <c>AllowAnonymous</c> metadata is added and authorization metadata is not applied, even if
    ///     <see cref="RequireAuthorization" /> is also enabled.
    /// </remarks>
    public bool AllowAnonymous { get; set; }

    /// <summary>
    ///     Gets or sets whether the group should be hidden from endpoint descriptions and OpenAPI output.
    /// </summary>
    /// <remarks>
    ///     When enabled, the group is configured with <c>ExcludeFromDescription</c>.
    /// </remarks>
    public bool ExcludeFromDescription { get; set; }

    /// <summary>
    ///     Gets or sets the roles required to access the group.
    /// </summary>
    /// <remarks>
    ///     Blank role entries are ignored when authorization metadata is created. Roles take precedence over
    ///     <see cref="RequirePolicy" /> when both are configured.
    /// </remarks>
    public string[] RequireRoles { get; set; } = []; // roles

    /// <summary>
    ///     Gets or sets the authentication schemes used by group authorization metadata.
    /// </summary>
    /// <remarks>
    ///     Blank scheme entries are ignored. When roles or a policy are configured, the schemes are added to the same
    ///     <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute" />. When only schemes are configured, the group
    ///     requires authorization using those schemes.
    /// </remarks>
    public string[] RequireAuthenticationSchemes { get; set; } = [];

    /// <summary>
    ///     Gets or sets the authorization policy required to access the group.
    /// </summary>
    /// <remarks>
    ///     The policy is applied when <see cref="RequireAuthorization" /> is enabled and no non-blank roles are configured.
    ///     Empty values are ignored.
    /// </remarks>
    public string RequirePolicy { get; set; }

    /// <summary>
    ///     Gets or sets the named CORS policy required for the group.
    /// </summary>
    /// <remarks>
    ///     The policy is applied through <c>RequireCors</c> unless <see cref="DisableCors" /> is enabled. Empty values are
    ///     ignored.
    /// </remarks>
    public string RequireCorsPolicy { get; set; }

    /// <summary>
    ///     Gets or sets whether CORS should be disabled for the group.
    /// </summary>
    /// <remarks>
    ///     When enabled, disable-CORS metadata is added and <see cref="RequireCorsPolicy" /> is not applied.
    /// </remarks>
    public bool DisableCors { get; set; }

    /// <summary>
    ///     Gets or sets the named rate limiting policy required for the group.
    /// </summary>
    /// <remarks>
    ///     The policy is applied through <c>RequireRateLimiting</c> unless <see cref="DisableRateLimiting" /> is enabled.
    ///     Empty values are ignored.
    /// </remarks>
    public string RequireRateLimitingPolicy { get; set; }

    /// <summary>
    ///     Gets or sets whether rate limiting should be disabled for the group.
    /// </summary>
    /// <remarks>
    ///     When enabled, disable-rate-limiting metadata is added and <see cref="RequireRateLimitingPolicy" /> is not applied.
    /// </remarks>
    public bool DisableRateLimiting { get; set; }

    /// <summary>
    ///     Gets custom metadata objects that are added to the endpoint group.
    /// </summary>
    /// <remarks>
    ///     Metadata entries are applied after the built-in group configuration. Null entries are ignored by
    ///     <see cref="EndpointsBase.MapGroup" />.
    /// </remarks>
    public IList<object> Metadata { get; } = [];
}