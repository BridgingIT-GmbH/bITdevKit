// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Orchestrations.Dashboard;

using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Presentation.Web.Orchestrations.Dashboard.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class OrchestrationDefinitionReader
{
    internal static IReadOnlyList<string> GetNames(IServiceProvider services, OrchestrationRegistrationStore registrations, ILogger logger)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in registrations?.GetRegisteredTypes() ?? [])
        {
            try
            {
                var orchestration = services.GetRequiredService(type);
                var name = type.GetProperty("Name")?.GetValue(orchestration) as string;
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
            }
            catch (Exception exception)
            {
                logger?.LogWarning(exception, "Orchestrations dashboard: definition name unavailable ({OrchestrationType}).", type.FullName);
            }
        }

        return names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static OrchestrationDefinitionSummary Read(Type type, IServiceProvider services, OrchestrationRegistrationStore registrations,
        ILogger logger)
    {
        var name = type.Name;
        try
        {
            var orchestration = services.GetRequiredService(type);
            name = type.GetProperty("Name")?.GetValue(orchestration) as string ?? name;
            var definition = type.GetMethod("GetDefinition", Type.EmptyTypes)?.Invoke(orchestration, null);
            if (definition is null) return new(name, type.FullName ?? type.Name) { Error = "Definition details unavailable." };
            return (OrchestrationDefinitionSummary)typeof(OrchestrationDefinitionReader)
                .GetMethod(nameof(Project), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(registrations.GetDataType(type)).Invoke(null, [name, type, definition]);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Orchestrations dashboard: definition details unavailable ({OrchestrationType}).", type.FullName);
            return new(name, type.FullName ?? type.Name) { Error = "Definition details could not be loaded." };
        }
    }

    private static OrchestrationDefinitionSummary Project<TData>(string name, Type type, OrchestrationDefinition<TData> definition)
        where TData : class, IOrchestrationData => new(name, type.FullName ?? type.Name)
        {
            TypeLabel = type.PrettyName(false),
            DataTypeLabel = typeof(TData).PrettyName(false),
            DataTypeName = typeof(TData).FullName ?? typeof(TData).Name,
            InitialState = definition.InitialState,
            States = definition.States.Values.Select(state => new OrchestrationDefinitionState(
                state.Name,
                state.Activities.Select(ActivityLabel).Concat(state.SignalHandlers.Concat(state.WaitingSignals)
                    .SelectMany(signal => signal.Activities.Select(activity => $"Signal {signal.SignalName}: {ActivityLabel(activity)}"))).ToArray(),
                state.Transitions.Select(transition => $"Transition → {transition.TargetState}")
                    .Concat(state.SignalHandlers.Select(signal => $"Signal {signal.SignalName}" + Target(signal.TargetState)))
                    .Concat(state.WaitingSignals.Select(signal => $"Wait for {signal.SignalName}" + Target(signal.TargetState)))
                    .Concat(state.Timers.Select(timer => $"Timer {timer.Delay:g}" + Target(timer.TargetState))).ToArray(),
                state.TerminalDirectiveKind == OrchestrationTerminalDirectiveKind.None ? null : state.TerminalDirectiveKind.ToString())).ToArray()
        };

    private static string Target(string target) => string.IsNullOrEmpty(target) ? string.Empty : " → " + target;

    private static string ActivityLabel<TData>(OrchestrationActivityDefinition<TData> activity) where TData : class, IOrchestrationData =>
        activity.Name + (activity.RetryPolicy is not null ? " · retry" : string.Empty) +
        (activity.Compensation is not null ? " · compensate: " + activity.Compensation.Name : string.Empty);
}
