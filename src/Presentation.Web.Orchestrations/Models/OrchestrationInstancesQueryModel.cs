namespace BridgingIT.DevKit.Presentation.Web.Orchestrations.Models;

using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Represents the query parameters used to list orchestration instances.
/// </summary>
public class OrchestrationInstancesQueryModel
{
    public static ValueTask<OrchestrationInstancesQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;

        return ValueTask.FromResult(new OrchestrationInstancesQueryModel
        {
            OrchestrationName = query["orchestrationName"],
            Statuses = query["statuses"],
            States = query["states"],
            CorrelationId = query["correlationId"],
            ConcurrencyKey = query["concurrencyKey"],
            StartedFrom = TryParseDateTimeOffset(query["startedFrom"]),
            StartedTo = TryParseDateTimeOffset(query["startedTo"]),
            CompletedFrom = TryParseDateTimeOffset(query["completedFrom"]),
            CompletedTo = TryParseDateTimeOffset(query["completedTo"]),
            Skip = TryParseInt32(query["skip"]) ?? 0,
            Take = TryParseInt32(query["take"]) ?? 50,
            SortBy = query.TryGetValue("sortBy", out var sortBy) && !StringValues.IsNullOrEmpty(sortBy) ? sortBy.ToString() : "StartedUtc",
            SortDescending = TryParseBoolean(query["sortDescending"]) ?? true,
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
    /// Gets or sets the correlation identifier filter.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the concurrency key filter.
    /// </summary>
    public string ConcurrencyKey { get; set; }

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

    /// <summary>
    /// Gets or sets the number of items to skip.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets the number of items to take.
    /// </summary>
    public int Take { get; set; } = 50;

    /// <summary>
    /// Gets or sets the property used for sorting.
    /// </summary>
    public string SortBy { get; set; } = "StartedUtc";

    /// <summary>
    /// Gets or sets a value indicating whether sorting should be descending.
    /// </summary>
    public bool SortDescending { get; set; } = true;

    private static DateTimeOffset? TryParseDateTimeOffset(StringValues value)
    {
        return DateTimeOffset.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static int? TryParseInt32(StringValues value)
    {
        return int.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static bool? TryParseBoolean(StringValues value)
    {
        return bool.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }
}