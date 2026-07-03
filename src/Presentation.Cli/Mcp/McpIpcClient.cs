namespace BridgingIT.DevKit.Cli;

using System.IO.Pipes;
using System.Net.Sockets;
using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Sends MCP IPC requests to a selected local runtime.
/// </summary>
/// <example>
/// <code>
/// var response = await client.InvokeAsync(host, "mcp.capabilities", "diagnostics", arguments, ct);
/// </code>
/// </example>
public sealed class McpIpcClient
{
    private const int ProtocolVersion = 1;
    private static readonly JsonSerializerOptions WireJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Invokes an MCP operation in the selected host.
    /// </summary>
    /// <param name="host">The selected runtime.</param>
    /// <param name="operation">The operation name.</param>
    /// <param name="toolset">The required toolset.</param>
    /// <param name="arguments">The operation arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The MCP response.</returns>
    public async Task<McpResponse> InvokeAsync(
        HostRuntimeInfo host,
        string operation,
        string toolset,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        if (host.Descriptor.Features?.TryGetValue("mcp", out var endpoint) != true)
        {
            return RuntimeUnavailable("The selected runtime does not advertise MCP.");
        }

        if (endpoint.ProtocolVersion != ProtocolVersion)
        {
            return McpResponse.Unavailable(
                McpErrorCode.VersionMismatch,
                "The selected runtime MCP protocol is not compatible.",
                "Restart the runtime and bdk MCP server from the same build, then call bdk_runtimes_refresh.",
                [new McpNextCall("bdk_runtimes_refresh", new { })]);
        }

        if (!string.Equals(endpoint.Transport, "named-pipe", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(endpoint.Transport, "unix-socket", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeUnavailable($"Unsupported MCP transport '{endpoint.Transport}'.");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            if (string.Equals(endpoint.Transport, "unix-socket", StringComparison.OrdinalIgnoreCase))
            {
                using var socket = await ConnectUnixSocketAsync(endpoint.Endpoint, timeout.Token).ConfigureAwait(false);
                await using var stream = new NetworkStream(socket, ownsSocket: false);

                return await this.SendAsync(stream, endpoint, operation, toolset, arguments, timeout.Token).ConfigureAwait(false);
            }

            await using var pipe = new NamedPipeClientStream(".", endpoint.Endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);

            return await this.SendAsync(pipe, endpoint, operation, toolset, arguments, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return McpResponse.Unavailable(McpErrorCode.Timeout, "The MCP operation was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return McpResponse.Unavailable(
                McpErrorCode.Timeout,
                "The selected runtime did not answer the MCP operation before the timeout.",
                "Call bdk_mcp_self_test to verify runtime IPC health.",
                [new McpNextCall("bdk_mcp_self_test", new { })]);
        }
        catch (IOException exception)
        {
            return RuntimeUnavailable("The selected runtime MCP IPC endpoint is unavailable.", exception.Message);
        }
        catch (SocketException exception)
        {
            return RuntimeUnavailable("The selected runtime MCP IPC endpoint is unavailable.", exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return RuntimeUnavailable("The selected runtime MCP IPC endpoint rejected the connection.", exception.Message);
        }
        catch (JsonException exception)
        {
            return McpResponse.Unavailable(
                McpErrorCode.OperationFailed,
                "The selected runtime returned an invalid MCP IPC response.",
                exception.Message,
                [new McpNextCall("bdk_mcp_self_test", new { })]);
        }
    }

    private async Task<McpResponse> SendAsync(
        Stream stream,
        HostFeatureEndpointMetadata endpoint,
        string operation,
        string toolset,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(stream, leaveOpen: true);
        var request = new McpIpcRequest(endpoint.Nonce, ProtocolVersion, operation, toolset, arguments);
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, WireJsonOptions)).ConfigureAwait(false);

        var responseJson = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize<McpIpcResponse>(responseJson, WireJsonOptions);
        if (response is null)
        {
            return McpResponse.Unavailable(
                McpErrorCode.OperationFailed,
                "The selected runtime returned an empty MCP IPC response.",
                "Call bdk_mcp_self_test to verify runtime IPC health.",
                [new McpNextCall("bdk_mcp_self_test", new { })]);
        }

        if (response.ProtocolVersion != ProtocolVersion)
        {
            return McpResponse.Unavailable(
                McpErrorCode.VersionMismatch,
                "The selected runtime MCP IPC response version is not compatible.",
                "Restart the runtime and bdk MCP server from the same build, then call bdk_runtimes_refresh.",
                [new McpNextCall("bdk_runtimes_refresh", new { })]);
        }

        return response.Response ?? McpResponse.Unavailable(
            McpErrorCode.OperationFailed,
            response.Error ?? "The selected runtime returned no MCP response.",
            "Call bdk_capabilities_get to inspect operations advertised by the selected runtime.",
            [new McpNextCall("bdk_capabilities_get", new { })]);
    }

    private static async Task<Socket> ConnectUnixSocketAsync(string endpoint, CancellationToken cancellationToken)
    {
        var retryUntil = DateTimeOffset.UtcNow.AddSeconds(2);
        while (true)
        {
            if (!File.Exists(endpoint) && DateTimeOffset.UtcNow < retryUntil)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
                continue;
            }

            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint), cancellationToken).ConfigureAwait(false);

                return socket;
            }
            catch (SocketException) when (DateTimeOffset.UtcNow < retryUntil && !cancellationToken.IsCancellationRequested)
            {
                socket.Dispose();
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static McpResponse RuntimeUnavailable(string summary, string reason = null)
        => McpResponse.Unavailable(
            McpErrorCode.SelectedRuntimeUnavailable,
            summary,
            string.IsNullOrWhiteSpace(reason)
                ? "Call bdk_runtimes_refresh and then bdk_runtimes_list to inspect ready runtimes."
                : $"{reason} Call bdk_runtimes_refresh and then bdk_runtimes_list to inspect ready runtimes.",
            [new McpNextCall("bdk_runtimes_refresh", new { }), new McpNextCall("bdk_runtimes_list", new { })]);
}
