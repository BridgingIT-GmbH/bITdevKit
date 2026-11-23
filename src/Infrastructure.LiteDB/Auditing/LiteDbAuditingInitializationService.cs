// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using Microsoft.Extensions.Hosting;

/// <summary>Initializes owned native audit state at host startup while leaving readiness probes read-only.</summary>
/// <param name="provider">The selected native auditing provider.</param>
public sealed class LiteDbAuditingInitializationService(LiteDbAuditingStorageProvider provider) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var result = await provider.InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) { throw new LiteDbProviderException(result); }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
