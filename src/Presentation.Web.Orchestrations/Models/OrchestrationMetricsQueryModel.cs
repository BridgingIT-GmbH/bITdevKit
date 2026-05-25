namespace BridgingIT.DevKit.Presentation.Web.Orchestrations.Models;

using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Represents the query parameters used to aggregate orchestration metrics.
/// </summary>
public class OrchestrationMetricsQueryModel
{
    public static ValueTask<OrchestrationMetricsQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;

        return ValueTask.FromResult(new OrchestrationMetricsQueryModel
        {
            OrchestrationName = query["orchestrationName"],
            Statuses = query["statuses"],
            States = query["states"],
            StartedFrom = TryParseDateTimeOffset(query["startedFrom"]),
            StartedTo = TryParseDateTimeOffset(query["startedTo"]),
            CompletedFrom = TryParseDateTimeOffset(query["completedFrom"]),
            CompletedTo = TryParseDateTimeOffset(query["completedTo"]),
        });
    }

    /// <summary>
    /// Gets or sets the orchestration definition name filter.
    /// </summary>
    public string OrchestrationName { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status filters.
    /// </summary>
    public StringValues Statuses { get; set; }

    /// <summary>
    /// Gets or sets the current business state filters.
    /// </summary>
    public StringValues States { get; set; }

    /// <summary>
    /// Gets or sets the inclusive start timestamp lower bound.
    /// </summary>
    public DateTimeOffset? StartedFrom { get; set; }

    /// <summary>
    /// Gets or sets the inclusive start timestamp upper bound.
    /// </summary>
    public DateTimeOffset? StartedTo { get; set; }

    /// <summary>
    /// Gets or sets the inclusive completion timestamp lower bound.
    /// </summary>
    public DateTimeOffset? CompletedFrom { get; set; }

    /// <summary>
    /// Gets or sets the inclusive completion timestamp upper bound.
    /// </summary>
    public DateTimeOffset? CompletedTo { get; set; }

    private static DateTimeOffset? TryParseDateTimeOffset(StringValues value)
    {
        return DateTimeOffset.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }
}