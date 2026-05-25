// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Renders Mermaid-compatible activity diagram text through flowchart syntax.
/// </summary>
public class MermaidActivityDiagramRenderer : IDiagramRenderer
{
    /// <inheritdoc />
    public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
    {
        return DiagramRenderResult.FromText(
            DiagramRenderFormat.Mermaid,
            MermaidFlowchartRenderer.Render(document, options, DiagramKind.Activity));
    }
}