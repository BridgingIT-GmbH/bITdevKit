namespace BridgingIT.DevKit.Cli;

using System.ComponentModel;
using System.Diagnostics;

/// <summary>
/// Manages host operating system processes.
/// </summary>
public interface IHostProcessManager
{
    /// <summary>
    /// Terminates a process by id.
    /// </summary>
    /// <param name="processId">The process id.</param>
    /// <returns>The termination result.</returns>
    HostProcessKillResult Kill(int processId);
}

/// <summary>
/// Describes a host process termination attempt.
/// </summary>
public sealed record HostProcessKillResult(bool Succeeded, string Reason = null)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>The successful result.</returns>
    public static HostProcessKillResult Success() => new(true);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="reason">The failure reason.</param>
    /// <returns>The failed result.</returns>
    public static HostProcessKillResult Failure(string reason) => new(false, reason);
}

/// <summary>
/// Terminates host processes through the operating system.
/// </summary>
public sealed class HostProcessManager : IHostProcessManager
{
    /// <inheritdoc />
    public HostProcessKillResult Kill(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            return HostProcessKillResult.Success();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception or UnauthorizedAccessException)
        {
            return HostProcessKillResult.Failure(exception.Message);
        }
    }
}