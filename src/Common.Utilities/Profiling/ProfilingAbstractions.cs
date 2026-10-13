// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Coordinates Profiling's fixed target snapshots over the standalone Broadcast service.
/// </summary>
/// <example><code>var targets = await broadcasts.PrepareTargetsAsync(scopes, cancellationToken);</code></example>
public interface IRuntimeProfilingBroadcastService
{
    /// <summary>Prepares the exact active registrations used by one Profiling operation.</summary>
    Task<IResult<RuntimeProfilingBroadcastTargetSnapshot>> PrepareTargetsAsync(
        IEnumerable<string> targetScopes = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Publishes one Profiling command to an already prepared target set.</summary>
    Task<IResult<BroadcastResult>> PublishAsync<TBroadcast>(
        TBroadcast payload,
        RuntimeProfilingBroadcastTargetSnapshot targetSnapshot,
        BroadcastPublishOptions options = null,
        CancellationToken cancellationToken = default
    )
        where TBroadcast : IRuntimeProfilingBroadcast;
}

/// <summary>Associates Runtime Broadcast registrations with cached profiling identities.</summary>
/// <example><code>var node = await adapter.GetAsync(registration, cancellationToken);</code></example>
public interface IRuntimeProfilingNodeRegistrationAdapter
{
    /// <summary>Resolves cached local or already registered remote process metadata.</summary>
    /// <example><code>var node = await adapter.GetAsync(registration, cancellationToken);</code></example>
    Task<IResult<ProfilingNode>> GetAsync(BroadcastNodeRegistration registration, CancellationToken cancellationToken = default);
    /// <summary>Registers the cached local identity under its private Broadcast correlation.</summary>
    /// <example><code>await adapter.RegisterLocalAsync(registration, cancellationToken);</code></example>
    Task<IResult<ProfilingNode>> RegisterLocalAsync(BroadcastNodeRegistration registration, CancellationToken cancellationToken = default);
}
