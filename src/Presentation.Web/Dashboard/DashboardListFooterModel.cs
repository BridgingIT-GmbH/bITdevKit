// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>Describes bounded append controls without querying feature data.</summary>
/// <param name="Prefix">The stable list instance prefix.</param>
/// <param name="BatchSize">The selected supported batch size.</param>
/// <param name="HasMore">Whether a continuation remains available.</param>
/// <param name="NextCursor">The feature-owned next cursor.</param>
/// <param name="Sizes">Supported choices, or null for the standard choices.</param>
/// <param name="SelectorId">An existing selector identifier to preserve.</param>
/// <param name="Boundary">The immutable identity boundary for this browsing session.</param>
/// <param name="LoadedCount">The currently loaded count, or null when supplied elsewhere.</param>
/// <param name="FormId">An optional owning filter form, preserving native form submission from the footer.</param>
/// <param name="SizeName">The batch-size query field name when the selector belongs to a form.</param>
/// <example><code>new DashboardListFooterModel("messages", 50, true, nextId, LoadedCount: 50);</code></example>
public sealed record DashboardListFooterModel(string Prefix, int BatchSize, bool HasMore, string NextCursor,
    IReadOnlyList<int> Sizes = null, string SelectorId = null, string Boundary = null, int? LoadedCount = null,
    string FormId = null, string SizeName = null);
