// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Attaches Runtime-only Broadcast correlation without replacing cached local identity.</summary>
/// <example><code>var adapter = new RuntimeProfilingNodeRegistrationAdapter(store, identities, broadcastIdentity);</code></example>
public sealed class RuntimeProfilingNodeRegistrationAdapter(
    IRuntimeProfilingStore store,
    IProfilingNodeIdentityProvider identities,
    IBroadcastNodeIdentityProvider broadcastIdentity = null) : IRuntimeProfilingNodeRegistrationAdapter
{
    /// <inheritdoc />
    public Task<IResult<ProfilingNode>> GetAsync(BroadcastNodeRegistration registration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsValid(registration))
        {
            return Task.FromResult<IResult<ProfilingNode>>(Result<ProfilingNode>.Failure().WithError(new ProfilingValidationError("A Broadcast node identity and process-start timestamp are required.")));
        }

        return string.Equals(registration.NodeIdentity, broadcastIdentity?.GetNodeIdentity(), StringComparison.OrdinalIgnoreCase)
            ? this.RegisterLocalAsync(registration, cancellationToken)
            : store.FindNodeAsync(Correlation(registration), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IResult<ProfilingNode>> RegisterLocalAsync(BroadcastNodeRegistration registration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsValid(registration))
        {
            return Task.FromResult<IResult<ProfilingNode>>(Result<ProfilingNode>.Failure().WithError(new ProfilingValidationError("A Broadcast node identity and process-start timestamp are required.")));
        }

        var correlation = Correlation(registration);
        return store.GetOrCreateNodeAsync(correlation, identities.GetNode() with { Correlation = correlation }, cancellationToken);
    }

    private static bool IsValid(BroadcastNodeRegistration registration) => registration is not null
        && !string.IsNullOrWhiteSpace(registration.NodeIdentity) && registration.ProcessStartedUtc != default;
    private static RuntimeProfilingNodeCorrelation Correlation(BroadcastNodeRegistration registration) =>
        new(registration.NodeIdentity.Trim(), registration.ProcessStartedUtc.ToUniversalTime());
}
