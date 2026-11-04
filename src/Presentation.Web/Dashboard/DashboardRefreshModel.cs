// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>Describes shared refresh controls while the feature owns polling and URLs.</summary>
/// <param name="Prefix">Stable prefix for status, interval and refresh identifiers.</param>
/// <param name="Label">The accessible refresh action name.</param>
/// <param name="Intervals">Whether the feature offers standard refresh intervals.</param>
/// <param name="TailId">An optional feature-owned live-tail switch identifier.</param>
/// <example><code>new DashboardRefreshModel("messaging", "Refresh messaging");</code></example>
public sealed record DashboardRefreshModel(string Prefix, string Label, bool Intervals = true, string TailId = null);
