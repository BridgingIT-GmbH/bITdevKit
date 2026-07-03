namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Diagrams;
public class MermaidActivityDiagramRendererTests
{
    private readonly MermaidActivityDiagramRenderer sut = new();

    [Fact]
    public void Render_ActivityDiagram_ShouldRenderDeterministicText()
    {
        var document = new ActivityDiagramBuilder()
            .AddStart("Start")
            .AddAction("Review", "Review Request")
            .AddDecision("Approved", "Approved?")
            .AddAction("Notify")
            .AddEnd("End")
            .AddTransition("Start", "Review")
            .AddTransition("Review", "Approved")
            .AddTransition("Approved", "Notify", "yes")
            .AddTransition("Approved", "End", "no")
            .AddTransition("Notify", "End")
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldBe(
            "flowchart TD\n" +
            "    Start([\"Start\"])\n" +
            "    Review[\"Review Request\"]\n" +
            "    Approved{\"Approved?\"}\n" +
            "    Notify[\"Notify\"]\n" +
            "    End((\"End\"))\n" +
            "\n" +
            "    Start --> Review\n" +
            "    Review --> Approved\n" +
            "    Approved -->|yes| Notify\n" +
            "    Approved -->|no| End\n" +
            "    Notify --> End");
    }

    [Fact]
    public void Render_WhenDiagramKindIsUnsupported_ShouldThrowNotSupportedException()
    {
        var document = new DiagramDocument(DiagramKind.Flow, [], [], [], []);

        var action = () => this.sut.Render(document);

        action.ShouldThrow<NotSupportedException>();
    }
}