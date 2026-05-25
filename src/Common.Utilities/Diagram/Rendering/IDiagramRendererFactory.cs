// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Resolves diagram renderers for a specific <see cref="DiagramKind"/> and <see cref="DiagramRenderFormat"/>.
/// </summary>
public interface IDiagramRendererFactory
{
    /// <summary>
    /// Gets the formats registered for the supplied diagram kind.
    /// </summary>
    /// <param name="kind">The diagram kind.</param>
    /// <returns>The registered formats.</returns>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRendering();
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// var formats = factory.GetFormats(DiagramKind.State);
    /// </code>
    /// </example>
    IReadOnlyList<DiagramRenderFormat> GetFormats(DiagramKind kind);

    /// <summary>
    /// Determines whether a renderer is registered for the supplied diagram kind using Mermaid output.
    /// </summary>
    /// <param name="kind">The diagram kind to check.</param>
    /// <returns><c>true</c> when a renderer is registered; otherwise <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRendering();
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// var canRender = factory.CanRender(DiagramKind.State);
    /// </code>
    /// </example>
    bool CanRender(DiagramKind kind);

    /// <summary>
    /// Determines whether a renderer is registered for the supplied diagram kind and format.
    /// </summary>
    /// <param name="kind">The diagram kind to check.</param>
    /// <param name="format">The render format to check.</param>
    /// <returns><c>true</c> when a renderer is registered; otherwise <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRendering();
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// var canRenderSvg = factory.CanRender(DiagramKind.State, DiagramRenderFormat.Svg);
    /// </code>
    /// </example>
    bool CanRender(DiagramKind kind, DiagramRenderFormat format);

    /// <summary>
    /// Gets the renderer registered for the supplied diagram kind using Mermaid output.
    /// </summary>
    /// <param name="kind">The diagram kind to resolve.</param>
    /// <returns>The registered renderer.</returns>
    /// <exception cref="NotSupportedException">Thrown when no renderer is registered for <paramref name="kind"/>.</exception>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRendering();
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// var renderer = factory.GetRenderer(DiagramKind.Sequence);
    /// </code>
    /// </example>
    IDiagramRenderer GetRenderer(DiagramKind kind);

    /// <summary>
    /// Gets the renderer registered for the supplied diagram kind and format.
    /// </summary>
    /// <param name="kind">The diagram kind to resolve.</param>
    /// <param name="format">The render format to resolve.</param>
    /// <returns>The registered renderer.</returns>
    /// <exception cref="NotSupportedException">Thrown when no renderer is registered for <paramref name="kind"/> and <paramref name="format"/>.</exception>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRendering();
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// var renderer = factory.GetRenderer(DiagramKind.Sequence, DiagramRenderFormat.Mermaid);
    /// </code>
    /// </example>
    IDiagramRenderer GetRenderer(DiagramKind kind, DiagramRenderFormat format);

    /// <summary>
    /// Tries to get the renderer registered for the supplied diagram kind using Mermaid output.
    /// </summary>
    /// <param name="kind">The diagram kind to resolve.</param>
    /// <param name="renderer">The resolved renderer when available.</param>
    /// <returns><c>true</c> when a renderer is registered; otherwise <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRendering();
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// if (factory.TryGetRenderer(DiagramKind.Class, out var renderer))
    /// {
    ///     var text = renderer.Render(new ClassDiagramBuilder().AddClass("Order").Build());
    /// }
    /// </code>
    /// </example>
    bool TryGetRenderer(DiagramKind kind, out IDiagramRenderer renderer);

    /// <summary>
    /// Tries to get the renderer registered for the supplied diagram kind and format.
    /// </summary>
    /// <param name="kind">The diagram kind to resolve.</param>
    /// <param name="format">The render format to resolve.</param>
    /// <param name="renderer">The resolved renderer when available.</param>
    /// <returns><c>true</c> when a renderer is registered; otherwise <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRendering();
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// if (factory.TryGetRenderer(DiagramKind.Class, DiagramRenderFormat.Mermaid, out var renderer))
    /// {
    ///     var text = renderer.Render(new ClassDiagramBuilder().AddClass("Order").Build()).GetText();
    /// }
    /// </code>
    /// </example>
    bool TryGetRenderer(DiagramKind kind, DiagramRenderFormat format, out IDiagramRenderer renderer);

    /// <summary>
    /// Renders the supplied document using the Mermaid renderer registered for <see cref="DiagramDocument.Kind"/>.
    /// </summary>
    /// <param name="document">The document to render.</param>
    /// <param name="options">The optional render options.</param>
    /// <returns>The rendered diagram output.</returns>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRendering();
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// var text = factory.Render(new FlowDiagramBuilder().AddNode("Start").Build()).GetText();
    /// </code>
    /// </example>
    DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null);

    /// <summary>
    /// Renders the supplied document using the renderer registered for <see cref="DiagramDocument.Kind"/> and the requested format.
    /// </summary>
    /// <param name="document">The document to render.</param>
    /// <param name="format">The output format.</param>
    /// <param name="options">The optional render options.</param>
    /// <returns>The rendered diagram output.</returns>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRendering();
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// var result = factory.Render(
    ///     new FlowDiagramBuilder().AddNode("Start").Build(),
    ///     DiagramRenderFormat.Mermaid);
    /// </code>
    /// </example>
    DiagramRenderResult Render(DiagramDocument document, DiagramRenderFormat format, DiagramRenderOptions options = null);
}