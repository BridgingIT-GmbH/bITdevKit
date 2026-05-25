// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using BridgingIT.DevKit.Common;

namespace BridgingIT.DevKit.Application.Orchestrations;
/// <summary>
/// Projects persisted orchestration runtime state into reusable diagram documents.
/// </summary>
public class OrchestrationInstanceDiagramProjector
{
    /// <summary>
    /// Projects the supplied orchestration instance data.
    /// </summary>
    /// <param name="definitionDocument">The reusable definition diagram document.</param>
    /// <param name="instance">The orchestration instance model.</param>
    /// <param name="history">The persisted orchestration history.</param>
    /// <param name="signals">The persisted orchestration signals.</param>
    /// <param name="timers">The persisted orchestration timers.</param>
    /// <param name="options">The projection options.</param>
    /// <returns>The reusable diagram document.</returns>
    /// <example>
    /// <code>
    /// var document = projector.Project(definitionDocument, instance, history, signals, timers, new OrchestrationDiagramOptions());
    /// </code>
    /// </example>
    public DiagramDocument Project(
        DiagramDocument definitionDocument,
        OrchestrationInstanceModel instance,
        IReadOnlyList<OrchestrationHistoryModel> history,
        IReadOnlyList<OrchestrationSignalModel> signals,
        IReadOnlyList<OrchestrationTimerModel> timers,
        OrchestrationDiagramOptions options)
    {
        ArgumentNullException.ThrowIfNull(definitionDocument);
        ArgumentNullException.ThrowIfNull(instance);

        options ??= new OrchestrationDiagramOptions();
        history ??= [];
        signals ??= [];
        timers ??= [];

        return this.ProjectFromDefinition(definitionDocument, instance, history, options);
    }

    private DiagramDocument ProjectFromDefinition(
        DiagramDocument definitionDocument,
        OrchestrationInstanceModel instance,
        IReadOnlyList<OrchestrationHistoryModel> history,
        OrchestrationDiagramOptions options)
    {
        var nodes = definitionDocument.Nodes.ToArray();
        var edges = definitionDocument.Edges
            .Where(edge => this.IncludeEdge(edge, options))
            .ToArray();
        var notes = BuildRuntimeNotes(instance, history, options);

        return new DiagramDocument(definitionDocument.Kind, nodes, edges, notes, definitionDocument.Groups, definitionDocument.Direction);
    }

    private static IReadOnlyList<DiagramNote> BuildRuntimeNotes(
        OrchestrationInstanceModel instance,
        IReadOnlyList<OrchestrationHistoryModel> history,
        OrchestrationDiagramOptions options)
    {
        var notes = new List<DiagramNote>();
        var visitedStates = history
            .Where(item => string.Equals(item.EventType, "StateEntered", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.State)
            .Where(state => !string.IsNullOrWhiteSpace(state))
            .ToList();

        if (visitedStates.Count == 0 && !string.IsNullOrWhiteSpace(instance.CurrentState))
        {
            visitedStates.Add(instance.CurrentState);
        }

        AddCurrentStateNote(notes, instance, visitedStates, options);
        AddParallelRuntime(notes, history, instance, options);

        if (options.IncludeActivities)
        {
            foreach (var activityEntry in history.Where(item => item.EventType is "ActivityCompleted" or "SignalActivityCompleted"))
            {
                if (!string.IsNullOrWhiteSpace(activityEntry.State) && !string.IsNullOrWhiteSpace(activityEntry.Activity))
                {
                    AddNote(notes, activityEntry.State, $"activity_{activityEntry.Activity}");
                }
            }
        }

        return notes;
    }

    private static void AddParallelRuntime(
        List<DiagramNote> notes,
        IReadOnlyList<OrchestrationHistoryModel> history,
        OrchestrationInstanceModel instance,
        OrchestrationDiagramOptions options)
    {
        var startedBranches = history
            .Where(item => string.Equals(item.EventType, "ParallelBranchActivityExecuted", StringComparison.OrdinalIgnoreCase))
            .Select(item => new ParallelBranchVisit(item.State, item.Activity, item.Message))
            .Distinct()
            .ToArray();
        var completedBranches = history
            .Where(item => string.Equals(item.EventType, "ParallelBranchCompleted", StringComparison.OrdinalIgnoreCase))
            .Select(item => new ParallelBranchVisit(item.State, item.Activity, item.Message))
            .ToHashSet();
        foreach (var branch in startedBranches)
        {
            var branchNodeId = $"Parallel_{branch.ParallelName}_{branch.BranchName}";

            var isCompleted = completedBranches.Contains(branch);
            var isFailed = string.Equals(instance.Status, nameof(OrchestrationStatus.Failed), StringComparison.OrdinalIgnoreCase) && !isCompleted;

            if (options.HighlightCurrentState && !isCompleted && !isFailed)
            {
                AddNote(notes, branchNodeId, "current_state");
            }
        }
    }

    private static void AddCurrentStateNote(
        List<DiagramNote> notes,
        OrchestrationInstanceModel instance,
        IReadOnlyList<string> visitedStates,
        OrchestrationDiagramOptions options)
    {
        if (!options.HighlightCurrentState || string.IsNullOrWhiteSpace(instance.CurrentState))
        {
            return;
        }

        var text = options.IncludeHistory && visitedStates.Count > 0
            ? $"current_state\nvisited: {string.Join(" -> ", visitedStates.Distinct(StringComparer.OrdinalIgnoreCase))}"
            : "current_state";

        AddNote(notes, instance.CurrentState, text);
    }

    private bool IncludeEdge(DiagramEdge edge, OrchestrationDiagramOptions options)
    {
        return edge.Kind switch
        {
            DiagramEdgeKind.Signal => options.IncludeSignals,
            DiagramEdgeKind.Timeout => options.IncludeTimers,
            _ => true,
        };
    }

    private static void AddNote(List<DiagramNote> notes, string targetId, string text, DiagramNotePosition position = DiagramNotePosition.Right)
    {
        if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (notes.Any(note =>
                string.Equals(note.TargetId, targetId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(note.Text, text, StringComparison.Ordinal) &&
                note.Position == position))
        {
            return;
        }

        notes.Add(new DiagramNote(targetId, text, position));
    }

    private sealed record ParallelBranchVisit(string StateName, string ParallelName, string BranchName);
}