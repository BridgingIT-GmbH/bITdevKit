// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents options used when rendering diagram output.
/// </summary>
public class DiagramRenderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the diagram header should be emitted.
    /// </summary>
    public bool IncludeHeader { get; init; } = true;

    /// <summary>
    /// Gets or sets the optional target width in pixels.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Gets or sets the optional target height in pixels.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Gets or sets the optional render scale factor.
    /// </summary>
    public double? Scale { get; init; }

    /// <summary>
    /// Gets or sets the optional theme identifier.
    /// </summary>
    public string Theme { get; init; }
}