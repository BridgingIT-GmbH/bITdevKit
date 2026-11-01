// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Http;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Streams an admitted attachment, publishing companion metadata after actual output reconciliation.</summary>
/// <example><code>return new AuditExportHttpResult(session, runtime, principalBinding);</code></example>
public sealed class AuditExportHttpResult(AuditExportSession session, AuditWebRuntime runtime, string principalBinding) : IResult
{
    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Audit-Export-Id"] = session.Metadata.ExportId.ToString("D");
        var name = $"audit-{session.Metadata.ExportId:N}.{session.Formatter.Extension}";
        try
        {
            if (session.Formatter.Format == "ndjson")
            {
                var remaining = session.Remaining;
                if (remaining <= TimeSpan.Zero) throw new AuditExportException("AuditExportCanceled");
                await NdjsonHttpResult.FromEncodedRecords<AuditExportEntry>(Frames(context.RequestAborted), new()
                {
                    Mode = NdjsonMode.Raw, DisableBatching = true, EnableCompression = false,
                    MaxRecordBytes = Math.Max(session.Options.GraphByteLimit, Math.Max(session.Options.MetadataByteReserve, session.Options.CompletionByteReserve)) - 1,
                    MaxDuration = remaining, WriteTimeout = remaining, CleanupTimeout = session.Options.CleanupTimeout, ObserverTimeout = session.Options.ObserverTimeout, DownloadFileName = name
                }).WithObserver(new AuditExportHttpObserver(session, runtime, principalBinding)).ExecuteAsync(context).ConfigureAwait(false);
            }
            else
            {
                context.Response.ContentType = session.Formatter.MediaType;
                context.Response.Headers.ContentDisposition = $"attachment; filename=\"{name}\"";
                var completed = await session.WriteAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
                var completion = completed.IsSuccess ? completed.Value : completed.Errors.OfType<AuditExportFailureError>().FirstOrDefault()?.Completion;
                if (completion is not null)
                {
                    if (completion.OutputConfirmed)
                    {
                        var token = runtime.Tokens.Protect(completion, principalBinding);
                        completion = completion.Progress.IsCheckpoint ? completion with { CheckpointToken = token } : completion with { ContinuationToken = token };
                    }

                    runtime.Attempts.Finish(session.Metadata.ExportId, completion);
                }

                if (completed.IsFailure) context.Abort();
            }
        }
        finally { await session.DisposeAsync().ConfigureAwait(false); }
    }

    private async IAsyncEnumerable<NdjsonEncodedRecord> Frames([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var frame in session.ReadFramesAsync(cancellationToken).ConfigureAwait(false))
        {
            using (frame) { yield return frame.TakeJson(); }
        }
    }
}
