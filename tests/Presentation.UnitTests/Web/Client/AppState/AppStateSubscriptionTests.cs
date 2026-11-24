// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies changed-only callbacks, renderer dispatch, and obsolete-result rejection.</summary>
public class AppStateSubscriptionTests
{
    /// <summary>Unrelated values do not invoke a selector callback and there is no initial replay.</summary>
    [Fact]
    public async Task Subscribe_UnchangedSelectedValue_DoesNotNotify()
    {
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
        var calls = 0;
        using var subscription = sut.Subscribe(s => s.Search, (change, token) => { calls++; return Task.CompletedTask; });
        calls.ShouldBe(0);
        await sut.SetPageAsync(1);
        calls.ShouldBe(0);
        var next = sut.TakeSnapshot();
        next.Search = "tools";
        await sut.RestoreSnapshot(next);
        calls.ShouldBe(1);
    }

    /// <summary>Disposal prevents a callback queued on a renderer from starting.</summary>
    [Fact]
    public async Task Subscribe_DisposedBeforeDispatch_RejectsQueuedCallback()
    {
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
        Func<Task> queued = null;
        var calls = 0;
        var subscription = sut.Subscribe(s => s.Page, (change, token) => { calls++; return Task.CompletedTask; },
            dispatch: work => { queued = work; return Task.CompletedTask; });
        await sut.SetPageAsync(1);
        subscription.Dispose();
        await queued();
        calls.ShouldBe(0);
    }

    /// <summary>A callback ignoring cancellation cannot apply an obsolete snapshot conditionally.</summary>
    [Fact]
    public async Task Subscribe_NewerEdit_RejectsOldAsyncResult()
    {
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = sut.Subscribe(s => s.Page, async (change, token) =>
        {
            if (change.Current != 1) { return; }

            var captured = sut.TakeSnapshot();
            captured.Page = 99;
            started.TrySetResult();
            await release.Task;
            completed.TrySetResult(await sut.TryRestoreSnapshot(captured, change.Version));
        });
        await sut.SetPageAsync(1);
        await started.Task;
        await sut.SetPageAsync(2);
        release.TrySetResult();
        (await completed.Task).ShouldBeFalse();
        sut.CurrentState.Page.ShouldBe(2);
    }

    /// <summary>Reentrant transitions cannot deliver an older selected value after a newer one.</summary>
    [Fact]
    public async Task Subscribe_ReentrantObserver_IgnoresOlderNotification()
    {
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
        var observed = new List<int>();
        sut.StateChanged += value =>
        {
            if (((CatalogViewModel)value).Page == 1) { _ = sut.SetPageAsync(2); }
        };
        using var subscription = sut.Subscribe(model => model.Page, (change, token) =>
        {
            observed.Add(change.Current);
            return Task.CompletedTask;
        });
        await sut.SetPageAsync(1);
        sut.CurrentState.Page.ShouldBe(2);
        observed.ShouldBe([2]);
    }

    /// <summary>Structural selection equality and callback mutation preserve independent comparison values.</summary>
    [Fact]
    public async Task Subscribe_StructuralComparer_IsolatesCallbackValues()
    {
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
        var calls = 0;
        using var subscription = sut.Subscribe(model => model.Selected, (change, token) =>
        {
            calls++;
            change.Current.Add(99);
            return Task.CompletedTask;
        }, new SelectionComparer());
        await sut.RestoreSnapshot(new CatalogViewModel { Selected = [1] });
        await sut.RestoreSnapshot(new CatalogViewModel { Selected = [1] });
        calls.ShouldBe(1);
        sut.CurrentState.Selected.ShouldBe([1]);
        await sut.RestoreSnapshot(new CatalogViewModel { Selected = [1, 2] });
        calls.ShouldBe(2);
        sut.CurrentState.Selected.ShouldBe([1, 2]);
    }

    private sealed class SelectionComparer : IEqualityComparer<List<int>>
    {
        /// <inheritdoc/>
        public bool Equals(List<int> x, List<int> y) => x.SequenceEqual(y);

        /// <inheritdoc/>
        public int GetHashCode(List<int> obj) => obj.Count;
    }
}
