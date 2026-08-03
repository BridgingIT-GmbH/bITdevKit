// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

/// <summary>
/// Manages persistent console command history storage and retrieval.
/// </summary>
/// <example>
/// <code>
/// ConsoleCommandHistory.Initialize("WeatherFiesta");
/// ConsoleCommandHistory.Append("storage blobs clients");
/// </code>
/// </example>
public static class ConsoleCommandHistory
{
    private static readonly object sync = new();
    private static readonly List<string> items = [];
    private static string filePath;
    private static string historyKey;

    /// <summary>
    /// Gets the last history storage error, when one occurred.
    /// </summary>
    /// <example>
    /// <code>
    /// var error = ConsoleCommandHistory.LastError;
    /// </code>
    /// </example>
    public static string LastError { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the active history file path.
    /// </summary>
    /// <example>
    /// <code>
    /// var path = ConsoleCommandHistory.FilePath;
    /// </code>
    /// </example>
    public static string FilePath
    {
        get
        {
            EnsureInitialized();

            lock (sync)
            {
                return filePath;
            }
        }
    }

    /// <summary>
    /// Initializes history storage for the current application.
    /// </summary>
    /// <param name="assemblyName">The application or assembly name used to isolate history files.</param>
    /// <example>
    /// <code>
    /// ConsoleCommandHistory.Initialize("WeatherFiesta");
    /// </code>
    /// </example>
    public static void Initialize(string assemblyName = null)
    {
        lock (sync)
        {
            var key = NormalizeHistoryKey(assemblyName);
            if (string.Equals(historyKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            historyKey = key;
            items.Clear();
            filePath = GetHistoryFilePath(key);

            LoadFromFile();
        }
    }

    /// <summary>
    /// Reloads the active history file into memory.
    /// </summary>
    /// <example>
    /// <code>
    /// ConsoleCommandHistory.Reload();
    /// </code>
    /// </example>
    public static void Reload()
    {
        EnsureInitialized();

        lock (sync)
        {
            LoadFromFile();
        }
    }

    /// <summary>
    /// Appends one command line to the in-memory and persisted history.
    /// </summary>
    /// <param name="line">The command line to append.</param>
    /// <example>
    /// <code>
    /// ConsoleCommandHistory.Append("help storage");
    /// </code>
    /// </example>
    public static void Append(string line)
    {
        var normalizedLine = NormalizeHistoryLine(line);
        if (string.IsNullOrWhiteSpace(normalizedLine))
        {
            return;
        }

        EnsureInitialized();

        lock (sync)
        {
            MoveToEnd(normalizedLine);
            try
            {
                SaveItems();
            }
            catch (Exception ex)
            {
                RecordError(ex);
            }
        }
    }

    /// <summary>
    /// Gets all loaded history entries.
    /// </summary>
    /// <returns>The loaded command history.</returns>
    /// <example>
    /// <code>
    /// var entries = ConsoleCommandHistory.GetAll();
    /// </code>
    /// </example>
    public static IReadOnlyList<string> GetAll()
    {
        EnsureInitialized();

        lock (sync)
        {
            return items.ToList();
        }
    }

    /// <summary>
    /// Clears history while optionally keeping the last entries.
    /// </summary>
    /// <param name="keepLast">The number of most recent entries to keep.</param>
    /// <example>
    /// <code>
    /// ConsoleCommandHistory.ClearKeepLast(10);
    /// </code>
    /// </example>
    public static void ClearKeepLast(int keepLast)
    {
        EnsureInitialized();

        lock (sync)
        {
            if (keepLast <= 0)
            {
                items.Clear();
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (Exception ex)
                {
                    RecordError(ex);
                }
                return;
            }

            if (keepLast >= items.Count)
            {
                return;
            }

            var retained = items.Skip(Math.Max(0, items.Count - keepLast)).ToList();
            items.Clear();
            items.AddRange(retained);
            try
            {
                SaveItems();
            }
            catch (Exception ex)
            {
                RecordError(ex);
            }
        }
    }

    private static void EnsureInitialized()
    {
        if (!string.IsNullOrWhiteSpace(historyKey))
        {
            return;
        }

        Initialize(Assembly.GetEntryAssembly()?.GetName().Name);
    }

    private static string NormalizeHistoryKey(string assemblyName)
    {
        var value = string.IsNullOrWhiteSpace(assemblyName) ? "unknown" : assemblyName.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        return builder.Length == 0 ? "unknown" : builder.ToString();
    }

    private static string GetHistoryFilePath(string key)
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                return Path.Combine(localAppData, "bdk", "console", $"{key}.txt");
            }
        }
        catch (Exception ex)
        {
            RecordError(ex);
        }

        return GetLegacyTempHistoryFilePath(key);
    }

    private static string GetLegacyTempHistoryFilePath(string key) =>
        Path.Combine(Path.GetTempPath(), $"bitdevkit_cli_history_{key}.txt");

    private static void LoadFromFile()
    {
        items.Clear();
        if (!File.Exists(filePath))
        {
            return;
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var normalizedLine = NormalizeHistoryLine(line);
                if (!string.IsNullOrWhiteSpace(normalizedLine))
                {
                    MoveToEnd(normalizedLine);
                }
            }

            if (items.Count != lines.Count(line => !string.IsNullOrWhiteSpace(NormalizeHistoryLine(line))))
            {
                SaveItems();
            }
        }
        catch (Exception ex)
        {
            RecordError(ex);
        }
    }

    private static void MoveToEnd(string line)
    {
        items.RemoveAll(item => string.Equals(item, line, StringComparison.Ordinal));
        items.Add(line);
    }

    private static string NormalizeHistoryLine(string line) =>
        string.IsNullOrWhiteSpace(line) ? string.Empty : line.Trim();

    private static void SaveItems()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        File.WriteAllLines(filePath, items);
    }

    private static void RecordError(Exception ex)
    {
        if (string.IsNullOrWhiteSpace(LastError))
        {
            LastError = ex.Message;
        }
    }
}
