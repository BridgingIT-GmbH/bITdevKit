// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Records finite selected scans, event roots and processor children, including caught processor failures.</summary>
/// <example><code>builder.WithAuditing(new() { Scans = true });</code></example>
public sealed class AuditingFileProcessingBehavior(IServiceScopeFactory scopes, FileMonitoringAuditOptions options = null) : IFileProcessingExecutionBehavior
{
    /// <inheritdoc />
    public Task<TResult> ExecuteAsync<TResult>(FileProcessingExecutionContext context, Func<Task<TResult>> next)
        => AuditSourceExecutor.RunAsync(scopes, builder => builder.Key("filemonitoring." + context.Operation.ToLowerInvariant()).Kind("File" + context.Operation), next,
            new() { Feature = "FileMonitoring", RequestId = context.EventId, FlowId = context.ScanId, HandlerType = context.ProcessorType?.FullName, ExecutionStatus = "Invoked" },
            starting: (_, scope) =>
            {
                context.CaptureAdmitted = true;
                scope.SetDetail("filemonitoring.location", context.LocationName?.Length <= 128 ? AuditValue.From(context.LocationName) : AuditValue.Policy("Oversized"));
                if (context.EventKind is not null) scope.SetDetail("filemonitoring.eventKind", AuditValue.From(context.EventKind));
            }, completed: (scope, result) =>
            {
                var failed = context.Failed || result is IResult { IsFailure: true };
                scope.Complete(context.Canceled ? AuditOutcome.Canceled : failed ? AuditOutcome.Failed : AuditOutcome.Succeeded);
                (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = context.Canceled ? "Canceled" : failed ? "Failed" : "Completed" });
                if (result is FileScanContext scan) scope.SetDetail("filemonitoring.detectedEvents", AuditValue.From((long)(scan.Events?.Count ?? 0)));
            }, select: () =>
            {
                var selection = context.Selection ?? options ?? new() { Events = false, Processors = false };
                if (selection.SelectLocation?.Invoke(context.LocationName) == false) return false;
                return context.Operation switch
                {
                    "Scan" => selection.Scans && context.ExplicitScan,
                    "Processor" => (context.SelectProcessor ?? selection.Processors) && selection.SelectProcessor?.Invoke(context.ProcessorType) != false,
                    _ => selection.Events
                };
            });
}
