// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a minimal builder for sequence diagram documents.
/// </summary>
/// <example>
/// <code>
/// var document = new SequenceDiagramBuilder()
///     .AddParticipant("User", kind: DiagramNodeKind.Actor)
///     .AddParticipant("Api", "Todo API")
///     .AddMessage("User", "Api", "GET /todos")
///     .Build();
/// </code>
/// </example>
public class SequenceDiagramBuilder
{
    private readonly List<DiagramEdge> edges = [];
    private readonly List<DiagramNote> notes = [];
    private readonly Dictionary<string, DiagramNode> nodes = new(StringComparer.Ordinal);
    private readonly List<string> nodeOrder = [];

    /// <summary>
    /// Adds a participant to the document.
    /// </summary>
    /// <param name="id">The stable participant identifier.</param>
    /// <param name="label">The optional participant label.</param>
    /// <param name="kind">The participant kind.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new SequenceDiagramBuilder()
    ///     .AddParticipant("User", kind: DiagramNodeKind.Actor);
    /// </code>
    /// </example>
    public SequenceDiagramBuilder AddParticipant(string id, string label = null, DiagramNodeKind kind = DiagramNodeKind.Participant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        this.UpsertNode(id, label, kind);
        return this;
    }

    /// <summary>
    /// Adds a message to the document.
    /// </summary>
    /// <param name="from">The source participant identifier.</param>
    /// <param name="to">The target participant identifier.</param>
    /// <param name="label">The message label.</param>
    /// <param name="kind">The message kind.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new SequenceDiagramBuilder()
    ///     .AddMessage("User", "Api", "GET /todos");
    /// </code>
    /// </example>
    public SequenceDiagramBuilder AddMessage(string from, string to, string label, DiagramEdgeKind kind = DiagramEdgeKind.Message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        this.EnsureParticipant(from);
        this.EnsureParticipant(to);
        this.edges.Add(new DiagramEdge(from, to, label, kind));
        return this;
    }

    /// <summary>
    /// Adds a note to the document.
    /// </summary>
    /// <param name="targetId">The target participant identifier.</param>
    /// <param name="text">The note text.</param>
    /// <param name="position">The note position.</param>
    /// <returns>The current builder.</returns>
    /// <example>
    /// <code>
    /// var builder = new SequenceDiagramBuilder()
    ///     .AddNote("Api", "cached response", DiagramNotePosition.Right);
    /// </code>
    /// </example>
    public SequenceDiagramBuilder AddNote(string targetId, string text, DiagramNotePosition position = DiagramNotePosition.Right)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        this.EnsureParticipant(targetId);
        this.notes.Add(new DiagramNote(targetId, text, position));
        return this;
    }

    /// <summary>
    /// Builds the sequence diagram document.
    /// </summary>
    /// <returns>The reusable diagram document.</returns>
    /// <example>
    /// <code>
    /// var document = new SequenceDiagramBuilder()
    ///     .AddParticipant("User")
    ///     .Build();
    /// </code>
    /// </example>
    public DiagramDocument Build()
    {
        return new DiagramDocument(
            DiagramKind.Sequence,
            this.nodeOrder.Select(id => this.nodes[id]).ToArray(),
            this.edges.ToArray(),
            this.notes.ToArray(),
            []);
    }

    private void EnsureParticipant(string id)
    {
        if (!this.nodes.ContainsKey(id))
        {
            this.UpsertNode(id, null, DiagramNodeKind.Participant);
        }
    }

    private void UpsertNode(string id, string label, DiagramNodeKind kind)
    {
        if (!this.nodes.TryGetValue(id, out var node))
        {
            this.nodeOrder.Add(id);
            this.nodes[id] = new DiagramNode(id, label, kind);
            return;
        }

        this.nodes[id] = node with
        {
            Label = label ?? node.Label,
            Kind = kind,
        };
    }
}