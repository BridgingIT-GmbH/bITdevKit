// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides orchestration administration operations backed by persisted state.
/// </summary>
public class OrchestrationAdministrationService(
    IOrchestrationQueryStore queryStore,
    IOrchestrationAdministrationStore administrationStore) : IOrchestrationAdministrationService
{
    /// <inheritdoc />
    public async Task<Result<string>> ArchiveAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await queryStore.GetInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return NotFound<string>(instanceId);
            }

            if (snapshot.IsArchived)
            {
                return Result<string>.Success($"Orchestration instance '{instanceId}' is already archived.");
            }

            if (!IsTerminal(snapshot.Status))
            {
                return InvalidState<string>($"Orchestration instance '{instanceId}' is not archivable in its current state.");
            }

            await administrationStore.ArchiveAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return Result<string>.Success($"Orchestration instance '{instanceId}' was archived.");
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Failure().WithError(new Error("Orchestration archive was canceled."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound<string>(instanceId);
        }
        catch (InvalidOperationException exception)
        {
            return InvalidState<string>(exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return Unsupported<string>(exception.Message);
        }
        catch (Exception exception)
        {
            return Result<string>.Failure().WithError(new Error(exception.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result<OrchestrationPurgeResult>> PurgeAsync(OrchestrationPurgeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            request ??= new OrchestrationPurgeRequest();

            var validation = ValidateStatuses(request.Statuses);
            if (validation.IsFailure)
            {
                return Result<OrchestrationPurgeResult>.Failure().WithErrors(validation.Errors);
            }

            var criteria = new OrchestrationPurgeCriteria
            {
                OlderThan = request.OlderThan,
                Statuses = ParseStatuses(request.Statuses),
                IsArchived = request.IsArchived,
            };

            var result = await administrationStore.PurgeAsync(criteria, cancellationToken).ConfigureAwait(false);
            return Result<OrchestrationPurgeResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            return Result<OrchestrationPurgeResult>.Failure().WithError(new Error("Orchestration purge was canceled."));
        }
        catch (NotSupportedException exception)
        {
            return Unsupported<OrchestrationPurgeResult>(exception.Message);
        }
        catch (Exception exception)
        {
            return Result<OrchestrationPurgeResult>.Failure().WithError(new Error(exception.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> ReleaseLeaseAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await queryStore.GetInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return NotFound<string>(instanceId);
            }

            await administrationStore.ReleaseLeaseAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return Result<string>.Success($"Lease for orchestration instance '{instanceId}' was released.");
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Failure().WithError(new Error("Orchestration lease release was canceled."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound<string>(instanceId);
        }
        catch (InvalidOperationException exception)
        {
            return InvalidState<string>(exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return Unsupported<string>(exception.Message);
        }
        catch (Exception exception)
        {
            return Result<string>.Failure().WithError(new Error(exception.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> RequeueTimersAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await queryStore.GetInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return NotFound<string>(instanceId);
            }

            if (IsTerminal(snapshot.Status))
            {
                return InvalidState<string>($"Orchestration instance '{instanceId}' is already terminal.");
            }

            var count = await administrationStore.RequeueTimersAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return Result<string>.Success($"{count} timer(s) for orchestration instance '{instanceId}' were requeued.");
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Failure().WithError(new Error("Orchestration timer requeue was canceled."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound<string>(instanceId);
        }
        catch (InvalidOperationException exception)
        {
            return InvalidState<string>(exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return Unsupported<string>(exception.Message);
        }
        catch (Exception exception)
        {
            return Result<string>.Failure().WithError(new Error(exception.Message));
        }
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

    private static IReadOnlyCollection<OrchestrationStatus> ParseStatuses(IReadOnlyList<string> values)
    {
        return values.SafeNull()
            .Where(value => Enum.TryParse<OrchestrationStatus>(value, true, out _))
            .Select(value => Enum.Parse<OrchestrationStatus>(value, true))
            .Distinct()
            .ToArray();
    }

    private static Result<T> InvalidState<T>(string message)
    {
        return Result<T>.Failure().WithError(new Error(message));
    }

    private static bool IsTerminal(OrchestrationStatus status)
    {
        return status is OrchestrationStatus.Completed or OrchestrationStatus.Cancelled or OrchestrationStatus.Terminated or OrchestrationStatus.Failed;
    }

    private static Result<T> NotFound<T>(Guid instanceId)
    {
        return Result<T>.Failure().WithError(new Error($"Orchestration instance '{instanceId}' was not found."));
    }

    private static Result<T> Unsupported<T>(string message)
    {
        return Result<T>.Failure().WithError(new Error(message));
    }
}