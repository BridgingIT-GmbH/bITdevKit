// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Serializes asynchronous work for equal keys within one lock pool.</summary>
/// <typeparam name="TKey">The type of key, with stable equality and hash codes while leased.</typeparam>
/// <remarks>
/// Each instance coordinates work in the current process. Equal keys share a semaphore even if rented
/// by the same caller. Different keys can be acquired independently. Dispose each lease to retire unused keys.
/// </remarks>
/// <example><code>
/// using var locks = new AsyncKeyedLock&lt;string&gt;(StringComparer.OrdinalIgnoreCase);
/// using var lease = locks.Rent("report-42");
/// if (await lease.TryAcquireAsync(TimeSpan.FromSeconds(2)))
/// {
///     await RefreshReportAsync("report-42");
/// }
/// </code></example>
public sealed class AsyncKeyedLock<TKey> : IDisposable
    where TKey : notnull
{
    private readonly Dictionary<TKey, Entry> entries;
    private readonly Lock gate = new();
    private bool disposed;

    /// <summary>Creates a keyed lock pool with the supplied key comparison.</summary>
    /// <param name="comparer">The key comparer, or null to use the default equality comparer for <typeparamref name="TKey"/>.</param>
    /// <example><code>using var locks = new AsyncKeyedLock&lt;string&gt;(StringComparer.Ordinal);</code></example>
    public AsyncKeyedLock(IEqualityComparer<TKey> comparer = null)
    {
        this.entries = new(comparer);
    }

    /// <summary>Reserves a single-use lease for a key without acquiring the lock.</summary>
    /// <param name="key">The key to lock.</param>
    /// <returns>A lease that the caller must dispose, even if it never attempts acquisition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The pool has been disposed.</exception>
    /// <example><code>using var lease = locks.Rent("report-42");</code></example>
    public ILease Rent(TKey key)
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);

            if (!this.entries.TryGetValue(key, out var entry))
            {
                entry = new();
                this.entries.Add(key, entry);
            }

            entry.References++;
            return new Lease(this, key, entry);
        }
    }

    /// <summary>Waits for exclusive access to a key and returns the acquired lease.</summary>
    /// <param name="key">The key to lock.</param>
    /// <param name="timeout">The wait limit. Use zero for an immediate attempt or <see cref="Timeout.InfiniteTimeSpan"/> for an indefinite wait.</param>
    /// <param name="cancellationToken">The token used to cancel the wait.</param>
    /// <returns>The acquired lease on success, or null on timeout. The caller disposes a returned lease to release the lock.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is outside the range accepted by <see cref="SemaphoreSlim.WaitAsync(TimeSpan, CancellationToken)"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="ObjectDisposedException">The pool has been disposed.</exception>
    /// <example><code>using var lease = await locks.TryAcquireAsync("report-42", TimeSpan.FromSeconds(2), cancellationToken);</code></example>
    public async Task<ILease> TryAcquireAsync(TKey key, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var lease = this.Rent(key);

        return await lease.TryAcquireAsync(timeout, cancellationToken).ConfigureAwait(false) ? lease : null;
    }

    /// <summary>Stops new rentals and lets existing leases finish.</summary>
    /// <remarks>Already rented leases can acquire, complete, and retire in the usual way.</remarks>
    /// <example><code>locks.Dispose();</code></example>
    public void Dispose()
    {
        lock (this.gate)
        {
            this.disposed = true;
        }
    }

    /// <summary>Reserves one acquisition attempt for a key and releases any acquired lock when disposed.</summary>
    /// <remarks>
    /// Dispose can be called repeatedly. During a pending wait, disposal defers cleanup until the wait
    /// settles. Use the acquisition's cancellation token to interrupt the wait. Keep the lease alive while
    /// using an acquired lock.
    /// </remarks>
    /// <example><code>
    /// using var lease = locks.Rent("report-42");
    /// var acquired = await lease.TryAcquireAsync(TimeSpan.Zero);
    /// </code></example>
    public interface ILease : IDisposable
    {
        /// <summary>Attempts to acquire the reserved key once.</summary>
        /// <param name="timeout">The wait limit. Use zero for an immediate attempt or <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.</param>
        /// <param name="cancellationToken">The token used to cancel the wait.</param>
        /// <returns>True on acquisition or false on timeout. A timeout, cancellation, or failed wait returns the reservation to the pool.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled during the attempt.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is outside the range accepted by <see cref="SemaphoreSlim.WaitAsync(TimeSpan, CancellationToken)"/>.</exception>
        /// <exception cref="InvalidOperationException">An attempt is already pending or the lease already owns the lock.</exception>
        /// <exception cref="ObjectDisposedException">The lease was already disposed, or disposal was requested during a wait that acquired the lock.</exception>
        /// <example><code>var acquired = await lease.TryAcquireAsync(TimeSpan.FromSeconds(2), cancellationToken);</code></example>
        Task<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    }

    private void Return(TKey key, Entry entry)
    {
        lock (this.gate)
        {
            entry.References--;

            if (entry.References == 0)
            {
                this.entries.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int References { get; set; }
    }

    private enum LeaseState
    {
        Rented,
        Waiting,
        Acquired,
        DisposePending,
        Disposed
    }

    private sealed class Lease(AsyncKeyedLock<TKey> pool, TKey key, Entry entry) : ILease
    {
        private readonly Lock gate = new();
        private LeaseState state;

        public async Task<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            lock (this.gate)
            {
                ObjectDisposedException.ThrowIf(this.state is LeaseState.DisposePending or LeaseState.Disposed, this);
                if (this.state != LeaseState.Rented)
                {
                    throw new InvalidOperationException("The lease has already attempted to acquire the lock.");
                }

                this.state = LeaseState.Waiting;
            }

            var acquired = false;
            bool disposedDuringWait;
            try
            {
                acquired = await entry.Semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (this.gate)
                {
                    disposedDuringWait = this.state == LeaseState.DisposePending;
                    if (acquired && !disposedDuringWait)
                    {
                        this.state = LeaseState.Acquired;
                    }
                    else
                    {
                        this.state = LeaseState.Disposed;
                        this.Release(acquired);
                    }
                }
            }

            ObjectDisposedException.ThrowIf(acquired && disposedDuringWait, this);
            return acquired;
        }

        public void Dispose()
        {
            lock (this.gate)
            {
                switch (this.state)
                {
                    case LeaseState.Waiting:
                        this.state = LeaseState.DisposePending;
                        break;
                    case LeaseState.Rented:
                    case LeaseState.Acquired:
                        var acquired = this.state == LeaseState.Acquired;
                        this.state = LeaseState.Disposed;
                        this.Release(acquired);
                        break;
                }
            }
        }

        private void Release(bool acquired)
        {
            if (acquired)
            {
                entry.Semaphore.Release();
            }

            pool.Return(key, entry);
        }
    }
}
