// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>Configures the common Apply and Reset controls without owning filter state.</summary>
/// <param name="ApplyId">The optional Apply button identifier.</param>
/// <param name="ResetId">The optional Reset control identifier.</param>
/// <param name="ResetUrl">A reset destination, or null for a feature-owned click handler.</param>
/// <param name="Submit">Whether Apply submits the owning form.</param>
/// <example><code>new DashboardFilterActionsModel("messages-apply", "messages-reset", Submit: false);</code></example>
public sealed record DashboardFilterActionsModel(string ApplyId = null, string ResetId = null, string ResetUrl = null, bool Submit = true);
