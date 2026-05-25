// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using Microsoft.Extensions.DependencyInjection;

namespace BridgingIT.DevKit.Common;
internal sealed class DiagramRendererFactory(
    IServiceProvider serviceProvider,
    DiagramRendererRegistrationStore registrations) : IDiagramRendererFactory
{
    public IReadOnlyList<DiagramRenderFormat> GetFormats(DiagramKind kind)
    {
        return registrations.GetFormats(kind);
    }

    public bool CanRender(DiagramKind kind)
    {
        return this.CanRender(kind, DiagramRenderFormat.Mermaid);
    }

    public bool CanRender(DiagramKind kind, DiagramRenderFormat format)
    {
        return registrations.TryGetRendererType(kind, format, out _);
    }

    public IDiagramRenderer GetRenderer(DiagramKind kind)
    {
        return this.GetRenderer(kind, DiagramRenderFormat.Mermaid);
    }

    public IDiagramRenderer GetRenderer(DiagramKind kind, DiagramRenderFormat format)
    {
        if (!registrations.TryGetRendererType(kind, format, out var rendererType))
        {
            throw new NotSupportedException($"Diagram kind '{kind}' does not have a registered renderer for format '{format}'.");
        }

        return (IDiagramRenderer)serviceProvider.GetRequiredService(rendererType);
    }

    public bool TryGetRenderer(DiagramKind kind, out IDiagramRenderer renderer)
    {
        return this.TryGetRenderer(kind, DiagramRenderFormat.Mermaid, out renderer);
    }

    public bool TryGetRenderer(DiagramKind kind, DiagramRenderFormat format, out IDiagramRenderer renderer)
    {
        if (registrations.TryGetRendererType(kind, format, out _))
        {
            renderer = this.GetRenderer(kind, format);
            return true;
        }

        renderer = null;
        return false;
    }

    public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
    {
        return this.Render(document, DiagramRenderFormat.Mermaid, options);
    }

    public DiagramRenderResult Render(DiagramDocument document, DiagramRenderFormat format, DiagramRenderOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        return this.GetRenderer(document.Kind, format).Render(document, options);
    }
}