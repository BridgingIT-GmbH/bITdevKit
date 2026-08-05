// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Denies privileged Broadcasting registry mutations by default.</summary>
/// <example><code>services.AddSingleton&lt;IBroadcastOperationalAuthorizer, DenyBroadcastOperationalAuthorizer&gt;();</code></example>
public sealed class DenyBroadcastOperationalAuthorizer : IBroadcastOperationalAuthorizer
{
    /// <inheritdoc />
    public ValueTask<bool> CanRemoveAsync(
        string nodeIdentity,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }
}

/// <summary>Provides a safe operational view over the effective node registry.</summary>
/// <param name="options">The shared Broadcasting configuration.</param>
/// <param name="registry">The effective node registry.</param>
/// <param name="authorizer">The operational mutation authorizer.</param>
/// <param name="metrics">The optional metrics service.</param>
/// <example><code>var snapshot = await diagnostics.GetAsync(cancellationToken);</code></example>
public sealed class BroadcastingDiagnostics(
    BroadcastingOptions options,
    IBroadcastRegistryStore registry,
    IBroadcastOperationalAuthorizer authorizer,
    IMetricsService metrics = null
) : IBroadcastingDiagnostics
{
    /// <inheritdoc />
    public async Task<BroadcastingDiagnosticSnapshot> GetAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!options.Enabled)
        {
            return new(false, []);
        }

        var nodes = await registry.ListAsync(cancellationToken).ConfigureAwait(false);
        var scopes = nodes
            .SelectMany(node => node.Scopes.Select(scope => (Scope: scope, Node: node)))
            .GroupBy(item => item.Scope, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new BroadcastScopeDiagnostic(
                group.First().Scope,
                group
                    .Select(item => item.Node)
                    .OrderBy(node => node.NodeIdentity, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            ))
            .ToArray();
        return new(true, scopes);
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(
        string nodeIdentity,
        CancellationToken cancellationToken = default
    )
    {
        if (!options.Enabled)
        {
            return Result.Failure().WithError(new BroadcastingDisabledError());
        }

        if (string.IsNullOrWhiteSpace(nodeIdentity))
        {
            return Result
                .Failure()
                .WithError(new BroadcastValidationError("A node identity is required."));
        }

        if (!await authorizer.CanRemoveAsync(nodeIdentity, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure().WithError(new BroadcastOperationalAuthorizationError());
        }

        await registry.RemoveAsync(nodeIdentity, cancellationToken).ConfigureAwait(false);
        BroadcastingMetrics.RecordStaleRemoval(metrics, 1);
        return Result.Success();
    }
}
