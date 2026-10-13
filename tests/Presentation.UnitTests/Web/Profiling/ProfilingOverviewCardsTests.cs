// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Profiling;

using System.Net;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ProfilingPageProvider = BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard.DashboardPageProvider;

/// <summary>Verifies bounded retained summaries, feature gating and safe overview failures.</summary>
/// <example><code>await suite.Overview_RealHistory_CountsAllOutcomesAndOnlyRecentHttpRequests();</code></example>
public sealed class ProfilingOverviewCardsTests
{
    /// <summary>Overview and refresh cards count complete recent selections, independently of their one-row query bound.</summary>
    /// <example><code>await suite.Overview_RealHistory_CountsAllOutcomesAndOnlyRecentHttpRequests();</code></example>
    [Fact]
    public async Task Overview_RealHistory_CountsAllOutcomesAndOnlyRecentHttpRequests()
    {
        // Arrange
        var clock = new OverviewClock(DateTimeOffset.UtcNow.AddHours(-1));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<TimeProvider>(clock);
        var registration = builder.Services.AddProfiling(options => options.Enabled()).WithOperationProfiling(options => options.FlushInterval(TimeSpan.FromMinutes(1)));
        registration.Options.Operations.WriterLeaseDuration = TimeSpan.FromHours(2); // Keep the writer lease valid across the fixture's clock jump.
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/admin"));
        await using var app = builder.Build();
        app.MapEndpoints(); await app.StartAsync();
        var profiler = app.Services.GetRequiredService<IOperationProfiler>();
        var writer = app.Services.GetRequiredService<OperationProfilingWriterService>();
        await writer.TickAsync();
        var oldId = Capture("old", OperationProfilingKind.HttpRequest, OperationProfilingOutcome.Completed);
        await writer.TickAsync();
        clock.UtcNow = clock.UtcNow.AddHours(1);
        Capture("job:cleanup", OperationProfilingKind.Job, OperationProfilingOutcome.Completed);
        Capture("job:cleanup", OperationProfilingKind.Job, OperationProfilingOutcome.Failed);
        Capture("/cities/{cityId}/weather", OperationProfilingKind.HttpRequest, OperationProfilingOutcome.Completed);
        Capture("/cities/{cityId}/weather", OperationProfilingKind.HttpRequest, OperationProfilingOutcome.Canceled);
        Capture("weather:review", OperationProfilingKind.Service, OperationProfilingOutcome.Completed);
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        await writer.TickAsync();
        var context = new DefaultHttpContext { RequestServices = app.Services };
        var provider = new ProfilingPageProvider(app.Services.GetRequiredService<DashboardEndpointsOptions>());

        // Act
        var pages = provider.GetPages(context).ToArray();
        var operations = await pages.Single(page => page.Key == "profiling.operations").Card(context);
        var requests = await pages.Single(page => page.Key == "profiling.requests").Card(context);

        // Assert
        (await app.Services.GetRequiredService<IOperationProfilingQueryService>().FindAsync(oldId)).Value.ShouldNotBeNull();
        pages.Count(page => page.ShowInSidebar).ShouldBe(1);
        operations.Value.ShouldBe("5"); operations.Detail.ShouldBe("3 groups · 1 node");
        requests.Value.ShouldBe("2"); requests.Detail.ShouldBe("1 group · 1 node");
        operations.Url.ShouldBe("/admin/profiling/operations"); requests.Url.ShouldBe("/admin/profiling/requests");
        operations.Subtitle.ShouldContain("last 15 min");
        foreach (var path in new[] { "/admin", "/admin/content" })
        {
            var response = await app.GetTestClient().GetAsync(path);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var html = await response.Content.ReadAsStringAsync();
            html.ShouldContain("href=\"/admin/profiling/operations\""); html.ShouldContain("href=\"/admin/profiling/requests\"");
            html.ShouldContain("3 groups"); html.ShouldContain("1 group");
        }

        Guid Capture(string key, OperationProfilingKind kind, OperationProfilingOutcome outcome)
        {
            using var operation = profiler.BeginOperation(new() { Key = key, Kind = kind.ToString() });
            if (outcome == OperationProfilingOutcome.Failed) { operation.Fail(new ProfilingFailureDescriptor { Source = "Test", Code = "Failure" }); }
            else if (outcome == OperationProfilingOutcome.Canceled) { operation.Cancel(); }
            else { operation.Complete(); }

            return operation.Id;
        }
    }

    /// <summary>A stopped capture still exposes history, and truncated node choices are not presented as exact totals.</summary>
    /// <example><code>await suite.Card_CaptureStopped_QueriesHistoryAndLabelsBoundedNodes();</code></example>
    [Fact]
    public async Task Card_CaptureStopped_QueriesHistoryAndLabelsBoundedNodes()
    {
        // Arrange
        var queries = Substitute.For<IOperationProfilingQueryService>();
        queries.GroupAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>()).Returns(Result<OperationProfilingGroupPage>.Success(new()
        {
            TotalOperationCount = 42, TotalGroupCount = 7, Nodes = [new(), new()], NodesTruncated = true,
        }));
        using var services = Services(queries);
        var context = new DefaultHttpContext { RequestServices = services };
        var page = new ProfilingPageProvider(services.GetRequiredService<DashboardEndpointsOptions>()).GetPages(context).Single(page => page.Key == "profiling.requests");

        // Act
        var card = await page.Card(context);

        // Assert
        card.Value.ShouldBe("42"); card.Detail.ShouldBe("7 groups · 2+ nodes");
        await queries.Received(1).GroupAsync(Arg.Is<OperationProfilingQuery>(query => query.PageSize == 1
            && query.Kind == "HttpRequest" && query.ToUtc - query.FromUtc == TimeSpan.FromMinutes(15)
            && query.Outcomes.Count == Enum.GetValues<OperationProfilingOutcome>().Length), Arg.Any<CancellationToken>());
    }

    /// <summary>History failures remain compact and never expose provider exception messages in the overview.</summary>
    /// <example><code>await suite.Card_HistoryFailure_ShowsSafeUnavailableSummary(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Card_HistoryFailure_ShowsSafeUnavailableSummary(bool throws)
    {
        // Arrange
        var queries = Substitute.For<IOperationProfilingQueryService>();
        queries.GroupAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>()).Returns(throws
            ? Task.FromException<IResult<OperationProfilingGroupPage>>(new InvalidOperationException("private provider details"))
            : Task.FromResult<IResult<OperationProfilingGroupPage>>(Result<OperationProfilingGroupPage>.Failure(new ProfilingUnavailableError("private provider details"))));
        using var services = Services(queries);
        var context = new DefaultHttpContext { RequestServices = services };
        var pages = new ProfilingPageProvider(services.GetRequiredService<DashboardEndpointsOptions>()).GetPages(context);

        // Act / Assert
        foreach (var page in pages.Where(page => page.Key != "profiling"))
        {
            var card = await page.Card(context);
            card.Value.ShouldBe("Unavailable"); card.Detail.ShouldBe("Profiling history unavailable");
        }
    }

    /// <summary>Disabling the parent feature also hides its overview cards; individual cards can be disabled independently.</summary>
    /// <example><code>suite.Pages_DisabledKeys_HideRelatedOverviewCards("profiling", 0);</code></example>
    [Theory]
    [InlineData("profiling", 0)]
    [InlineData("profiling.requests", 2)]
    public void Pages_DisabledKeys_HideRelatedOverviewCards(string key, int count)
    {
        // Arrange
        using var services = Services(Substitute.For<IOperationProfilingQueryService>(), key);
        var context = new DefaultHttpContext { RequestServices = services };

        // Act / Assert
        context.GetDashboardPages().Count.ShouldBe(count);
    }

    private static ServiceProvider Services(IOperationProfilingQueryService queries, string disabledKey = null)
    {
        var options = new DashboardEndpointsOptionsBuilder().DisablePages(disabledKey is null ? [] : [disabledKey]).Build();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton(new ProfilingOptions { Enabled = true });
        services.AddSingleton(queries);
        services.AddSingleton<IDashboardPageProvider>(new ProfilingPageProvider(options));
        return services.BuildServiceProvider();
    }

    private sealed class OverviewClock(DateTimeOffset now) : TimeProvider
    {
        /// <summary>Gets or sets the fixture's UTC observation time.</summary>
        /// <example><code>clock.UtcNow = clock.UtcNow.AddMinutes(1);</code></example>
        public DateTimeOffset UtcNow { get; set; } = now;
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => this.UtcNow;
    }
}
