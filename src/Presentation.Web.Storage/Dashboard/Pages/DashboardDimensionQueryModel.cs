// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Dashboard.Pages;

/// <summary>Presentation settings for the storage dashboard's typed condition editor.</summary>
/// <param name="FilterJson">The applied feature-owned filter representation.</param>
/// <param name="SupportsQueries">Whether the selected provider supports Dimension queries.</param>
/// <param name="Operators">The selected provider's supported operators by scalar type.</param>
/// <example><code>var model = new DashboardDimensionQueryModel(null, capabilities.SupportsQueries, capabilities.SupportedOperators);</code></example>
public sealed record DashboardDimensionQueryModel(
    string FilterJson,
    bool SupportsQueries,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Operators);
