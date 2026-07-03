namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Diagrams;
public class SvgStateDiagramRendererTests
{
    private readonly SvgStateDiagramRenderer sut = new();

    [Fact]
    public void Render_StateDiagram_ShouldRenderSvgMarkup()
    {
        var document = new StateDiagramBuilder()
            .AddState("Created")
            .AddState("Awaiting Approval")
            .AddTransition("[*]", "Created")
            .AddTransition("Created", "Awaiting Approval", "requires approval")
            .AddNote("Awaiting Approval", "current state")
            .Build();

        var result = this.sut.Render(document);

        result.Format.ShouldBe(DiagramRenderFormat.Svg);
        result.ContentType.ShouldBe("image/svg+xml; charset=utf-8");
        var text = result.GetText();
        text.ShouldContain("<svg ");
        text.ShouldContain("aria-label=\"State diagram\"");
        text.ShouldContain(">Created<");
        text.ShouldContain(">Awaiting Approval<");
        text.ShouldContain(">requires approval<");
        text.ShouldContain(">current state<");
        text.ShouldContain("marker-end=\"url(#arrow)\"");
    }

    [Fact]
    public void Render_WhenSvgOptionsAreProvided_ShouldUseConfiguredCanvasAndStyle()
    {
        var document = new StateDiagramBuilder()
            .AddState("Created")
            .Build();

        var result = this.sut.Render(document, new SvgDiagramRenderOptions
        {
            Width = 500,
            Height = 300,
            Scale = 2,
            FontFamily = "Fira Sans",
            BackgroundColor = "#ffffff",
        });

        var text = result.GetText();
        text.ShouldContain("width=\"1000\"");
        text.ShouldContain("height=\"600\"");
        text.ShouldContain("viewBox=\"0 0 500 300\"");
        text.ShouldContain("font-family=\"Fira Sans\"");
        text.ShouldContain("fill=\"#ffffff\"");
    }

    [Fact]
    public void Render_WhenDiagramKindIsUnsupported_ShouldThrowNotSupportedException()
    {
        var document = new DiagramDocument(DiagramKind.Sequence, [], [], [], []);

        var action = () => this.sut.Render(document);

        action.ShouldThrow<NotSupportedException>();
    }
}