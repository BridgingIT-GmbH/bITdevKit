// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Observes a real test-owned composition without adding scopes, behaviors, or outcomes.</summary>
/// <remarks>Supply an isolated provider or explicit test-owned audit IDs. Original access belongs to controlled tests, never a public payload route.</remarks>
/// <example><code>var harness = new AuditTestHarness(isolatedProvider);</code></example>
public sealed class AuditTestHarness(IAuditingStorageProvider provider, IAuditIntegrityVerifier verifier = null)
{
    /// <summary>Invokes business code once and returns its exact result plus independently confirmed evidence.</summary>
    /// <remarks>Business exceptions and cancellation propagate unchanged. The harness adds no instrumentation to repair missing capture.</remarks>
    /// <example><code>var observed = await harness.ObserveAsync(ct => service.RenameAsync(ct), () => [capturedId]);</code></example>
    public async Task<AuditObservedResult<T>> ObserveAsync<T>(Func<CancellationToken, Task<T>> invoke, Func<IReadOnlyCollection<Guid>> auditIds, CancellationToken cancellationToken = default)
    {
        var value = await invoke(cancellationToken).ConfigureAwait(false);
        var flush = Result<AuditFlushResult>.Failure(new AuditError("AuditEvidenceUnconfirmed"));
        Result<ImmutableArray<AuditRecord>> evidence;
        try
        {
            flush = await this.FlushAsync(cancellationToken).ConfigureAwait(false);
            evidence = flush.IsSuccess && flush.Value.Pending == 0 && flush.Value.Failed == 0
                ? await this.ReadEvidenceAsync(auditIds(), cancellationToken).ConfigureAwait(false)
                : Result<ImmutableArray<AuditRecord>>.Failure(new AuditError("AuditEvidenceUnconfirmed"));
        }
        catch (Exception)
        {
            evidence = Result<ImmutableArray<AuditRecord>>.Failure(new AuditError("AuditObservationUnavailable"));
        }

        return new() { Value = value, Observation = new() { Flush = flush, Evidence = evidence } };
    }

    /// <summary>Confirms the current admission boundary with a fresh ten-second deadline.</summary>
    /// <example><code>var flushed = await harness.FlushAsync();</code></example>
    public async Task<Result<AuditFlushResult>> FlushAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try { return await provider.FlushAsync(timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Result<AuditFlushResult>.Failure(new AuditError("AuditFlushTimeout")); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Result<AuditFlushResult>.Failure(new AuditError("AuditFlushUnavailable")); }
    }

    /// <summary>Reads at most 100 original records and 8 MiB; overflow fails instead of checking partial capture.</summary>
    /// <example><code>var originals = await harness.ReadEvidenceAsync([auditId]);</code></example>
    public async Task<Result<ImmutableArray<AuditRecord>>> ReadEvidenceAsync(IReadOnlyCollection<Guid> auditIds, CancellationToken cancellationToken = default)
    {
        if (auditIds is null || auditIds.Count > 100) return Result<ImmutableArray<AuditRecord>>.Failure(new AuditError("AuditObservationLimit"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var records = ImmutableArray.CreateBuilder<AuditRecord>();
        long bytes = 0;
        try
        {
            foreach (var id in auditIds.Distinct())
            {
                var found = await provider.FindAsync(id, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
                if (found.IsFailure) return Result<ImmutableArray<AuditRecord>>.Failure(found);
                var record = found.Value;
                if (verifier is not null)
                {
                    var verified = await verifier.VerifyAsync(record, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
                    if (verified.IsFailure || verified.Value is not ("Verified" or "NotProtected")) return Result<ImmutableArray<AuditRecord>>.Failure(new AuditError("AuditIntegrityInvalid"));
                }
                else if (record.IntegrityProtection is not null) return Result<ImmutableArray<AuditRecord>>.Failure(new AuditError("AuditIntegrityUnavailable"));
                bytes += AuditRecordCodec.Encode(record).Length;
                if (bytes > 8_388_608) return Result<ImmutableArray<AuditRecord>>.Failure(new AuditError("AuditObservationLimit"));
                records.Add(record);
            }

            return Result<ImmutableArray<AuditRecord>>.Success(records.ToImmutable());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Result<ImmutableArray<AuditRecord>>.Failure(new AuditError("AuditObservationTimeout")); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Result<ImmutableArray<AuditRecord>>.Failure(new AuditError("AuditObservationUnavailable")); }
    }
}
