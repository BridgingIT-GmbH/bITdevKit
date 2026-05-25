// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Renders deterministic SVG for component diagrams.
/// </summary>
public class SvgComponentDiagramRenderer : IDiagramRenderer
{
    /// <inheritdoc />
    public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
    {
        return SvgFlowchartDiagramRenderer.Render(document, options, DiagramKind.Component);
    }
}