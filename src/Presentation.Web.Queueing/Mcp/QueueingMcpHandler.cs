namespace BridgingIT.DevKit.Presentation.Web.Queueing;

using System.Text.Json;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides MCP operations for queue broker diagnostics and control.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing().AddMcpHandlers();
/// </code>
/// </example>
public sealed class QueueingMcpHandler(IQueueBrokerService broker) : IMcpHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<McpCapability> Capabilities { get; } =
    [
        Capability("queueing.summary", McpToolset.Diagnostics, "Returns queue broker runtime summary."),
        Capability("queueing.subscriptions", McpToolset.Diagnostics, "Lists queue subscriptions."),
        Capability("queueing.waiting", McpToolset.Diagnostics, "Lists queue messages waiting for handlers."),
        Capability("queueing.messages", McpToolset.Diagnostics, "Lists retained queue messages."),
        Capability("queueing.messageDetails", McpToolset.Diagnostics, "Returns queue message details."),
        Capability("queueing.retry", McpToolset.Operations, "Retries a queue message."),
        Capability("queueing.releaseLease", McpToolset.Operations, "Releases a queue message lease."),
        Capability("queueing.archive", McpToolset.Operations, "Archives a queue message."),
        Capability("queueing.pauseQueue", McpToolset.Operations, "Pauses processing for a logical queue."),
        Capability("queueing.resumeQueue", McpToolset.Operations, "Resumes processing for a logical queue."),
        Capability("queueing.pauseType", McpToolset.Operations, "Pauses processing for a queue message type."),
        Capability("queueing.resumeType", McpToolset.Operations, "Resumes processing for a queue message type."),
        Capability("queueing.purge", McpToolset.Admin, "Purges retained queue messages.")
    ];

    /// <inheritdoc />
    public async ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
        => request.Operation switch
        {
            "queueing.summary" => await SummaryAsync(cancellationToken).ConfigureAwait(false),
            "queueing.subscriptions" => await SubscriptionsAsync(cancellationToken).ConfigureAwait(false),
            "queueing.waiting" => await WaitingAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "queueing.messages" => await ListAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "queueing.messageDetails" => await DetailsAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "queueing.retry" => await RetryAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "queueing.releaseLease" => await ReleaseLeaseAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "queueing.archive" => await ArchiveAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "queueing.pauseQueue" => await PauseQueueAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "queueing.resumeQueue" => await ResumeQueueAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "queueing.pauseType" => await PauseTypeAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "queueing.resumeType" => await ResumeTypeAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "queueing.purge" => await PurgeAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            _ => McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Operation '{request.Operation}' is not handled by queueing.")
        };

    private async Task<McpResponse> SummaryAsync(CancellationToken cancellationToken)
    {
        var summary = await broker.GetSummaryAsync(cancellationToken).ConfigureAwait(false);

        return McpResponse.Success("Returned queue broker summary.", new { summary });
    }

    private async Task<McpResponse> SubscriptionsAsync(CancellationToken cancellationToken)
    {
        var subscriptions = (await broker.GetSubscriptionsAsync(cancellationToken).ConfigureAwait(false)).ToArray();

        return McpResponse.Success($"Returned {subscriptions.Length} queue subscription{(subscriptions.Length == 1 ? string.Empty : "s")}.", new { subscriptions });
    }

    private async Task<McpResponse> WaitingAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var messages = (await broker.GetWaitingMessagesAsync(Math.Min(McpArgumentReader.GetInt32(arguments, "take", 50) ?? 50, 500), cancellationToken).ConfigureAwait(false)).ToArray();

        return McpResponse.Success($"Returned {messages.Length} waiting queue message{(messages.Length == 1 ? string.Empty : "s")}.", new { messages });
    }

    private async Task<McpResponse> ListAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var messages = (await broker.GetMessagesAsync(
            McpArgumentReader.GetEnum<QueueMessageStatus>(arguments, "status"),
            McpArgumentReader.GetString(arguments, "type"),
            McpArgumentReader.GetString(arguments, "queueName"),
            McpArgumentReader.GetString(arguments, "messageId"),
            McpArgumentReader.GetString(arguments, "lockedBy"),
            McpArgumentReader.GetBoolean(arguments, "isArchived", false),
            McpArgumentReader.GetDateTimeOffset(arguments, "createdAfter"),
            McpArgumentReader.GetDateTimeOffset(arguments, "createdBefore"),
            Math.Min(McpArgumentReader.GetInt32(arguments, "take", 50) ?? 50, 500),
            cancellationToken).ConfigureAwait(false)).ToArray();

        return McpResponse.Success($"Returned {messages.Length} queue message{(messages.Length == 1 ? string.Empty : "s")}.", new { messages });
    }

    private async Task<McpResponse> DetailsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var message = await broker.GetMessageAsync(id.Value, cancellationToken).ConfigureAwait(false);
        var includeContent = McpArgumentReader.GetBoolean(arguments, "includeContent", false) == true;
        var content = includeContent ? await broker.GetMessageContentAsync(id.Value, cancellationToken).ConfigureAwait(false) : null;

        return message is null
            ? McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Queue message '{id.Value}' was not found.")
            : McpResponse.Success($"Returned queue message '{id.Value}'.", new { message, content });
    }

    private async Task<McpResponse> RetryAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        await broker.RetryMessageAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Retried queue message '{id.Value}'.", new { id = id.Value });
    }

    private async Task<McpResponse> ReleaseLeaseAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        await broker.ReleaseLeaseAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Released queue message lease '{id.Value}'.", new { id = id.Value });
    }

    private async Task<McpResponse> ArchiveAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        await broker.ArchiveMessageAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Archived queue message '{id.Value}'.", new { id = id.Value });
    }

    private async Task<McpResponse> PauseQueueAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var queueName = RequiredString(arguments, "queueName");
        if (queueName.Response is not null)
        {
            return queueName.Response;
        }

        await broker.PauseQueueAsync(queueName.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Paused queue '{queueName.Value}'.", new { queueName = queueName.Value });
    }

    private async Task<McpResponse> ResumeQueueAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var queueName = RequiredString(arguments, "queueName");
        if (queueName.Response is not null)
        {
            return queueName.Response;
        }

        await broker.ResumeQueueAsync(queueName.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Resumed queue '{queueName.Value}'.", new { queueName = queueName.Value });
    }

    private async Task<McpResponse> PauseTypeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var type = RequiredString(arguments, "type");
        if (type.Response is not null)
        {
            return type.Response;
        }

        await broker.PauseMessageTypeAsync(type.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Paused queue message type '{type.Value}'.", new { type = type.Value });
    }

    private async Task<McpResponse> ResumeTypeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var type = RequiredString(arguments, "type");
        if (type.Response is not null)
        {
            return type.Response;
        }

        await broker.ResumeMessageTypeAsync(type.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Resumed queue message type '{type.Value}'.", new { type = type.Value });
    }

    private async Task<McpResponse> PurgeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!HasConfirmation(arguments, "purge queue messages"))
        {
            return McpResponse.Unavailable("confirmation_required", "Queue message purge requires confirmation.", "Set confirm=true and confirmation='purge queue messages'.");
        }

        await broker.PurgeMessagesAsync(
            McpArgumentReader.GetDateTimeOffset(arguments, "olderThan"),
            McpArgumentReader.GetEnumArray<QueueMessageStatus>(arguments, "statuses"),
            McpArgumentReader.GetBoolean(arguments, "isArchived"),
            cancellationToken).ConfigureAwait(false);

        return McpResponse.Success("Purged retained queue messages.", new
        {
            olderThan = McpArgumentReader.GetDateTimeOffset(arguments, "olderThan"),
            statuses = McpArgumentReader.GetStringArray(arguments, "statuses"),
            isArchived = McpArgumentReader.GetBoolean(arguments, "isArchived")
        });
    }

    private static RequiredGuid RequiredId(JsonElement arguments)
    {
        var id = McpArgumentReader.GetGuid(arguments, "id");

        return id.HasValue
            ? new RequiredGuid(id.Value, null)
            : new RequiredGuid(Guid.Empty, McpResponse.Unavailable(McpErrorCode.OperationFailed, "id is required."));
    }

    private static RequiredText RequiredString(JsonElement arguments, string name)
    {
        var value = McpArgumentReader.GetString(arguments, name);

        return string.IsNullOrWhiteSpace(value)
            ? new RequiredText(null, McpResponse.Unavailable(McpErrorCode.OperationFailed, $"{name} is required."))
            : new RequiredText(value, null);
    }

    private static bool HasConfirmation(JsonElement arguments, string confirmation)
        => McpArgumentReader.GetBoolean(arguments, "confirm") == true &&
            string.Equals(McpArgumentReader.GetString(arguments, "confirmation"), confirmation, StringComparison.OrdinalIgnoreCase);

    private static McpCapability Capability(string name, string toolset, string description)
        => new(name, toolset, "queueing", description) { Owner = "bdk", Category = toolset == McpToolset.Diagnostics ? "inspect" : "operate" };

    private sealed record RequiredGuid(Guid Value, McpResponse Response);

    private sealed record RequiredText(string Value, McpResponse Response);
}
