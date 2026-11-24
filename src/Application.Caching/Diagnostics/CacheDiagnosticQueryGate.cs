// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Shares one finite no-queue allowance across snapshot and metric-history chart queries.</summary>
/// <param name="catalog">The immutable validated diagnostics limits.</param>
/// <example><code>if (gate.TryEnter(out var lease)) { using (lease) { Query(); } }</code></example>
public sealed class CacheDiagnosticQueryGate(CacheCatalog catalog)
{
    private readonly Lock gate = new();
    private int occupied;

    /// <summary>Acquires one chart-query slot immediately, returning false when the bounded allowance is occupied.</summary>
    /// <param name="lease">The query's lifetime lease, or null when rejected.</param>
    /// <returns>Whether query work can begin.</returns>
    public bool TryEnter(out IDisposable lease)
    {
        lock (this.gate)
        {
            if (this.occupied >= catalog.Options.Diagnostics.MaximumChartQueries) { lease = null; return false; }

            this.occupied++;
            lease = new Lease(this);
            return true;
        }
    }

    private sealed class Lease(CacheDiagnosticQueryGate owner) : IDisposable
    {
        private int disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) { return; }

            lock (owner.gate) { owner.occupied--; }
        }
    }
}