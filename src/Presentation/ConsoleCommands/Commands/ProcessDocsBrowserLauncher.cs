namespace BridgingIT.DevKit.Presentation;

using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Opens documentation URLs with the operating system default browser.
/// </summary>
public sealed class ProcessDocsBrowserLauncher : IDocsBrowserLauncher
{
    /// <summary>
    /// Gets the default process-based browser launcher instance.
    /// </summary>
    public static ProcessDocsBrowserLauncher Instance { get; } = new();

    /// <inheritdoc />
    public void Open(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
            return;
        }

        Process.Start("xdg-open", url);
    }
}