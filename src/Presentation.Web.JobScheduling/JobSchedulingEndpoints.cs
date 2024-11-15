// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.JobScheduling;

using System.Net;
using Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Quartz;
using Quartz.Impl.Matchers;
using Constants = BridgingIT.DevKit.Application.JobScheduling.Constants;
using IResult = Microsoft.AspNetCore.Http.IResult;

public class JobSchedulingEndpoints(
    ISchedulerFactory schedulerFactory,
    JobSchedulingEndpointsOptions options = null) : EndpointsBase
{
    private readonly ISchedulerFactory schedulerFactory = schedulerFactory;
    private readonly JobSchedulingEndpointsOptions options = options ?? new JobSchedulingEndpointsOptions();

    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(this.options.GroupPrefix).WithTags(this.options.GroupTag);

        if (this.options.RequireAuthorization)
        {
            group.RequireAuthorization();
        }

        group.MapGet(string.Empty, this.GetJobs)
            //.AllowAnonymous()
            .Produces<IEnumerable<JobModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        group.MapPost("{name}", this.PostJob)
            //.AllowAnonymous()
            .Produces<IEnumerable<JobModel>>((int)HttpStatusCode.Accepted)
            .Produces<IEnumerable<JobModel>>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);
    }

    private async Task<IResult> GetJobs(CancellationToken cancellationToken)
    {
        return Results.Ok(await this.GetAllJobs(await this.schedulerFactory.GetScheduler(cancellationToken),
            cancellationToken));
    }

    private async Task<IResult> PostJob(string name, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var scheduler = await this.schedulerFactory.GetScheduler(cancellationToken);
        if (!(await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken)).Any(j => j.Name == name))
        {
            return Results.NotFound();
        }

        var data = new JobDataMap
        {
            [Constants.CorrelationIdKey] = httpContext.TryGetCorrelationId(),
            [Constants.TriggeredByKey] = nameof(JobSchedulingEndpoints) // or CurrentUserService
        };
        await scheduler.TriggerJob(new JobKey(name), data, cancellationToken);
        await Task.Delay(300, cancellationToken); // TODO: job properties are only available after job has finished
        var job = (await this.GetAllJobs(scheduler, cancellationToken))?.FirstOrDefault(j => j.Name == name);

        return Results.Accepted(null, job);
    }

    private async Task<IEnumerable<JobModel>> GetAllJobs(IScheduler scheduler, CancellationToken cancellationToken)
    {
        var results = new List<JobModel>();
        var jobGroups = await scheduler.GetJobGroupNames(cancellationToken);
        var triggerGroups = await scheduler.GetTriggerGroupNames(cancellationToken);
        var executingJobs = await scheduler.GetCurrentlyExecutingJobs(cancellationToken);

        foreach (var group in jobGroups.SafeNull())
        {
            var groupMatcher = GroupMatcher<JobKey>.GroupContains(group);
            var jobKeys = await scheduler.GetJobKeys(groupMatcher, cancellationToken);

            foreach (var jobKey in jobKeys.SafeNull())
            {
                var job = await scheduler.GetJobDetail(jobKey, cancellationToken);
                var triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken);
                var props = job.JobDataMap.ToDictionary();

                foreach (var trigger in triggers.SafeNull())
                {
                    var jobInfo = new JobModel
                    {
                        Group = group,
                        Name = jobKey.Name,
                        Description = $"{job.Description} ({trigger.Description})",
                        Type = job.JobType.FullName,
                        TriggerName = trigger.Key.Name,
                        TriggerGroup = trigger.Key.Group,
                        TriggerType = trigger.GetType().Name,
                        TriggerState = (await scheduler.GetTriggerState(trigger.Key, cancellationToken)).ToString(),
                        NextFireTime = trigger.GetNextFireTimeUtc(),
                        PreviousFireTime = trigger.GetPreviousFireTimeUtc(),
                        CurrentlyExecuting = executingJobs.SafeWhere(j => j.JobDetail.Key.Name == job.Key.Name)
                            .SafeAny(),
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly
                        Properties = job.JobDataMap?.ToDictionary() ?? []
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly
                    };
                    results.Add(jobInfo);
                }
            }
        }

        return results;
    }
}