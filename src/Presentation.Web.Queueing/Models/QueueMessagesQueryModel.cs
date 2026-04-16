namespace BridgingIT.DevKit.Presentation.Web.Queueing.Models;

using BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Represents the query parameters used to list queue messages.
/// </summary>
/// <example>
/// <code>
/// GET /api/_system/queueing/messages?status=Failed&amp;queueName=Orders&amp;take=100
/// </code>
/// </example>
public class QueueMessagesQueryModel
{
    /// <summary>
    /// Gets or sets the optional queue message status filter.
    /// </summary>
    public QueueMessageStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the optional persisted CLR message type filter.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the optional logical queue name filter.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the optional logical message identifier filter.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the optional lease-owner filter.
    /// </summary>
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the optional archive-state filter. When <c>null</c>, both active and archived messages are included.
    /// </summary>
    public bool? IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the optional lower creation-date filter.
    /// </summary>
    public DateTimeOffset? CreatedAfter { get; set; }

    /// <summary>
    /// Gets or sets the optional upper creation-date filter.
    /// </summary>
    public DateTimeOffset? CreatedBefore { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum number of results to return.
    /// </summary>
    public int? Take { get; set; }
}