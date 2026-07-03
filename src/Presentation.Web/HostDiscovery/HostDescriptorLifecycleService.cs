namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Writes and cleans the current host descriptor with the host lifecycle.
/// </summary>
/// <example>
/// <code>
/// services.AddHostedService&lt;HostDescriptorLifecycleService&gt;();
/// </code>
/// </example>
public sealed class HostDescriptorLifecycleService(
    IServiceProvider services,
    HostRuntimeDescriptorWriter writer,
    HostDescriptorCleanupService cleanup) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
        => writer.WriteAsync(services, cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        cleanup.RemoveCurrentDescriptor();

        return Task.CompletedTask;
    }
}