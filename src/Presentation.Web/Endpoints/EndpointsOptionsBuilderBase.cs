// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

/// <summary>
///     Provides a fluent base builder for endpoint options that derive from <see cref="EndpointsOptionsBase" />.
/// </summary>
/// <typeparam name="TOptions">The concrete endpoint options type being configured.</typeparam>
/// <typeparam name="TBuilder">The concrete builder type returned from fluent methods.</typeparam>
/// <remarks>
///     Concrete endpoint option builders inherit this type to expose shared group configuration without duplicating
///     methods. Each method mutates the underlying <see cref="OptionsBuilder{T}.Target" /> instance and returns the
///     concrete builder so module-specific builder methods can be chained with the shared methods.
///
///     Example:
///     <code>
///     var options = new OrdersEndpointsOptionsBuilder()
///         .GroupPath("/api/orders")
///         .GroupTag("Orders")
///         .RequirePolicy("Orders.Read")
///         .Build();
///     </code>
/// </remarks>
public abstract class EndpointsOptionsBuilderBase<TOptions, TBuilder> : OptionsBuilder<TOptions>
    where TOptions : EndpointsOptionsBase, new()
    where TBuilder : EndpointsOptionsBuilderBase<TOptions, TBuilder>
{
    /// <summary>
    ///     Configures whether the endpoint set is enabled.
    /// </summary>
    /// <param name="enabled">When <c>true</c>, the endpoint set is available for mapping.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     This sets <see cref="EndpointsOptionsBase.Enabled" />. Endpoint implementations decide whether to read this flag
    ///     during mapping.
    /// </remarks>
    public TBuilder Enabled(bool enabled = true)
    {
        this.Target.Enabled = enabled;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures the base route path for the endpoint group.
    /// </summary>
    /// <param name="path">The route group path passed to <c>MapGroup</c>.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     The path is assigned as provided. Use <see cref="NormalizeGroupPath" /> to normalize separators before mapping.
    /// </remarks>
    public TBuilder GroupPath(string path)
    {
        this.Target.GroupPath = path;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures whether the route group path is normalized before mapping.
    /// </summary>
    /// <param name="enabled">When <c>true</c>, path normalization is enabled.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Normalization is performed by <see cref="EndpointsBase.MapGroup" /> and affects only the mapped route group path,
    ///     not the stored <see cref="EndpointsOptionsBase.GroupPath" /> value.
    /// </remarks>
    public TBuilder NormalizeGroupPath(bool enabled = true)
    {
        this.Target.NormalizeGroupPath = enabled;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures the primary OpenAPI tag for the endpoint group.
    /// </summary>
    /// <param name="tag">The primary tag value.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     The tag is applied before any values configured with <see cref="GroupTags" />.
    /// </remarks>
    public TBuilder GroupTag(string tag)
    {
        this.Target.GroupTag = tag;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures additional OpenAPI tags for the endpoint group.
    /// </summary>
    /// <param name="tags">Additional tag values. A null array is normalized to an empty array.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     These tags are appended after <see cref="EndpointsOptionsBase.GroupTag" /> when the group is configured.
    /// </remarks>
    public TBuilder GroupTags(params string[] tags)
    {
        this.Target.GroupTags = tags ?? [];

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures the OpenAPI group name applied to endpoints in the group.
    /// </summary>
    /// <param name="name">The group name to apply.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Empty values are stored but ignored when <see cref="EndpointsBase.MapGroup" /> configures the route group.
    /// </remarks>
    public TBuilder GroupName(string name)
    {
        this.Target.GroupName = name;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures the OpenAPI summary applied to the endpoint group.
    /// </summary>
    /// <param name="summary">The summary text to apply.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Empty values are stored but ignored when the route group is configured.
    /// </remarks>
    public TBuilder Summary(string summary)
    {
        this.Target.Summary = summary;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures the OpenAPI description applied to the endpoint group.
    /// </summary>
    /// <param name="description">The description text to apply.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Empty values are stored but ignored when the route group is configured.
    /// </remarks>
    public TBuilder Description(string description)
    {
        this.Target.Description = description;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures whether the endpoint group is marked as deprecated.
    /// </summary>
    /// <param name="deprecated">When <c>true</c>, deprecation metadata is added to the group.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Deprecation is represented by <see cref="ObsoleteAttribute" /> metadata on the route group.
    /// </remarks>
    public TBuilder Deprecated(bool deprecated = true)
    {
        this.Target.Deprecated = deprecated;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures the prefix used when endpoint implementations build route names.
    /// </summary>
    /// <param name="prefix">The prefix to store as group metadata.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     The prefix is not applied automatically by ASP.NET Core. Endpoint implementations should call
    ///     <see cref="EndpointsBase.BuildRouteName" /> before passing names to <c>WithName</c>.
    /// </remarks>
    public TBuilder RouteNamePrefix(string prefix)
    {
        this.Target.RouteNamePrefix = prefix;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures whether authorization metadata is required for the endpoint group.
    /// </summary>
    /// <param name="enabled">When <c>true</c>, authorization metadata is applied during group mapping.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Authorization metadata is built from roles, policy, and authentication schemes. It is skipped when
    ///     <see cref="AllowAnonymous" /> is enabled.
    /// </remarks>
    public TBuilder RequireAuthorization(bool enabled = true)
    {
        this.Target.RequireAuthorization = enabled;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures whether the endpoint group allows anonymous access.
    /// </summary>
    /// <param name="allowed">When <c>true</c>, anonymous access metadata is applied.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Anonymous access takes precedence over authorization metadata when the route group is configured.
    /// </remarks>
    public TBuilder AllowAnonymous(bool allowed = true)
    {
        this.Target.AllowAnonymous = allowed;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures whether endpoints in the group are excluded from endpoint descriptions.
    /// </summary>
    /// <param name="excluded">When <c>true</c>, the group is excluded from generated descriptions such as OpenAPI.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     The value is applied through <c>ExcludeFromDescription</c> when the group is mapped.
    /// </remarks>
    public TBuilder ExcludeFromDescription(bool excluded = true)
    {
        this.Target.ExcludeFromDescription = excluded;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures the roles required to access the endpoint group.
    /// </summary>
    /// <param name="roles">The role names to require. A null array is normalized to an empty array.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Blank role names are ignored when authorization metadata is created. Roles take precedence over a configured
    ///     policy.
    /// </remarks>
    public TBuilder RequireRoles(params string[] roles)
    {
        this.Target.RequireRoles = roles ?? [];

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures authentication schemes used by group authorization metadata.
    /// </summary>
    /// <param name="schemes">The authentication scheme names. A null array is normalized to an empty array.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Blank scheme names are ignored when authorization metadata is created.
    /// </remarks>
    public TBuilder RequireAuthenticationSchemes(params string[] schemes)
    {
        this.Target.RequireAuthenticationSchemes = schemes ?? [];

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures the authorization policy required to access the endpoint group.
    /// </summary>
    /// <param name="policy">The policy name to require.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     The policy is applied only when authorization is required and no non-blank roles are configured.
    /// </remarks>
    public TBuilder RequirePolicy(string policy)
    {
        this.Target.RequirePolicy = policy;


        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures the named CORS policy required for the endpoint group.
    /// </summary>
    /// <param name="policy">The CORS policy name to require.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     The policy is ignored when <see cref="DisableCors" /> is enabled.
    /// </remarks>
    public TBuilder RequireCorsPolicy(string policy)
    {
        this.Target.RequireCorsPolicy = policy;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures whether CORS is disabled for the endpoint group.
    /// </summary>
    /// <param name="disabled">When <c>true</c>, disable-CORS metadata is applied.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Disabled CORS takes precedence over a configured CORS policy when the group is mapped.
    /// </remarks>
    public TBuilder DisableCors(bool disabled = true)
    {
        this.Target.DisableCors = disabled;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures the named rate limiting policy required for the endpoint group.
    /// </summary>
    /// <param name="policy">The rate limiting policy name to require.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     The policy is ignored when <see cref="DisableRateLimiting" /> is enabled.
    /// </remarks>
    public TBuilder RequireRateLimitingPolicy(string policy)
    {
        this.Target.RequireRateLimitingPolicy = policy;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Configures whether rate limiting is disabled for the endpoint group.
    /// </summary>
    /// <param name="disabled">When <c>true</c>, disable-rate-limiting metadata is applied.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Disabled rate limiting takes precedence over a configured rate limiting policy when the group is mapped.
    /// </remarks>
    public TBuilder DisableRateLimiting(bool disabled = true)
    {
        this.Target.DisableRateLimiting = disabled;

        return (TBuilder)this;
    }

    /// <summary>
    ///     Adds custom metadata objects to the endpoint group configuration.
    /// </summary>
    /// <param name="metadata">The metadata objects to add. Null entries are ignored.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    ///     Metadata is applied to the route group after built-in group configuration. Multiple calls append additional
    ///     metadata entries to <see cref="EndpointsOptionsBase.Metadata" />.
    /// </remarks>
    public TBuilder WithMetadata(params object[] metadata)
    {
        foreach (var item in metadata.SafeNull().Where(item => item is not null))
        {
            this.Target.Metadata.Add(item);
        }

        return (TBuilder)this;
    }
}
