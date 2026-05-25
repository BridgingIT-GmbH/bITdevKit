// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents bitmap-specific render options.
/// </summary>
/// <example>
/// <code>
/// var options = new BitmapDiagramRenderOptions
/// {
///     Width = 1200,
///     Height = 800,
///     Dpi = 144,
///     BackgroundColor = "#ffffff"
/// };
/// </code>
/// </example>
public sealed class BitmapDiagramRenderOptions : DiagramRenderOptions
{
    /// <summary>
    /// Gets or sets the target DPI.
    /// </summary>
    public int Dpi { get; init; } = 96;

    /// <summary>
    /// Gets or sets the bitmap background color.
    /// </summary>
    public string BackgroundColor { get; init; } = "#ffffff";
}