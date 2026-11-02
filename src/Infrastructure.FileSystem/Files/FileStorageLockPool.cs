// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem;

internal sealed class FileStorageLockPool : IDisposable
{
    private readonly Dictionary<string, Entry> entries = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly Lock gate = new();
    private bool disposed;

    internal Lease Rent(string path)
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);

            if (!this.entries.TryGetValue(path, out var entry))
            {
                entry = new();
                this.entries.Add(path, entry);
            }

            entry.References++;

            return new(this, path, entry);
        }
    }

    private void Return(string path, Entry entry)
    {
        lock (this.gate)
        {
            entry.References--;

            if (entry.References == 0)
            {
                this.entries.Remove(path);
                entry.Semaphore.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (this.gate)
        {
            // Active holders and waiters retire their entry when the last lease is returned.
            this.disposed = true;
        }
    }

    internal sealed class Entry
    {
        internal SemaphoreSlim Semaphore { get; } = new(1, 1);

        internal int References { get; set; }
    }

    internal sealed class Lease(FileStorageLockPool pool, string path, Entry entry) : IDisposable
    {
        private bool acquired;
        private int disposed;

        internal async Task<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                this.acquired = await entry.Semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);

                if (!this.acquired)
                {
                    this.Dispose();
                }

                return this.acquired;
            }
            catch
            {
                this.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            if (this.acquired)
            {
                entry.Semaphore.Release();
            }

            pool.Return(path, entry);
        }
    }
}