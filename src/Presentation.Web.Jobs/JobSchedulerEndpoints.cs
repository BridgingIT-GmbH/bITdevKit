// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Jobs;

using System.Net;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Exposes operational REST endpoints for inspecting and managing jobs scheduler state.
/// </summary>
/// <example>
/// <code>
/// services.AddJobScheduler()
///     .AddEndpoints(options => options
///         .GroupPath("/_bdk/api/jobs")
///         .GroupTag("_bdk/jobs")
///         .RequireAuthorization());
/// </code>
/// </example>
public class JobSchedulerEndpoints(
    ILoggerFactory loggerFactory,
    IJobSchedulerService scheduler,
    IJobSchedulerQueryService query,
    IJobSchedulerMaintenanceService maintenance,
    JobSchedulerEndpointsOptions options = null) : EndpointsBase
{
    private readonly ILogger<JobSchedulerEndpoints> logger = loggerFactory?.CreateLogger<JobSchedulerEndpoints>() ?? NullLogger<JobSchedulerEndpoints>.Instance;
    private readonly JobSchedulerEndpointsOptions options = options ?? new JobSchedulerEndpointsOptions();

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled || !this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options)
            .WithTags("_bdk.Jobs");

        var definitions = group.MapGroup("definitions");
        var occurrences = group.MapGroup("occurrences");
        var batches = group.MapGroup("batches");
        var maintenanceGroup = group.MapGroup("maintenance");
        var dashboard = group.MapGroup("dashboard");

        group.MapGet(string.Empty, this.GetJobs)
            .Produces<ResultPaged<JobSchedulerJobModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetJobs")
            .WithSummary("List jobs")
            .WithDescription("Retrieves registered jobs with operational overlay, paging, filtering, and sorting.");

        definitions.MapGet("{jobName}", this.GetJob)
            .Produces<JobSchedulerJobModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetJob")
            .WithSummary("Get job details")
            .WithDescription("Retrieves a single registered job definition with operational state.");

        group.MapGet("triggers", this.GetTriggers)
            .Produces<ResultPaged<JobSchedulerTriggerModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetTriggers")
            .WithSummary("List triggers")
            .WithDescription("Retrieves job triggers with paging, filtering, and sorting.");

        group.MapGet("triggers/recurring", this.GetRecurringTriggers)
            .Produces<ResultPaged<JobSchedulerRecurringTriggerModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetRecurringTriggers")
            .WithSummary("List recurring triggers")
            .WithDescription("Retrieves recurring job triggers with paging, filtering, and sorting.");

        definitions.MapGet("{jobName}/triggers/{triggerName}", this.GetTrigger)
            .Produces<JobSchedulerTriggerModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetTrigger")
            .WithSummary("Get trigger details")
            .WithDescription("Retrieves a single registered trigger with operational state.");

        group.MapGet("occurrences", this.GetOccurrences)
            .Produces<ResultPaged<JobSchedulerOccurrenceModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetOccurrences")
            .WithSummary("List occurrences")
            .WithDescription("Retrieves materialized job occurrences with paging, filtering, and sorting.");

        group.MapDelete("occurrences", this.PurgeOccurrences)
            .Produces<JobMaintenanceReport>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.PurgeOccurrences")
            .WithSummary("Purge occurrences")
            .WithDescription("Purges retained terminal occurrences by age and optional status, job, trigger, and archive filters.");

        group.MapGet("retries", this.GetRetries)
            .Produces<ResultPaged<JobSchedulerRetryModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetRetries")
            .WithSummary("List retries")
            .WithDescription("Retrieves retry-scheduled or retryable occurrences.");

        group.MapGet("dependencies", this.GetDependencies)
            .Produces<ResultPaged<JobSchedulerDependencyModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetDependencies")
            .WithSummary("List dependencies")
            .WithDescription("Retrieves persisted occurrence dependency links with prerequisite and dependent state.");

        batches.MapGet(string.Empty, this.GetBatches)
            .Produces<ResultPaged<JobSchedulerBatchModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetBatches")
            .WithSummary("List batches")
            .WithDescription("Retrieves job batches with paging, filtering, and sorting.");

        batches.MapGet("{batchId}", this.GetBatch)
            .Produces<JobSchedulerBatchModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetBatch")
            .WithSummary("Get batch details")
            .WithDescription("Retrieves a single job batch.");

        batches.MapGet("{batchId}/occurrences", this.GetBatchOccurrences)
            .Produces<ResultPaged<JobSchedulerBatchChildOccurrenceModel>>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetBatchOccurrences")
            .WithSummary("List batch child occurrences")
            .WithDescription("Retrieves child occurrences for a specific batch.");

        batches.MapGet("{batchId}/history", this.GetBatchHistory)
            .Produces<ResultPaged<JobSchedulerBatchHistoryModel>>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetBatchHistory")
            .WithSummary("List batch history")
            .WithDescription("Retrieves append-only history for a specific batch.");

        group.MapGet("executions", this.GetExecutions)
            .Produces<ResultPaged<JobSchedulerExecutionModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetExecutions")
            .WithSummary("List executions")
            .WithDescription("Retrieves execution attempts with paging, filtering, and sorting.");

        group.MapGet("history", this.GetHistory)
            .Produces<ResultPaged<JobSchedulerExecutionHistoryModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetHistory")
            .WithSummary("List execution history")
            .WithDescription("Retrieves retained execution history with paging, filtering, and sorting.");

        group.MapGet("leases", this.GetLeases)
            .Produces<ResultPaged<JobSchedulerLeaseModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetLeases")
            .WithSummary("List leases")
            .WithDescription("Retrieves active and expired occurrence lease diagnostics.");

        group.MapGet("servers", this.GetServers)
            .Produces<ResultPaged<JobSchedulerServerModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetServers")
            .WithSummary("List scheduler servers")
            .WithDescription("Retrieves observed scheduler server instances.");

        group.MapGet("metrics", this.GetMetrics)
            .Produces<Result<JobSchedulerMetricsModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetMetrics")
            .WithSummary("Get scheduler metrics")
            .WithDescription("Retrieves aggregate jobs metrics for operational monitoring.");

        dashboard.MapGet("summary", this.GetDashboardSummary)
            .Produces<Result<JobSchedulerDashboardSummaryModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetDashboardSummary")
            .WithSummary("Get dashboard summary")
            .WithDescription("Retrieves aggregate counts and summary diagnostics for the jobs dashboard.");

        dashboard.MapGet("timeline", this.GetDashboardTimeline)
            .Produces<Result<JobSchedulerTimelineModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.GetDashboardTimeline")
            .WithSummary("Get dashboard timeline")
            .WithDescription("Retrieves timeline buckets for occurrences or executions.");

        dashboard.MapGet(string.Empty, this.GetDashboardSummary)
            .Produces<Result<JobSchedulerDashboardSummaryModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Get dashboard")
            .WithDescription("Retrieves the primary dashboard summary projection.");

        dashboard.MapGet("navigation", this.GetDashboardNavigation)
            .Produces<Result<JobSchedulerDashboardNavigationModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Get dashboard navigation")
            .WithDescription("Retrieves dashboard navigation metadata derived from the dashboard summary projection.");

        dashboard.MapGet("overview", this.GetDashboardOverview)
            .Produces<Result<JobSchedulerDashboardOverviewModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Get dashboard overview")
            .WithDescription("Retrieves dashboard overview data derived from the dashboard summary projection.");

        group.MapGet("recurring", this.GetRecurringTriggers)
            .Produces<ResultPaged<JobSchedulerRecurringTriggerModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("List recurring jobs")
            .WithDescription("Retrieves recurring job triggers using the spec-shaped operational route.");

        group.MapGet("recurring/{jobName}/{triggerName}", this.GetRecurringTrigger)
            .Produces<JobSchedulerRecurringTriggerModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Get recurring trigger details")
            .WithDescription("Retrieves a single recurring trigger using the spec-shaped operational route.");

        group.MapGet("{jobName}/triggers", this.GetJobTriggers)
            .Produces<ResultPaged<JobSchedulerTriggerModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("List job triggers")
            .WithDescription("Retrieves triggers for a specific job using the spec-shaped operational route.");

        group.MapGet("{jobName}/triggers/{triggerName}", this.GetTrigger)
            .Produces<JobSchedulerTriggerModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Get trigger details")
            .WithDescription("Retrieves a single trigger using the spec-shaped operational route.");

        occurrences.MapGet("{occurrenceId:guid}", this.GetOccurrence)
            .Produces<JobSchedulerOccurrenceModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Get occurrence details")
            .WithDescription("Retrieves one materialized occurrence by its internal occurrence identifier.");

        occurrences.MapGet("{occurrenceId:guid}/history", this.GetOccurrenceHistory)
            .Produces<ResultPaged<JobSchedulerExecutionHistoryModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Get occurrence history")
            .WithDescription("Retrieves execution history for one occurrence.");

        group.MapGet("{jobName}", this.GetJob)
            .Produces<JobSchedulerJobModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Get job details")
            .WithDescription("Retrieves a single registered job definition with operational state using the spec-shaped route.");

        definitions.MapPost("{jobName}/dispatch", this.DispatchJob)
            .Produces<Result<JobDispatchResult>>((int)HttpStatusCode.Accepted)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.DispatchJob")
            .WithSummary("Dispatch a job")
            .WithDescription("Dispatches a registered job using its configured manual trigger.");

        group.MapPost("{jobName}/dispatch", this.DispatchJob)
            .Produces<Result<JobDispatchResult>>((int)HttpStatusCode.Accepted)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Dispatch a job")
            .WithDescription("Dispatches a registered job using the spec-shaped operational route.");

        definitions.MapPost("{jobName}/enable", this.EnableJob)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.EnableJob")
            .WithSummary("Enable a job")
            .WithDescription("Enables a registered job through durable runtime state.");

        group.MapPost("{jobName}/enable", this.EnableJob)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Enable a job")
            .WithDescription("Enables a registered job using the spec-shaped operational route.");

        definitions.MapPost("{jobName}/disable", this.DisableJob)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.DisableJob")
            .WithSummary("Disable a job")
            .WithDescription("Disables a registered job through durable runtime state.");

        group.MapPost("{jobName}/disable", this.DisableJob)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Disable a job")
            .WithDescription("Disables a registered job using the spec-shaped operational route.");

        definitions.MapPost("{jobName}/pause", this.PauseJob)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.PauseJob")
            .WithSummary("Pause a job")
            .WithDescription("Pauses a registered job without mutating its code-first definition.");

        group.MapPost("{jobName}/pause", this.PauseJob)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Pause a job")
            .WithDescription("Pauses a registered job using the spec-shaped operational route.");

        definitions.MapPost("{jobName}/resume", this.ResumeJob)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.ResumeJob")
            .WithSummary("Resume a job")
            .WithDescription("Resumes a previously paused job.");

        group.MapPost("{jobName}/resume", this.ResumeJob)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Resume a job")
            .WithDescription("Resumes a previously paused job using the spec-shaped operational route.");

        definitions.MapPost("{jobName}/triggers/{triggerName}/pause", this.PauseTrigger)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.PauseTrigger")
            .WithSummary("Pause a trigger")
            .WithDescription("Pauses a registered trigger without mutating its code-first definition.");

        group.MapPost("{jobName}/triggers/{triggerName}/pause", this.PauseTrigger)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Pause a trigger")
            .WithDescription("Pauses a registered trigger using the spec-shaped operational route.");

        definitions.MapPost("{jobName}/triggers/{triggerName}/resume", this.ResumeTrigger)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.ResumeTrigger")
            .WithSummary("Resume a trigger")
            .WithDescription("Resumes a previously paused trigger.");

        group.MapPost("{jobName}/triggers/{triggerName}/resume", this.ResumeTrigger)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Resume a trigger")
            .WithDescription("Resumes a previously paused trigger using the spec-shaped operational route.");

        definitions.MapPost("{jobName}/triggers/{triggerName}/enable", this.EnableTrigger)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.EnableTrigger")
            .WithSummary("Enable a trigger")
            .WithDescription("Enables a registered trigger through durable runtime state.");

        group.MapPost("{jobName}/triggers/{triggerName}/enable", this.EnableTrigger)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Enable a trigger")
            .WithDescription("Enables a registered trigger using the spec-shaped operational route.");

        definitions.MapPost("{jobName}/triggers/{triggerName}/disable", this.DisableTrigger)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.DisableTrigger")
            .WithSummary("Disable a trigger")
            .WithDescription("Disables a registered trigger through durable runtime state.");

        group.MapPost("{jobName}/triggers/{triggerName}/disable", this.DisableTrigger)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithSummary("Disable a trigger")
            .WithDescription("Disables a registered trigger using the spec-shaped operational route.");

        occurrences.MapPost("{occurrenceId:guid}/pause", this.PauseOccurrence)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.PauseOccurrence")
            .WithSummary("Pause an occurrence")
            .WithDescription("Pauses an eligible occurrence before a new attempt starts.");

        occurrences.MapPost("{occurrenceId:guid}/resume", this.ResumeOccurrence)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.ResumeOccurrence")
            .WithSummary("Resume an occurrence")
            .WithDescription("Resumes a previously paused occurrence.");

        occurrences.MapPost("{occurrenceId:guid}/cancel", this.CancelOccurrence)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.CancelOccurrence")
            .WithSummary("Cancel an occurrence")
            .WithDescription("Requests cancellation of an occurrence.");

        occurrences.MapPost("{occurrenceId:guid}/interrupt", this.InterruptOccurrence)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.InterruptOccurrence")
            .WithSummary("Interrupt an occurrence")
            .WithDescription("Requests interruption of a running occurrence.");

        occurrences.MapPost("{occurrenceId:guid}/retry", this.RetryOccurrence)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.RetryOccurrence")
            .WithSummary("Retry an occurrence")
            .WithDescription("Requests retry of an eligible failed occurrence.");

        occurrences.MapPost("{occurrenceId:guid}/archive", this.ArchiveOccurrence)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.ArchiveOccurrence")
            .WithSummary("Archive an occurrence")
            .WithDescription("Archives a terminal occurrence.");

        occurrences.MapPost("{occurrenceId:guid}/repair/release-lease", this.ReleaseOccurrenceLease)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.ReleaseOccurrenceLease")
            .WithSummary("Release an occurrence lease")
            .WithDescription("Releases an active occurrence lease so the occurrence can be repaired or recovered.");

        occurrences.MapPost("bulk/retry", this.RetryOccurrences)
            .Produces<Result<JobBulkOperationResult>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.RetryOccurrences")
            .WithSummary("Retry occurrences")
            .WithDescription("Retries the selected eligible failed occurrences as one bulk operation.");

        occurrences.MapPost("bulk/cancel", this.CancelOccurrences)
            .Produces<Result<JobBulkOperationResult>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.CancelOccurrences")
            .WithSummary("Cancel occurrences")
            .WithDescription("Cancels the selected eligible occurrences as one bulk operation.");

        occurrences.MapPost("bulk/archive", this.ArchiveOccurrences)
            .Produces<Result<JobBulkOperationResult>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.ArchiveOccurrences")
            .WithSummary("Archive occurrences")
            .WithDescription("Archives the selected eligible retained occurrences as one bulk operation.");

        batches.MapPost(string.Empty, this.CreateBatch)
            .Produces<Result<JobBatchDispatchResult>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.CreateBatch")
            .WithSummary("Create a batch")
            .WithDescription("Creates an empty or described durable batch record.");

        batches.MapPost("dispatch", this.DispatchBatch)
            .Produces<Result<JobBatchDispatchResult>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.DispatchBatch")
            .WithSummary("Dispatch a batch")
            .WithDescription("Creates a batch and dispatches child occurrences as one accepted operation.");

        batches.MapPost("{batchId}/attach", this.AttachToBatch)
            .Produces<Result<JobBatchDispatchResult>>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.AttachToBatch")
            .WithSummary("Attach to batch")
            .WithDescription("Attaches additional child occurrences to an existing batch.");

        batches.MapPost("{batchId}/retry", this.RetryBatch)
            .Produces<Result<JobBulkOperationResult>>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.RetryBatch")
            .WithSummary("Retry batch")
            .WithDescription("Retries eligible failed child occurrences for a batch.");

        batches.MapPost("{batchId}/cancel", this.CancelBatch)
            .Produces<Result<JobBulkOperationResult>>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.CancelBatch")
            .WithSummary("Cancel batch")
            .WithDescription("Cancels eligible child occurrences for a batch.");

        batches.MapPost("{batchId}/pause", this.PauseBatch)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.PauseBatch")
            .WithSummary("Pause batch")
            .WithDescription("Pauses eligible child occurrences for a batch.");

        batches.MapPost("{batchId}/resume", this.ResumeBatch)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.ResumeBatch")
            .WithSummary("Resume batch")
            .WithDescription("Resumes eligible child occurrences for a batch.");

        batches.MapPost("{batchId}/archive", this.ArchiveBatch)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.ArchiveBatch")
            .WithSummary("Archive batch")
            .WithDescription("Archives a batch and eligible retained child occurrences.");

        maintenanceGroup.MapPost("purge-history", this.PurgeHistory)
            .Produces<JobMaintenanceReport>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.PurgeHistory")
            .WithSummary("Purge history")
            .WithDescription("Purges archived execution history older than the configured retention window.");

        maintenanceGroup.MapPost("release-expired-leases", this.ReleaseExpiredLeases)
            .Produces<JobMaintenanceReport>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.ReleaseExpiredLeases")
            .WithSummary("Release expired leases")
            .WithDescription("Releases expired leases and repairs the affected occurrences.");

        maintenanceGroup.MapPost("recover-stuck-occurrences", this.RecoverStuckOccurrences)
            .Produces<JobMaintenanceReport>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.RecoverStuckOccurrences")
            .WithSummary("Recover stuck occurrences")
            .WithDescription("Recovers stale occurrences that no longer have a valid active lease.");

        maintenanceGroup.MapPost("detect-orphaned-runtime-state", this.DetectOrphanedRuntimeState)
            .Produces<JobMaintenanceReport>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Jobs.DetectOrphanedRuntimeState")
            .WithSummary("Detect orphaned runtime state")
            .WithDescription("Detects runtime-state rows that no longer correspond to active code-first registrations.");

        this.IsRegistered = true;
    }

    private async Task<HttpResult> GetJobs([AsParameters] JobSchedulerJobQueryRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.QueryJobsAsync(request, cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));

    private async Task<HttpResult> GetJob(string jobName, CancellationToken cancellationToken)
    {
        var result = await query.QueryJobsAsync(new JobSchedulerJobQueryRequest { JobName = jobName, Take = 1, SortDescending = false }, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return this.OkOrProblem(result, value => TypedResults.Ok(value));
        }

        var model = result.Value.FirstOrDefault(x => string.Equals(x.JobName, jobName, StringComparison.OrdinalIgnoreCase));
        return model is not null ? TypedResults.Ok(model) : TypedResults.NotFound($"Job '{jobName}' was not found.");
    }

    private async Task<HttpResult> GetTriggers(JobSchedulerTriggerQueryModel request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.QueryTriggersAsync(request?.ToRequest(), cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));

    private async Task<HttpResult> GetJobTriggers(string jobName, JobSchedulerTriggerQueryModel request, CancellationToken cancellationToken)
    {
        var queryRequest = request?.ToRequest() ?? new JobSchedulerTriggerQueryRequest();
        queryRequest.JobName = jobName;
        return this.OkOrProblem(await query.QueryTriggersAsync(queryRequest, cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));
    }

    private async Task<HttpResult> GetRecurringTriggers(JobSchedulerTriggerQueryModel request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.QueryRecurringTriggersAsync(request?.ToRecurringRequest(), cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));

    private async Task<HttpResult> GetRecurringTrigger(string jobName, string triggerName, CancellationToken cancellationToken)
    {
        var result = await query.QueryRecurringTriggersAsync(new JobSchedulerRecurringTriggerQueryRequest
        {
            JobName = jobName,
            TriggerName = triggerName,
            Take = 10,
            SortDescending = false,
        }, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return this.OkOrProblem(result, value => TypedResults.Ok(value));
        }

        var model = result.Value.FirstOrDefault(x =>
            string.Equals(x.JobName, jobName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase));

        return model is not null
            ? TypedResults.Ok(model)
            : TypedResults.NotFound($"Recurring trigger '{triggerName}' on job '{jobName}' was not found.");
    }

    private async Task<HttpResult> GetTrigger(string jobName, string triggerName, CancellationToken cancellationToken)
    {
        var result = await query.QueryTriggersAsync(new JobSchedulerTriggerQueryRequest { JobName = jobName, TriggerName = triggerName, Take = 10, SortDescending = false }, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return this.OkOrProblem(result, value => TypedResults.Ok(value));
        }

        var model = result.Value.FirstOrDefault(x =>
            string.Equals(x.JobName, jobName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase));

        return model is not null
            ? TypedResults.Ok(model)
            : TypedResults.NotFound($"Trigger '{triggerName}' on job '{jobName}' was not found.");
    }

    private async Task<HttpResult> GetOccurrences(JobSchedulerOccurrenceQueryModel request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.QueryOccurrencesAsync(request?.ToRequest(), cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));

    private async Task<HttpResult> PurgeOccurrences([AsParameters] JobOccurrencesPurgeModel request, CancellationToken cancellationToken)
        => TypedResults.Ok(await maintenance.PurgeOccurrencesAsync(new JobPurgeOccurrencesRequest
        {
            OlderThan = request?.OlderThan,
            Statuses = request?.Statuses,
            JobName = request?.JobName,
            TriggerName = request?.TriggerName,
            IsArchived = request?.IsArchived,
            DryRun = request?.DryRun ?? false,
            BatchSize = request?.BatchSize > 0 ? request.BatchSize : 100,
        }, cancellationToken).ConfigureAwait(false));

    private async Task<HttpResult> GetOccurrence(Guid occurrenceId, CancellationToken cancellationToken)
    {
        var result = await query.QueryOccurrencesAsync(new JobSchedulerOccurrenceQueryRequest
        {
            OccurrenceId = occurrenceId,
            Take = 1,
            SortDescending = false,
        }, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return this.OkOrProblem(result, value => TypedResults.Ok(value));
        }

        var model = result.Value.FirstOrDefault(x => x.OccurrenceId == occurrenceId);
        return model is not null ? TypedResults.Ok(model) : TypedResults.NotFound($"Occurrence '{occurrenceId}' was not found.");
    }

    private async Task<HttpResult> GetOccurrenceHistory(Guid occurrenceId, JobSchedulerExecutionHistoryQueryModel request, CancellationToken cancellationToken)
    {
        var queryRequest = request?.ToRequest() ?? new JobSchedulerExecutionHistoryQueryRequest();
        queryRequest.OccurrenceId = occurrenceId;
        return this.OkOrProblem(await query.QueryExecutionHistoryAsync(queryRequest, cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));
    }

    private async Task<HttpResult> GetRetries([AsParameters] JobSchedulerRetryQueryRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.QueryRetriesAsync(request, cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));

    private async Task<HttpResult> GetDependencies(JobSchedulerDependencyQueryModel request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.QueryDependenciesAsync(request?.ToRequest(), cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));

    private async Task<HttpResult> GetBatches(JobSchedulerBatchQueryModel request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.QueryBatchesAsync(request?.ToRequest(), cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));

    private async Task<HttpResult> GetBatch(string batchId, CancellationToken cancellationToken)
    {
        var result = await query.QueryBatchesAsync(new JobSchedulerBatchQueryRequest { BatchId = batchId, Take = 1, SortDescending = false }, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return this.OkOrProblem(result, value => TypedResults.Ok(value));
        }

        var model = result.Value.FirstOrDefault(x => string.Equals(x.ExternalBatchId, batchId, StringComparison.OrdinalIgnoreCase));
        return model is not null ? TypedResults.Ok(model) : TypedResults.NotFound($"Batch '{batchId}' was not found.");
    }

    private async Task<HttpResult> GetBatchOccurrences(string batchId, JobSchedulerBatchOccurrenceQueryModel request, CancellationToken cancellationToken)
    {
        var batch = await this.TryGetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return TypedResults.NotFound($"Batch '{batchId}' was not found.");
        }

        return this.OkOrProblem(await query.QueryBatchOccurrencesAsync(batchId, request?.ToRequest(), cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));
    }

    private async Task<HttpResult> GetBatchHistory(string batchId, JobSchedulerBatchHistoryQueryModel request, CancellationToken cancellationToken)
    {
        var batch = await this.TryGetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return TypedResults.NotFound($"Batch '{batchId}' was not found.");
        }

        return this.OkOrProblem(await query.QueryBatchHistoryAsync(batchId, request?.ToRequest(), cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));
    }

    private async Task<HttpResult> GetExecutions(JobSchedulerExecutionQueryModel request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.QueryExecutionsAsync(request?.ToRequest(), cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));

    private async Task<HttpResult> GetHistory(JobSchedulerExecutionHistoryQueryModel request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.QueryExecutionHistoryAsync(request?.ToRequest(), cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));

    private async Task<HttpResult> GetLeases(JobSchedulerLeaseQueryModel request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.QueryLeasesAsync(request?.ToRequest(), cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));

    private async Task<HttpResult> GetServers(JobSchedulerServerQueryModel request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.QueryServersAsync(request?.ToRequest(), cancellationToken).ConfigureAwait(false), result => TypedResults.Ok(result));

    private async Task<HttpResult> GetMetrics(JobSchedulerMetricsQueryModel request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.GetMetricsAsync(request?.ToRequest(), cancellationToken).ConfigureAwait(false), value => TypedResults.Ok(value));

    private async Task<HttpResult> GetDashboardSummary([AsParameters] JobSchedulerDashboardSummaryRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.GetDashboardSummaryAsync(request, cancellationToken).ConfigureAwait(false), value => TypedResults.Ok(value));

    private async Task<HttpResult> GetDashboardNavigation(CancellationToken cancellationToken)
        => this.OkOrProblem(await query.GetDashboardNavigationAsync(cancellationToken).ConfigureAwait(false), value => TypedResults.Ok(value));

    private async Task<HttpResult> GetDashboardOverview(CancellationToken cancellationToken)
        => this.OkOrProblem(await query.GetDashboardOverviewAsync(cancellationToken).ConfigureAwait(false), value => TypedResults.Ok(value));

    private async Task<HttpResult> GetDashboardTimeline(JobSchedulerTimelineQueryModel request, CancellationToken cancellationToken)
        => this.OkOrProblem(await query.GetDashboardTimelineAsync(request?.ToRequest(), cancellationToken).ConfigureAwait(false), value => TypedResults.Ok(value));

    private async Task<HttpResult> DispatchJob(string jobName, [FromBody] JobDispatchRequest request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Dispatching job {JobName}.", jobName);
        var result = await scheduler.DispatchAsync(jobName, request?.Data, request?.Options, cancellationToken).ConfigureAwait(false);
        return this.OkOrProblem(result, value => TypedResults.Accepted($"{this.options.GroupPath}/occurrences/{value.OccurrenceId}", value));
    }

    private async Task<HttpResult> EnableJob(string jobName, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        var job = await this.TryGetJobAsync(jobName, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.NotFound($"Job '{jobName}' was not found.");
        }

        if (job.EffectiveEnabled)
        {
            return this.Problem(HttpStatusCode.Conflict, "/problems/jobs/invalid-state", "Invalid scheduler state", $"Job '{jobName}' is already enabled.");
        }

        return this.OkOrProblem(await scheduler.EnableJobAsync(jobName, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Job '{jobName}' was enabled."));
    }

    private async Task<HttpResult> DisableJob(string jobName, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        var job = await this.TryGetJobAsync(jobName, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.NotFound($"Job '{jobName}' was not found.");
        }

        if (!job.EffectiveEnabled)
        {
            return this.Problem(HttpStatusCode.Conflict, "/problems/jobs/invalid-state", "Invalid scheduler state", $"Job '{jobName}' is already disabled.");
        }

        return this.OkOrProblem(await scheduler.DisableJobAsync(jobName, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Job '{jobName}' was disabled."));
    }

    private async Task<HttpResult> PauseJob(string jobName, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        var job = await this.TryGetJobAsync(jobName, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.NotFound($"Job '{jobName}' was not found.");
        }

        if (job.Paused)
        {
            return this.Problem(HttpStatusCode.Conflict, "/problems/jobs/invalid-state", "Invalid scheduler state", $"Job '{jobName}' is already paused.");
        }

        return this.OkOrProblem(await scheduler.PauseJobAsync(jobName, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Job '{jobName}' was paused."));
    }

    private async Task<HttpResult> ResumeJob(string jobName, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        var job = await this.TryGetJobAsync(jobName, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.NotFound($"Job '{jobName}' was not found.");
        }

        if (!job.Paused)
        {
            return this.Problem(HttpStatusCode.Conflict, "/problems/jobs/invalid-state", "Invalid scheduler state", $"Job '{jobName}' is not paused.");
        }

        return this.OkOrProblem(await scheduler.ResumeJobAsync(jobName, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Job '{jobName}' was resumed."));
    }

    private async Task<HttpResult> PauseTrigger(string jobName, string triggerName, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        var trigger = await this.TryGetTriggerAsync(jobName, triggerName, cancellationToken).ConfigureAwait(false);
        if (trigger is null)
        {
            return TypedResults.NotFound($"Trigger '{triggerName}' on job '{jobName}' was not found.");
        }

        if (trigger.Paused)
        {
            return this.Problem(HttpStatusCode.Conflict, "/problems/jobs/invalid-state", "Invalid scheduler state", $"Trigger '{triggerName}' on job '{jobName}' is already paused.");
        }

        return this.OkOrProblem(await scheduler.PauseTriggerAsync(jobName, triggerName, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Trigger '{triggerName}' on job '{jobName}' was paused."));
    }

    private async Task<HttpResult> ResumeTrigger(string jobName, string triggerName, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        var trigger = await this.TryGetTriggerAsync(jobName, triggerName, cancellationToken).ConfigureAwait(false);
        if (trigger is null)
        {
            return TypedResults.NotFound($"Trigger '{triggerName}' on job '{jobName}' was not found.");
        }

        if (!trigger.Paused)
        {
            return this.Problem(HttpStatusCode.Conflict, "/problems/jobs/invalid-state", "Invalid scheduler state", $"Trigger '{triggerName}' on job '{jobName}' is not paused.");
        }

        return this.OkOrProblem(await scheduler.ResumeTriggerAsync(jobName, triggerName, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Trigger '{triggerName}' on job '{jobName}' was resumed."));
    }

    private async Task<HttpResult> EnableTrigger(string jobName, string triggerName, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        var trigger = await this.TryGetTriggerAsync(jobName, triggerName, cancellationToken).ConfigureAwait(false);
        if (trigger is null)
        {
            return TypedResults.NotFound($"Trigger '{triggerName}' on job '{jobName}' was not found.");
        }

        if (trigger.EffectiveEnabled)
        {
            return this.Problem(HttpStatusCode.Conflict, "/problems/jobs/invalid-state", "Invalid scheduler state", $"Trigger '{triggerName}' on job '{jobName}' is already enabled.");
        }

        return this.OkOrProblem(await scheduler.EnableTriggerAsync(jobName, triggerName, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Trigger '{triggerName}' on job '{jobName}' was enabled."));
    }

    private async Task<HttpResult> DisableTrigger(string jobName, string triggerName, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        var trigger = await this.TryGetTriggerAsync(jobName, triggerName, cancellationToken).ConfigureAwait(false);
        if (trigger is null)
        {
            return TypedResults.NotFound($"Trigger '{triggerName}' on job '{jobName}' was not found.");
        }

        if (!trigger.EffectiveEnabled)
        {
            return this.Problem(HttpStatusCode.Conflict, "/problems/jobs/invalid-state", "Invalid scheduler state", $"Trigger '{triggerName}' on job '{jobName}' is already disabled.");
        }

        return this.OkOrProblem(await scheduler.DisableTriggerAsync(jobName, triggerName, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Trigger '{triggerName}' on job '{jobName}' was disabled."));
    }

    private async Task<HttpResult> PauseOccurrence(Guid occurrenceId, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await scheduler.PauseOccurrenceAsync(occurrenceId, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Occurrence '{occurrenceId}' was paused."));

    private async Task<HttpResult> ResumeOccurrence(Guid occurrenceId, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await scheduler.ResumeOccurrenceAsync(occurrenceId, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Occurrence '{occurrenceId}' was resumed."));

    private async Task<HttpResult> CancelOccurrence(Guid occurrenceId, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await scheduler.CancelOccurrenceAsync(occurrenceId, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Occurrence '{occurrenceId}' cancellation was requested."));

    private async Task<HttpResult> InterruptOccurrence(Guid occurrenceId, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await scheduler.InterruptOccurrenceAsync(occurrenceId, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Occurrence '{occurrenceId}' interruption was requested."));

    private async Task<HttpResult> RetryOccurrence(Guid occurrenceId, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await scheduler.RetryOccurrenceAsync(occurrenceId, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Occurrence '{occurrenceId}' was scheduled for retry."));

    private async Task<HttpResult> ArchiveOccurrence(Guid occurrenceId, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await scheduler.ArchiveOccurrenceAsync(occurrenceId, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Occurrence '{occurrenceId}' was archived."));

    private async Task<HttpResult> ReleaseOccurrenceLease(Guid occurrenceId, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await scheduler.ReleaseOccurrenceLeaseAsync(occurrenceId, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Lease for occurrence '{occurrenceId}' was released."));

    private async Task<HttpResult> RetryOccurrences([FromBody] JobBulkOccurrenceRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await scheduler.RetryOccurrencesAsync(request?.OccurrenceIds, request?.Reason, cancellationToken).ConfigureAwait(false), value => TypedResults.Ok(value));

    private async Task<HttpResult> CancelOccurrences([FromBody] JobBulkOccurrenceRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await scheduler.CancelOccurrencesAsync(request?.OccurrenceIds, request?.Reason, cancellationToken).ConfigureAwait(false), value => TypedResults.Ok(value));

    private async Task<HttpResult> ArchiveOccurrences([FromBody] JobBulkOccurrenceRequest request, CancellationToken cancellationToken)
        => this.OkOrProblem(await scheduler.ArchiveOccurrencesAsync(request?.OccurrenceIds, request?.Reason, cancellationToken).ConfigureAwait(false), value => TypedResults.Ok(value));

    private async Task<HttpResult> CreateBatch([FromBody] JobBatchCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await scheduler.CreateBatchAsync(request, cancellationToken).ConfigureAwait(false);
        return this.OkOrProblem(result, value => TypedResults.Ok(value));
    }

    private async Task<HttpResult> DispatchBatch([FromBody] JobBatchDispatchRequest request, CancellationToken cancellationToken)
    {
        var result = await scheduler.DispatchBatchAsync(request, cancellationToken).ConfigureAwait(false);
        return this.OkOrProblem(result, value => TypedResults.Ok(value));
    }

    private async Task<HttpResult> AttachToBatch(string batchId, [FromBody] JobBatchDispatchRequest request, CancellationToken cancellationToken)
    {
        var batch = await this.TryGetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return TypedResults.NotFound($"Batch '{batchId}' was not found.");
        }

        if (batch.Status == JobBatchStatus.Archived)
        {
            return this.Problem(HttpStatusCode.Conflict, "/problems/jobs/invalid-state", "Invalid scheduler state", $"Batch '{batchId}' is archived and cannot accept additional child occurrences.");
        }

        var result = await scheduler.AttachToBatchAsync(batchId, request, cancellationToken).ConfigureAwait(false);
        return this.OkOrProblem(result, value => TypedResults.Ok(value));
    }

    private async Task<HttpResult> RetryBatch(string batchId, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        if (await this.TryGetBatchAsync(batchId, cancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound($"Batch '{batchId}' was not found.");
        }

        var result = await scheduler.RetryBatchAsync(batchId, request?.Reason, cancellationToken).ConfigureAwait(false);
        return this.OkOrProblem(result, value => TypedResults.Ok(value));
    }

    private async Task<HttpResult> CancelBatch(string batchId, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        if (await this.TryGetBatchAsync(batchId, cancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound($"Batch '{batchId}' was not found.");
        }

        var result = await scheduler.CancelBatchAsync(batchId, request?.Reason, cancellationToken).ConfigureAwait(false);
        return this.OkOrProblem(result, value => TypedResults.Ok(value));
    }

    private async Task<HttpResult> PauseBatch(string batchId, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        if (await this.TryGetBatchAsync(batchId, cancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound($"Batch '{batchId}' was not found.");
        }

        return this.OkOrProblem(await scheduler.PauseBatchAsync(batchId, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Batch '{batchId}' was paused."));
    }

    private async Task<HttpResult> ResumeBatch(string batchId, CancellationToken cancellationToken)
    {
        if (await this.TryGetBatchAsync(batchId, cancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound($"Batch '{batchId}' was not found.");
        }

        return this.OkOrProblem(await scheduler.ResumeBatchAsync(batchId, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Batch '{batchId}' was resumed."));
    }

    private async Task<HttpResult> ArchiveBatch(string batchId, [FromBody] JobReasonRequest request, CancellationToken cancellationToken)
    {
        if (await this.TryGetBatchAsync(batchId, cancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound($"Batch '{batchId}' was not found.");
        }

        return this.OkOrProblem(await scheduler.ArchiveBatchAsync(batchId, request?.Reason, cancellationToken).ConfigureAwait(false), () => TypedResults.Ok($"Batch '{batchId}' was archived."));
    }

    private async Task<HttpResult> PurgeHistory([FromBody] JobPurgeHistoryJobData request, CancellationToken cancellationToken)
        => TypedResults.Ok(await maintenance.PurgeHistoryAsync(request ?? new JobPurgeHistoryJobData(), cancellationToken).ConfigureAwait(false));

    private async Task<HttpResult> ReleaseExpiredLeases([FromBody] JobReleaseExpiredLeasesJobData request, CancellationToken cancellationToken)
        => TypedResults.Ok(await maintenance.ReleaseExpiredLeasesAsync(request ?? new JobReleaseExpiredLeasesJobData(), cancellationToken).ConfigureAwait(false));

    private async Task<HttpResult> RecoverStuckOccurrences([FromBody] JobRecoverStuckOccurrencesJobData request, CancellationToken cancellationToken)
        => TypedResults.Ok(await maintenance.RecoverStuckOccurrencesAsync(request ?? new JobRecoverStuckOccurrencesJobData(), cancellationToken).ConfigureAwait(false));

    private async Task<HttpResult> DetectOrphanedRuntimeState([FromBody] JobDetectOrphanedRuntimeStateJobData request, CancellationToken cancellationToken)
        => TypedResults.Ok(await maintenance.DetectOrphanedRuntimeStateAsync(request ?? new JobDetectOrphanedRuntimeStateJobData(), cancellationToken).ConfigureAwait(false));

    private async Task<JobSchedulerJobModel> TryGetJobAsync(string jobName, CancellationToken cancellationToken)
    {
        var result = await query.QueryJobsAsync(new JobSchedulerJobQueryRequest { JobName = jobName, Take = 1, SortDescending = false }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value.FirstOrDefault(x => string.Equals(x.JobName, jobName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private async Task<JobSchedulerTriggerModel> TryGetTriggerAsync(string jobName, string triggerName, CancellationToken cancellationToken)
    {
        var result = await query.QueryTriggersAsync(new JobSchedulerTriggerQueryRequest { JobName = jobName, TriggerName = triggerName, Take = 10, SortDescending = false }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value.FirstOrDefault(x => string.Equals(x.JobName, jobName, StringComparison.OrdinalIgnoreCase) && string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private async Task<JobSchedulerBatchModel> TryGetBatchAsync(string batchId, CancellationToken cancellationToken)
    {
        var result = await query.QueryBatchesAsync(new JobSchedulerBatchQueryRequest { BatchId = batchId, Take = 1, SortDescending = false }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value.FirstOrDefault(x => string.Equals(x.ExternalBatchId, batchId, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private HttpResult OkOrProblem(Result result, Func<HttpResult> onSuccess)
        => result.IsSuccess ? onSuccess() : this.MapFailure(result.Errors, result.Messages);

    private HttpResult OkOrProblem<T>(Result<T> result, Func<T, HttpResult> onSuccess)
        where T : class
        => result.IsSuccess ? onSuccess(result.Value) : this.MapFailure(result.Errors, result.Messages);

    private HttpResult OkOrProblem<T>(ResultPaged<T> result, Func<ResultPaged<T>, HttpResult> onSuccess)
        => result.IsSuccess ? onSuccess(result) : this.MapFailure(result.Errors, result.Messages);

    private HttpResult MapFailure(IReadOnlyList<IResultError> errors, IReadOnlyList<string> messages)
    {
        var status = this.ResolveStatusCode(errors);
        var type = status switch
        {
            HttpStatusCode.BadRequest => "/problems/jobs/validation",
            HttpStatusCode.NotFound => "/problems/jobs/not-found",
            HttpStatusCode.Conflict => "/problems/jobs/invalid-state",
            _ => "/problems/jobs/operation-failed",
        };

        var title = status switch
        {
            HttpStatusCode.BadRequest => "Invalid jobs request",
            HttpStatusCode.NotFound => "Jobs resource not found",
            HttpStatusCode.Conflict => "Invalid scheduler state",
            _ => "Jobs operation failed",
        };

        var detail = string.Join(" ", errors.Select(x => x.Message).Concat(messages).Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = "The jobs operation failed.";
        }

        return this.Problem(status, type, title, detail);
    }

    private HttpStatusCode ResolveStatusCode(IReadOnlyList<IResultError> errors)
    {
        if (errors.OfType<ConcurrencyError>().Any() || errors.OfType<ConflictError>().Any())
        {
            return HttpStatusCode.Conflict;
        }

        var validationMessages = errors.OfType<ValidationError>().Select(x => x.Message).ToArray();
        if (validationMessages.Any(IsNotFoundMessage))
        {
            return HttpStatusCode.NotFound;
        }

        if (validationMessages.Any(IsConflictMessage))
        {
            return HttpStatusCode.Conflict;
        }

        if (validationMessages.Length > 0)
        {
            return HttpStatusCode.BadRequest;
        }

        return HttpStatusCode.InternalServerError;
    }

    private static bool IsNotFoundMessage(string message)
        => !string.IsNullOrWhiteSpace(message)
            && (message.Contains("was not found", StringComparison.OrdinalIgnoreCase)
                || message.Contains("is not registered", StringComparison.OrdinalIgnoreCase));

    private static bool IsConflictMessage(string message)
        => !string.IsNullOrWhiteSpace(message)
            && (message.Contains("already", StringComparison.OrdinalIgnoreCase)
                || message.Contains("cannot", StringComparison.OrdinalIgnoreCase)
                || message.Contains("is not paused", StringComparison.OrdinalIgnoreCase)
                || message.Contains("is paused", StringComparison.OrdinalIgnoreCase)
                || message.Contains("is not running", StringComparison.OrdinalIgnoreCase)
                || message.Contains("archived", StringComparison.OrdinalIgnoreCase));

    private HttpResult Problem(HttpStatusCode status, string type, string title, string detail)
        => TypedResults.Problem(detail: detail, statusCode: (int)status, title: title, type: type);

    private class JobDispatchRequest
    {
        public object Data { get; set; }

        public JobDispatchOptions Options { get; set; }
    }

    private sealed class JobReasonRequest
    {
        public string Reason { get; set; }
    }
}
