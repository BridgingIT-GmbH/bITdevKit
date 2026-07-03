namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Diagrams;
public class MermaidFlowDiagramRendererTests
{
    private readonly MermaidFlowDiagramRenderer sut = new();

    [Fact]
    public void Render_FlowDiagram_ShouldRenderDeterministicText()
    {
        var document = new FlowDiagramBuilder()
            .AddNode("Start", kind: DiagramNodeKind.Start)
            .AddNode("Validate", "Validate Request")
            .AddNode("IsValid", "Valid?", DiagramNodeKind.Decision)
            .AddNode("Persist")
            .AddNode("Reject", kind: DiagramNodeKind.Terminal)
            .AddTransition("Start", "Validate")
            .AddTransition("Validate", "IsValid")
            .AddTransition("IsValid", "Persist", "yes")
            .AddTransition("IsValid", "Reject", "no")
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldBe(
            "flowchart TD\n" +
            "    Start([\"Start\"])\n" +
            "    Validate[\"Validate Request\"]\n" +
            "    IsValid{\"Valid?\"}\n" +
            "    Persist[\"Persist\"]\n" +
            "    Reject((\"Reject\"))\n" +
            "\n" +
            "    Start --> Validate\n" +
            "    Validate --> IsValid\n" +
            "    IsValid -->|yes| Persist\n" +
            "    IsValid -->|no| Reject");
    }

    [Fact]
    public void Render_WhenGroupExists_ShouldRenderSubgraph()
    {
        var document = new FlowDiagramBuilder(DiagramDirection.LeftToRight)
            .AddNode("Fetch")
            .AddNode("Map")
            .AddGroup("Pipeline", "Pipeline", DiagramGroupKind.ChildDiagram, ["Fetch", "Map"])
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldContain("flowchart LR");
        result.ShouldContain("subgraph Pipeline[\"Pipeline\"]");
    }
}