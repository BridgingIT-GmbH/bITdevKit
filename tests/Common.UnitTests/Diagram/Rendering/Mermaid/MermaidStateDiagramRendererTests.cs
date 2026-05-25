
using BridgingIT.DevKit.Common;

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Diagrams;
public class MermaidStateDiagramRendererTests
{
    private readonly MermaidStateDiagramRenderer sut = new();

    [Fact]
    public void Render_StateDiagram_ShouldRenderDeterministicText()
    {
        var document = new StateDiagramBuilder()
            .AddState("Created")
            .AddState("Awaiting Approval")
            .AddTransition("[*]", "Created")
            .AddTransition("Created", "Awaiting Approval", "requires approval")
            .AddNote("Awaiting Approval", "current state")
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldBe(
            "stateDiagram-v2\n" +
            "    [*] --> Created\n" +
            "    Created --> Awaiting_Approval: requires_approval\n" +
            "\n" +
            "    note right of Awaiting_Approval\n" +
            "        current state\n" +
            "    end note");
    }

    [Fact]
    public void Render_WhenStandaloneStateExists_ShouldRenderExplicitDeclaration()
    {
        var document = new StateDiagramBuilder()
            .AddState("Awaiting Approval")
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldBe(
            "stateDiagram-v2\n" +
            "    state Awaiting_Approval");
    }

    [Fact]
    public void Render_WhenIdentifiersContainUnsupportedCharacters_ShouldNormalizeIdentifiers()
    {
        var document = new StateDiagramBuilder()
            .AddTransition("Phone-Destroyed", "2026 completed", "signal: approved")
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldBe(
            "stateDiagram-v2\n" +
            "    Phone_Destroyed --> _2026_completed: signal_approved");
    }

    [Fact]
    public void Render_WhenSpecialStateIdentifierIsUsed_ShouldPreserveStartOrEndMarker()
    {
        var document = new StateDiagramBuilder()
            .AddTransition("[*]", "Created")
            .AddTransition("Created", "[*]")
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldContain("[*] --> Created");
        result.ShouldContain("Created --> [*]");
    }

    [Fact]
    public void Render_WhenParallelBranchNodesAreProvided_ShouldRenderBranchAndJoinEdges()
    {
        var document = new StateDiagramBuilder()
            .AddState("Created")
            .AddState("Parallel_Inventory", kind: DiagramNodeKind.Branch)
            .AddState("Parallel_Payment", kind: DiagramNodeKind.Branch)
            .AddState("ParallelJoin_Processing", kind: DiagramNodeKind.Join)
            .AddTransition("Created", "Parallel_Inventory", "branch_Inventory", DiagramEdgeKind.Branch)
            .AddTransition("Created", "Parallel_Payment", "branch_Payment", DiagramEdgeKind.Branch)
            .AddTransition("Parallel_Inventory", "ParallelJoin_Processing", "completed", DiagramEdgeKind.Join)
            .AddTransition("Parallel_Payment", "ParallelJoin_Processing", "completed", DiagramEdgeKind.Join)
            .AddTransition("ParallelJoin_Processing", "Confirmed", "join_all", DiagramEdgeKind.Join)
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldBe(
            "stateDiagram-v2\n" +
            "    Created --> Parallel_Inventory: branch_Inventory\n" +
            "    Created --> Parallel_Payment: branch_Payment\n" +
            "    Parallel_Inventory --> ParallelJoin_Processing: completed\n" +
            "    Parallel_Payment --> ParallelJoin_Processing: completed\n" +
            "    ParallelJoin_Processing --> Confirmed: join_all");
    }

    [Fact]
    public void Render_WhenDiagramKindIsUnsupported_ShouldThrowNotSupportedException()
    {
        var document = new DiagramDocument(DiagramKind.Flow, [], [], [], []);

        var action = () => this.sut.Render(document);

        action.ShouldThrow<NotSupportedException>();
    }

    [Fact]
    public void Render_ShouldNotProduceTrailingWhitespace()
    {
        var document = new StateDiagramBuilder()
            .AddTransition("[*]", "Created")
            .AddNote("Created", "current_state")
            .Build();

        var result = this.sut.Render(document).GetText();

        foreach (var line in result.Split('\n'))
        {
            line.EndsWith(' ').ShouldBeFalse();
        }
    }
}