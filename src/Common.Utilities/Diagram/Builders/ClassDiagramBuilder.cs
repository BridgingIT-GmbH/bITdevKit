// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a minimal builder for class diagram documents.
/// </summary>
/// <example>
/// <code>
/// var document = new ClassDiagramBuilder()
///     .AddClass("OrderService")
///     .AddMethod("OrderService", "Create", "Result")
///     .Build();
/// </code>
/// </example>
public class ClassDiagramBuilder
{
    private readonly List<DiagramEdge> edges = [];
    private readonly Dictionary<string, DiagramNode> nodes = new(StringComparer.Ordinal);
    private readonly List<string> nodeOrder = [];

    /// <summary>
    /// Adds a class-like node to the document.
    /// </summary>
    /// <param name="id">The stable class identifier.</param>
    /// <param name="label">The optional class label.</param>
    /// <param name="kind">The class kind.</param>
    /// <param name="stereotype">The optional class stereotype.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new ClassDiagramBuilder()
    ///     .AddClass("IOrderRepository", kind: DiagramNodeKind.Interface);
    /// </code>
    /// </example>
    public ClassDiagramBuilder AddClass(string id, string label = null, DiagramNodeKind kind = DiagramNodeKind.Class, string stereotype = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        this.UpsertNode(id, label, kind, stereotype, null);
        return this;
    }

    /// <summary>
    /// Adds a member to a class-like node.
    /// </summary>
    /// <param name="classId">The target class identifier.</param>
    /// <param name="name">The member name or signature.</param>
    /// <param name="kind">The member kind.</param>
    /// <param name="type">The optional return or value type.</param>
    /// <param name="visibility">The member visibility.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new ClassDiagramBuilder()
    ///     .AddClass("Order")
    ///     .AddMember("Order", "Id", DiagramMemberKind.Property, "Guid", DiagramVisibility.Public);
    /// </code>
    /// </example>
    public ClassDiagramBuilder AddMember(
        string classId,
        string name,
        DiagramMemberKind kind = DiagramMemberKind.Property,
        string type = null,
        DiagramVisibility visibility = DiagramVisibility.Public)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var node = this.GetRequiredNode(classId);
        var members = node.Members?.ToList() ?? [];
        members.Add(new DiagramNodeMember(name, kind, type, visibility));
        this.nodes[classId] = node with { Members = members.ToArray() };
        return this;
    }

    /// <summary>
    /// Adds a property member to a class-like node.
    /// </summary>
    /// <param name="classId">The target class identifier.</param>
    /// <param name="name">The property name.</param>
    /// <param name="type">The optional property type.</param>
    /// <param name="visibility">The property visibility.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new ClassDiagramBuilder()
    ///     .AddClass("Order")
    ///     .AddProperty("Order", "Status", "string");
    /// </code>
    /// </example>
    public ClassDiagramBuilder AddProperty(string classId, string name, string type = null, DiagramVisibility visibility = DiagramVisibility.Public)
    {
        return this.AddMember(classId, name, DiagramMemberKind.Property, type, visibility);
    }

    /// <summary>
    /// Adds a method member to a class-like node.
    /// </summary>
    /// <param name="classId">The target class identifier.</param>
    /// <param name="name">The method name or signature.</param>
    /// <param name="returnType">The optional return type.</param>
    /// <param name="visibility">The method visibility.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new ClassDiagramBuilder()
    ///     .AddClass("OrderService")
    ///     .AddMethod("OrderService", "CreateOrder", "Result");
    /// </code>
    /// </example>
    public ClassDiagramBuilder AddMethod(string classId, string name, string returnType = null, DiagramVisibility visibility = DiagramVisibility.Public)
    {
        return this.AddMember(classId, name, DiagramMemberKind.Method, returnType, visibility);
    }

    /// <summary>
    /// Adds a relationship between two class-like nodes.
    /// </summary>
    /// <param name="from">The source class identifier.</param>
    /// <param name="to">The target class identifier.</param>
    /// <param name="kind">The relationship kind.</param>
    /// <param name="label">The optional relationship label.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new ClassDiagramBuilder()
    ///     .AddClass("OrderService")
    ///     .AddClass("IOrderRepository", kind: DiagramNodeKind.Interface)
    ///     .AddRelationship("OrderService", "IOrderRepository", DiagramEdgeKind.Dependency, "uses");
    /// </code>
    /// </example>
    public ClassDiagramBuilder AddRelationship(string from, string to, DiagramEdgeKind kind = DiagramEdgeKind.Association, string label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);

        _ = this.GetRequiredNode(from);
        _ = this.GetRequiredNode(to);
        this.edges.Add(new DiagramEdge(from, to, label, kind));
        return this;
    }

    /// <summary>
    /// Builds the class diagram document.
    /// </summary>
    /// <returns>The reusable diagram document.</returns>
    /// <example>
    /// <code>
    /// var document = new ClassDiagramBuilder()
    ///     .AddClass("Order")
    ///     .Build();
    /// </code>
    /// </example>
    public DiagramDocument Build()
    {
        return new DiagramDocument(
            DiagramKind.Class,
            this.nodeOrder.Select(id => this.nodes[id]).ToArray(),
            this.edges.ToArray(),
            [],
            []);
    }

    private DiagramNode GetRequiredNode(string id)
    {
        if (this.nodes.TryGetValue(id, out var node))
        {
            return node;
        }

        throw new InvalidOperationException($"Class node '{id}' must be added before it can be referenced.");
    }

    private void UpsertNode(string id, string label, DiagramNodeKind kind, string stereotype, IReadOnlyList<DiagramNodeMember> members)
    {
        if (!this.nodes.TryGetValue(id, out var node))
        {
            this.nodeOrder.Add(id);
            this.nodes[id] = new DiagramNode(id, label, kind, stereotype, members);
            return;
        }

        this.nodes[id] = node with
        {
            Label = label ?? node.Label,
            Kind = kind,
            Stereotype = stereotype ?? node.Stereotype,
            Members = members ?? node.Members,
        };
    }
}