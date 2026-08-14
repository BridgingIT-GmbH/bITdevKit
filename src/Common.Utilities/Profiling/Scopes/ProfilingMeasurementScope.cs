// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Closes one stored profiling segment and its optionally owned session.</summary>
/// <example><code>await using var scope = measurementResult.Value;</code></example>
public sealed class ProfilingMeasurementScope : IProfilingMeasurementScope
{
    private readonly IProfilingStore store;
    private readonly IProfilingControlService control;
    private readonly ProfilingActiveSessionContext activeSessionContext;
    private readonly ProfilingSegmentContext.Frame ambientFrame;
    private readonly ProfilingSession session;
    private readonly ProfilingSegment segment;
    private readonly TimeProvider timeProvider;
    private readonly long startedTimestamp;
    private readonly bool ownsSession;
    private readonly object outcomeSync = new();
    private Task<Result> completion;
    private ProfilingSegmentOutcome outcome = ProfilingSegmentOutcome.Success;
    private string exceptionType;
    private string exceptionMessage;

    internal ProfilingMeasurementScope(
        IProfilingStore store,
        IProfilingControlService control,
        ProfilingActiveSessionContext activeSessionContext,
        ProfilingSegmentContext.Frame ambientFrame,
        ProfilingSession session,
        ProfilingSegment segment,
        TimeProvider timeProvider,
        long startedTimestamp,
        bool ownsSession
    )
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.control = control ?? throw new ArgumentNullException(nameof(control));
        this.activeSessionContext =
            activeSessionContext ?? throw new ArgumentNullException(nameof(activeSessionContext));
        this.ambientFrame = ambientFrame ?? throw new ArgumentNullException(nameof(ambientFrame));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.segment = segment ?? throw new ArgumentNullException(nameof(segment));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.startedTimestamp = startedTimestamp;
        this.ownsSession = ownsSession;
    }

    /// <inheritdoc />
    public string SessionKey => this.session.Identity.Key;

    /// <inheritdoc />
    public void MarkFailed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        lock (this.outcomeSync)
        {
            this.outcome = ProfilingSegmentOutcome.Failure;
            this.exceptionType = exception.GetType().FullName;
            this.exceptionMessage = exception.Message;
        }
    }

    /// <inheritdoc />
    public void MarkCancelled()
    {
        lock (this.outcomeSync)
        {
            this.outcome = ProfilingSegmentOutcome.Cancellation;
            this.exceptionType = null;
            this.exceptionMessage = null;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        this.ambientFrame.Deactivate();
        return new ValueTask(this.DisposeCoreAsync());
    }

    internal Task<Result> CompleteAsync()
    {
        this.ambientFrame.Deactivate();
        lock (this.outcomeSync)
        {
            return this.completion ??= this.CompleteCoreAsync();
        }
    }

    private async Task DisposeCoreAsync()
    {
        _ = await this.CompleteAsync().ConfigureAwait(false);
    }

    private async Task<Result> CompleteCoreAsync()
    {
        var now = this.timeProvider.GetUtcNow();
        ProfilingSegmentOutcome terminalOutcome;
        string terminalExceptionType;
        string terminalExceptionMessage;
        lock (this.outcomeSync)
        {
            terminalOutcome = this.outcome;
            terminalExceptionType = this.exceptionType;
            terminalExceptionMessage = this.exceptionMessage;
        }

        var sessionResult = await this
            .store.FindSessionAsync(this.session.Identity.Key, CancellationToken.None)
            .ConfigureAwait(false);
        var collectionEnded =
            sessionResult.IsFailure
            || sessionResult.Value.State != ProfilingSessionState.Running
            || now >= sessionResult.Value.EndsUtc;
        var segmentResult = await this
            .store.UpsertSegmentAsync(
                this.segment with
                {
                    EndedUtc = now,
                    Elapsed = this.timeProvider.GetElapsedTime(this.startedTimestamp),
                    Outcome = terminalOutcome,
                    ExceptionType = terminalExceptionType,
                    ExceptionMessage = terminalExceptionMessage,
                    CollectionEndedBeforeOperation = collectionEnded,
                },
                CancellationToken.None
            )
            .ConfigureAwait(false);

        var stopResult = Result.Success();
        if (
            this.ownsSession
            && sessionResult.IsSuccess
            && sessionResult.Value.State == ProfilingSessionState.Running
            && now < sessionResult.Value.EndsUtc
        )
        {
            var controlResult = await this
                .control.StopAsync(CancellationToken.None)
                .ConfigureAwait(false);
            stopResult = controlResult.IsSuccess
                ? Result.Success()
                : Result
                    .Failure()
                    .WithErrors(controlResult.Errors)
                    .WithMessages(controlResult.Messages);
        }

        if (this.ownsSession)
        {
            this.activeSessionContext.Clear(this.session.Identity.Id);
        }

        if (segmentResult.IsFailure)
        {
            return Result
                .Failure()
                .WithErrors(segmentResult.Errors)
                .WithMessages(segmentResult.Messages);
        }

        return stopResult;
    }
}
