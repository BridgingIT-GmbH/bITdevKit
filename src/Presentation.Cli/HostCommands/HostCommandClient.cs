namespace BridgingIT.DevKit.Cli;

using System.IO.Pipes;
using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Sends Console Command forwarding requests to a selected host.
/// </summary>
public sealed class HostCommandClient
{
    private const int ProtocolVersion = 1;
    private static readonly JsonSerializerOptions WireJsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Runs a Console Command in the selected host process.
    /// </summary>
    /// <param name="host">The selected host.</param>
    /// <param name="rawTokens">The command tokens to execute in the host.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The forwarding response.</returns>
    public async Task<HostCommandForwardingResponse> RunAsync(
        HostRuntimeInfo host,
        string[] rawTokens,
        CancellationToken cancellationToken = default)
    {
        if (host.Descriptor.Features?.TryGetValue("consoleCommands", out var endpoint) != true)
        {
            return HostCommandForwardingResponse.Unavailable("feature_unavailable", "The selected host does not advertise Console Command forwarding.", CliExitCode.HostNotFound);
        }

        if (endpoint.ProtocolVersion != ProtocolVersion)
        {
            return HostCommandForwardingResponse.Unavailable("version_mismatch", "The host Console Command forwarding protocol is not compatible.", CliExitCode.ProtocolVersionMismatch);
        }

        if (!string.Equals(endpoint.Transport, "named-pipe", StringComparison.OrdinalIgnoreCase))
        {
            return HostCommandForwardingResponse.Unavailable("selected_host_unavailable", $"Unsupported host command transport '{endpoint.Transport}'.", CliExitCode.SelectedHostUnavailable);
        }

        try
        {
            await using var pipe = new NamedPipeClientStream(".", endpoint.Endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);

            await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, leaveOpen: true);
            var request = new LocalConsoleCommandIpcRequest(endpoint.Nonce, ProtocolVersion, "run", rawTokens);
            await writer.WriteLineAsync(JsonSerializer.Serialize(request)).ConfigureAwait(false);
            var responseJson = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            var response = JsonSerializer.Deserialize<LocalConsoleCommandIpcResponse>(responseJson, WireJsonOptions);

            if (response is null)
            {
                return HostCommandForwardingResponse.Unavailable("host_command_failed", "The host returned an empty response.", CliExitCode.CommandFailed);
            }

            if (response.ProtocolVersion != ProtocolVersion)
            {
                return HostCommandForwardingResponse.Unavailable("version_mismatch", "The host Console Command forwarding protocol response is not compatible.", CliExitCode.ProtocolVersionMismatch);
            }

            return response.Ok
                ? HostCommandForwardingResponse.Success(response.Output ?? string.Empty)
                : HostCommandForwardingResponse.HostCommandFailed(
                    response.Error ?? "The host command failed.",
                    response.Output ?? string.Empty,
                    response.ExitCode == 0 ? CliExitCode.CommandFailed : (CliExitCode)response.ExitCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return HostCommandForwardingResponse.Unavailable("timeout", "The host command was cancelled.", CliExitCode.CommandFailed);
        }
        catch (OperationCanceledException)
        {
            return HostCommandForwardingResponse.Unavailable("timeout", "The host command did not complete before the timeout.", CliExitCode.CommandFailed);
        }
        catch (IOException exception)
        {
            return HostCommandForwardingResponse.Unavailable("selected_host_unavailable", exception.Message, CliExitCode.SelectedHostUnavailable);
        }
        catch (TimeoutException exception)
        {
            return HostCommandForwardingResponse.Unavailable("timeout", exception.Message, CliExitCode.CommandFailed);
        }
        catch (UnauthorizedAccessException exception)
        {
            return HostCommandForwardingResponse.Unavailable("selected_host_unavailable", exception.Message, CliExitCode.SelectedHostUnavailable);
        }
    }

    private sealed record LocalConsoleCommandIpcRequest(string Nonce, int ProtocolVersion, string Operation, string[] RawTokens);

    private sealed record LocalConsoleCommandIpcResponse(bool Ok, int ProtocolVersion, int ExitCode, string Output, string Error);
}

/// <summary>
/// Represents the result of a host Console Command forwarding request.
/// </summary>
/// <param name="Available">A value indicating whether the command was accepted by the host.</param>
/// <param name="Succeeded">A value indicating whether the forwarded command succeeded.</param>
/// <param name="Code">The stable error code when unavailable.</param>
/// <param name="Summary">The error summary when unavailable.</param>
/// <param name="ExitCode">The CLI exit code.</param>
/// <param name="Output">The captured host command output.</param>
public sealed record HostCommandForwardingResponse(bool Available, bool Succeeded, string Code, string Summary, CliExitCode ExitCode, string Output)
{
    /// <summary>
    /// Creates a successful forwarding response.
    /// </summary>
    /// <param name="output">The captured host command output.</param>
    /// <returns>The successful response.</returns>
    public static HostCommandForwardingResponse Success(string output)
        => new(true, true, null, null, CliExitCode.Success, output);

    /// <summary>
    /// Creates a response for a host command that ran and failed.
    /// </summary>
    /// <param name="summary">The failure summary.</param>
    /// <param name="output">The captured host command output.</param>
    /// <param name="exitCode">The command exit code.</param>
    /// <returns>The failed host command response.</returns>
    public static HostCommandForwardingResponse HostCommandFailed(string summary, string output, CliExitCode exitCode)
        => new(true, false, "host_command_failed", summary, exitCode, output);

    /// <summary>
    /// Creates an unavailable forwarding response.
    /// </summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="summary">The error summary.</param>
    /// <param name="exitCode">The CLI exit code.</param>
    /// <returns>The unavailable response.</returns>
    public static HostCommandForwardingResponse Unavailable(string code, string summary, CliExitCode exitCode)
        => new(false, false, code, summary, exitCode, string.Empty);
}
