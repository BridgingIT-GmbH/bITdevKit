// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Reflection;
using BridgingIT.DevKit.Common;

namespace BridgingIT.DevKit.Application.Orchestrations;
/// <summary>
/// Projects orchestration definitions into reusable diagram documents.
/// </summary>
public class OrchestrationDefinitionDiagramProjector
{
    private static readonly IServiceProvider EmptyProvider = new EmptyServiceProvider();

    /// <summary>
    /// Projects the supplied orchestration definition.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="definition">The orchestration definition.</param>
    /// <returns>The reusable diagram document.</returns>
    /// <example>
    /// <code>
    /// var document = projector.Project(orchestration.GetDefinition());
    /// </code>
    /// </example>
    public DiagramDocument Project<TData>(OrchestrationDefinition<TData> definition)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(definition);

        var builder = new StateDiagramBuilder();
        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edgeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddState(builder, nodeIds, definition.InitialState);
        AddEdge(builder, edgeKeys, "[*]", definition.InitialState);

        foreach (var state in definition.States.Values)
        {
            AddState(builder, nodeIds, state.Name);

            var parallelDefinitions = GetParallelDefinitions(state.Activities).ToArray();
            var renderedParallelSuccessors = false;
            foreach (var parallel in parallelDefinitions)
            {
                renderedParallelSuccessors = true;
                var joinNodeId = $"ParallelJoin_{parallel.Name}";
                AddState(builder, nodeIds, joinNodeId, DiagramNodeKind.Join);

                var branchNodeIds = new List<string>();
                foreach (var branch in parallel.Branches)
                {
                    var branchNodeId = $"Parallel_{parallel.Name}_{branch.Name}";
                    branchNodeIds.Add(branchNodeId);
                    AddState(builder, nodeIds, branchNodeId, DiagramNodeKind.Branch);
                    AddEdge(builder, edgeKeys, state.Name, branchNodeId, $"branch_{branch.Name}", DiagramEdgeKind.Branch);
                    AddEdge(builder, edgeKeys, branchNodeId, joinNodeId, "completed", DiagramEdgeKind.Join);
                }

                builder.AddGroup($"ParallelGroup_{parallel.Name}", parallel.Name, DiagramGroupKind.Parallel, branchNodeIds.ToArray());

                var joinLabel = parallel.JoinMode == OrchestrationParallelJoinMode.Any ? "join_any" : "join_all";
                foreach (var successor in GetSuccessors(state))
                {
                    AddEdge(builder, edgeKeys, joinNodeId, successor, joinLabel, DiagramEdgeKind.Join);
                }
            }

            foreach (var transition in state.Transitions)
            {
                if (!renderedParallelSuccessors)
                {
                    AddEdge(builder, edgeKeys, state.Name, transition.TargetState);
                }

                AddState(builder, nodeIds, transition.TargetState);
            }

            foreach (var signal in state.SignalHandlers.Concat(state.WaitingSignals).Where(signal => !string.IsNullOrWhiteSpace(signal.TargetState)))
            {
                AddState(builder, nodeIds, signal.TargetState);
                AddEdge(builder, edgeKeys, state.Name, signal.TargetState, $"signal_{signal.SignalName}", DiagramEdgeKind.Signal);
            }

            foreach (var timer in state.Timers.Where(timer => !string.IsNullOrWhiteSpace(timer.TargetState)))
            {
                AddState(builder, nodeIds, timer.TargetState);
                AddEdge(builder, edgeKeys, state.Name, timer.TargetState, "timeout", DiagramEdgeKind.Timeout);
            }

            if (!renderedParallelSuccessors && state.TerminalDirectiveKind is OrchestrationTerminalDirectiveKind.Complete or OrchestrationTerminalDirectiveKind.Cancel or OrchestrationTerminalDirectiveKind.Terminate)
            {
                AddEdge(builder, edgeKeys, state.Name, "[*]", kind: DiagramEdgeKind.Terminal);
            }
        }

        return builder.Build();
    }

    private static IEnumerable<string> GetSuccessors<TData>(OrchestrationStateDefinition<TData> state)
        where TData : class, IOrchestrationData
    {
        foreach (var target in state.Transitions.Select(item => item.TargetState))
        {
            yield return target;
        }

        if (state.TerminalDirectiveKind is OrchestrationTerminalDirectiveKind.Complete or OrchestrationTerminalDirectiveKind.Cancel or OrchestrationTerminalDirectiveKind.Terminate)
        {
            yield return "[*]";
        }
    }

    private static IEnumerable<OrchestrationParallelDefinition<TData>> GetParallelDefinitions<TData>(IEnumerable<OrchestrationActivityDefinition<TData>> activities)
        where TData : class, IOrchestrationData
    {
        foreach (var activity in activities)
        {
            if (TryGetCapturedValue(activity, out OrchestrationParallelDefinition<TData> parallelDefinition))
            {
                yield return parallelDefinition;
            }
        }
    }

    private static bool TryGetCapturedValue<TData, TValue>(OrchestrationActivityDefinition<TData> activity, out TValue value)
        where TData : class, IOrchestrationData
        where TValue : class
    {
        value = null;

        try
        {
            var activityInstance = activity.Factory(EmptyProvider);
            if (activityInstance is not InlineOrchestrationActivity<TData>)
            {
                return false;
            }

            var delegateField = activityInstance.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .FirstOrDefault(field => typeof(Delegate).IsAssignableFrom(field.FieldType));
            var callback = delegateField?.GetValue(activityInstance) as Delegate;
            var target = callback?.Target;
            if (target is null)
            {
                return false;
            }

            var capturedField = target.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(field => typeof(TValue).IsAssignableFrom(field.FieldType));
            value = capturedField?.GetValue(target) as TValue;
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void AddState(StateDiagramBuilder builder, HashSet<string> nodeIds, string id, DiagramNodeKind kind = DiagramNodeKind.Normal)
    {
        if (string.IsNullOrWhiteSpace(id) || !nodeIds.Add(id))
        {
            return;
        }

        builder.AddState(id, kind: kind);
    }

    private static void AddEdge(StateDiagramBuilder builder, HashSet<string> edgeKeys, string from, string to, string label = null, DiagramEdgeKind kind = DiagramEdgeKind.Normal)
    {
        var key = $"{from}|{to}|{label}|{kind}";
        if (!edgeKeys.Add(key))
        {
            return;
        }

        builder.AddTransition(from, to, label, kind);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
        {
            return null;
        }
    }
}