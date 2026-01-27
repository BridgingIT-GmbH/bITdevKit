// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Represents the complete export configuration for a type.
/// </summary>
public sealed class ExportConfiguration
{
    /// <summary>
    /// Gets or sets the source type this configuration is for.
    /// </summary>
    public Type SourceType { get; set; }

    /// <summary>
    /// Gets or sets the sheet/section name.
    /// </summary>
    public string SheetName { get; set; }

    /// <summary>
    /// Gets the column configurations.
    /// </summary>
    public List<ColumnConfiguration> Columns { get; set; } = [];

    /// <summary>
    /// Gets the header row configurations.
    /// </summary>
    public List<HeaderRowConfiguration> HeaderRows { get; set; } = [];

    /// <summary>
    /// Gets the footer row configurations.
    /// </summary>
    public List<FooterRowConfiguration> FooterRows { get; set; } = [];

    /// <summary>
    /// Gets or sets the culture to use for formatting.
    /// </summary>
    public System.Globalization.CultureInfo Culture { get; set; } = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets or sets a value indicating whether to include column headers.
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;
}
