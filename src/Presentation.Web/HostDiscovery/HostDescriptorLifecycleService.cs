namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Writes and cleans the current host descriptor with the host lifecycle. The descriptor is written again
/// once the server has started, so it carries the actual listening addresses. Startup also prunes descriptors
/// of hosts that no longer run.
/// </summary>
/// <example>
/// <code>
/// services.AddHostedService&lt;HostDescriptorLifecycleService&gt;();
/// </code>
/// </example>
public sealed class HostDescriptorLifecycleService(
    IServiceProvider services,
    HostRuntimeDescriptorWriter writer,
    HostDescriptorCleanupService cleanup,
    IHostApplicationLifetime lifetime) : IHostedService
{
    private CancellationTokenRegistration startedRegistration;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.startedRegistration = lifetime.ApplicationStarted.Register(() => _ = Task.Run(() => writer.WriteAsync(services)));
        _ = Task.Run(writer.PruneStale, CancellationToken.None);

        return writer.WriteAsync(services, cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.startedRegistration.Dispose();
        cleanup.RemoveCurrentDescriptor();

        return Task.CompletedTask;
    }
}