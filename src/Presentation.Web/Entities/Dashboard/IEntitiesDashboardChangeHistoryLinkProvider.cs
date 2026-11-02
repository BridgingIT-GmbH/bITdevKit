// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

/// <summary>Provides the Change History dashboard link for an entity row, so the Entities page needs no direct coupling.</summary>
/// <example>
/// <code>
/// public sealed class CustomerHistoryLinkProvider : IEntitiesDashboardChangeHistoryLinkProvider
/// {
///     public string GetLink(Type entityType, string entityId) =>
///         entityType == typeof(Customer)
///             ? $"/dashboard/change-history?entityType={entityType.FullName}&amp;entityId={entityId}"
///             : null;
/// }
/// </code>
/// </example>
public interface IEntitiesDashboardChangeHistoryLinkProvider
{
    /// <summary>Gets the Change History URL for an entity, or null when the type is not tracked or the page is disabled.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id as invariant text.</param>
    /// <returns>The URL, or null.</returns>
    /// <example>
    /// <code>
    /// var link = provider.GetLink(typeof(Customer), customerId);
    /// </code>
    /// </example>
    string GetLink(Type entityType, string entityId);
}
