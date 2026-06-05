// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Net.Http.Json;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class JobSchedulerEndpointsApplication(ITestOutputHelper output) : WebApplicationFactory<JobSchedulerEndpointsTests>
{
    public IJobSchedulerService Scheduler { get; } = Substitute.For<IJobSchedulerService>();

    public IJobSchedulerQueryService Query { get; } = Substitute.For<IJobSchedulerQueryService>();

    public IJobSchedulerMaintenanceService Maintenance { get; } = Substitute.For<IJobSchedulerMaintenanceService>();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();

        appBuilder.Services.AddRouting();
        appBuilder.Services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new XunitLoggerProvider(output));
        });
        appBuilder.Services.AddSingleton(this.Scheduler);
        appBuilder.Services.AddSingleton(this.Query);
        appBuilder.Services.AddSingleton(this.Maintenance);
        appBuilder.Services.AddSingleton(new JobSchedulerEndpointsOptions
        {
            RequireAuthorization = false,
        });
        appBuilder.Services.AddEndpoints<JobSchedulerEndpoints>();

        var app = appBuilder.Build();
        app.UseRouting();
        app.MapEndpoints();
        app.Start();

        return app;
    }
}

public class JobSchedulerEndpointsTests : IAsyncDisposable
{
    private readonly JobSchedulerEndpointsApplication factory;
    private readonly HttpClient client;
    private readonly IJobSchedulerService scheduler;
    private readonly IJobSchedulerQueryService query;

    public JobSchedulerEndpointsTests(ITestOutputHelper output)
    {
        this.factory = new JobSchedulerEndpointsApplication(output);
        this.client = this.factory.CreateClient();
        this.scheduler = this.factory.Scheduler;
        this.query = this.factory.Query;
    }

    public async ValueTask DisposeAsync()
    {
        await this.factory.DisposeAsync();
    }

    [Fact]
    public async Task GetJobs_ShouldBindQueryParameters()
    {
        this.query.QueryJobsAsync(Arg.Any<JobSchedulerJobQueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(ResultPaged<JobSchedulerJobModel>.Success(
            [
                new JobSchedulerJobModel
                {
                    JobName = "cleanup",
                    DisplayName = "cleanup",
                    Group = "ops",
                    Module = "Billing",
                    RegisteredEnabled = true,
                    EffectiveEnabled = true,
                },
            ], 1, 1, 25));

        var response = await this.client.GetAsync("/_bdk/api/jobs?jobName=cleanup&group=ops&module=Billing&enabled=true&includeOrphanedRuntimeState=true&skip=5&take=25&sortBy=JobName&sortDescending=false");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.query.Received(1).QueryJobsAsync(
            Arg.Is<JobSchedulerJobQueryRequest>(request =>
                request.JobName == "cleanup"
                && request.Group == "ops"
                && request.Module == "Billing"
                && request.Enabled == true
                && request.IncludeOrphanedRuntimeState
                && request.Skip == 5
                && request.Take == 25
                && request.SortBy == "JobName"
                && request.SortDescending == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDashboardTimeline_ShouldBindSchedulerInstanceIdAndWindow()
    {
        var fromUtc = new DateTimeOffset(2026, 05, 26, 00, 00, 00, TimeSpan.Zero);
        var toUtc = fromUtc.AddHours(1);

        this.query.GetDashboardTimelineAsync(Arg.Any<JobSchedulerTimelineRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<JobSchedulerTimelineModel>.Success(new JobSchedulerTimelineModel
            {
                Mode = JobSchedulerTimelineMode.Executions,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                BucketMinutes = 15,
                Buckets = [],
            }));

        var response = await this.client.GetAsync($"/_bdk/api/jobs/dashboard/timeline?mode=Executions&jobName=cleanup&triggerName=nightly&schedulerInstanceId=node-a&from={Uri.EscapeDataString(fromUtc.ToString("O"))}&to={Uri.EscapeDataString(toUtc.ToString("O"))}&bucket=15");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.query.Received(1).GetDashboardTimelineAsync(
            Arg.Is<JobSchedulerTimelineRequest>(request =>
                request.Mode == JobSchedulerTimelineMode.Executions
                && request.JobName == "cleanup"
                && request.TriggerName == "nightly"
                && request.SchedulerInstanceId == "node-a"
                && request.From == fromUtc
                && request.To == toUtc
                && request.Bucket == 15),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchJob_ShouldReturnAcceptedOccurrenceLocation()
    {
        var occurrenceId = Guid.NewGuid();
        this.scheduler.DispatchAsync("cleanup", Arg.Any<object>(), Arg.Any<JobDispatchOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result<JobDispatchResult>.Success(new JobDispatchResult
            {
                JobName = "cleanup",
                TriggerName = "manual",
                OccurrenceId = occurrenceId,
                CorrelationId = "corr-1",
                AcceptedUtc = DateTimeOffset.UtcNow,
            }));

        var response = await this.client.PostAsJsonAsync("/_bdk/api/jobs/cleanup/dispatch", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldEndWith($"/_bdk/api/jobs/occurrences/{occurrenceId}");
        await this.scheduler.Received(1).DispatchAsync(
            "cleanup",
            Arg.Any<object>(),
            Arg.Any<JobDispatchOptions>(),
            Arg.Any<CancellationToken>());
    }
}
