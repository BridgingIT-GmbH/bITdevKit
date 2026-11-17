// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

#nullable disable
namespace Presentation.TestHost;

using System.Text.Json.Serialization;
using BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>Contains one fictional reporting filter and its initialization marker.</summary>
/// <example><code>var filter = new LifecycleProbeModel { Active = false };</code></example>
public sealed record LifecycleProbeModel
{
    /// <summary>Gets the inclusive reporting start.</summary>
    public DateTimeOffset StartUtc { get; init; } = new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Gets the exclusive reporting end.</summary>
    public DateTimeOffset EndUtc { get; init; } = new(2026, 11, 17, 17, 0, 0, TimeSpan.Zero);

    /// <summary>Gets the accepted Active value.</summary>
    public bool Active { get; init; } = true;

    /// <summary>Gets the marker proving whether native activation repeated feature initialization.</summary>
    public int Initialization { get; init; }
}

/// <summary>Exercises native handoff independently of presentation shell behavior.</summary>
/// <param name="logger">The fixture logger.</param>
/// <param name="options">The configured logical owner.</param>
/// <param name="storageProvider">The optional preference provider.</param>
/// <param name="userContextProvider">The optional browser partition.</param>
/// <param name="debugger">The optional diagnostic observer.</param>
/// <example><code>await state.SetFilter(start, end, active: false);</code></example>
public sealed class LifecycleProbeState(ILogger<LifecycleProbeState> logger, AppStateOptions options,
    IAppStateStoreProvider storageProvider = null, IUserContextProvider userContextProvider = null,
    AppStateDebugger debugger = null)
    : AppState<LifecycleProbeModel>(logger, options, storageProvider, userContextProvider, debugger)
{
    private static int Initializations;
    private LifecycleProbeModel current = new();

    /// <summary>Simulates a feature's initial read using an observable monotonically increasing marker.</summary>
    /// <returns>The accepted initial filter.</returns>
    /// <example><code>if (!lifecycle.Restored) { await state.Initialize(); }</code></example>
    public Task Initialize() => this.RestoreSnapshot(new()
    {
        Active = false, Initialization = Interlocked.Increment(ref Initializations)
    });

    /// <summary>Accepts the complete filter while retaining its initialization marker.</summary>
    /// <param name="start">The inclusive start.</param>
    /// <param name="end">The exclusive end.</param>
    /// <param name="active">The accepted checkbox value.</param>
    /// <returns>The accepted change.</returns>
    /// <example><code>await state.SetFilter(start, end, active: true);</code></example>
    public Task SetFilter(DateTimeOffset start, DateTimeOffset end, bool active)
    {
        if (end <= start) { throw new ArgumentOutOfRangeException(nameof(end)); }

        return this.RestoreSnapshot(this.current with
        {
            StartUtc = start.ToUniversalTime(), EndUtc = end.ToUniversalTime(), Active = active
        });
    }

    /// <inheritdoc/>
    protected override LifecycleProbeModel GetCurrentState() => this.current;

    /// <inheritdoc/>
    protected override void UpdateState(LifecycleProbeModel state) => this.current = state;

    /// <inheritdoc/>
    protected override LifecycleProbeModel CreateDefaultState() => new();

    /// <inheritdoc/>
    protected override LifecycleProbeModel CloneState(LifecycleProbeModel value) => value is null ? null : value with { };
}

/// <summary>Supplies model metadata for the optional native transfer.</summary>
/// <example><code>var metadata = LifecycleProbeJson.Default.LifecycleProbeModel;</code></example>
[JsonSerializable(typeof(LifecycleProbeModel))]
public partial class LifecycleProbeJson : JsonSerializerContext;
