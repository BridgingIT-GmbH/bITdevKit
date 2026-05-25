// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Provides shared runtime metadata helpers for orchestration state visits, helper keys, and durable wait plans.
/// </summary>
internal static class OrchestrationRuntimeMetadata
{
    internal const string ApprovalPrefix = "__orchestration.approval.";
    internal const string ChildPrefix = "__orchestration.child.";
    internal const string HumanTaskPrefix = "__orchestration.human-task.";
    internal const string LoopPrefix = "__orchestration.loop.";
    internal const string ParallelPrefix = "__orchestration.parallel.";
    internal const string WaitPlanPropertyName = "__orchestration.wait.plan";

    private const string StateVisitPrefix = "__orchestration.state.visit.";

    /// <summary>
    /// Advances and stores the visit counter for a state.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="context">The orchestration context.</param>
    /// <param name="stateName">The state name.</param>
    /// <returns>The current visit number for the state.</returns>
    public static int EnterStateVisit<TData>(OrchestrationContext<TData> context, string stateName)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);

        var visit = GetStateVisit(context, stateName) + 1;
        context.Properties[BuildStateVisitKey(stateName)] = visit;
        return visit;
    }

    /// <summary>
    /// Gets the current visit counter for a state.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="context">The orchestration context.</param>
    /// <param name="stateName">The optional state name. When omitted, the current context state is used.</param>
    /// <returns>The current visit number.</returns>
    public static int GetStateVisit<TData>(OrchestrationContext<TData> context, string stateName = null)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(context);

        var effectiveStateName = stateName ?? context.CurrentState;
        if (string.IsNullOrWhiteSpace(effectiveStateName))
        {
            return 0;
        }

        return context.Properties.Get<int>(BuildStateVisitKey(effectiveStateName), 0);
    }

    /// <summary>
    /// Builds a state-visit-scoped helper key for durable helper state.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="prefix">The helper key prefix.</param>
    /// <param name="context">The orchestration context.</param>
    /// <param name="helperName">The helper name.</param>
    /// <returns>The scoped helper key.</returns>
    public static string BuildHelperKey<TData>(string prefix, OrchestrationContext<TData> context, string helperName)
        where TData : class, IOrchestrationData
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(helperName);

        var stateName = context.CurrentState ?? string.Empty;
        var visit = Math.Max(1, GetStateVisit(context, stateName));
        return $"{prefix}{stateName}:{visit}:{helperName}";
    }

    /// <summary>
    /// Removes helper state from previous visits to the same state while preserving the current visit scope.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="context">The orchestration context.</param>
    /// <param name="stateName">The state name.</param>
    /// <param name="currentVisit">The active state visit number.</param>
    public static void CleanupStateScopedHelperKeys<TData>(OrchestrationContext<TData> context, string stateName, int currentVisit)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);

        if (currentVisit <= 0)
        {
            return;
        }

        var activePrefixes = new[]
        {
            ApprovalPrefix,
            ChildPrefix,
            HumanTaskPrefix,
            LoopPrefix,
            ParallelPrefix,
        };

        context.Properties.RemoveAll((key, _) =>
            activePrefixes.Any(prefix =>
                key.StartsWith($"{prefix}{stateName}:", StringComparison.OrdinalIgnoreCase) &&
                !key.StartsWith($"{prefix}{stateName}:{currentVisit}:", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Resolves the effective externally visible outcome for the current status and last outcome.
    /// </summary>
    /// <param name="status">The orchestration status.</param>
    /// <param name="lastOutcome">The last recorded orchestration outcome.</param>
    /// <returns>The effective outcome name.</returns>
    public static string GetEffectiveOutcome(OrchestrationStatus status, OrchestrationOutcome lastOutcome)
    {
        if (status == OrchestrationStatus.Failed)
        {
            return OrchestrationStatus.Failed.ToString();
        }

        if (lastOutcome is not null)
        {
            return lastOutcome.Kind.ToString();
        }

        return status switch
        {
            OrchestrationStatus.Completed => OrchestrationOutcomeKind.Complete.ToString(),
            OrchestrationStatus.Cancelled => OrchestrationOutcomeKind.Cancel.ToString(),
            OrchestrationStatus.Terminated => OrchestrationOutcomeKind.Terminate.ToString(),
            OrchestrationStatus.Waiting => OrchestrationOutcomeKind.Wait.ToString(),
            _ => null,
        };
    }

    /// <summary>
    /// Stores or clears the durable wait plan on the orchestration context.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="context">The orchestration context.</param>
    /// <param name="plan">The wait plan to store, or <see langword="null"/> to clear it.</param>
    public static void SetWaitPlan<TData>(OrchestrationContext<TData> context, OrchestrationWaitPlan plan)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(context);

        if (plan is null)
        {
            context.Properties.Remove(WaitPlanPropertyName);
            return;
        }

        context.Properties[WaitPlanPropertyName] = plan;
    }

    /// <summary>
    /// Attempts to retrieve the durable wait plan from the orchestration context.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="context">The orchestration context.</param>
    /// <param name="plan">The retrieved wait plan when available.</param>
    /// <returns><see langword="true"/> when a wait plan exists; otherwise <see langword="false"/>.</returns>
    public static bool TryGetWaitPlan<TData>(OrchestrationContext<TData> context, out OrchestrationWaitPlan plan)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Properties.TryGet<OrchestrationWaitPlan>(WaitPlanPropertyName, out plan) && plan is not null)
        {
            return true;
        }

        plan = null;
        return false;
    }

    private static string BuildStateVisitKey(string stateName)
    {
        return $"{StateVisitPrefix}{stateName}";
    }
}

/// <summary>
/// Describes a durable waiting boundary persisted for recovery.
/// </summary>
internal sealed record OrchestrationWaitPlan
{
    public string Kind { get; init; }

    public string StateName { get; init; }

    public int ActivityIndex { get; init; }

    public string Reason { get; init; }

    public DateTimeOffset StartedUtc { get; init; }

    public string[] SignalNames { get; init; } = [];

    public IReadOnlyList<OrchestrationExpectedTimer> ExpectedTimers { get; init; } = [];
}

/// <summary>
/// Describes an expected timer artifact for a durable waiting boundary.
/// </summary>
internal sealed record OrchestrationExpectedTimer
{
    public string TriggerKind { get; init; }

    public DateTimeOffset DueTimeUtc { get; init; }

    public string TargetState { get; init; }

    public string Continuation { get; init; }
}

/// <summary>
/// Represents the serialized property payload used to recover durable wait-plan metadata from stored context snapshots.
/// </summary>
internal sealed record OrchestrationStoredContextSnapshot
{
    public Dictionary<string, OrchestrationContextPropertySnapshot> Properties { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
