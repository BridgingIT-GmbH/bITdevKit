namespace BridgingIT.DevKit.Presentation;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Writes DevKit host startup diagnostics at debug level.
/// </summary>
/// <example>
/// <code>
/// services.AddHostedService&lt;DevKitHostStartupDiagnosticsService&gt;();
/// </code>
/// </example>
public sealed class DevKitHostStartupDiagnosticsService(
    DevKitHostStartupDiagnostics diagnostics,
    ILogger<DevKitHostStartupDiagnosticsService> logger) : IHostedService
{
    private const string LogKey = "BDK";

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "[{LogKey}] devkit host startup diagnostics (hostKind={HostKind}, application={ApplicationName}, environment={EnvironmentName}, contentRoot={ContentRootPath}, descriptorEligible={DescriptorEligible}, descriptorWriterRegistered={DescriptorWriterRegistered}, descriptorPath={DescriptorPath}, localToolingEnabled={LocalToolingEnabled}, features={Features}, reason={Reason})",
            LogKey,
            diagnostics.HostKind,
            diagnostics.ApplicationName,
            diagnostics.EnvironmentName,
            diagnostics.ContentRootPath,
            diagnostics.DescriptorEligible,
            diagnostics.DescriptorWriterRegistered,
            diagnostics.DescriptorPath,
            diagnostics.LocalToolingEnabled,
            string.Join(",", diagnostics.Features),
            diagnostics.Reason);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}