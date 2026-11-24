// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Proves fixed deadline notification faults cannot escape timers or release occupied work.</summary>
public sealed class CacheCancellationDeadlineTests
{
    /// <summary>All callbacks run despite a failing callback, and callback disposal safely ends the deadline owner.</summary>
    [Fact]
    public void Deadline_ThrowingCallbackAndReentrantDisposal_ContainsFaultAndNotifiesAllCallbacks()
    {
        var time = new FakeTimeProvider();
        using var sut = new CacheCancellationDeadline(TimeSpan.FromSeconds(5), time);
        var token = sut.Token;
        var notified = 0;
        using var disposal = token.Register(() => { notified++; sut.Dispose(); });
        using var failure = token.Register(() => throw new InvalidOperationException("Expected notification fault."));

        time.Advance(TimeSpan.FromSeconds(5));

        token.IsCancellationRequested.ShouldBeTrue();
        notified.ShouldBe(1);
        sut.Cancel();
        time.Advance(TimeSpan.FromSeconds(5));
        notified.ShouldBe(1);
    }

    /// <summary>A native provider callback can fail cancellation while its operation remains admitted until actual completion.</summary>
    [Fact]
    public async Task ProviderAttempt_ThrowingNativeCallback_RetainsAdmissionUntilActualCompletion()
    {
        var native = Substitute.For<ICacheStoreProvider>();
        native.Name.Returns("database");
        native.Capabilities.Returns(new CacheProviderCapabilities(true, true, true, false, true, false));
        await using var fixture = new CacheBackgroundFixture(builder => builder
            .WithProviderKind("relational", native.Capabilities, (_, _, _) => native)
            .WithProvider("database", provider => provider.With(x => x.Kind, "relational").With(x => x.AttemptTimeout, TimeSpan.FromSeconds(5)))
            .WithCollection<string>("cards", collection => collection.With(x => x.Provider, "database")));
        var resources = fixture.Services.GetRequiredService<CacheRuntimeResources>();
        var tasks = fixture.Services.GetRequiredService<CacheRuntimeTasks>();
        var sut = new CacheProviderAccess(fixture.Catalog.Collections["cards"], native, resources, new(fixture.Catalog), tasks, fixture.Time);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask<Result<int>> Native(CancellationToken token)
        {
            using var registration = token.Register(() => throw new InvalidOperationException("Expected native notification fault."));
            entered.TrySetResult();
            await release.Task;
            return Result<int>.Success(1);
        }

        var pending = sut.CallAsync(Native, default, initialize: false).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            fixture.Time.Advance(TimeSpan.FromSeconds(5));
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));

            result.IsFailure.ShouldBeTrue();
            result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.ProviderUnavailable);
            resources.ProviderIO.Active.ShouldBe(1);
        }
        finally { release.TrySetResult(); }

        using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (tasks.Count != 0) { await Task.Delay(1, drain.Token); }

        resources.ProviderIO.Active.ShouldBe(0);
    }
}