// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a minimal builder for state diagram documents.
/// </summary>
/// <example>
/// <code>
/// var document = new StateDiagramBuilder()
///     .AddState("Created")
///     .AddState("Completed")
///     .AddTransition("Created", "Completed", "done")
///     .Build();
/// </code>
/// </example>
public class StateDiagramBuilder
{
    private readonly List<DiagramEdge> edges = [];
    private readonly List<DiagramGroup> groups = [];
    private readonly List<DiagramNode> nodes = [];
    private readonly List<DiagramNote> notes = [];

    /// <summary>
    /// Adds a state node to the document.
    /// </summary>
    /// <param name="id">The state identifier.</param>
    /// <param name="label">The optional state label.</param>
    /// <param name="kind">The state kind.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new StateDiagramBuilder()
    ///     .AddState("AwaitingApproval", "Awaiting Approval");
    /// </code>
    /// </example>
    public StateDiagramBuilder AddState(string id, string label = null, DiagramNodeKind kind = DiagramNodeKind.Normal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        this.nodes.Add(new DiagramNode(id, label, kind));
        return this;
    }

    /// <summary>
    /// Adds a transition to the document.
    /// </summary>
    /// <param name="from">The source state identifier.</param>
    /// <param name="to">The target state identifier.</param>
    /// <param name="label">The optional transition label.</param>
    /// <param name="kind">The transition kind.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new StateDiagramBuilder()
    ///     .AddTransition("Created", "Running", "started");
    /// </code>
    /// </example>
    public StateDiagramBuilder AddTransition(string from, string to, string label = null, DiagramEdgeKind kind = DiagramEdgeKind.Normal)
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
    /// <example>
    /// <code>
    /// var builder = new StateDiagramBuilder()
    ///     .AddNote("AwaitingApproval", "current_state");
    /// </code>
    /// </example>
    public StateDiagramBuilder AddNote(string targetId, string text, DiagramNotePosition position = DiagramNotePosition.Right)
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
    /// <example>
    /// <code>
    /// var builder = new StateDiagramBuilder()
    ///     .AddGroup("parallel_group", "Parallel", DiagramGroupKind.Parallel, ["Left", "Right"]);
    /// </code>
    /// </example>
    public StateDiagramBuilder AddGroup(string id, string label, DiagramGroupKind kind, IReadOnlyList<string> nodeIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(nodeIds);

        this.groups.Add(new DiagramGroup(id, label, kind, nodeIds));
        return this;
    }

    /// <summary>
    /// Builds the state diagram document.
    /// </summary>
    /// <returns>The reusable diagram document.</returns>
    /// <example>
    /// <code>
    /// var document = new StateDiagramBuilder()
    ///     .AddState("Created")
    ///     .Build();
    /// </code>
    /// </example>
    public DiagramDocument Build()
    {
        return new DiagramDocument(
            DiagramKind.State,
            this.nodes.ToArray(),
            this.edges.ToArray(),
            this.notes.ToArray(),
            this.groups.ToArray());
    }
}