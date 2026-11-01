// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Records source admission and actual processing lifetime, including partial results and interrupted enumeration.</summary>
/// <example><code>services.AddDataPorter().WithAuditing();</code></example>
public sealed class AuditingDataPorterOperationBehavior(IServiceScopeFactory scopes) : IDataPorterOperationBehavior
{
    /// <inheritdoc />
    public Task<TResult> ExecuteAsync<TResult>(DataPorterOperationContext context, Func<Task<TResult>> next) where TResult : IResult
        => AuditSourceExecutor.RunAsync(scopes, builder => Configure(builder, context), next, new() { Feature = "DataPorter", ExecutionStatus = "Invoked" },
            starting: (_, scope) => Start(scope, context), completed: (scope, result) => Complete(scope, context, result.IsFailure));

    /// <inheritdoc />
    public async IAsyncEnumerable<Result<T>> ExecuteStreamAsync<T>(DataPorterOperationContext context, Func<IAsyncEnumerable<Result<T>>> next,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, new()
    {
        await using var lease = AuditSourceLease.Begin(scopes, builder => Configure(builder, context), new() { Feature = "DataPorter", ExecutionStatus = "Invoked" }, scope => Start(scope, context));
        IAsyncEnumerator<Result<T>> source = null;
        var exhausted = false;
        var failed = false;
        var rejected = false;
        try
        {
            try { source = next().GetAsyncEnumerator(cancellationToken); }
            catch (Exception error) { failed = true; lease.Fail(error); throw; }

            while (true)
            {
                bool moved;
                Result<T> current = default;
                try
                {
                    moved = await source.MoveNextAsync().ConfigureAwait(false);
                    if (moved) current = source.Current;
                }
                catch (Exception error) { failed = true; lease.Fail(error); throw; }

                if (!moved) { exhausted = true; break; }

                rejected |= current.IsFailure;
                yield return current;
            }
        }
        finally
        {
            try { if (source is not null) await source.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { failed = true; lease.Fail(error); throw; }
            finally
            {
                lease.Observe(scope =>
                {
                    Counts(scope, context);
                    if (!failed && exhausted) Complete(scope, context, rejected);
                    else if (!failed) (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = "Incomplete" });
                });
            }
        }
    }

    private static void Configure(AuditScopeOptionsBuilder builder, DataPorterOperationContext context) => builder.Key("dataporter." + context.Operation.ToLowerInvariant()).Kind("DataPorter");
    private static void Start(IAuditScope scope, DataPorterOperationContext context)
    {
        scope.SetDetail("dataporter.operation", AuditValue.From(context.Operation));
        scope.SetDetail("dataporter.format", context.Format.Key?.Length <= 128 ? AuditValue.From(context.Format.Key) : AuditValue.Policy("Oversized"));
        if (context.DataType is not null) scope.SetDetail("dataporter.dataType", context.DataType.FullName?.Length <= 512 ? AuditValue.From(context.DataType.FullName) : AuditValue.Policy("Oversized"));
    }
    private static void Counts(IAuditScope scope, DataPorterOperationContext context)
    {
        scope.SetDetail("dataporter.processingStarted", AuditValue.From(context.ProcessingStarted));
        scope.SetDetail("dataporter.observedRows", AuditValue.From(context.TotalRows));
        scope.SetDetail("dataporter.acceptedRows", AuditValue.From(context.SuccessfulRows));
        scope.SetDetail("dataporter.failedRows", AuditValue.From(context.FailedRows));
        scope.SetDetail("dataporter.skippedRows", AuditValue.From(context.SkippedRows));
    }
    private static void Complete(IAuditScope scope, DataPorterOperationContext context, bool failed)
    {
        Counts(scope, context);
        var partial = context.FailedRows > 0 || context.HasErrors;
        scope.Complete(context.Canceled ? AuditOutcome.Canceled : partial ? AuditOutcome.Failed : failed ? context.ProcessingStarted ? AuditOutcome.Failed : AuditOutcome.Rejected : AuditOutcome.Succeeded);
        (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = context.Canceled ? "Canceled" : partial ? "Partial" : failed ? context.ProcessingStarted ? "Failed" : "Rejected" : "Completed" });
    }
}
