// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides the default runtime-neutral trigger evaluation implementation.
/// </summary>
public class JobTriggerEvaluator(
    TimeProvider timeProvider,
    IJobCronEngine cronEngine,
    IJobCalendarEngine calendarEngine,
    IServiceProvider serviceProvider) : IJobTriggerEvaluator
{
    /// <inheritdoc />
    public Result<JobTriggerEvaluationResult> Materialize(
        JobDefinition job,
        JobTriggerDefinition trigger,
        JobTriggerEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(request);

        var runtimeState = request.RuntimeState ?? JobTriggerRuntimeState.Empty;
        var triggerEnabled = runtimeState.Enabled ?? trigger.Enabled;

        if (!triggerEnabled)
        {
            return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(runtimeState, []));
        }

        var nowUtc = timeProvider.GetUtcNow().ToUniversalTime();

        return trigger.TriggerType switch
        {
            JobTriggerType.Manual => this.MaterializeManual(job, trigger, request, runtimeState, nowUtc),
            JobTriggerType.OneTime => this.MaterializeSingleFire(job, trigger, request, runtimeState, nowUtc, trigger.DueUtc),
            JobTriggerType.Delayed => this.MaterializeSingleFire(job, trigger, request, runtimeState, nowUtc, ResolveDelayedDueUtc(trigger, runtimeState, request, nowUtc, false)),
            JobTriggerType.StartupDelay => this.MaterializeSingleFire(job, trigger, request, runtimeState, nowUtc, ResolveDelayedDueUtc(trigger, runtimeState, request, nowUtc, true)),
            JobTriggerType.Cron => this.MaterializeCron(job, trigger, request, runtimeState, nowUtc),
            JobTriggerType.Calendar => this.MaterializeCalendar(job, trigger, request, runtimeState, nowUtc),
            JobTriggerType.Custom => this.MaterializeCustom(job, trigger, request, runtimeState, nowUtc),
            _ => Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(runtimeState, [])),
        };
    }

    private Result<JobTriggerEvaluationResult> MaterializeManual(
        JobDefinition job,
        JobTriggerDefinition trigger,
        JobTriggerEvaluationRequest request,
        JobTriggerRuntimeState runtimeState,
        DateTimeOffset nowUtc)
    {
        if (!request.ManualDispatchRequested)
        {
            return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(runtimeState, []));
        }

        var dueUtc = (request.DispatchRequestedUtc ?? nowUtc).ToUniversalTime();
        return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(
            runtimeState with { ActivatedUtc = runtimeState.ActivatedUtc ?? dueUtc },
            [CreateOccurrence(job, trigger, dueUtc, null, request.DispatchIdentity, request.OverrideData, request.OverrideProperties)]));
    }

    private Result<JobTriggerEvaluationResult> MaterializeSingleFire(
        JobDefinition job,
        JobTriggerDefinition trigger,
        JobTriggerEvaluationRequest request,
        JobTriggerRuntimeState runtimeState,
        DateTimeOffset nowUtc,
        DateTimeOffset? dueUtc)
    {
        if (dueUtc is null)
        {
            return Result<JobTriggerEvaluationResult>.Failure().WithError(new ValidationError($"The trigger '{trigger.TriggerName}' requires a due UTC instant."));
        }

        var normalizedDueUtc = dueUtc.Value.ToUniversalTime();
        var nextState = runtimeState with
        {
            ActivatedUtc = runtimeState.ActivatedUtc ?? request.ActivationUtc ?? request.SchedulerStartedUtc ?? nowUtc,
            DueUtc = runtimeState.DueUtc ?? normalizedDueUtc,
        };

        if (runtimeState.HasMaterializedOccurrence || normalizedDueUtc > nowUtc)
        {
            return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(nextState, []));
        }

        if (normalizedDueUtc < nowUtc && trigger.MissedOccurrencePolicy == JobMissedOccurrencePolicy.Skip)
        {
            return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(
                nextState with { HasMaterializedOccurrence = true, LastMaterializedScheduledUtc = normalizedDueUtc },
                []));
        }

        return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(
            nextState with { HasMaterializedOccurrence = true, LastMaterializedScheduledUtc = normalizedDueUtc },
            [CreateOccurrence(job, trigger, normalizedDueUtc, normalizedDueUtc, null, null, null)]));
    }

    private Result<JobTriggerEvaluationResult> MaterializeCron(
        JobDefinition job,
        JobTriggerDefinition trigger,
        JobTriggerEvaluationRequest request,
        JobTriggerRuntimeState runtimeState,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(trigger.Schedule))
        {
            return Result<JobTriggerEvaluationResult>.Failure().WithError(new ValidationError($"The cron trigger '{trigger.TriggerName}' requires a schedule."));
        }

        var activatedUtc = runtimeState.ActivatedUtc ?? request.ActivationUtc ?? nowUtc;
        var fromUtc = runtimeState.LastMaterializedScheduledUtc ?? activatedUtc;
        var occurrencesResult = cronEngine.GetOccurrencesUtc(
            trigger.Schedule,
            fromUtc,
            nowUtc,
            trigger.TimeZone,
            runtimeState.LastMaterializedScheduledUtc is null,
            true);

        if (!occurrencesResult.IsSuccess)
        {
            return Result<JobTriggerEvaluationResult>.Failure().WithErrors(occurrencesResult.Errors);
        }

        var occurrences = occurrencesResult.Value;
        if (occurrences.Count == 0)
        {
            return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(runtimeState with { ActivatedUtc = activatedUtc }, []));
        }

        var selected = trigger.MissedOccurrencePolicy switch
        {
            JobMissedOccurrencePolicy.Skip => Array.Empty<DateTimeOffset>(),
            JobMissedOccurrencePolicy.RunOnce => [occurrences[^1]],
            _ => occurrences.Take(request.MaxCatchUpOccurrences).ToArray(),
        };

        var lastCoveredUtc = trigger.MissedOccurrencePolicy == JobMissedOccurrencePolicy.Skip
            ? occurrences[^1]
            : selected.Length > 0 ? selected[^1] : runtimeState.LastMaterializedScheduledUtc;

        var materializedOccurrences = selected
            .Select(x => CreateOccurrence(job, trigger, x, x, null, null, null))
            .DistinctBy(x => x.OccurrenceKey)
            .ToArray();

        return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(
            runtimeState with
            {
                ActivatedUtc = activatedUtc,
                LastMaterializedScheduledUtc = lastCoveredUtc,
            },
            materializedOccurrences));
    }

    private Result<JobTriggerEvaluationResult> MaterializeCalendar(
        JobDefinition job,
        JobTriggerDefinition trigger,
        JobTriggerEvaluationRequest request,
        JobTriggerRuntimeState runtimeState,
        DateTimeOffset nowUtc)
    {
        if (trigger.Calendar is null)
        {
            return Result<JobTriggerEvaluationResult>.Failure().WithError(new ValidationError($"The calendar trigger '{trigger.TriggerName}' requires a calendar definition."));
        }

        var activatedUtc = runtimeState.ActivatedUtc ?? request.ActivationUtc ?? nowUtc;
        var fromUtc = runtimeState.LastMaterializedScheduledUtc ?? activatedUtc;
        var occurrencesResult = calendarEngine.GetOccurrencesUtc(
            trigger.Calendar,
            fromUtc,
            nowUtc,
            trigger.TimeZone,
            runtimeState.LastMaterializedScheduledUtc is null,
            true);

        if (!occurrencesResult.IsSuccess)
        {
            return Result<JobTriggerEvaluationResult>.Failure().WithErrors(occurrencesResult.Errors);
        }

        var occurrences = occurrencesResult.Value;
        if (occurrences.Count == 0)
        {
            return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(runtimeState with { ActivatedUtc = activatedUtc }, []));
        }

        var selected = trigger.MissedOccurrencePolicy switch
        {
            JobMissedOccurrencePolicy.Skip => Array.Empty<DateTimeOffset>(),
            JobMissedOccurrencePolicy.RunOnce => [occurrences[^1]],
            _ => occurrences.Take(request.MaxCatchUpOccurrences).ToArray(),
        };

        var lastCoveredUtc = trigger.MissedOccurrencePolicy == JobMissedOccurrencePolicy.Skip
            ? occurrences[^1]
            : selected.Length > 0 ? selected[^1] : runtimeState.LastMaterializedScheduledUtc;

        var materializedOccurrences = selected
            .Select(x => CreateOccurrence(job, trigger, x, x, null, null, null))
            .DistinctBy(x => x.OccurrenceKey)
            .ToArray();

        return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(
            runtimeState with
            {
                ActivatedUtc = activatedUtc,
                LastMaterializedScheduledUtc = lastCoveredUtc,
            },
            materializedOccurrences));
    }

    private Result<JobTriggerEvaluationResult> MaterializeCustom(
        JobDefinition job,
        JobTriggerDefinition trigger,
        JobTriggerEvaluationRequest request,
        JobTriggerRuntimeState runtimeState,
        DateTimeOffset nowUtc)
    {
        if (trigger.CustomTriggerProviderType is null)
        {
            return Result<JobTriggerEvaluationResult>.Failure().WithError(new ValidationError($"The custom trigger '{trigger.TriggerName}' does not declare a provider type."));
        }

        if (serviceProvider.GetService(trigger.CustomTriggerProviderType) is not IJobCustomTriggerProvider provider)
        {
            return Result<JobTriggerEvaluationResult>.Failure().WithError(new ValidationError($"The custom trigger provider '{trigger.CustomTriggerProviderType.FullName}' is not registered."));
        }

        return provider.Materialize(new JobTriggerEvaluationContext(job, trigger, request, runtimeState, nowUtc));
    }

    private static DateTimeOffset? ResolveDelayedDueUtc(
        JobTriggerDefinition trigger,
        JobTriggerRuntimeState runtimeState,
        JobTriggerEvaluationRequest request,
        DateTimeOffset nowUtc,
        bool useSchedulerStartedUtc)
    {
        if (runtimeState.DueUtc is not null)
        {
            return runtimeState.DueUtc.Value.ToUniversalTime();
        }

        if (trigger.Delay is null)
        {
            return null;
        }

        var anchorUtc = useSchedulerStartedUtc
            ? request.SchedulerStartedUtc ?? runtimeState.ActivatedUtc ?? nowUtc
            : request.ActivationUtc ?? runtimeState.ActivatedUtc ?? nowUtc;

        return anchorUtc.ToUniversalTime().Add(trigger.Delay.Value);
    }

    private static JobOccurrenceMaterialization CreateOccurrence(
        JobDefinition job,
        JobTriggerDefinition trigger,
        DateTimeOffset dueUtc,
        DateTimeOffset? scheduledUtc,
        string identity,
        object overrideData,
        PropertyBag overrideProperties)
    {
        var effectiveData = overrideData ?? trigger.Data;
        var properties = trigger.Properties?.Clone() ?? new PropertyBag();
        properties.Merge(overrideProperties);

        var occurrenceKey = JobOccurrenceKeyFactory.Create(job.JobName, trigger.TriggerName, trigger.TriggerType, dueUtc, scheduledUtc, identity);
        return new JobOccurrenceMaterialization(
            occurrenceKey,
            job.JobName,
            trigger.TriggerName,
            trigger.TriggerType,
            dueUtc,
            scheduledUtc,
            effectiveData,
            trigger.DataType,
            properties,
            occurrenceKey);
    }
}