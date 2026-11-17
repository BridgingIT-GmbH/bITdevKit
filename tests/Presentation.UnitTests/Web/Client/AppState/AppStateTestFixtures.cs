// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.Logging;

/// <summary>Mutable preferences for the fictional catalog test consumer.</summary>
public sealed class CatalogViewModel
{
    /// <summary>Gets or sets the selected page.</summary>
    public int Page { get; set; }

    /// <summary>Gets or sets the search query.</summary>
    public string Search { get; set; } = string.Empty;

    /// <summary>Gets or sets independently copied selected IDs.</summary>
    public List<int> Selected { get; set; } = [];
}

/// <summary>Preserves the existing five-argument state constructor and overrides.</summary>
public class CatalogViewState : AppState<CatalogViewModel>
{
    private CatalogViewModel current = new();

    /// <summary>Creates the test state using the original consumer contract.</summary>
    /// <param name="logger">The state logger.</param>
    /// <param name="options">The per-instance settings.</param>
    /// <param name="storageProvider">The optional storage adapter.</param>
    /// <param name="userContextProvider">The optional user partition provider.</param>
    /// <param name="debugger">The optional diagnostic observer.</param>
    public CatalogViewState(ILogger<CatalogViewState> logger, AppStateOptions options,
        IAppStateStoreProvider storageProvider = null, IUserContextProvider userContextProvider = null,
        AppStateDebugger debugger = null) : base(logger, options, storageProvider, userContextProvider, debugger)
    {
    }

    /// <summary>Accepts a new page through the existing snapshot/restore API.</summary>
    /// <param name="page">The new page number.</param>
    /// <returns>The accepted transition.</returns>
    public Task SetPageAsync(int page)
    {
        var next = this.TakeSnapshot();
        next.Page = page;
        return this.RestoreSnapshot(next);
    }

    /// <inheritdoc/>
    protected override CatalogViewModel GetCurrentState() => this.current;

    /// <inheritdoc/>
    protected override void UpdateState(CatalogViewModel state) => this.current = state;

    /// <inheritdoc/>
    protected override CatalogViewModel CreateDefaultState() => new();
}
