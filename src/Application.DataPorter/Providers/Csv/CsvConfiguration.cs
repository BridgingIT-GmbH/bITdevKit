// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;
using System.Text;

/// <summary>
/// Configuration options for the CSV provider.
/// </summary>
public sealed class CsvConfiguration
{
    /// <summary>
    /// Gets or sets the delimiter character.
    /// </summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>
    /// Gets or sets the encoding.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Gets or sets the culture for parsing and formatting.
    /// </summary>
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets or sets a value indicating whether to include the header row.
    /// </summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets the quote character.
    /// </summary>
    public char QuoteCharacter { get; set; } = '"';

    /// <summary>
    /// Gets or sets a value indicating whether to trim whitespace from fields.
    /// </summary>
    public bool TrimFields { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to ignore blank lines.
    /// </summary>
    public bool IgnoreBlankLines { get; set; } = true;
}
