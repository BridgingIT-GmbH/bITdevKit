namespace BridgingIT.DevKit.Cli;

using System.Reflection;
using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Hosts the bdk MCP server over newline-delimited JSON-RPC STDIO.
/// </summary>
/// <example>
/// <code>
/// await server.RunAsync(Console.In, Console.Out, Console.Error, options, ct);
/// </code>
/// </example>
public sealed class StdioMcpServer(McpToolCatalog catalog, McpToolExecutor executor)
{
    private const string ProtocolVersion = "2025-06-18";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Runs the STDIO MCP server until the input stream closes or cancellation is requested.
    /// </summary>
    /// <param name="input">The input reader.</param>
    /// <param name="output">The protocol output writer.</param>
    /// <param name="error">The diagnostic error writer.</param>
    /// <param name="options">The MCP options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The process exit code.</returns>
    public async Task<int> RunAsync(
        TextReader input,
        TextWriter output,
        TextWriter error,
        McpCliOptions options,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await this.HandleLineAsync(line, output, error, options, cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }

    private async Task HandleLineAsync(
        string line,
        TextWriter output,
        TextWriter error,
        McpCliOptions options,
        CancellationToken cancellationToken)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException exception)
        {
            await WriteErrorAsync(output, null, -32700, "Parse error", exception.Message, cancellationToken).ConfigureAwait(false);
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String)
            {
                await WriteErrorAsync(output, TryGetId(root), -32600, "Invalid Request", "JSON-RPC method is required.", cancellationToken).ConfigureAwait(false);
                return;
            }

            var id = TryGetId(root);
            var method = methodElement.GetString();
            if (method?.StartsWith("notifications/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return;
            }

            try
            {
                switch (method)
                {
                    case "initialize":
                        await WriteResultAsync(output, id, CreateInitializeResult(), cancellationToken).ConfigureAwait(false);
                        return;
                    case "tools/list":
                        await WriteResultAsync(output, id, new
                        {
                            tools = catalog.Tools.Select(tool => new
                            {
                                name = tool.Name,
                                description = tool.Description,
                                inputSchema = tool.InputSchema
                            })
                        }, cancellationToken).ConfigureAwait(false);
                        return;
                    case "tools/call":
                        await this.HandleToolCallAsync(root, output, id, options, cancellationToken).ConfigureAwait(false);
                        return;
                    default:
                        await WriteErrorAsync(output, id, -32601, "Method not found", $"Method '{method}' is not supported.", cancellationToken).ConfigureAwait(false);
                        return;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (options.Verbose)
                {
                    await error.WriteLineAsync(exception.ToString()).ConfigureAwait(false);
                }

                await WriteErrorAsync(output, id, -32603, "Internal error", "The bdk MCP server failed to handle the request.", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleToolCallAsync(
        JsonElement root,
        TextWriter output,
        JsonElement? id,
        McpCliOptions options,
        CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("params", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            await WriteErrorAsync(output, id, -32602, "Invalid params", "tools/call requires params.name.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var name = nameElement.GetString();
        var arguments = parameters.TryGetProperty("arguments", out var argumentElement) && argumentElement.ValueKind == JsonValueKind.Object
            ? argumentElement.Clone()
            : McpJson.EmptyObject();
        var response = await executor.ExecuteAsync(name, arguments, options, cancellationToken).ConfigureAwait(false);
        var responseJson = JsonSerializer.Serialize(response, JsonOptions);

        await WriteResultAsync(output, id, new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = responseJson
                }
            },
            structuredContent = response,
            isError = !response.Available
        }, cancellationToken).ConfigureAwait(false);
    }

    private static object CreateInitializeResult()
        => new
        {
            protocolVersion = ProtocolVersion,
            capabilities = new
            {
                tools = new
                {
                    listChanged = false
                }
            },
            serverInfo = new
            {
                name = "bdk",
                version = typeof(CliApplication).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
                    typeof(CliApplication).Assembly.GetName().Version?.ToString()
            }
        };

    private static JsonElement? TryGetId(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("id", out var id))
        {
            return id.Clone();
        }

        return null;
    }

    private static async Task WriteResultAsync(TextWriter output, JsonElement? id, object result, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return;
        }

        await output.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = id.Value,
            result
        }, JsonOptions).AsMemory(), cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteErrorAsync(TextWriter output, JsonElement? id, int code, string message, string data, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            error = new
            {
                code,
                message,
                data
            }
        }, JsonOptions).AsMemory(), cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
