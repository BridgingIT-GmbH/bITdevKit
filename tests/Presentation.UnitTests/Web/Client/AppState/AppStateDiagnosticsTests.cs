// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies bounded metadata and default payload exclusion.</summary>
public class AppStateDiagnosticsTests
{
    /// <summary>Tracking does not retain private values or mutation reasons by default.</summary>
    [Fact]
    public async Task Track_OverCapacity_RetainsOnlyBoundedSanitizedMetadata()
    {
        var sut = new AppStateDebugger(NullLogger<AppStateDebugger>.Instance, null,
            new AppStateDebugOptions { StateChangesTracked = true, MaxTrackedChanges = 2 });
        for (var i = 0; i < 4; i++)
        {
            await sut.TrackStateChangeAsync(new StateChangeMetadata<object>
            {
                StateId = "catalog", Operation = "Update", Reason = "private reason",
                OldValue = new { Secret = "private" }, NewValue = new { Secret = "private" }
            });
        }

        var entries = sut.GetStateChanges();
        entries.Count.ShouldBe(2);
        entries.ShouldAllBe(entry => entry.OldValue == null && entry.NewValue == null && entry.Reason == null);
        sut.Clear();
        entries.Count.ShouldBe(2);
        sut.GetStateChanges().ShouldBeEmpty();
    }

    /// <summary>Diagnostic projections cannot mutate the live state or retain old-context values.</summary>
    [Fact]
    public async Task Projection_ContextReset_ReceivesIndependentAndSanitizedValues()
    {
        var debugger = new AppStateDebugger(NullLogger<AppStateDebugger>.Instance, null,
            new AppStateDebugOptions { StateChangesTracked = true, ValueProjection = value =>
            {
                if (value is CatalogViewModel model) { var page = model.Page; model.Page = 999; return page; }

                return null;
            }});
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions(), debugger: debugger);
        await sut.SetPageAsync(5);
        sut.CurrentState.Page.ShouldBe(5);
        await sut.ChangeContext(new("other", "workspace"));
        sut.CurrentState.Page.ShouldBe(0);
        var entry = debugger.GetStateChanges().Single();
        entry.OldValue.ShouldBeNull();
        entry.NewValue.ShouldBe(0);
    }

}
