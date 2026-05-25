// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Placeholder bitmap renderer registration for diagram output.
/// </summary>
public class BitmapDiagramRenderer : IDiagramRenderer
{
    /// <inheritdoc />
    public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        throw new NotImplementedException($"Bitmap diagram rendering is not implemented yet for diagram kind '{document.Kind}'.");
    }
}