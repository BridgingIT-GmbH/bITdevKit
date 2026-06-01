namespace BridgingIT.DevKit.Presentation.Web.Jobs;

using System.Reflection;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

internal abstract class JobSchedulerPagedQueryModel
{
    public int Skip { get; set; }

    public int Take { get; set; } = 50;

    public string SortBy { get; set; }

    public bool SortDescending { get; set; } = true;

    internal static void BindPaged(IQueryCollection query, JobSchedulerPagedQueryModel model)
    {
        model.Skip = ParseInt32(query, "skip") ?? 0;
        model.Take = ParseInt32(query, "take") ?? 50;
        model.SortBy = GetString(query, "sortBy");
        model.SortDescending = ParseBoolean(query, "sortDescending") ?? true;
    }

    internal static string GetString(IQueryCollection query, params string[] names)
    {
        foreach (var name in names)
        {
            if (query.TryGetValue(name, out var value) && !StringValues.IsNullOrEmpty(value))
            {
                return value.ToString();
            }
        }

        return null;
    }

    internal static Guid? ParseGuid(IQueryCollection query, params string[] names)
    {
        var value = GetString(query, names);
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    internal static int? ParseInt32(IQueryCollection query, params string[] names)
    {
        var value = GetString(query, names);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    internal static bool? ParseBoolean(IQueryCollection query, params string[] names)
    {
        var value = GetString(query, names);
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    internal static DateTimeOffset? ParseDateTimeOffset(IQueryCollection query, params string[] names)
    {
        var value = GetString(query, names);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    internal static TEnum? ParseEnum<TEnum>(IQueryCollection query, params string[] names)
        where TEnum : struct, Enum
    {
        var value = GetString(query, names);
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : null;
    }

    internal static TEnum[] ParseEnumValues<TEnum>(IQueryCollection query, params string[] names)
        where TEnum : struct, Enum
        => [.. ParseValues(query, names)
            .Select(value => Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : (TEnum?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)];

    internal static string[] ParseStringValues(IQueryCollection query, params string[] names)
        => [.. ParseValues(query, names)];

    private static IEnumerable<string> ParseValues(IQueryCollection query, params string[] names)
    {
        foreach (var name in names)
        {
            if (!query.TryGetValue(name, out var values) || StringValues.IsNullOrEmpty(values))
            {
                continue;
            }

            return values
                .SelectMany(static value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return [];
    }
}

internal sealed class JobSchedulerTriggerQueryModel : JobSchedulerPagedQueryModel
{
    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public JobTriggerType[] TriggerTypes { get; set; } = [];

    public bool? Enabled { get; set; }

    public bool? Paused { get; set; }

    public static ValueTask<JobSchedulerTriggerQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;
        var model = new JobSchedulerTriggerQueryModel
        {
            JobName = GetString(query, "jobName"),
            TriggerName = GetString(query, "triggerName"),
            TriggerTypes = ParseEnumValues<JobTriggerType>(query, "triggerTypes"),
            Enabled = ParseBoolean(query, "enabled"),
            Paused = ParseBoolean(query, "paused"),
        };

        BindPaged(query, model);
        return ValueTask.FromResult(model);
    }

    public JobSchedulerTriggerQueryRequest ToRequest() => new()
    {
        JobName = this.JobName,
        TriggerName = this.TriggerName,
        TriggerTypes = this.TriggerTypes,
        Enabled = this.Enabled,
        Paused = this.Paused,
        Skip = this.Skip,
        Take = this.Take,
        SortBy = this.SortBy,
        SortDescending = this.SortDescending,
    };

    public JobSchedulerRecurringTriggerQueryRequest ToRecurringRequest() => new()
    {
        JobName = this.JobName,
        TriggerName = this.TriggerName,
        TriggerTypes = this.TriggerTypes,
        Enabled = this.Enabled,
        Paused = this.Paused,
        Skip = this.Skip,
        Take = this.Take,
        SortBy = this.SortBy,
        SortDescending = this.SortDescending,
    };
}

internal sealed class JobSchedulerOccurrenceQueryModel : JobSchedulerPagedQueryModel
{
    public Guid? OccurrenceId { get; set; }

    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public JobTriggerType? TriggerType { get; set; }

    public JobOccurrenceStatus[] Statuses { get; set; } = [];

    public string CorrelationId { get; set; }

    public string IdempotencyKey { get; set; }

    public string SchedulerInstanceId { get; set; }

    public DateTimeOffset? DueFrom { get; set; }

    public DateTimeOffset? DueTo { get; set; }

    public DateTimeOffset? StartedFrom { get; set; }

    public DateTimeOffset? StartedTo { get; set; }

    public DateTimeOffset? CompletedFrom { get; set; }

    public DateTimeOffset? CompletedTo { get; set; }

    public DateTimeOffset? CreatedFromUtc { get; set; }

    public DateTimeOffset? CreatedToUtc { get; set; }

    public static ValueTask<JobSchedulerOccurrenceQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;
        var model = new JobSchedulerOccurrenceQueryModel
        {
            OccurrenceId = ParseGuid(query, "occurrenceId"),
            JobName = GetString(query, "jobName"),
            TriggerName = GetString(query, "triggerName"),
            TriggerType = ParseEnum<JobTriggerType>(query, "triggerType"),
            Statuses = ParseEnumValues<JobOccurrenceStatus>(query, "statuses"),
            CorrelationId = GetString(query, "correlationId"),
            IdempotencyKey = GetString(query, "idempotencyKey"),
            SchedulerInstanceId = GetString(query, "schedulerInstanceId"),
            DueFrom = ParseDateTimeOffset(query, "dueFrom", "dueFromUtc"),
            DueTo = ParseDateTimeOffset(query, "dueTo", "dueToUtc"),
            StartedFrom = ParseDateTimeOffset(query, "startedFrom", "startedFromUtc"),
            StartedTo = ParseDateTimeOffset(query, "startedTo", "startedToUtc"),
            CompletedFrom = ParseDateTimeOffset(query, "completedFrom", "completedFromUtc"),
            CompletedTo = ParseDateTimeOffset(query, "completedTo", "completedToUtc"),
            CreatedFromUtc = ParseDateTimeOffset(query, "createdFromUtc"),
            CreatedToUtc = ParseDateTimeOffset(query, "createdToUtc"),
        };

        BindPaged(query, model);
        return ValueTask.FromResult(model);
    }

    public JobSchedulerOccurrenceQueryRequest ToRequest() => new()
    {
        OccurrenceId = this.OccurrenceId,
        JobName = this.JobName,
        TriggerName = this.TriggerName,
        TriggerType = this.TriggerType,
        Statuses = this.Statuses,
        CorrelationId = this.CorrelationId,
        IdempotencyKey = this.IdempotencyKey,
        SchedulerInstanceId = this.SchedulerInstanceId,
        DueFrom = this.DueFrom,
        DueTo = this.DueTo,
        StartedFrom = this.StartedFrom,
        StartedTo = this.StartedTo,
        CompletedFrom = this.CompletedFrom,
        CompletedTo = this.CompletedTo,
        CreatedFromUtc = this.CreatedFromUtc,
        CreatedToUtc = this.CreatedToUtc,
        Skip = this.Skip,
        Take = this.Take,
        SortBy = this.SortBy,
        SortDescending = this.SortDescending,
    };
}

internal sealed class JobSchedulerBatchQueryModel : JobSchedulerPagedQueryModel
{
    public string BatchId { get; set; }

    public string CorrelationId { get; set; }

    public string IdempotencyKey { get; set; }

    public JobBatchStatus[] Statuses { get; set; } = [];

    public DateTimeOffset? CreatedFromUtc { get; set; }

    public DateTimeOffset? CreatedToUtc { get; set; }

    public static ValueTask<JobSchedulerBatchQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;
        var model = new JobSchedulerBatchQueryModel
        {
            BatchId = GetString(query, "batchId"),
            CorrelationId = GetString(query, "correlationId"),
            IdempotencyKey = GetString(query, "idempotencyKey"),
            Statuses = ParseEnumValues<JobBatchStatus>(query, "statuses"),
            CreatedFromUtc = ParseDateTimeOffset(query, "createdFromUtc"),
            CreatedToUtc = ParseDateTimeOffset(query, "createdToUtc"),
        };

        BindPaged(query, model);
        return ValueTask.FromResult(model);
    }

    public JobSchedulerBatchQueryRequest ToRequest() => new()
    {
        BatchId = this.BatchId,
        CorrelationId = this.CorrelationId,
        IdempotencyKey = this.IdempotencyKey,
        Statuses = this.Statuses,
        CreatedFromUtc = this.CreatedFromUtc,
        CreatedToUtc = this.CreatedToUtc,
        Skip = this.Skip,
        Take = this.Take,
        SortBy = this.SortBy,
        SortDescending = this.SortDescending,
    };
}

internal sealed class JobSchedulerBatchOccurrenceQueryModel : JobSchedulerPagedQueryModel
{
    public JobOccurrenceStatus[] Statuses { get; set; } = [];

    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public static ValueTask<JobSchedulerBatchOccurrenceQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;
        var model = new JobSchedulerBatchOccurrenceQueryModel
        {
            Statuses = ParseEnumValues<JobOccurrenceStatus>(query, "statuses"),
            JobName = GetString(query, "jobName"),
            TriggerName = GetString(query, "triggerName"),
        };

        BindPaged(query, model);
        return ValueTask.FromResult(model);
    }

    public JobSchedulerBatchOccurrenceQueryRequest ToRequest() => new()
    {
        Statuses = this.Statuses,
        JobName = this.JobName,
        TriggerName = this.TriggerName,
        Skip = this.Skip,
        Take = this.Take,
        SortBy = this.SortBy,
        SortDescending = this.SortDescending,
    };
}

internal sealed class JobSchedulerBatchHistoryQueryModel : JobSchedulerPagedQueryModel
{
    public string EventName { get; set; }

    public JobBatchStatus[] BatchStatuses { get; set; } = [];

    public string SchedulerInstanceId { get; set; }

    public DateTimeOffset? RecordedFromUtc { get; set; }

    public DateTimeOffset? RecordedToUtc { get; set; }

    public static ValueTask<JobSchedulerBatchHistoryQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;
        var model = new JobSchedulerBatchHistoryQueryModel
        {
            EventName = GetString(query, "eventName"),
            BatchStatuses = ParseEnumValues<JobBatchStatus>(query, "batchStatuses", "statuses"),
            SchedulerInstanceId = GetString(query, "schedulerInstanceId"),
            RecordedFromUtc = ParseDateTimeOffset(query, "recordedFromUtc"),
            RecordedToUtc = ParseDateTimeOffset(query, "recordedToUtc"),
        };

        BindPaged(query, model);
        return ValueTask.FromResult(model);
    }

    public JobSchedulerBatchHistoryQueryRequest ToRequest() => new()
    {
        EventName = this.EventName,
        BatchStatuses = this.BatchStatuses,
        SchedulerInstanceId = this.SchedulerInstanceId,
        RecordedFromUtc = this.RecordedFromUtc,
        RecordedToUtc = this.RecordedToUtc,
        Skip = this.Skip,
        Take = this.Take,
        SortBy = this.SortBy,
        SortDescending = this.SortDescending,
    };
}

internal sealed class JobSchedulerDependencyQueryModel : JobSchedulerPagedQueryModel
{
    public Guid? DependencyId { get; set; }

    public Guid? OccurrenceId { get; set; }

    public Guid? DependentOccurrenceId { get; set; }

    public Guid? PrerequisiteOccurrenceId { get; set; }

    public JobDependencyStatus[] Statuses { get; set; } = [];

    public JobDependencyFailurePolicy[] FailurePolicies { get; set; } = [];

    public DateTimeOffset? CreatedFromUtc { get; set; }

    public DateTimeOffset? CreatedToUtc { get; set; }

    public static ValueTask<JobSchedulerDependencyQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;
        var model = new JobSchedulerDependencyQueryModel
        {
            DependencyId = ParseGuid(query, "dependencyId"),
            OccurrenceId = ParseGuid(query, "occurrenceId"),
            DependentOccurrenceId = ParseGuid(query, "dependentOccurrenceId"),
            PrerequisiteOccurrenceId = ParseGuid(query, "prerequisiteOccurrenceId"),
            Statuses = ParseEnumValues<JobDependencyStatus>(query, "statuses"),
            FailurePolicies = ParseEnumValues<JobDependencyFailurePolicy>(query, "failurePolicies"),
            CreatedFromUtc = ParseDateTimeOffset(query, "createdFromUtc"),
            CreatedToUtc = ParseDateTimeOffset(query, "createdToUtc"),
        };

        BindPaged(query, model);
        return ValueTask.FromResult(model);
    }

    public JobSchedulerDependencyQueryRequest ToRequest() => new()
    {
        DependencyId = this.DependencyId,
        OccurrenceId = this.OccurrenceId,
        DependentOccurrenceId = this.DependentOccurrenceId,
        PrerequisiteOccurrenceId = this.PrerequisiteOccurrenceId,
        Statuses = this.Statuses,
        FailurePolicies = this.FailurePolicies,
        CreatedFromUtc = this.CreatedFromUtc,
        CreatedToUtc = this.CreatedToUtc,
        Skip = this.Skip,
        Take = this.Take,
        SortBy = this.SortBy,
        SortDescending = this.SortDescending,
    };
}

internal sealed class JobSchedulerExecutionQueryModel : JobSchedulerPagedQueryModel
{
    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public JobTriggerType? TriggerType { get; set; }

    public JobExecutionStatus[] Statuses { get; set; } = [];

    public string SchedulerInstanceId { get; set; }

    public string CorrelationId { get; set; }

    public string IdempotencyKey { get; set; }

    public DateTimeOffset? DueFrom { get; set; }

    public DateTimeOffset? DueTo { get; set; }

    public DateTimeOffset? StartedFrom { get; set; }

    public DateTimeOffset? StartedTo { get; set; }

    public DateTimeOffset? CompletedFrom { get; set; }

    public DateTimeOffset? CompletedTo { get; set; }

    public static ValueTask<JobSchedulerExecutionQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;
        var model = new JobSchedulerExecutionQueryModel
        {
            JobName = GetString(query, "jobName"),
            TriggerName = GetString(query, "triggerName"),
            TriggerType = ParseEnum<JobTriggerType>(query, "triggerType"),
            Statuses = ParseEnumValues<JobExecutionStatus>(query, "statuses"),
            SchedulerInstanceId = GetString(query, "schedulerInstanceId"),
            CorrelationId = GetString(query, "correlationId"),
            IdempotencyKey = GetString(query, "idempotencyKey"),
            DueFrom = ParseDateTimeOffset(query, "dueFrom"),
            DueTo = ParseDateTimeOffset(query, "dueTo"),
            StartedFrom = ParseDateTimeOffset(query, "startedFrom", "startedFromUtc"),
            StartedTo = ParseDateTimeOffset(query, "startedTo", "startedToUtc"),
            CompletedFrom = ParseDateTimeOffset(query, "completedFrom", "completedFromUtc"),
            CompletedTo = ParseDateTimeOffset(query, "completedTo", "completedToUtc"),
        };

        BindPaged(query, model);
        return ValueTask.FromResult(model);
    }

    public JobSchedulerExecutionQueryRequest ToRequest() => new()
    {
        JobName = this.JobName,
        TriggerName = this.TriggerName,
        TriggerType = this.TriggerType,
        Statuses = this.Statuses,
        SchedulerInstanceId = this.SchedulerInstanceId,
        CorrelationId = this.CorrelationId,
        IdempotencyKey = this.IdempotencyKey,
        DueFrom = this.DueFrom,
        DueTo = this.DueTo,
        StartedFrom = this.StartedFrom,
        StartedTo = this.StartedTo,
        CompletedFrom = this.CompletedFrom,
        CompletedTo = this.CompletedTo,
        Skip = this.Skip,
        Take = this.Take,
        SortBy = this.SortBy,
        SortDescending = this.SortDescending,
    };
}

internal sealed class JobSchedulerExecutionHistoryQueryModel : JobSchedulerPagedQueryModel
{
    public Guid? OccurrenceId { get; set; }

    public Guid? ExecutionId { get; set; }

    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public string SchedulerInstanceId { get; set; }

    public JobOccurrenceStatus[] OccurrenceStatuses { get; set; } = [];

    public JobExecutionStatus[] ExecutionStatuses { get; set; } = [];

    public string[] EventNames { get; set; } = [];

    public DateTimeOffset? RecordedFromUtc { get; set; }

    public DateTimeOffset? RecordedToUtc { get; set; }

    public static ValueTask<JobSchedulerExecutionHistoryQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;
        var model = new JobSchedulerExecutionHistoryQueryModel
        {
            OccurrenceId = ParseGuid(query, "occurrenceId"),
            ExecutionId = ParseGuid(query, "executionId"),
            JobName = GetString(query, "jobName"),
            TriggerName = GetString(query, "triggerName"),
            SchedulerInstanceId = GetString(query, "schedulerInstanceId"),
            OccurrenceStatuses = ParseEnumValues<JobOccurrenceStatus>(query, "occurrenceStatuses"),
            ExecutionStatuses = ParseEnumValues<JobExecutionStatus>(query, "executionStatuses"),
            EventNames = ParseStringValues(query, "eventNames"),
            RecordedFromUtc = ParseDateTimeOffset(query, "recordedFromUtc"),
            RecordedToUtc = ParseDateTimeOffset(query, "recordedToUtc"),
        };

        BindPaged(query, model);
        return ValueTask.FromResult(model);
    }

    public JobSchedulerExecutionHistoryQueryRequest ToRequest() => new()
    {
        OccurrenceId = this.OccurrenceId,
        ExecutionId = this.ExecutionId,
        JobName = this.JobName,
        TriggerName = this.TriggerName,
        SchedulerInstanceId = this.SchedulerInstanceId,
        OccurrenceStatuses = this.OccurrenceStatuses,
        ExecutionStatuses = this.ExecutionStatuses,
        EventNames = this.EventNames,
        RecordedFromUtc = this.RecordedFromUtc,
        RecordedToUtc = this.RecordedToUtc,
        Skip = this.Skip,
        Take = this.Take,
        SortBy = this.SortBy,
        SortDescending = this.SortDescending,
    };
}

internal sealed class JobSchedulerLeaseQueryModel : JobSchedulerPagedQueryModel
{
    public string SchedulerInstanceId { get; set; }

    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public JobSchedulerLeaseStatus[] Statuses { get; set; } = [];

    public DateTimeOffset? ExpiresFromUtc { get; set; }

    public DateTimeOffset? ExpiresToUtc { get; set; }

    public static ValueTask<JobSchedulerLeaseQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;
        var model = new JobSchedulerLeaseQueryModel
        {
            SchedulerInstanceId = GetString(query, "schedulerInstanceId"),
            JobName = GetString(query, "jobName"),
            TriggerName = GetString(query, "triggerName"),
            Statuses = ParseEnumValues<JobSchedulerLeaseStatus>(query, "statuses"),
            ExpiresFromUtc = ParseDateTimeOffset(query, "expiresFromUtc"),
            ExpiresToUtc = ParseDateTimeOffset(query, "expiresToUtc"),
        };

        BindPaged(query, model);
        return ValueTask.FromResult(model);
    }

    public JobSchedulerLeaseQueryRequest ToRequest() => new()
    {
        SchedulerInstanceId = this.SchedulerInstanceId,
        JobName = this.JobName,
        TriggerName = this.TriggerName,
        Statuses = this.Statuses,
        ExpiresFromUtc = this.ExpiresFromUtc,
        ExpiresToUtc = this.ExpiresToUtc,
        Skip = this.Skip,
        Take = this.Take,
        SortBy = this.SortBy,
        SortDescending = this.SortDescending,
    };
}

internal sealed class JobSchedulerServerQueryModel : JobSchedulerPagedQueryModel
{
    public string SchedulerInstanceId { get; set; }

    public JobSchedulerServerStatus[] Statuses { get; set; } = [];

    public static ValueTask<JobSchedulerServerQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;
        var model = new JobSchedulerServerQueryModel
        {
            SchedulerInstanceId = GetString(query, "schedulerInstanceId"),
            Statuses = ParseEnumValues<JobSchedulerServerStatus>(query, "statuses"),
        };

        BindPaged(query, model);
        return ValueTask.FromResult(model);
    }

    public JobSchedulerServerQueryRequest ToRequest() => new()
    {
        SchedulerInstanceId = this.SchedulerInstanceId,
        Statuses = this.Statuses,
        Skip = this.Skip,
        Take = this.Take,
        SortBy = this.SortBy,
        SortDescending = this.SortDescending,
    };
}

internal sealed class JobSchedulerMetricsQueryModel
{
    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public JobTriggerType? TriggerType { get; set; }

    public JobOccurrenceStatus[] OccurrenceStatuses { get; set; } = [];

    public JobExecutionStatus[] ExecutionStatuses { get; set; } = [];

    public string SchedulerInstanceId { get; set; }

    public DateTimeOffset? DueFrom { get; set; }

    public DateTimeOffset? DueTo { get; set; }

    public DateTimeOffset? CompletedFrom { get; set; }

    public DateTimeOffset? CompletedTo { get; set; }

    public static ValueTask<JobSchedulerMetricsQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;

        return ValueTask.FromResult(new JobSchedulerMetricsQueryModel
        {
            JobName = JobSchedulerPagedQueryModel.GetString(query, "jobName"),
            TriggerName = JobSchedulerPagedQueryModel.GetString(query, "triggerName"),
            TriggerType = JobSchedulerPagedQueryModel.ParseEnum<JobTriggerType>(query, "triggerType"),
            OccurrenceStatuses = JobSchedulerPagedQueryModel.ParseEnumValues<JobOccurrenceStatus>(query, "occurrenceStatuses"),
            ExecutionStatuses = JobSchedulerPagedQueryModel.ParseEnumValues<JobExecutionStatus>(query, "executionStatuses"),
            SchedulerInstanceId = JobSchedulerPagedQueryModel.GetString(query, "schedulerInstanceId"),
            DueFrom = JobSchedulerPagedQueryModel.ParseDateTimeOffset(query, "dueFrom", "fromUtc"),
            DueTo = JobSchedulerPagedQueryModel.ParseDateTimeOffset(query, "dueTo", "toUtc"),
            CompletedFrom = JobSchedulerPagedQueryModel.ParseDateTimeOffset(query, "completedFrom"),
            CompletedTo = JobSchedulerPagedQueryModel.ParseDateTimeOffset(query, "completedTo"),
        });
    }

    public JobSchedulerMetricsRequest ToRequest() => new()
    {
        JobName = this.JobName,
        TriggerName = this.TriggerName,
        TriggerType = this.TriggerType,
        OccurrenceStatuses = this.OccurrenceStatuses,
        ExecutionStatuses = this.ExecutionStatuses,
        SchedulerInstanceId = this.SchedulerInstanceId,
        DueFrom = this.DueFrom,
        DueTo = this.DueTo,
        CompletedFrom = this.CompletedFrom,
        CompletedTo = this.CompletedTo,
    };
}

internal sealed class JobSchedulerTimelineQueryModel
{
    public JobSchedulerTimelineMode Mode { get; set; } = JobSchedulerTimelineMode.Occurrences;

    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public string SchedulerInstanceId { get; set; }

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    public int Bucket { get; set; } = 60;

    public string[] Statuses { get; set; } = [];

    public JobOccurrenceStatus[] OccurrenceStatuses { get; set; } = [];

    public JobExecutionStatus[] ExecutionStatuses { get; set; } = [];

    public static ValueTask<JobSchedulerTimelineQueryModel> BindAsync(HttpContext context, ParameterInfo _)
    {
        var query = context.Request.Query;

        return ValueTask.FromResult(new JobSchedulerTimelineQueryModel
        {
            Mode = JobSchedulerPagedQueryModel.ParseEnum<JobSchedulerTimelineMode>(query, "mode") ?? JobSchedulerTimelineMode.Occurrences,
            JobName = JobSchedulerPagedQueryModel.GetString(query, "jobName"),
            TriggerName = JobSchedulerPagedQueryModel.GetString(query, "triggerName"),
            SchedulerInstanceId = JobSchedulerPagedQueryModel.GetString(query, "schedulerInstanceId"),
            From = JobSchedulerPagedQueryModel.ParseDateTimeOffset(query, "from", "fromUtc"),
            To = JobSchedulerPagedQueryModel.ParseDateTimeOffset(query, "to", "toUtc"),
            Bucket = JobSchedulerPagedQueryModel.ParseInt32(query, "bucket", "bucketMinutes") ?? 60,
            Statuses = JobSchedulerPagedQueryModel.ParseStringValues(query, "statuses"),
            OccurrenceStatuses = JobSchedulerPagedQueryModel.ParseEnumValues<JobOccurrenceStatus>(query, "occurrenceStatuses"),
            ExecutionStatuses = JobSchedulerPagedQueryModel.ParseEnumValues<JobExecutionStatus>(query, "executionStatuses"),
        });
    }

    public JobSchedulerTimelineRequest ToRequest() => new()
    {
        Mode = this.Mode,
        JobName = this.JobName,
        TriggerName = this.TriggerName,
        SchedulerInstanceId = this.SchedulerInstanceId,
        From = this.From,
        To = this.To,
        Bucket = this.Bucket,
        Statuses = this.Statuses,
        OccurrenceStatuses = this.OccurrenceStatuses,
        ExecutionStatuses = this.ExecutionStatuses,
    };
}
