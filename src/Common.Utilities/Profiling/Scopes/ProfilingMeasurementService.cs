// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

/// <summary>
/// Creates application-facing profiling scopes without exposing collector implementation details.
/// </summary>
/// <example><code>await measurements.MeasureAsync("load", action, cancellationToken);</code></example>
public sealed class ProfilingMeasurementService(
    ProfilingOptions options,
    IProfilingControlService control = null,
    IProfilingStore store = null,
    IProfilingNodeIdentityProvider nodes = null,
    IBroadcastRegistryStore registry = null,
    IBroadcastNodeIdentityProvider broadcastIdentity = null,
    ProfilingActiveSessionContext activeSessionContext = null,
    ProfilingSegmentContext segmentContext = null,
    TimeProvider timeProvider = null
) : IProfilingMeasurementService
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public Task<Result<IProfilingMeasurementScope>> BeginAsync(
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var ambientFrame = segmentContext?.PushPending();
        return this.BeginCoreAsync(name, ambientFrame, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> MeasureAsync(
        string name,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(action);
        var beginResult = await this.BeginAsync(name, cancellationToken).ConfigureAwait(false);
        if (beginResult.IsFailure)
        {
            return Result
                .Failure()
                .WithErrors(beginResult.Errors)
                .WithMessages(beginResult.Messages);
        }

        var scope = (ProfilingMeasurementScope)beginResult.Value;
        try
        {
            await action(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            scope.MarkCancelled();
            _ = await scope.CompleteAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            scope.MarkFailed(exception);
            _ = await scope.CompleteAsync().ConfigureAwait(false);
            throw;
        }

        return await scope.CompleteAsync().ConfigureAwait(false);
    }

    private async Task<Result<IProfilingMeasurementScope>> BeginCoreAsync(
        string name,
        ProfilingSegmentContext.Frame ambientFrame,
        CancellationToken cancellationToken
    )
    {
        var ownsStartedSession = false;
        try
        {
            var operationalError = this.GetOperationalError();
            if (operationalError is not null)
            {
                return Failure<IProfilingMeasurementScope>(operationalError);
            }

            var normalizedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 100)
            {
                return Failure<IProfilingMeasurementScope>(
                    new ProfilingValidationError(
                        "A measurement name of at most 100 characters is required."
                    )
                );
            }

            cancellationToken.ThrowIfCancellationRequested();
            var activeResult = await store
                .GetActiveSessionAsync(cancellationToken)
                .ConfigureAwait(false);
            ProfilingSession session;
            bool ownsSession;
            if (activeResult.IsSuccess)
            {
                session = activeResult.Value;
                ownsSession = false;
            }
            else if (activeResult.Errors.Any(error => error is ProfilingInvalidStateError))
            {
                var startResult = await control
                    .StartAsync(new ProfilingStartRequest(normalizedName), cancellationToken)
                    .ConfigureAwait(false);
                if (startResult.IsFailure)
                {
                    return CopyFailure<IProfilingMeasurementScope, ProfilingControlResult>(
                        startResult
                    );
                }

                session = startResult.Value.Session;
                ownsSession = startResult.Value.Created;
                ownsStartedSession = ownsSession;
            }
            else
            {
                return CopyFailure<IProfilingMeasurementScope, ProfilingSession>(activeResult);
            }

            var registration = await registry
                .FindAsync(broadcastIdentity.GetNodeIdentity(), cancellationToken)
                .ConfigureAwait(false);
            if (registration is null || !registration.IsActive)
            {
                await this.StopOwnedSessionAsync(ownsSession).ConfigureAwait(false);
                ownsStartedSession = false;
                return Failure<IProfilingMeasurementScope>(
                    new ProfilingUnavailableError(
                        "The local Broadcast registration is unavailable for profiling measurement."
                    )
                );
            }

            var nodeResult = await nodes
                .GetAsync(registration, cancellationToken)
                .ConfigureAwait(false);
            if (nodeResult.IsFailure)
            {
                await this.StopOwnedSessionAsync(ownsSession).ConfigureAwait(false);
                ownsStartedSession = false;
                return CopyFailure<IProfilingMeasurementScope, ProfilingNode>(nodeResult);
            }

            var node = nodeResult.Value;
            var parent = ambientFrame.Parent?.Segment;
            if (
                parent is not null
                && (parent.SessionId != session.Identity.Id || parent.NodeId != node.Identity.Id)
            )
            {
                await this.StopOwnedSessionAsync(ownsSession).ConfigureAwait(false);
                ownsStartedSession = false;
                return Failure<IProfilingMeasurementScope>(
                    new ProfilingValidationError(
                        "An ambient parent segment must belong to the same session and node."
                    )
                );
            }

            var startedUtc = this.timeProvider.GetUtcNow();
            var startedTimestamp = this.timeProvider.GetTimestamp();
            var segment = new ProfilingSegment
            {
                Id = Guid.NewGuid(),
                SessionId = session.Identity.Id,
                SessionKey = session.Identity.Key,
                NodeId = node.Identity.Id,
                NodeKey = node.Identity.Key,
                Name = normalizedName,
                StartedUtc = startedUtc,
                Outcome = ProfilingSegmentOutcome.Open,
                ParentSegmentId = parent?.SegmentId,
                CorrelationId = Activity.Current?.TraceId.ToString(),
            };
            var segmentResult = await store
                .UpsertSegmentAsync(segment, cancellationToken)
                .ConfigureAwait(false);
            if (segmentResult.IsFailure)
            {
                await this.StopOwnedSessionAsync(ownsSession).ConfigureAwait(false);
                ownsStartedSession = false;
                return CopyFailure<IProfilingMeasurementScope, ProfilingSegment>(segmentResult);
            }

            ambientFrame.Activate(
                new(
                    segment.Id,
                    session.Identity.Id,
                    node.Identity.Id,
                    session.Identity.Key,
                    node.Identity.Key
                )
            );
            activeSessionContext.Set(session, node);
            return Result<IProfilingMeasurementScope>.Success(
                new ProfilingMeasurementScope(
                    store,
                    control,
                    activeSessionContext,
                    ambientFrame,
                    session,
                    segment,
                    this.timeProvider,
                    startedTimestamp,
                    ownsSession
                )
            );
        }
        catch (OperationCanceledException)
        {
            await this.StopOwnedSessionAsync(ownsStartedSession).ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (ambientFrame?.Segment is null)
            {
                ambientFrame?.Deactivate();
            }
        }
    }

    private IResultError GetOperationalError()
    {
        if (options?.Enabled != true)
        {
            return new ProfilingDisabledError();
        }

        return
            control is null
            || store is null
            || nodes is null
            || registry is null
            || broadcastIdentity is null
            || activeSessionContext is null
            || segmentContext is null
            ? new ProfilingUnavailableError(
                "Profiling measurement requires control, storage, Broadcast registration, and local context services."
            )
            : null;
    }

    private async Task StopOwnedSessionAsync(bool ownsSession)
    {
        if (ownsSession)
        {
            _ = await control.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static Result<T> Failure<T>(IResultError error) => Result<T>.Failure().WithError(error);

    private static Result<TTarget> CopyFailure<TTarget, TSource>(Result<TSource> source) =>
        Result<TTarget>.Failure().WithErrors(source.Errors).WithMessages(source.Messages);
}
