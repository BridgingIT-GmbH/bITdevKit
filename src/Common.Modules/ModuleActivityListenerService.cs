// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Hosting;

/// <summary>
///     Manages the shared activity listener used to trace module activity for an application host.
/// </summary>
/// <remarks>
///     Starting the service acquires a listener lease. Stopping or disposing the service releases that lease.
/// </remarks>
/// <example>
/// <code>
/// services.AddHostedService&lt;ModuleActivityListenerService&gt;();
/// </code>
/// </example>
public sealed class ModuleActivityListenerService : IHostedService, IDisposable
{
    private IDisposable lease;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.lease ??= ModuleActivityListenerCoordinator.Acquire();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.lease?.Dispose();
        this.lease = null;
    }
}
