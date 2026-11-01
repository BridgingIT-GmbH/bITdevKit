// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Utilities;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Selects startup tasks locally without evaluating business metadata when capture is unavailable.</summary>
/// <example><code>var options = new StartupTaskAuditOptions { SelectTask = type => type == typeof(CustomerCacheTask) };</code></example>
public sealed record StartupTaskAuditOptions
{
    /// <summary>Selects task types during enabled execution. A null selector selects all configured task boundaries.</summary>
    /// <example><code>var selected = options.SelectTask?.Invoke(typeof(CustomerCacheTask));</code></example>
    public Func<Type, bool> SelectTask { get; init; }
}

/// <summary>Records one actual startup-task pipeline invocation, joining an explicit caller or owning a finite root.</summary>
/// <remarks>Placement inside retry records actual repeated invocations. The source contract exposes no native attempt number.</remarks>
/// <example><code>services.AddStartupTasks().WithBehavior&lt;AuditingStartupTaskBehavior&gt;();</code></example>
public sealed class AuditingStartupTaskBehavior(IServiceScopeFactory scopes, StartupTaskAuditOptions options = null) : IStartupTaskBehavior
{
    /// <inheritdoc />
    public Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
        => AuditSourceExecutor.RunAsync(scopes, builder => builder.For(task.GetType(), "Run").Kind("StartupTask"), async () =>
        {
            await next().ConfigureAwait(false);
            return true;
        }, new() { Feature = "StartupTasks", HandlerType = task.GetType().FullName, ExecutionStatus = "Invoked" },
            completed: (scope, _) => (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = "Completed" }),
            select: () => options?.SelectTask?.Invoke(task.GetType()) != false);
}
