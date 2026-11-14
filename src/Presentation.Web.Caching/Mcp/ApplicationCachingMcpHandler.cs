// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exposes bounded cache metadata and operational controls through the existing MCP discovery and dispatch contracts.</summary>
/// <param name="scopeFactory">The factory for a fresh host-authorized requester scope per tool call.</param>
/// <param name="configuration">The validated independent MCP exposure policy.</param>
/// <remarks>No tool invokes arbitrary sources, downloads bulk payloads, or accepts identity/action authority from JSON arguments.</remarks>
/// <example><code>services.AddApplicationCaching().WithMcpHandlers().WithManagementAccessContext&lt;OperationsAccessContextFactory&gt;();</code></example>
public sealed class ApplicationCachingMcpHandler(IServiceScopeFactory scopeFactory, CacheManagementConfiguration configuration) : IMcpHandler
{
    private static readonly IReadOnlyCollection<McpCapability> DeclaredCapabilities = CacheManagementOperations.Names.Select(operation =>
        new McpCapability("application-caching." + operation, Toolset(operation), "application.caching", "Bounded application cache " + operation + ".")
        {
            Category = CacheManagementOperations.RequiredAction(operation) == CacheAccessActions.Read ? "inspect" : "mutate",
            ArgumentSchema = new
            {
                type = "object", additionalProperties = false,
                properties = new Dictionary<string, object>
                {
                    ["collection"] = new { type = "string", maxLength = 128 },
                    ["collections"] = new { type = "array", maxItems = 32, uniqueItems = true, items = new { type = "string", maxLength = 128 } },
                    ["key"] = new { type = "string", maxLength = 4096 },
                    ["prefix"] = new { type = "string", maxLength = 4096 },
                    ["pattern"] = new { type = "string", maxLength = 4096 },
                    ["selectionJson"] = new { type = "string", maxLength = 65536 },
                    ["take"] = new { type = "integer", minimum = 1, maximum = 1000 },
                    ["cutoff"] = new { type = "string", format = "date-time" },
                    ["continuation"] = new { type = "string", maxLength = 100000 },
                    ["operationId"] = new { type = "string", pattern = "^[0-9a-fA-F]{32}$" },
                    ["background"] = new { type = "boolean" }
                }
            }
        }).ToArray();
    private static readonly HashSet<string> StringArguments = new(["collection", "key", "prefix", "pattern", "selectionJson", "continuation", "cutoff", "operationId"], StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyCollection<McpCapability> Capabilities => configuration.McpEnabled ? DeclaredCapabilities : [];

    /// <inheritdoc />
    public async ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
    {
        var capability = this.Capabilities.FirstOrDefault(value => value.Name.Equals(request.Operation, StringComparison.OrdinalIgnoreCase));
        if (capability is null) { return McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, "This application cache management tool is unavailable."); }

        if (!capability.Toolset.Equals(request.Toolset, StringComparison.OrdinalIgnoreCase))
        {
            return McpResponse.Failure(McpErrorCode.UnauthorizedToolset, "The requested toolset does not permit this cache action.");
        }

        try
        {
            ValidateArguments(request.Arguments);
            var selected = request.Arguments.EnumerateObject().Any(property => property.Name.Equals("collections", StringComparison.OrdinalIgnoreCase)) ?
                McpArgumentReader.GetStringArray(request.Arguments, "collections") : null;
            var cutoff = McpArgumentReader.GetDateTimeOffset(request.Arguments, "cutoff");
            if (McpArgumentReader.GetString(request.Arguments, "cutoff") is not null && !cutoff.HasValue) { throw new JsonException("Invalid original page cutoff."); }

            var arguments = new CacheManagementArguments(McpArgumentReader.GetString(request.Arguments, "collection"), selected,
                McpArgumentReader.GetString(request.Arguments, "key"), McpArgumentReader.GetString(request.Arguments, "prefix"),
                McpArgumentReader.GetString(request.Arguments, "pattern"), McpArgumentReader.GetString(request.Arguments, "selectionJson"),
                McpArgumentReader.GetInt32(request.Arguments, "take", 100) ?? 100, McpArgumentReader.GetString(request.Arguments, "continuation"),
                McpArgumentReader.GetString(request.Arguments, "operationId"),
                McpArgumentReader.GetBoolean(request.Arguments, "background", false) == true, cutoff);
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<CacheManagementDispatcher>();
            var result = await dispatcher.ExecuteAsync("Mcp", capability.Name["application-caching.".Length..], arguments, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return McpResponse.Failure(result.Errors.Any(error => error is UnauthorizedError) ? McpErrorCode.UnauthorizedToolset : McpErrorCode.OperationFailed,
                    "The cache management request failed.", string.Join(" ", result.Errors.Select(error => error.Message)));
            }

            return McpResponse.Success("Returned bounded cache management data.", result.Value);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException or FormatException)
        {
            return McpResponse.Failure(McpErrorCode.OperationFailed, "The cache management arguments are invalid.");
        }
    }

    private static string Toolset(string operation) => CacheManagementOperations.RequiredAction(operation) == CacheAccessActions.Read ? McpToolset.Diagnostics :
        operation is "clear" or "invalidate" ? McpToolset.Admin : McpToolset.Operations;

    private static void ValidateArguments(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object) { throw new JsonException("Arguments must be a bounded object."); }

        var count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in arguments.EnumerateObject())
        {
            if (++count > 11 || !seen.Add(property.Name)) { throw new JsonException("Too many management arguments."); }

            if (StringArguments.Contains(property.Name))
            {
                if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) { throw new JsonException("Expected a string argument."); }
            }
            else if (property.Name.Equals("collections", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind != JsonValueKind.Array || property.Value.GetArrayLength() > 32 ||
                    property.Value.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String)) { throw new JsonException("Invalid bounded target set."); }
            }
            else if (property.Name.Equals("take", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out _)) { throw new JsonException("Invalid page size."); }
            }
            else if (property.Name.Equals("background", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) { throw new JsonException("Invalid background flag."); }
            }
            else { throw new JsonException("Unknown management argument."); }
        }
    }
}