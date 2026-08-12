// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Resolves the local Broadcast process registration to one stable profiling node.
/// </summary>
/// <param name="store">The configured profiling store.</param>
/// <example><code>var node = await provider.GetAsync(registration, cancellationToken);</code></example>
public sealed class ProfilingNodeIdentityProvider(IProfilingStore store)
    : IProfilingNodeIdentityProvider
{
    private readonly IProfilingStore store =
        store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc />
    public Task<Result<ProfilingNode>> GetAsync(
        BroadcastNodeRegistration registration,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (
            registration is null
            || string.IsNullOrWhiteSpace(registration.NodeIdentity)
            || registration.ProcessStartedUtc == default
        )
        {
            return Task.FromResult(
                Result<ProfilingNode>
                    .Failure()
                    .WithError(
                        new ProfilingValidationError(
                            "A Broadcast node identity and process-start timestamp are required."
                        )
                    )
            );
        }

        var correlation = new ProfilingNodeCorrelation(
            registration.NodeIdentity.Trim(),
            registration.ProcessStartedUtc.ToUniversalTime()
        );
        var proposed = new ProfilingNode
        {
            Identity = ProfilingNodeIdentity.Create(),
            Correlation = correlation,
            HostName = Environment.MachineName,
            ProcessId = Environment.ProcessId,
        };

        return this.store.GetOrCreateNodeAsync(correlation, proposed, cancellationToken);
    }
}
