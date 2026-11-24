// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

#nullable disable
namespace Presentation.TestHost;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

/// <summary>Safe fictional catalog preferences used by the native browser fixture.</summary>
public sealed class StateProbeModel
{
    /// <summary>Gets or sets the selected page.</summary>
    public int Page { get; set; }
}

/// <summary>Uses the supported derived-state constructor without a framework page base.</summary>
public sealed class StateProbeState : AppState<StateProbeModel>
{
    private StateProbeModel current = new();

    /// <summary>Creates independent probe state without browser I/O.</summary>
    /// <param name="logger">The state logger.</param>
    /// <param name="options">The copied configuration.</param>
    /// <param name="storageProvider">The configured preference storage.</param>
    /// <param name="userContextProvider">The optional partition provider.</param>
    /// <param name="debugger">The optional bounded observer.</param>
    public StateProbeState(ILogger<StateProbeState> logger, AppStateOptions options,
        IAppStateStoreProvider storageProvider = null, IUserContextProvider userContextProvider = null,
        AppStateDebugger debugger = null) : base(logger, options, storageProvider, userContextProvider, debugger) { }

    /// <summary>Advances one accepted page value.</summary>
    /// <returns>The accepted transition.</returns>
    public Task IncrementAsync() => this.RestoreSnapshot(new() { Page = this.current.Page + 1 });

    /// <inheritdoc/>
    protected override StateProbeModel GetCurrentState() => this.current;

    /// <inheritdoc/>
    protected override void UpdateState(StateProbeModel state) => this.current = state;

    /// <inheritdoc/>
    protected override StateProbeModel CreateDefaultState() => new();
}

/// <summary>Supplies trim-safe model serialization for the probe.</summary>
[JsonSerializable(typeof(StateProbeModel))]
public partial class StateProbeJson : JsonSerializerContext { }
