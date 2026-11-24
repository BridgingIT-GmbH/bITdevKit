// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Provides bounded hierarchical active/queue admission without occupying a parent slot while a child waits.</summary>
/// <remarks>One bounded root queue skips temporarily blocked child groups so unrelated groups can progress.
/// This limits occupied work rather than operations per time interval. A lease stays owned until actual work completes.</remarks>
/// <example><code>using var lease = (await admission.AcquireAsync(TimeSpan.FromSeconds(5), cancellationToken)).Value;</code></example>
public sealed class ResourceAdmission
{
    private readonly ResourceAdmission parent;
    private readonly State state;
    private readonly TimeProvider timeProvider;
    private int active;
    private int queued;

    /// <summary>Creates an admission group. Children share their parent's clock and root queue.</summary>
    /// <param name="name">The diagnostic resource category.</param>
    /// <param name="maximumActive">The positive occupied-operation ceiling.</param>
    /// <param name="maximumQueued">The nonnegative queue ceiling for this group and its descendants.</param>
    /// <param name="parent">An optional parent whose limits also apply.</param>
    /// <param name="timeProvider">The root clock, ignored when a parent supplies its shared clock.</param>
    /// <example><code>var group = new ResourceAdmission("uploads", 4, 16, processResources);</code></example>
    public ResourceAdmission(string name, int maximumActive, int maximumQueued, ResourceAdmission parent = null, TimeProvider timeProvider = null)
    {
        this.Name = name ?? throw new ArgumentNullException(nameof(name));
        this.MaximumActive = maximumActive > 0 ? maximumActive : throw new ArgumentOutOfRangeException(nameof(maximumActive));
        this.MaximumQueued = maximumQueued >= 0 ? maximumQueued : throw new ArgumentOutOfRangeException(nameof(maximumQueued));
        this.parent = parent;
        this.state = parent?.state ?? new();
        this.timeProvider = parent?.timeProvider ?? timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the stable resource category.</summary>
    /// <example><code>var category = admission.Name;</code></example>
    public string Name { get; }

    /// <summary>Gets this group's occupied-operation ceiling.</summary>
    /// <example><code>var limit = admission.MaximumActive;</code></example>
    public int MaximumActive { get; }

    /// <summary>Gets this group's finite queue ceiling.</summary>
    /// <example><code>var queueLimit = admission.MaximumQueued;</code></example>
    public int MaximumQueued { get; }

    /// <summary>Gets active work. A timeout alone does not decrement this count.</summary>
    /// <example><code>var occupied = admission.Active;</code></example>
    public int Active
    {
        get
        {
            lock (this.state.Gate) { return this.active; }
        }
    }

    /// <summary>Gets queued admissions for this group and its descendants.</summary>
    /// <example><code>var waiting = admission.Queued;</code></example>
    public int Queued
    {
        get
        {
            lock (this.state.Gate) { return this.queued; }
        }
    }

    /// <summary>Attempts immediate admission without queueing or waiting.</summary>
    /// <param name="lease">The caller-owned lease on success, or null when any group is full.</param>
    /// <returns>Whether this group and all its parents had available occupied capacity.</returns>
    /// <example><code>if (admission.TryAcquire(out var owned)) { using (owned) { Execute(); } }</code></example>
    public bool TryAcquire(out ResourceAdmissionLease lease)
    {
        lock (this.state.Gate)
        {
            if (!this.CanActivate())
            {
                lease = null;
                return false;
            }

            this.ChangeActive(1);
            lease = new(this);
            return true;
        }
    }

    /// <summary>Acquires bounded capacity or returns a quota or timeout failure; caller cancellation propagates.</summary>
    /// <param name="timeout">The positive finite maximum queue wait.</param>
    /// <param name="cancellationToken">Cancellation which withdraws only this pending admission.</param>
    /// <returns>The caller-owned lease, or a neutral quota or timeout error.</returns>
    /// <example><code>var result = await admission.AcquireAsync(TimeSpan.FromSeconds(5), cancellationToken);</code></example>
    public ValueTask<Result<ResourceAdmissionLease>> AcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
        {
            return ValueTask.FromResult(Failure(new TimeoutError("The admission deadline has elapsed.")));
        }

        lock (this.state.Gate)
        {
            if (this.CanActivate())
            {
                this.ChangeActive(1);
                return ValueTask.FromResult(Result<ResourceAdmissionLease>.Success(new(this)));
            }

            if (!this.CanQueue())
            {
                return ValueTask.FromResult(Failure(new QuotaExceededError("The bounded resource admission queue is full.")));
            }

            if (timeout.TotalMilliseconds > uint.MaxValue - 1)
            {
                return ValueTask.FromResult(Failure(new TimeoutError("The resource admission wait exceeds the supported timer interval.")));
            }

            var ticket = new Ticket(this);
            ticket.Node = this.state.Queue.AddLast(ticket);
            this.ChangeQueued(1);
            return this.WaitAsync(ticket, timeout, cancellationToken);
        }
    }

    /// <summary>Returns one actually finished occupied unit and admits queued work within the shared parent and child limits.</summary>
    /// <remarks>Prefer disposing the acquired lease. Call this only for a raw unit whose ownership was transferred explicitly.</remarks>
    /// <example><code>admission.Release();</code></example>
    public void Release()
    {
        lock (this.state.Gate)
        {
            if (this.active == 0) { throw new InvalidOperationException("No owned resource admission is available to release."); }

            this.ChangeActive(-1);
            var node = this.state.Queue.First;
            while (node is not null)
            {
                var next = node.Next;
                var ticket = node.Value;
                if (ticket.Owner.CanActivate())
                {
                    this.state.Queue.Remove(node);
                    ticket.Node = null;
                    ticket.Owner.ChangeQueued(-1);
                    ticket.Owner.ChangeActive(1);
                    ticket.Completion.SetResult(new(ticket.Owner));
                }

                node = next;
            }
        }
    }

    private async ValueTask<Result<ResourceAdmissionLease>> WaitAsync(Ticket ticket, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return Result<ResourceAdmissionLease>.Success(
                await ticket.Completion.Task.WaitAsync(timeout, this.timeProvider, cancellationToken).ConfigureAwait(false));
        }
        catch (TimeoutException)
        {
            this.Withdraw(ticket);
            return Failure(new TimeoutError("The bounded resource admission wait expired."));
        }
        catch (OperationCanceledException)
        {
            this.Withdraw(ticket);
            throw;
        }
    }

    private void Withdraw(Ticket ticket)
    {
        ResourceAdmissionLease granted = null;
        lock (this.state.Gate)
        {
            if (ticket.Node is not null)
            {
                this.state.Queue.Remove(ticket.Node);
                ticket.Node = null;
                ticket.Owner.ChangeQueued(-1);
                ticket.Completion.TrySetCanceled(new CancellationToken(true));
            }
            else if (ticket.Completion.Task.IsCompletedSuccessfully)
            {
                granted = ticket.Completion.Task.Result;
            }
        }

        granted?.Dispose(); // A grant racing cancellation still owns capacity until returned here.
    }

    private bool CanActivate() => this.active < this.MaximumActive && (this.parent?.CanActivate() ?? true);

    private bool CanQueue() => this.queued < this.MaximumQueued && (this.parent?.CanQueue() ?? true);

    private void ChangeActive(int delta)
    {
        this.active += delta;
        if (this.active < 0)
        {
            throw new InvalidOperationException("Active resource admission accounting became negative.");
        }

        this.parent?.ChangeActive(delta);
    }

    private void ChangeQueued(int delta)
    {
        this.queued += delta;
        this.parent?.ChangeQueued(delta);
    }

    private static Result<ResourceAdmissionLease> Failure(ResultErrorBase error) => Result<ResourceAdmissionLease>.Failure(error);

    private sealed class State
    {
        public readonly Lock Gate = new();
        public readonly LinkedList<Ticket> Queue = [];
    }

    private sealed class Ticket(ResourceAdmission owner)
    {
        public readonly ResourceAdmission Owner = owner;
        public readonly TaskCompletionSource<ResourceAdmissionLease> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<Ticket> Node;
    }
}

/// <summary>Owns active admission until execution or transfer actually terminates.</summary>
/// <example><code>using var lease = result.Value;</code></example>
public sealed class ResourceAdmissionLease : IDisposable
{
    private ResourceAdmission owner;

    /// <summary>Takes ownership of one already admitted occupied unit; disposal returns it exactly once.</summary>
    /// <param name="owner">The group whose already occupied unit this lease owns.</param>
    /// <example><code>using var lease = new ResourceAdmissionLease(alreadyAdmittedGroup);</code></example>
    public ResourceAdmissionLease(ResourceAdmission owner) => this.owner = owner;

    /// <summary>Releases active work exactly once. Cancelling a wait/source does not release it automatically.</summary>
    /// <example><code>lease.Dispose();</code></example>
    public void Dispose() => Interlocked.Exchange(ref this.owner, null)?.Release();
}