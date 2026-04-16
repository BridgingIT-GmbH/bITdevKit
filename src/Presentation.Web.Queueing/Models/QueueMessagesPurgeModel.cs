namespace BridgingIT.DevKit.Presentation.Web.Queueing.Models;

using BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Represents the query parameters used to purge persisted queue messages.
/// </summary>
/// <example>
/// <code>
/// DELETE /api/_system/queueing/messages?olderThan=2026-01-01T00:00:00Z&amp;statuses=Succeeded&amp;isArchived=true
/// </code>
/// </example>
public class QueueMessagesPurgeModel
{
    /// <summary>
    /// Gets or sets the optional upper age filter.
    /// </summary>
    public DateTimeOffset? OlderThan { get; set; }

    /// <summary>
    /// Gets or sets the optional statuses to purge.
    /// </summary>
    public QueueMessageStatus[] Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional archive-state filter.
    /// </summary>
    public bool? IsArchived { get; set; }
}