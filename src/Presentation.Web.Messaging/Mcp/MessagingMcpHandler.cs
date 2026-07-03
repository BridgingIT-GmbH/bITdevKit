namespace BridgingIT.DevKit.Presentation.Web.Messaging;

using System.Text.Json;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides MCP operations for durable message broker diagnostics and control.
/// </summary>
/// <example>
/// <code>
/// services.AddMessaging().AddMcpHandlers();
/// </code>
/// </example>
public sealed class MessagingMcpHandler(IMessageBrokerService broker) : IMcpHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<McpCapability> Capabilities { get; } =
    [
        Capability("messages.summary", McpToolset.Diagnostics, "Returns broker message runtime summary."),
        Capability("messages.subscriptions", McpToolset.Diagnostics, "Lists message subscriptions."),
        Capability("messages.waiting", McpToolset.Diagnostics, "Lists messages waiting for handlers."),
        Capability("messages.list", McpToolset.Diagnostics, "Lists retained broker messages."),
        Capability("messages.details", McpToolset.Diagnostics, "Returns broker message details."),
        Capability("messages.content", McpToolset.Diagnostics, "Returns persisted broker message content."),
        Capability("messages.retry", McpToolset.Operations, "Retries a broker message or handler."),
        Capability("messages.releaseLease", McpToolset.Operations, "Releases a broker message lease."),
        Capability("messages.archive", McpToolset.Operations, "Archives a broker message."),
        Capability("messages.pauseType", McpToolset.Operations, "Pauses broker message processing for a type."),
        Capability("messages.resumeType", McpToolset.Operations, "Resumes broker message processing for a type."),
        Capability("messages.purge", McpToolset.Admin, "Purges retained broker messages.")
    ];

    /// <inheritdoc />
    public async ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
        => request.Operation switch
        {
            "messages.summary" => await SummaryAsync(cancellationToken).ConfigureAwait(false),
            "messages.subscriptions" => await SubscriptionsAsync(cancellationToken).ConfigureAwait(false),
            "messages.waiting" => await WaitingAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "messages.list" => await ListAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "messages.details" => await DetailsAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "messages.content" => await ContentAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "messages.retry" => await RetryAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "messages.releaseLease" => await ReleaseLeaseAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "messages.archive" => await ArchiveAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "messages.pauseType" => await PauseTypeAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "messages.resumeType" => await ResumeTypeAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "messages.purge" => await PurgeAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            _ => McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Operation '{request.Operation}' is not handled by messaging.")
        };

    private async Task<McpResponse> SummaryAsync(CancellationToken cancellationToken)
    {
        var summary = await broker.GetSummaryAsync(cancellationToken).ConfigureAwait(false);

        return McpResponse.Success("Returned broker message summary.", new { summary });
    }

    private async Task<McpResponse> SubscriptionsAsync(CancellationToken cancellationToken)
    {
        var subscriptions = (await broker.GetSubscriptionsAsync(cancellationToken).ConfigureAwait(false)).ToArray();

        return McpResponse.Success($"Returned {subscriptions.Length} message subscription{(subscriptions.Length == 1 ? string.Empty : "s")}.", new { subscriptions });
    }

    private async Task<McpResponse> WaitingAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var messages = (await broker.GetWaitingMessagesAsync(Math.Min(McpArgumentReader.GetInt32(arguments, "take", 50) ?? 50, 500), cancellationToken).ConfigureAwait(false)).ToArray();

        return McpResponse.Success($"Returned {messages.Length} waiting broker message{(messages.Length == 1 ? string.Empty : "s")}.", new { messages });
    }

    private async Task<McpResponse> ListAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var messages = (await broker.GetMessagesAsync(
            McpArgumentReader.GetEnum<BrokerMessageStatus>(arguments, "status"),
            McpArgumentReader.GetString(arguments, "type"),
            McpArgumentReader.GetString(arguments, "messageId"),
            McpArgumentReader.GetString(arguments, "lockedBy"),
            McpArgumentReader.GetBoolean(arguments, "isArchived", false),
            McpArgumentReader.GetDateTimeOffset(arguments, "createdAfter"),
            McpArgumentReader.GetDateTimeOffset(arguments, "createdBefore"),
            McpArgumentReader.GetBoolean(arguments, "includeHandlers", false) == true,
            Math.Min(McpArgumentReader.GetInt32(arguments, "take", 50) ?? 50, 500),
            cancellationToken).ConfigureAwait(false)).ToArray();

        return McpResponse.Success($"Returned {messages.Length} broker message{(messages.Length == 1 ? string.Empty : "s")}.", new { messages });
    }

    private async Task<McpResponse> DetailsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var message = await broker.GetMessageAsync(id.Value, includeHandlers: true, cancellationToken).ConfigureAwait(false);

        return message is null
            ? McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Broker message '{id.Value}' was not found.")
            : McpResponse.Success($"Returned broker message '{id.Value}'.", new { message });
    }

    private async Task<McpResponse> ContentAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var content = await broker.GetMessageContentAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return content is null
            ? McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Broker message content '{id.Value}' was not found.")
            : McpResponse.Success($"Returned broker message content '{id.Value}'.", new { content });
    }

    private async Task<McpResponse> RetryAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        var handlerType = McpArgumentReader.GetString(arguments, "handlerType");
        if (string.IsNullOrWhiteSpace(handlerType))
        {
            await broker.RetryMessageAsync(id.Value, cancellationToken).ConfigureAwait(false);
            return McpResponse.Success($"Retried broker message '{id.Value}'.", new { id = id.Value });
        }

        await broker.RetryMessageHandlerAsync(id.Value, handlerType, cancellationToken).ConfigureAwait(false);
        return McpResponse.Success($"Retried handler '{handlerType}' for broker message '{id.Value}'.", new { id = id.Value, handlerType });
    }

    private async Task<McpResponse> ReleaseLeaseAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        await broker.ReleaseLeaseAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Released broker message lease '{id.Value}'.", new { id = id.Value });
    }

    private async Task<McpResponse> ArchiveAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var id = RequiredId(arguments);
        if (id.Response is not null)
        {
            return id.Response;
        }

        await broker.ArchiveMessageAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Archived broker message '{id.Value}'.", new { id = id.Value });
    }

    private async Task<McpResponse> PauseTypeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var type = RequiredString(arguments, "type");
        if (type.Response is not null)
        {
            return type.Response;
        }

        await broker.PauseMessageTypeAsync(type.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Paused broker message type '{type.Value}'.", new { type = type.Value });
    }

    private async Task<McpResponse> ResumeTypeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var type = RequiredString(arguments, "type");
        if (type.Response is not null)
        {
            return type.Response;
        }

        await broker.ResumeMessageTypeAsync(type.Value, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Resumed broker message type '{type.Value}'.", new { type = type.Value });
    }

    private async Task<McpResponse> PurgeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!HasConfirmation(arguments, "purge messages"))
        {
            return McpResponse.Unavailable("confirmation_required", "Message purge requires confirmation.", "Set confirm=true and confirmation='purge messages'.");
        }

        await broker.PurgeMessagesAsync(
            McpArgumentReader.GetDateTimeOffset(arguments, "olderThan"),
            McpArgumentReader.GetEnumArray<BrokerMessageStatus>(arguments, "statuses"),
            McpArgumentReader.GetBoolean(arguments, "isArchived"),
            cancellationToken).ConfigureAwait(false);

        return McpResponse.Success("Purged retained broker messages.", new
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
        => new(name, toolset, "messaging", description) { Owner = "bdk", Category = toolset == McpToolset.Diagnostics ? "inspect" : "operate" };

    private sealed record RequiredGuid(Guid Value, McpResponse Response);

    private sealed record RequiredText(string Value, McpResponse Response);
}
