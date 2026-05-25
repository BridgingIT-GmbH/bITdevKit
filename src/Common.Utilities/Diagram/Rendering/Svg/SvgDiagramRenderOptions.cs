// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents SVG-specific render options.
/// </summary>
/// <example>
/// <code>
/// var options = new SvgDiagramRenderOptions
/// {
///     Width = 640,
///     Height = 480,
///     FontFamily = "Segoe UI",
///     NodeWidth = 180
/// };
/// </code>
/// </example>
public sealed class SvgDiagramRenderOptions : DiagramRenderOptions
{
    /// <summary>
    /// Gets or sets the outer margin in pixels.
    /// </summary>
    public int Margin { get; init; } = 24;

    /// <summary>
    /// Gets or sets the default node width in pixels.
    /// </summary>
    public int NodeWidth { get; init; } = 180;

    /// <summary>
    /// Gets or sets the default node height in pixels.
    /// </summary>
    public int NodeHeight { get; init; } = 56;

    /// <summary>
    /// Gets or sets the horizontal spacing between nodes in pixels.
    /// </summary>
    public int HorizontalSpacing { get; init; } = 96;

    /// <summary>
    /// Gets or sets the vertical spacing between nodes in pixels.
    /// </summary>
    public int VerticalSpacing { get; init; } = 72;

    /// <summary>
    /// Gets or sets the note width in pixels.
    /// </summary>
    public int NoteWidth { get; init; } = 180;

    /// <summary>
    /// Gets or sets the note height in pixels.
    /// </summary>
    public int NoteHeight { get; init; } = 64;

    /// <summary>
    /// Gets or sets the pseudo-state marker radius in pixels.
    /// </summary>
    public int MarkerRadius { get; init; } = 10;

    /// <summary>
    /// Gets or sets the node corner radius in pixels.
    /// </summary>
    public int CornerRadius { get; init; } = 10;

    /// <summary>
    /// Gets or sets the font family.
    /// </summary>
    public string FontFamily { get; init; } = "Segoe UI";

    /// <summary>
    /// Gets or sets the font size in pixels.
    /// </summary>
    public int FontSize { get; init; } = 14;

    /// <summary>
    /// Gets or sets the background fill color.
    /// </summary>
    public string BackgroundColor { get; init; } = "transparent";

    /// <summary>
    /// Gets or sets the node fill color.
    /// </summary>
    public string FillColor { get; init; } = "#ffffff";

    /// <summary>
    /// Gets or sets the note fill color.
    /// </summary>
    public string NoteFillColor { get; init; } = "#f8fafc";

    /// <summary>
    /// Gets or sets the stroke color.
    /// </summary>
    public string StrokeColor { get; init; } = "#0f172a";

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public string TextColor { get; init; } = "#0f172a";
}