// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides a fluent builder base for endpoint option types.
/// </summary>
/// <typeparam name="TOptions">The endpoint options type being configured.</typeparam>
/// <typeparam name="TBuilder">The concrete builder type.</typeparam>
public abstract class EndpointsOptionsBuilderBase<TOptions, TBuilder> : OptionsBuilder<TOptions>
    where TOptions : EndpointsOptionsBase, new()
    where TBuilder : EndpointsOptionsBuilderBase<TOptions, TBuilder>
{
    /// <summary>
    /// Enables or disables endpoint registration.
    /// </summary>
    /// <param name="enabled">The endpoint registration state.</param>
    /// <returns>The current builder.</returns>
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
    public TBuilder RequirePolicy(string policy)
    {
        this.Target.RequirePolicy = policy;

        return (TBuilder)this;
    }
}
