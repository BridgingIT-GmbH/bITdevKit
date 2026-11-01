// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>Owns validation, current access binding, verification and fixed-window extraction independently of HTTP.</summary>
/// <example><code>await using var session = (await exports.OpenAsync(new() { Format = "csv" })).Value;</code></example>
public sealed class AuditExportService(IAuditContext audit, IAuditQueryService queries, IAuditingStorageProvider provider, AuditExportRegistry registry,
    AuditExportOptions options, AuditExportLimiter limiter, AuditIntegrityReadPolicy integrity, AuditReadProjection projection,
    AuditExposureOptions exposure, AuditingOptions capture, AuditFieldCatalog catalog, TimeProvider time) : IAuditExportService
{
    /// <inheritdoc />
    public async Task<Result<AuditExportSession>> OpenAsync(AuditExportRequest request = null, CancellationToken cancellationToken = default)
    {
        using var suppression = audit.Suppress();
        IDisposable lease = null;
        Task preflight = null;
        var startedTimestamp = time.GetTimestamp();
        try
        {
            options.Validate();
            if (!provider.Capabilities.SupportsResumableExtraction) return Result<AuditExportSession>.Failure(new AuditError("AuditCapabilityUnsupported"));
            request ??= new();
            var filter = AuditFilterSnapshot.Copy(request.Filter);
            if (filter?.Page > 1 || filter?.Orderings?.Count > 0) return Result<AuditExportSession>.Failure(new AuditError("AuditExportOrderingUnsupported"));
            var selected = registry.Find(request.Format);
            if (selected.IsFailure) return Result<AuditExportSession>.Failure(selected);
            var formatter = selected.Value;
            var prepared = request.Resume is null ? await queries.PrepareAsync(filter, cancellationToken).ConfigureAwait(false)
                : await queries.PrepareAtAsync(filter, request.Resume.AsOfUtc, cancellationToken).ConfigureAwait(false);
            if (prepared.IsFailure) return Result<AuditExportSession>.Failure(prepared);
            var plan = prepared.Value;
            var exposureFingerprint = this.ExposureFingerprint(formatter);
            var now = time.GetUtcNow();
            var progress = request.Resume;
            if (progress is not null && (progress.ExpiresUtc <= now || progress.StoreId == Guid.Empty || progress.StoreEpoch == Guid.Empty
                || progress.AfterPosition < 0 || progress.UpperPosition < progress.AfterPosition || progress.AvailabilityBaseline < 0 || progress.LifecycleGeneration < 0
                || progress.Format != formatter.Format || progress.Profile != formatter.Profile || progress.SelectionFingerprint != plan.Fingerprint
                || progress.ScopeFingerprint != plan.ScopeFingerprint || progress.ExposureFingerprint != exposureFingerprint))
                return Result<AuditExportSession>.Failure(new AuditError("AuditExportProgressInvalid"));
            lease = limiter.TryAcquire();
            if (lease is null) return Result<AuditExportSession>.Failure(new AuditError("AuditExportBusy"));
            Result<AuditExtractionCursor> opened;
            using (var read = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                read.CancelAfter(options.ProviderReadTimeout);
                if (progress is null || progress.IsCheckpoint)
                {
                    var opening = provider.OpenExtractionAsync(new()
                    {
                        Selection = plan, StoreId = progress?.StoreId, StoreEpoch = progress?.StoreEpoch,
                        AfterPosition = progress?.AfterPosition ?? 0, AvailabilityBaseline = progress?.AvailabilityBaseline,
                        BatchSize = 1, MaxBatchBytes = 16_777_216, MaxRecordBytes = 16_777_216
                    }, read.Token);
                    preflight = opening;
                    opened = await opening.WaitAsync(read.Token).ConfigureAwait(false);
                }
                else
                {
                    var cursor = new AuditExtractionCursor
                    {
                        Selection = plan, StoreId = progress.StoreId, StoreEpoch = progress.StoreEpoch, AfterPosition = progress.AfterPosition,
                        UpperPosition = progress.UpperPosition, AvailabilityBaseline = progress.AvailabilityBaseline, LifecycleGeneration = progress.LifecycleGeneration,
                        BatchSize = 1, MaxBatchBytes = 16_777_216, MaxRecordBytes = 16_777_216
                    };
                    // Validate the fixed window without consuming its next matching source record.
                    var validating = provider.ReadExtractionAsync(cursor with { AfterPosition = cursor.UpperPosition }, read.Token);
                    preflight = validating;
                    var validation = await validating.WaitAsync(read.Token).ConfigureAwait(false);
                    opened = validation.IsSuccess ? Result<AuditExtractionCursor>.Success(cursor) : Result<AuditExtractionCursor>.Failure(validation);
                }
            }

            if (opened.IsFailure) { lease.Dispose(); lease = null; return Result<AuditExportSession>.Failure(opened); }

            var cursorValue = opened.Value;
            var metadata = new AuditExportMetadata
            {
                ExportId = request.ExportId is { } requested && requested != Guid.Empty ? requested : Guid.NewGuid(), SourceStoreId = cursorValue.StoreId, SourceStoreEpoch = cursorValue.StoreEpoch,
                Format = formatter.Format, Profile = formatter.Profile, SelectionFingerprint = plan.Fingerprint, ScopeFingerprint = plan.ScopeFingerprint,
                ExposureFingerprint = exposureFingerprint, FromUtc = plan.FromUtc, ToUtc = plan.ToUtc, AsOfUtc = plan.AsOfUtc,
                StartedUtc = now, ExpiresUtc = now + options.MetadataLifetime, Lifecycle = plan.Lifecycle, RequiresStableLifecycle = plan.RequiresStableLifecycle,
                PositionsExposed = plan.IsUnrestrictedScope,
                WindowLowerPosition = plan.IsUnrestrictedScope ? cursorValue.AfterPosition : null, WindowUpperPosition = plan.IsUnrestrictedScope ? cursorValue.UpperPosition : null,
                SourceVerificationPolicy = integrity.IsConfigured ? "OriginalVerificationRequired" : "UnprotectedHistoryAllowed", Omissions = formatter.Omissions
            };
            async Task<Result> ValidateCurrent(CancellationToken token)
            {
                var current = await queries.PrepareAtAsync(filter, plan.AsOfUtc, token).ConfigureAwait(false);
                return current.IsFailure ? Result.Failure().WithErrors(current.Errors) : current.Value.Fingerprint != plan.Fingerprint || current.Value.ScopeFingerprint != plan.ScopeFingerprint
                    || this.ExposureFingerprint(formatter) != exposureFingerprint ? Result.Failure(new AuditError("AuditExportScopeChanged")) : Result.Success();
            }

            var session = new AuditExportSession(audit, provider, formatter, options, cursorValue, metadata, integrity, projection, time, lease, ValidateCurrent, startedTimestamp);
            lease = null;
            return Result<AuditExportSession>.Success(session);
        }
        catch (OperationCanceledException) { ReleasePreflight(); return Result<AuditExportSession>.Failure(new AuditError("AuditExportCanceled")); }
        catch (Exception) { ReleasePreflight(); return Result<AuditExportSession>.Failure(new AuditError("AuditExportUnavailable")); }

        void ReleasePreflight()
        {
            var retained = lease; lease = null;
            if (preflight is { IsCompleted: false })
            {
                _ = preflight.ContinueWith(completed => { _ = completed.Exception; retained?.Dispose(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            else { _ = preflight?.Exception; retained?.Dispose(); }
        }
    }

    /// <summary>Hashes current source exposure and formatter configuration without retaining source values.</summary>
    /// <example><code>var fingerprint = exports.ExposureFingerprint(formatter);</code></example>
    public string ExposureFingerprint(IAuditExportFormatter formatter) => AuditCanonicalizerV1.Hash(AuditCanonicalizerV1.Encode(JsonSerializer.SerializeToElement(new
    {
        exposure.Revision, exposure.ExposeChangeValues, capture.Limits,
        Exclusions = capture.CapturePolicy.ExcludeNames.Order(StringComparer.Ordinal).ToArray(),
        Redactions = capture.CapturePolicy.RedactNames.Order(StringComparer.Ordinal).ToArray(),
        Hashes = capture.CapturePolicy.HashNames.Order(StringComparer.Ordinal).ToArray(),
        Fields = catalog.Fields.Where(field => field.Detail is not null).Select(field => new { field.Domain, field.Name, field.Detail.ExposeValue }).ToArray(),
        formatter.Format, formatter.Profile, FormatterRevision = formatter.Revision
    })));
}
