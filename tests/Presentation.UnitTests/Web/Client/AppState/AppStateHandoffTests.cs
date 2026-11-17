// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies lifecycle initialization, source precedence and logical ownership.</summary>
public class AppStateHandoffTests
{
    /// <summary>Only independent model values transfer; runtime tokens and undo history remain local.</summary>
    [Fact]
    public async Task TryRestoreHandoff_FreshDestination_CopiesValuesWithoutHistory()
    {
        using var source = CreateState();
        await source.RestoreSnapshot(new() { Page = 17, Selected = [42] });
        var handoff = source.CaptureHandoff();
        handoff.Model.Selected.Add(99);
        source.CurrentState.Selected.ShouldBe([42]);
        using var sut = CreateState();
        var originalVersion = sut.Version;

        sut.TryRestoreHandoff(handoff, originalVersion).ShouldBeTrue();

        sut.CurrentState.Page.ShouldBe(17);
        sut.CurrentState.Selected.ShouldBe([42, 99]);
        sut.CurrentState.ShouldNotBeSameAs(handoff.Model);
        sut.InstanceId.ShouldNotBe(source.InstanceId);
        sut.IsCurrent(source.Version).ShouldBeFalse();
        sut.IsCurrent(originalVersion).ShouldBeFalse();
        sut.CanUndo.ShouldBeFalse();
        sut.CanRedo.ShouldBeFalse();
        sut.TryRestoreHandoff(handoff, sut.Version).ShouldBeFalse();
    }

    /// <summary>Application, module, state, instance, schema, principal and workspace must all match.</summary>
    [Theory]
    [InlineData("application")]
    [InlineData("module")]
    [InlineData("state")]
    [InlineData("instance")]
    [InlineData("schema")]
    [InlineData("principal")]
    [InlineData("workspace")]
    public async Task TryRestoreHandoff_DifferentLogicalOwner_RejectsSnapshot(string dimension)
    {
        using var source = CreateState();
        await source.ChangeContext(new("operator", "north"));
        await source.SetPageAsync(17);
        var options = CreateOptions();
        switch (dimension)
        {
            case "application": options.ApplicationId = "other"; break;
            case "module": options.ModuleId = "other"; break;
            case "state": options.StateName = "other"; break;
            case "instance": options.InstanceKey = "other"; break;
            case "schema": options.SchemaVersion++; break;
        }

        using var sut = CreateState(options);
        await sut.ChangeContext(new(dimension == "principal" ? "other" : "operator",
            dimension == "workspace" ? "other" : "north"));

        sut.TryRestoreHandoff(source.CaptureHandoff(), sut.Version).ShouldBeFalse();
        sut.CurrentState.Page.ShouldBe(0);
    }

    /// <summary>A prior edit wins even if attachment captures the latest destination version.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryRestoreHandoff_AcceptedEdit_PreservesInput(bool captureAfterEdit)
    {
        using var source = CreateState();
        await source.SetPageAsync(17);
        using var sut = CreateState();
        var version = sut.Version;
        await sut.SetPageAsync(3);
        if (captureAfterEdit) { version = sut.Version; }

        sut.TryRestoreHandoff(source.CaptureHandoff(), version).ShouldBeFalse();
        sut.CurrentState.Page.ShouldBe(3);
        sut.CanUndo.ShouldBeTrue();
    }

    /// <summary>Both an in-flight load and a later load establish readiness without replacing handoff values.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Load_AcceptedHandoff_PreservesValuesWithoutAutomaticSave(bool loadFirst)
    {
        using var source = CreateState();
        await source.SetPageAsync(17);
        var store = new AppStatePersistenceTests.ControlledStore
        {
            HoldLoad = loadFirst, LoadValue = new() { Page = 8 }
        };
        var options = CreateOptions();
        options.PersistenceType = AppStatePersistenceType.CustomStorage;
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance, options, store);
        var loading = loadFirst ? sut.Load() : Task.CompletedTask;
        if (loadFirst) { await store.LoadStarted.Task; }

        sut.TryRestoreHandoff(source.CaptureHandoff(), sut.Version).ShouldBeTrue();
        if (!loadFirst) { loading = sut.Load(); }

        store.LoadRelease.TrySetResult();
        await loading;

        sut.CurrentState.Page.ShouldBe(17);
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.Ready);
        sut.CanUndo.ShouldBeFalse();
        store.Saves.ShouldBe(0);
        await sut.Save();
        store.Saves.ShouldBe(1);
    }

    /// <summary>Reset releases native-snapshot precedence so explicitly reloaded preferences can be used.</summary>
    [Fact]
    public async Task Reset_AcceptedHandoff_ReleasesPreferencePrecedence()
    {
        using var source = CreateState();
        await source.SetPageAsync(17);
        var store = new AppStatePersistenceTests.ControlledStore { LoadValue = new() { Page = 8 } };
        var options = CreateOptions();
        options.PersistenceType = AppStatePersistenceType.CustomStorage;
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance, options, store);
        sut.TryRestoreHandoff(source.CaptureHandoff(), sut.Version).ShouldBeTrue();

        await sut.Reset();
        await sut.Load();

        sut.CurrentState.Page.ShouldBe(8);
        sut.CanUndo.ShouldBeFalse();
    }

    /// <summary>A context invalidation between capture and application rejects the old version.</summary>
    [Fact]
    public async Task TryRestoreHandoff_ContextChanged_RejectsOldAcceptance()
    {
        using var source = CreateState();
        await source.SetPageAsync(17);
        using var sut = CreateState();
        var version = sut.Version;
        await sut.ChangeContext(new("other", "north"));

        sut.TryRestoreHandoff(source.CaptureHandoff(), version).ShouldBeFalse();
        sut.CurrentState.Page.ShouldBe(0);
    }

    /// <summary>Reentrant copying cannot invalidate a destination and then overwrite its newly accepted values.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryRestoreHandoff_InvalidatedDuringOldModelCopy_RejectsSnapshot(bool changeContext)
    {
        using var source = CreateState();
        await source.SetPageAsync(17);
        using var sut = new ChangingDuringCopyState(changeContext);

        sut.TryRestoreHandoff(source.CaptureHandoff(), sut.Version).ShouldBeFalse();
        sut.CurrentState.Page.ShouldBe(changeContext ? 0 : 3);
    }

    private static CatalogViewState CreateState(AppStateOptions options = null) =>
        new(NullLogger<CatalogViewState>.Instance, options ?? CreateOptions());

    private static AppStateOptions CreateOptions() => new()
    {
        ApplicationId = "catalog-handoff", ModuleId = "reports", StateName = "filter", InstanceKey = "header",
        HistoryEnabled = true, MaxHistoryItems = 3, DebounceDelay = TimeSpan.FromHours(1)
    };

    private sealed class ChangingDuringCopyState(bool changeContext)
        : CatalogViewState(NullLogger<CatalogViewState>.Instance, CreateOptions())
    {
        private int copies;

        /// <inheritdoc/>
        protected override CatalogViewModel CloneState(CatalogViewModel value)
        {
            if (++this.copies == 2)
            {
                if (changeContext) { _ = this.ChangeContext(new("other", "south")); }
                else { _ = this.SetPageAsync(3); }
            }

            return base.CloneState(value);
        }
    }
}
