// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies supported consumer contracts and isolated runtime ownership.</summary>
public class AppStateCompatibilityTests
{
    /// <summary>Default services need neither browser interop nor authentication.</summary>
    [Fact]
    public async Task AddAppState_NoBrowserServices_ResolvesAndUpdates()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAppState().AddState<CatalogViewState>().Done();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        sut.ShouldBeSameAs(scope.ServiceProvider.GetServices<IAppState>().Single());
        await sut.SetPageAsync(2);
        sut.CurrentState.Page.ShouldBe(2);
    }

    /// <summary>Disabling optional persistence never exposes the live snapshot reference.</summary>
    [Fact]
    public async Task TakeSnapshot_Disabled_RemainsIndependentAndMutable()
    {
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions { Enabled = false });
        var next = sut.TakeSnapshot();
        next.Page = 3;
        next.Selected.Add(42);
        sut.CurrentState.Page.ShouldBe(0);
        await sut.RestoreSnapshot(next);
        next.Selected.Add(99);
        sut.CurrentState.Page.ShouldBe(3);
        sut.CurrentState.Selected.ShouldBe([42]);
    }

    /// <summary>Undo and redo preserve a bounded, correct sequence.</summary>
    [Fact]
    public async Task UndoRedo_BoundedHistory_PreservesOppositeStack()
    {
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance,
            new AppStateOptions { HistoryEnabled = true, MaxHistoryItems = 2 });
        await sut.SetPageAsync(1);
        await sut.SetPageAsync(2);
        await sut.SetPageAsync(3);
        await sut.Undo();
        sut.CurrentState.Page.ShouldBe(2);
        await sut.Undo();
        sut.CurrentState.Page.ShouldBe(1);
        sut.CanUndo.ShouldBeFalse();
        await sut.Redo();
        sut.CurrentState.Page.ShouldBe(2);
        await sut.Redo();
        sut.CurrentState.Page.ShouldBe(3);
    }

    /// <summary>An observer failure cannot undo a transition or prevent another observer.</summary>
    [Fact]
    public async Task RestoreSnapshot_ThrowingObserver_CommitsAndNotifiesOthers()
    {
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
        var count = 0;
        sut.StateChanged += _ => throw new InvalidOperationException("observer");
        sut.StateChanged += _ => count++;
        await sut.SetPageAsync(1);
        count.ShouldBe(1);
        sut.CurrentState.Page.ShouldBe(1);
    }
}
