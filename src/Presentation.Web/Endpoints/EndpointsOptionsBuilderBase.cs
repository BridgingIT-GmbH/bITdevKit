// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

/// <summary>
///     Provides a fluent builder base for endpoint option types.
/// </summary>
/// <typeparam name="TOptions">The endpoint options type being configured.</typeparam>
/// <typeparam name="TBuilder">The concrete builder type.</typeparam>
/// <remarks>
///     Each method mutates the underlying <see cref="OptionsBuilder{T}.Target" /> instance and returns the concrete
///     builder type so endpoint options can be configured in a fluent chain.
/// </remarks>
public abstract class EndpointsOptionsBuilderBase<TOptions, TBuilder> : OptionsBuilder<TOptions>
    where TOptions : EndpointsOptionsBase, new()
    where TBuilder : EndpointsOptionsBuilderBase<TOptions, TBuilder>
{
    /// <summary>
    /// Enables or disables endpoint registration.
    /// </summary>
    /// <param name="enabled">The endpoint registration state.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    ///     This value is later used by endpoint implementations to decide whether their route mappings should be skipped.
    /// </remarks>
    public TBuilder Enabled(bool enabled = true)
    {
        this.Target.Enabled = enabled;

        return (TBuilder)this;
    }

    /// <summary>
    /// Sets the route group path.
    /// </summary>
    /// <param name="path">The route group path.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    ///     The configured value is passed to <see cref="Microsoft.AspNetCore.Routing.IEndpointRouteBuilder.MapGroup" /> by
    ///     <see cref="EndpointsBase.MapGroup" />.
    /// </remarks>
    public TBuilder GroupPath(string path)
    {
        this.Target.GroupPath = path;

        return (TBuilder)this;
    }

    /// <summary>
    /// Sets the route group tag.
    /// </summary>
    /// <param name="tag">The route group tag.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    ///     The tag is applied to the route group for endpoint metadata and generated API descriptions.
    /// </remarks>
    public TBuilder GroupTag(string tag)
    {
        this.Target.GroupTag = tag;

        return (TBuilder)this;
    }

    /// <summary>
    /// Requires authorization for the endpoint group.
    /// </summary>
    /// <param name="enabled">Indicates whether authorization is required.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    ///     When enabled, <see cref="EndpointsBase.MapGroup" /> applies role authorization first, then policy authorization,
    ///     and finally default authorization when no roles or policy are configured.
    /// </remarks>
    public TBuilder RequireAuthorization(bool enabled = true)
    {
        this.Target.RequireAuthorization = enabled;

        return (TBuilder)this;
    }

    /// <summary>
    /// Excludes the endpoint group from API descriptions.
    /// </summary>
    /// <param name="excluded">Indicates whether the endpoint group should be excluded.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    ///     When set to <c>true</c>, the route group is excluded from generated endpoint descriptions such as OpenAPI output.
    /// </remarks>
    public TBuilder ExcludeFromDescription(bool excluded = true)
    {
        this.Target.ExcludeFromDescription = excluded;

        return (TBuilder)this;
    }

    /// <summary>
    /// Requires one or more roles for the endpoint group.
    /// </summary>
    /// <param name="roles">The required roles.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    ///     A <c>null</c> role array is normalized to an empty array. Roles are only applied when authorization is enabled for
    ///     the endpoint group.
    /// </remarks>
    public TBuilder RequireRoles(params string[] roles)
    {
        this.Target.RequireRoles = roles ?? [];

        return (TBuilder)this;
    }

    /// <summary>
    /// Requires an authorization policy for the endpoint group.
    /// </summary>
    /// <param name="policy">The required policy.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    ///     The policy is applied only when authorization is enabled and no roles were configured for the endpoint group.
    /// </remarks>
    public TBuilder RequirePolicy(string policy)
    {
        this.Target.RequirePolicy = policy;

        return (TBuilder)this;
    }
}
