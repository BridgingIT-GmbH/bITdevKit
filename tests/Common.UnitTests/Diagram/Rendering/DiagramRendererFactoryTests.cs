
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Diagrams;
public class DiagramRendererFactoryTests
{
    [Fact]
    public void AddDiagramRendering_ShouldRegisterBuiltInMermaidRenderers()
    {
        var services = new ServiceCollection();

        services.AddDiagramRendering();
        var provider = services.BuildServiceProvider();

        var sut = provider.GetRequiredService<IDiagramRendererFactory>();

        sut.GetRenderer(DiagramKind.State, DiagramRenderFormat.Mermaid).ShouldBeOfType<MermaidStateDiagramRenderer>();
        sut.GetRenderer(DiagramKind.State, DiagramRenderFormat.Svg).ShouldBeOfType<SvgStateDiagramRenderer>();
        sut.GetRenderer(DiagramKind.Flow, DiagramRenderFormat.Mermaid).ShouldBeOfType<MermaidFlowDiagramRenderer>();
        sut.GetRenderer(DiagramKind.Activity, DiagramRenderFormat.Mermaid).ShouldBeOfType<MermaidActivityDiagramRenderer>();
        sut.GetRenderer(DiagramKind.Sequence, DiagramRenderFormat.Mermaid).ShouldBeOfType<MermaidSequenceDiagramRenderer>();
        sut.GetRenderer(DiagramKind.Class, DiagramRenderFormat.Mermaid).ShouldBeOfType<MermaidClassDiagramRenderer>();
        sut.GetRenderer(DiagramKind.Component, DiagramRenderFormat.Mermaid).ShouldBeOfType<MermaidComponentDiagramRenderer>();
        sut.GetFormats(DiagramKind.State).ShouldContain(DiagramRenderFormat.Mermaid);
        sut.GetFormats(DiagramKind.State).ShouldContain(DiagramRenderFormat.Svg);
    }

    [Fact]
    public void Render_WhenDocumentKindHasRegisteredRenderer_ShouldUseMatchingRenderer()
    {
        var services = new ServiceCollection();

        services.AddDiagramRendering();
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IDiagramRendererFactory>();
        var document = new SequenceDiagramBuilder()
            .AddParticipant("Client")
            .AddParticipant("Server")
            .AddMessage("Client", "Server", "ping")
            .Build();

        var result = sut.Render(document, DiagramRenderFormat.Mermaid);

        result.Format.ShouldBe(DiagramRenderFormat.Mermaid);
        result.GetText().ShouldContain("sequenceDiagram");
        result.GetText().ShouldContain("Client->>Server: ping");
    }

    [Fact]
    public void TryGetRenderer_WhenDiagramFormatIsNotRegistered_ShouldReturnFalse()
    {
        var services = new ServiceCollection();

        services.AddDiagramRenderer<MermaidStateDiagramRenderer>(DiagramKind.State, DiagramRenderFormat.Mermaid);
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IDiagramRendererFactory>();

        var result = sut.TryGetRenderer(DiagramKind.Class, DiagramRenderFormat.Svg, out var renderer);

        result.ShouldBeFalse();
        renderer.ShouldBeNull();
    }

    [Fact]
    public void AddDiagramRendering_WhenCustomMermaidRendererWasRegistered_ShouldKeepCustomRenderer()
    {
        var services = new ServiceCollection();

        services.AddDiagramRenderer<TestStateDiagramRenderer>(DiagramKind.State, DiagramRenderFormat.Mermaid);
        services.AddDiagramRendering();
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IDiagramRendererFactory>();

        sut.GetRenderer(DiagramKind.State, DiagramRenderFormat.Mermaid).ShouldBeOfType<TestStateDiagramRenderer>();
        sut.GetRenderer(DiagramKind.State, DiagramRenderFormat.Svg).ShouldBeOfType<SvgStateDiagramRenderer>();
        sut.GetRenderer(DiagramKind.Sequence, DiagramRenderFormat.Mermaid).ShouldBeOfType<MermaidSequenceDiagramRenderer>();
    }

    [Fact]
    public void Render_WhenSvgStateRendererIsResolvedThroughFactory_ShouldReturnSvgResult()
    {
        var services = new ServiceCollection();

        services.AddDiagramRendering();
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IDiagramRendererFactory>();
        var document = new StateDiagramBuilder()
            .AddState("Created")
            .AddTransition("[*]", "Created")
            .Build();

        var result = sut.Render(document, DiagramRenderFormat.Svg, new SvgDiagramRenderOptions { Width = 640, Height = 320 });

        result.Format.ShouldBe(DiagramRenderFormat.Svg);
        result.ContentType.ShouldBe("image/svg+xml; charset=utf-8");
        result.GetText().ShouldContain("viewBox=\"0 0 640 320\"");
        result.GetText().ShouldContain(">Created<");
    }

    [Fact]
    public void AddDiagramRenderer_WhenDifferentFormatIsRegisteredForSameKind_ShouldAllowBoth()
    {
        var services = new ServiceCollection();
        services.AddDiagramRenderer<TestStateDiagramRenderer>(DiagramKind.State, DiagramRenderFormat.Mermaid);
        services.AddDiagramRenderer<TestSvgStateDiagramRenderer>(DiagramKind.State, DiagramRenderFormat.Svg);
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IDiagramRendererFactory>();

        sut.GetRenderer(DiagramKind.State, DiagramRenderFormat.Mermaid).ShouldBeOfType<TestStateDiagramRenderer>();
        sut.GetRenderer(DiagramKind.State, DiagramRenderFormat.Svg).ShouldBeOfType<TestSvgStateDiagramRenderer>();
        sut.GetFormats(DiagramKind.State).ShouldContain(DiagramRenderFormat.Mermaid);
        sut.GetFormats(DiagramKind.State).ShouldContain(DiagramRenderFormat.Svg);
    }

    [Fact]
    public void AddDiagramRenderer_WhenDifferentRendererIsAlreadyRegisteredForKindAndFormat_ShouldThrowInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddDiagramRenderer<TestStateDiagramRenderer>(DiagramKind.State, DiagramRenderFormat.Mermaid);

        var action = () => services.AddDiagramRenderer<MermaidStateDiagramRenderer>(DiagramKind.State, DiagramRenderFormat.Mermaid);

        action.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("Diagram kind 'State' is already mapped to renderer 'TestStateDiagramRenderer' for format 'Mermaid'.");
    }

    private sealed class TestStateDiagramRenderer : IDiagramRenderer
    {
        public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
        {
            return DiagramRenderResult.FromText(DiagramRenderFormat.Mermaid, "custom-state");
        }
    }

    private sealed class TestSvgStateDiagramRenderer : IDiagramRenderer
    {
        public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
        {
            return DiagramRenderResult.FromText(DiagramRenderFormat.Svg, "<svg />", "image/svg+xml; charset=utf-8");
        }
    }
}