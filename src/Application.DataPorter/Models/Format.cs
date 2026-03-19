// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents an extensible DataPorter format identifier.
/// </summary>
public readonly record struct Format
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Format"/> struct.
    /// </summary>
    /// <param name="key">The unique format key.</param>
    public Format(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Format key cannot be null or whitespace.", nameof(key));
        }

        this.Key = key.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Gets the normalized format key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the Microsoft Excel format identifier.
    /// </summary>
    public static Format Excel { get; } = new("excel");

    /// <summary>
    /// Gets the Comma-Separated Values format identifier.
    /// </summary>
    public static Format Csv { get; } = new("csv");

    /// <summary>
    /// Gets the typed-row Comma-Separated Values format identifier.
    /// </summary>
    public static Format CsvTyped { get; } = new("csvtyped");

    /// <summary>
    /// Gets the JavaScript Object Notation format identifier.
    /// </summary>
    public static Format Json { get; } = new("json");

    /// <summary>
    /// Gets the Extensible Markup Language format identifier.
    /// </summary>
    public static Format Xml { get; } = new("xml");

    /// <summary>
    /// Gets the Portable Document Format identifier.
    /// </summary>
    public static Format Pdf { get; } = new("pdf");

    private static readonly Format[] BuiltInFormats = [Excel, Csv, CsvTyped, Json, Xml, Pdf];

    /// <summary>
    /// Gets the built-in DataPorter formats.
    /// </summary>
    public static IReadOnlyCollection<Format> BuiltIns => BuiltInFormats;

    /// <summary>
    /// Attempts to parse the specified value into a <see cref="Format"/>.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="format">The parsed format.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string value, out Format format)
    {
        format = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            format = new Format(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether this format is one of the built-in DataPorter formats.
    /// </summary>
    /// <returns><see langword="true"/> if the format is built in; otherwise <see langword="false"/>.</returns>
    public bool IsBuiltIn()
    {
        return BuiltInFormats.Contains(this);
    }

    /// <summary>
    /// Returns the normalized format key.
    /// </summary>
    public override string ToString()
    {
        return this.Key;
    }
}
