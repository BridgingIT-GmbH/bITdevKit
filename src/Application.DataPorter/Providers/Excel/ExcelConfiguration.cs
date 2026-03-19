// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Configuration options for the Excel provider.
/// </summary>
public sealed class ExcelConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to use table formatting for exported data.
    /// </summary>
    public bool UseTableFormatting { get; set; } = true;

    /// <summary>
    /// Gets or sets the default table style name for Excel tables.
    /// </summary>
    public string DefaultTableStyleName { get; set; } = "TableStyleMedium2";

    /// <summary>
    /// Gets or sets a value indicating whether to auto-fit columns to content.
    /// </summary>
    public bool AutoFitColumns { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to freeze the header row.
    /// </summary>
    public bool FreezeHeaderRow { get; set; } = true;

    /// <summary>
    /// Gets or sets the default font name.
    /// </summary>
    public string DefaultFontName { get; set; } = "Calibri";

    /// <summary>
    /// Gets or sets the default font size.
    /// </summary>
    public double DefaultFontSize { get; set; } = 11;

    /// <summary>
    /// Gets or sets the maximum column width for auto-fit.
    /// </summary>
    public double MaxColumnWidth { get; set; } = 100;
}
