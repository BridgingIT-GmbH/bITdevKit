// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Configuration options for the PDF provider.
/// </summary>
public sealed class PdfConfiguration
{
    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public PdfPageSize PageSize { get; set; } = PdfPageSize.A4;

    /// <summary>
    /// Gets or sets the page orientation.
    /// </summary>
    public PdfPageOrientation Orientation { get; set; } = PdfPageOrientation.Portrait;

    /// <summary>
    /// Gets or sets the page margins in points.
    /// </summary>
    public float Margin { get; set; } = 50;

    /// <summary>
    /// Gets or sets the document title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the document author.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the document subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the header text.
    /// </summary>
    public string HeaderText { get; set; }

    /// <summary>
    /// Gets or sets the footer text.
    /// </summary>
    public string FooterText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show page numbers.
    /// </summary>
    public bool ShowPageNumbers { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to show the generation date.
    /// </summary>
    public bool ShowGenerationDate { get; set; } = true;

    /// <summary>
    /// Gets or sets the font family name.
    /// </summary>
    public string FontFamily { get; set; } = "Helvetica";

    /// <summary>
    /// Gets or sets the header font size.
    /// </summary>
    public float HeaderFontSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets the body font size.
    /// </summary>
    public float BodyFontSize { get; set; } = 9;

    /// <summary>
    /// Gets or sets the table header background color (hex).
    /// </summary>
    public string TableHeaderBackgroundColor { get; set; } = "#4472C4";

    /// <summary>
    /// Gets or sets the table header text color (hex).
    /// </summary>
    public string TableHeaderTextColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Gets or sets the alternate row background color (hex).
    /// </summary>
    public string AlternateRowBackgroundColor { get; set; } = "#F2F2F2";

    /// <summary>
    /// Gets or sets a value indicating whether to use alternating row colors.
    /// </summary>
    public bool UseAlternatingRowColors { get; set; } = true;

    /// <summary>
    /// Gets or sets the date format string.
    /// </summary>
    public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// Gets or sets a value indicating whether nested structured values should be rendered.
    /// When disabled, nested objects and collections without an explicit converter are ignored during PDF export.
    /// </summary>
    public bool UseNesting { get; set; } = false;
}

/// <summary>
/// PDF page size options.
/// </summary>
public enum PdfPageSize
{
    /// <summary>
    /// A4 page size (210 x 297 mm).
    /// </summary>
    A4,

    /// <summary>
    /// A3 page size (297 x 420 mm).
    /// </summary>
    A3,

    /// <summary>
    /// Letter page size (8.5 x 11 inches).
    /// </summary>
    Letter,

    /// <summary>
    /// Legal page size (8.5 x 14 inches).
    /// </summary>
    Legal
}

/// <summary>
/// PDF page orientation options.
/// </summary>
public enum PdfPageOrientation
{
    /// <summary>
    /// Portrait orientation.
    /// </summary>
    Portrait,

    /// <summary>
    /// Landscape orientation.
    /// </summary>
    Landscape
}
