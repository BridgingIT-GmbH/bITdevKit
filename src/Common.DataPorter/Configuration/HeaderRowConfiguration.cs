// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Represents the configuration for a header row in export operations.
/// </summary>
public sealed class HeaderRowConfiguration
{
    /// <summary>
    /// Gets or sets the content of the header row.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text should be bold.
    /// </summary>
    public bool IsBold { get; set; }

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public double? FontSize { get; set; }

    /// <summary>
    /// Gets or sets the horizontal alignment.
    /// </summary>
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
}

/// <summary>
/// Represents the configuration for a footer row in export operations.
/// </summary>
public sealed class FooterRowConfiguration
{
    /// <summary>
    /// Gets or sets the static content of the footer row.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the content factory function that receives the data collection.
    /// </summary>
    public Func<IEnumerable<object>, string> ContentFactory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text should be bold.
    /// </summary>
    public bool IsBold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text should be italic.
    /// </summary>
    public bool IsItalic { get; set; }

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public double? FontSize { get; set; }

    /// <summary>
    /// Gets or sets the horizontal alignment.
    /// </summary>
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
}
