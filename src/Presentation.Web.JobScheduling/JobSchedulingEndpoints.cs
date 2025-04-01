// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.JobScheduling;

using System.Net;
using System.Threading;
using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

public class JobSchedulingEndpoints(
    ILoggerFactory loggerFactory,
    IJobService jobService,
    JobSchedulingEndpointsOptions options = null) : EndpointsBase
{
    private readonly ILogger<JobSchedulingEndpoints> logger = loggerFactory?.CreateLogger<JobSchedulingEndpoints>() ?? NullLogger<JobSchedulingEndpoints>.Instance;
    private readonly JobSchedulingEndpointsOptions options = options ?? new JobSchedulingEndpointsOptions();

    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options);

        group.MapGet(string.Empty, this.GetJobs)
            .Produces<IEnumerable<JobInfo>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("GetJobs")
            .WithDescription("Retrieves a list of all scheduled jobs.");

        group.MapGet("{jobName}/{jobGroup}", this.GetJob)
            .Produces<JobInfo>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("GetJob")
            .WithDescription("Retrieves details for a specific job.");

        group.MapGet("{jobName}/{jobGroup}/runs", this.GetJobRuns)
            .Produces<IEnumerable<JobRun>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("GetJobRuns")
            .WithDescription("Retrieves execution history for a specific job with optional filters.");

        group.MapGet("{jobName}/{jobGroup}/stats", this.GetJobRunStats)
            .Produces<JobRunStats>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("GetJobRunStats")
            .WithDescription("Retrieves aggregated statistics for a job’s execution history.");

        group.MapGet("{jobName}/{jobGroup}/triggers", this.GetJobTriggers)
            .Produces<IEnumerable<TriggerInfo>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("GetJobTriggers")
            .WithDescription("Retrieves all triggers associated with a specific job.");

        group.MapPost("{jobName}/{jobGroup}/trigger", this.TriggerJob)
            .Produces<string>((int)HttpStatusCode.Accepted)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("TriggerJob")
            .WithDescription("Triggers a job to run immediately with optional data.");

        group.MapPost("{jobName}/{jobGroup}/pause", this.PauseJob)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("PauseJob")
            .WithDescription("Pauses the execution of a specific job.");

        group.MapPost("{jobName}/{jobGroup}/resume", this.ResumeJob)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("ResumeJob")
            .WithDescription("Resumes the execution of a paused job.");

        group.MapDelete("{jobName}/{jobGroup}/runs", this.PurgeJobRuns)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("PurgeJobRuns")
            .WithDescription("Purges job run history older than a specified date.");

        this.IsRegistered = true;
    }

    private async Task<IResult> GetJobs(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching all jobs");
        var jobs = await jobService.GetJobsAsync(cancellationToken);

        return Results.Ok(jobs);
    }

    private async Task<IResult> GetJob(string jobName, string jobGroup, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching job {JobName} in group {JobGroup}", jobName, jobGroup);
        var job = await jobService.GetJobAsync(jobName, jobGroup, cancellationToken);

        return job != null ? Results.Ok(job) : Results.NotFound($"Job {jobName} in group {jobGroup} not found.");
    }

    private async Task<IResult> GetJobRuns(
        string jobName,
        string jobGroup,
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        [FromQuery] string? status,
        [FromQuery] int? priority,
        [FromQuery] string? instanceName,
        [FromQuery] string? resultContains,
        [FromQuery] int? take,
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching run history for job {JobName} in group {JobGroup}", jobName, jobGroup);
        var runs = await jobService.GetJobRunsAsync(jobName, jobGroup, startDate, endDate, status, priority, instanceName, resultContains, take, cancellationToken);
        return Results.Ok(runs);
    }

    private async Task<IResult> GetJobRunStats(
        string jobName,
        string jobGroup,
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching run stats for job {JobName} in group {JobGroup}", jobName, jobGroup);
        var stats = await jobService.GetJobRunStatsAsync(jobName, jobGroup, startDate, endDate, cancellationToken);
        return Results.Ok(stats);
    }

    private async Task<IResult> GetJobTriggers(string jobName, string jobGroup, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching triggers for job {JobName} in group {JobGroup}", jobName, jobGroup);
        var triggers = await jobService.GetTriggersAsync(jobName, jobGroup, cancellationToken);
        return Results.Ok(triggers);
    }

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    private async Task<IResult> TriggerJob(string jobName, string jobGroup, [FromBody] Dictionary<string, object>? data, CancellationToken cancellationToken)
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    {
        this.logger.LogInformation("Triggering job {JobName} in group {JobGroup}", jobName, jobGroup);
        try
        {
            await jobService.TriggerJobAsync(jobName, jobGroup, data ?? [], cancellationToken);
            return Results.Accepted(null, $"Job {jobName} in group {jobGroup} triggered successfully.");
        }
        catch (SchedulerException ex)
        {
            return Results.Problem($"Failed to trigger job {jobName} in group {jobGroup}: {ex.Message}", statusCode: (int)HttpStatusCode.BadRequest);
        }
    }

    private async Task<IResult> PauseJob(string jobName, string jobGroup, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Pausing job {JobName} in group {JobGroup}", jobName, jobGroup);
        try
        {
            await jobService.PauseJobAsync(jobName, jobGroup, cancellationToken);
            return Results.Ok($"Job {jobName} in group {jobGroup} paused successfully.");
        }
        catch (SchedulerException ex)
        {
            return Results.Problem($"Failed to pause job {jobName} in group {jobGroup}: {ex.Message}", statusCode: (int)HttpStatusCode.BadRequest);
        }
    }

    private async Task<IResult> ResumeJob(string jobName, string jobGroup, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Resuming job {JobName} in group {JobGroup}", jobName, jobGroup);
        try
        {
            await jobService.ResumeJobAsync(jobName, jobGroup, cancellationToken);
            return Results.Ok($"Job {jobName} in group {jobGroup} resumed successfully.");
        }
        catch (SchedulerException ex)
        {
            return Results.Problem($"Failed to resume job {jobName} in group {jobGroup}: {ex.Message}", statusCode: (int)HttpStatusCode.BadRequest);
        }
    }

    private async Task<IResult> PurgeJobRuns(string jobName, string jobGroup, [FromQuery] DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Purging run history for job {JobName} in group {JobGroup} older than {OlderThan}", jobName, jobGroup, olderThan);
        await jobService.PurgeJobRunsAsync(jobName, jobGroup, olderThan, cancellationToken);
        return Results.Ok($"Run history for job {jobName} in group {jobGroup} older than {olderThan} purged successfully.");
    }
}