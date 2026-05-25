// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Renders a reusable diagram document into deterministic output.
/// </summary>
public interface IDiagramRenderer
{
    /// <summary>
    /// Renders the supplied diagram document.
    /// </summary>
    /// <param name="document">The reusable diagram document.</param>
    /// <param name="options">The optional render options.</param>
    /// <returns>The rendered diagram output.</returns>
    /// <example>
    /// <code>
    /// var renderer = new MermaidStateDiagramRenderer();
    /// var document = new StateDiagramBuilder()
    ///     .AddState("Created")
    ///     .Build();
    /// var result = renderer.Render(document);
    /// var text = result.GetText();
    /// </code>
    /// </example>
    DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null);
}