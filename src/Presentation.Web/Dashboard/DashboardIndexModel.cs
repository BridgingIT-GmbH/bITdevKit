// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>Groups overview cards and selects the requested group's cards.</summary>
/// <example><code>var model = new DashboardIndexModel(cards, "Operations");</code></example>
public sealed class DashboardIndexModel
{
    /// <summary>Creates an overview, falling back to the first group for an unknown selection.</summary>
    /// <param name="cards">The available plugin cards.</param>
    /// <param name="selectedGroup">The requested group name.</param>
    public DashboardIndexModel(IReadOnlyList<DashboardPageCard> cards, string selectedGroup = null)
    {
        var groups = cards
            .OrderBy(card => card.GroupOrder)
            .ThenBy(card => card.Group, StringComparer.OrdinalIgnoreCase)
            .GroupBy(card => string.IsNullOrWhiteSpace(card.Group) ? "Dashboard" : card.Group, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        this.Groups = groups.Select(group => group.Key).ToArray();
        var selected = groups.FirstOrDefault(group => string.Equals(group.Key, selectedGroup, StringComparison.OrdinalIgnoreCase))
            ?? groups.FirstOrDefault();
        this.SelectedGroup = selected?.Key;
        this.Cards = selected?
            .OrderBy(card => card.Order)
            .ThenBy(card => card.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    /// <summary>Gets the available group names in navigation order.</summary>
    public IReadOnlyList<string> Groups { get; }

    /// <summary>Gets the selected group name, or null when no cards are available.</summary>
    public string SelectedGroup { get; }

    /// <summary>Gets the selected group's cards in display order.</summary>
    public IReadOnlyList<DashboardPageCard> Cards { get; }
}
