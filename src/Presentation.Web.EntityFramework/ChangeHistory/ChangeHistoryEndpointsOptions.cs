// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory;

/// <summary>
/// Configures ChangeHistory HTTP endpoints for one entity and EF Core context.
/// </summary>
/// <example>
/// <code>
/// services.AddChangeHistoryEndpoints&lt;Customer, AppDbContext&gt;(options =&gt; options
///     .GroupPath("/_bdk/api/customers/history")
///     .RequireReadPolicy("Customers.History.Read")
///     .RequireRestorePolicy("Customers.History.Restore"));
/// </code>
/// </example>
public class ChangeHistoryEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeHistoryEndpointsOptions" /> class.
    /// </summary>
    public ChangeHistoryEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/change-history";
        this.GroupTag = "_bdk.ChangeHistory";
        this.RouteNamePrefix = "_bdk.ChangeHistory";
    }

    /// <summary>
    /// Gets or sets the optional authorization policy required by read endpoints.
    /// </summary>
    public string ReadPolicy { get; set; }

    /// <summary>
    /// Gets or sets the optional authorization policy required by restore endpoints.
    /// </summary>
    public string RestorePolicy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether query endpoints include serialized old/new values.
    /// </summary>
    public bool IncludeValues { get; set; }
}

/// <summary>
/// Provides a fluent builder for <see cref="ChangeHistoryEndpointsOptions" />.
/// </summary>
/// <example>
/// <code>
/// var options = new ChangeHistoryEndpointsOptionsBuilder()
///     .GroupPath("/api/customers/history")
///     .RequireReadPolicy("Customers.Read")
///     .Build();
/// </code>
/// </example>
public class ChangeHistoryEndpointsOptionsBuilder
    : EndpointsOptionsBuilderBase<ChangeHistoryEndpointsOptions, ChangeHistoryEndpointsOptionsBuilder>
{
    /// <summary>
    /// Configures the authorization policy required to query ChangeHistory rows.
    /// </summary>
    /// <param name="policy">The authorization policy name.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEndpointsOptionsBuilder RequireReadPolicy(string policy)
    {
        this.Target.ReadPolicy = policy;

        return this;
    }

    /// <summary>
    /// Configures the authorization policy required to restore a ChangeHistory change set.
    /// </summary>
    /// <param name="policy">The authorization policy name.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEndpointsOptionsBuilder RequireRestorePolicy(string policy)
    {
        this.Target.RestorePolicy = policy;

        return this;
    }

    /// <summary>
    /// Configures whether query endpoints include serialized old/new values.
    /// </summary>
    /// <param name="enabled">When true, serialized values are included.</param>
    /// <returns>The current builder.</returns>
    public ChangeHistoryEndpointsOptionsBuilder IncludeValues(bool enabled = true)
    {
        this.Target.IncludeValues = enabled;

        return this;
    }
}
