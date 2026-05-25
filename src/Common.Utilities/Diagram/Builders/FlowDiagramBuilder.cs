// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a minimal builder for flow diagram documents.
/// </summary>
/// <example>
/// <code>
/// var document = new FlowDiagramBuilder()
///     .AddNode("Start", kind: DiagramNodeKind.Start)
///     .AddNode("Validate", "Validate Request")
///     .AddTransition("Start", "Validate")
///     .Build();
/// </code>
/// </example>
public class FlowDiagramBuilder
{
    private readonly DiagramDirection direction;
    private readonly List<DiagramEdge> edges = [];
    private readonly List<DiagramGroup> groups = [];
    private readonly List<DiagramNode> nodes = [];
    private readonly List<DiagramNote> notes = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowDiagramBuilder"/> class.
    /// </summary>
    /// <param name="direction">The preferred diagram direction.</param>
    public FlowDiagramBuilder(DiagramDirection direction = DiagramDirection.TopToBottom)
    {
        this.direction = direction;
    }

    /// <summary>
    /// Adds a node to the document.
    /// </summary>
    /// <param name="id">The node identifier.</param>
    /// <param name="label">The optional node label.</param>
    /// <param name="kind">The node kind.</param>
    /// <param name="stereotype">The optional stereotype.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new FlowDiagramBuilder()
    ///     .AddNode("Decision", kind: DiagramNodeKind.Decision);
    /// </code>
    /// </example>
    public FlowDiagramBuilder AddNode(string id, string label = null, DiagramNodeKind kind = DiagramNodeKind.Normal, string stereotype = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        this.nodes.Add(new DiagramNode(id, label, kind, stereotype));
        return this;
    }

    /// <summary>
    /// Adds a transition to the document.
    /// </summary>
    /// <param name="from">The source node identifier.</param>
    /// <param name="to">The target node identifier.</param>
    /// <param name="label">The optional transition label.</param>
    /// <param name="kind">The transition kind.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new FlowDiagramBuilder()
    ///     .AddTransition("Validate", "Persist", "valid");
    /// </code>
    /// </example>
    public FlowDiagramBuilder AddTransition(string from, string to, string label = null, DiagramEdgeKind kind = DiagramEdgeKind.Normal)
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
    public FlowDiagramBuilder AddNote(string targetId, string text, DiagramNotePosition position = DiagramNotePosition.Right)
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
    public FlowDiagramBuilder AddGroup(string id, string label, DiagramGroupKind kind, IReadOnlyList<string> nodeIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(nodeIds);

        this.groups.Add(new DiagramGroup(id, label, kind, nodeIds));
        return this;
    }

    /// <summary>
    /// Builds the flow diagram document.
    /// </summary>
    /// <returns>The reusable diagram document.</returns>
    /// <example>
    /// <code>
    /// var document = new FlowDiagramBuilder().Build();
    /// </code>
    /// </example>
    public DiagramDocument Build()
    {
        return new DiagramDocument(
            DiagramKind.Flow,
            this.nodes.ToArray(),
            this.edges.ToArray(),
            this.notes.ToArray(),
            this.groups.ToArray(),
            this.direction);
    }
}