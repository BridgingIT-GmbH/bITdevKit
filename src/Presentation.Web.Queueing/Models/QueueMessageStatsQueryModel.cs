namespace BridgingIT.DevKit.Presentation.Web.Queueing.Models;

/// <summary>
/// Represents the query parameters used to retrieve aggregate queue message statistics.
/// </summary>
/// <example>
/// <code>
/// GET /api/_system/queueing/messages/stats?isArchived=false
/// </code>
/// </example>
public class QueueMessageStatsQueryModel
{
    /// <summary>
    /// Gets or sets the optional lower date filter.
    /// </summary>
    public DateTimeOffset? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the optional upper date filter.
    /// </summary>
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the optional archive-state filter. When <c>null</c>, both active and archived messages are included.
    /// </summary>
    public bool? IsArchived { get; set; }
}