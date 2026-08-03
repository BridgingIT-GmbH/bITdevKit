// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides MCP operations for non-sensitive Document Storage diagnostics.
/// </summary>
/// <example>
/// <code>
/// services.AddDocumentStorage()
///     .WithInMemoryClient&lt;Person&gt;();
/// </code>
/// </example>
public sealed class DocumentStorageMcpHandler(IDocumentStorageDiagnosticsService diagnostics) : IMcpHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<McpCapability> Capabilities { get; } =
    [
        Capability("documents.summary", "Returns Document Storage registration and health summary."),
        Capability("documents.clients", "Lists registered Document Storage clients with provider capabilities and probe status."),
        Capability("documents.probe", "Returns non-mutating probe details for one registered Document Storage client.")
    ];

    /// <inheritdoc />
    public async ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
        => request.Operation switch
        {
            "documents.summary" => await this.SummaryAsync(cancellationToken).ConfigureAwait(false),
            "documents.clients" => await this.ClientsAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "documents.probe" => await this.ProbeAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            _ => McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Operation '{request.Operation}' is not handled by Document Storage.")
        };

    private async Task<McpResponse> SummaryAsync(CancellationToken cancellationToken)
    {
        var result = await diagnostics.CaptureAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Failure(result, "Document Storage diagnostics snapshot failed.");
        }

        var clients = result.Value.Clients;
        return McpResponse.Success(
            $"Document Storage has {clients.Count} registered client{(clients.Count == 1 ? string.Empty : "s")}.",
            new
            {
                summary = new
                {
                    ClientCount = clients.Count,
                    HealthyClientCount = clients.Count(client => client.IsHealthy),
                    UnhealthyClientCount = clients.Count(client => !client.IsHealthy)
                },
                unhealthyClients = clients.Where(client => !client.IsHealthy).Select(client => client.Name).ToArray()
            });
    }

    private async Task<McpResponse> ClientsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var result = await diagnostics.CaptureAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Failure(result, "Document Storage client diagnostics failed.");
        }

        var name = McpArgumentReader.GetString(arguments, "name", McpArgumentReader.GetString(arguments, "clientName"));
        var providerName = McpArgumentReader.GetString(arguments, "providerName");
        var healthy = McpArgumentReader.GetBoolean(arguments, "healthy");
        var clients = result.Value.Clients
            .Where(client => string.IsNullOrWhiteSpace(name) || string.Equals(client.Name, name, StringComparison.OrdinalIgnoreCase))
            .Where(client => string.IsNullOrWhiteSpace(providerName) || string.Equals(client.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))
            .Where(client => !healthy.HasValue || client.IsHealthy == healthy.Value)
            .OrderBy(client => client.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return McpResponse.Success($"Returned {clients.Length} Document Storage client{(clients.Length == 1 ? string.Empty : "s")}.", new { clients });
    }

    private async Task<McpResponse> ProbeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var name = McpArgumentReader.GetString(arguments, "name", McpArgumentReader.GetString(arguments, "clientName"));
        if (string.IsNullOrWhiteSpace(name))
        {
            return McpResponse.Unavailable(McpErrorCode.OperationFailed, "Document Storage client name is required.", "Supply the name argument.");
        }

        var result = await diagnostics.CaptureAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Failure(result, $"Document Storage probe for client '{name}' failed.");
        }

        var client = result.Value.Clients.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        return client is null
            ? McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Document Storage client '{name}' was not found.")
            : McpResponse.Success($"Document Storage client '{client.Name}' is {(client.IsHealthy ? "healthy" : "unhealthy")}.", new { client });
    }

    private static McpResponse Failure(IResult result, string summary)
        => McpResponse.Unavailable(
            McpErrorCode.OperationFailed,
            summary,
            string.Join("; ", result.Messages.Concat(result.Errors.Select(error => error.Message)).Where(message => !string.IsNullOrWhiteSpace(message)).DefaultIfEmpty("The Document Storage diagnostics operation failed.")));

    private static McpCapability Capability(string name, string description)
        => new(name, McpToolset.Diagnostics, "documents", description) { Owner = "bdk", Category = "inspect" };
}
