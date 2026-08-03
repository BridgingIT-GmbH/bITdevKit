// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;

/// <summary>
/// Stores and exposes the process-wide native console theme.
/// </summary>
/// <example>
/// <code>
/// ConsoleTheme.Set("matrix");
/// var current = ConsoleTheme.Current;
/// </code>
/// </example>
public static class ConsoleTheme
{
    private static readonly object sync = new();
    private static ConsoleThemePalette current;
    private static bool loaded;

    /// <summary>
    /// Gets the currently selected console theme.
    /// </summary>
    /// <example>
    /// <code>
    /// var promptStyle = ConsoleTheme.Current.PromptStyle;
    /// </code>
    /// </example>
    public static ConsoleThemePalette Current
    {
        get
        {
            EnsureLoaded();
            lock (sync)
            {
                return current;
            }
        }
    }

    /// <summary>
    /// Gets the file path used to persist the native console theme.
    /// </summary>
    /// <example>
    /// <code>
    /// var path = ConsoleTheme.FilePath;
    /// </code>
    /// </example>
    public static string FilePath => Path.Combine(GetConsoleDirectory(), "theme.txt");

    /// <summary>
    /// Changes the current console theme and persists it.
    /// </summary>
    /// <param name="name">The theme name.</param>
    /// <returns><c>true</c> when the theme exists and was applied; otherwise <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// ConsoleTheme.Set("carbon");
    /// </code>
    /// </example>
    public static bool Set(string name)
    {
        if (!ConsoleThemeRegistry.TryGet(name, out var theme))
        {
            return false;
        }

        lock (sync)
        {
            current = theme;
            loaded = true;
        }

        Save(theme.Name);
        return true;
    }

    /// <summary>
    /// Applies the current DevKit console theme to Spectre.Console rendering.
    /// </summary>
    /// <param name="console">The console instance to configure.</param>
    /// <returns>The same console instance for chaining.</returns>
    /// <example>
    /// <code>
    /// var console = ConsoleTheme.ApplyTo(AnsiConsole.Create(settings));
    /// </code>
    /// </example>
    public static IAnsiConsole ApplyTo(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);

        console.Pipeline.Attach(new ConsoleThemeRenderHook());

        return console;
    }

    /// <summary>
    /// Reloads the current theme from the persisted console preference.
    /// </summary>
    /// <example>
    /// <code>
    /// ConsoleTheme.Reload();
    /// </code>
    /// </example>
    public static void Reload()
    {
        lock (sync)
        {
            loaded = false;
            current = null;
        }

        EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        lock (sync)
        {
            if (loaded)
            {
                return;
            }

            var name = ReadPersistedThemeName();
            current = ConsoleThemeRegistry.Get(name);
            loaded = true;
        }
    }

    private static string ReadPersistedThemeName()
    {
        try
        {
            var path = FilePath;
            return File.Exists(path)
                ? File.ReadAllText(path).Trim()
                : ConsoleThemeRegistry.DefaultThemeName;
        }
        catch
        {
            return ConsoleThemeRegistry.DefaultThemeName;
        }
    }

    private static void Save(string name)
    {
        try
        {
            var directory = GetConsoleDirectory();
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "theme.txt"), name);
        }
        catch
        {
            // Theme persistence is a convenience preference; terminal output must keep working.
        }
    }

    private static string GetConsoleDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.GetTempPath();
        }

        return Path.Combine(root, "bdk", "console");
    }
}
