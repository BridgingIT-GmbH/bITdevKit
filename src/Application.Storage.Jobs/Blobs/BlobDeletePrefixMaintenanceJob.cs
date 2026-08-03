// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scheduled job that deletes blobs under a configured prefix for a named blob-store client.
/// </summary>
/// <param name="logger">The logger.</param>
/// <param name="factory">The blob-store client factory.</param>
/// <example>
/// <code>
/// services.AddJobScheduler()
///     .WithJob&lt;BlobDeletePrefixMaintenanceJob&gt;("blob-delete-prefix", job =&gt; job
///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
/// </code>
/// </example>
public partial class BlobDeletePrefixMaintenanceJob(
    ILogger<BlobDeletePrefixMaintenanceJob> logger,
    IBlobStoreClientFactory factory) : JobBase<BlobDeletePrefixMaintenanceJobData>
{
    /// <inheritdoc />
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<BlobDeletePrefixMaintenanceJobData> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var data = context.Data ?? new BlobDeletePrefixMaintenanceJobData();
        TypedLogger.LogStart(logger, Constants.LogKey, data.StoreName, data.Container, data.Prefix, data.DryRun);

        Result<BlobDeletePrefixResult> result;
        try
        {
            var client = factory.CreateClient(data.StoreName);
            result = await client.DeleteByPrefixAsync(
                data.Container,
                data.Prefix,
                new BlobDeletePrefixOptions
                {
                    Take = data.Take,
                    MaxItems = data.MaxItems,
                    DryRun = data.DryRun,
                    AllowFullScan = data.AllowFullScan,
                    ContinueOnError = data.ContinueOnError
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result = Result<BlobDeletePrefixResult>.Failure(new BlobStoreProviderError(ex.GetBaseException().Message));
        }

        if (result.IsFailure)
        {
            var details = string.Join("; ", result.Errors.Select(error => error.Message));
            context.Items["Failure"] = details;
            TypedLogger.LogFailure(logger, Constants.LogKey, data.StoreName, details);
            return Result.Failure(result.Messages.DefaultIfEmpty(details)).WithErrors(result.Errors);
        }

        context.Items["CandidateCount"] = result.Value.CandidateCount;
        context.Items["DeletedCount"] = result.Value.DeletedCount;
        context.Items["DryRun"] = result.Value.DryRun;
        context.Messages.Add($"blob-delete-prefix: candidates={result.Value.CandidateCount}, deleted={result.Value.DeletedCount}, dryRun={result.Value.DryRun}".ToLowerInvariant());
        TypedLogger.LogCompleted(logger, Constants.LogKey, data.StoreName, result.Value.CandidateCount, result.Value.DeletedCount, result.Value.DryRun);

        return Result.Success().WithMessages(result.Messages);
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] blob maintenance: delete prefix started (store={StoreName}, container={Container}, prefix={Prefix}, dryRun={DryRun})")]
        public static partial void LogStart(ILogger logger, string logKey, string storeName, string container, string prefix, bool dryRun);

        [LoggerMessage(1, LogLevel.Information, "[{LogKey}] blob maintenance: delete prefix completed (store={StoreName}, candidates={CandidateCount}, deleted={DeletedCount}, dryRun={DryRun})")]
        public static partial void LogCompleted(ILogger logger, string logKey, string storeName, int candidateCount, int deletedCount, bool dryRun);

        [LoggerMessage(2, LogLevel.Error, "[{LogKey}] blob maintenance: delete prefix failed (store={StoreName}) {Details}")]
        public static partial void LogFailure(ILogger logger, string logKey, string storeName, string details);
    }
}
