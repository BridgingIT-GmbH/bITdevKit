// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Options for export operations.
/// </summary>
public sealed record ExportOptions
{
    /// <summary>
    /// Gets or sets the format to export to.
    /// </summary>
    public DataPorterFormat Format { get; init; } = DataPorterFormat.Excel;

    /// <summary>
    /// Gets or sets the name of a specific profile to use.
    /// </summary>
    public string ProfileName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to use attribute-based configuration.
    /// </summary>
    public bool UseAttributes { get; init; } = true;

    /// <summary>
    /// Gets or sets the culture to use for formatting.
    /// </summary>
    public System.Globalization.CultureInfo Culture { get; init; } = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets or sets the sheet/section name for the export.
    /// </summary>
    public string SheetName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to include headers.
    /// </summary>
    public bool IncludeHeaders { get; init; } = true;

    /// <summary>
    /// Gets or sets provider-specific options.
    /// </summary>
    public IReadOnlyDictionary<string, object> ProviderOptions { get; init; } = new Dictionary<string, object>();
}
