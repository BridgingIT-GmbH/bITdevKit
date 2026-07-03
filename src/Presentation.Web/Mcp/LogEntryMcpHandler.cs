namespace BridgingIT.DevKit.Presentation.Web;

using System.Text.Json;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides MCP diagnostics operations for retained application logs.
/// </summary>
/// <example>
/// <code>
/// services.AddMcpHandler&lt;LogEntryMcpHandler&gt;();
/// </code>
/// </example>
public sealed class LogEntryMcpHandler(IServiceProvider services) : IMcpHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<McpCapability> Capabilities { get; } =
    [
        Capability("logs.query", McpToolset.Diagnostics, "Queries retained log entries."),
        Capability("logs.tail", McpToolset.Diagnostics, "Returns the newest retained log entries."),
        Capability("logs.purge", McpToolset.Admin, "Purges retained log entries."),
        Capability("errors.recent", McpToolset.Diagnostics, "Returns recent error log entries."),
        Capability("errors.details", McpToolset.Diagnostics, "Returns a selected error log entry."),
        Capability("correlation.inspect", McpToolset.Diagnostics, "Returns log entries for a correlation id."),
        Capability("investigate.recentErrors", McpToolset.Diagnostics, "Aggregates recent error diagnostics."),
        Capability("investigate.correlation", McpToolset.Diagnostics, "Aggregates diagnostics for a correlation id.")
    ];

    /// <inheritdoc />
    public async ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
    {
        var service = services.GetService<ILogEntryService>();
        if (service is null)
        {
            return McpResponse.Unavailable(
                McpErrorCode.FeatureUnavailable,
                "Retained log diagnostics are not available in the selected runtime.",
                "Register an ILogEntryService-backed logging store to enable log MCP tools.");
        }

        return request.Operation switch
        {
            "logs.query" => await QueryAsync(service, request.Arguments, cancellationToken).ConfigureAwait(false),
            "logs.tail" => await QueryAsync(service, request.Arguments, cancellationToken, "tail").ConfigureAwait(false),
            "logs.purge" => await PurgeAsync(service, request.Arguments, cancellationToken).ConfigureAwait(false),
            "errors.recent" => await QueryAsync(service, request.Arguments, cancellationToken, "errors").ConfigureAwait(false),
            "errors.details" => await ErrorDetailsAsync(service, request.Arguments, cancellationToken).ConfigureAwait(false),
            "correlation.inspect" or "investigate.correlation" => await CorrelationAsync(service, request.Arguments, cancellationToken).ConfigureAwait(false),
            "investigate.recentErrors" => await RecentErrorsAsync(service, request.Arguments, cancellationToken).ConfigureAwait(false),
            _ => McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Operation '{request.Operation}' is not handled by log diagnostics.")
        };
    }

    private static async Task<McpResponse> QueryAsync(ILogEntryService service, JsonElement arguments, CancellationToken cancellationToken, string operationHint = null)
    {
        var request = BuildQueryRequest(arguments, operationHint);
        var response = await service.QueryAsync(request, cancellationToken).ConfigureAwait(false);
        var count = response.Items?.Count ?? 0;

        return McpResponse.Success(
            $"Returned {count} log entr{(count == 1 ? "y" : "ies")}.",
            new
            {
                response.Items,
                response.ContinuationToken,
                response.PageSize
            },
            truncated: !string.IsNullOrWhiteSpace(response.ContinuationToken));
    }

    private static async Task<McpResponse> ErrorDetailsAsync(ILogEntryService service, JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = McpArgumentReader.GetInt64(arguments, "id");
        if (!id.HasValue)
        {
            return McpResponse.Unavailable(McpErrorCode.OperationFailed, "id is required.");
        }

        var response = await service.QueryAsync(new LogEntryQueryRequest { AfterId = id.Value - 1, PageSize = 25 }, cancellationToken).ConfigureAwait(false);
        var entry = response.Items?.FirstOrDefault(item => item.Id == id.Value);

        return entry is null
            ? McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Error log entry {id.Value} was not found.")
            : McpResponse.Success($"Returned error log entry {id.Value}.", new { entry });
    }

    private static async Task<McpResponse> CorrelationAsync(ILogEntryService service, JsonElement arguments, CancellationToken cancellationToken)
    {
        var correlationId = McpArgumentReader.GetString(arguments, "correlationId");
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return McpResponse.Unavailable(McpErrorCode.OperationFailed, "correlationId is required.");
        }

        var request = BuildQueryRequest(arguments);
        request.CorrelationId = correlationId;
        request.PageSize = Math.Min(request.PageSize, 200);
        var response = await service.QueryAsync(request, cancellationToken).ConfigureAwait(false);
        var errors = response.Items?.Where(item => string.Equals(item.Level, "Error", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(item.Exception)).ToArray() ?? [];

        return McpResponse.Success(
            $"Correlation '{correlationId}' returned {response.Items.Count} log entr{(response.Items.Count == 1 ? "y" : "ies")} and {errors.Length} error entr{(errors.Length == 1 ? "y" : "ies")}.",
            new
            {
                correlationId,
                logs = response.Items,
                errors,
                response.ContinuationToken
            },
            truncated: !string.IsNullOrWhiteSpace(response.ContinuationToken));
    }

    private static async Task<McpResponse> RecentErrorsAsync(ILogEntryService service, JsonElement arguments, CancellationToken cancellationToken)
    {
        var response = await service.QueryAsync(BuildQueryRequest(arguments, "errors"), cancellationToken).ConfigureAwait(false);

        return McpResponse.Success(
            $"Found {response.Items.Count} recent error log entr{(response.Items.Count == 1 ? "y" : "ies")}.",
            new
            {
                errors = response.Items,
                response.ContinuationToken
            },
            truncated: !string.IsNullOrWhiteSpace(response.ContinuationToken));
    }

    private static async Task<McpResponse> PurgeAsync(ILogEntryService service, JsonElement arguments, CancellationToken cancellationToken)
    {
        var olderThan = McpArgumentReader.GetDateTimeOffset(arguments, "olderThan");
        if (!olderThan.HasValue)
        {
            return McpResponse.Unavailable(McpErrorCode.OperationFailed, "olderThan is required.");
        }

        if (!HasConfirmation(arguments, "purge logs"))
        {
            return McpResponse.Unavailable("confirmation_required", "Log purge requires confirmation.", "Set confirm=true and confirmation='purge logs'.");
        }

        await service.CleanupAsync(
            olderThan.Value,
            McpArgumentReader.GetBoolean(arguments, "archive", false) == true,
            McpArgumentReader.GetInt32(arguments, "batchSize", 1000) ?? 1000,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return McpResponse.Success("Log purge completed.", new { olderThan, confirmed = true });
    }

    private static LogEntryQueryRequest BuildQueryRequest(JsonElement arguments, string operationHint = null)
    {
        var request = new LogEntryQueryRequest
        {
            StartTime = McpArgumentReader.GetDateTimeOffset(arguments, "startTime"),
            EndTime = McpArgumentReader.GetDateTimeOffset(arguments, "endTime"),
            TraceId = McpArgumentReader.GetString(arguments, "traceId"),
            SpanId = McpArgumentReader.GetString(arguments, "spanId"),
            CorrelationId = McpArgumentReader.GetString(arguments, "correlationId"),
            LogKey = McpArgumentReader.GetString(arguments, "logKey"),
            ModuleName = McpArgumentReader.GetString(arguments, "moduleName"),
            ShortTypeName = McpArgumentReader.GetString(arguments, "shortTypeName"),
            SearchText = McpArgumentReader.GetString(arguments, "searchText"),
            ContinuationToken = McpArgumentReader.GetString(arguments, "continuationToken"),
            AfterId = McpArgumentReader.GetInt64(arguments, "afterId"),
            PageSize = Math.Min(McpArgumentReader.GetInt32(arguments, "take", McpArgumentReader.GetInt32(arguments, "pageSize", 100)) ?? 100, 500)
        };

        var level = McpArgumentReader.GetString(arguments, "level");
        if (Enum.TryParse<LogLevel>(level, ignoreCase: true, out var parsedLevel))
        {
            request.Level = parsedLevel;
        }

        var ageSeconds = McpArgumentReader.GetInt32(arguments, "ageSeconds");
        if (ageSeconds.HasValue)
        {
            request.Age = TimeSpan.FromSeconds(ageSeconds.Value);
        }

        operationHint ??= McpArgumentReader.GetString(arguments, "operationHint");

        if (request.StartTime is null && request.Age is null && string.Equals(operationHint, "tail", StringComparison.OrdinalIgnoreCase))
        {
            request.Age = TimeSpan.FromMinutes(15);
        }

        if (string.Equals(operationHint, "errors", StringComparison.OrdinalIgnoreCase))
        {
            request.Level = LogLevel.Error;
        }

        request.Validate();

        return request;
    }

    private static bool HasConfirmation(JsonElement arguments, string confirmation)
        => McpArgumentReader.GetBoolean(arguments, "confirm") == true &&
            string.Equals(McpArgumentReader.GetString(arguments, "confirmation"), confirmation, StringComparison.OrdinalIgnoreCase);

    private static McpCapability Capability(string name, string toolset, string description)
        => new(name, toolset, "logs", description) { Owner = "bdk", Category = toolset == McpToolset.Diagnostics ? "inspect" : "admin" };
}
