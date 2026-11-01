// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Preserves one business invocation and its result while owning optional source capture.</summary>
/// <example><code>return await AuditBehaviorExecutor.RunAsync(() => audit.BeginScope(options), next, metadata);</code></example>
public static class AuditBehaviorExecutor
{
    /// <summary>Observes one awaited boundary and finalizes its actual outcome under ReportOnly behavior.</summary>
    /// <example><code>var result = await AuditBehaviorExecutor.RunAsync(begin, next, new() { Feature = "Requester" });</code></example>
    public static async Task<T> RunAsync<T>(Func<IAuditScope> begin, Func<Task<T>> next, AuditSourceMetadata metadata = null, Action<IAuditScope, T> completed = null, Action<IAuditScope> starting = null, Action<IAuditScope, Exception> failed = null)
    {
        IAuditScope scope = NoOpAuditScope.Instance;
        try
        {
            scope = begin() ?? NoOpAuditScope.Instance;
            (scope as IAuditSourceScope)?.SetSource(metadata);
            if (scope.IsRecording) starting?.Invoke(scope);
        }
        catch (Exception) { Report(scope); }

        try
        {
            var result = await next().ConfigureAwait(false);
            try
            {
                scope.Complete(result is IResult { IsFailure: true } ? AuditOutcome.Rejected : AuditOutcome.Succeeded);
            }
            catch (Exception) { Report(scope); }

            try { if (scope.IsRecording) completed?.Invoke(scope, result); }
            catch (Exception) { Report(scope); }

            return result;
        }
        catch (Exception error)
        {
            try { scope.Fail(error); }
            catch (Exception) { Report(scope); }

            try { if (scope.IsRecording) failed?.Invoke(scope, error); }
            catch (Exception) { Report(scope); }

            throw;
        }
        finally
        {
            try { await scope.DisposeAsync().ConfigureAwait(false); }
            catch (Exception) { Report(scope); }
        }
    }

    private static void Report(IAuditScope scope)
    {
        try { (scope as IAuditCaptureDiagnostics)?.ReportFailure("AuditSourceMetadataFailed"); }
        catch (Exception) { /* Failure reporting never replaces business execution. */ }
    }
}
