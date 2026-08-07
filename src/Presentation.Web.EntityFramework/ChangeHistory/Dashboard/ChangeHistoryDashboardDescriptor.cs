// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;

using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Describes one ChangeHistory endpoint registration that can be shown by the dashboard.
/// </summary>
/// <example>
/// <code>
/// var descriptor = new ChangeHistoryDashboardDescriptor(typeof(Customer), typeof(AppDbContext), options);
/// </code>
/// </example>
public sealed class ChangeHistoryDashboardDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeHistoryDashboardDescriptor" /> class.
    /// </summary>
    /// <param name="entityType">The tracked entity type.</param>
    /// <param name="contextType">The EF Core context type.</param>
    /// <param name="options">The endpoint options used by the management endpoints.</param>
    public ChangeHistoryDashboardDescriptor(
        Type entityType,
        Type contextType,
        ChangeHistoryEndpointsOptions options)
    {
        this.EntityType = entityType;
        this.ContextType = contextType;
        this.Options = options ?? new ChangeHistoryEndpointsOptions();
        this.Key = $"{contextType.FullName}:{entityType.FullName}";
    }

    /// <summary>
    /// Gets the stable dashboard key for this ChangeHistory registration.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the tracked entity type.
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// Gets the EF Core context type.
    /// </summary>
    public Type ContextType { get; }

    /// <summary>
    /// Gets the endpoint options used by the management endpoints.
    /// </summary>
    public ChangeHistoryEndpointsOptions Options { get; }

    /// <summary>
    /// Gets the display name of the tracked entity type.
    /// </summary>
    public string EntityTypeName => this.EntityType.Name;

    /// <summary>
    /// Gets the display name of the EF Core context type.
    /// </summary>
    public string ContextTypeName => this.ContextType.Name;

    /// <summary>
    /// Gets the configured management endpoint path.
    /// </summary>
    public string ManagementPath => this.Options.GroupPath;

    /// <summary>
    /// Gets a value indicating whether the descriptor is usable.
    /// </summary>
    public bool IsValid => typeof(DbContext).IsAssignableFrom(this.ContextType);
}
