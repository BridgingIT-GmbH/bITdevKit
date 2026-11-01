// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Runs finite whole-graph retention mutations under audit suppression.</summary>
/// <example><code>var result = await coordinator.SweepAsync(cancellationToken);</code></example>
public sealed class AuditRetentionCoordinator(IAuditingStorageProvider provider, IAuditContext context, AuditRetentionOptions options, TimeProvider time)
{
    /// <summary>Applies total CompletedUtc ages with cancellation and a ten-second sweep budget.</summary>
    /// <example><code>var changed = await coordinator.SweepAsync();</code></example>
    public async Task<Result<long>> SweepAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        using var suppression = context.Suppress();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var now = time.GetUtcNow();
        long changed = 0;
        try
        {
            if (options.ArchiveAfter is TimeSpan archive)
            {
                for (var batch = 0; batch < options.MaxBatches; batch++)
                {
                    var result = await provider.ArchiveBeforeAsync(now - archive, options.BatchSize, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
                    if (result.IsFailure) return Result<long>.Failure(result);
                    changed += result.Value;
                    if (result.Value < options.BatchSize) break;
                }
            }

            if (options.RemoveAfter is TimeSpan remove)
            {
                for (var batch = 0; batch < options.MaxBatches; batch++)
                {
                    var result = await provider.RemoveArchivedBeforeAsync(now - remove, options.BatchSize, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
                    if (result.IsFailure) return Result<long>.Failure(result);
                    changed += result.Value;
                    if (result.Value < options.BatchSize) break;
                }
            }

            return Result<long>.Success(changed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Result<long>.Failure(new AuditError("AuditRetentionTimeout")); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Result<long>.Failure(new AuditError("AuditRetentionUnavailable")); }
    }
}
