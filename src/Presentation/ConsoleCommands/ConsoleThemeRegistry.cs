// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Provides the built-in native console themes aligned with the dashboard theme names.
/// </summary>
/// <example>
/// <code>
/// var matrix = ConsoleThemeRegistry.Get("matrix");
/// </code>
/// </example>
public static class ConsoleThemeRegistry
{
    private static readonly IReadOnlyList<ConsoleThemePalette> themes =
    [
        new(
            "dark",
            "Dark",
            "cyan",
            "cyan",
            "grey",
            "grey",
            "blue",
            "white",
            "yellow",
            "red",
            "bold red"),
        new(
            "catppuccin",
            "Catppuccin",
            "blue",
            "blue",
            "grey",
            "grey",
            "magenta",
            "white",
            "yellow",
            "magenta",
            "bold magenta"),
        new(
            "darkforest",
            "Darkforest",
            "green",
            "green",
            "grey",
            "grey",
            "green",
            "white",
            "yellow",
            "red",
            "bold red"),
        new(
            "carbon",
            "Carbon",
            "blue",
            "blue",
            "grey",
            "grey",
            "blue",
            "white",
            "yellow",
            "magenta",
            "bold magenta"),
        new(
            "tokionights",
            "Tokyo Night",
            "blue",
            "blue",
            "grey",
            "grey",
            "blue",
            "white",
            "yellow",
            "purple",
            "bold purple"),
        new(
            "matrix",
            "Matrix",
            "lime",
            "lime",
            "grey",
            "grey",
            "green",
            "lime",
            "yellow",
            "red",
            "bold red"),
        new(
            "light",
            "Light",
            "blue",
            "blue",
            "grey",
            "grey",
            "blue",
            "white",
            "yellow",
            "red",
            "bold red"),
        new(
            "system",
            "System",
            "cyan",
            "cyan",
            "grey",
            "grey",
            "blue",
            "white",
            "yellow",
            "red",
            "bold red")
    ];

    /// <summary>
    /// Gets the default console theme name.
    /// </summary>
    /// <example>
    /// <code>
    /// var name = ConsoleThemeRegistry.DefaultThemeName;
    /// </code>
    /// </example>
    public const string DefaultThemeName = "dark";

    /// <summary>
    /// Gets all built-in console themes.
    /// </summary>
    /// <example>
    /// <code>
    /// foreach (var theme in ConsoleThemeRegistry.All) { }
    /// </code>
    /// </example>
    public static IReadOnlyList<ConsoleThemePalette> All => themes;

    /// <summary>
    /// Gets a theme by name.
    /// </summary>
    /// <param name="name">The theme name.</param>
    /// <returns>The matching theme, or the default theme when no match exists.</returns>
    /// <example>
    /// <code>
    /// var theme = ConsoleThemeRegistry.Get("carbon");
    /// </code>
    /// </example>
    public static ConsoleThemePalette Get(string name) =>
        TryGet(name, out var theme) ? theme : themes.First(t => t.Name == DefaultThemeName);

    /// <summary>
    /// Attempts to get a theme by name.
    /// </summary>
    /// <param name="name">The theme name.</param>
    /// <param name="theme">The matching theme when found.</param>
    /// <returns><c>true</c> when the theme exists; otherwise <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// if (ConsoleThemeRegistry.TryGet("matrix", out var theme)) { }
    /// </code>
    /// </example>
    public static bool TryGet(string name, out ConsoleThemePalette theme)
    {
        theme = themes.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        return theme is not null;
    }
}
