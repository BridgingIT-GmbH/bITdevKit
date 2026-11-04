// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>Builds chronological seek criteria for retained dashboard histories.</summary>
/// <example><code>DashboardHistoryCursor.Apply(filter, "Id", cursor, boundary);</code></example>
public static class DashboardHistoryCursor
{
    /// <summary>Encodes an immutable creation timestamp and its exact identifier.</summary>
    public static string Encode(DateTimeOffset createdDate, Guid id) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Cursor(createdDate, id)));

    /// <summary>Orders newest first and applies exclusive continuation and inclusive initial bounds.</summary>
    public static void Apply(FilterModel filter, string idField, string cursor, string boundary)
    {
        filter.Orderings =
        [
            new() { Field = "CreatedDate", Direction = OrderDirection.Descending },
            new() { Field = idField, Direction = OrderDirection.Descending }
        ];
        AddBound(filter, idField, cursor, false);
        AddBound(filter, idField, boundary, true);
    }

    private static void AddBound(FilterModel filter, string idField, string token, bool inclusive)
    {
        if (string.IsNullOrEmpty(token)) return;
        var value = JsonSerializer.Deserialize<Cursor>(Convert.FromBase64String(token))
            ?? throw new ArgumentException("Invalid history cursor.");
        filter.Filters.Add(new FilterCriteria
        {
            Filters =
            [
                new("CreatedDate", FilterOperator.LessThan, value.CreatedDate) { Logic = FilterLogicOperator.Or },
                new() { Filters = [new("CreatedDate", FilterOperator.Equal, value.CreatedDate),
                    new(idField, inclusive ? FilterOperator.LessThanOrEqual : FilterOperator.LessThan, value.Id)] }
            ]
        });
    }

    private sealed record Cursor(DateTimeOffset CreatedDate, Guid Id);
}
