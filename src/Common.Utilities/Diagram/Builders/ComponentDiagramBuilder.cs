// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a minimal builder for component diagram documents.
/// </summary>
/// <example>
/// <code>
/// var document = new ComponentDiagramBuilder(DiagramDirection.LeftToRight)
///     .AddComponent("Api", "Orders API")
///     .AddComponent("Database", kind: DiagramNodeKind.Database)
///     .AddDependency("Api", "Database", "reads")
///     .Build();
/// </code>
/// </example>
public class ComponentDiagramBuilder
{
    private readonly DiagramDirection direction;
    private readonly List<DiagramEdge> edges = [];
    private readonly List<DiagramGroup> groups = [];
    private readonly List<DiagramNode> nodes = [];
    private readonly List<DiagramNote> notes = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentDiagramBuilder"/> class.
    /// </summary>
    /// <param name="direction">The preferred diagram direction.</param>
    public ComponentDiagramBuilder(DiagramDirection direction = DiagramDirection.LeftToRight)
    {
        this.direction = direction;
    }

    /// <summary>
    /// Adds a component-like node to the document.
    /// </summary>
    /// <param name="id">The component identifier.</param>
    /// <param name="label">The optional component label.</param>
    /// <param name="kind">The component kind.</param>
    /// <param name="stereotype">The optional stereotype.</param>
    /// <returns>The current builder.</returns>
    public ComponentDiagramBuilder AddComponent(string id, string label = null, DiagramNodeKind kind = DiagramNodeKind.Component, string stereotype = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        this.nodes.Add(new DiagramNode(id, label, kind, stereotype));
        return this;
    }

    /// <summary>
    /// Adds a dependency between two component-like nodes.
    /// </summary>
    /// <param name="from">The source component identifier.</param>
    /// <param name="to">The target component identifier.</param>
    /// <param name="label">The optional relationship label.</param>
    /// <param name="kind">The relationship kind.</param>
    /// <returns>The current builder.</returns>
    public ComponentDiagramBuilder AddDependency(string from, string to, string label = null, DiagramEdgeKind kind = DiagramEdgeKind.Dependency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);

        this.edges.Add(new DiagramEdge(from, to, label, kind));
        return this;
    }

    /// <summary>
    /// Adds a note to the document.
    /// </summary>
    /// <param name="targetId">The target node identifier.</param>
    /// <param name="text">The note text.</param>
    /// <param name="position">The note position.</param>
    /// <returns>The current builder.</returns>
    public ComponentDiagramBuilder AddNote(string targetId, string text, DiagramNotePosition position = DiagramNotePosition.Right)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        this.notes.Add(new DiagramNote(targetId, text, position));
        return this;
    }

    /// <summary>
    /// Adds a group to the document.
    /// </summary>
    /// <param name="id">The group identifier.</param>
    /// <param name="label">The optional group label.</param>
    /// <param name="kind">The group kind.</param>
    /// <param name="nodeIds">The grouped node identifiers.</param>
    /// <returns>The current builder.</returns>
    public ComponentDiagramBuilder AddGroup(string id, string label, DiagramGroupKind kind, IReadOnlyList<string> nodeIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(nodeIds);

        this.groups.Add(new DiagramGroup(id, label, kind, nodeIds));
        return this;
    }

    /// <summary>
    /// Builds the component diagram document.
    /// </summary>
    /// <returns>The reusable diagram document.</returns>
    public DiagramDocument Build()
    {
        return new DiagramDocument(
            DiagramKind.Component,
            this.nodes.ToArray(),
            this.edges.ToArray(),
            this.notes.ToArray(),
            this.groups.ToArray(),
            this.direction);
    }
}