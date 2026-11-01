// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;

/// <summary>
/// Overrides dashboard settings for one tracked entity/context pair independently of management REST endpoints.
/// </summary>
/// <example>
/// <code>
/// services.AddChangeHistoryDashboard&lt;Customer, AppDbContext&gt;(options =&gt; options.IncludeValues = true);
/// </code>
/// </example>
public sealed class ChangeHistoryDashboardOptions
{
    /// <summary>
    /// Gets or sets whether dashboard queries include serialized old/new values. Defaults to false.
    /// </summary>
    /// <example>
    /// <code>options.IncludeValues = true;</code>
    /// </example>
    public bool IncludeValues { get; set; }
}
