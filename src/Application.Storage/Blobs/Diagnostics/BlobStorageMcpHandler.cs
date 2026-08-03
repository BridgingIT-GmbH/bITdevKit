// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides MCP operations for provider-neutral Blob Storage diagnostics.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
public sealed class BlobStorageMcpHandler(IBlobStorageDiagnosticsService diagnostics) : IMcpHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<McpCapability> Capabilities { get; } =
    [
        Capability("blobs.summary", McpToolset.Diagnostics, "Returns Blob Storage registration and health summary."),
        Capability("blobs.clients", McpToolset.Diagnostics, "Lists registered Blob Storage clients with provider capabilities and probe status."),
        Capability("blobs.probe", McpToolset.Diagnostics, "Returns non-mutating probe details for one registered Blob Storage client.")
    ];

    /// <inheritdoc />
    public async ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
        => request.Operation switch
        {
            "blobs.summary" => await this.SummaryAsync(cancellationToken).ConfigureAwait(false),
            "blobs.clients" => await this.ClientsAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "blobs.probe" => await this.ProbeAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            _ => McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Operation '{request.Operation}' is not handled by Blob Storage.")
        };

    private async Task<McpResponse> SummaryAsync(CancellationToken cancellationToken)
    {
        var result = await diagnostics.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Failure(result, "Blob Storage diagnostics snapshot failed.");
        }

        var snapshot = result.Value;

        return McpResponse.Success(
            $"Blob Storage has {snapshot.ClientCount} registered client{(snapshot.ClientCount == 1 ? string.Empty : "s")}.",
            new
            {
                summary = new
                {
                    snapshot.ClientCount,
                    snapshot.HealthyClientCount,
                    snapshot.UnhealthyClientCount
                },
                unhealthyClients = snapshot.Clients
                    .Where(client => !client.IsHealthy)
                    .Select(client => client.Name)
                    .ToArray()
            });
    }

    private async Task<McpResponse> ClientsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var result = await diagnostics.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Failure(result, "Blob Storage client diagnostics failed.");
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

        return McpResponse.Success(
            $"Returned {clients.Length} Blob Storage client{(clients.Length == 1 ? string.Empty : "s")}.",
            new
            {
                clients,
                filters = new { name, providerName, healthy }
            });
    }

    private async Task<McpResponse> ProbeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var name = McpArgumentReader.GetString(arguments, "name", McpArgumentReader.GetString(arguments, "clientName"));
        if (string.IsNullOrWhiteSpace(name))
        {
            return McpResponse.Unavailable(McpErrorCode.OperationFailed, "Blob Storage client name is required.", "Supply the name argument.");
        }

        var result = await diagnostics.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Failure(result, $"Blob Storage probe for client '{name}' failed.");
        }

        var client = result.Value.Clients.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        return client is null
            ? McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Blob Storage client '{name}' was not found.")
            : McpResponse.Success(
                $"Blob Storage client '{client.Name}' is {client.HealthStatus}.",
                new { client });
    }

    private static McpResponse Failure(IResult result, string summary)
        => McpResponse.Unavailable(McpErrorCode.OperationFailed, summary, string.Join("; ", FailureMessages(result)));

    private static IEnumerable<string> FailureMessages(IResult result)
    {
        if (result is null || result.IsSuccess)
        {
            return [];
        }

        return result.Messages
            .Concat(result.Errors.Select(error => error.Message))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .DefaultIfEmpty("The Blob Storage diagnostics operation failed.");
    }

    private static McpCapability Capability(string name, string toolset, string description)
        => new(name, toolset, "blobs", description) { Owner = "bdk", Category = "inspect" };
}
