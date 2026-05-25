// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the output format produced by a diagram renderer.
/// </summary>
public enum DiagramRenderFormat
{
    /// <summary>
    /// Mermaid text output.
    /// </summary>
    Mermaid,

    /// <summary>
    /// Scalable vector graphics output.
    /// </summary>
    Svg,

    /// <summary>
    /// Bitmap image output.
    /// </summary>
    Bitmap,
}