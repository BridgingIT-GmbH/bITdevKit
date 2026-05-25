// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides persisted-state-backed orchestration query operations for dashboards, endpoints, and support tooling.
/// </summary>
public class OrchestrationQueryService(IOrchestrationQueryStore queryStore) : IOrchestrationQueryService
{
    /// <inheritdoc />
    public async Task<Result<OrchestrationInstanceModel>> GetAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await queryStore.GetInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return Result<OrchestrationInstanceModel>.Failure()
                    .WithError(new Error($"Orchestration instance '{instanceId}' was not found."));
            }

            return Result<OrchestrationInstanceModel>.Success(MapInstance(snapshot));
        }
        catch (OperationCanceledException)
        {
            return Result<OrchestrationInstanceModel>.Failure()
                .WithError(new Error("Orchestration instance query was canceled."));
        }
    }

    /// <inheritdoc />
    public async Task<Result<OrchestrationContextSnapshotModel>> GetContextAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await queryStore.GetInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return Result<OrchestrationContextSnapshotModel>.Failure()
                    .WithError(new Error($"Orchestration instance '{instanceId}' was not found."));
            }

            return Result<OrchestrationContextSnapshotModel>.Success(new OrchestrationContextSnapshotModel
            {
                InstanceId = snapshot.InstanceId,
                OrchestrationName = snapshot.OrchestrationName,
                Status = snapshot.Status.ToString(),
                CurrentState = snapshot.CurrentState,
                SnapshotUtc = snapshot.UpdatedDate,
                ContextType = snapshot.ContextType,
                ContextJson = snapshot.SerializedContext,
            });
        }
        catch (OperationCanceledException)
        {
            return Result<OrchestrationContextSnapshotModel>.Failure()
                .WithError(new Error("Orchestration context query was canceled."));
        }
    }

    /// <inheritdoc />
    public async Task<ResultPaged<OrchestrationInstanceModel>> QueryAsync(OrchestrationQueryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            request ??= new OrchestrationQueryRequest();

            var validation = ValidateQueryRequest(request);
            if (validation.IsFailure)
            {
                return ResultPaged<OrchestrationInstanceModel>.Failure().WithErrors(validation.Errors);
            }

            var snapshots = await GetSnapshotsAsync(request, cancellationToken).ConfigureAwait(false);
            var sorted = SortSnapshots(snapshots, request.SortBy, request.SortDescending);
            var take = request.Take <= 0 ? 50 : request.Take;
            var skip = Math.Max(0, request.Skip);
            var paged = sorted
                .Skip(skip)
                .Take(take)
                .Select(MapInstance)
                .ToArray();
            var page = (skip / take) + 1;

            return ResultPaged<OrchestrationInstanceModel>.Success(paged, sorted.Count, page, take);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<OrchestrationInstanceModel>.Failure()
                .WithError(new Error("Orchestration query was canceled."));
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<OrchestrationHistoryModel>>> GetHistoryAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var instance = await queryStore.GetInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (instance is null)
            {
                return Result<IReadOnlyList<OrchestrationHistoryModel>>.Failure()
                    .WithError(new Error($"Orchestration instance '{instanceId}' was not found."));
            }

            var history = await queryStore.GetHistoryAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<OrchestrationHistoryModel>>.Success(history.Select(MapHistory).ToArray());
        }
        catch (OperationCanceledException)
        {
            return Result<IReadOnlyList<OrchestrationHistoryModel>>.Failure()
                .WithError(new Error("Orchestration history query was canceled."));
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<OrchestrationSignalModel>>> GetSignalsAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var instance = await queryStore.GetInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (instance is null)
            {
                return Result<IReadOnlyList<OrchestrationSignalModel>>.Failure()
                    .WithError(new Error($"Orchestration instance '{instanceId}' was not found."));
            }

            var signals = await queryStore.GetSignalsAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<OrchestrationSignalModel>>.Success(signals.Select(MapSignal).ToArray());
        }
        catch (OperationCanceledException)
        {
            return Result<IReadOnlyList<OrchestrationSignalModel>>.Failure()
                .WithError(new Error("Orchestration signal query was canceled."));
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<OrchestrationTimerModel>>> GetTimersAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var instance = await queryStore.GetInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (instance is null)
            {
                return Result<IReadOnlyList<OrchestrationTimerModel>>.Failure()
                    .WithError(new Error($"Orchestration instance '{instanceId}' was not found."));
            }

            var timers = await queryStore.GetTimersAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<OrchestrationTimerModel>>.Success(timers.Select(MapTimer).ToArray());
        }
        catch (OperationCanceledException)
        {
            return Result<IReadOnlyList<OrchestrationTimerModel>>.Failure()
                .WithError(new Error("Orchestration timer query was canceled."));
        }
    }

    /// <inheritdoc />
    public async Task<Result<OrchestrationMetricsModel>> GetMetricsAsync(OrchestrationMetricsRequest request = null, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            request ??= new OrchestrationMetricsRequest();

            var validation = ValidateStatuses(request.Statuses);
            if (validation.IsFailure)
            {
                return Result<OrchestrationMetricsModel>.Failure().WithErrors(validation.Errors);
            }

            var snapshots = await GetSnapshotsAsync(new OrchestrationQueryRequest
            {
                OrchestrationName = request.OrchestrationName,
                Statuses = request.Statuses,
                States = request.States,
                StartedFrom = request.StartedFrom,
                StartedTo = request.StartedTo,
                CompletedFrom = request.CompletedFrom,
                CompletedTo = request.CompletedTo,
                Skip = 0,
                Take = int.MaxValue,
                SortBy = "StartedUtc",
                SortDescending = true,
            }, cancellationToken).ConfigureAwait(false);

            var durationValues = snapshots
                .Where(item => item.CompletedUtc.HasValue)
                .Select(item => (item.CompletedUtc.Value - item.StartedUtc).TotalSeconds)
                .ToArray();

            var metrics = new OrchestrationMetricsModel
            {
                TotalCount = snapshots.Count,
                RunningCount = snapshots.LongCount(item => item.Status == OrchestrationStatus.Running),
                WaitingCount = snapshots.LongCount(item => item.Status == OrchestrationStatus.Waiting),
                PausedCount = snapshots.LongCount(item => item.Status == OrchestrationStatus.Paused),
                CompletedCount = snapshots.LongCount(item => item.Status == OrchestrationStatus.Completed),
                FailedCount = snapshots.LongCount(item => item.Status == OrchestrationStatus.Failed),
                CancelledCount = snapshots.LongCount(item => item.Status == OrchestrationStatus.Cancelled),
                TerminatedCount = snapshots.LongCount(item => item.Status == OrchestrationStatus.Terminated),
                AverageDurationSeconds = durationValues.Length == 0 ? null : durationValues.Average(),
                OldestWaitingStartedUtc = snapshots
                    .Where(item => item.Status == OrchestrationStatus.Waiting)
                    .OrderBy(item => item.StartedUtc)
                    .Select(item => (DateTimeOffset?)item.StartedUtc)
                    .FirstOrDefault(),
                CountsByOrchestration = snapshots
                    .GroupBy(item => item.OrchestrationName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => (long)group.LongCount(), StringComparer.OrdinalIgnoreCase),
                CountsByState = snapshots
                    .GroupBy(item => item.CurrentState ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => (long)group.LongCount(), StringComparer.OrdinalIgnoreCase),
            };

            return Result<OrchestrationMetricsModel>.Success(metrics);
        }
        catch (OperationCanceledException)
        {
            return Result<OrchestrationMetricsModel>.Failure()
                .WithError(new Error("Orchestration metrics query was canceled."));
        }
    }

    private static Result ValidateQueryRequest(OrchestrationQueryRequest request)
    {
        if (request is null)
        {
            return Result.Success();
        }

        if (request.Skip < 0)
        {
            return Result.Failure().WithError(new Error("Skip must be zero or greater."));
        }

        if (request.Take < 0)
        {
            return Result.Failure().WithError(new Error("Take must be zero or greater."));
        }

        var statusValidation = ValidateStatuses(request.Statuses);
        if (statusValidation.IsFailure)
        {
            return statusValidation;
        }

        return ValidateSortBy(request.SortBy);
    }

    private static Result ValidateStatuses(IReadOnlyList<string> statuses)
    {
        foreach (var status in statuses.SafeNull())
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                continue;
            }

            if (!Enum.TryParse<OrchestrationStatus>(status, true, out _))
            {
                return Result.Failure().WithError(new Error($"Unknown orchestration status '{status}'."));
            }
        }

        return Result.Success();
    }

    private static Result ValidateSortBy(string sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return Result.Success();
        }

        return IsSupportedSort(sortBy)
            ? Result.Success()
            : Result.Failure().WithError(new Error($"Unsupported orchestration sort field '{sortBy}'."));
    }

    private async Task<IReadOnlyList<OrchestrationInstanceSnapshot>> GetSnapshotsAsync(OrchestrationQueryRequest request, CancellationToken cancellationToken)
    {
        var parsedStatuses = ParseStatuses(request?.Statuses);
        var parsedStates = ParseValues(request?.States);

        var query = new OrchestrationInstanceQuery
        {
            OrchestrationName = request?.OrchestrationName,
            Statuses = parsedStatuses.ToList(),
            States = parsedStates.ToList(),
            CorrelationId = request?.CorrelationId,
            ConcurrencyKey = request?.ConcurrencyKey,
            StartedFromUtc = request?.StartedFrom,
            StartedToUtc = request?.StartedTo,
            CompletedFromUtc = request?.CompletedFrom,
            CompletedToUtc = request?.CompletedTo,
            Skip = 0,
            Take = int.MaxValue,
        };

        var result = await queryStore.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        return result.Items.ToArray();
    }

    private static List<OrchestrationInstanceSnapshot> SortSnapshots(IEnumerable<OrchestrationInstanceSnapshot> snapshots, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "StartedUtc" : sortBy.Trim();
        Func<OrchestrationInstanceSnapshot, object> selector = normalized.ToUpperInvariant() switch
        {
            "ORCHESTRATIONNAME" => item => item.OrchestrationName ?? string.Empty,
            "STATUS" => item => item.Status.ToString(),
            "CURRENTSTATE" => item => item.CurrentState ?? string.Empty,
            "CORRELATIONID" => item => item.CorrelationId ?? string.Empty,
            "COMPLETEDUTC" => item => item.CompletedUtc ?? DateTimeOffset.MinValue,
            "LASTUPDATEDUTC" => item => item.UpdatedDate,
            _ => item => item.StartedUtc,
        };

        var ordered = descending
            ? snapshots.OrderByDescending(selector).ThenBy(item => item.InstanceId)
            : snapshots.OrderBy(selector).ThenBy(item => item.InstanceId);

        return ordered.ToList();
    }

    private static bool IsSupportedSort(string sortBy)
    {
        return sortBy.Trim().ToUpperInvariant() is
            "STARTEDUTC" or
            "COMPLETEDUTC" or
            "LASTUPDATEDUTC" or
            "ORCHESTRATIONNAME" or
            "STATUS" or
            "CURRENTSTATE" or
            "CORRELATIONID";
    }

    private static HashSet<OrchestrationStatus> ParseStatuses(IReadOnlyList<string> values)
    {
        var result = new HashSet<OrchestrationStatus>();

        foreach (var value in values.SafeNull())
        {
            if (Enum.TryParse<OrchestrationStatus>(value, true, out var parsed))
            {
                result.Add(parsed);
            }
        }

        return result;
    }

    private static HashSet<string> ParseValues(IReadOnlyList<string> values)
    {
        return values.SafeNull()
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static OrchestrationInstanceModel MapInstance(OrchestrationInstanceSnapshot snapshot)
    {
        return new OrchestrationInstanceModel
        {
            InstanceId = snapshot.InstanceId,
            OrchestrationName = snapshot.OrchestrationName,
            Status = snapshot.Status.ToString(),
            CurrentState = snapshot.CurrentState,
            CurrentActivity = snapshot.CurrentActivity,
            CorrelationId = snapshot.CorrelationId,
            ConcurrencyKey = snapshot.ConcurrencyKey,
            CreatedBy = snapshot.CreatedBy,
            CreatedDate = snapshot.CreatedDate,
            UpdatedBy = snapshot.UpdatedBy,
            UpdatedDate = snapshot.UpdatedDate,
            StartedUtc = snapshot.StartedUtc,
            CompletedUtc = snapshot.CompletedUtc,
            LastUpdatedUtc = snapshot.UpdatedDate,
        };
    }

    private static OrchestrationHistoryModel MapHistory(OrchestrationHistoryEntry entry)
    {
        var (message, dataJson) = SplitDetails(entry.Details);

        return new OrchestrationHistoryModel
        {
            Id = entry.EntryId,
            InstanceId = entry.InstanceId,
            CreatedBy = entry.RecordedBy,
            CreatedDate = entry.RecordedAt,
            TimestampUtc = entry.RecordedAt,
            EventType = entry.EventType,
            State = entry.StateName,
            Activity = entry.ActivityName,
            Message = message,
            DataJson = dataJson,
        };
    }

    private static OrchestrationSignalModel MapSignal(OrchestrationSignalRecord signal)
    {
        return new OrchestrationSignalModel
        {
            Id = signal.SignalId,
            InstanceId = signal.InstanceId,
            SignalName = signal.SignalName,
            ProcessingStatus = signal.Status.ToString(),
            IdempotencyKey = signal.IdempotencyKey,
            CreatedBy = signal.CreatedBy,
            CreatedDate = signal.CreatedDate,
            UpdatedBy = signal.UpdatedBy,
            UpdatedDate = signal.UpdatedDate,
            ReceivedUtc = signal.ReceivedUtc,
            ProcessedUtc = signal.ProcessedUtc,
            PayloadJson = signal.Payload,
        };
    }

    private static OrchestrationTimerModel MapTimer(OrchestrationTimerRecord timer)
    {
        return new OrchestrationTimerModel
        {
            Id = timer.TimerId,
            InstanceId = timer.InstanceId,
            TimerKind = timer.TriggerKind,
            ProcessingStatus = timer.Status.ToString(),
            CreatedBy = timer.CreatedBy,
            CreatedDate = timer.CreatedDate,
            UpdatedBy = timer.UpdatedBy,
            UpdatedDate = timer.UpdatedDate,
            DueUtc = timer.DueTimeUtc,
            ProcessedUtc = timer.ProcessedUtc,
            MetadataJson = BuildTimerMetadata(timer),
        };
    }

    private static string BuildTimerMetadata(OrchestrationTimerRecord timer)
    {
        if (string.IsNullOrWhiteSpace(timer.TargetState) && string.IsNullOrWhiteSpace(timer.Continuation) && string.IsNullOrWhiteSpace(timer.StatusReason))
        {
            return null;
        }

        return JsonSerializer.Serialize(new
        {
            timer.TargetState,
            timer.Continuation,
            timer.StatusReason,
        });
    }

    private static (string Message, string DataJson) SplitDetails(string details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return (null, null);
        }

        var trimmed = details.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal)
            ? (null, details)
            : (details, null);
    }
}