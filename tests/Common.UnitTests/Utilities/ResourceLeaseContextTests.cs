// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

/// <summary>Verifies reference-counted ownership across ambient scopes and asynchronous continuations.</summary>
public sealed class ResourceLeaseContextTests
{
    /// <summary>A null scope leaves the existing ambient resource available for capture.</summary>
    [Fact]
    public void Begin_NullResource_PreservesParentContext()
    {
        var resource = new Probe();
        var scope = ResourceLeaseContext.Begin(resource);

        ResourceLeaseContext.Begin(null).ShouldBeNull();
        var captured = ResourceLeaseContext.Capture();
        scope.Dispose();

        resource.Disposals.ShouldBe(0);
        captured.Dispose();
        captured.Dispose();
        resource.Disposals.ShouldBe(1);
    }

    /// <summary>A nested captured resource retains both accounts until the actual tail releases ownership.</summary>
    [Fact]
    public void Capture_NestedScope_RetainsParentUntilChildCompletes()
    {
        var parent = new Probe();
        var child = new Probe();
        var outer = ResourceLeaseContext.Begin(parent);
        var inner = ResourceLeaseContext.Begin(child);
        var captured = ResourceLeaseContext.Capture();

        inner.Dispose();
        outer.Dispose();

        ResourceLeaseContext.Capture().ShouldBeNull();
        parent.Disposals.ShouldBe(0);
        child.Disposals.ShouldBe(0);
        captured.Dispose();
        parent.Disposals.ShouldBe(1);
        child.Disposals.ShouldBe(1);
    }

    /// <summary>An inherited flow can extend a live capture after scope closure but cannot resurrect a released resource.</summary>
    [Fact]
    public async Task Capture_InheritedFlowAfterScopeEnds_ExtendsOnlyLiveResource()
    {
        var resource = new Probe();
        var scope = ResourceLeaseContext.Begin(resource);
        var captured = ResourceLeaseContext.Capture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var child = Task.Run(async () =>
        {
            await release.Task;
            var extended = ResourceLeaseContext.Capture();
            extended.ShouldNotBeNull();
            captured.Dispose();
            resource.Disposals.ShouldBe(0);
            extended.Dispose();
            ResourceLeaseContext.Capture().ShouldBeNull();
        });

        scope.Dispose();
        release.SetResult();
        await child.WaitAsync(TimeSpan.FromSeconds(10));

        resource.Disposals.ShouldBe(1);
    }

    private sealed class Probe : IDisposable
    {
        public int Disposals;

        public void Dispose() => Interlocked.Increment(ref this.Disposals);
    }
}