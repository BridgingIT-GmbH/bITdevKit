namespace BridgingIT.DevKit.Cli;

using System.Diagnostics;

/// <summary>
/// Starts the one-shot reset processes of <c>bdk dev reset</c> and <c>bdk dev prune --confirm</c>, and the hosts of <c>bdk dev run</c>.
/// </summary>
/// <example>
/// <code>
/// var result = await runner.RunAsync("dotnet", ["run", "--project", projectPath, "--no-launch-profile", "--", "--bdk-dev-reset=forecasthub:w02"], repositoryRoot, cancellationToken);
/// </code>
/// </example>
public interface IDevAppInstanceProcessRunner
{
    /// <summary>
    /// Runs a process to completion and captures its output.
    /// </summary>
    /// <param name="fileName">The executable.</param>
    /// <param name="arguments">The structured arguments.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The process result.</returns>
    Task<DevAppInstanceProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken);

    /// <summary>
    /// Runs a long-lived process and streams its output lines until it exits.
    /// </summary>
    /// <remarks>
    /// The process shares the console of the CLI, so a console Ctrl+C reaches it directly. When
    /// <paramref name="stopToken"/> fires, the runner waits for the process to exit on its own. When
    /// <paramref name="killToken"/> fires after that, it terminates the process tree.
    /// </remarks>
    /// <param name="fileName">The executable.</param>
    /// <param name="arguments">The structured arguments.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="onOutput">Receives every standard output and standard error line.</param>
    /// <param name="stopToken">Signals that the process should stop.</param>
    /// <param name="killToken">Signals that the process tree must be terminated.</param>
    /// <returns>The process exit code.</returns>
    Task<int> RunStreamingAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, Action<string> onOutput, CancellationToken stopToken, CancellationToken killToken);
}

/// <summary>
/// The captured result of a process started by <see cref="IDevAppInstanceProcessRunner"/>.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">The captured standard output.</param>
/// <param name="StandardError">The captured standard error.</param>
public sealed record DevAppInstanceProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Runs processes through <see cref="Process"/> with structured arguments.
/// </summary>
/// <remarks>
/// The reset hosts start without a launch profile, so the runner sets <c>ASPNETCORE_ENVIRONMENT</c> and
/// <c>DOTNET_ENVIRONMENT</c> to <c>Development</c> when the caller's environment sets neither.
/// </remarks>
public sealed class DevAppInstanceProcessRunner : IDevAppInstanceProcessRunner
{
    /// <inheritdoc />
    public async Task<DevAppInstanceProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(fileName, arguments, workingDirectory, createNoWindow: true);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start '{fileName}'.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new DevAppInstanceProcessResult(process.ExitCode, await output, await error);
    }

    /// <inheritdoc />
    public async Task<int> RunStreamingAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, Action<string> onOutput, CancellationToken stopToken, CancellationToken killToken)
    {
        // The child keeps the CLI console (no CreateNoWindow) so a console Ctrl+C reaches it.
        var startInfo = CreateStartInfo(fileName, arguments, workingDirectory, createNoWindow: false);
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => Forward(e.Data);
        process.ErrorDataReceived += (_, e) => Forward(e.Data);
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start '{fileName}'.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(stopToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                await process.WaitForExitAsync(killToken);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }

        process.WaitForExit();

        return process.ExitCode;

        void Forward(string line)
        {
            if (line is not null)
            {
                onOutput?.Invoke(line);
            }
        }
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, IReadOnlyList<string> arguments, string workingDirectory, bool createNoWindow)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = createNoWindow,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")))
        {
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        }

        return startInfo;
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The process exited between the check and the kill.
        }
    }
}
