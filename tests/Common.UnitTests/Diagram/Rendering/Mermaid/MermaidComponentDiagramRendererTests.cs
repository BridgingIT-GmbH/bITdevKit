namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Diagrams;
public class MermaidComponentDiagramRendererTests
{
    private readonly MermaidComponentDiagramRenderer sut = new();

    [Fact]
    public void Render_ComponentDiagram_ShouldRenderDeterministicText()
    {
        var document = new ComponentDiagramBuilder(DiagramDirection.LeftToRight)
            .AddComponent("Api", "Orders API", stereotype: "service")
            .AddComponent("Db", "Orders DB", DiagramNodeKind.Database)
            .AddDependency("Api", "Db", "reads")
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldBe(
            "flowchart LR\n" +
            "    Api[[\"Orders API<br/><<service>>\"]]\n" +
            "    Db[(\"Orders DB\")]\n" +
            "\n" +
            "    Api -. reads .-> Db");
    }

    [Fact]
    public void Render_WhenGroupExists_ShouldRenderComponentSubgraph()
    {
        var document = new ComponentDiagramBuilder()
            .AddComponent("Frontend", stereotype: "ui")
            .AddComponent("Backend", stereotype: "service")
            .AddGroup("Platform", "Platform", DiagramGroupKind.ChildDiagram, ["Frontend", "Backend"])
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldContain("subgraph Platform[\"Platform\"]");
        result.ShouldContain("Frontend[[\"Frontend<br/><<ui>>\"]]");
    }
}