
using BridgingIT.DevKit.Common;

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Diagrams;
public class MermaidSequenceDiagramRendererTests
{
    private readonly MermaidSequenceDiagramRenderer sut = new();

    [Fact]
    public void Render_SequenceDiagram_ShouldRenderDeterministicText()
    {
        var document = new SequenceDiagramBuilder()
            .AddParticipant("User", kind: DiagramNodeKind.Actor)
            .AddParticipant("TodoApi", "Todo API")
            .AddMessage("User", "TodoApi", "GET /todos")
            .AddMessage("TodoApi", "User", "200 OK", DiagramEdgeKind.Reply)
            .AddNote("TodoApi", "returns cached items")
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldBe(
            "sequenceDiagram\n" +
            "    actor User\n" +
            "    participant TodoApi as \"Todo API\"\n" +
            "    User->>TodoApi: GET /todos\n" +
            "    TodoApi-->>User: 200 OK\n" +
            "    Note right of TodoApi: returns cached items");
    }

    [Fact]
    public void Render_WhenParticipantIsImplicit_ShouldRenderDerivedParticipant()
    {
        var document = new SequenceDiagramBuilder()
            .AddMessage("Client App", "Backend", "ping")
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldContain("participant Client_App");
        result.ShouldContain("participant Backend");
        result.ShouldContain("Client_App->>Backend: ping");
    }

    [Fact]
    public void Render_WhenDiagramKindIsUnsupported_ShouldThrowNotSupportedException()
    {
        var document = new DiagramDocument(DiagramKind.State, [], [], [], []);

        var action = () => this.sut.Render(document);

        action.ShouldThrow<NotSupportedException>();
    }
}