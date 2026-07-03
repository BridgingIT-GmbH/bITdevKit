namespace BridgingIT.DevKit.Presentation.Web.Orchestrations;

using System.Text.Json;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides MCP operations for orchestration diagnostics, control, and maintenance.
/// </summary>
/// <example>
/// <code>
/// services.AddOrchestrations().AddMcpHandlers();
/// </code>
/// </example>
public sealed class OrchestrationMcpHandler(
    IOrchestrationQueryService queryService,
    IOrchestrationService orchestrationService,
    IOrchestrationAdministrationService administrationService) : IMcpHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<McpCapability> Capabilities { get; } =
    [
        Capability("orchestrations.list", McpToolset.Diagnostics, "Lists orchestration instances."),
        Capability("orchestrations.instanceDetails", McpToolset.Diagnostics, "Returns orchestration instance details."),
        Capability("orchestrations.history", McpToolset.Diagnostics, "Returns orchestration execution history."),
        Capability("orchestrations.signals", McpToolset.Diagnostics, "Returns persisted orchestration signals."),
        Capability("orchestrations.timers", McpToolset.Diagnostics, "Returns persisted orchestration timers."),
        Capability("orchestrations.signal", McpToolset.Operations, "Sends a signal to an orchestration."),
        Capability("orchestrations.pause", McpToolset.Operations, "Pauses an orchestration."),
        Capability("orchestrations.resume", McpToolset.Operations, "Resumes an orchestration."),
        Capability("orchestrations.cancel", McpToolset.Operations, "Cancels an orchestration."),
        Capability("orchestrations.terminate", McpToolset.Operations, "Terminates an orchestration."),
        Capability("orchestrations.repair", McpToolset.Operations, "Runs an orchestration repair operation."),
        Capability("orchestrations.purge", McpToolset.Admin, "Purges retained orchestration data."),
        Capability("investigate.orchestrationInstance", McpToolset.Diagnostics, "Aggregates diagnostics for an orchestration instance.")
    ];

    /// <inheritdoc />
    public async ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
        => request.Operation switch
        {
            "orchestrations.list" => await ListAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "orchestrations.instanceDetails" => await DetailsAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "orchestrations.history" => await HistoryAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "orchestrations.signals" => await SignalsAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "orchestrations.timers" => await TimersAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "orchestrations.signal" => await SignalAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "orchestrations.pause" => await PauseAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "orchestrations.resume" => await ResumeAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "orchestrations.cancel" => await CancelAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "orchestrations.terminate" => await TerminateAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "orchestrations.repair" => await RepairAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "orchestrations.purge" => await PurgeAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "investigate.orchestrationInstance" => await InvestigateAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            _ => McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Operation '{request.Operation}' is not handled by orchestrations.")
        };

    private async Task<McpResponse> ListAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var result = await queryService.QueryAsync(BuildQuery(arguments), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Failure("orchestrations.list", result.Messages, result.Errors);
        }

        var instances = result.Value.ToArray();

        return McpResponse.Success(
            $"Returned {instances.Length} orchestration instance{(instances.Length == 1 ? string.Empty : "s")}.",
            new
            {
                instances,
                result.TotalCount,
                result.CurrentPage,
                result.PageSize,
                result.TotalPages,
                result.HasNextPage
            },
            truncated: result.HasNextPage);
    }

    private async Task<McpResponse> DetailsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredInstanceId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var instance = await queryService.GetAsync(id.Value, cancellationToken).ConfigureAwait(false);
        if (instance.IsFailure)
        {
            return Failure("orchestrations.instanceDetails", instance.Messages, instance.Errors);
        }

        var context = await queryService.GetContextAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success(
            $"Returned orchestration instance '{id.Value}'.",
            new
            {
                instance = instance.Value,
                context = context.IsSuccess ? context.Value : null,
                contextErrors = context.IsFailure ? Describe(context.Messages, context.Errors) : []
            });
    }

    private async Task<McpResponse> HistoryAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredInstanceId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var result = await queryService.GetHistoryAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Failure("orchestrations.history", result.Messages, result.Errors)
            : McpResponse.Success($"Returned {result.Value.Count} history entr{(result.Value.Count == 1 ? "y" : "ies")} for orchestration '{id.Value}'.", new { history = result.Value });
    }

    private async Task<McpResponse> SignalsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredInstanceId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var result = await queryService.GetSignalsAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Failure("orchestrations.signals", result.Messages, result.Errors)
            : McpResponse.Success($"Returned {result.Value.Count} signal{(result.Value.Count == 1 ? string.Empty : "s")} for orchestration '{id.Value}'.", new { signals = result.Value });
    }

    private async Task<McpResponse> TimersAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredInstanceId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var result = await queryService.GetTimersAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Failure("orchestrations.timers", result.Messages, result.Errors)
            : McpResponse.Success($"Returned {result.Value.Count} timer{(result.Value.Count == 1 ? string.Empty : "s")} for orchestration '{id.Value}'.", new { timers = result.Value });
    }

    private async Task<McpResponse> SignalAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredInstanceId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var signalName = RequiredString(arguments, "signalName");
        if (signalName.Response is not null)
        {
            return signalName.Response;
        }

        var result = await orchestrationService.SignalAsync(
            id.Value,
            signalName.Value,
            McpArgumentReader.GetObject(arguments, "payload"),
            McpArgumentReader.GetString(arguments, "idempotencyKey"),
            cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Failure("orchestrations.signal", result.Messages, result.Errors)
            : McpResponse.Success($"Signaled orchestration '{id.Value}'.", new { instanceId = id.Value, signalName = signalName.Value });
    }

    private async Task<McpResponse> PauseAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredInstanceId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var result = await orchestrationService.PauseAsync(id.Value, McpArgumentReader.GetString(arguments, "reason"), cancellationToken).ConfigureAwait(false);

        return result.IsFailure ? Failure("orchestrations.pause", result.Messages, result.Errors) : McpResponse.Success($"Paused orchestration '{id.Value}'.", new { instanceId = id.Value });
    }

    private async Task<McpResponse> ResumeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredInstanceId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var result = await orchestrationService.ResumeAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return result.IsFailure ? Failure("orchestrations.resume", result.Messages, result.Errors) : McpResponse.Success($"Resumed orchestration '{id.Value}'.", new { instanceId = id.Value });
    }

    private async Task<McpResponse> CancelAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredInstanceId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var result = await orchestrationService.CancelAsync(id.Value, McpArgumentReader.GetString(arguments, "reason"), cancellationToken).ConfigureAwait(false);

        return result.IsFailure ? Failure("orchestrations.cancel", result.Messages, result.Errors) : McpResponse.Success($"Cancelled orchestration '{id.Value}'.", new { instanceId = id.Value });
    }

    private async Task<McpResponse> TerminateAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredInstanceId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var result = await orchestrationService.TerminateAsync(id.Value, McpArgumentReader.GetString(arguments, "reason"), cancellationToken).ConfigureAwait(false);

        return result.IsFailure ? Failure("orchestrations.terminate", result.Messages, result.Errors) : McpResponse.Success($"Terminated orchestration '{id.Value}'.", new { instanceId = id.Value });
    }

    private async Task<McpResponse> RepairAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredInstanceId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var action = McpArgumentReader.GetString(arguments, "action");
        var result = action switch
        {
            "archive" => await administrationService.ArchiveAsync(id.Value, cancellationToken).ConfigureAwait(false),
            "releaseLease" => await administrationService.ReleaseLeaseAsync(id.Value, cancellationToken).ConfigureAwait(false),
            "requeueTimers" => await administrationService.RequeueTimersAsync(id.Value, cancellationToken).ConfigureAwait(false),
            _ => Result<string>.Failure("Unsupported repair action. Use archive, releaseLease, or requeueTimers.")
        };

        return result.IsFailure
            ? Failure("orchestrations.repair", result.Messages, result.Errors)
            : McpResponse.Success($"Completed orchestration repair action '{action}'.", new { instanceId = id.Value, action, message = result.Value });
    }

    private async Task<McpResponse> PurgeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!HasConfirmation(arguments, "purge orchestrations"))
        {
            return McpResponse.Unavailable("confirmation_required", "Orchestration purge requires confirmation.", "Set confirm=true and confirmation='purge orchestrations'.");
        }

        var result = await administrationService.PurgeAsync(new OrchestrationPurgeRequest
        {
            OlderThan = McpArgumentReader.GetDateTimeOffset(arguments, "olderThan"),
            Statuses = McpArgumentReader.GetStringArray(arguments, "statuses"),
            IsArchived = McpArgumentReader.GetBoolean(arguments, "isArchived")
        }, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Failure("orchestrations.purge", result.Messages, result.Errors)
            : McpResponse.Success("Purged retained orchestration data.", new { result = result.Value });
    }

    private async Task<McpResponse> InvestigateAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var details = await DetailsAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (!details.Available)
        {
            return details;
        }

        var id = RequiredInstanceId(arguments).Value;
        var history = await queryService.GetHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        var signals = await queryService.GetSignalsAsync(id, cancellationToken).ConfigureAwait(false);
        var timers = await queryService.GetTimersAsync(id, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success(
            $"Aggregated diagnostics for orchestration '{id}'.",
            new
            {
                details = details.Data,
                history = history.IsSuccess ? history.Value : [],
                signals = signals.IsSuccess ? signals.Value : [],
                timers = timers.IsSuccess ? timers.Value : []
            });
    }

    private static OrchestrationQueryRequest BuildQuery(JsonElement arguments)
        => new()
        {
            OrchestrationName = McpArgumentReader.GetString(arguments, "orchestrationName"),
            Statuses = McpArgumentReader.GetStringArray(arguments, "statuses"),
            States = McpArgumentReader.GetStringArray(arguments, "states"),
            CorrelationId = McpArgumentReader.GetString(arguments, "correlationId"),
            ConcurrencyKey = McpArgumentReader.GetString(arguments, "concurrencyKey"),
            StartedFrom = McpArgumentReader.GetDateTimeOffset(arguments, "startedFrom"),
            StartedTo = McpArgumentReader.GetDateTimeOffset(arguments, "startedTo"),
            CompletedFrom = McpArgumentReader.GetDateTimeOffset(arguments, "completedFrom"),
            CompletedTo = McpArgumentReader.GetDateTimeOffset(arguments, "completedTo"),
            Skip = McpArgumentReader.GetInt32(arguments, "skip", 0) ?? 0,
            Take = Math.Min(McpArgumentReader.GetInt32(arguments, "take", 50) ?? 50, 500),
            SortBy = McpArgumentReader.GetString(arguments, "sortBy", "StartedUtc"),
            SortDescending = McpArgumentReader.GetBoolean(arguments, "sortDescending", true) == true
        };

    private static RequiredGuid RequiredInstanceId(JsonElement arguments)
    {
        var id = McpArgumentReader.GetGuid(arguments, "instanceId") ?? McpArgumentReader.GetGuid(arguments, "id");

        return id.HasValue
            ? new RequiredGuid(id.Value, null)
            : new RequiredGuid(Guid.Empty, McpResponse.Unavailable(McpErrorCode.OperationFailed, "instanceId is required."));
    }

    private static RequiredText RequiredString(JsonElement arguments, string name)
    {
        var value = McpArgumentReader.GetString(arguments, name);

        return string.IsNullOrWhiteSpace(value)
            ? new RequiredText(null, McpResponse.Unavailable(McpErrorCode.OperationFailed, $"{name} is required."))
            : new RequiredText(value, null);
    }

    private static McpResponse Failure(string operation, IReadOnlyList<string> messages, IReadOnlyList<IResultError> errors)
        => McpResponse.Failure(
            McpErrorCode.OperationFailed,
            $"MCP operation '{operation}' failed.",
            string.Join("; ", Describe(messages, errors)));

    private static string[] Describe(IReadOnlyList<string> messages, IReadOnlyList<IResultError> errors)
        => messages.Concat(errors.Select(error => error.Message)).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();

    private static bool HasConfirmation(JsonElement arguments, string confirmation)
        => McpArgumentReader.GetBoolean(arguments, "confirm") == true &&
            string.Equals(McpArgumentReader.GetString(arguments, "confirmation"), confirmation, StringComparison.OrdinalIgnoreCase);

    private static McpCapability Capability(string name, string toolset, string description)
        => new(name, toolset, "orchestrations", description) { Owner = "bdk", Category = toolset == McpToolset.Diagnostics ? "inspect" : "operate" };

    private sealed record RequiredGuid(Guid Value, McpResponse Response);

    private sealed record RequiredText(string Value, McpResponse Response);
}
