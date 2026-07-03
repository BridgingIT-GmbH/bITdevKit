namespace BridgingIT.DevKit.Presentation.Web;

using System.IO.Pipes;
using System.Net.Sockets;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosts the local MCP IPC endpoint for app-side MCP handlers.
/// </summary>
/// <example>
/// <code>
/// services.AddHostedService&lt;McpIpcServer&gt;();
/// </code>
/// </example>
public sealed class McpIpcServer(
    LocalIpcEndpointState endpoints,
    McpDispatcher dispatcher,
    ILogger<McpIpcServer> logger) : BackgroundService
{
    private const string LogKey = "BDK";
    private const int ProtocolVersion = 1;
    private static readonly JsonSerializerOptions WireJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HostFeatureEndpointMetadata endpoint = endpoints.GetOrCreate("mcp");

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("[{LogKey}] MCP IPC endpoint started (transport={Transport}, endpoint={Endpoint})", LogKey, this.endpoint.Transport, this.endpoint.Endpoint);

        if (string.Equals(this.endpoint.Transport, "unix-socket", StringComparison.OrdinalIgnoreCase))
        {
            await this.ExecuteUnixSocketAsync(stoppingToken).ConfigureAwait(false);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                this.endpoint.Endpoint,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                await HandleConnectionAsync(pipe, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[{LogKey}] MCP IPC connection failed (endpoint={Endpoint})", LogKey, this.endpoint.Endpoint);
            }
        }
    }

    private async Task ExecuteUnixSocketAsync(CancellationToken stoppingToken)
    {
        if (File.Exists(this.endpoint.Endpoint))
        {
            File.Delete(this.endpoint.Endpoint);
        }

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(this.endpoint.Endpoint));
        socket.Listen(1);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var connection = await socket.AcceptAsync(stoppingToken).ConfigureAwait(false);
                    await using var stream = new NetworkStream(connection, ownsSocket: false);
                    await this.HandleConnectionAsync(stream, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[{LogKey}] MCP Unix socket connection failed (endpoint={Endpoint})", LogKey, this.endpoint.Endpoint);
                }
            }
        }
        finally
        {
            if (File.Exists(this.endpoint.Endpoint))
            {
                File.Delete(this.endpoint.Endpoint);
            }
        }
    }

    private async Task HandleConnectionAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
        var requestJson = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        var response = await this.HandleRequestAsync(requestJson, cancellationToken).ConfigureAwait(false);

        await writer.WriteLineAsync(JsonSerializer.Serialize(response, WireJsonOptions)).ConfigureAwait(false);
    }

    private async ValueTask<McpIpcResponse> HandleRequestAsync(string requestJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return McpIpcResponse.Failure(McpErrorCode.OperationFailed, "The MCP IPC request was empty.", "Empty request.");
        }

        McpIpcRequest request;
        try
        {
            request = JsonSerializer.Deserialize<McpIpcRequest>(requestJson, WireJsonOptions);
        }
        catch (JsonException exception)
        {
            return McpIpcResponse.Failure(McpErrorCode.OperationFailed, "The MCP IPC request was invalid JSON.", exception.Message);
        }

        if (request is null)
        {
            return McpIpcResponse.Failure(McpErrorCode.OperationFailed, "The MCP IPC request was invalid.", "Request payload was null.");
        }

        if (!string.Equals(request.Nonce, this.endpoint.Nonce, StringComparison.Ordinal))
        {
            return McpIpcResponse.Failure(McpErrorCode.SelectedRuntimeUnavailable, "The MCP IPC nonce was rejected.", "Invalid nonce.");
        }

        if (request.ProtocolVersion != ProtocolVersion)
        {
            return McpIpcResponse.Failure(McpErrorCode.VersionMismatch, "The MCP IPC protocol version is not compatible.", "Unsupported protocol version.");
        }

        var response = await dispatcher.DispatchAsync(
            new McpRequest(request.Operation, request.Toolset, request.Arguments),
            cancellationToken).ConfigureAwait(false);

        return McpIpcResponse.Success(response);
    }
}
