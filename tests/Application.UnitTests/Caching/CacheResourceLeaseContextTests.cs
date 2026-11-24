// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Proves admitted caller resources stay charged through native tails without resurrection or nested-scope leakage.</summary>
public sealed class CacheResourceLeaseContextTests
{
    /// <summary>The last capture releases the original account exactly once after the caller scope ends.</summary>
    [Fact]
    public void CapturedResource_OutlivesCallerAndDisposesExactlyOnce()
    {
        var resource = new Probe();
        var scope = CacheResourceLeaseContext.Begin(resource);
        var first = CacheResourceLeaseContext.Capture();
        scope.Dispose();
        scope.Dispose();
        resource.Disposals.ShouldBe(0);
        CacheResourceLeaseContext.Capture().ShouldBeNull();
        first.Dispose();
        first.Dispose();
        resource.Disposals.ShouldBe(1);
    }

    /// <summary>An inherited execution context can extend an outstanding reference after its caller closes, but cannot revive a released account.</summary>
    [Fact]
    public async Task InheritedFlow_CanExtendOnlyWhileAnotherReferenceRemains()
    {
        var resource = new Probe();
        var scope = CacheResourceLeaseContext.Begin(resource);
        var retained = CacheResourceLeaseContext.Capture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var child = Task.Run(async () =>
        {
            await release.Task;
            var extended = CacheResourceLeaseContext.Capture();
            extended.ShouldNotBeNull();
            retained.Dispose();
            resource.Disposals.ShouldBe(0);
            extended.Dispose();
            CacheResourceLeaseContext.Capture().ShouldBeNull();
        });
        scope.Dispose();
        release.SetResult();
        await child;
        resource.Disposals.ShouldBe(1);
    }

    /// <summary>A nested native tail retains both admitted accounts, while scope disposal restores the previous context.</summary>
    [Fact]
    public void NestedScope_RetainsParentAndRestoresItsAmbientReference()
    {
        var parent = new Probe();
        var child = new Probe();
        var outer = CacheResourceLeaseContext.Begin(parent);
        var inner = CacheResourceLeaseContext.Begin(child);
        var tail = CacheResourceLeaseContext.Capture();
        inner.Dispose();
        using (CacheResourceLeaseContext.Capture()) { parent.Disposals.ShouldBe(0); }

        outer.Dispose();
        parent.Disposals.ShouldBe(0);
        child.Disposals.ShouldBe(0);
        tail.Dispose();
        parent.Disposals.ShouldBe(1);
        child.Disposals.ShouldBe(1);
    }

    private sealed class Probe : IDisposable
    {
        public int Disposals;

        public void Dispose() => Interlocked.Increment(ref this.Disposals);
    }
}