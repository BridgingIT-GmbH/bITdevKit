
using BridgingIT.DevKit.Common;

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Diagrams;
public class MermaidClassDiagramRendererTests
{
    private readonly MermaidClassDiagramRenderer sut = new();

    [Fact]
    public void Render_ClassDiagram_ShouldRenderDeterministicText()
    {
        var document = new ClassDiagramBuilder()
            .AddClass("OrderService")
            .AddMethod("OrderService", "CreateOrder", "Result")
            .AddClass("IOrderRepository", kind: DiagramNodeKind.Interface)
            .AddMethod("IOrderRepository", "FindById", "Order")
            .AddRelationship("OrderService", "IOrderRepository", DiagramEdgeKind.Dependency, "uses")
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldBe(
            "classDiagram\n" +
            "    class OrderService {\n" +
            "        +CreateOrder(): Result\n" +
            "    }\n" +
            "    class IOrderRepository {\n" +
            "        <<interface>>\n" +
            "        +FindById(): Order\n" +
            "    }\n" +
            "\n" +
            "    OrderService ..> IOrderRepository : uses");
    }

    [Fact]
    public void Render_WhenInheritanceRelationshipExists_ShouldRenderDerivedToBaseRelationship()
    {
        var document = new ClassDiagramBuilder()
            .AddClass("BaseEntity")
            .AddClass("Order")
            .AddRelationship("Order", "BaseEntity", DiagramEdgeKind.Inheritance)
            .Build();

        var result = this.sut.Render(document).GetText();

        result.ShouldContain("BaseEntity <|-- Order");
    }

    [Fact]
    public void Render_WhenDiagramKindIsUnsupported_ShouldThrowNotSupportedException()
    {
        var document = new DiagramDocument(DiagramKind.Sequence, [], [], [], []);

        var action = () => this.sut.Render(document);

        action.ShouldThrow<NotSupportedException>();
    }
}