// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;

/// <summary>Observes already admitted tasks and retains ambient resource ownership until actual task completion.</summary>
/// <remarks>This grants no capacity and does not start tasks. The caller must independently bound admitted work.
/// Stopping requests cancellation; it never treats an expired wait as completion of the actual work.</remarks>
/// <example><code>await using var tasks = new OwnedTaskTracker(TimeSpan.FromSeconds(5));
/// tasks.Track(admittedWork, "upload");</code></example>
public sealed class OwnedTaskTracker : IAsyncDisposable
{
    private readonly ConcurrentDictionary<long, Task> tasks = [];
    private readonly TimeSpan stopTimeout;
    private readonly Action<Exception, string> faulted;
    private readonly Action<int> stopTimedOut;
    private readonly CancellationTokenSource stopping = new();
    private readonly CancellationTokenRegistration hostStop;
    private readonly Lock stopGate = new();
    private long next;
    private int cancelling;
    private bool disposalRequested;
    private bool sourceDisposed;

    /// <summary>Creates a task owner with a finite shutdown wait and optional fault and timeout observers.</summary>
    /// <param name="stopTimeout">The nonnegative finite time to wait for actual work when disposed.</param>
    /// <param name="faulted">An optional observer receiving task or cleanup failures and the caller-defined category.</param>
    /// <param name="stopTimedOut">An optional observer receiving the occupied task count when the stop wait expires.</param>
    /// <param name="stoppingToken">An optional host or parent signal which requests cancellation without releasing ownership.</param>
    /// <example><code>var tracker = new OwnedTaskTracker(TimeSpan.FromSeconds(5),
    /// (exception, kind) => RecordFailure(kind, exception.GetType().Name), stoppingToken: hostStopping);</code></example>
    public OwnedTaskTracker(TimeSpan stopTimeout, Action<Exception, string> faulted = null,
        Action<int> stopTimedOut = null, CancellationToken stoppingToken = default)
    {
        if (stopTimeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(stopTimeout), "A nonnegative finite stop timeout is required.");
        }

        this.stopTimeout = stopTimeout;
        this.faulted = faulted;
        this.stopTimedOut = stopTimedOut;
        this.Stopping = this.stopping.Token;
        this.hostStop = stoppingToken.UnsafeRegister(static state => ((OwnedTaskTracker)state).BeginStop(), this);
    }

    /// <summary>Gets cancellation requested by a stop signal or disposal; the token remains observable after disposal.</summary>
    /// <example><code>await nativeWork(tracker.Stopping);</code></example>
    public CancellationToken Stopping { get; }

    /// <summary>Gets the actual tracked tasks whose observation and resource cleanup have not finished.</summary>
    /// <example><code>var occupied = tracker.Count;</code></example>
    public int Count => this.tasks.Count;

    /// <summary>Requests cancellation while containing notification callback failures.</summary>
    /// <remarks>Actual tasks and their captured resources remain owned until completion.</remarks>
    /// <example><code>tracker.BeginStop();</code></example>
    public void BeginStop()
    {
        lock (this.stopGate)
        {
            if (this.sourceDisposed) { return; }

            this.cancelling++;
        }

        try { this.stopping.Cancel(); }
        catch (AggregateException) { /* Cancellation does not stand in for task completion. */ }
        finally
        {
            lock (this.stopGate)
            {
                this.cancelling--;
                this.DisposeSourceIfFinished();
            }
        }
    }

    /// <summary>Captures any live ambient resource lease and observes one task through actual completion.</summary>
    /// <param name="task">Already admitted actual work, including work which may ignore cancellation.</param>
    /// <param name="kind">A caller-defined diagnostic category, not a task identifier or payload.</param>
    /// <remarks>Tracking does not start work or grant admission. An already admitted tail may be tracked after stopping.</remarks>
    /// <example><code>tracker.Track(actualWrite, "provider-write");</code></example>
    public void Track(Task task, string kind)
    {
        ArgumentNullException.ThrowIfNull(task);
        var resource = ResourceLeaseContext.Capture();
        var id = Interlocked.Increment(ref this.next);
        this.tasks[id] = task;
        _ = this.ObserveAsync(id, task, kind, resource);
    }

    /// <summary>Requests cancellation and waits at most the configured stop budget for the observed actual tasks.</summary>
    /// <remarks>Timed-out tasks remain tracked and charged. Observer failures cannot prevent cleanup.</remarks>
    /// <example><code>await tracker.DisposeAsync();</code></example>
    public async ValueTask DisposeAsync()
    {
        this.hostStop.Dispose();
        this.BeginStop();
        try
        {
            await Task.WhenAll(this.tasks.Values).WaitAsync(this.stopTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            try { this.stopTimedOut?.Invoke(this.Count); }
            catch (Exception) { /* An observer cannot change task ownership. */ }
        }
        catch (Exception) { /* Each task's observation reports its failure independently. */ }
        finally
        {
            lock (this.stopGate)
            {
                this.disposalRequested = true;
                this.DisposeSourceIfFinished();
            }
        }
    }

    private async Task ObserveAsync(long id, Task task, string kind, IDisposable resource)
    {
        try { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { this.ObserveFailure(exception, kind); }
        finally
        {
            try { resource?.Dispose(); }
            catch (Exception exception) { this.ObserveFailure(exception, kind); }
            finally
            {
                this.tasks.TryRemove(id, out _);
                lock (this.stopGate) { this.DisposeSourceIfFinished(); }
            }
        }
    }

    private void ObserveFailure(Exception exception, string kind)
    {
        try { this.faulted?.Invoke(exception, kind); }
        catch (Exception) { /* Observation must not create an unobserved task failure. */ }
    }

    private void DisposeSourceIfFinished()
    {
        if (this.disposalRequested && this.cancelling == 0 && this.tasks.IsEmpty && !this.sourceDisposed)
        {
            this.sourceDisposed = true;
            this.stopping.Dispose();
        }
    }
}