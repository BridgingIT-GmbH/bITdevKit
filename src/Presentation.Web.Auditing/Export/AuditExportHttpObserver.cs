// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;

/// <summary>Reconciles shared NDJSON transport outcomes with Auditing's expected prefix and final source checks.</summary>
/// <example><code>result.WithObserver(new AuditExportHttpObserver(session, runtime, principalBinding));</code></example>
public sealed class AuditExportHttpObserver(AuditExportSession session, AuditWebRuntime runtime, string principalBinding) : INdjsonStreamObserver
{
    /// <inheritdoc />
    public async Task OnFinishedAsync(NdjsonStreamOutcome outcome, CancellationToken cancellationToken = default)
    {
        try
        {
            var confirmed = await session.ConfirmAsync(outcome, cancellationToken).ConfigureAwait(false);
            var completion = confirmed.IsSuccess ? confirmed.Value : confirmed.Errors.OfType<AuditExportFailureError>().FirstOrDefault()?.Completion
                ?? new AuditExportCompletion { Metadata = session.Metadata, Status = "Failed", ReasonCode = "AuditExportOutputUnconfirmed" };
            if (completion.OutputConfirmed)
            {
                var token = runtime.Tokens.Protect(completion, principalBinding);
                completion = completion.Progress.IsCheckpoint ? completion with { CheckpointToken = token } : completion with { ContinuationToken = token };
            }

            cancellationToken.ThrowIfCancellationRequested();
            runtime.Attempts.Finish(session.Metadata.ExportId, completion);
        }
        finally { await session.DisposeAsync().ConfigureAwait(false); }
    }
}
