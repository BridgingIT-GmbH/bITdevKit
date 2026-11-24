// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Retains a caller-owned resource while asynchronous work outlives its bounded caller wait.</summary>
/// <remarks>This context grants no admission. It contains only disposable resource accounts; it never carries payloads, identities or HTTP objects.
/// Dispose scopes in reverse creation order in the same asynchronous flow. Captures may extend a live resource after its original scope closes,
/// provided another capture still owns it. A resource whose last reference was released cannot be resurrected.</remarks>
/// <example><code>using var lease = ResourceLeaseContext.Begin(decodedGraphCharge); await operation();</code></example>
public static class ResourceLeaseContext
{
    private static readonly AsyncLocal<Scope> Current = new();

    /// <summary>Takes ownership of one existing resource account and establishes an ambient reference until this scope and all captures end.</summary>
    /// <param name="resource">An already admitted resource; the context becomes responsible for its exactly-once disposal. Null adds no scope.</param>
    /// <returns>A scope which restores the previous ambient context and releases its own reference when disposed, or null for a null resource.</returns>
    /// <remarks>Nested scopes retain their parent account transitively. Do not also dispose the supplied account independently.</remarks>
    /// <example><code>using var scope = ResourceLeaseContext.Begin(alreadyAdmittedLease);</code></example>
    public static IDisposable Begin(IDisposable resource)
    {
        if (resource is null) { return null; }

        var parent = Current.Value;
        var scope = new Scope(new State(resource, parent?.State.Retain()), parent);
        Current.Value = scope;
        return scope;
    }

    /// <summary>Retains the current live resource for already admitted work, without changing the current asynchronous flow.</summary>
    /// <returns>An independently disposable reference, or null when no ambient resource remains live.</returns>
    /// <example><code>using var retained = ResourceLeaseContext.Capture(); await actualNativeWork;</code></example>
    public static IDisposable Capture() => Current.Value?.State.Retain();

    private sealed class Scope(State state, Scope previous) : IDisposable
    {
        private int disposed;

        public State State { get; } = state;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) { return; }

            if (ReferenceEquals(Current.Value, this)) { Current.Value = previous; }

            this.State.Release();
        }
    }

    private sealed class State(IDisposable resource, IDisposable parent)
    {
        private int references = 1;

        public IDisposable Retain()
        {
            while (true)
            {
                var observed = Volatile.Read(ref this.references);
                if (observed == 0) { return null; }

                if (observed == int.MaxValue) { throw new InvalidOperationException("The resource reference bound was exceeded."); }

                if (Interlocked.CompareExchange(ref this.references, observed + 1, observed) == observed) { return new Reference(this); }
            }
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref this.references) != 0) { return; }

            try { resource.Dispose(); }
            finally { parent?.Dispose(); }
        }
    }

    private sealed class Reference(State state) : IDisposable
    {
        private State retained = state;

        public void Dispose() => Interlocked.Exchange(ref this.retained, null)?.Release();
    }
}