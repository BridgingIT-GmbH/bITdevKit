// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a minimal builder for activity diagram documents.
/// </summary>
/// <example>
/// <code>
/// var document = new ActivityDiagramBuilder()
///     .AddStart("Start")
///     .AddAction("Validate")
///     .AddTransition("Start", "Validate")
///     .Build();
/// </code>
/// </example>
public class ActivityDiagramBuilder
{
    private readonly FlowDiagramBuilder innerBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityDiagramBuilder"/> class.
    /// </summary>
    /// <param name="direction">The preferred diagram direction.</param>
    public ActivityDiagramBuilder(DiagramDirection direction = DiagramDirection.TopToBottom)
    {
        this.innerBuilder = new FlowDiagramBuilder(direction);
    }

    /// <summary>
    /// Adds a start node to the document.
    /// </summary>
    /// <param name="id">The start identifier.</param>
    /// <param name="label">The optional start label.</param>
    /// <returns>The current builder.</returns>
    public ActivityDiagramBuilder AddStart(string id, string label = null)
    {
        this.innerBuilder.AddNode(id, label, DiagramNodeKind.Start);
        return this;
    }

    /// <summary>
    /// Adds an action node to the document.
    /// </summary>
    /// <param name="id">The action identifier.</param>
    /// <param name="label">The optional action label.</param>
    /// <returns>The current builder.</returns>
    public ActivityDiagramBuilder AddAction(string id, string label = null)
    {
        this.innerBuilder.AddNode(id, label, DiagramNodeKind.Normal);
        return this;
    }

    /// <summary>
    /// Adds a decision node to the document.
    /// </summary>
    /// <param name="id">The decision identifier.</param>
    /// <param name="label">The optional decision label.</param>
    /// <returns>The current builder.</returns>
    public ActivityDiagramBuilder AddDecision(string id, string label = null)
    {
        this.innerBuilder.AddNode(id, label, DiagramNodeKind.Decision);
        return this;
    }

    /// <summary>
    /// Adds an end node to the document.
    /// </summary>
    /// <param name="id">The end identifier.</param>
    /// <param name="label">The optional end label.</param>
    /// <returns>The current builder.</returns>
    public ActivityDiagramBuilder AddEnd(string id, string label = null)
    {
        this.innerBuilder.AddNode(id, label, DiagramNodeKind.Terminal);
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
    public ActivityDiagramBuilder AddTransition(string from, string to, string label = null, DiagramEdgeKind kind = DiagramEdgeKind.Normal)
    {
        this.innerBuilder.AddTransition(from, to, label, kind);
        return this;
    }

    /// <summary>
    /// Adds a note to the document.
    /// </summary>
    /// <param name="targetId">The target node identifier.</param>
    /// <param name="text">The note text.</param>
    /// <param name="position">The note position.</param>
    /// <returns>The current builder.</returns>
    public ActivityDiagramBuilder AddNote(string targetId, string text, DiagramNotePosition position = DiagramNotePosition.Right)
    {
        this.innerBuilder.AddNote(targetId, text, position);
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
    public ActivityDiagramBuilder AddGroup(string id, string label, DiagramGroupKind kind, IReadOnlyList<string> nodeIds)
    {
        this.innerBuilder.AddGroup(id, label, kind, nodeIds);
        return this;
    }

    /// <summary>
    /// Builds the activity diagram document.
    /// </summary>
    /// <returns>The reusable diagram document.</returns>
    public DiagramDocument Build()
    {
        var document = this.innerBuilder.Build();
        return document with { Kind = DiagramKind.Activity };
    }
}