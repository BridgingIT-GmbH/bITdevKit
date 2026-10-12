// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard.Pages;
using BridgingIT.DevKit.Presentation.Web.Logging.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies precise UTC filters and shared quick range controls across dashboard pages.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~DashboardDateRangeTests</code></example>
[UnitTest("Presentation")]
public sealed class DashboardDateRangeTests
{
    /// <summary>Timezone-free inputs mean UTC; explicit offsets select the same instant.</summary>
    /// <example><code>suite.ParseUtc_Input_DoesNotUseLocalTimeZone("2026-10-12T10:11:12.123");</code></example>
    [Theory]
    [InlineData("2026-10-12T10:11:12.123")]
    [InlineData("2026-10-12T10:11:12.123Z")]
    [InlineData("2026-10-12T12:11:12.123+02:00")]
    public void ParseUtc_Input_DoesNotUseLocalTimeZone(string input)
    {
        DashboardDateTime.ParseUtc(input).ShouldBe(new DateTimeOffset(2026, 10, 12, 10, 11, 12, 123, TimeSpan.Zero));
        DashboardDateTime.ParseUtc(input).Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    /// <summary>Picker values without seconds default to zero seconds and milliseconds in UTC.</summary>
    /// <example><code>suite.ParseUtc_MinuteInput_DefaultsSecondsToZero();</code></example>
    [Fact]
    public void ParseUtc_MinuteInput_DefaultsSecondsToZero()
    {
        DashboardDateTime.ParseUtc("2026-10-12T10:11").ShouldBe(new DateTimeOffset(2026, 10, 12, 10, 11, 0, TimeSpan.Zero));
    }

    /// <summary>Empty and invalid date inputs have no explicit bound.</summary>
    /// <example><code>suite.ParseUtc_InvalidInput_HasNoBound("");</code></example>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    public void ParseUtc_InvalidInput_HasNoBound(string input)
    {
        DashboardDateTime.ParseUtc(input).ShouldBeNull();
    }

    /// <summary>Paging and exports retain exact times instead of silently widening minute ranges to days.</summary>
    /// <example><code>suite.LogFilters_QueryRoundTrip_PreservesExactBounds();</code></example>
    [Fact]
    public void LogFilters_QueryRoundTrip_PreservesExactBounds()
    {
        var start = new DateTimeOffset(2026, 10, 12, 12, 11, 12, 123, TimeSpan.FromHours(2)).AddTicks(4567);
        var end = start.AddMinutes(1);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(LogEntriesDashboard.BuildQuery(new() { StartTime = start, EndTime = end }));

        var filter = LogEntriesDashboard.CreateFilter(context);
        var request = LogEntriesDashboard.CreateRequest(filter);

        request.StartTime.ShouldBe(start.ToUniversalTime());
        request.EndTime.ShouldBe(end.ToUniversalTime());
        (request.EndTime - request.StartTime).ShouldBe(TimeSpan.FromMinutes(1));
        DashboardDateTime.FormatInput(start).ShouldBe("2026-10-12T10:11");
        context.Request.Query["from"].ToString().ShouldEndWith("Z");
    }

    /// <summary>Existing date-only links still select the whole UTC day, while new timestamps remain exact.</summary>
    /// <example><code>suite.LogFilters_DateOnlyLink_RetainsWholeDay();</code></example>
    [Fact]
    public void LogFilters_DateOnlyLink_RetainsWholeDay()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new("?from=2026-10-12&to=2026-10-12");

        var filter = LogEntriesDashboard.CreateFilter(context);

        filter.StartTime.ShouldBe(new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero));
        filter.EndTime.ShouldBe(filter.StartTime.Value.AddDays(1).AddTicks(-1));
    }

    /// <summary>An empty end keeps Logs and Change History queries open when a quick range sets only the start.</summary>
    /// <example><code>suite.Filters_EmptyEnd_RemainsOpen();</code></example>
    [Fact]
    public void Filters_EmptyEnd_RemainsOpen()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new("?from=2026-10-12T10:11Z&to=&changedDateFrom=2026-10-12T10:11&changedDateTo=");

        LogEntriesDashboard.CreateRequest(LogEntriesDashboard.CreateFilter(context)).EndTime.ShouldBeNull();
        ChangeHistoryDashboardQueryState.FromRequest(context).ChangedDateTo.ShouldBeNull();
    }

    /// <summary>Change History applies UTC input bounds independently of host culture and timezone.</summary>
    /// <example><code>suite.ChangeHistory_InputBounds_AreUtc();</code></example>
    [Fact]
    public void ChangeHistory_InputBounds_AreUtc()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new("?changedDateFrom=2026-10-12T10:11:12.123&changedDateTo=2026-10-12T10:12:12.123");

        var filter = ChangeHistoryDashboardQueryState.FromRequest(context);

        filter.ChangedDateFrom.ShouldBe(new DateTimeOffset(2026, 10, 12, 10, 11, 12, 123, TimeSpan.Zero));
        (filter.ChangedDateTo - filter.ChangedDateFrom).ShouldBe(TimeSpan.FromMinutes(1));
    }

    /// <summary>Logs, Errors and Change History render the shared picker and precise inputs with the existing layout.</summary>
    /// <example><code>await suite.Pages_RenderSharedDateRanges("logentries", "logentries", "from", "to");</code></example>
    [Theory]
    [InlineData("logentries", "logentries", "from", "to")]
    [InlineData("errors", "dashboard-errors", "from", "to")]
    [InlineData("change-history", "change-history-date", "changedDateFrom", "changedDateTo")]
    public async Task Pages_RenderSharedDateRanges(string page, string prefix, string from, string to)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/admin")
            .WithPluginAssemblyContaining<BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard.DashboardEndpoints>());
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();

        var html = await app.GetTestClient().GetStringAsync("/admin/" + page + "?" + from + "=2026-10-12T10:11:12.123Z&" + to + "=2026-10-12T10:12:12.123Z");

        html.ShouldContain("data-dashboard-date-range-from=\"" + prefix + "-from\"");
        html.ShouldContain("data-dashboard-date-range-to=\"" + prefix + "-to\"");
        html.ShouldContain("type=\"datetime-local\"");
        html.ShouldContain("value=\"2026-10-12T10:11\"");
        html.ShouldContain("value=\"2026-10-12T10:12\"");
        html.ShouldContain("step=\"60\"");
        html.ShouldNotContain("step=\"0.001\"");
        html.ShouldContain("Last minute");
        html.ShouldContain("Last week");
    }
}
