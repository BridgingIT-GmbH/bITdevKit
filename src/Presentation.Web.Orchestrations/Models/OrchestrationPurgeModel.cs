namespace BridgingIT.DevKit.Presentation.Web.Orchestrations.Models;

using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Represents the query parameters used to purge retained orchestration data.
/// </summary>
public class OrchestrationPurgeModel
{
    public static ValueTask<OrchestrationPurgeModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;

        return ValueTask.FromResult(new OrchestrationPurgeModel
        {
            OlderThan = TryParseDateTimeOffset(query["olderThan"]),
            Statuses = query["statuses"],
            IsArchived = TryParseBoolean(query["isArchived"]),
        });
    }

    /// <summary>
    /// Gets or sets the optional upper age filter.
    /// </summary>
    public DateTimeOffset? OlderThan { get; set; }

    /// <summary>
    /// Gets or sets the optional orchestration status filters.
    /// </summary>
    public StringValues Statuses { get; set; }

    /// <summary>
    /// Gets or sets the optional archive-state filter.
    /// </summary>
    public bool? IsArchived { get; set; }

    private static DateTimeOffset? TryParseDateTimeOffset(StringValues value)
    {
        return DateTimeOffset.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static bool? TryParseBoolean(StringValues value)
    {
        return bool.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }
}