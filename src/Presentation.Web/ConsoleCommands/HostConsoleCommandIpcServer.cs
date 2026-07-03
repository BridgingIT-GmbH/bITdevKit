namespace BridgingIT.DevKit.Presentation.Web;

using System.IO.Pipes;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

/// <summary>
/// Hosts the local IPC endpoint used by future CLI Console Command forwarding.
/// </summary>
/// <example>
/// <code>
/// services.AddHostedService&lt;HostConsoleCommandIpcServer&gt;();
/// </code>
/// </example>
public sealed class HostConsoleCommandIpcServer(
    LocalIpcEndpointState endpoints,
    IServiceProvider services,
    ILogger<HostConsoleCommandIpcServer> logger) : BackgroundService
{
    private const int ProtocolVersion = 1;
    private const string LogKey = "BDK";
    private readonly HostFeatureEndpointMetadata endpoint = endpoints.GetOrCreate("consoleCommands");

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!services.GetServices<IConsoleCommand>().Any())
        {
            logger.LogDebug("[{LogKey}] console command IPC endpoint skipped, no commands registered", LogKey);

            return;
        }

        logger.LogDebug("[{LogKey}] console command IPC endpoint started (transport={Transport}, endpoint={Endpoint})", LogKey, this.endpoint.Transport, this.endpoint.Endpoint);

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
                logger.LogDebug(ex, "[{LogKey}] console command IPC connection failed (endpoint={Endpoint})", LogKey, this.endpoint.Endpoint);
            }
        }
    }

    private async Task HandleConnectionAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
        var requestJson = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        var request = string.IsNullOrWhiteSpace(requestJson) ? null : JsonSerializer.Deserialize<LocalConsoleCommandIpcRequest>(requestJson);

        if (request?.Nonce != this.endpoint.Nonce)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new LocalConsoleCommandIpcResponse(false, ProtocolVersion, 1, null, "Invalid nonce."))).ConfigureAwait(false);
            return;
        }

        if (request.ProtocolVersion != ProtocolVersion)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new LocalConsoleCommandIpcResponse(false, ProtocolVersion, 6, null, "Protocol version mismatch."))).ConfigureAwait(false);
            return;
        }

        if (string.Equals(request.Operation, "ping", StringComparison.OrdinalIgnoreCase))
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new LocalConsoleCommandIpcResponse(true, ProtocolVersion, 0, null, null))).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(request.Operation, "run", StringComparison.OrdinalIgnoreCase))
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new LocalConsoleCommandIpcResponse(false, ProtocolVersion, 1, null, "Unsupported operation."))).ConfigureAwait(false);
            return;
        }

        var commandTokens = request.RawTokens is { Length: > 0 }
            ? request.RawTokens
            : ConsoleCommandExecutor.SplitArgs(request.CommandLine ?? string.Empty);

        if (commandTokens.Length == 0)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new LocalConsoleCommandIpcResponse(false, ProtocolVersion, 1, null, "Command line is required."))).ConfigureAwait(false);
            return;
        }

        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Out = new TextWriterAnsiConsoleOutput(output, 120, 32)
        });
        var executor = services.GetService<ConsoleCommandExecutor>() ?? new ConsoleCommandExecutor();
        var result = await executor.ExecuteAsync(commandTokens, console, services, ConsoleCommandExecutionSource.Web, cancellationToken).ConfigureAwait(false);
        var exitCode = result.Succeeded ? 0 : 1;

        await writer.WriteLineAsync(JsonSerializer.Serialize(new LocalConsoleCommandIpcResponse(result.Succeeded, ProtocolVersion, exitCode, output.ToString(), result.Error))).ConfigureAwait(false);
    }
}
